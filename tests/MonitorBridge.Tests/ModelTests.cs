using MonitorBridge.Core;
using Xunit;

namespace MonitorBridge.Tests;

public class EdidTests
{
    public static byte[] MakeEdid(int wmm, int hmm, bool detailed = true, uint serial = 0x1234, string name = "TEST27")
    {
        var e = new byte[128];
        byte[] header = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        header.CopyTo(e, 0);
        // 제조사 "GSM": G=7,S=19,M=13
        int mfg = (7 << 10) | (19 << 5) | 13;
        e[8] = (byte)(mfg >> 8); e[9] = (byte)(mfg & 0xFF);
        e[10] = 0x09; e[11] = 0x5B; // product 0x5B09
        e[12] = (byte)serial; e[13] = (byte)(serial >> 8); e[14] = (byte)(serial >> 16); e[15] = (byte)(serial >> 24);
        e[21] = (byte)(wmm / 10); e[22] = (byte)(hmm / 10);
        if (detailed)
        {
            e[54] = 0x01; e[55] = 0x01; // pixel clock != 0
            e[66] = (byte)(wmm & 0xFF); e[67] = (byte)(hmm & 0xFF);
            e[68] = (byte)(((wmm >> 8) << 4) | (hmm >> 8));
        }
        // 이름 디스크립터는 3번째 블록(90)
        e[90] = 0; e[91] = 0; e[92] = 0; e[93] = 0xFC; e[94] = 0;
        var nm = (name + "\n").PadRight(13, ' ');
        for (int i = 0; i < 13; i++) e[95 + i] = (byte)nm[i];
        int sum = 0; for (int i = 0; i < 127; i++) sum += e[i];
        e[127] = (byte)((256 - (sum & 0xFF)) & 0xFF);
        return e;
    }

    [Fact]
    public void Parses_detailed_timing_size_mm()
    {
        var info = EdidParser.Parse(MakeEdid(597, 336));
        Assert.NotNull(info);
        Assert.Equal(597, info!.WidthMm);
        Assert.Equal(336, info.HeightMm);
        Assert.Equal("GSM5B09", info.ModelId);
        Assert.Equal("GSM5B09-00001234", info.UniqueId);
        Assert.Equal("TEST27", info.Name);
    }

    [Fact]
    public void Falls_back_to_cm_when_no_detailed_timing()
    {
        var info = EdidParser.Parse(MakeEdid(530, 300, detailed: false));
        Assert.Equal(530, info!.WidthMm);
        Assert.Equal(300, info.HeightMm);
    }

    [Fact]
    public void Bad_checksum_or_header_is_rejected()
    {
        var e = MakeEdid(597, 336);
        e[127] ^= 0x55;
        Assert.Null(EdidParser.Parse(e));
        Assert.Null(EdidParser.Parse(new byte[64]));
        Assert.Null(EdidParser.Parse(null));
    }

    [Fact]
    public void Zero_size_means_unknown()
    {
        var info = EdidParser.Parse(MakeEdid(0, 0, detailed: false));
        Assert.NotNull(info);
        Assert.Null(info!.WidthMm);
    }
}

public class SettingsAndMatchingTests
{
    private static MonitorSnapshot Snap(string key, string uid, int l, int t, int w, int h, bool primary = false,
        Rotation rot = Rotation.Deg0, double? wmm = 530, double? hmm = 300, int dpi = 96) =>
        new(key, uid.Length >= 7 ? uid[..7] : uid, uid, "\\\\.\\DISPLAY1", 1, "x", PxRect.FromSize(l, t, w, h), dpi, rot, primary, wmm, hmm);

    [Fact]
    public void Settings_roundtrip_json()
    {
        var s = new AppSettings { Enabled = true, Monitors = { new MonitorSetting { Key = "k", DeskXmm = 1.5, NativeWidthMm = 500, NativeHeightMm = 300 } } };
        var back = AppSettings.FromJson(s.ToJson());
        Assert.True(back.Enabled);
        Assert.Equal(1.5, back.Monitors[0].DeskXmm);
    }

    [Fact]
    public void Exact_key_matches()
    {
        var cur = new[] { Snap("a", "AAA0001-00000001", 0, 0, 1920, 1080, true), Snap("b", "BBB0002-00000002", 1920, 0, 1920, 1080) };
        var saved = new[] { new MonitorSetting { Key = "b" }, new MonitorSetting { Key = "a" } };
        var m = MonitorMatcher.Match(cur, saved);
        Assert.True(m.Ok);
        Assert.Equal("a", m.Pairs[0].Setting.Key);
    }

