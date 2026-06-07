using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.App.Configuration;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.ViewModels;

public sealed class PixelDownloadViewModel : ViewModelBase
{
    private readonly MinecraftDownloadService _downloadService = new();
    private readonly MinecraftInstallService _installService;
    private readonly MinecraftModLoaderCatalogService _loaderCatalogService = new();
    private readonly MinecraftMergedInstallService _mergedInstallService;
    private CancellationTokenSource? _operationCancellation;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _isInstallSelectionOpen;
    private string _statusText = "准备加载 Minecraft 版本列表";
    private MinecraftVersionManifestEntry? _selectedVersion;
    private MinecraftLoaderKind _selectedLoaderKind = MinecraftLoaderKind.Vanilla;
    private string _selectedLoaderVersion = string.Empty;
    private string _instanceName = string.Empty;
    private string _targetFolder;
    private bool _isLoaderChoicesLoading;
    private string? _loaderChoicesError;
    private MinecraftMergedLoaderSelection _mergedSelection = new();
    private MinecraftDownloadTaskInfo? _selectedTask;

    public PixelDownloadViewModel()
    {
        _installService = new MinecraftInstallService(_downloadService);
        _mergedInstallService = new MinecraftMergedInstallService(_downloadService, _loaderCatalogService);
        _targetFolder = MinecraftInstanceScanner.GetDefaultMinecraftFolders().FirstOrDefault() ??
                        Path.Combine(AppContext.BaseDirectory, ".minecraft");
        _downloadService.ProgressChanged += (_, info) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var existing = Tasks.FirstOrDefault(task => task.Id == info.Id);
                if (existing is not null) Tasks.Remove(existing);
                Tasks.Insert(0, info);
                while (Tasks.Count > 30) Tasks.RemoveAt(Tasks.Count - 1);
                if (SelectedTask is null || SelectedTask.Id == info.Id)
                    SelectedTask = info;
                StatusText = $"{info.Name} {info.State} {info.Progress:P0}";
            });
        };
    }

    public ObservableCollection<MinecraftVersionManifestEntry> Versions { get; } = [];
    public ObservableCollection<MinecraftVersionManifestEntry> FilteredVersions { get; } = [];
    public ObservableCollection<MinecraftDownloadTaskInfo> Tasks { get; } = [];
    public ObservableCollection<PixelLoaderChoiceGroup> LoaderChoiceGroups { get; } = [];
    public MyLoadingStateSimulator VersionLoadingState { get; } = new() { LoadingState = MyLoadingState.Unloaded };

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            ApplyFilter();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                OnPropertyChanged(nameof(CanInstall));
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsLoaderChoicesLoading
    {
        get => _isLoaderChoicesLoading;
        private set => SetField(ref _isLoaderChoicesLoading, value);
    }

    public string? LoaderChoicesError
    {
        get => _loaderChoicesError;
        private set => SetField(ref _loaderChoicesError, value);
    }

    public string TargetFolder
    {
        get => _targetFolder;
        set
        {
            if (SetField(ref _targetFolder, value))
                OnPropertyChanged(nameof(CanInstall));
        }
    }

    public MinecraftVersionManifestEntry? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (SetField(ref _selectedVersion, value))
            {
                if (value is not null && string.IsNullOrWhiteSpace(InstanceName))
                    InstanceName = value.Id;
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(LoaderOptions));
            }
        }
    }

    public bool IsInstallSelectionOpen
    {
        get => _isInstallSelectionOpen;
        private set => SetField(ref _isInstallSelectionOpen, value);
    }

    public MinecraftLoaderKind SelectedLoaderKind
    {
        get => _selectedLoaderKind;
        private set
        {
            if (!SetField(ref _selectedLoaderKind, value)) return;
            OnPropertyChanged(nameof(SelectedLoaderLabel));
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    public string SelectedLoaderVersion
    {
        get => _selectedLoaderVersion;
        private set
        {
            if (!SetField(ref _selectedLoaderVersion, value)) return;
            OnPropertyChanged(nameof(SelectedLoaderLabel));
        }
    }

    public string InstanceName
    {
        get => _instanceName;
        set
        {
            if (SetField(ref _instanceName, value))
                OnPropertyChanged(nameof(CanInstall));
        }
    }

    public string SelectedLoaderLabel => SelectedLoaderKind == MinecraftLoaderKind.Vanilla
        ? "原版"
        : MinecraftLoaderCatalog.GetDisplayName(SelectedLoaderKind) +
          (string.IsNullOrWhiteSpace(SelectedLoaderVersion) ? " 最新版" : " " + SelectedLoaderVersion);

    public MinecraftMergedLoaderSelection MergedSelection
    {
        get => _mergedSelection;
        private set
        {
            _mergedSelection = value;
            OnPropertyChanged(nameof(MergedSelection));
            OnPropertyChanged(nameof(SelectedLoaderLabel));
        }
    }

    public IReadOnlyList<MinecraftLoaderOption> LoaderOptions =>
        MinecraftLoaderCatalog.GetOptions(SelectedVersion?.Id);

    public bool CanInstall => !IsBusy &&
                              SelectedVersion is not null &&
                              !string.IsNullOrWhiteSpace(TargetFolder) &&
                              !string.IsNullOrWhiteSpace(InstanceName);

    public MinecraftDownloadTaskInfo? SelectedTask
    {
        get => _selectedTask;
        private set => SetField(ref _selectedTask, value);
    }

    public int MaxDownloadThreads => _downloadService.Scheduler.MaxConcurrency;

    public event EventHandler<MinecraftInstanceInfo>? InstanceInstalled;

    public void OpenInstallSelection(MinecraftVersionManifestEntry version)
    {
        SelectedVersion = version;
        InstanceName = version.Id;
        SelectedLoaderKind = MinecraftLoaderKind.Vanilla;
        SelectedLoaderVersion = string.Empty;
        MergedSelection = new MinecraftMergedLoaderSelection();
        LoaderChoiceGroups.Clear();
        LoaderChoicesError = null;
        IsInstallSelectionOpen = true;
        StatusText = "正在获取 Mod Loader";
        _ = RefreshLoaderChoicesAsync();
    }

    public void CloseInstallSelection()
    {
        IsInstallSelectionOpen = false;
        SelectedLoaderKind = MinecraftLoaderKind.Vanilla;
        SelectedLoaderVersion = string.Empty;
        MergedSelection = new MinecraftMergedLoaderSelection();
        LoaderChoiceGroups.Clear();
        LoaderChoicesError = null;
        IsLoaderChoicesLoading = false;
        StatusText = "已返回 Minecraft 版本列表";
    }

    public void SelectTask(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            SelectedTask = Tasks.FirstOrDefault();
            return;
        }

        SelectedTask = Tasks.FirstOrDefault(task => string.Equals(task.Id, taskId, StringComparison.OrdinalIgnoreCase))
                       ?? SelectedTask;
    }

    public void SelectLoader(MinecraftLoaderKind kind, string version = "")
    {
        SelectedLoaderKind = kind;
        SelectedLoaderVersion = version;
        if (SelectedVersion is not null)
        {
            InstanceName = MinecraftLoaderCatalog.BuildDefaultInstanceName(
                SelectedVersion.Id,
                new MinecraftLoaderSelection(kind, string.IsNullOrWhiteSpace(version) ? null : version));
        }
    }

    public void SelectLoaderVersion(MinecraftLoaderVersionEntry entry)
    {
        MergedSelection = MinecraftLoaderCompatibility.SelectLoader(MergedSelection, entry);
        NormalizeSelection();
        SelectedLoaderKind = entry.Kind;
        SelectedLoaderVersion = entry.Version;
        RefreshSelectedLoaderAfterChoiceChange();
        UpdateDefaultInstanceName();
        AutoSelectCompatibleAddons();
    }

    public void SelectAddon(MinecraftAddonFileEntry entry)
    {
        MergedSelection = entry.Kind switch
        {
            MinecraftAddonKind.FabricApi => MergedSelection with { FabricApi = entry },
            MinecraftAddonKind.LegacyFabricApi => MergedSelection with { LegacyFabricApi = entry },
            MinecraftAddonKind.Qsl => MergedSelection with { Qsl = entry, FabricApi = null },
            MinecraftAddonKind.OptiFabric => MergedSelection with { OptiFabric = entry },
            _ => MergedSelection
        };
        NormalizeSelection();
        UpdateDefaultInstanceName();
    }

    public void ClearChoice(PixelLoaderChoiceGroup group)
    {
        if (group.LoaderKind is { } loaderKind)
            ClearLoader(loaderKind);
        else if (group.AddonKind is { } addonKind)
            ClearAddon(addonKind);
    }

    public void ClearLoader(MinecraftLoaderKind kind)
    {
        MergedSelection = kind switch
        {
            MinecraftLoaderKind.OptiFine => MergedSelection with { OptiFine = null, OptiFabric = null },
            MinecraftLoaderKind.Forge => MergedSelection with { Forge = null },
            MinecraftLoaderKind.NeoForge => MergedSelection with { NeoForge = null },
            MinecraftLoaderKind.Cleanroom => MergedSelection with { Cleanroom = null },
            MinecraftLoaderKind.Fabric => MergedSelection with { Fabric = null, FabricApi = null, OptiFabric = null },
            MinecraftLoaderKind.LegacyFabric => MergedSelection with { LegacyFabric = null, LegacyFabricApi = null },
            MinecraftLoaderKind.Quilt => MergedSelection with { Quilt = null, Qsl = null },
            MinecraftLoaderKind.LiteLoader => MergedSelection with { LiteLoader = null },
            MinecraftLoaderKind.LabyMod => MergedSelection with { LabyMod = null },
            _ => MergedSelection
        };
        NormalizeSelection();
        RefreshSelectedLoaderAfterChoiceChange();
        UpdateDefaultInstanceName();
    }

    public void ClearAddon(MinecraftAddonKind kind)
    {
        MergedSelection = kind switch
        {
            MinecraftAddonKind.FabricApi => MergedSelection with { FabricApi = null },
            MinecraftAddonKind.LegacyFabricApi => MergedSelection with { LegacyFabricApi = null },
            MinecraftAddonKind.Qsl => MergedSelection with { Qsl = null },
            MinecraftAddonKind.OptiFabric => MergedSelection with { OptiFabric = null },
            _ => MergedSelection
        };
        NormalizeSelection();
        RefreshSelectedLoaderAfterChoiceChange();
        UpdateDefaultInstanceName();
    }

    private Progress<MinecraftDownloadTaskInfo> CreateStatusProgress()
    {
        return new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            Dispatcher.UIThread.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
    }

    private static MinecraftDownloadFile? CreateVersionDownloadFile(JsonObject? node, string targetPath, string name, string id)
    {
        var url = node?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return new MinecraftDownloadFile(
            id,
            [url],
            targetPath,
            node?["size"]?.GetValue<long?>(),
            node?["sha1"]?.GetValue<string>(),
            name);
    }

    private static async Task WriteServerLaunchScriptAsync(string versionFolder, string versionId, CancellationToken cancellationToken)
    {
        var command = $@"""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar ""{versionId}-server.jar"" nogui";
        var powerShellCommand = $@"& ""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar ""{versionId}-server.jar"" nogui";
        var bat = $"""
@echo off
title {versionId} 原版服务端
echo 如果服务端立即停止，请右键编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。
echo 你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。
echo ------------------------------
echo 如果提示 "You need to agree to the EULA in order to run the server"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。
echo ------------------------------
{powerShellCommand}
echo ----------------------
echo 服务端已停止。
pause
""";
        await File.WriteAllTextAsync(
            Path.Combine(versionFolder, "Launch Server.bat"),
            bat.Replace("\n", "\r\n", StringComparison.Ordinal),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);

        var ps = $"""
$Host.UI.RawUI.WindowTitle = "{versionId} 原版服务端"
Write-Host "如果服务端立即停止，请编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。"
Write-Host "你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。"
Write-Host "------------------------------"
Write-Host "如果提示 `"You need to agree to the EULA in order to run the server`"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。"
Write-Host "------------------------------"
{command}
Write-Host "----------------------"
Write-Host "服务端已停止。"
Read-Host "按 Enter 键退出"
""";
        await File.WriteAllTextAsync(
            Path.Combine(versionFolder, "Launch Server.ps1"),
            ps.Replace("\n", "\r\n", StringComparison.Ordinal),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);

        var sh = $"""
#!/usr/bin/env sh
printf '%s\n' '{versionId} 原版服务端'
printf '%s\n' '如果服务端立即停止，请编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java 路径。'
printf '%s\n' '你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java 一般在其中的 bin 文件夹下。'
printf '%s\n' '------------------------------'
printf '%s\n' '如果提示 "You need to agree to the EULA in order to run the server"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。'
printf '%s\n' '------------------------------'
{command}
printf '%s\n' '----------------------'
printf '%s\n' '服务端已停止。'
printf '%s' '按 Enter 键退出'
read _
""";
        var shPath = Path.Combine(versionFolder, "Launch Server.sh");
        await File.WriteAllTextAsync(shPath, sh, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        TryMakeExecutable(shPath);
    }

    private static void TryMakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                      UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                      UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort permission update.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private void RefreshSelectedLoaderAfterChoiceChange()
    {
        var loaders = MergedSelection.Loaders.ToArray();
        if (loaders.Length == 0)
        {
            SelectedLoaderKind = MinecraftLoaderKind.Vanilla;
            SelectedLoaderVersion = string.Empty;
            return;
        }

        var loader = loaders[^1];
        SelectedLoaderKind = loader.Kind;
        SelectedLoaderVersion = loader.Version;
    }

    public async Task RefreshVersionsAsync()
    {
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusText = "正在获取 Minecraft 版本列表";
        VersionLoadingState.Error = null;
        VersionLoadingState.LoadingState = MyLoadingState.Run;
        try
        {
            var versions = await _downloadService.GetVersionManifestAsync(_operationCancellation.Token).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                Versions.Clear();
                foreach (var version in versions)
                    Versions.Add(version);
                SelectedVersion = Versions.FirstOrDefault(static v => v.Type == "release") ?? Versions.FirstOrDefault();
                ApplyFilter();
                StatusText = $"已加载 {Versions.Count} 个版本";
                IsBusy = false;
                VersionLoadingState.LoadingState = MyLoadingState.Stop;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "版本列表加载失败：" + ex.Message;
                IsBusy = false;
                VersionLoadingState.Error = ex;
                VersionLoadingState.LoadingState = MyLoadingState.Error;
            });
        }
    }

    public async Task InstallSelectedAsync(MinecraftLoaderSelection? loader = null, bool saveServerJar = false)
    {
        if (SelectedVersion is not { } version || IsBusy)
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusText = "正在安装 " + version.Id;
        try
        {
            await DebugSettingsService.DelayIfNeededAsync("安装 Minecraft", _operationCancellation.Token).ConfigureAwait(false);
            if (IsInstallSelectionOpen && loader is null)
            {
                await InstallMergedSelectedAsync(version, saveServerJar).ConfigureAwait(false);
                return;
            }

            var request = new MinecraftInstallRequest(
                version.Id,
                version.Url,
                TargetFolder,
                string.IsNullOrWhiteSpace(InstanceName) ? null : InstanceName,
                loader ?? BuildSelectedLoader(saveServerJar));
            var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"{info.Name} {info.State} {info.Progress:P0}";
                });
            });
            var instance = await _installService.InstallAsync(request, progress, _operationCancellation.Token).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "安装完成：" + instance.Name;
                IsBusy = false;
                IsInstallSelectionOpen = false;
                InstanceInstalled?.Invoke(this, instance);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "安装失败：" + ex.Message;
                IsBusy = false;
            });
        }
    }

    public async Task SaveClientCoreAsync(MinecraftVersionManifestEntry version, string baseFolder)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(baseFolder))
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        SelectedVersion = version;
        InstanceName = version.Id;
        StatusText = "正在保存 " + version.Id;
        try
        {
            var versionFolder = Path.Combine(baseFolder, version.Id);
            Directory.CreateDirectory(versionFolder);
            var jsonPath = Path.Combine(versionFolder, version.Id + ".json");
            var jarPath = Path.Combine(versionFolder, version.Id + ".jar");
            var progress = CreateStatusProgress();

            await _downloadService.DownloadFilesAsync([
                new MinecraftDownloadFile(
                    "save-json:" + version.Id,
                    [version.Url],
                    jsonPath,
                    Name: "下载实例 JSON 文件")
            ], progress, _operationCancellation.Token).ConfigureAwait(false);

            var json = JsonNode.Parse(await File.ReadAllTextAsync(jsonPath, _operationCancellation.Token).ConfigureAwait(false))?.AsObject()
                       ?? throw new InvalidDataException("版本 JSON 无效。");
            var clientFile = CreateVersionDownloadFile(json["downloads"]?["client"] as JsonObject, jarPath, "下载核心 JAR 文件", "save-client:" + version.Id)
                             ?? throw new InvalidDataException("版本 JSON 中没有客户端 JAR 下载地址。");

            await _downloadService.DownloadFilesAsync([clientFile], progress, _operationCancellation.Token).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "保存完成：" + version.Id;
                IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "保存失败：" + ex.Message;
                IsBusy = false;
            });
        }
    }

    public async Task SaveServerJarAsync(MinecraftVersionManifestEntry version, string baseFolder)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(baseFolder))
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        SelectedVersion = version;
        InstanceName = version.Id;
        StatusText = "正在保存 " + version.Id + " 服务端";
        try
        {
            var versionFolder = Path.Combine(baseFolder, version.Id);
            Directory.CreateDirectory(versionFolder);
            var jsonPath = Path.Combine(versionFolder, version.Id + ".json");
            var serverPath = Path.Combine(versionFolder, version.Id + "-server.jar");
            var progress = CreateStatusProgress();

            await _downloadService.DownloadFilesAsync([
                new MinecraftDownloadFile(
                    "server-json:" + version.Id,
                    [version.Url],
                    jsonPath,
                    Name: "下载实例 JSON 文件")
            ], progress, _operationCancellation.Token).ConfigureAwait(false);

            var json = JsonNode.Parse(await File.ReadAllTextAsync(jsonPath, _operationCancellation.Token).ConfigureAwait(false))?.AsObject()
                       ?? throw new InvalidDataException("版本 JSON 无效。");
            var serverFile = CreateVersionDownloadFile(json["downloads"]?["server"] as JsonObject, serverPath, "下载服务端文件", "save-server:" + version.Id)
                             ?? throw new InvalidDataException($"Mojang 没有给 Minecraft {version.Id} 提供官方服务端下载。");

            await WriteServerLaunchScriptAsync(versionFolder, version.Id, _operationCancellation.Token).ConfigureAwait(false);
            await _downloadService.DownloadFilesAsync([serverFile], progress, _operationCancellation.Token).ConfigureAwait(false);
            TryDelete(jsonPath);

            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "服务端保存完成：" + version.Id;
                IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "服务端保存失败：" + ex.Message;
                IsBusy = false;
            });
        }
    }

    public async Task RefreshLoaderChoicesAsync()
    {
        if (SelectedVersion is not { } version) return;
        IsLoaderChoicesLoading = true;
        LoaderChoicesError = null;
        StatusText = "正在获取 Mod Loader 版本列表";
        var groups = new List<PixelLoaderChoiceGroup>();
        try
        {
            foreach (var item in GetPcl2InstallChoiceOrder())
            {
                if (item.LoaderKind is { } kind)
                {
                    var option = MinecraftLoaderCatalog.GetOptions(version.Id).FirstOrDefault(option => option.Kind == kind);
                    if (option is null) continue;
                    groups.Add(option.IsAvailable
                        ? await BuildLoaderGroupAsync(kind, version.Id, option).ConfigureAwait(false)
                        : new PixelLoaderChoiceGroup(
                            option.DisplayName,
                            option.Description,
                            option.Icon,
                            kind,
                            null,
                            [],
                            [],
                            option.StatusText,
                            false));
                }
                else if (item.AddonKind is { } addon)
                {
                    groups.Add(await BuildAddonGroupAsync(addon, version.Id).ConfigureAwait(false));
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                LoaderChoiceGroups.Clear();
                foreach (var group in groups)
                    LoaderChoiceGroups.Add(group);
                AutoSelectCompatibleAddons();
                IsLoaderChoicesLoading = false;
                LoaderChoicesError = groups.Count == 0 ? "当前版本暂无可用 Mod Loader" : null;
                StatusText = "选择 Mod Loader 后即可开始下载";
                OnPropertyChanged(nameof(LoaderChoiceGroups));
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsLoaderChoicesLoading = false;
                LoaderChoicesError = "Mod Loader 加载失败：" + ex.Message;
                StatusText = LoaderChoicesError;
                OnPropertyChanged(nameof(LoaderChoiceGroups));
            });
        }
    }

    public void Cancel()
    {
        _operationCancellation?.Cancel();
        _downloadService.Scheduler.CancelAll();
        StatusText = "已请求取消";
    }

    public bool CancelSelectedTask()
    {
        if (SelectedTask is null)
            return false;
        return CancelTask(SelectedTask.Id);
    }

    public bool CancelTask(string taskId) => _downloadService.CancelTask(taskId);

    private MinecraftLoaderSelection BuildSelectedLoader(bool saveServerJar)
    {
        if (SelectedLoaderKind == MinecraftLoaderKind.Vanilla)
            return new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla, SaveServerJar: saveServerJar);
        return new MinecraftLoaderSelection(
            SelectedLoaderKind,
            string.IsNullOrWhiteSpace(SelectedLoaderVersion) ? null : SelectedLoaderVersion,
            saveServerJar);
    }

    private async Task InstallMergedSelectedAsync(MinecraftVersionManifestEntry version, bool saveServerJar)
    {
        var request = new MinecraftMergedInstallRequest(
            version.Id,
            version.Url,
            TargetFolder,
            string.IsNullOrWhiteSpace(InstanceName) ? version.Id : InstanceName,
            MergedSelection,
            saveServerJar);
        var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            Dispatcher.UIThread.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
        var stage = new Progress<MinecraftInstallStageInfo>(info =>
        {
            Dispatcher.UIThread.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
        var instance = await _mergedInstallService.InstallAsync(request, progress, stage, _operationCancellation?.Token ?? CancellationToken.None).ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = "安装完成：" + instance.Name;
            IsBusy = false;
            IsInstallSelectionOpen = false;
            InstanceInstalled?.Invoke(this, instance);
        });
    }

    private async Task<PixelLoaderChoiceGroup> BuildLoaderGroupAsync(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        MinecraftLoaderOption option)
    {
        try
        {
            var versions = await _loaderCatalogService.GetLoaderVersionsAsync(kind, minecraftVersion, _operationCancellation?.Token ?? CancellationToken.None).ConfigureAwait(false);
            return new PixelLoaderChoiceGroup(
                option.DisplayName,
                option.Description,
                option.Icon,
                kind,
                null,
                versions.Take(40).ToArray(),
                [],
                versions.Count == 0 ? "无可用版本" : "可以添加",
                versions.Count != 0);
        }
        catch (Exception ex)
        {
            return new PixelLoaderChoiceGroup(option.DisplayName, option.Description, option.Icon, kind, null, [], [], "获取失败：" + ex.Message, false);
        }
    }

    private static IReadOnlyList<(MinecraftLoaderKind? LoaderKind, MinecraftAddonKind? AddonKind)> GetPcl2InstallChoiceOrder() =>
    [
        (MinecraftLoaderKind.Cleanroom, null),
        (MinecraftLoaderKind.NeoForge, null),
        (MinecraftLoaderKind.Forge, null),
        (MinecraftLoaderKind.Fabric, null),
        (null, MinecraftAddonKind.FabricApi),
        (MinecraftLoaderKind.LegacyFabric, null),
        (null, MinecraftAddonKind.LegacyFabricApi),
        (MinecraftLoaderKind.Quilt, null),
        (null, MinecraftAddonKind.Qsl),
        (MinecraftLoaderKind.LabyMod, null),
        (MinecraftLoaderKind.OptiFine, null),
        (null, MinecraftAddonKind.OptiFabric),
        (MinecraftLoaderKind.LiteLoader, null)
    ];

    private async Task<PixelLoaderChoiceGroup> BuildAddonGroupAsync(MinecraftAddonKind kind, string minecraftVersion)
    {
        var (title, description, icon, parent) = kind switch
        {
            MinecraftAddonKind.FabricApi => ("Fabric API", "Fabric / Quilt 常用 API；Fabric 安装后推荐添加。", "Assets/Blocks/Fabric.png", (MinecraftLoaderKind?)null),
            MinecraftAddonKind.LegacyFabricApi => ("Legacy Fabric API", "Legacy Fabric 常用 API；旧版本 Fabric 安装后推荐添加。", "Assets/Blocks/Fabric.png", MinecraftLoaderKind.LegacyFabric),
            MinecraftAddonKind.Qsl => ("QFAPI / QSL", "Quilt 常用 API；Quilt 安装后推荐添加。", "Assets/Blocks/Quilt.png", MinecraftLoaderKind.Quilt),
            MinecraftAddonKind.OptiFabric => ("OptiFabric", "让 Fabric 与 OptiFine 一起工作。", "Assets/Blocks/OptiFabric.png", MinecraftLoaderKind.Fabric),
            _ => ("附加 Mod", "", "mdi-package-variant", MinecraftLoaderKind.Vanilla)
        };
        try
        {
            var files = await _loaderCatalogService.GetAddonFilesAsync(kind, _operationCancellation?.Token ?? CancellationToken.None).ConfigureAwait(false);
            var compatible = files.Where(file => MinecraftModLoaderCatalogService.IsAddonCompatible(file, minecraftVersion, parent)).Take(40).ToArray();
            return new PixelLoaderChoiceGroup(title, description, icon, null, kind, [], compatible, compatible.Length == 0 ? "无可用版本" : "可以添加", compatible.Length != 0);
        }
        catch (Exception ex)
        {
            return new PixelLoaderChoiceGroup(title, description, icon, null, kind, [], [], "获取失败：" + ex.Message, false);
        }
    }

    private void AutoSelectCompatibleAddons()
    {
        if (SelectedVersion is null) return;
        if (MergedSelection.Fabric is not null && MergedSelection.FabricApi is null)
            SelectFirstAddon(MinecraftAddonKind.FabricApi);
        if (MergedSelection.LegacyFabric is not null && MergedSelection.LegacyFabricApi is null)
            SelectFirstAddon(MinecraftAddonKind.LegacyFabricApi);
        if (MergedSelection.Quilt is not null && MergedSelection.Qsl is null)
            SelectFirstAddon(MinecraftAddonKind.Qsl);
        if (MergedSelection.Fabric is not null && MergedSelection.OptiFine is not null && MergedSelection.OptiFabric is null)
            SelectFirstAddon(MinecraftAddonKind.OptiFabric);
        NormalizeSelection();
    }

    private void SelectFirstAddon(MinecraftAddonKind kind)
    {
        var addon = LoaderChoiceGroups.FirstOrDefault(group => group.AddonKind == kind)?.AddonFiles.FirstOrDefault();
        if (addon is not null)
            SelectAddon(addon);
    }

    private void UpdateDefaultInstanceName()
    {
        if (SelectedVersion is null) return;
        InstanceName = MinecraftMergedInstallService.BuildDefaultInstanceName(SelectedVersion.Id, MergedSelection);
    }

    private void NormalizeSelection()
    {
        if (SelectedVersion is null)
        {
            MergedSelection = MinecraftLoaderCompatibility.Normalize(MergedSelection);
            return;
        }

        MergedSelection = MinecraftLoaderCompatibility.Normalize(MergedSelection, SelectedVersion.Id);
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        FilteredVersions.Clear();
        foreach (var version in Versions.Where(version =>
                     string.IsNullOrWhiteSpace(query) ||
                     version.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     version.Type.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredVersions.Add(version);
        }
    }

}

public sealed record PixelLoaderChoiceGroup(
    string Title,
    string Description,
    string Icon,
    MinecraftLoaderKind? LoaderKind,
    MinecraftAddonKind? AddonKind,
    IReadOnlyList<MinecraftLoaderVersionEntry> LoaderVersions,
    IReadOnlyList<MinecraftAddonFileEntry> AddonFiles,
    string StatusText,
    bool CanSelect)
{
    public bool IsAddon => AddonKind is not null;
}
