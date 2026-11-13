using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MonitorBridge.Core;

namespace MonitorBridge.App.Ui;

/// <summary>
/// 책상 위 모니터 배치도. 좌표 단위는 mm(책상), 그리는 단위는 WPF 논리 좌표(DIP)이며
/// 모니터의 Win32 물리 픽셀과는 섞이지 않는다. 드래그로 배치를 수정하고, 통과 가능한 경계 구간을 초록선으로 표시한다.
/// </summary>
internal sealed class DeskCanvas : FrameworkElement
{
    private List<MonitorVm> _items = new();
    private MonitorVm? _selected;
    private MonitorVm? _dragging;
    private Vector _dragOffsetMm;
    private (double scale, double ox, double oy)? _frozen;
    private Point _lastMouseDip;

    public event Action<MonitorVm?>? SelectionChanged;
    public event Action? LayoutEdited;

    public MonitorVm? Selected
    {
        get => _selected;
        set { if (!ReferenceEquals(_selected, value)) { _selected = value; InvalidateVisual(); SelectionChanged?.Invoke(value); } }
    }

    public void SetItems(IEnumerable<MonitorVm> items)
    {
        _items = items.ToList();
        _selected = _items.FirstOrDefault(i => ReferenceEquals(i, _selected)) ?? _items.FirstOrDefault();
        SelectionChanged?.Invoke(_selected);
        InvalidateVisual();
    }

    public DeskLayout CurrentLayout() => new(_items.Select(i => i.ToDeskMonitor()));

    private (double scale, double ox, double oy) Transform()
    {
        if (_frozen is { } f) return f;
        if (_items.Count == 0) return (1, 0, 0);
        double minX = _items.Min(i => i.DeskRect.X), minY = _items.Min(i => i.DeskRect.Y);
        double maxX = _items.Max(i => i.DeskRect.Right), maxY = _items.Max(i => i.DeskRect.Bottom);
        double padMm = Math.Max(maxX - minX, maxY - minY) * 0.15 + 40;
        minX -= padMm; minY -= padMm; maxX += padMm; maxY += padMm;
        double s = Math.Min(ActualWidth / (maxX - minX), ActualHeight / (maxY - minY));
        if (!(s > 0) || double.IsInfinity(s)) s = 1;
        double ox = (ActualWidth - (maxX - minX) * s) / 2 - minX * s;
        double oy = (ActualHeight - (maxY - minY) * s) / 2 - minY * s;
        return (s, ox, oy);
    }

    private Rect ToDip(MmRect r, (double scale, double ox, double oy) t) =>
        new(r.X * t.scale + t.ox, r.Y * t.scale + t.oy, r.Width * t.scale, r.Height * t.scale);

    protected override void OnRender(DrawingContext dc)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(Brush("#F8FAFC"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        var t = Transform();

        // 10cm 격자
        var gridPen = new Pen(Brush("#E2E8F0"), 1);
        double step = 100 * t.scale;
        if (step > 12)
        {
            double startX = t.ox % step, startY = t.oy % step;
            for (double x = startX; x < ActualWidth; x += step) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
            for (double y = startY; y < ActualHeight; y += step) dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
        }

        var layout = CurrentLayout();
        var overlapIds = layout.FindOverlaps().SelectMany(p => new[] { p.A, p.B }).ToHashSet();

        foreach (var m in _items)
        {
            var r = ToDip(m.DeskRect, t);
            bool sel = ReferenceEquals(m, _selected);
            bool bad = overlapIds.Contains(m.Snap.Key);
            var fill = bad ? Brush("#FEE2E2") : sel ? Brush("#DBEAFE") : Brush("#EFF6FF");
            var pen = new Pen(bad ? Brush("#DC2626") : sel ? Brush("#2563EB") : Brush("#64748B"), sel ? 3 : 1.5);
            dc.DrawRoundedRectangle(fill, pen, r, 4, 4);

            double numSize = Math.Max(14, Math.Min(r.Height * 0.45, r.Width * 0.35));
            DrawText(dc, m.Number.ToString(CultureInfo.InvariantCulture), numSize, Brush("#1E3A8A"), true, r, 0.42, dpi);
            double small = Math.Max(9, Math.Min(13, r.Width / 16));
            DrawText(dc, $"{m.Snap.Bounds.Width}×{m.Snap.Bounds.Height} · {m.Snap.ScalePercent}%", small, Brush("#334155"), false, r, 0.72, dpi);
            DrawText(dc, $"{m.WidthMm:0}×{m.HeightMm:0} mm · {m.DiagonalInch:0.#}\"", small, Brush("#475569"), false, r, 0.84, dpi);
        }

        // 통과 가능한 경계 구간(초록)
        var edgePen = new Pen(Brush("#16A34A"), 5) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        foreach (var e in layout.GetSharedEdges())
        {
            bool vertical = e.Side is EdgeSide.Left or EdgeSide.Right;
            Point a = vertical ? new Point(e.FixedMm * t.scale + t.ox, e.StartMm * t.scale + t.oy)
                               : new Point(e.StartMm * t.scale + t.ox, e.FixedMm * t.scale + t.oy);
            Point b = vertical ? new Point(a.X, e.EndMm * t.scale + t.oy) : new Point(e.EndMm * t.scale + t.ox, a.Y);
            dc.DrawLine(edgePen, a, b);
        }

        // 범례
        var legend = new FormattedText("■ 초록 선: 커서가 넘나드는 구간 (그 밖의 경계는 막힘)", CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brush("#15803D"), dpi);
        dc.DrawText(legend, new Point(10, ActualHeight - 22));
    }

    private static void DrawText(DrawingContext dc, string text, double size, Brush brush, bool bold, Rect r, double yFrac, double dpi)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, brush, dpi)
        { MaxTextWidth = Math.Max(10, r.Width - 8), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
        dc.DrawText(ft, new Point(r.X + 4, r.Y + r.Height * yFrac - ft.Height / 2));
    }

    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

