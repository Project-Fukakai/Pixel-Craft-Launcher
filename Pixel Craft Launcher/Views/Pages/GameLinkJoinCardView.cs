using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkJoinCardView : MyCard
{
    public GameLinkJoinCardView(
        PixelGameLinkJoinCardSnapshot snapshot,
        Func<string, Task> join,
        Func<Task<string?>> paste)
    {
        Title = snapshot.Title;
        CanSwap = false;

        var input = new MyTextBox
        {
            HintText = snapshot.InputHint,
            MinWidth = 260,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var joinButton = CreateActionButton(snapshot.JoinButtonText, "mdi-login");
        joinButton.Click += async (_, _) => await RunButtonActionAsync(joinButton, async () => await join(input.Text ?? string.Empty));

        var pasteButton = CreateActionButton(snapshot.PasteButtonText, "mdi-clipboard-text-outline");
        pasteButton.Click += async (_, _) => input.Text = await paste();

        var clearButton = CreateActionButton(snapshot.ClearButtonText, "mdi-close");
        clearButton.Click += (_, _) => input.Text = string.Empty;

        var joinActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { clearButton, pasteButton, joinButton }
        };
        DockPanel.SetDock(joinActions, Dock.Right);

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
                        joinActions,
                        input
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
