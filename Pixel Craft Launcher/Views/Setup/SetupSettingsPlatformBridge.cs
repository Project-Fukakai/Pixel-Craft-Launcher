using System;
using PCL.Core.App.Pixel;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class SetupSettingsPlatformBridge(
    PixelSettingsChangeService settingsChangeService,
    PixelSettingDisplayService settingDisplayService,
    Action refreshSetupRightPage,
    Action refreshShellTheme,
    Action<string, HintType> showHint,
    Action<string, string, bool> showMessage)
{
    private bool _isRunWaitVisible;

    public void InitializeDisplayState()
    {
        _isRunWaitVisible = settingDisplayService.IsRunWaitVisible();
    }

    public void OnSettingChanged(string? key, object? value)
    {
        var effect = settingsChangeService.Apply(key, value);
        var presentation = settingsChangeService.CreatePresentation(effect, _isRunWaitVisible);
        _isRunWaitVisible = presentation.RunWaitVisible;

        if (presentation.RefreshSetupRightPage)
            refreshSetupRightPage();
        if (presentation.RefreshShellTheme)
            refreshShellTheme();
        if (presentation.Notification is { } notification)
            ShowSettingNotification(notification);
    }

    public void ShowSettingNotification(PixelSettingNotification notification)
    {
        if (notification.Kind == PixelSettingNotificationKind.Hint)
        {
            showHint(notification.Text, HintType.Info);
            return;
        }

        var messages = settingsChangeService.GetNotificationMessages();
        showMessage(notification.Title ?? messages.DefaultMessageTitle, notification.Text, notification.IsWarning);
    }
}
