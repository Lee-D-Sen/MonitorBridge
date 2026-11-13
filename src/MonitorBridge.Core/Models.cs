using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorBridge.Core;

/// <summary>현재 Windows가 보고하는 모니터 상태(런타임 스냅샷).</summary>
public sealed record MonitorSnapshot(
    string Key,                 // 저장/매칭용 안정 키 (모델+일련번호+연결 인스턴스)
    string ModelId,             // 모델 식별자(일련번호 제외). 없으면 ""
    string UniqueId,            // 모델+일련번호. 없으면 ""
    string DeviceName,          // \\.\DISPLAY1
    int DisplayNumber,          // Windows 설정의 번호(1부터)
    string FriendlyName,
    PxRect Bounds,              // Win32 물리 픽셀
    int DpiX,                   // 유효 DPI (96 = 100%)
    Rotation Rotation,
    bool IsPrimary,
    double? EdidWidthMm,        // 기본 방향(가로) 기준 실제 크기, 모르면 null
    double? EdidHeightMm)
{
    public int ScalePercent => (int)Math.Round(DpiX * 100.0 / 96.0);
    /// <summary>회전 전(기본 방향) 해상도.</summary>
    public (int W, int H) NativePixels => Rotation.IsPortrait() ? (Bounds.Height, Bounds.Width) : (Bounds.Width, Bounds.Height);
}

/// <summary>Core가 만드는 사용자 메시지의 언어. 앱이 시작 시 설정한다.</summary>
public static class CoreText
{
    public static bool English { get; set; }
}

