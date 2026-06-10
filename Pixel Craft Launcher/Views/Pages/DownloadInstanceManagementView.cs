using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadInstanceManagementView : UserControl
{
    public DownloadInstanceManagementView(
        PixelInstanceManagementSnapshot snapshot,
        Action<string> selectInstance,
        Action<string> openInstanceFolder,
        Action refreshInstances,
        Action<string> openSelectedChildFolder)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var instance in snapshot.Instances)
        {
            var item = new MyListItem
            {
                Title = instance.Name,
                Info = instance.Info,
                Icon = instance.Icon,
                Type = MyListItem.CheckType.RadioBox,
                Checked = instance.IsSelected
            };
            item.Check += (_, _) => selectInstance(instance.VersionDirectory);
            var open = new MyIconButton { Icon = "mdi-folder-open-outline", Width = 24, Height = 24 };
            ToolTip.SetTip(open, snapshot.OpenInstanceFolderTip);
            open.Click += (_, _) => openInstanceFolder(instance.VersionDirectory);
            item.AddButton(open);
            stack.Children.Add(item);
        }

        if (snapshot.IsEmpty)
            stack.Children.Add(CreateBodyText(snapshot.EmptyText));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refresh = new MyButton { Text = snapshot.RefreshButtonText, Height = 34 };
        refresh.Click += (_, _) => refreshInstances();
        actions.Children.Add(refresh);
        var openMods = new MyButton { Text = snapshot.OpenModsButtonText, Height = 34, IsEnabled = snapshot.CanOpenSelectedChildFolders };
        openMods.Click += (_, _) => openSelectedChildFolder("mods");
        actions.Children.Add(openMods);
        var openSaves = new MyButton { Text = snapshot.OpenSavesButtonText, Height = 34, IsEnabled = snapshot.CanOpenSelectedChildFolders };
        openSaves.Click += (_, _) => openSelectedChildFolder("saves");
        actions.Children.Add(openSaves);
        stack.Children.Add(actions);
        Content = stack;
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
}
