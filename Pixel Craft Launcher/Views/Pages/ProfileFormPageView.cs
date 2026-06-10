using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ProfileFormPageView : UserControl
{
    public ProfileFormPageView(string title, IReadOnlyList<Control> controls)
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(title, " "));
        var form = new StackPanel { Spacing = 10, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var control in controls)
            form.Children.Add(control);
        stack.Children.Add(BuildCard(title, form));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static StackPanel CreatePageStack()
    {
        return new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 26),
            Spacing = 14
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
}
