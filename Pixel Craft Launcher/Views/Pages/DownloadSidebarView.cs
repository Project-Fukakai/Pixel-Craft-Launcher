using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadSidebarView : UserControl
{
    public DownloadSidebarView(
        PixelDownloadSidebarSnapshot snapshot,
        bool isInstallSelectionOpen,
        Action closeInstallSelection,
        Action<int> navigateCategory,
        Action<int> refreshCategory)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        foreach (var group in snapshot.Groups)
        {
            if (!string.IsNullOrWhiteSpace(group.Title))
                AddCategory(stack, group.Title);
            foreach (var item in group.Items)
                AddItem(stack, item, isInstallSelectionOpen, closeInstallSelection, navigateCategory, refreshCategory);
        }

        Content = new MyScrollViewer { Content = stack };
    }

    private static void AddCategory(StackPanel stack, string title)
    {
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(14, 14, 8, 4),
            Opacity = 0.6,
            FontSize = 12,
            Foreground = ThemeBrushes.Text
        });
    }

    private static void AddItem(
        StackPanel stack,
        PixelDownloadSidebarItemSnapshot snapshot,
        bool isInstallSelectionOpen,
        Action closeInstallSelection,
        Action<int> navigateCategory,
        Action<int> refreshCategory)
    {
        var item = new MyListItem
        {
            Title = snapshot.Title,
            Type = MyListItem.CheckType.RadioBox,
            Checked = snapshot.IsSelected,
            Icon = snapshot.Icon,
            Height = 36,
            LogoSize = 18,
            ContentPadding = new Thickness(8, 0, 8, 0),
            IsSidebarItem = true,
            Tag = snapshot.Category
        };
        item.Check += (_, _) =>
        {
            if (isInstallSelectionOpen)
                closeInstallSelection();
            navigateCategory(snapshot.Category);
        };

        var refreshButton = new MyIconButton
        {
            Icon = "mdi-refresh",
            IconSize = 10.8,
            Padding = new Thickness(4),
            Width = 22,
            Height = 22,
            Tag = snapshot.Category
        };
        ToolTip.SetTip(refreshButton, snapshot.RefreshButtonTooltip);
        refreshButton.Click += (_, _) => refreshCategory(snapshot.Category);
        item.AddButton(refreshButton);
        stack.Children.Add(item);
    }
}
