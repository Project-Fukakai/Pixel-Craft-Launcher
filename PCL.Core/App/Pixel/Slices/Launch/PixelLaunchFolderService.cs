using System.Collections.Generic;
using System.IO;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Minecraft;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed class PixelLaunchFolderService
{
    public string SelectedFolder
    {
        get => States.Game.SelectedFolder;
        set => States.Game.SelectedFolder = value;
    }

    internal void EnsureSelectedFolder(IReadOnlyList<MinecraftFolderInfo> folders)
    {
        if (!string.IsNullOrWhiteSpace(SelectedFolder) &&
            folders.Any(folder => string.Equals(folder.Path, SelectedFolder, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedFolder = folders.FirstOrDefault()?.Path ?? string.Empty;
    }

    internal IReadOnlyList<PixelLaunchFolderSnapshot> GetFolderSnapshots(IReadOnlyList<MinecraftFolderInfo> folders)
    {
        return folders
            .Select(folder => new PixelLaunchFolderSnapshot(
                folder.Path,
                GetFolderTitle(folder),
                folder.IsDefault ? "mdi-folder-home-outline" : "mdi-folder-outline",
                IsSelected(folder.Path)))
            .ToArray();
    }

    public bool IsSelected(string path) =>
        string.Equals(SelectedFolder, path, StringComparison.OrdinalIgnoreCase);

    internal string GetFolderTitle(MinecraftFolderInfo folder)
    {
        if (folder.IsDefault && folder.IsCustom)
            return "默认 / 自定义文件夹";
        if (folder.IsDefault)
        {
            if (string.Equals(folder.Path, GetBundledMinecraftFolder(), StringComparison.OrdinalIgnoreCase))
                return "当前文件夹";

            return "官方启动器文件夹";
        }

        var name = Path.GetFileName(folder.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "自定义文件夹" : name;
    }

    public string AddCustomFolder(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var folders = GetCustomFolders().ToList();
        if (folders.All(folder => !string.Equals(Path.GetFullPath(folder), fullPath, StringComparison.OrdinalIgnoreCase)))
            folders.Add(fullPath);
        States.Game.Folders = string.Join('|', folders);
        return fullPath;
    }

    public IReadOnlyList<string> GetCustomFolders()
    {
        return States.Game.Folders
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExpandExecutableToken)
            .ToArray();
    }

    private static string ExpandExecutableToken(string folder) =>
        folder.Replace("$", Basics.ExecutableDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string GetBundledMinecraftFolder() =>
        Path.GetFullPath(Path.Combine(Basics.ExecutableDirectory, ".minecraft"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

public sealed record PixelLaunchFolderSnapshot(
    string Path,
    string Title,
    string Icon,
    bool IsSelected);
