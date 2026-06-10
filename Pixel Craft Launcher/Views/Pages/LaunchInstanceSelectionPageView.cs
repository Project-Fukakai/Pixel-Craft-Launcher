using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchInstanceSelectionPageView : UserControl
{
    public LaunchInstanceSelectionPageView(
        PixelLaunchInstanceSelectionPageSnapshot snapshot,
        PixelLaunchInstanceActionVisibilitySnapshot actionVisibility,
        PixelLaunchPageMessages messages,
        bool canOpenSelectedFolder,
        Action refresh,
        Action openSelectedFolder,
        Action<string> selectInstance,
        Action<string> openInstanceFolder,
        Action<string, string> openChildFolder)
    {
        var stack = CreatePageStack();

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refreshButton = new MyButton { Text = messages.InstanceRefreshButtonText, Height = 34 };
        refreshButton.Click += (_, _) => refresh();
        actions.Children.Add(refreshButton);

        var openRoot = new MyButton
        {
            Text = messages.InstanceOpenFolderButtonText,
            Height = 34,
            IsEnabled = canOpenSelectedFolder
        };
        openRoot.Click += (_, _) => openSelectedFolder();
        actions.Children.Add(openRoot);
        stack.Children.Add(BuildCard(messages.InstanceSelectButtonText, actions));

        if (snapshot.IsEmpty)
        {
            stack.Children.Add(BuildCard(messages.InstanceListCardTitle, CreateSubText(messages.InstanceListEmptyText)));
        }
        else
        {
            foreach (var group in snapshot.Groups)
                stack.Children.Add(BuildLaunchInstanceGroupCard(group, actionVisibility, messages, selectInstance, openInstanceFolder, openChildFolder));
        }

        Content = new MainPaneScrollHost
        {
            Children = { stack }
        };
    }

    private static MyCard BuildLaunchInstanceGroupCard(
        PixelLaunchInstanceGroupSnapshot group,
        PixelLaunchInstanceActionVisibilitySnapshot actionVisibility,
        PixelLaunchPageMessages messages,
        Action<string> selectInstance,
        Action<string> openInstanceFolder,
        Action<string, string> openChildFolder)
    {
        var list = new StackPanel { Spacing = 4 };
        foreach (var instance in group.Instances)
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

            if (actionVisibility.CanOpenFolder)
            {
                var open = CreateLaunchInstanceActionButton("mdi-folder-open-outline", messages.OpenInstanceFolderTip);
                open.Click += (_, _) => openInstanceFolder(instance.VersionDirectory);
                item.AddButton(open);
            }

            if (actionVisibility.CanOpenMods)
            {
                var mods = CreateLaunchInstanceActionButton("mdi-puzzle-outline", messages.OpenModsFolderTip);
                mods.Click += (_, _) => openChildFolder(instance.VersionDirectory, "mods");
                item.AddButton(mods);
            }

            if (actionVisibility.CanOpenSaves)
            {
                var saves = CreateLaunchInstanceActionButton("mdi-content-save-outline", messages.OpenSavesFolderTip);
                saves.Click += (_, _) => openChildFolder(instance.VersionDirectory, "saves");
                item.AddButton(saves);
            }
            list.Children.Add(item);
        }

        return BuildCard(group.Title, list);
    }

    private static MyIconButton CreateLaunchInstanceActionButton(string icon, string tip)
    {
        var button = new MyIconButton
        {
            Icon = icon,
            IconSize = 12,
            Width = 24,
            Height = 24,
            Padding = new Thickness(4)
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static StackPanel CreatePageStack()
    {
        return new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 26),
            Spacing = 14
        };
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

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            LineHeight = 20
        };
    }
}
