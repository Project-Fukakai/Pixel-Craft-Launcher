using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Core.App.Pixel.Slices.Profiles;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ProfileOperationPlatformBridge(
    PixelProfilePageService profilePageService,
    Action refreshProfileBindings,
    Action refreshProfileManagerPage,
    Action<string, HintType> showHint,
    Func<TextBlock, PixelProfileLoginProgressAdapter> createLoginProgress,
    Func<PixelProfileUiCallbacks> createCallbacks)
{
    public async Task RunWithBusyStateAsync(Func<Task> operation, params Control[] controls)
    {
        foreach (var control in controls)
            control.IsEnabled = false;
        try
        {
            await operation();
        }
        finally
        {
            foreach (var control in controls)
                control.IsEnabled = true;
        }
    }

    public async Task SaveOfflineProfileAsync(
        PixelOfflineProfileEditorSnapshot editor,
        int selectedModeIndex,
        string username,
        string? uuid,
        TextBlock? status = null,
        Action? closeDialog = null)
    {
        var selectedMode = selectedModeIndex switch
        {
            1 => PixelOfflineUuidMode.Legacy,
            2 => PixelOfflineUuidMode.Custom,
            _ => PixelOfflineUuidMode.Standard
        };
        var result = await profilePageService.SaveOfflineProfileAsync(editor.EditingProfileId, username, selectedMode, uuid);
        ApplyResult(result, status, closeDialog);
    }

    public async Task AddMicrosoftProfileAsync(TextBlock status, Action? closeDialog = null)
    {
        var result = await profilePageService.AddMicrosoftProfileAsync(createCallbacks(), createLoginProgress(status));
        ApplyResult(result, status, closeDialog);
    }

    public async Task AddAuthlibProfileAsync(
        PixelAuthlibProfileFormSnapshot form,
        string username,
        string password,
        TextBlock status,
        Action? closeDialog = null)
    {
        var result = await profilePageService.AddAuthlibProfileAsync(
            form.ApiRoot,
            username,
            password,
            createCallbacks(),
            createLoginProgress(status));
        ApplyResult(result, status, closeDialog);
    }

    public async Task AddAuthServerAsync(
        string name,
        string apiRoot,
        string registerUrl,
        TextBlock? status = null,
        Action? closeDialog = null)
    {
        var result = await profilePageService.AddAuthServerAsync(name, apiRoot, registerUrl);
        ApplyResult(result, status, closeDialog, refreshBindings: false);
    }

    public async Task SelectProfileAsync(string profileId)
    {
        await profilePageService.SelectProfileAsync(profileId);
        refreshProfileBindings();
        refreshProfileManagerPage();
    }

    public async Task CopyProfileUuidAsync(object? clipboard, PixelProfileItemSnapshot snapshot)
    {
        await ClipboardCompatBridge.SetTextAsync(clipboard, snapshot.Uuid);
        showHint(profilePageService.GetPageMessages().UuidCopied, HintType.Finish);
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await profilePageService.RemoveProfileAsync(profileId);
        refreshProfileBindings();
        refreshProfileManagerPage();
        showHint(profilePageService.GetPageMessages().ProfileDeleted, HintType.Info);
    }

    private void ApplyResult(
        PixelProfileOperationResultSnapshot result,
        TextBlock? status,
        Action? closeDialog,
        bool refreshBindings = true)
    {
        if (!result.IsSuccess)
        {
            if (status is not null)
                status.Text = result.StatusText;
            showHint(result.HintText, HintType.Critical);
            return;
        }

        if (refreshBindings)
            refreshProfileBindings();
        closeDialog?.Invoke();
        refreshProfileManagerPage();
        showHint(result.HintText, HintType.Finish);
    }
}
