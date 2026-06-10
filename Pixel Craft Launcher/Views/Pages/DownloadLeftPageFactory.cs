using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadLeftPageFactory(
    PixelDownloadViewModel downloadViewModel,
    int selectedDownloadPage,
    bool isSecondaryPage,
    bool isTaskRoute,
    bool isDarkMode,
    Action closeInstallSelection,
    Action<int> navigateCategory,
    Action<int> refreshCategory,
    Action requestInstallSidebarRefresh)
{
    private readonly PixelDownloadPageMessages _messages = downloadViewModel.GetPageMessagesSnapshot();

    public Control Build()
    {
        if (isSecondaryPage)
            return isTaskRoute ? BuildManagerStatsPage() : BuildInstallSidebarPage();

        return new DownloadSidebarView(
            downloadViewModel.GetSidebarSnapshot(selectedDownloadPage),
            downloadViewModel.IsInstallSelectionOpen,
            closeInstallSelection,
            navigateCategory,
            refreshCategory);
    }

    public Control BuildManagerStatsPage()
    {
        return new DownloadManagerStatsView(
            downloadViewModel,
            _messages.ManagerStats);
    }

    private Control BuildInstallSidebarPage()
    {
        return new DownloadInstallSidebarView(
            downloadViewModel,
            _messages.InstallSidebar,
            isDarkMode,
            requestInstallSidebarRefresh);
    }
}
