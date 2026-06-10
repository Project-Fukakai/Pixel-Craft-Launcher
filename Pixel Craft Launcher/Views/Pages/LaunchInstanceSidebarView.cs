using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchInstanceSidebarView : UserControl
{
    private readonly PixelLaunchPageMessages _messages;
    private readonly Action<string> _openFolder;
    private readonly Action _refresh;
    private readonly Canvas _popupOverlay;
    private readonly Func<Size> _getFallbackOverlaySize;

    public LaunchInstanceSidebarView(
        IReadOnlyList<PixelLaunchFolderSnapshot> folders,
        PixelLaunchPageMessages messages,
        Action<string> selectFolder,
        Action addOrImportFolder,
        Action<string> openFolder,
        Action refresh,
        Canvas popupOverlay,
        Func<Size> getFallbackOverlaySize)
    {
        _messages = messages;
        _openFolder = openFolder;
        _refresh = refresh;
        _popupOverlay = popupOverlay;
        _getFallbackOverlaySize = getFallbackOverlaySize;

        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        AddCategory(stack, messages.FolderListCategoryTitle);
        foreach (var folder in folders)
            AddFolderItem(stack, folder, selectFolder);

        AddCategory(stack, messages.FolderManageCategoryTitle);
        var add = new MyListItem
        {
            Title = messages.AddFolderButtonText,
            Info = messages.AddFolderButtonDescription,
            Icon = "mdi-folder-plus-outline",
            Type = MyListItem.CheckType.Clickable,
            IsSidebarItem = true
        };
        add.Click += (_, _) => addOrImportFolder();
        stack.Children.Add(add);

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

    private void AddFolderItem(
        StackPanel stack,
        PixelLaunchFolderSnapshot folder,
        Action<string> selectFolder)
    {
        var item = new MyListItem
        {
            Title = folder.Title,
            Info = folder.Path,
            Icon = folder.Icon,
            Type = MyListItem.CheckType.RadioBox,
            Checked = folder.IsSelected,
            IsSidebarItem = true
        };
        AddFolderActionButton(item, folder);
        item.Check += (_, _) => selectFolder(folder.Path);
        stack.Children.Add(item);
    }

    private void AddFolderActionButton(MyListItem item, PixelLaunchFolderSnapshot folder)
    {
        var action = new MyIconButton
        {
            Icon = "mdi-cog",
            IconSize = 12,
            Padding = new Thickness(4),
            Width = 24,
            Height = 24
        };
        ToolTip.SetTip(action, _messages.FolderActionButtonTooltip);
        action.Click += (_, e) =>
        {
            e.Handled = true;
            ShowFolderActionMenu(action, folder);
        };
        item.AddButton(action);
    }

    private void ShowFolderActionMenu(Control anchor, PixelLaunchFolderSnapshot folder)
    {
        HidePopupOverlay();

        var stack = new StackPanel
        {
            MinWidth = 108,
            Spacing = 1
        };
        stack.Children.Add(BuildOverlayMenuButton(_messages.FolderOpenActionText, "mdi-folder-open-outline", () => _openFolder(folder.Path)));
        stack.Children.Add(BuildOverlayMenuButton(_messages.FolderRefreshActionText, "mdi-refresh", _refresh));

        var menu = new Border
        {
            Background = ThemeBrushes.Surface,
            BorderBrush = ThemeBrushes.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Child = stack
        };
        _popupOverlay.Children.Add(menu);
        _popupOverlay.IsHitTestVisible = true;

        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var point = anchor.TranslatePoint(new Point(anchor.Bounds.Width, anchor.Bounds.Height), _popupOverlay) ?? default;
        var fallback = _getFallbackOverlaySize();
        var overlayWidth = _popupOverlay.Bounds.Width > 0 ? _popupOverlay.Bounds.Width : fallback.Width;
        var overlayHeight = _popupOverlay.Bounds.Height > 0 ? _popupOverlay.Bounds.Height : fallback.Height;
        var left = Math.Clamp(point.X, 8, Math.Max(8, overlayWidth - menu.DesiredSize.Width - 8));
        var top = Math.Clamp(point.Y, 8, Math.Max(8, overlayHeight - menu.DesiredSize.Height - 8));
        Canvas.SetLeft(menu, left);
        Canvas.SetTop(menu, top);
    }

    private Button BuildOverlayMenuButton(string text, string icon, Action action)
    {
        var iconPath = new Avalonia.Controls.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            Data = MaterialIconGeometry.TryGet(icon),
            Fill = ThemeBrushes.Text
        };
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = ThemeBrushes.Text,
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(iconPath);
        content.Children.Add(label);

        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 3),
            MinWidth = 100,
            Height = 30
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            HidePopupOverlay();
            action();
        };
        return button;
    }

    private void HidePopupOverlay()
    {
        _popupOverlay.Children.Clear();
        _popupOverlay.IsHitTestVisible = false;
    }
}
