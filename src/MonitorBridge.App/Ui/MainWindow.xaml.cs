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
    /// <summary>사용자가 언어를 바꿨다. App이 창을 다시 만든다.</summary>
    public event Action<string>? LanguageChosen;
    private bool _langReady;
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
        if (AppPackage.IsPackaged) StartupCheck.Visibility = Visibility.Collapsed;
        foreach (var (code, name) in Loc.Languages)
        {
            var item = new ComboBoxItem { Content = name, Tag = code };
            LangBox.Items.Add(item);
            if (code == Loc.Lang) LangBox.SelectedItem = item;
        }
        _langReady = true;
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
        SelSource.Text = vm is null ? "" : Loc.T("sel.source", vm.SizeSource);
    }

    private void OnFieldUpdated(object? sender, DataTransferEventArgs e)
    {
        Desk.InvalidateVisual();
        if (Desk.Selected is { } vm) SelSource.Text = Loc.T("sel.source", vm.SizeSource);
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
                title = Loc.T("banner.on");
                detail = Loc.T("banner.on.detail", _c.Snapshots.Count, edges) + (dirty ? Loc.T("banner.on.dirty") : "");
                break;
            case RuntimeState.Suspended:
                bg = "#FEF3C7"; fg = "#92400E";
                title = Loc.T("banner.susp");
                detail = _c.Problem ?? Loc.T("banner.susp.detail");
                break;
            default:
                bg = "#E2E8F0"; fg = "#334155";
                title = Loc.T("banner.off");
                detail = _c.Snapshots.Count < 2
                    ? Loc.T("banner.off.one")
                    : Loc.T("banner.off.many");
                break;
        }
        Banner.Background = (Brush)new BrushConverter().ConvertFromString(bg)!;
        BannerTitle.Foreground = (Brush)new BrushConverter().ConvertFromString(fg)!;
        BannerTitle.Text = title;
        BannerDetail.Text = detail;
        ToggleBtn.Content = _c.State == RuntimeState.On ? Loc.T("btn.off") : Loc.T("btn.on");
        ApplyBtn.IsEnabled = _c.Snapshots.Count >= 2;
        ToggleBtn.IsEnabled = _c.Snapshots.Count >= 2 || _c.State == RuntimeState.On;
    }

    // ---------- 버튼 ----------
    private void OnApply(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        var err = _c.Apply(_vms.Select(v => v.Setting).ToList());
        if (err is not null) MessageBox.Show(this, err, Loc.T("dlg.apply.fail"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        if (_c.State == RuntimeState.On) { _c.Disable(); return; }
        OnApply(sender, e);
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show(this,
            Loc.T("dlg.reset.text"),
            Loc.T("dlg.reset.title"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
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
            MessageBox.Show(this, Loc.T("dlg.startup.fail", ex.Message), Loc.T("dlg.error"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_langReady || LangBox.SelectedItem is not ComboBoxItem { Tag: string lang } || lang == Loc.Lang) return;
        LanguageChosen?.Invoke(lang);
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
