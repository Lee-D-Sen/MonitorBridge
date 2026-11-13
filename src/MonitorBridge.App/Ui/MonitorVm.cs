using System.ComponentModel;
using MonitorBridge.App.Services;
using MonitorBridge.Core;

namespace MonitorBridge.App.Ui;

/// <summary>GUI에서 편집하는 모니터 하나. 길이 단위는 모두 mm(실제 크기)이며 화면 픽셀/DIP와 무관하다.</summary>
internal sealed class MonitorVm : INotifyPropertyChanged
{
    public MonitorSnapshot Snap { get; }
    public MonitorSetting Setting { get; }

    public MonitorVm(MonitorSnapshot snap, MonitorSetting setting) { Snap = snap; Setting = setting; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(params string[] names) { foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n)); }
    public void RaiseAll() => Raise(nameof(XMm), nameof(YMm), nameof(WidthMm), nameof(HeightMm), nameof(DiagonalInch), nameof(SizeSource), nameof(Title));

    public int Number => Snap.DisplayNumber;
    public string Title => Loc.T("vm.title", Snap.DisplayNumber, Snap.FriendlyName);
    public string Detail => Loc.T("vm.detail", Snap.Bounds.Width, Snap.Bounds.Height, Snap.ScalePercent, OrientationText);
    public string OrientationText => Snap.Rotation switch
    {
        Rotation.Deg0 => Loc.T("vm.landscape"), Rotation.Deg90 => Loc.T("vm.portrait90"), Rotation.Deg180 => Loc.T("vm.landscape180"), _ => Loc.T("vm.portrait270"),
    };
    public string SizeSource => Setting.SizeUserDefined ? Loc.T("vm.src.user") : Loc.T("vm.src.edid");

    // 사용자가 보는 방향(회전 적용) 기준의 실제 크기
    public double WidthMm
    {
        get => LayoutBuilder.EffectiveSizeMm(Setting, Snap.Rotation).W;
        set => SetEffective(value, HeightMm);
    }
    public double HeightMm
    {
        get => LayoutBuilder.EffectiveSizeMm(Setting, Snap.Rotation).H;
        set => SetEffective(WidthMm, value);
    }

    private void SetEffective(double w, double h)
    {
        if (!(w > 0) || !(h > 0) || double.IsInfinity(w) || double.IsInfinity(h)) return;
        if (Snap.Rotation.IsPortrait()) { Setting.NativeWidthMm = h; Setting.NativeHeightMm = w; }
        else { Setting.NativeWidthMm = w; Setting.NativeHeightMm = h; }
        Setting.SizeUserDefined = true;
        RaiseAll();
    }

    /// <summary>대각선(인치). 설정하면 현재 화면 픽셀 비율을 유지한 채 가로·세로를 다시 계산한다.</summary>
    public double DiagonalInch
    {
        get => Math.Sqrt(WidthMm * WidthMm + HeightMm * HeightMm) / 25.4;
        set
        {
            if (!(value > 3) || value > 150) return;
            var (nw, nh) = Snap.NativePixels;
            var (w, h) = LayoutBuilder.EstimateNativeSizeMm(nw, nh, value);
            Setting.NativeWidthMm = w; Setting.NativeHeightMm = h;
            Setting.SizeUserDefined = true;
            RaiseAll();
        }
    }

    public double XMm
    {
        get => Setting.DeskXmm;
        set { if (double.IsFinite(value)) { Setting.DeskXmm = value; RaiseAll(); } }
    }
    public double YMm
    {
        get => Setting.DeskYmm;
        set { if (double.IsFinite(value)) { Setting.DeskYmm = value; RaiseAll(); } }
    }

    public MmRect DeskRect => new(Setting.DeskXmm, Setting.DeskYmm, WidthMm, HeightMm);

    public DeskMonitor ToDeskMonitor() => new(Snap.Key, Snap.Bounds, DeskRect);
}
