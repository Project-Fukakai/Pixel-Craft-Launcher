using Avalonia.Controls;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private DownloadPagePlatformBridge? _downloadPageBridge;
    private DownloadPageView? _downloadPageView;

    private Control BuildDownloadRightPage()
    {
        return GetDownloadPageView().BuildRightPage(
            _selectedDownloadPage,
            IsDownloadInstallRoute(),
            IsDownloadTaskRoute());
    }

    private DownloadPageView GetDownloadPageView()
    {
        return _downloadPageView ??= new DownloadPageView(
            _downloadViewModel,
            GetDownloadPageBridge(),
            GetDownloadVersionLoadingState,
            () => _themeBridge.IsDarkMode,
            CloseInstallSelectionWithoutRefresh,
            tag => _shellViewModel.NavigateDownloadCategory(tag),
            RefreshDownloadCategory,
            () => ScheduleDownloadRightPageRefresh(refreshLeft: true),
            versionId => _shellViewModel.NavigateMinecraftInstall(versionId),
            OpenExternalUrl,
            message => ShowHint(message, HintType.Info));
    }

    private DownloadPagePlatformBridge GetDownloadPageBridge()
    {
        return _downloadPageBridge ??= new DownloadPagePlatformBridge(
            _instanceViewModel,
            _launchViewModel,
            StorageProvider,
            () => ScheduleDownloadRightPageRefresh());
    }

    private PixelLoadingTriggerAdapter GetDownloadVersionLoadingState() =>
        _downloadVersionLoadingState ??= new PixelLoadingTriggerAdapter(_downloadViewModel.VersionLoadingState);
}
