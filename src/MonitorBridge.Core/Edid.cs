namespace MonitorBridge.Core;

public sealed record EdidInfo(string ManufacturerId, int ProductCode, uint Serial, double? WidthMm, double? HeightMm, string? Name)
{
    /// <summary>모델 식별자. 예: GSM5B09</summary>
    public string ModelId => $"{ManufacturerId}{ProductCode:X4}";
    /// <summary>모델 + 일련번호(일련번호가 0이면 모델만)</summary>
    public string UniqueId => Serial == 0 ? ModelId : $"{ModelId}-{Serial:X8}";
}

/// <summary>EDID 128바이트 기본 블록 파서. 화면 크기는 신뢰할 수 있을 때만 값을 채운다.</summary>
public static class EdidParser
{
    private static readonly byte[] Header = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };

    public static EdidInfo? Parse(byte[]? edid)
    {
        if (edid is null || edid.Length < 128) return null;
        for (int i = 0; i < 8; i++) if (edid[i] != Header[i]) return null;
        int sum = 0;
        for (int i = 0; i < 128; i++) sum += edid[i];
        if ((sum & 0xFF) != 0) return null; // 체크섬 불일치 → 신뢰하지 않음

        int mfg = (edid[8] << 8) | edid[9];
        char c1 = (char)('A' + ((mfg >> 10) & 0x1F) - 1);
        char c2 = (char)('A' + ((mfg >> 5) & 0x1F) - 1);
        char c3 = (char)('A' + (mfg & 0x1F) - 1);
        int product = edid[10] | (edid[11] << 8);
        uint serial = (uint)(edid[12] | (edid[13] << 8) | (edid[14] << 16) | (edid[15] << 24));

        double? w = null, h = null;
        // 1) 첫 상세 타이밍 디스크립터(mm 단위, 가장 정확)
        int d = 54;
        int pixelClock = edid[d] | (edid[d + 1] << 8);
        if (pixelClock != 0)
        {
            int wmm = edid[d + 12] | ((edid[d + 14] & 0xF0) << 4);
            int hmm = edid[d + 13] | ((edid[d + 14] & 0x0F) << 8);
            if (wmm > 0 && hmm > 0) { w = wmm; h = hmm; }
        }
        // 2) 기본 파라미터(cm 단위). 둘 다 0이 아니어야 하며, 0이면 투사형/미지정
        if (w is null)
        {
            int wcm = edid[21], hcm = edid[22];
            if (wcm > 0 && hcm > 0) { w = wcm * 10.0; h = hcm * 10.0; }
        }
        // 터무니없는 값 방어(10~3000mm)
        if (w is not null && (w < 100 || w > 3000 || h < 60 || h > 3000)) { w = null; h = null; }

        string? name = null;
        for (int off = 54; off <= 108; off += 18)
        {
            if (edid[off] == 0 && edid[off + 1] == 0 && edid[off + 3] == 0xFC)
            {
                var chars = new char[13];
                int n = 0;
                for (int k = 0; k < 13; k++)
                {
                    byte b = edid[off + 5 + k];
                    if (b == 0x0A) break;
                    chars[n++] = (char)b;
                }
                name = new string(chars, 0, n).Trim();
                break;
            }
        }
        return new EdidInfo($"{c1}{c2}{c3}", product, serial, w, h, name);
    }
}
