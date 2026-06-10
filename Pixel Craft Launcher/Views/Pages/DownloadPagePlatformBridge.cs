using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadPagePlatformBridge(
    PixelInstanceViewModel instanceViewModel,
    PixelLaunchViewModel launchViewModel,
    IStorageProvider storageProvider,
    Action refreshDownloadRightPage)
{
    public async Task<string?> PickSaveFolderAsync(string title)
    {
        var selected = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return selected.Count > 0 ? selected[0].TryGetLocalPath() : null;
    }

    public Control BuildInstanceManagementPanel()
    {
        instanceViewModel.Refresh();
        var snapshot = instanceViewModel.GetManagementSnapshot(12);
        return new DownloadInstanceManagementView(
            snapshot,
            path => instanceViewModel.SelectInstanceByPath(path),
            path =>
            {
                instanceViewModel.SelectInstanceByPath(path);
                instanceViewModel.OpenSelectedFolder();
            },
            () =>
            {
                launchViewModel.RefreshInstances();
                instanceViewModel.Refresh();
                refreshDownloadRightPage();
            },
            childFolder => instanceViewModel.OpenSelectedFolder(childFolder));
    }
}
