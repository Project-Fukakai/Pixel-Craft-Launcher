using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkEasyTierCardView : MyCard
{
    public GameLinkEasyTierCardView(
        PixelGameLinkEasyTierSnapshot snapshot,
        Func<Task> install,
        Action check)
    {
        Title = snapshot.Title;
        CanSwap = false;

        var installButton = CreateActionButton(snapshot.InstallText, "mdi-download");
        installButton.IsEnabled = snapshot.CanInstall;
        installButton.Click += async (_, _) =>
        {
            installButton.IsEnabled = false;
            try
            {
                await install();
            }
            finally
            {
                installButton.IsEnabled = snapshot.CanInstall;
            }
        };

        var checkButton = CreateActionButton(snapshot.CheckButtonText, "mdi-refresh");
        checkButton.Click += (_, _) => check();

        CardContent = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = snapshot.StateText, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = snapshot.Details, TextWrapping = TextWrapping.Wrap },
                new WrapPanel { Children = { installButton, checkButton } }
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
}
