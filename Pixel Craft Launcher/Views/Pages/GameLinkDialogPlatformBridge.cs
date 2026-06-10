using System;
using System.Threading.Tasks;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views.Pages;

public delegate void GameLinkShowMessageWithActions(
    string title,
    string markdown,
    bool isWarn = false,
    string? button1 = null,
    string? button2 = null,
    string? button3 = null,
    Action? onButton1 = null,
    Action? onButton2 = null,
    Action? onButton3 = null);

public sealed class GameLinkDialogPlatformBridge(
    PixelGameLinkViewModel gameLinkViewModel,
    PixelGameLinkToolsPageController controller,
    Func<object?> getClipboard,
    Action<string, HintType> showHint,
    Action<string, string> showMessage,
    GameLinkShowMessageWithActions showMessageWithActions,
    Func<string, string?, string?, MyMsgForm> showFormDialog,
    Action refreshGameLinkPage)
{
    public async Task CopyLobbyCodeAsync(PixelGameLinkFinishSnapshot finish)
    {
        var code = gameLinkViewModel.CurrentLobbyCode;
        if (!string.IsNullOrWhiteSpace(code))
            await ClipboardCompatBridge.SetTextAsync(getClipboard(), code);
        showHint(finish.LobbyCodeCopiedMessage, HintType.Finish);
    }

    public void ShowVirtualIpDialog(PixelGameLinkFinishSnapshot finish, string ip)
    {
        var dialog = finish.VirtualIpDialog;
        if (dialog is null)
            return;

        showMessageWithActions(
            dialog.Title,
            dialog.Body,
            button1: dialog.CopyButtonText,
            button2: dialog.BackButtonText,
            onButton1: async () => await ClipboardCompatBridge.SetTextAsync(getClipboard(), ip));
    }

    public void ShowPlayerDetails(PixelGameLinkPlayerSnapshot player)
    {
        showMessage(player.DetailTitle, player.DetailBody);
    }

    public void ConfirmDisableGameLink(PixelGameLinkFooterSnapshot footer)
    {
        showMessageWithActions(
            footer.DisableConfirmation.Title,
            footer.DisableConfirmation.Body,
            true,
            button1: footer.DisableConfirmation.PrimaryButtonText,
            button2: footer.DisableConfirmation.SecondaryButtonText,
            onButton1: async () => await ResetAuthorizationAsync());
    }

    private async Task ResetAuthorizationAsync()
    {
        var result = await controller.ResetAuthorizationAsync();
        showHint(result.Notification.Message, ToHintType(result.Notification.Kind));
        if (result.IsSuccess)
            refreshGameLinkPage();
    }

    public void ShowManualPortDialog(PixelGameLinkCreateCardSnapshot createCard, Func<int, Task> createLobby)
    {
        var dialogSnapshot = createCard.ManualPortDialog;
        var input = new MyTextBox
        {
            HintText = dialogSnapshot.InputHint,
            Width = 220
        };
        input.ValidateRules.Add(text =>
            int.TryParse(text, out var port) && port is >= 1024 and <= 65535
                ? null
                : dialogSnapshot.ValidationErrorText);

        var dialog = showFormDialog(dialogSnapshot.Title, dialogSnapshot.CreateButtonText, dialogSnapshot.CancelButtonText);
        dialog.ContentPanel.Children.Add(input);
        dialog.Button1Click += async (_, _) =>
        {
            if (!int.TryParse(input.Text, out var port) || port is < 1024 or > 65535)
            {
                showHint(dialogSnapshot.ValidationErrorText, HintType.Critical);
                return;
            }

            await createLobby(port);
        };
    }

    private static HintType ToHintType(PixelGameLinkNotificationKind kind) =>
        kind switch
        {
            PixelGameLinkNotificationKind.Success => HintType.Finish,
            PixelGameLinkNotificationKind.Critical => HintType.Critical,
            _ => HintType.Info
        };
}
