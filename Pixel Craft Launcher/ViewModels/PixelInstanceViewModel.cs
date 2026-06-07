using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCL.Core.Minecraft;

namespace Pixel_Craft_Launcher.ViewModels;

public sealed class PixelInstanceViewModel : ViewModelBase
{
    private readonly MinecraftInstanceService _instanceService = new();
    private MinecraftInstanceSummary? _selectedInstance;
    private string _statusText = "准备扫描实例";

    public ObservableCollection<MinecraftFolderInfo> Folders { get; } = [];
    public ObservableCollection<MinecraftInstanceSummary> Instances { get; } = [];

    public MinecraftInstanceSummary? SelectedInstance
    {
        get => _selectedInstance;
        set => SetField(ref _selectedInstance, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public void Refresh()
    {
        Folders.Clear();
        foreach (var folder in _instanceService.GetFolders())
            Folders.Add(folder);

        Instances.Clear();
        foreach (var instance in _instanceService.GetInstances())
            Instances.Add(instance);

        SelectedInstance = Instances.FirstOrDefault();
        StatusText = $"已找到 {Instances.Count} 个实例";
    }

    public void OpenSelectedFolder(string childFolder = "")
    {
        if (SelectedInstance is null) return;
        var path = string.IsNullOrWhiteSpace(childFolder)
            ? SelectedInstance.VersionDirectory
            : Path.Combine(SelectedInstance.VersionDirectory, childFolder);
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public void SetHidden(MinecraftInstanceSummary summary, bool hidden)
    {
        var instance = _instanceService.Load(summary.VersionDirectory);
        if (instance is null) return;
        _instanceService.SetHidden(instance, hidden);
        Refresh();
    }

}
