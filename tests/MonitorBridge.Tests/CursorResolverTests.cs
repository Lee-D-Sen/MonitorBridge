using MonitorBridge.Core;
using Xunit;

namespace MonitorBridge.Tests;

public class CursorResolverTests
{
    // 27인치 4K (16:9) ≈ 597.7 x 336.2 mm, 24인치 FHD ≈ 531.4 x 298.9 mm
    private const double W27 = 597.7, H27 = 336.2, W24 = 531.4, H24 = 298.9;

    private static DeskMonitor Mon(string id, int l, int t, int w, int h, double x, double y, double wmm, double hmm)
        => new(id, PxRect.FromSize(l, t, w, h), new MmRect(x, y, wmm, hmm));

    private static double DeskY(DeskMonitor m, int pxY) => m.Desk.Y + (pxY + 0.5 - m.Px.Top) * m.MmPerPxY;
    private static double DeskX(DeskMonitor m, int pxX) => m.Desk.X + (pxX + 0.5 - m.Px.Left) * m.MmPerPxX;

    // ---------- 4K(150%)와 FHD(100%) 좌우, 아래선 맞춤 ----------
    [Fact]
    public void Right_exit_from_4K_150pct_to_FHD_keeps_physical_height()
    {
        // 4K 150%는 논리 2560x1440이지만 Win32 물리 픽셀은 3840x2160. FHD는 오른쪽에 윗선 정렬(Windows 기본)로 붙어 있다.
        var k4 = Mon("4k", 0, 0, 3840, 2160, 0, 0, W27, H27);
        var fhd = Mon("fhd", 3840, 0, 1920, 1080, W27, H27 - H24, W24, H24); // 책상 위에서는 아래쪽이 같은 선
        var layout = new DeskLayout(new[] { k4, fhd });

        foreach (int y in new[] { 2159, 2000, 1500, 1200, 1100 })
        {
            var r = CursorResolver.Resolve(layout, new PxPoint(3839, y), new PxPoint(3841, y));
            Assert.Equal(ResolveKind.Move, r.Kind);
            Assert.Equal("fhd", r.TargetId);
            Assert.InRange(r.Point.X, 3840, 3841);
            double expectedDesk = DeskY(k4, y);
            double actualDesk = DeskY(fhd, r.Point.Y);
            Assert.InRange(Math.Abs(expectedDesk - actualDesk), 0, fhd.MmPerPxY * 1.01);
        }
    }

    [Fact]
    public void Right_exit_below_FHD_extent_is_blocked_and_clamped()
    {
        var k4 = Mon("4k", 0, 0, 3840, 2160, 0, 0, W27, H27);
        var fhd = Mon("fhd", 3840, 0, 1920, 1080, W27, H27 - H24, W24, H24);
        var layout = new DeskLayout(new[] { k4, fhd });

        // 4K 위쪽 영역(책상 위에서 FHD 윗선(37.3mm)보다 위)에는 이웃이 없다.
        var r = CursorResolver.Resolve(layout, new PxPoint(3839, 100), new PxPoint(3845, 100));
        Assert.Equal(ResolveKind.Clamp, r.Kind);
        Assert.Equal(new PxPoint(3839, 100), r.Point);
    }

    [Fact]
    public void Return_trip_lands_on_same_physical_height_and_no_ping_pong()
    {
        var k4 = Mon("4k", 0, 0, 3840, 2160, 0, 0, W27, H27);
        var fhd = Mon("fhd", 3840, 0, 1920, 1080, W27, H27 - H24, W24, H24);
        var layout = new DeskLayout(new[] { k4, fhd });

        var go = CursorResolver.Resolve(layout, new PxPoint(3839, 1800), new PxPoint(3841, 1800));
        Assert.Equal(ResolveKind.Move, go.Kind);

        // 건너온 뒤 같은 방향으로 계속 이동 → 개입하지 않음
        var cont = CursorResolver.Resolve(layout, go.Point, new PxPoint(go.Point.X + 10, go.Point.Y));
        Assert.Equal(ResolveKind.Stay, cont.Kind);

        // 되돌아 가면 4K 쪽 거의 같은 높이
        var back = CursorResolver.Resolve(layout, new PxPoint(3840, go.Point.Y), new PxPoint(3838, go.Point.Y));
        Assert.Equal(ResolveKind.Move, back.Kind);
        Assert.Equal("4k", back.TargetId);
        Assert.InRange(Math.Abs(back.Point.Y - 1800), 0, 3);

        // 막 넘어온 지점에서 1픽셀 제자리 이동은 개입하지 않는다
        var jitter = CursorResolver.Resolve(layout, go.Point, new PxPoint(go.Point.X + 1, go.Point.Y));
        Assert.Equal(ResolveKind.Stay, jitter.Kind);
    }

