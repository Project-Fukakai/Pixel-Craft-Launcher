using System;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private LaunchInstancesPlatformBridge? _launchInstancesBridge;

    private LaunchInstancesPlatformBridge GetLaunchInstancesBridge()
    {
        return _launchInstancesBridge ??= new LaunchInstancesPlatformBridge(
            _instanceViewModel,
            _launchViewModel,
            _launchFolderService,
            _launchSidebarService,
            StorageProvider,
            RunLaunchRefreshSuppressed,
            RefreshLaunchPage,
            RefreshLaunchInstanceRouteContent,
            ShowHint);
    }

    private Control BuildLaunchInstanceLeftPage()
    {
        GetLaunchInstancesBridge().EnsureSelectedFolder();
        var messages = _launchSidebarService.GetPageMessages();
        return new LaunchInstanceSidebarView(
            _instanceViewModel.GetLaunchFolderSnapshots(_launchFolderService),
            messages,
            path =>
            {
                GetLaunchInstancesBridge().SelectFolder(path);
                SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
            },
            () => _ = GetLaunchInstancesBridge().AddOrImportFolderAsync(),
            GetLaunchInstancesBridge().OpenFolder,
            RefreshLaunchInstanceRoute,
            PanPopupOverlay,
            () => new Size(Bounds.Width, Bounds.Height));
    }

    private void RefreshLaunchInstanceRoute()
    {
        GetLaunchInstancesBridge().RefreshInstances();
        RefreshLaunchInstanceRouteContent();
    }

    private void RefreshLaunchInstanceRouteContent()
    {
        SetPageHostContent(LeftContentHost, BuildLaunchInstanceLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
    }

    private Control BuildLaunchInstanceSelectionPage()
    {
        GetLaunchInstancesBridge().EnsureSelectedFolder();
        var snapshot = _launchViewModel.GetInstanceSelectionPageSnapshot(
            _launchInstanceListService,
            GetLaunchInstancesBridge().SelectedFolder);
        var selectedFolder = _launchViewModel.GetSelectedInstanceFolderSnapshot(_launchSidebarService);
        var actionVisibility = _launchSidebarService.GetInstanceActions();
        var messages = _launchSidebarService.GetPageMessages();
        return new LaunchInstanceSelectionPageView(
            snapshot,
            actionVisibility,
            messages,
            selectedFolder.CanOpen,
            GetLaunchInstancesBridge().RefreshRouteWithSuccessHint,
            GetLaunchInstancesBridge().OpenSelectedInstanceFolder,
            GetLaunchInstancesBridge().SelectInstance,
            GetLaunchInstancesBridge().OpenFolder,
            GetLaunchInstancesBridge().OpenChildFolder);
    }

    private void RunLaunchRefreshSuppressed(Action action)
    {
        _suppressLaunchRefresh = true;
        try
        {
            action();
        }
        finally
        {
            _suppressLaunchRefresh = false;
        }
    }
}
