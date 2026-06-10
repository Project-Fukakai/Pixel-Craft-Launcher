using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchRightPageView : MainPaneScrollHost
{
    public LaunchRightPageView(Control? homepage, PixelLaunchPageMessages messages)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(25, 25, 25, 10),
            Spacing = 15
        };

        if (homepage is not null)
            stack.Children.Add(homepage);

        var launchLog = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 38, 20, 18),
            Foreground = ThemeBrushes.Text
        };
        launchLog.Bind(TextBlock.TextProperty, new Binding("Launch.LaunchLog"));
        stack.Children.Add(BuildCard(messages.LaunchLogCardTitle, launchLog));

        Children.Add(stack);
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            Margin = new Thickness(0, 0, 0, 8),
            CardContent = content
        };
    }
}
