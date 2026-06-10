using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class SettingsSectionView : UserControl
{
    public SettingsSectionView(
        PixelSettingSection section,
        Func<PixelSettingDescriptor, bool> shouldDisplay,
        Func<PixelSettingDescriptor, Control> buildSettingControl)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };

        foreach (var group in section.Groups)
        {
            var content = new StackPanel { Spacing = 12 };
            foreach (var setting in group.Settings)
            {
                if (shouldDisplay(setting))
                    content.Children.Add(buildSettingControl(setting));
            }

            stack.Children.Add(new MyCard
            {
                Title = group.Title,
                CanSwap = false,
                CardContent = content
            });
        }

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }
}
