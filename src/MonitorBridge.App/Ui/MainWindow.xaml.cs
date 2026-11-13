using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MonitorBridge.App.Services;
using MonitorBridge.Core;

namespace MonitorBridge.App.Ui;

internal partial class MainWindow : Window
{
    private readonly AppController _c;
    private List<MonitorVm> _vms = new();

    public bool ExitRequested { get; set; }
    public event Action? FirstHide;
    private bool _hidFirst;

    public MainWindow(AppController controller)
    {
        _c = controller;
        InitializeComponent();
        Desk.SelectionChanged += OnSelectionChanged;
        Desk.LayoutEdited += UpdateStatus;
        _c.Changed += UpdateStatus;
        _c.SnapshotsChanged += RebuildVms;
        StartupCheck.IsChecked = _c.Settings.StartWithWindows;
        RebuildVms();
    }

    private void RebuildVms()
    {
        _vms = _c.Snapshots.Select((s, i) => new MonitorVm(s, _c.Draft[i])).ToList();
        Desk.SetItems(_vms);
        UpdateStatus();
    }

    private void OnSelectionChanged(MonitorVm? vm)
    {
        Panel.DataContext = vm;
        Panel.IsEnabled = vm is not null;
        SelTitle.Text = vm?.Title ?? "";
        SelDetail.Text = vm?.Detail ?? "";
        SelSource.Text = vm is null ? "" : "크기 출처: " + vm.SizeSource;
    }

    private void OnFieldUpdated(object? sender, DataTransferEventArgs e)
    {
        Desk.InvalidateVisual();
        if (Desk.Selected is { } vm) SelSource.Text = "크기 출처: " + vm.SizeSource;
        UpdateStatus();
    }

    private void OnFieldKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
        }
    }

    // ---------- 상태 표시 ----------
    private void UpdateStatus()
    {
        var draft = _vms.Select(v => v.Setting).ToList();
        bool dirty = draft.Count > 0 && _c.IsDirty(draft);
        int edges = _vms.Count >= 2 ? Desk.CurrentLayout().GetSharedEdges().Count : 0;
        string bg, fg, title, detail;
        switch (_c.State)
        {
            case RuntimeState.On:
                bg = "#DCFCE7"; fg = "#166534";
                title = "● 보정 켜짐";
                detail = $"모니터 {_c.Snapshots.Count}대 · 통과 가능한 경계 구간 {edges}곳" + (dirty ? "  —  편집한 내용은 '적용'을 눌러야 반영됩니다." : "");
                break;
            case RuntimeState.Suspended:
                bg = "#FEF3C7"; fg = "#92400E";
                title = "▲ 보정 일시 중지 (Windows 기본 동작)";
                detail = _c.Problem ?? "화면 구성을 다시 확인하는 중입니다.";
                break;
            default:
                bg = "#E2E8F0"; fg = "#334155";
                title = "○ 보정 꺼짐 (Windows 기본 동작)";
                detail = _c.Snapshots.Count < 2
                    ? "모니터가 1대로 감지되었습니다. 2대 이상 연결하면 보정할 수 있습니다."
                    : "초록 선 구간이 실제 책상 위 배치와 맞는지 확인한 뒤 '적용'을 누르면 켜집니다.";
                break;
        }
        Banner.Background = (Brush)new BrushConverter().ConvertFromString(bg)!;
        BannerTitle.Foreground = (Brush)new BrushConverter().ConvertFromString(fg)!;
        BannerTitle.Text = title;
        BannerDetail.Text = detail;
        ToggleBtn.Content = _c.State == RuntimeState.On ? "보정 끄기" : "보정 켜기";
        ApplyBtn.IsEnabled = _c.Snapshots.Count >= 2;
        ToggleBtn.IsEnabled = _c.Snapshots.Count >= 2 || _c.State == RuntimeState.On;
    }

    // ---------- 버튼 ----------
    private void OnApply(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        var err = _c.Apply(_vms.Select(v => v.Setting).ToList());
        if (err is not null) MessageBox.Show(this, err, "적용할 수 없습니다", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        if (_c.State == RuntimeState.On) { _c.Disable(); return; }
        OnApply(sender, e);
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show(this,
            "저장된 배치를 지우고 Windows 배치 기준 기본값으로 되돌립니다.\n보정은 꺼지며, 다시 켜려면 '적용'을 눌러야 합니다.\n(Windows 디스플레이 설정은 바뀌지 않습니다.)",
            "기본값으로 되돌리기", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.OK) _c.ResetToDefaults();
    }

    private void OnRedetect(object sender, RoutedEventArgs e) => _c.Refresh();
    private void OnIdentify(object sender, RoutedEventArgs e) => IdentifyWindow.ShowAll(_c.Snapshots);

    private void OnStartupClick(object sender, RoutedEventArgs e)
    {
        try { _c.SetStartWithWindows(StartupCheck.IsChecked == true); }
        catch (Exception ex)
        {
            StartupCheck.IsChecked = !StartupCheck.IsChecked;
            MessageBox.Show(this, "시작 프로그램 설정을 바꾸지 못했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------- 이웃에 맞춤 ----------
    private void OnAlignStart(object s, RoutedEventArgs e) => Align(0);
    private void OnAlignCenter(object s, RoutedEventArgs e) => Align(1);
    private void OnAlignEnd(object s, RoutedEventArgs e) => Align(2);

    private void Align(int mode)
    {
        if (Desk.Selected is not { } sel) return;
        MonitorVm? nb = null; double best = double.MaxValue; bool beside = true;
        foreach (var o in _vms)
        {
            if (ReferenceEquals(o, sel)) continue;
            var a = sel.DeskRect; var b = o.DeskRect;
            double gapX = Math.Max(0, Math.Max(b.X - a.Right, a.X - b.Right));
            double gapY = Math.Max(0, Math.Max(b.Y - a.Bottom, a.Y - b.Bottom));
            double d = gapX + gapY;
            if (d < best) { best = d; nb = o; beside = gapX >= gapY; }
        }
        if (nb is null) return;
        var s = sel.DeskRect; var n = nb.DeskRect;
        if (beside) sel.YMm = Math.Round(mode == 0 ? n.Y : mode == 1 ? n.Y + n.Height / 2 - s.Height / 2 : n.Bottom - s.Height, 1);
        else sel.XMm = Math.Round(mode == 0 ? n.X : mode == 1 ? n.X + n.Width / 2 - s.Width / 2 : n.Right - s.Width, 1);
        Desk.InvalidateVisual();
        UpdateStatus();
    }

    // ---------- 창 닫기 → 트레이로 ----------
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ExitRequested) return;
        e.Cancel = true;
        Hide();
        if (!_hidFirst) { _hidFirst = true; FirstHide?.Invoke(); }
    }
}
