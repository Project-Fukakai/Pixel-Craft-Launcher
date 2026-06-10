using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadPendingPageView : UserControl
{
    public DownloadPendingPageView(
        PixelDownloadPendingPageSnapshot snapshot,
        Control instanceManagementPanel)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 26),
            Spacing = 14
        };
        stack.Children.Add(BuildHeroCard(snapshot.Title, snapshot.Description));
        var status = new StackPanel { Spacing = 8 };
        foreach (var line in snapshot.StatusLines)
            status.Children.Add(CreateBodyText(line));
        stack.Children.Add(BuildCard(snapshot.StatusTitle, status));
        stack.Children.Add(BuildCard(snapshot.InstanceManagementTitle, instanceManagementPanel));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static MyCard BuildHeroCard(string title, string description)
    {
        return BuildCard(title, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = description,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrushes.Text
                }
            }
        });
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            Margin = new Thickness(0, 0, 0, 2),
            CardContent = content
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            LineHeight = 22
        };
    }
}