    // ---------- Windows 배치와 무관하게 GUI 배치를 따른다 ----------
    [Fact]
    public void Gui_connected_but_not_adjacent_in_windows_pixels_can_cross()
    {
        // 픽셀 좌표에서는 1000px 떨어져 있지만(Windows에서는 막힘) GUI 상으로는 맞닿아 있다.
        var a = Mon("a", 0, 0, 1920, 1080, 0, 0, 531.4, 298.9);
        var b = Mon("b", 3000, 0, 1920, 1080, 531.4, 0, 531.4, 298.9);
        var layout = new DeskLayout(new[] { a, b });
        var r = CursorResolver.Resolve(layout, new PxPoint(1919, 500), new PxPoint(1925, 500));
        Assert.Equal(ResolveKind.Move, r.Kind);
        Assert.Equal(new PxPoint(3000 + 5, 500), r.Point); // 오버슈트 5.5칸 → 5
    }

    [Fact]
    public void Windows_adjacent_but_gui_disconnected_is_blocked()
    {
        var a = Mon("a", 0, 0, 1920, 1080, 0, 0, 531.4, 298.9);
        var b = Mon("b", 1920, 0, 1920, 1080, 700, 0, 531.4, 298.9); // 책상에서는 멀리 떨어짐
        var layout = new DeskLayout(new[] { a, b });
        var r = CursorResolver.Resolve(layout, new PxPoint(1919, 500), new PxPoint(1930, 500));
        Assert.Equal(ResolveKind.Clamp, r.Kind);
        Assert.Equal(new PxPoint(1919, 500), r.Point);
    }

    // ---------- 주 모니터 왼쪽의 음수 좌표 ----------
    [Fact]
    public void Negative_coordinates_left_of_primary()
    {
        var left = Mon("left", -1920, 0, 1920, 1080, -W24, 0, W24, H24);
        var primary = Mon("primary", 0, 0, 3840, 2160, 0, -(H27 - H24), W27, H27); // 위쪽 선 위로 튀어나오지 않게: 아래선 맞춤
        var layout = new DeskLayout(new[] { left, primary });

        // primary 에서 왼쪽으로 나가기: 아래선이 같으므로 primary 맨 아래 → left 맨 아래
        var r = CursorResolver.Resolve(layout, new PxPoint(0, 2159), new PxPoint(-2, 2159));
        // primary 의 desk y 는 음수 오프셋 - 맨 아래는 desk y = 0 + ... 좌측 모니터 하단과 맞는지만 확인
        Assert.Equal(ResolveKind.Move, r.Kind);
        Assert.Equal("left", r.TargetId);
        Assert.True(r.Point.X < 0 && r.Point.X >= -1920);
        Assert.InRange(r.Point.Y, 1070, 1079);

        // 왼쪽 모니터에서 오른쪽으로 되돌아오기
        var back = CursorResolver.Resolve(layout, new PxPoint(-1, 1079), new PxPoint(1, 1079));
        Assert.Equal(ResolveKind.Move, back.Kind);
        Assert.Equal("primary", back.TargetId);
        Assert.InRange(back.Point.Y, 2150, 2159);
    }

