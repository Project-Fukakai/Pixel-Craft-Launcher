using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchInstancesPlatformBridge(
    PixelInstanceViewModel instanceViewModel,
    PixelLaunchViewModel launchViewModel,
    PixelLaunchFolderService launchFolderService,
    PixelLaunchSidebarService launchSidebarService,
    IStorageProvider storageProvider,
    Action<Action> runLaunchRefreshSuppressed,
    Action<string?> refreshLaunchPage,
    Action refreshLaunchInstanceRoute,
    Action<string, HintType> showHint)
{
    public string SelectedFolder => launchFolderService.SelectedFolder;

    public void EnsureSelectedFolder()
    {
        instanceViewModel.EnsureLaunchSelectedFolder(launchFolderService);
    }

    public void SelectFolder(string path)
    {
        launchFolderService.SelectedFolder = path;
    }

    public void RefreshInstances()
    {
        instanceViewModel.Refresh();
        runLaunchRefreshSuppressed(launchViewModel.RefreshInstances);
        EnsureSelectedFolder();
    }

    public void RefreshRoute()
    {
        RefreshInstances();
        refreshLaunchInstanceRoute();
    }

    public void RefreshRouteWithSuccessHint()
    {
        RefreshRoute();
        showHint(launchSidebarService.GetPageMessages().InstancesRefreshSuccess, HintType.Finish);
    }

    public void SelectInstance(string path)
    {
        runLaunchRefreshSuppressed(() => launchViewModel.SelectInstanceByPath(path));
        refreshLaunchPage(nameof(PixelLaunchViewModel.Summary));
    }

    public void OpenSelectedInstanceFolder()
    {
        var folder = launchViewModel.GetSelectedInstanceFolderSnapshot(launchSidebarService);
        if (!folder.CanOpen || string.IsNullOrWhiteSpace(folder.VersionDirectory))
            return;
        OpenFolder(folder.VersionDirectory);
    }

    public void OpenFolder(string path)
    {
        try
        {
            instanceViewModel.OpenFolder(path);
        }
        catch (Exception ex)
        {
            showHint(PixelLaunchSidebarService.GetOpenFolderFailedMessage(ex), HintType.Critical);
        }
    }

    public void OpenChildFolder(string path, string childFolder)
    {
        instanceViewModel.OpenChildFolder(path, childFolder);
    }

    public async Task AddOrImportFolderAsync()
    {
        var messages = launchSidebarService.GetPageMessages();
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = messages.AddFolderPickerTitle,
            AllowMultiple = false
        });
        var folder = result.FirstOrDefault();
        if (folder is null)
            return;

        var path = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            showHint(messages.FolderPathUnavailable, HintType.Critical);
            return;
        }

        SelectFolder(launchFolderService.AddCustomFolder(path));
        RefreshRoute();
        showHint(messages.FolderAdded, HintType.Finish);
    }
}
