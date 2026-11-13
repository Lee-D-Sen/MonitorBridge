using System.Windows.Threading;
using Microsoft.Win32;
using MonitorBridge.App.Native;
using MonitorBridge.Core;

namespace MonitorBridge.App.Services;

public enum RuntimeState { Off, On, Suspended }

/// <summary>
/// 앱의 상태와 동작을 총괄한다: 모니터 감지, 설정 저장, 보정 켜기/끄기, 구성 변경 감시.
/// 모든 공개 멤버는 UI 스레드에서만 호출한다.
/// </summary>
internal sealed class AppController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _debounce;
    private readonly MouseCorrector _corrector = new();

    public AppSettings Settings { get; private set; }
    public IReadOnlyList<MonitorSnapshot> Snapshots { get; private set; } = Array.Empty<MonitorSnapshot>();
    /// <summary>GUI에서 편집 중인 설정(저장 전 초안). Snapshots와 같은 순서.</summary>
    public List<MonitorSetting> Draft { get; private set; } = new();
    public RuntimeState State { get; private set; } = RuntimeState.Off;
    /// <summary>사용자에게 보여줄 경고/오류(없으면 null).</summary>
    public string? Problem { get; private set; }
    /// <summary>저장된 설정이 현재 모니터와 안전하게 맞는지.</summary>
    public bool SavedMatchesCurrent { get; private set; }

    public event Action? Changed;          // 상태/문제/스냅샷이 바뀜
    public event Action? SnapshotsChanged; // 모니터 구성이 다시 감지됨(GUI가 초안을 다시 만들어야 함)

    public bool ProcessInjected { get => _corrector.ProcessInjected; set => _corrector.ProcessInjected = value; }
    public void EnableTrace(string path) => _corrector.EnableTrace(path);

    public AppController()
    {
        Settings = SettingsStore.Load();
        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(900) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        SystemEvents.DisplaySettingsChanged += OnDisplayEvent;
        SystemEvents.PowerModeChanged += OnPowerMode;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public void Start()
    {
        Refresh();
    }

    private void OnDisplayEvent(object? s, EventArgs e) => _dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
    private void OnPowerMode(object? s, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Interval = TimeSpan.FromMilliseconds(2500); _debounce.Start(); });
        else if (e.Mode == PowerModes.Suspend) _dispatcher.BeginInvoke(SuspendForReconfig);
    }
    private void OnSessionSwitch(object? s, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            _dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
    }

    /// <summary>화면 구성이 바뀌는 도중에는 보정을 멈춰 두었다가 재감지 후 안전할 때만 다시 켠다.</summary>
    private void SuspendForReconfig()
    {
        if (State == RuntimeState.On)
        {
            _corrector.Stop();
            State = RuntimeState.Suspended;
            Changed?.Invoke();
        }
    }

    /// <summary>모니터를 다시 감지하고, 저장된 설정과 비교해 보정 상태를 정한다.</summary>
    public void Refresh()
    {
        _debounce.Interval = TimeSpan.FromMilliseconds(900);
        Snapshots = MonitorEnumerator.Enumerate();
        var match = MonitorMatcher.Match(Snapshots, Settings.Monitors);
        SavedMatchesCurrent = match.Ok;

        if (match.Ok)
        {
            // 짝지어진 저장 설정을 초안으로 사용
            Draft = match.Pairs.Select(p => Clone(p.Setting, p.Snap)).ToList();
        }
        else
        {
            Draft = LayoutBuilder.DefaultSettings(Snapshots);
        }
        SnapshotsChanged?.Invoke();
        Evaluate(matchReason: match.Reason);
    }

    /// <summary>의도(Settings.Enabled)와 현재 구성으로 훅을 켜거나 끈다.</summary>
    private void Evaluate(string? matchReason)
    {
        Problem = null;
        if (!Settings.Enabled)
        {
            _corrector.Stop();
            State = RuntimeState.Off;
            Changed?.Invoke();
            return;
        }

        string? problem = null;
        DeskLayout? layout = null;
        if (Snapshots.Count < 2) problem = "모니터가 1대뿐이어서 보정이 필요 없습니다. 보정을 일시 중지했습니다.";
        else if (!SavedMatchesCurrent) problem = (matchReason ?? "저장된 모니터와 현재 모니터가 다릅니다.") + " 안전을 위해 보정을 껐습니다. 배치를 확인하고 '적용'을 눌러 다시 켜세요.";
        else
        {
            var match = MonitorMatcher.Match(Snapshots, Settings.Monitors);
            layout = LayoutBuilder.Build(match.Pairs);
            var overlaps = layout.FindOverlaps().ToList();
            if (overlaps.Count > 0) { problem = "모니터 배치가 서로 겹쳐 있습니다(회전 또는 해상도 변경 후일 수 있음). 배치를 조정하고 '적용'을 눌러 주세요."; layout = null; }
        }

        if (layout is null)
        {
            _corrector.Stop();
            State = RuntimeState.Suspended;
            Problem = problem;
        }
        else
        {
            try { _corrector.Start(layout); State = RuntimeState.On; }
            catch (Exception ex) { _corrector.Stop(); State = RuntimeState.Suspended; Problem = ex.Message; }
        }
        Changed?.Invoke();
    }

    /// <summary>초안을 검증해 저장하고 보정을 켠다. 실패하면 이유를 반환한다.</summary>
    public string? Apply(IReadOnlyList<MonitorSetting> draft)
    {
        if (Snapshots.Count < 2) return "모니터가 2대 이상일 때 보정할 수 있습니다.";
        foreach (var d in draft)
            if (!(d.NativeWidthMm >= 100 && d.NativeHeightMm >= 60 && d.NativeWidthMm <= 3000 && d.NativeHeightMm <= 3000))
                return "화면 크기가 올바르지 않습니다(가로 100~3000mm, 세로 60~3000mm).";
        var pairs = Snapshots.Select((s, i) => (s, draft[i])).ToList();
        var layout = LayoutBuilder.Build(pairs);
        var overlaps = layout.FindOverlaps().ToList();
        if (overlaps.Count > 0) return "모니터가 책상 위에서 서로 겹쳐 있습니다. 겹치지 않게 배치해 주세요.";

        Settings.Monitors = draft.Select((d, i) => Clone(d, Snapshots[i])).ToList();
        Settings.Enabled = true;
        SettingsStore.Save(Settings);
        Refresh();
        return null;
    }

    /// <summary>보정을 끄고(저장) Windows 기본 동작으로 즉시 되돌린다.</summary>
    public void Disable()
    {
        _corrector.Stop();
        Settings.Enabled = false;
        SettingsStore.Save(Settings);
        Evaluate(null);
    }

    public void ResetToDefaults()
    {
        _corrector.Stop();
        SettingsStore.Delete();
        Settings = new AppSettings { StartWithWindows = Settings.StartWithWindows };
        SettingsStore.Save(Settings);
        Refresh();
    }

    public void SetStartWithWindows(bool on)
    {
        StartupRegistration.Set(on);
        Settings.StartWithWindows = on;
        SettingsStore.Save(Settings);
    }

    /// <summary>마지막으로 저장된 설정과 초안이 다른지.</summary>
    public bool IsDirty(IReadOnlyList<MonitorSetting> draft)
    {
        if (!SavedMatchesCurrent) return true;
        var match = MonitorMatcher.Match(Snapshots, Settings.Monitors);
        for (int i = 0; i < draft.Count; i++)
        {
            var s = match.Pairs[i].Setting; var d = draft[i];
            if (Math.Abs(s.DeskXmm - d.DeskXmm) > 0.01 || Math.Abs(s.DeskYmm - d.DeskYmm) > 0.01
                || Math.Abs(s.NativeWidthMm - d.NativeWidthMm) > 0.01 || Math.Abs(s.NativeHeightMm - d.NativeHeightMm) > 0.01) return true;
        }
        return false;
    }

    private static MonitorSetting Clone(MonitorSetting s, MonitorSnapshot snap) => new()
    {
        Key = snap.Key, ModelId = snap.ModelId, UniqueId = snap.UniqueId,
        DeskXmm = s.DeskXmm, DeskYmm = s.DeskYmm,
        NativeWidthMm = s.NativeWidthMm, NativeHeightMm = s.NativeHeightMm, SizeUserDefined = s.SizeUserDefined,
    };

    public void Shutdown()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayEvent;
        SystemEvents.PowerModeChanged -= OnPowerMode;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _corrector.Stop(); // 훅 해제 → Windows 기본 동작
    }

    public void Dispose() => Shutdown();
}
