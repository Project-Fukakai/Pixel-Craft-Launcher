using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkEulaCardView : MyCard
{
    public GameLinkEulaCardView(
        PixelGameLinkEulaSnapshot snapshot,
        Action accept,
        Action<string> openUrl)
    {
        Title = snapshot.Title;
        CanSwap = false;

        var agree = new MyButton
        {
            Text = snapshot.AcceptButtonText,
            PrependIcon = "mdi-check",
            Variant = MyButtonVariant.Flat
        };
        agree.Click += (_, _) => accept();

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(CreateBodyText(snapshot.IntroText));
        foreach (var link in snapshot.Links)
            content.Children.Add(BuildExternalLinkItem(link, openUrl));
        content.Children.Add(CreateBodyText(snapshot.CommitmentText));
        content.Children.Add(new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { agree } });

        CardContent = new StackPanel
        {
            Spacing = 12,
            Children = { content }
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            FontSize = 14,
            LineHeight = 22
        };
    }

    private static MyListItem BuildExternalLinkItem(PixelGameLinkLinkSnapshot link, Action<string> openUrl)
    {
        var item = new MyListItem
        {
            Title = link.Text,
            Info = link.Info ?? string.Empty,
            Type = MyListItem.CheckType.Clickable,
            Icon = "mdi-open-in-new"
        };
        item.Click += (_, _) => openUrl(link.Url);
        return item;
    }
}
