using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadRightPageFactory(
    PixelDownloadViewModel downloadViewModel,
    int selectedDownloadPage,
    bool isInstallRoute,
    bool isTaskRoute,
    Func<PixelLoadingTriggerAdapter> getVersionLoadingState,
    Func<Control> buildInstanceManagementPanel,
    Func<string, Task<string?>> pickSaveFolder,
    Action<string> navigateInstallSelection,
    Action<string?> openExternalUrl,
    Action<string> showInfoHint)
{
    private readonly PixelDownloadPageMessages _messages = downloadViewModel.GetPageMessagesSnapshot();

    public Control Build()
    {
        var presentation = downloadViewModel.GetRightPagePresentation(selectedDownloadPage, isInstallRoute, isTaskRoute);
        _ = downloadViewModel.RefreshRightPageDataAsync(presentation);

        return presentation.Kind switch
        {
            PixelDownloadRightPageKind.TaskDetails => BuildTaskDetailsPage(),
            PixelDownloadRightPageKind.InstallSelection => BuildInstallSelectionPage(),
            PixelDownloadRightPageKind.InstallVersionList => BuildVersionListView(installMode: true),
            PixelDownloadRightPageKind.ClientVersionList => BuildVersionListView(installMode: false),
            PixelDownloadRightPageKind.Loading => BuildLoadingPage(),
            _ => BuildPendingPage()
        };
    }

    private DownloadVersionListView BuildVersionListView(bool installMode)
    {
        return new DownloadVersionListView(
            downloadViewModel,
            installMode,
            versionId =>
            {
                downloadViewModel.OpenInstallSelection(versionId);
                navigateInstallSelection(versionId);
            },
            versionId => downloadViewModel.InstallVanillaVersionAsync(versionId),
            (versionId, folder) => downloadViewModel.SaveClientCoreAsync(versionId, folder),
            (versionId, folder) => downloadViewModel.SaveServerJarAsync(versionId, folder),
            pickSaveFolder,
            wikiSuffix => openExternalUrl("https://zh.minecraft.wiki/w/Special:Search?search=" + wikiSuffix),
            _messages.VersionList);
    }

    private Control BuildLoadingPage()
    {
        return new DownloadLoadingPageView(
            _messages.VersionLoadingText,
            getVersionLoadingState());
    }

    private Control BuildPendingPage()
    {
        return new DownloadPendingPageView(
            downloadViewModel.GetPendingPageSnapshot(selectedDownloadPage),
            buildInstanceManagementPanel());
    }

    private Control BuildInstallSelectionPage()
    {
        return new DownloadInstallPanelView(
            downloadViewModel,
            _messages.InstallPanel,
            () => downloadViewModel.RefreshLoaderChoicesAsync());
    }

    private Control BuildTaskDetailsPage()
    {
        return new DownloadTaskDetailsView(
            downloadViewModel,
            _messages.TaskDetails,
            showInfoHint);
    }
}
