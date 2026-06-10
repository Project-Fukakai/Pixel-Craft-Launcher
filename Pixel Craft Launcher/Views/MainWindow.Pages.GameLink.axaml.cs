using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private GameLinkToolsPlatformBridge? _gameLinkToolsBridge;
    private GameLinkDialogPlatformBridge? _gameLinkDialogBridge;
    private GameLinkAnnouncementPlatformBridge? _gameLinkAnnouncementBridge;
    private GameLinkRuntimePlatformBridge? _gameLinkRuntimeBridge;
    private GameLinkToolsNavigationBridge? _gameLinkToolsNavigationBridge;

    private void DisposeGameLinkSubscriptions()
    {
        _gameLinkAnnouncementBridge?.Dispose();
        _gameLinkAnnouncementBridge = null;
        _gameLinkRuntimeBridge?.Dispose();
        _gameLinkRuntimeBridge = null;
    }

    private GameLinkToolsPlatformBridge GetGameLinkToolsBridge()
    {
        return _gameLinkToolsBridge ??= new GameLinkToolsPlatformBridge(
            _gameLinkViewModel,
            _gameLinkToolsController,
            ShowHint,
            (title, message) => ShowMessage(title, message),
            RefreshToolsGameLinkPage);
    }

    private GameLinkDialogPlatformBridge GetGameLinkDialogBridge()
    {
        return _gameLinkDialogBridge ??= new GameLinkDialogPlatformBridge(
            _gameLinkViewModel,
            _gameLinkToolsController,
            () => Clipboard,
            ShowHint,
            (title, message) => ShowMessage(title, message),
            ShowMessageWithActions,
            ShowFormDialog,
            () => RefreshToolsGameLinkPage());
    }

    private GameLinkAnnouncementPlatformBridge GetGameLinkAnnouncementBridge()
    {
        return _gameLinkAnnouncementBridge ??= new GameLinkAnnouncementPlatformBridge(
            _gameLinkViewModel,
            _gameLinkToolsController,
            () => SelectedMainPage == MainPageKind.Tools,
            () => RefreshToolsGameLinkPage());
    }

    private GameLinkRuntimePlatformBridge GetGameLinkRuntimeBridge()
    {
        return _gameLinkRuntimeBridge ??= new GameLinkRuntimePlatformBridge(
            _gameLinkViewModel,
            _gameLinkToolsController,
            () => _ = GetGameLinkToolsBridge().EnsureEasyTierInstalledAsync(true),
            ShowHint,
            (title, message) => ShowMessage(title, message),
            RefreshToolsGameLinkPage);
    }

    private GameLinkToolsNavigationBridge GetGameLinkToolsNavigationBridge()
    {
        return _gameLinkToolsNavigationBridge ??= new GameLinkToolsNavigationBridge(
            _gameLinkViewModel,
            RefreshToolsGameLinkPage,
            () => SetPageHostContent(
                RightContentHost,
                BuildControlsPreviewContentPage(),
                PageHostUpdateMode.SilentRefresh));
    }

    private Control BuildGameLinkToolsPage()
    {
        EnsureGameLinkToolsInitialized();

        return new GameLinkToolsPageFactory(
                _gameLinkViewModel,
                GetGameLinkToolsBridge,
                GetGameLinkDialogBridge,
                GetClipboardTextCompatAsync,
                ShowHint,
                OpenExternalUrl,
                AcceptGameLinkEula,
                RefreshGameLinkWorldsAsync,
                () => RefreshToolsGameLinkPage(true),
                () => SetPageHostContent(RightContentHost, BuildControlsPreviewContentPage(), PageHostUpdateMode.SilentRefresh))
            .Build(_shellVisibilityService.IsToolVisible(PixelToolFeature.Test));
    }

    private void EnsureGameLinkToolsInitialized()
    {
        if (!_gameLinkViewModel.TryBeginInitialization())
            return;

        GetGameLinkRuntimeBridge().EnsureSubscribed();
        _ = InitializeGameLinkAsync();
    }

    private async Task InitializeGameLinkAsync()
    {
        await GetGameLinkAnnouncementBridge().LoadAnnouncementsAsync();
        await _gameLinkToolsController.InitializeLobbyAsync();
        GetGameLinkToolsBridge().TryStartEasyTierAutoInstall();
        RefreshToolsGameLinkPage();
    }

    private void AcceptGameLinkEula()
    {
        _gameLinkViewModel.AcceptEula();
        GetGameLinkToolsBridge().TryStartEasyTierAutoInstall();
        RefreshToolsGameLinkPage(true);
    }

    private async Task RefreshGameLinkWorldsAsync()
    {
        await _gameLinkToolsController.DiscoverWorldsAsync();
        RefreshToolsGameLinkPage();
    }

    private void RefreshToolsGameLinkPage(bool refreshLeft = false)
    {
        if (SelectedMainPage != MainPageKind.Tools)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedMainPage == MainPageKind.Tools)
            {
                if (refreshLeft)
                    SetPageHostContent(LeftContentHost, BuildLeftPage(MainPageKind.Tools), PageHostUpdateMode.SilentRefresh);
                SetPageHostContent(RightContentHost, BuildGameLinkToolsPage(), PageHostUpdateMode.SilentRefresh);
            }
        }, DispatcherPriority.Background);
    }

    private Task<string?> GetClipboardTextCompatAsync() =>
        ClipboardCompatBridge.GetTextAsync(Clipboard);
}
