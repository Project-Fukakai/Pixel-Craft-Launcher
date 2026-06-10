using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ToolsLeftPageView : UserControl
{
    public ToolsLeftPageView(
        PixelGameLinkSidebarSnapshot gameLink,
        bool showTestEntry,
        GameLinkToolsNavigationBridge navigation)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(14, 16),
            Spacing = 10
        };

        stack.Children.Add(new TextBlock
        {
            Text = gameLink.Title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.Text,
            Margin = new Thickness(2, 0, 2, 8)
        });

        stack.Children.Add(CreateToolsSidebarItem(gameLink.AnnouncementTitle, gameLink.AnnouncementInfo, gameLink.AnnouncementIcon, false, navigation.RefreshGameLinkPage));
        stack.Children.Add(CreateToolsSidebarItem(gameLink.EulaTitle, gameLink.EulaInfo, gameLink.EulaIcon,
            navigation.Subpage == PixelGameLinkSubpage.Eula,
            navigation.ShowEula));
        stack.Children.Add(CreateToolsSidebarItem(gameLink.SelectTitle, gameLink.SelectInfo, gameLink.SelectIcon,
            navigation.Subpage == PixelGameLinkSubpage.Select,
            navigation.ShowSelectOrEula));
        stack.Children.Add(CreateToolsSidebarItem(gameLink.CurrentLobbyTitle, gameLink.CurrentLobbyInfo, gameLink.CurrentLobbyIcon,
            navigation.Subpage == PixelGameLinkSubpage.Finish,
            navigation.ShowConnectedLobby));

        stack.Children.Add(new Border { Height = 1, Background = ThemeBrushes.SidebarBorder, Margin = new Thickness(2, 4) });
        stack.Children.Add(CreateToolsSidebarItem(gameLink.EasyTierTitle, gameLink.EasyTierInfo, gameLink.EasyTierIcon, false, navigation.RefreshGameLinkPage));

        if (showTestEntry)
            stack.Children.Add(CreateToolsSidebarItem(gameLink.TestEntryTitle, gameLink.TestEntryInfo, gameLink.TestEntryIcon, false, navigation.OpenControlsPreview));

        Content = stack;
    }

    private static MyListItem CreateToolsSidebarItem(string title, string info, string icon, bool active, Action action)
    {
        var item = new MyListItem
        {
            Title = title,
            Info = info,
            Type = MyListItem.CheckType.RadioBox,
            Checked = active,
            Icon = icon,
            IsSidebarItem = true
        };
        item.Click += (_, _) => action();
        return item;
    }
}