/// <summary>저장되는 모니터별 설정.</summary>
public sealed class MonitorSetting
{
    public string Key { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string UniqueId { get; set; } = "";
    /// <summary>책상 위 좌상단 위치(mm), 사용자가 보는 방향 기준.</summary>
    public double DeskXmm { get; set; }
    public double DeskYmm { get; set; }
    /// <summary>회전 전(기본 가로 방향) 실제 화면 크기(mm).</summary>
    public double NativeWidthMm { get; set; }
    public double NativeHeightMm { get; set; }
    /// <summary>true면 EDID 값이 아닌 사용자 입력 또는 추정값.</summary>
    public bool SizeUserDefined { get; set; }
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool StartWithWindows { get; set; }
    /// <summary>UI 언어("ko"/"en"). null이면 아직 선택하지 않음(첫 실행).</summary>
    public string? Language { get; set; }
    public List<MonitorSetting> Monitors { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static AppSettings FromJson(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
}

public static class LayoutBuilder
{
    /// <summary>회전을 적용한, 사용자가 보는 방향의 실제 크기(mm).</summary>
    public static (double W, double H) EffectiveSizeMm(MonitorSetting s, Rotation rot) =>
        rot.IsPortrait() ? (s.NativeHeightMm, s.NativeWidthMm) : (s.NativeWidthMm, s.NativeHeightMm);

    public static DeskLayout Build(IReadOnlyList<(MonitorSnapshot Snap, MonitorSetting Setting)> items) =>
        new(items.Select(i =>
        {
            var (w, h) = EffectiveSizeMm(i.Setting, i.Snap.Rotation);
            return new DeskMonitor(i.Snap.Key, i.Snap.Bounds, new MmRect(i.Setting.DeskXmm, i.Setting.DeskYmm, w, h));
        }));

    /// <summary>EDID 크기가 없을 때의 추정: 24인치 대각선, 픽셀 비율 유지(정사각 픽셀 가정). 반드시 사용자 확인이 필요한 값.</summary>
    public static (double W, double H) EstimateNativeSizeMm(int nativeW, int nativeH, double diagonalInch = 24.0)
    {
        double diagMm = diagonalInch * 25.4;
        double diagPx = Math.Sqrt((double)nativeW * nativeW + (double)nativeH * nativeH);
        double k = diagMm / diagPx;
        return (nativeW * k, nativeH * k);
    }

    /// <summary>
    /// Windows 배치를 출발점으로 한 기본 책상 배치. 주 모니터를 (0,0)에 두고 Windows에서 맞닿은 모니터를 그 옆에 붙인다.
    /// 맞닿은 축의 수직 위치는 Windows 픽셀 오프셋을 기준 모니터의 mm/px로 환산한다(보통 윗선 정렬).
    /// </summary>
    public static List<MonitorSetting> DefaultSettings(IReadOnlyList<MonitorSnapshot> snaps)
    {
        var settings = new List<MonitorSetting>();
        foreach (var s in snaps)
        {
            double nw, nh; bool user;
            if (s.EdidWidthMm is double ew && s.EdidHeightMm is double eh) { nw = ew; nh = eh; user = false; }
            else
            {
                (nw, nh) = EstimateNativeSizeMm(s.NativePixels.W, s.NativePixels.H);
                user = true;
            }
            settings.Add(new MonitorSetting
            {
                Key = s.Key, ModelId = s.ModelId, UniqueId = s.UniqueId,
                NativeWidthMm = nw, NativeHeightMm = nh, SizeUserDefined = user,
            });
        }
        if (snaps.Count == 0) return settings;

        int primary = Math.Max(0, snaps.ToList().FindIndex(m => m.IsPrimary));
        var placed = new HashSet<int> { primary };
        settings[primary].DeskXmm = 0; settings[primary].DeskYmm = 0;
        var queue = new Queue<int>(); queue.Enqueue(primary);

        double W(int i) => LayoutBuilder.EffectiveSizeMm(settings[i], snaps[i].Rotation).W;
        double H(int i) => LayoutBuilder.EffectiveSizeMm(settings[i], snaps[i].Rotation).H;

        while (queue.Count > 0)
        {
            int a = queue.Dequeue();
            var ra = snaps[a].Bounds;
            double mmX = W(a) / ra.Width, mmY = H(a) / ra.Height;
            for (int b = 0; b < snaps.Count; b++)
            {
                if (placed.Contains(b)) continue;
                var rb = snaps[b].Bounds;
                bool rightOf = rb.Left == ra.Right && Overlap(ra.Top, ra.Bottom, rb.Top, rb.Bottom);
                bool leftOf = rb.Right == ra.Left && Overlap(ra.Top, ra.Bottom, rb.Top, rb.Bottom);
                bool below = rb.Top == ra.Bottom && Overlap(ra.Left, ra.Right, rb.Left, rb.Right);
                bool above = rb.Bottom == ra.Top && Overlap(ra.Left, ra.Right, rb.Left, rb.Right);
                if (!(rightOf || leftOf || below || above)) continue;
                var sa = settings[a]; var sb = settings[b];
                if (rightOf) { sb.DeskXmm = sa.DeskXmm + W(a); sb.DeskYmm = sa.DeskYmm + (rb.Top - ra.Top) * mmY; }
                else if (leftOf) { sb.DeskXmm = sa.DeskXmm - W(b); sb.DeskYmm = sa.DeskYmm + (rb.Top - ra.Top) * mmY; }
                else if (below) { sb.DeskYmm = sa.DeskYmm + H(a); sb.DeskXmm = sa.DeskXmm + (rb.Left - ra.Left) * mmX; }
                else { sb.DeskYmm = sa.DeskYmm - H(b); sb.DeskXmm = sa.DeskXmm + (rb.Left - ra.Left) * mmX; }
                placed.Add(b); queue.Enqueue(b);
            }
        }
        // Windows에서 어디에도 맞닿지 않은 모니터는 가장 오른쪽 끝 옆에 둔다.
        for (int b = 0; b < snaps.Count; b++)
        {
            if (placed.Contains(b)) continue;
            double right = placed.Max(i => settings[i].DeskXmm + W(i));
            settings[b].DeskXmm = right; settings[b].DeskYmm = 0;
            placed.Add(b);
        }
        return settings;
    }

    private static bool Overlap(int a0, int a1, int b0, int b1) => Math.Min(a1, b1) > Math.Max(a0, b0);
}

public sealed record MatchResult(bool Ok, string? Reason, IReadOnlyList<(MonitorSnapshot Snap, MonitorSetting Setting)> Pairs);

public static class MonitorMatcher
{
    /// <summary>
    /// 저장된 설정과 현재 모니터를 안전하게 짝지을 수 있는지 판단한다.
    /// 1) Key가 정확히 같은 것끼리 짝, 2) 남은 것은 일련번호 포함 UniqueId가 양쪽에서 유일하게 같을 때만 짝.
    /// 하나라도 짝이 없거나(연결 해제/새 모니터) 모호하면 실패 → 호출자는 보정을 꺼야 한다.
    /// </summary>
    public static MatchResult Match(IReadOnlyList<MonitorSnapshot> current, IReadOnlyList<MonitorSetting> saved)
    {
        bool en = CoreText.English;
        if (saved.Count == 0) return new(false, en ? "No saved settings." : "저장된 설정이 없습니다.", Array.Empty<(MonitorSnapshot, MonitorSetting)>());
        if (current.Count != saved.Count)
            return new(false, en
                ? $"The number of monitors differs from the saved settings (saved {saved.Count}, now {current.Count})."
                : $"모니터 수가 저장된 설정({saved.Count}대)과 다릅니다(현재 {current.Count}대).", Array.Empty<(MonitorSnapshot, MonitorSetting)>());

        var pairs = new List<(MonitorSnapshot, MonitorSetting)>();
        var unmatchedCur = current.ToList();
        var unmatchedSaved = saved.ToList();

        foreach (var c in current)
        {
            var s = unmatchedSaved.FirstOrDefault(x => x.Key == c.Key);
            if (s is null) continue;
            pairs.Add((c, s)); unmatchedCur.Remove(c); unmatchedSaved.Remove(s);
        }
        foreach (var c in unmatchedCur.ToList())
        {
            if (c.UniqueId == "") continue;
            var candidates = unmatchedSaved.Where(x => x.UniqueId == c.UniqueId).ToList();
            var curSame = unmatchedCur.Count(x => x.UniqueId == c.UniqueId);
            if (candidates.Count == 1 && curSame == 1)
            {
                pairs.Add((c, candidates[0])); unmatchedCur.Remove(c); unmatchedSaved.Remove(candidates[0]);
            }
        }
        if (unmatchedCur.Count > 0 || unmatchedSaved.Count > 0)
            return new(false, en
                ? "Cannot safely match the saved monitors to the current ones (a monitor was replaced or its port changed)."
                : "저장된 모니터와 현재 모니터를 안전하게 연결할 수 없습니다(모니터가 바뀌었거나 연결 포트가 달라졌습니다).", Array.Empty<(MonitorSnapshot, MonitorSetting)>());

        // 현재 snapshot 순서 유지
        var ordered = current.Select(c => pairs.First(p => ReferenceEquals(p.Item1, c))).ToList();
        return new(true, null, ordered);
    }
}
