using Microsoft.Win32;
using MonitorBridge.Core;

namespace MonitorBridge.App.Services;

internal static class SettingsStore
{
    public static string Directory => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorBridge");
    public static string FilePath => System.IO.Path.Combine(Directory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return AppSettings.FromJson(File.ReadAllText(FilePath));
        }
        catch
        {
            // 손상된 파일은 백업하고 기본값으로 시작
            try { File.Move(FilePath, FilePath + ".broken", true); } catch { }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, s.ToJson());
        File.Move(tmp, FilePath, true);
    }

    public static void Delete()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }
}

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MonitorBridge";

    public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException();
            k.SetValue(ValueName, $"\"{exe}\" --minimized");
        }
        else k.DeleteValue(ValueName, false);
    }
}
