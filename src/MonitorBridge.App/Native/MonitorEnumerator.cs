using System.Text.RegularExpressions;
using Microsoft.Win32;
using MonitorBridge.Core;

namespace MonitorBridge.App.Native;

/// <summary>Windows가 보고하는 현재 모니터 목록을 읽는다. 변경 작업은 하지 않는다(읽기 전용).</summary>
internal static class MonitorEnumerator
{
    public static List<MonitorSnapshot> Enumerate()
    {
        var list = new List<MonitorSnapshot>();
        NativeMethods.MonitorEnumProc cb = (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var mi = new NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (!NativeMethods.GetMonitorInfo(hMon, ref mi)) return true;

            uint dpiX = 96;
            try { if (NativeMethods.GetDpiForMonitor(hMon, 0, out var dx, out _) == 0 && dx > 0) dpiX = dx; } catch { }

            var rot = Rotation.Deg0;
            var dm = new NativeMethods.DEVMODE { dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DEVMODE>() };
            if (NativeMethods.EnumDisplaySettings(mi.szDevice, -1, ref dm))
                rot = dm.dmDisplayOrientation switch { 1 => Rotation.Deg90, 2 => Rotation.Deg180, 3 => Rotation.Deg270, _ => Rotation.Deg0 };

            var (hwid, instance, friendly, edid) = ReadMonitorDevice(mi.szDevice);
            var info = EdidParser.Parse(edid);
            string modelId = info?.ModelId ?? (hwid ?? "");
            string uniqueId = info?.UniqueId ?? "";
            string key = (uniqueId != "" ? uniqueId : (hwid ?? mi.szDevice)) + "|" + (instance ?? mi.szDevice);

            int number = 0;
            var m = Regex.Match(mi.szDevice, @"DISPLAY(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) int.TryParse(m.Groups[1].Value, out number);

            string name = info?.Name is { Length: > 0 } n ? n : (friendly ?? mi.szDevice);
            var bounds = new PxRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
            bool primary = (mi.dwFlags & 1) != 0;

            // EDID는 물리 패널 기준(가로)으로 보고되는 경우가 대부분. 회전 여부는 LayoutBuilder가 처리한다.
            list.Add(new MonitorSnapshot(key, modelId, uniqueId, mi.szDevice, number, name, bounds, (int)dpiX, rot, primary,
                info?.WidthMm, info?.HeightMm));
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
        GC.KeepAlive(cb);

        return list.OrderBy(s => s.DisplayNumber).ThenBy(s => s.Bounds.Left).ToList();
    }

    private static (string? hwid, string? instance, string? friendly, byte[]? edid) ReadMonitorDevice(string adapterName)
    {
        // 어댑터(\\.\DISPLAY1)에 연결된 활성 모니터 장치를 찾는다.
        for (uint i = 0; i < 8; i++)
        {
            var dd = new NativeMethods.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(adapterName, i, ref dd, 1 /*EDD_GET_DEVICE_INTERFACE_NAME*/)) break;
            if ((dd.StateFlags & 0x1) == 0) continue; // ACTIVE
            // 예: \\?\DISPLAY#GSM5B09#5&2a3b4c&0&UID4357#{e6f07b5f-...}
            var parts = dd.DeviceID.Split('#');
            if (parts.Length < 3) return (null, null, dd.DeviceString, null);
            string hwid = parts[1], instance = parts[2];
            byte[]? edid = null;
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{hwid}\{instance}\Device Parameters");
                edid = k?.GetValue("EDID") as byte[];
            }
            catch { /* 권한/레지스트리 문제 시 EDID 없음으로 취급 */ }
            return (hwid, instance, dd.DeviceString, edid);
        }
        return (null, null, null, null);
    }
}
