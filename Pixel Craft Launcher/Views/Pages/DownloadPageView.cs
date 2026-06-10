using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadPageView(
    PixelDownloadViewModel downloadViewModel,
    DownloadPagePlatformBridge platformBridge,
    Func<PixelLoadingTriggerAdapter> getVersionLoadingState,
    Func<bool> isDarkMode,
    Action closeInstallSelection,
    Action<int> navigateCategory,
    Action<int> refreshCategory,
    Action requestInstallSidebarRefresh,
    Action<string> navigateInstallSelection,
    Action<string?> openExternalUrl,
    Action<string> showInfoHint)
{
    public Control BuildLeftPage(
        int selectedDownloadPage,
        bool isSecondaryPage,
        bool isTaskRoute)
    {
        return new DownloadLeftPageFactory(
            downloadViewModel,
            selectedDownloadPage,
            isSecondaryPage,
            isTaskRoute,
            isDarkMode(),
            closeInstallSelection,
            navigateCategory,
            refreshCategory,
            requestInstallSidebarRefresh)
            .Build();
    }

    public Control BuildManagerLeftPage(int selectedDownloadPage)
    {
        return new DownloadLeftPageFactory(
            downloadViewModel,
            selectedDownloadPage,
            isSecondaryPage: true,
            isTaskRoute: true,
            isDarkMode(),
            closeInstallSelection,
            navigateCategory,
            refreshCategory,
            requestInstallSidebarRefresh)
            .BuildManagerStatsPage();
    }

    public Control BuildRightPage(
        int selectedDownloadPage,
        bool isInstallRoute,
        bool isTaskRoute)
    {
        return new DownloadRightPageFactory(
            downloadViewModel,
            selectedDownloadPage,
            isInstallRoute,
            isTaskRoute,
            getVersionLoadingState,
            platformBridge.BuildInstanceManagementPanel,
            platformBridge.PickSaveFolderAsync,
            navigateInstallSelection,
            openExternalUrl,
            showInfoHint)
            .Build();
    }
}
