using System.Windows;
using MonitorBridge.App.Services;
using MonitorBridge.App.Ui;
using WinForms = System.Windows.Forms;

namespace MonitorBridge.App;

public partial class App : Application
{
    private const string MutexName = @"Local\MonitorBridge.SingleInstance";
    private const string ShowEventName = @"Local\MonitorBridge.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private AppController? _controller;
    private MainWindow? _window;
    private WinForms.NotifyIcon? _tray;
    private WinForms.ToolStripMenuItem? _toggleItem;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool first);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!first)
        {
            // 이미 실행 중이면 기존 창을 띄우고 종료
            _showEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(Loc.T("app.error", ex.Exception.Message), "MonitorBridge", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        bool minimized = e.Args.Contains("--minimized");
        _controller = new AppController();
        // 저장된 언어가 없으면 Windows 표시 언어를 따르고, 지원하지 않는 언어는 영어로 시작한다(창에서 변경 가능).
        Loc.Set(Loc.Resolve(_controller.Settings.Language));
        if (e.Args.Contains("--process-injected")) _controller.ProcessInjected = true; // 자동 검증용
        int ti = Array.IndexOf(e.Args, "--trace");
        if (ti >= 0 && ti + 1 < e.Args.Length) _controller.EnableTrace(e.Args[ti + 1]);

        _controller.Start();
        CreateWindow();
        CreateTray();
        _controller.Changed += UpdateTray;
        UpdateTray();

        var waiter = new Thread(() =>
        {
            while (_showEvent.WaitOne()) { if (_exiting) return; Dispatcher.BeginInvoke(ShowWindow); }
        }) { IsBackground = true };
        waiter.Start();

        if (!minimized) ShowWindow();
    }

    private void CreateWindow()
    {
        _window = new MainWindow(_controller!);
        _window.FirstHide += () => _tray?.ShowBalloonTip(3000, "MonitorBridge", Loc.T("tray.hint"), WinForms.ToolTipIcon.Info);
        _window.LanguageChosen += OnLanguageChosen;
    }

    private void OnLanguageChosen(string lang)
    {
        if (_controller is null) return;
        _controller.Settings.Language = lang;
        try { SettingsStore.Save(_controller.Settings); } catch { }
        Loc.Set(lang);
        var old = _window;
        // 현재 이벤트 처리가 끝난 뒤 창을 다시 만든다(문자열은 창 생성 시점에 정해짐).
        Dispatcher.BeginInvoke(() =>
        {
            CreateWindow();
            RebuildTrayMenu();
            _controller.Refresh();
            if (old is not null) { old.ExitRequested = true; old.Close(); }
            ShowWindow();
        });
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _controller?.Refresh();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private WinForms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Loc.T("tray.open"), null, (_, _) => ShowWindow());
        _toggleItem = new WinForms.ToolStripMenuItem(Loc.T("tray.toggle"));
        _toggleItem.Click += (_, _) => ToggleFromTray();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Loc.T("tray.exit"), null, (_, _) => ExitApp());
        return menu;
    }

    private void RebuildTrayMenu()
    {
        if (_tray is null) return;
        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildTrayMenu();
        old?.Dispose();
        UpdateTray();
    }

    private void CreateTray()
    {
        var menu = BuildTrayMenu();

        var icon = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute))!.Stream;
        _tray = new WinForms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(icon),
            Visible = true,
            Text = "MonitorBridge",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    private void ToggleFromTray()
    {
        if (_controller is null) return;
        if (_controller.State == RuntimeState.On) { _controller.Disable(); return; }
        // 켜기: 저장된 설정이 현재 모니터와 맞을 때만 바로 켠다. 아니면 창을 열어 확인하게 한다.
        if (_controller.SavedMatchesCurrent && _controller.Settings.Monitors.Count >= 2)
        {
            var err = _controller.Apply(_controller.Draft);
            if (err is null) return;
        }
        ShowWindow();
    }

    private void UpdateTray()
    {
        if (_tray is null || _controller is null) return;
        string state = _controller.State switch { RuntimeState.On => Loc.T("state.on"), RuntimeState.Suspended => Loc.T("state.suspended"), _ => Loc.T("state.off") };
        _tray.Text = Loc.T("tray.text", state);   // 63자 제한 내
        if (_toggleItem is not null) _toggleItem.Text = _controller.State == RuntimeState.On ? Loc.T("tray.on") : Loc.T("tray.off");
    }

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        _controller?.Shutdown();     // 훅 해제 → Windows 기본 동작
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        if (_window is not null) { _window.ExitRequested = true; _window.Close(); }
        _showEvent?.Set();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Shutdown();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
