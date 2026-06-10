using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkCreateCardView : MyCard
{
    public GameLinkCreateCardView(
        PixelGameLinkCreateCardSnapshot snapshot,
        Func<int, Task> create,
        Func<Task> refresh,
        Action manual)
    {
        Title = snapshot.Title;
        CanSwap = false;

        var combo = new MyComboBox
        {
            MinWidth = 260
        };
        foreach (var world in snapshot.Worlds)
            combo.Items.Add(new MyComboBoxItem { Content = world.Name, Tag = world.Port });
        combo.IsEnabled = snapshot.CanCreate;
        if (snapshot.DefaultWorldIndex >= 0)
            combo.SelectedIndex = snapshot.DefaultWorldIndex;

        var createButton = CreateActionButton(snapshot.CreateButtonText, "mdi-plus-circle-outline");
        createButton.IsEnabled = snapshot.CanCreate;
        createButton.Click += async (_, _) =>
        {
            if (combo.SelectedItem is MyComboBoxItem { Tag: int port })
                await RunButtonActionAsync(createButton, async () => await create(port));
        };

        var refreshButton = CreateActionButton(snapshot.RefreshButtonText, "mdi-refresh");
        refreshButton.Click += async (_, _) => await RunButtonActionAsync(refreshButton, refresh);

        var manualButton = CreateActionButton(snapshot.ManualButtonText, "mdi-keyboard-outline");
        manualButton.Click += (_, _) => manual();

        var createActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { manualButton, refreshButton, createButton }
        };
        DockPanel.SetDock(createActions, Dock.Right);

        CardContent = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = string.Join(Environment.NewLine, snapshot.Steps), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        createActions,
                        combo
                    }
                }
            }
        };
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

    private static async Task RunButtonActionAsync(MyButton button, Func<Task> action)
    {
        button.IsEnabled = false;
        try
        {
            await action();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
