using System.Windows;
using System.Windows.Controls;
using MonitorBridge.App.Services;

namespace MonitorBridge.App.Ui;

/// <summary>첫 실행에서 UI 언어를 고르는 작은 창. 안내문은 두 언어를 함께 보여준다.</summary>
internal sealed class LanguageDialog : Window
{
    public string Chosen { get; private set; }

    public LanguageDialog(string preselected)
    {
        Chosen = preselected;
        Title = "MonitorBridge — Language / 언어";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Choose the interface language.\n인터페이스 언어를 선택하세요.\n\nYou can change it later in the main window.\n나중에 메인 창에서 바꿀 수 있습니다.",
            Margin = new Thickness(0, 0, 0, 16),
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(MakeButton("English", "en", preselected == "en"));
        row.Children.Add(MakeButton("한국어", "ko", preselected == "ko"));
        panel.Children.Add(row);
        Content = panel;
    }

    private Button MakeButton(string text, string lang, bool isDefault)
    {
        var b = new Button { Content = text, MinWidth = 110, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(6, 0, 6, 0), IsDefault = isDefault };
        b.Click += (_, _) => { Chosen = lang; DialogResult = true; };
        return b;
    }
}