    // ---------- 세로 회전 ----------
    [Fact]
    public void Portrait_monitor_on_the_right()
    {
        var main = Mon("main", 0, 0, 2560, 1440, 0, 0, W27, H27 * 0 + 336.2); // 27" QHD
        // 24" FHD 세로 회전: 픽셀 1080x1920, 실제 가로 298.9, 세로 531.4
        var portrait = Mon("portrait", 2560, -200, 1080, 1920, W27, -150, H24, W24);
        var layout = new DeskLayout(new[] { main, portrait });

        // main 맨 위(y=0 → desk 0.x mm) → 세로 화면에서 desk y=0 지점 = 150mm 아래 = (150/531.4)*1920 ≈ 542 px, 화면 윗단은 -200
        var r = CursorResolver.Resolve(layout, new PxPoint(2559, 0), new PxPoint(2562, 0));
        Assert.Equal(ResolveKind.Move, r.Kind);
        Assert.Equal("portrait", r.TargetId);
        double wantMm = DeskY(main, 0);
        double gotMm = DeskY(portrait, r.Point.Y);
        Assert.InRange(Math.Abs(wantMm - gotMm), 0, portrait.MmPerPxY * 1.01);

        // 세로 화면 위쪽(desk y < 0)에서 왼쪽으로 나가면 main 위쪽 밖 → 막힘
        var blocked = CursorResolver.Resolve(layout, new PxPoint(2560, -100), new PxPoint(2557, -100));
        Assert.Equal(ResolveKind.Clamp, blocked.Kind);
        Assert.Equal(new PxPoint(2560, -100), blocked.Point);
    }

    // ---------- 3대 ----------
    [Fact]
    public void Three_monitors_side_by_side_with_different_heights()
    {
        var left = Mon("L", -1920, 100, 1920, 1080, -W24, 37.3, W24, H24);
        var mid = Mon("M", 0, 0, 3840, 2160, 0, 0, W27, H27);
        var right = Mon("R", 3840, 0, 2560, 1440, W27, 0, 597.7, 336.2);
        var layout = new DeskLayout(new[] { left, mid, right });

        var toRight = CursorResolver.Resolve(layout, new PxPoint(3839, 1080), new PxPoint(3841, 1080));
        Assert.Equal("R", toRight.TargetId);
        Assert.InRange(Math.Abs(DeskY(mid, 1080) - DeskY(right, toRight.Point.Y)), 0, right.MmPerPxY * 1.01);

        var toLeft = CursorResolver.Resolve(layout, new PxPoint(0, 1080), new PxPoint(-1, 1080));
        Assert.Equal("L", toLeft.TargetId);
        Assert.InRange(toLeft.Point.Y - left.Px.Top, 0, 1079);

        // 왼쪽 모니터에서 곧장 오른쪽 끝까지 아주 빠르게(두 칸 이상 건너뛰는 이동) → 첫 경계에서 처리되어 mid 에 착지
        var fast = CursorResolver.Resolve(layout, new PxPoint(-1, 500), new PxPoint(4000, 700));
        Assert.Equal("M", fast.TargetId);
    }

    [Fact]
    public void Three_monitors_stacked_vertically_and_diagonal_neighbor_is_not_connected()
    {
        var top = Mon("T", 0, -1080, 1920, 1080, 0, -H24, W24, H24);
        var mid = Mon("M", 0, 0, 1920, 1080, 0, 0, W24, H24);
        var bottom = Mon("B", 0, 1080, 1920, 1080, 0, H24, W24, H24);
        var layout = new DeskLayout(new[] { top, mid, bottom });

        var up = CursorResolver.Resolve(layout, new PxPoint(960, 0), new PxPoint(960, -3));
        Assert.Equal("T", up.TargetId);
        Assert.Equal(960, up.Point.X);
        Assert.InRange(up.Point.Y, -3, -1);

        var down = CursorResolver.Resolve(layout, new PxPoint(960, 1079), new PxPoint(960, 1083));
        Assert.Equal("B", down.TargetId);

        // top 에서 아래로 두 칸(=mid 를 지나 bottom)으로 빠르게 가도 첫 경계(mid)에서 처리
        var fast = CursorResolver.Resolve(layout, new PxPoint(960, -1), new PxPoint(960, 1500));
        Assert.Equal("M", fast.TargetId);
    }

    // ---------- 겹치지 않는 구간 / 모서리 ----------
    [Fact]
    public void Non_overlapping_span_is_blocked_and_slides_along_edge()
    {
        // A(위쪽 높음) 오른쪽에 B 가 아래쪽에만 붙어 있다. B 의 윗선 = A 의 y 150mm.
        var a = Mon("A", 0, 0, 1920, 1080, 0, 0, W24, H24);
        var b = Mon("B", 1920, 0, 1920, 1080, W24, 150, W24, H24);
        var layout = new DeskLayout(new[] { a, b });

        // A 위쪽 구간(y<150mm) → 막힘, 그리고 y 성분은 유지
        var r = CursorResolver.Resolve(layout, new PxPoint(1919, 100), new PxPoint(1925, 130));
        Assert.Equal(ResolveKind.Clamp, r.Kind);
        Assert.Equal(1919, r.Point.X);
        Assert.Equal(130, r.Point.Y);

        // 겹치는 구간 → 이동
        var ok = CursorResolver.Resolve(layout, new PxPoint(1919, 700), new PxPoint(1922, 700));
        Assert.Equal(ResolveKind.Move, ok.Kind);
    }

