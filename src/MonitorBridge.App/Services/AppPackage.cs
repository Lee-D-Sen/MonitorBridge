using System.Runtime.InteropServices;

namespace MonitorBridge.App.Services;

/// <summary>MSIX(Microsoft Store) 패키지로 실행 중인지 감지한다. 패키지 앱은 HKCU\Run 쓰기가 가상화되므로 자체 시작 등록 UI를 숨긴다.</summary>
internal static class AppPackage
{
    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, [Out] char[]? name);

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) != AppModelErrorNoPackage;
        }
        catch { return false; }
    }
}
