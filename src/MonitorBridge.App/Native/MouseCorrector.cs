using System.Diagnostics;
using System.Runtime.InteropServices;
using MonitorBridge.Core;

namespace MonitorBridge.App.Native;

/// <summary>
/// 저수준 마우스 훅(WH_MOUSE_LL) + Raw Input으로 모니터 경계를 넘는 순간에만 개입한다.
/// - 같은 모니터 안의 이동은 전혀 건드리지 않는다.
/// - Windows가 경계를 넘겨 주는 경우(훅 pt가 다른 모니터에 있음): 책상 배치 기준 위치로 옮기거나, 연결되지 않았으면 현재 모니터 안으로 되돌린다.
/// - Windows가 막는 경우(커서가 가장자리에 고정됨): Raw Input의 상대 이동량으로 "밀고 있음"을 감지해 연결된 구간이면 넘겨 준다.
/// 훅과 Raw Input은 같은 전용 스레드의 메시지 루프에서 직렬로 처리되므로 서로 경쟁하지 않는다.
/// Stop()하면 훅이 즉시 해제되어 Windows 기본 동작으로 돌아간다.
/// </summary>
internal sealed class MouseCorrector : IDisposable
{
    private const string WindowClass = "MonitorBridgeRawInputSink";

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _active;
    private volatile DeskLayout? _layout;
    private IntPtr _hook;
    private IntPtr _hwnd;
    private NativeMethods.HookProc? _hookProc;
    private NativeMethods.WndProc? _wndProc;
    private PxPoint _last;
    private bool _haveLast;
    private TextWriter? _trace;

    /// <summary>true면 SendInput 등 주입된 이동도 처리한다(자동 검증용). 기본은 false: 다른 프로그램의 순간이동은 건드리지 않는다.</summary>
    public bool ProcessInjected { get; set; }
    public bool IsRunning => _thread is { IsAlive: true } && _active;

    public long CrossingCount { get; private set; }

    public void EnableTrace(string path) => _trace = TextWriter.Synchronized(new StreamWriter(path, true) { AutoFlush = true });

