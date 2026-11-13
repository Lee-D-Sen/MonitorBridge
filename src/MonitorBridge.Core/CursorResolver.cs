namespace MonitorBridge.Core;

public enum ResolveKind
{
    /// <summary>개입하지 않는다(같은 모니터 안에서의 이동이거나, 판단할 수 없는 경우).</summary>
    Stay,
    /// <summary>다른 모니터의 Point로 옮긴다.</summary>
    Move,
    /// <summary>연결된 화면이 없다. 현재 모니터 안의 Point로 제한한다(경계를 따라 미끄러지는 동작 유지).</summary>
    Clamp,
}

public readonly record struct ResolveResult(ResolveKind Kind, PxPoint Point, string? TargetId = null);

/// <summary>
/// 커서가 from에서 to로 이동하려 할 때, 책상 위 실제 배치(DeskLayout)에 따라 결과 위치를 계산한다.
/// GUI·Win32와 무관한 순수 계산이므로 단위 테스트 가능. 모든 좌표는 Win32 물리 픽셀.
/// </summary>
public static class CursorResolver
{
    public static ResolveResult Resolve(DeskLayout layout, PxPoint from, PxPoint to)
    {
        var a = layout.FindByPixel(from);
        if (a is null) return new ResolveResult(ResolveKind.Stay, to);
        if (a.Px.Contains(to)) return new ResolveResult(ResolveKind.Stay, to);

        // 픽셀은 [p, p+1) 칸이며 커서는 칸의 중심에 있다고 본다.
        double fx = from.X + 0.5, fy = from.Y + 0.5;
        double tx = to.X + 0.5, ty = to.Y + 0.5;

        PxPoint curTo = to;
        // 모서리를 지나는 경우 한 축이 막혀도 다른 축으로 미끄러질 수 있으므로 최대 2번 평가한다.
        for (int iter = 0; iter < 2; iter++)
        {
            if (a.Px.Contains(curTo)) return new ResolveResult(ResolveKind.Clamp, curTo);

            var exit = FindExit(a, fx, fy, tx, ty);
            var moved = TryCross(layout, a, exit, tx, ty);
            if (moved is not null) return moved.Value;

            // 이 축은 막힘 → 해당 축만 모니터 안으로 제한하고 다시 평가
            if (exit.Axis == 0)
            {
                tx = exit.Positive ? a.Px.Right - 0.5 : a.Px.Left + 0.5;
            }
            else
            {
                ty = exit.Positive ? a.Px.Bottom - 0.5 : a.Px.Top + 0.5;
            }
            curTo = new PxPoint((int)Math.Floor(tx), (int)Math.Floor(ty));
        }
        return new ResolveResult(ResolveKind.Clamp, a.Px.Clamp(curTo));
    }

    private readonly record struct ExitInfo(int Axis, bool Positive, double Ex, double Ey);

    private static ExitInfo FindExit(DeskMonitor a, double fx, double fy, double tx, double ty)
    {
        double L = a.Px.Left, R = a.Px.Right, T = a.Px.Top, B = a.Px.Bottom;
        double tX = double.PositiveInfinity, tY = double.PositiveInfinity;
        bool posX = false, posY = false;
        double dx = tx - fx, dy = ty - fy;

        if (tx > R) { tX = (R - fx) / dx; posX = true; }
        else if (tx < L) { tX = (L - fx) / dx; posX = false; }
        if (ty > B) { tY = (B - fy) / dy; posY = true; }
        else if (ty < T) { tY = (T - fy) / dy; posY = false; }

        bool useX = tX <= tY; // 동률이면 가로 경계를 먼저
        double t = useX ? tX : tY;
        if (double.IsInfinity(t)) t = 0; // 방어: 이 경우는 from이 이미 경계 위
        return new ExitInfo(useX ? 0 : 1, useX ? posX : posY, fx + t * dx, fy + t * dy);
    }

    private static ResolveResult? TryCross(DeskLayout layout, DeskMonitor a, ExitInfo exit, double tx, double ty)
    {
        double mx = a.MmPerPxX, my = a.MmPerPxY;
        // 나간 지점의 책상 좌표(mm)
        double deskX = a.Desk.X + (exit.Ex - a.Px.Left) * mx;
        double deskY = a.Desk.Y + (exit.Ey - a.Px.Top) * my;
        // 나간 뒤 남은 이동량(mm)
        double overX = (tx - exit.Ex) * mx;
        double overY = (ty - exit.Ey) * my;

        DeskMonitor? best = null;
        double bestGap = double.MaxValue;
        foreach (var b in layout.Monitors)
        {
            if (ReferenceEquals(b, a)) continue;
            double gap, perp, bStart, bEnd;
            if (exit.Axis == 0)
            {
                double aEdge = exit.Positive ? a.Desk.Right : a.Desk.X;
                double bEdge = exit.Positive ? b.Desk.X : b.Desk.Right;
                gap = Math.Abs(aEdge - bEdge);
                perp = deskY; bStart = b.Desk.Y; bEnd = b.Desk.Bottom;
                // 진행 방향 쪽에 있는 모니터인지(반대편 모니터는 제외)
                if (exit.Positive ? b.Desk.Right <= a.Desk.X : b.Desk.X >= a.Desk.Right) continue;
            }
            else
            {
                double aEdge = exit.Positive ? a.Desk.Bottom : a.Desk.Y;
                double bEdge = exit.Positive ? b.Desk.Y : b.Desk.Bottom;
                gap = Math.Abs(aEdge - bEdge);
                perp = deskX; bStart = b.Desk.X; bEnd = b.Desk.Right;
                if (exit.Positive ? b.Desk.Bottom <= a.Desk.Y : b.Desk.Y >= a.Desk.Bottom) continue;
            }
            if (gap > DeskLayout.EdgeToleranceMm) continue;
            if (perp < bStart - 1e-9 || perp >= bEnd - 1e-9) continue;
            if (gap < bestGap) { best = b; bestGap = gap; }
        }
        if (best is null) return null;

        var bm = best;
        double landX, landY;
        if (exit.Axis == 0)
        {
            landY = bm.Px.Top + (deskY + overY - bm.Desk.Y) / bm.MmPerPxY;
            double along = Math.Abs(overX) / bm.MmPerPxX;
            landX = exit.Positive ? bm.Px.Left + along : bm.Px.Right - along;
        }
        else
        {
            landX = bm.Px.Left + (deskX + overX - bm.Desk.X) / bm.MmPerPxX;
            double along = Math.Abs(overY) / bm.MmPerPxY;
            landY = exit.Positive ? bm.Px.Top + along : bm.Px.Bottom - along;
        }
        var land = bm.Px.Clamp(new PxPoint((int)Math.Floor(landX + 1e-9), (int)Math.Floor(landY + 1e-9)));
        return new ResolveResult(ResolveKind.Move, land, bm.Id);
    }
}