    // ---------- 마우스 편집 ----------
    private MonitorVm? HitTest(Point p)
    {
        var t = Transform();
        // 위에 그려진(나중) 것 우선
        for (int i = _items.Count - 1; i >= 0; i--)
            if (ToDip(_items[i].DeskRect, t).Contains(p)) return _items[i];
        return null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var p = e.GetPosition(this);
        var hit = HitTest(p);
        Selected = hit ?? _selected;
        if (hit is null) return;
        _frozen = Transform(); // 드래그 중 화면이 흔들리지 않게 고정
        var t = _frozen.Value;
        _dragging = hit;
        _dragOffsetMm = new Vector((p.X - t.ox) / t.scale - hit.XMm, (p.Y - t.oy) / t.scale - hit.YMm);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _lastMouseDip = e.GetPosition(this);
        if (_dragging is null || _frozen is null) return;
        var t = _frozen.Value;
        double nx = (_lastMouseDip.X - t.ox) / t.scale - _dragOffsetMm.X;
        double ny = (_lastMouseDip.Y - t.oy) / t.scale - _dragOffsetMm.Y;
        bool noSnap = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        if (!noSnap) (nx, ny) = Snap(_dragging, nx, ny, 10 / t.scale);

        // 겹치게 되는 위치는 받아들이지 않는다(마지막 정상 위치 유지)
        var candidate = new MmRect(nx, ny, _dragging.WidthMm, _dragging.HeightMm);
        foreach (var o in _items)
            if (!ReferenceEquals(o, _dragging) && DeskLayout.Overlaps(candidate, o.DeskRect)) return;

        _dragging.Setting.DeskXmm = Math.Round(nx, 1);
        _dragging.Setting.DeskYmm = Math.Round(ny, 1);
        _dragging.RaiseAll();
        InvalidateVisual();
        LayoutEdited?.Invoke();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging is null) return;
        _dragging = null; _frozen = null;
        ReleaseMouseCapture();
        InvalidateVisual();
    }

    /// <summary>다른 모니터의 가장자리/윗선/아랫선/중앙선에 달라붙게 한다(thr: 허용 거리 mm). Alt를 누르면 해제.</summary>
    private (double x, double y) Snap(MonitorVm m, double x, double y, double thr)
    {
        double w = m.WidthMm, h = m.HeightMm;
        double bestDx = double.MaxValue, bestDy = double.MaxValue, sx = x, sy = y;
        foreach (var o in _items)
        {
            if (ReferenceEquals(o, m)) continue;
            var r = o.DeskRect;
            // x 후보: 붙이기(좌/우) + 같은 선 맞춤
            foreach (var c in new[] { r.Right, r.X - w, r.X, r.Right - w, r.X + r.Width / 2 - w / 2 })
                if (Math.Abs(c - x) < Math.Abs(bestDx) && Math.Abs(c - x) <= thr) { bestDx = c - x; sx = c; }
            foreach (var c in new[] { r.Bottom, r.Y - h, r.Y, r.Bottom - h, r.Y + r.Height / 2 - h / 2 })
                if (Math.Abs(c - y) < Math.Abs(bestDy) && Math.Abs(c - y) <= thr) { bestDy = c - y; sy = c; }
        }
        return (sx, sy);
    }
}
