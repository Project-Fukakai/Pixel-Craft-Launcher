using System;
using System.Threading.Tasks;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkToolsPlatformBridge(
    PixelGameLinkViewModel gameLinkViewModel,
    PixelGameLinkToolsPageController gameLinkToolsController,
    Action<string, HintType> showHint,
    Action<string, string> showMessage,
    Action<bool> refreshGameLinkPage)
{
    public void TryStartEasyTierAutoInstall()
    {
        if (!gameLinkViewModel.ShouldStartEasyTierAutoInstall())
            return;

        _ = EnsureEasyTierInstalledAsync(false);
    }

    public async Task HandleNatayarkLoginClickAsync()
    {
        if (gameLinkToolsController.GetNatayarkLoginStartMessage() is { } startMessage)
            showHint(startMessage, HintType.Info);

        var result = await gameLinkToolsController.ToggleNatayarkLoginAsync();
        ShowNotification(result.Notification);
        refreshGameLinkPage(false);
    }

    public async Task RunToolsNatTestAsync()
    {
        var result = await gameLinkToolsController.RunToolsNatTestAsync();
        if (result.Dialog is { } dialog)
            showMessage(dialog.Title, dialog.Body);
        else if (!string.IsNullOrWhiteSpace(result.Message))
            showHint(result.Message, HintType.Critical);
    }

    public async Task EnsureEasyTierInstalledAsync(bool showResult)
    {
        refreshGameLinkPage(true);
        var result = await gameLinkToolsController.EnsureEasyTierInstalledAsync();
        if (showResult)
            ShowNotification(result.Notification);
        refreshGameLinkPage(true);
    }

    public async Task CreateLobbyAsync(int port)
    {
        var operation = gameLinkToolsController.CreateLobbyAsync(port);
        refreshGameLinkPage(true);
        var controllerResult = await operation;
        ShowNotification(gameLinkToolsController.GetOperationFailureNotification(controllerResult));
        refreshGameLinkPage(true);
    }

    public async Task JoinLobbyAsync(string lobbyCode)
    {
        var operation = gameLinkToolsController.JoinLobbyAsync(lobbyCode);
        refreshGameLinkPage(true);
        var controllerResult = await operation;
        ShowNotification(gameLinkToolsController.GetOperationFailureNotification(controllerResult));
        refreshGameLinkPage(true);
    }

    public async Task LeaveLobbyAsync()
    {
        var operation = gameLinkToolsController.LeaveLobbyAsync();
        refreshGameLinkPage(true);
        await operation;
    }

    private void ShowNotification(PixelGameLinkNotificationSnapshot? notification)
    {
        if (notification is null)
            return;

        showHint(notification.Message, notification.Kind switch
        {
            PixelGameLinkNotificationKind.Success => HintType.Finish,
            PixelGameLinkNotificationKind.Critical => HintType.Critical,
            _ => HintType.Info
        });
    }
}
