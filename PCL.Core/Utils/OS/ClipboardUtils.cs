using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace PCL.Core.Utils.OS;

public static class ClipboardUtils
{
    [Obsolete("Use SetClipboardFilesAsync(TopLevel, string[]) so Avalonia can access the platform clipboard.")]
    public static void SetClipboardFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("Paths cannot be null or empty.", nameof(paths));
    }

    public static async Task SetClipboardFilesAsync(TopLevel topLevel, string[] paths)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("Paths cannot be null or empty.", nameof(paths));
        if (topLevel.Clipboard is null)
            throw new InvalidOperationException("当前 Avalonia TopLevel 未提供剪贴板服务。");

        var storageItems = new List<IStorageItem>();
        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path);
            IStorageItem? item = File.Exists(fullPath)
                ? await topLevel.StorageProvider.TryGetFileFromPathAsync(fullPath).ConfigureAwait(false)
                : Directory.Exists(fullPath)
                    ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(fullPath).ConfigureAwait(false)
                    : null;
            if (item is not null) storageItems.Add(item);
        }

        if (storageItems.Count == 0)
            throw new FileNotFoundException("没有可写入剪贴板的有效文件或文件夹。");

        await topLevel.Clipboard.SetFilesAsync(storageItems).ConfigureAwait(false);
    }
}
