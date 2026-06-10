using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class SetupSidebarView : UserControl
{
    public SetupSidebarView(
        PixelSetupSidebarSnapshot snapshot,
        Action<PixelSettingSectionKind> selectSection,
        Action<PixelSettingSectionKind> resetSection)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        foreach (var group in snapshot.Groups)
        {
            if (!group.HasItems)
                continue;

            AddCategory(stack, group.Title);
            foreach (var item in group.Items)
                AddItem(stack, item, snapshot.ResetButtonTooltip, selectSection, resetSection);
        }

        Content = new MyScrollViewer { Content = stack };
    }

    private static void AddCategory(StackPanel stack, string title)
    {
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(13, 10, 5, 4),
            Opacity = 0.6,
            FontSize = 12,
            Foreground = ThemeBrushes.Text
        });
    }

    private static void AddItem(
        StackPanel stack,
        PixelSetupSidebarItemSnapshot snapshot,
        string resetButtonTooltip,
        Action<PixelSettingSectionKind> selectSection,
        Action<PixelSettingSectionKind> resetSection)
    {
        var item = new MyListItem
        {
            Title = snapshot.Title,
            Info = snapshot.Info,
            Type = MyListItem.CheckType.RadioBox,
            Checked = snapshot.IsSelected,
            Icon = snapshot.Icon,
            Height = 36,
            IsSidebarItem = true,
            Tag = snapshot.Section
        };
        item.Check += (_, _) => selectSection(snapshot.Section);

        var resetButton = new MyIconButton
        {
            Icon = "mdi-restore",
            IconSize = 11,
            Padding = new Thickness(4),
            Width = 22,
            Height = 22,
            Tag = snapshot.Section
        };
        ToolTip.SetTip(resetButton, resetButtonTooltip);
        resetButton.Click += (_, _) => resetSection(snapshot.Section);
        item.AddButton(resetButton);

        stack.Children.Add(item);
    }
}
