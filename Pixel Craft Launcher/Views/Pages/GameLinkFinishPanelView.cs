using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkFinishPanelView : Grid
{
    public GameLinkFinishPanelView(
        PixelGameLinkFinishSnapshot snapshot,
        Func<Task> copyCode,
        Action<string> copyVirtualIp,
        Func<Task> leave,
        Action<PixelGameLinkPlayerSnapshot> showPlayerDetails)
    {
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var copyCodeButton = CreateActionButton(snapshot.CopyCodeButtonText, "mdi-content-copy");
        copyCodeButton.Click += async (_, _) => await copyCode();

        var copyIpButton = CreateActionButton(snapshot.CopyVirtualIpButtonText, "mdi-ip-network-outline");
        copyIpButton.IsEnabled = snapshot.CanCopyVirtualIp;
        copyIpButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(snapshot.VirtualIp))
                copyVirtualIp(snapshot.VirtualIp);
        };

        var exitButton = CreateActionButton(snapshot.ExitButtonText, "mdi-exit-run");
        exitButton.Click += async (_, _) => await leave();

        var infoList = new StackPanel { Spacing = 8 };
        foreach (var line in snapshot.InfoLines)
            infoList.Children.Add(CreateBodyText(line));

        var left = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                BuildCard(snapshot.InfoTitle, infoList),
                BuildCard(snapshot.ActionsTitle, new WrapPanel { Children = { copyIpButton, copyCodeButton, exitButton } })
            }
        };

        var playerList = new StackPanel { Spacing = 4 };
        if (!snapshot.HasPlayers)
        {
            playerList.Children.Add(CreateBodyText(snapshot.EmptyPlayersText));
        }
        else
        {
            foreach (var player in snapshot.Players)
                playerList.Children.Add(BuildPlayerListItem(player, showPlayerDetails));
        }

        Children.Add(left);
        var members = new MyCard
        {
            Title = snapshot.MembersTitle,
            Margin = new Thickness(10, 0, 0, 0),
            CardContent = playerList,
            MinWidth = 300
        };
        SetColumn(members, 1);
        Children.Add(members);
    }

    private static MyButton CreateActionButton(string text, string icon)
    {
        return new MyButton
        {
            Text = text,
            PrependIcon = icon,
            Variant = MyButtonVariant.Flat
        };
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            CanSwap = false,
            CardContent = content
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
    }

    private static MyListItem BuildPlayerListItem(
        PixelGameLinkPlayerSnapshot player,
        Action<PixelGameLinkPlayerSnapshot> showPlayerDetails)
    {
        var item = new MyListItem
        {
            Title = player.Name,
            Info = player.Info,
            Type = MyListItem.CheckType.Clickable,
            Icon = player.Icon
        };
        item.Click += (_, _) => showPlayerDetails(player);
        return item;
    }
}
