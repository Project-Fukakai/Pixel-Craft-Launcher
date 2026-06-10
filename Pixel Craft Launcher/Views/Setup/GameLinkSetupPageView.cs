using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class GameLinkSetupPageView : UserControl
{
    public GameLinkSetupPageView(
        PixelSettingSection section,
        PixelGameLinkSetupPageSnapshot snapshot,
        Func<PixelSettingDescriptor, Control> buildSettingControl,
        Control networkTestPanel)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };

        stack.Children.Add(new MyHint
        {
            Text = snapshot.RestartRequiredHint,
            Theme = MyHint.Themes.Yellow,
            CanClose = false
        });

        foreach (var group in section.Groups)
        {
            var content = new StackPanel { Spacing = 12 };
            foreach (var setting in group.Settings)
                content.Children.Add(buildSettingControl(setting));

            stack.Children.Add(new MyCard
            {
                Title = group.Title,
                CanSwap = false,
                CardContent = content
            });
        }

        stack.Children.Add(new MyCard
        {
            Title = snapshot.NetworkTestTitle,
            CanSwap = false,
            CardContent = networkTestPanel
        });

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }
}
