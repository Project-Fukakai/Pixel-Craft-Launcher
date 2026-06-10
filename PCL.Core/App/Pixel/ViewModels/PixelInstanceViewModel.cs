using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.Minecraft;

namespace PCL.Core.App.Pixel.ViewModels;

public sealed class PixelInstanceViewModel(
    MinecraftInstanceService instanceService,
    IExternalProcessService externalProcessService)
    : PixelViewModelBase
{
    private MinecraftInstanceSummary? _selectedInstance;
    private string _statusText = "准备扫描实例";

    public PixelInstanceViewModel()
        : this(new MinecraftInstanceService(), new ExternalProcessService())
    {
    }

    internal ObservableCollection<MinecraftFolderInfo> Folders { get; } = [];

    internal ObservableCollection<MinecraftInstanceSummary> Instances { get; } = [];

    private MinecraftInstanceSummary? SelectedInstance
    {
        get => _selectedInstance;
        set => SetField(ref _selectedInstance, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public PixelInstanceManagementSnapshot GetManagementSnapshot(int limit)
    {
        return new PixelInstanceManagementSnapshot(
            Instances
                .Take(Math.Max(limit, 0))
                .Select(instance => new PixelInstanceManagementItemSnapshot(
                    instance.VersionDirectory,
                    instance.Name,
                    instance.VersionDirectory,
                    instance.IsHidden ? "mdi-eye-off-outline" : "mdi-cube-outline",
                    Equals(instance, SelectedInstance)))
                .ToArray(),
            Instances.Count == 0,
            SelectedInstance is not null,
            "未找到实例，安装完成后会自动刷新。",
            "刷新实例",
            "打开 Mods",
            "打开存档",
            "打开实例文件夹");
    }

    public IReadOnlyList<PixelLaunchFolderSnapshot> GetLaunchFolderSnapshots(PixelLaunchFolderService launchFolderService) =>
        launchFolderService.GetFolderSnapshots(Folders);

    public void EnsureLaunchSelectedFolder(PixelLaunchFolderService launchFolderService) =>
        launchFolderService.EnsureSelectedFolder(Folders);

    public void SelectInstanceByPath(string versionDirectory)
    {
        var instance = Instances.FirstOrDefault(item =>
            string.Equals(item.VersionDirectory, versionDirectory, StringComparison.Ordinal));
        if (instance is not null)
            SelectedInstance = instance;
    }

    public void Refresh()
    {
        Folders.Clear();
        foreach (var folder in instanceService.GetFolders())
            Folders.Add(folder);

        Instances.Clear();
        foreach (var instance in instanceService.GetInstances())
            Instances.Add(instance);

        SelectedInstance = Instances.FirstOrDefault();
        StatusText = $"已找到 {Instances.Count} 个实例";
    }

    public void OpenSelectedFolder(string childFolder = "")
    {
        if (SelectedInstance is null)
            return;

        OpenChildFolder(SelectedInstance.VersionDirectory, childFolder);
    }

    public void OpenChildFolder(string versionDirectory, string childFolder)
    {
        var path = string.IsNullOrWhiteSpace(childFolder)
            ? versionDirectory
            : Path.Combine(versionDirectory, childFolder);
        OpenFolder(path);
    }

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        externalProcessService.OpenPath(path);
    }

    private void SetHidden(MinecraftInstanceSummary summary, bool hidden)
    {
        var instance = instanceService.Load(summary.VersionDirectory);
        if (instance is null)
            return;

        instanceService.SetHidden(instance, hidden);
        Refresh();
    }
}

public sealed record PixelInstanceManagementSnapshot(
    IReadOnlyList<PixelInstanceManagementItemSnapshot> Instances,
    bool IsEmpty,
    bool CanOpenSelectedChildFolders,
    string EmptyText,
    string RefreshButtonText,
    string OpenModsButtonText,
    string OpenSavesButtonText,
    string OpenInstanceFolderTip);

public sealed record PixelInstanceManagementItemSnapshot(
    string VersionDirectory,
    string Name,
    string Info,
    string Icon,
    bool IsSelected);
