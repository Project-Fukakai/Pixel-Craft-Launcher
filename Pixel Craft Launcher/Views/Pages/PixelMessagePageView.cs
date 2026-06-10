using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class PixelMessagePageView : UserControl
{
    public PixelMessagePageView(string title, string message)
    {
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Children =
                {
                    new MyCard
                    {
                        Title = title,
                        CanSwap = false,
                        CardContent = new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = ThemeBrushes.TextSecondary,
                            FontSize = 14,
                            LineHeight = 22
                        }
                    }
                }
            }
        };
    }
}
