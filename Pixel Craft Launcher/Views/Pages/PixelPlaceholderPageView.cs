using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class PixelPlaceholderPageView : UserControl
{
    public PixelPlaceholderPageView(PixelPlaceholderPageSnapshot snapshot)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };
        stack.Children.Add(BuildHeroCard(snapshot.Title, snapshot.Description));
        foreach (var section in snapshot.Sections)
            stack.Children.Add(BuildCard(section.Title, section.Text));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static MyCard BuildHeroCard(string title, string description)
    {
        return new MyCard
        {
            Title = title,
            CanSwap = false,
            CardContent = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = description,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ThemeBrushes.TextSecondary,
                        FontSize = 14
                    }
                }
            }
        };
    }

    private static MyCard BuildCard(string title, string text)
    {
        return new MyCard
        {
            Title = title,
            CanSwap = false,
            CardContent = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrushes.Text,
                FontSize = 14,
                LineHeight = 22
            }
        };
    }
}