    [Fact]
    public void Same_monitor_on_different_port_matches_by_unique_id()
    {
        var cur = new[] { Snap("a@port2", "AAA0001-00000001", 0, 0, 1920, 1080, true) , Snap("b", "BBB0002-00000002", 1920, 0, 1920, 1080) };
        var saved = new[] { new MonitorSetting { Key = "a@port1", UniqueId = "AAA0001-00000001" }, new MonitorSetting { Key = "b" } };
        Assert.True(MonitorMatcher.Match(cur, saved).Ok);
    }

    [Fact]
    public void Identical_models_without_serial_on_changed_ports_are_ambiguous()
    {
        var cur = new[] { Snap("x@3", "", 0, 0, 1920, 1080, true), Snap("x@4", "", 1920, 0, 1920, 1080) };
        var saved = new[] { new MonitorSetting { Key = "x@1" }, new MonitorSetting { Key = "x@2" } };
        var m = MonitorMatcher.Match(cur, saved);
        Assert.False(m.Ok);
        Assert.NotNull(m.Reason);
    }

    [Fact]
    public void Disconnected_or_new_monitor_fails()
    {
        var one = new[] { Snap("a", "A-1", 0, 0, 1920, 1080, true) };
        var savedTwo = new[] { new MonitorSetting { Key = "a" }, new MonitorSetting { Key = "b" } };
        Assert.False(MonitorMatcher.Match(one, savedTwo).Ok);
        var two = new[] { Snap("a", "A-1", 0, 0, 1920, 1080, true), Snap("c", "C-1", 1920, 0, 1920, 1080) };
        Assert.False(MonitorMatcher.Match(two, savedTwo).Ok);
        Assert.False(MonitorMatcher.Match(two, Array.Empty<MonitorSetting>()).Ok);
    }

    [Fact]
    public void Default_layout_follows_windows_adjacency_with_physical_sizes()
    {
        // 4K 150% 주 모니터 + 오른쪽 FHD, Windows 에서는 윗선 정렬
        var k4 = Snap("4k", "A-1", 0, 0, 3840, 2160, true, wmm: 597.7, hmm: 336.2, dpi: 144);
        var fhd = Snap("fhd", "B-1", 3840, 0, 1920, 1080, wmm: 531.4, hmm: 298.9);
        var s = LayoutBuilder.DefaultSettings(new[] { k4, fhd });
        Assert.Equal(0, s[0].DeskXmm);
        Assert.Equal(597.7, s[1].DeskXmm, 3);
        Assert.Equal(0, s[1].DeskYmm, 3);
        Assert.False(s[0].SizeUserDefined);
    }

    [Fact]
    public void Default_layout_left_negative_and_rotation_swaps_size()
    {
        var main = Snap("m", "A-1", 0, 0, 2560, 1440, true, wmm: 597.7, hmm: 336.2);
        var left = Snap("l", "B-1", -1080, -200, 1080, 1920, rot: Rotation.Deg90, wmm: 531.4, hmm: 298.9);
        var s = LayoutBuilder.DefaultSettings(new[] { main, left });
        // 세로 회전이므로 폭 298.9
        Assert.Equal(-298.9, s[1].DeskXmm, 3);
        var layout = LayoutBuilder.Build(new[] { (main, s[0]), (left, s[1]) });
        Assert.Equal(298.9, layout.Monitors[1].Desk.Width, 3);
        Assert.Equal(531.4, layout.Monitors[1].Desk.Height, 3);
        Assert.Empty(layout.FindOverlaps());
    }

    [Fact]
    public void Missing_edid_size_is_estimated_and_flagged()
    {
        var a = Snap("a", "", 0, 0, 1920, 1080, true, wmm: null, hmm: null);
        var s = LayoutBuilder.DefaultSettings(new[] { a });
        Assert.True(s[0].SizeUserDefined);
        Assert.Equal(24 * 25.4, Math.Sqrt(s[0].NativeWidthMm * s[0].NativeWidthMm + s[0].NativeHeightMm * s[0].NativeHeightMm), 3);
    }
}