    public void Start(DeskLayout layout)
    {
        if (IsRunning) { Update(layout); return; }
        _layout = layout;
        _haveLast = false;
        _active = true;
        var ready = new ManualResetEventSlim(false);
        bool ok = false;
        _thread = new Thread(() => ThreadMain(ready, v => ok = v)) { IsBackground = true, Name = "MonitorBridge.Hook" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(3000);
        if (!ok) { _active = false; throw new InvalidOperationException("마우스 훅을 설치하지 못했습니다."); }
    }

    public void Update(DeskLayout layout)
    {
        _layout = layout;
        _haveLast = false; // 배치가 바뀌면 이전 위치 기억을 버린다
    }

    public void Stop()
    {
        _active = false;               // 즉시 모든 이벤트를 통과시킴
        var t = _thread;
        if (t is null) return;
        if (_threadId != 0) NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        t.Join(2000);
        _thread = null;
        _threadId = 0;
    }

    public void Dispose() => Stop();

    private void ThreadMain(ManualResetEventSlim ready, Action<bool> setOk)
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _hookProc = HookCallback;
        _wndProc = WndProc;
        var inst = NativeMethods.GetModuleHandle(null);
        try
        {
            _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, inst, 0);
            if (_hook == IntPtr.Zero) { setOk(false); return; }

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(), lpfnWndProc = _wndProc,
                hInstance = inst, lpszClassName = WindowClass,
            };
            NativeMethods.RegisterClassEx(ref wc);
            _hwnd = NativeMethods.CreateWindowEx(0, WindowClass, null, 0, 0, 0, 0, 0, NativeMethods.HWND_MESSAGE, IntPtr.Zero, inst, IntPtr.Zero);
            if (_hwnd != IntPtr.Zero)
            {
                var rid = new[] { new NativeMethods.RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = NativeMethods.RIDEV_INPUTSINK, hwndTarget = _hwnd } };
                NativeMethods.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>());
            }
            setOk(true);
            ready.Set();

            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (_hook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            if (_hwnd != IntPtr.Zero) { NativeMethods.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
            NativeMethods.UnregisterClass(WindowClass, inst);
            ready.Set();
        }
    }

    // ---------- 저수준 마우스 훅 ----------
    private IntPtr HookCallback(int nCode, UIntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _active && (int)wParam == NativeMethods.WM_MOUSEMOVE)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                if (OnHookMove(info)) return (IntPtr)1; // 원래 이동을 삼키고 우리가 계산한 위치로 이동했음
            }
            catch (Exception ex) { _trace?.WriteLine("hook error: " + ex); }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool OnHookMove(NativeMethods.MSLLHOOKSTRUCT info)
    {
        var p = new PxPoint(info.pt.X, info.pt.Y);
        var layout = _layout;
        bool injected = (info.flags & (NativeMethods.LLMHF_INJECTED | NativeMethods.LLMHF_LOWER_IL_INJECTED)) != 0;

        if (layout is null || (injected && !ProcessInjected) || IsCursorClipped())
        {
            _last = p; _haveLast = true;
            return false;
        }
        if (!_haveLast) { _last = p; _haveLast = true; return false; }

        var r = CursorResolver.Resolve(layout, _last, p);
        _trace?.WriteLine($"hook last={_last} pt={p} -> {r.Kind} {r.Point} {r.TargetId}");
        switch (r.Kind)
        {
            case ResolveKind.Move:
                CrossingCount++;
                return MoveCursor(r.Point);
            case ResolveKind.Clamp when r.Point != p:
                return MoveCursor(r.Point);
            default:
                _last = p;
                return false;
        }
    }

    private bool MoveCursor(PxPoint target)
    {
        _last = target;
        NativeMethods.SetCursorPos(target.X, target.Y);
        return true;
    }

    // ---------- Raw Input: Windows가 막아 커서가 가장자리에 멈춘 경우 ----------
    private IntPtr WndProc(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_INPUT && _active)
        {
            try { OnRawInput(lParam); } catch (Exception ex) { _trace?.WriteLine("raw error: " + ex); }
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void OnRawInput(IntPtr hRaw)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<NativeMethods.RAWINPUTHEADER>();
        NativeMethods.GetRawInputData(hRaw, NativeMethods.RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0 || size > 1024) return;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (NativeMethods.GetRawInputData(hRaw, NativeMethods.RID_INPUT, buf, ref size, headerSize) != size) return;
            uint type = (uint)Marshal.ReadInt32(buf, 0);
            if (type != NativeMethods.RIM_TYPEMOUSE) return;
            int off = (int)headerSize;
            ushort flags = (ushort)Marshal.ReadInt16(buf, off);
            if ((flags & NativeMethods.MOUSE_MOVE_ABSOLUTE) != 0) return;
            int dx = Marshal.ReadInt32(buf, off + 12);
            int dy = Marshal.ReadInt32(buf, off + 16);
            if (dx == 0 && dy == 0) return;
            HandleEdgePush(dx, dy);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private void HandleEdgePush(int dx, int dy)
    {
        var layout = _layout;
        if (layout is null || IsCursorClipped()) return;
        if (!NativeMethods.GetCursorPos(out var c)) return;
        var cur = new PxPoint(c.X, c.Y);
        var a = layout.FindByPixel(cur);
        if (a is null) return;

        bool pushOut = (dx > 0 && cur.X >= a.Px.Right - 1) || (dx < 0 && cur.X <= a.Px.Left)
                    || (dy > 0 && cur.Y >= a.Px.Bottom - 1) || (dy < 0 && cur.Y <= a.Px.Top);
        if (!pushOut) return;

        var to = new PxPoint(cur.X + dx, cur.Y + dy);
        // Windows가 어차피 넘겨 주는 위치(실제 픽셀 배치상 이웃 모니터 안)라면 훅이 처리한다.
        var other = layout.FindByPixel(to);
        if (other is not null && !ReferenceEquals(other, a)) return;

        var r = CursorResolver.Resolve(layout, cur, to);
        _trace?.WriteLine($"raw cur={cur} d=({dx},{dy}) -> {r.Kind} {r.Point} {r.TargetId}");
        if (r.Kind == ResolveKind.Move)
        {
            CrossingCount++;
            MoveCursor(r.Point);
        }
    }

    private static bool IsCursorClipped()
    {
        if (!NativeMethods.GetClipCursor(out var clip)) return false;
        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return clip.Left > vx || clip.Top > vy || clip.Right < vx + vw || clip.Bottom < vy + vh;
    }
}
