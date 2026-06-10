using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkFooterView : MyCard
{
    public GameLinkFooterView(
        PixelGameLinkFooterSnapshot snapshot,
        Action<string> openUrl,
        Action disableGameLink)
    {
        Title = snapshot.Title;
        CanSwap = false;

        var links = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var link in snapshot.Links)
        {
            links.Children.Add(CreateTextLink(link, openUrl));
            links.Children.Add(CreateFooterSeparator());
        }

        var stop = new MyTextButton { Text = snapshot.DisableButtonText };
        stop.Click += (_, _) => disableGameLink();
        links.Children.Add(stop);

        var friendLinks = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        friendLinks.Children.Add(new TextBlock { Text = snapshot.FriendPrefix, Foreground = ThemeBrushes.TextSecondary });
        foreach (var link in snapshot.FriendLinks)
        {
            friendLinks.Children.Add(CreateTextLink(link, openUrl));
            friendLinks.Children.Add(CreateFooterSeparator());
        }

        CardContent = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                links,
                friendLinks
            }
        };
    }

    private static MyTextButton CreateTextLink(PixelGameLinkLinkSnapshot link, Action<string> openUrl)
    {
        var button = new MyTextButton { Text = link.Text };
        button.Click += (_, _) => openUrl(link.Url);
        return button;
    }

    private static TextBlock CreateFooterSeparator() => new()
    {
        Text = " | ",
        Foreground = ThemeBrushes.TextSecondary,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };
}
