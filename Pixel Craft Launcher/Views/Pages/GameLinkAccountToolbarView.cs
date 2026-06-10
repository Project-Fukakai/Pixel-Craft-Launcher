using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkAccountToolbarView : WrapPanel
{
    public GameLinkAccountToolbarView(PixelGameLinkAccountSnapshot snapshot, Func<Task> runNatTest, Func<Task> toggleLogin)
    {
        HorizontalAlignment = HorizontalAlignment.Right;

        var natTest = CreateActionButton(snapshot.NatTestButtonText, "mdi-earth");
        natTest.Click += async (_, _) => await RunButtonActionAsync(natTest, snapshot.NatTestBusyText, runNatTest);

        var account = CreateActionButton(snapshot.Text, "mdi-account-circle-outline");
        account.Click += async (_, _) => await RunButtonActionAsync(account, snapshot.Text, toggleLogin);

        Children.Add(natTest);
        Children.Add(account);
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

    private static async Task RunButtonActionAsync(MyButton button, string busyText, Func<Task> action)
    {
        var oldText = button.Text;
        button.IsEnabled = false;
        button.Text = busyText;
        try
        {
            await action();
        }
        finally
        {
            button.Text = oldText;
            button.IsEnabled = true;
        }
    }
}