    [Fact]
    public void Diagonal_corner_exit_with_no_diagonal_neighbor_is_clamped()
    {
        var a = Mon("A", 0, 0, 1920, 1080, 0, 0, W24, H24);
        var b = Mon("B", 1920, 0, 1920, 1080, W24, 0, W24, H24);
        var layout = new DeskLayout(new[] { a, b });
        // A 우하단 모서리에서 대각선 이동: 아래쪽으로 나가는 건 막히고, 오른쪽으로 가는 성분은 B 로 넘어갈 수 있어야 함
        var r = CursorResolver.Resolve(layout, new PxPoint(1915, 1075), new PxPoint(1925, 1090));
        Assert.True(r.Kind == ResolveKind.Move && r.TargetId == "B");
        Assert.True(b.Px.Contains(r.Point));
        Assert.InRange(r.Point.Y, 1070, 1079); // 아래로 벗어난 y 는 B 안으로 제한
    }

    [Fact]
    public void Gap_within_tolerance_is_connected_but_larger_gap_is_not()
    {
        var a = Mon("A", 0, 0, 1920, 1080, 0, 0, W24, H24);
        var near = Mon("B", 1920, 0, 1920, 1080, W24 + 0.5, 0, W24, H24);
        var far = Mon("B", 1920, 0, 1920, 1080, W24 + 5, 0, W24, H24);
        Assert.Equal(ResolveKind.Move, CursorResolver.Resolve(new DeskLayout(new[] { a, near }), new PxPoint(1919, 10), new PxPoint(1921, 10)).Kind);
        Assert.Equal(ResolveKind.Clamp, CursorResolver.Resolve(new DeskLayout(new[] { a, far }), new PxPoint(1919, 10), new PxPoint(1921, 10)).Kind);
    }

    [Fact]
    public void Movement_inside_same_monitor_or_from_unknown_point_stays()
    {
        var a = Mon("A", 0, 0, 1920, 1080, 0, 0, W24, H24);
        var layout = new DeskLayout(new[] { a });
        Assert.Equal(ResolveKind.Stay, CursorResolver.Resolve(layout, new PxPoint(10, 10), new PxPoint(20, 20)).Kind);
        Assert.Equal(ResolveKind.Stay, CursorResolver.Resolve(layout, new PxPoint(-500, -500), new PxPoint(20, 20)).Kind);
    }
}

public class DeskLayoutTests
{
    [Fact]
    public void Shared_edges_report_only_overlapping_span()
    {
        var a = new DeskMonitor("A", PxRect.FromSize(0, 0, 1920, 1080), new MmRect(0, 0, 500, 300));
        var b = new DeskMonitor("B", PxRect.FromSize(1920, 0, 1920, 1080), new MmRect(500, 120, 500, 300));
        var edges = new DeskLayout(new[] { a, b }).GetSharedEdges();
        var e = Assert.Single(edges);
        Assert.Equal(EdgeSide.Right, e.Side);
        Assert.Equal(120, e.StartMm, 6);
        Assert.Equal(300, e.EndMm, 6);
    }

    [Fact]
    public void Corner_touch_only_is_not_an_edge()
    {
        var a = new DeskMonitor("A", PxRect.FromSize(0, 0, 100, 100), new MmRect(0, 0, 100, 100));
        var b = new DeskMonitor("B", PxRect.FromSize(100, 100, 100, 100), new MmRect(100, 100, 100, 100));
        Assert.Empty(new DeskLayout(new[] { a, b }).GetSharedEdges());
    }

    [Fact]
    public void Overlap_detected()
    {
        var a = new DeskMonitor("A", PxRect.FromSize(0, 0, 100, 100), new MmRect(0, 0, 100, 100));
        var b = new DeskMonitor("B", PxRect.FromSize(100, 0, 100, 100), new MmRect(50, 0, 100, 100));
        Assert.Single(new DeskLayout(new[] { a, b }).FindOverlaps());
    }
}
