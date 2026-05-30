using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PCL.Core.Logging;

namespace PCL.Core.UI;

public static partial class SystemDialogs
{
    private const uint WinMessageBoxIconError = 0x00000010;
    private const uint WinMessageBoxOk = 0x00000000;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int _MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static string SelectSaveFile(string title, string fileName, string? fileFilter = null, string? initialDirectory = null)
    {
        var storage = _GetStorageProvider();
        if (storage is null) return "";

        LogWrapper.Info("Dialog", $"打开保存文件对话框：{title}");
        var result = storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = fileName,
            SuggestedStartLocation = _TryGetFolder(storage, initialDirectory),
            FileTypeChoices = [_CreateFileType(fileFilter)]
        }).ConfigureAwait(false).GetAwaiter().GetResult();

        var selectedPath = result?.Path.LocalPath;
        if (string.IsNullOrEmpty(selectedPath)) return "";
        LogWrapper.Info("Dialog", $"选择文件返回：{selectedPath}");
        return Path.GetFullPath(selectedPath);
    }

    public static string SelectFile(string fileFilter, string title, string? initialDirectory = null)
    {
        var result = SelectFiles(fileFilter, title, initialDirectory, false);
        return result.Length == 0 ? "" : result[0];
    }

    public static string[] SelectFiles(string fileFilter, string title = "选择文件", string? initialDirectory = null, bool allowMultiSelect = true)
    {
        var storage = _GetStorageProvider();
        if (storage is null) return [];

        var num = allowMultiSelect ? "多" : "单";
        LogWrapper.Info("Dialog", $"打开选择{num}个文件对话框: {title}");
        var result = storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiSelect,
            SuggestedStartLocation = _TryGetFolder(storage, initialDirectory),
            FileTypeFilter = [_CreateFileType(fileFilter)]
        }).ConfigureAwait(false).GetAwaiter().GetResult();

        var selectedFiles = result.Select(x => Path.GetFullPath(x.Path.LocalPath)).ToArray();
        LogWrapper.Info("Dialog", $"选择{num}个文件返回: {string.Join(",", selectedFiles)}");
        return selectedFiles;
    }

    public static string SelectFolder(string title = "选择文件夹", string? initialDirectory = null)
    {
        var storage = _GetStorageProvider();
        if (storage is null) return "";

        LogWrapper.Info("Dialog", $"打开选择文件夹对话框: {title}");
        var result = storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = _TryGetFolder(storage, initialDirectory)
        }).ConfigureAwait(false).GetAwaiter().GetResult();

        var selectedPath = result.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(selectedPath)) return "";
        var normalizedPath = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        LogWrapper.Info("Dialog", $"选择文件夹返回: {normalizedPath}");
        return normalizedPath;
    }

    private static IStorageProvider? _GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return window.StorageProvider;
        return null;
    }

    private static IStorageFolder? _TryGetFolder(IStorageProvider storage, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return null;
        return storage.TryGetFolderFromPathAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static FilePickerFileType _CreateFileType(string? wpfFilter)
    {
        if (string.IsNullOrWhiteSpace(wpfFilter)) return FilePickerFileTypes.All;
        var parts = wpfFilter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts.Length >= 2 ? parts[0] : "文件";
        var patterns = parts.Length >= 2 ? parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : ["*.*"];
        return new FilePickerFileType(name) { Patterns = patterns };
    }

    public static bool ShowNativeMessageBox(string message, string caption, MsgBoxTheme theme = MsgBoxTheme.Info)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var icon = theme == MsgBoxTheme.Error ? WinMessageBoxIconError : 0u;
                _ = _MessageBoxW(IntPtr.Zero, message, caption, WinMessageBoxOk | icon);
                return true;
            }

            if (OperatingSystem.IsMacOS())
                return _RunNativeDialog("osascript", [
                    "-e",
                    $"display dialog {_ToAppleScriptString(message)} with title {_ToAppleScriptString(caption)} buttons {{\"OK\"}} default button \"OK\" with icon stop"
                ]);

            if (OperatingSystem.IsLinux())
            {
                if (_RunNativeDialog("zenity", ["--error", "--title", caption, "--text", message, "--no-wrap"]))
                    return true;
                if (_RunNativeDialog("kdialog", ["--error", message, "--title", caption]))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool _RunNativeDialog(string fileName, string[] arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string _ToAppleScriptString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
