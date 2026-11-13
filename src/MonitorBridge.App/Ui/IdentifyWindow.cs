using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MonitorBridge.App.Native;
using MonitorBridge.Core;

namespace MonitorBridge.App.Ui;

/// <summary>각 모니터 위에 번호를 잠깐 띄운다. 위치는 Win32 물리 픽셀로 직접 지정한다(모니터마다 DPI가 달라도 정확).</summary>
internal sealed class IdentifyWindow : Window
{
    private readonly PxRect _target;

    private IdentifyWindow(MonitorSnapshot snap)
    {
        _target = snap.Bounds;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(140, 15, 23, 42));
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = snap.DisplayNumber.ToString(), Foreground = Brushes.White, FontSize = 220, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{snap.Bounds.Width}×{snap.Bounds.Height} · {snap.ScalePercent}%", Foreground = Brushes.White, FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        Content = stack;
        SourceInitialized += (_, _) => Place();
        // 다른 DPI 모니터로 옮겨지면 WPF가 크기를 다시 계산하므로, 표시 후 한 번 더 맞춘다.
        ContentRendered += (_, _) => Place();
    }

    private void Place()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, _target.Left, _target.Top, _target.Width, _target.Height, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public static void ShowAll(IEnumerable<MonitorSnapshot> snaps)
    {
        var windows = new List<IdentifyWindow>();
        foreach (var s in snaps)
        {
            var w = new IdentifyWindow(s);
            windows.Add(w);
            w.Show();
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) => { timer.Stop(); foreach (var w in windows) w.Close(); };
        timer.Start();
    }
}
