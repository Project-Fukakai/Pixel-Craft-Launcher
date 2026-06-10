using System;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkRuntimePlatformBridge(
    PixelGameLinkViewModel gameLinkViewModel,
    PixelGameLinkToolsPageController gameLinkToolsController,
    Action ensureEasyTierInstalled,
    Action<string, HintType> showHint,
    Action<string, string> showMessage,
    Action<bool> refreshGameLinkPage) : IDisposable
{
    private IDisposable? _runtimeSubscription;

    public void EnsureSubscribed()
    {
        if (_runtimeSubscription is not null)
            return;

        var callbacks = new PixelGameLinkRuntimeCallbacks(
            HandleRuntimeRefresh,
            ensureEasyTierInstalled,
            () => { },
            () => { },
            () => { },
            _ => { });

        _runtimeSubscription = gameLinkToolsController.SubscribeRuntimeEvents(callbacks);
    }

    public void Dispose()
    {
        _runtimeSubscription?.Dispose();
        _runtimeSubscription = null;
    }

    private void HandleRuntimeRefresh(bool rebuild)
    {
        var presentation = gameLinkViewModel.ConsumeRuntimeRefreshPresentation(rebuild);
        if (presentation.Notification is { } notification)
        {
            if (notification.Kind == PixelGameLinkRuntimeNotificationKind.CriticalHint)
                showHint(notification.Message, HintType.Critical);
            else
                showMessage(notification.Title, notification.Message);
        }

        refreshGameLinkPage(presentation.Rebuild);
    }
}
