using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel.Slices.Java;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class JavaSetupPlatformBridge(
    PixelJavaService javaService,
    IStorageProvider storageProvider,
    Action<string, HintType> showHint,
    Action<string, string> showMessage,
    Action refreshPage,
    Action<string> openPath)
{
    public async Task AddJavaAsync()
    {
        var messages = javaService.GetMessages();
        var patterns = OperatingSystem.IsWindows() ? new[] { "java.exe" } : new[] { "java", "java.exe", "*" };
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = messages.AddPickerTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(messages.AddPickerFileTypeName)
                {
                    Patterns = patterns
                }
            ]
        });
        var path = result.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await javaService.AddForPageAsync(path);
        }
        catch (Exception ex)
        {
            showHint(messages.GetOperationFailureMessage(ex), HintType.Critical);
            return;
        }

        showHint(messages.AddSuccessMessage, HintType.Finish);
        refreshPage();
    }

    public async Task RefreshJavaListAsync()
    {
        var messages = javaService.GetMessages();
        showHint(messages.RefreshStartMessage, HintType.Info);
        try
        {
            await javaService.RefreshForPageAsync();
        }
        catch (Exception ex)
        {
            showHint(messages.GetOperationFailureMessage(ex), HintType.Critical);
            return;
        }

        showHint(messages.RefreshSuccessMessage, HintType.Finish);
        refreshPage();
    }

    public async Task SelectDefaultJavaAsync(PixelJavaEntrySnapshot? snapshot)
    {
        var messages = javaService.GetMessages();
        try
        {
            await javaService.SelectDefaultForPageAsync(snapshot?.JavaExecutablePath);
        }
        catch (Exception ex)
        {
            showHint(messages.GetOperationFailureMessage(ex), HintType.Critical);
            return;
        }

        showHint(
            snapshot is null
                ? messages.AutoSelectSuccessMessage
                : messages.GetDefaultSelectedMessage(snapshot.VersionLabel),
            HintType.Finish);
    }

    public async Task ToggleJavaEntryAsync(PixelJavaEntrySnapshot snapshot)
    {
        var messages = javaService.GetMessages();
        try
        {
            var enabled = await javaService.ToggleEnabledForPageAsync(snapshot.JavaExecutablePath);
            showHint(messages.GetToggleEnabledMessage(enabled), HintType.Finish);
        }
        catch (Exception ex)
        {
            showHint(messages.GetOperationFailureMessage(ex), HintType.Critical);
            return;
        }

        refreshPage();
    }

    public void ShowJavaInfo(PixelJavaEntrySnapshot snapshot)
    {
        var messages = javaService.GetMessages();
        showMessage(messages.InfoDialogTitle, snapshot.Details);
    }

    public void OpenJavaFolder(string folder)
    {
        var messages = javaService.GetMessages();
        try
        {
            openPath(folder);
        }
        catch (Exception ex)
        {
            showHint(messages.GetOpenFolderFailedMessage(ex), HintType.Critical);
        }
    }
}
