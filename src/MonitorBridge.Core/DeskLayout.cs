namespace MonitorBridge.Core;

/// <summary>
/// 보정 계산에 쓰는 모니터 하나. 픽셀 사각형(Win32 물리 픽셀)과 책상 위 실제 사각형(mm)을 함께 가진다.
/// 배율(DPI scale)은 의도적으로 포함하지 않는다 — 실제 크기는 mm로만 표현한다.
/// </summary>
public sealed record DeskMonitor(string Id, PxRect Px, MmRect Desk)
{
    public double MmPerPxX => Desk.Width / Px.Width;
    public double MmPerPxY => Desk.Height / Px.Height;
}

public enum EdgeSide { Left, Right, Top, Bottom }

/// <summary>두 모니터 사이에서 커서가 통과할 수 있는 경계 구간(책상 좌표 mm).</summary>
public sealed record SharedEdge(string FromId, string ToId, EdgeSide Side, double FixedMm, double StartMm, double EndMm);

public sealed class DeskLayout
{
    /// <summary>이 거리(mm) 이하로 떨어져 있거나 겹친 경계는 맞닿은 것으로 본다.</summary>
    public const double EdgeToleranceMm = 1.0;

    public IReadOnlyList<DeskMonitor> Monitors { get; }

    public DeskLayout(IEnumerable<DeskMonitor> monitors) => Monitors = monitors.ToList();

    public DeskMonitor? FindByPixel(PxPoint p) => Monitors.FirstOrDefault(m => m.Px.Contains(p));

    /// <summary>두 모니터가 tol 이상 겹쳐 있으면 true(배치가 잘못된 상태).</summary>
    public static bool Overlaps(MmRect a, MmRect b, double tol = EdgeToleranceMm)
    {
        double ox = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        double oy = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        return ox > tol && oy > tol;
    }

    public IEnumerable<(string A, string B)> FindOverlaps()
    {
        for (int i = 0; i < Monitors.Count; i++)
            for (int j = i + 1; j < Monitors.Count; j++)
                if (Overlaps(Monitors[i].Desk, Monitors[j].Desk))
                    yield return (Monitors[i].Id, Monitors[j].Id);
    }

    /// <summary>모든 방향의 통과 가능한 경계 구간을 계산한다(각 구간은 양방향이므로 (A→B) 한쪽 기준으로 한 번씩 반환).</summary>
    public IReadOnlyList<SharedEdge> GetSharedEdges()
    {
        var result = new List<SharedEdge>();
        for (int i = 0; i < Monitors.Count; i++)
        {
            for (int j = i + 1; j < Monitors.Count; j++)
            {
                var a = Monitors[i];
                var b = Monitors[j];
                // A 오른쪽 - B 왼쪽
                TryVertical(a, b, EdgeSide.Right, result);
                // A 왼쪽 - B 오른쪽
                TryVertical(a, b, EdgeSide.Left, result);
                TryHorizontal(a, b, EdgeSide.Bottom, result);
                TryHorizontal(a, b, EdgeSide.Top, result);
            }
        }
        return result;
    }

    private static void TryVertical(DeskMonitor a, DeskMonitor b, EdgeSide aSide, List<SharedEdge> sink)
    {
        double aEdge = aSide == EdgeSide.Right ? a.Desk.Right : a.Desk.X;
        double bEdge = aSide == EdgeSide.Right ? b.Desk.X : b.Desk.Right;
        if (Math.Abs(aEdge - bEdge) > EdgeToleranceMm) return;
        double s = Math.Max(a.Desk.Y, b.Desk.Y);
        double e = Math.Min(a.Desk.Bottom, b.Desk.Bottom);
        if (e - s <= 1e-9) return;
        sink.Add(new SharedEdge(a.Id, b.Id, aSide, aEdge, s, e));
    }

    private static void TryHorizontal(DeskMonitor a, DeskMonitor b, EdgeSide aSide, List<SharedEdge> sink)
    {
        double aEdge = aSide == EdgeSide.Bottom ? a.Desk.Bottom : a.Desk.Y;
        double bEdge = aSide == EdgeSide.Bottom ? b.Desk.Y : b.Desk.Bottom;
        if (Math.Abs(aEdge - bEdge) > EdgeToleranceMm) return;
        double s = Math.Max(a.Desk.X, b.Desk.X);
        double e = Math.Min(a.Desk.Right, b.Desk.Right);
        if (e - s <= 1e-9) return;
        sink.Add(new SharedEdge(a.Id, b.Id, aSide, aEdge, s, e));
    }
}
