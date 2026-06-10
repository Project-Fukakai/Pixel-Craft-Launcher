using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Download;
using PCL.Core.IO.Download;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.ViewModels;

public sealed class PixelDownloadViewModel : PixelViewModelBase
{
    private readonly MinecraftDownloadService _downloadService;
    private readonly MinecraftInstallService _installService;
    private readonly MinecraftModLoaderCatalogService _loaderCatalogService;
    private readonly MinecraftMergedInstallService _mergedInstallService;
    private readonly MinecraftCorePackageSaver _corePackageSaver;
    private readonly PixelLoaderChoiceService _loaderChoiceService;
    private readonly PixelLoaderSelectionService _loaderSelectionService;
    private readonly PixelDownloadCategoryRefreshService _categoryRefreshService;
    private readonly IPixelCommandBus _commandBus;
    private readonly PixelDownloadStateMachine? _downloadStateMachine;
    private readonly IPixelOperationDelayService _operationDelayService;
    private readonly IUiDispatcher _uiDispatcher;
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

    public static PixelDownloadPageMessages GetPageMessages()
    {
        return new PixelDownloadPageMessages(
            new PixelDownloadWindowMessages(
                "正在刷新……",
                "安装完成：{0}"),
            "正在获取 Minecraft 版本列表",
            new PixelDownloadInstallPanelMessages(
                "Mod Loader",
                "当前版本暂无可用 Mod Loader",
                "Mod Loader - 正在加载可用版本",
                "正在加载可用版本",
                "重试",
                "加载失败",
                "安装",
                "清除选择"),
            new PixelDownloadInstallSidebarMessages(
                "实例名称",
                "安装清单",
                "名称",
                "目录",
                "删除",
                "原版实例"),
            new PixelDownloadVersionListMessages(
                "Minecraft 版本",
                "最新版本",
                "正在加载版本列表...",
                "没有匹配的版本。",
                "另存为",
                "选择保存位置",
                "更新日志",
                "下载服务端",
                "选择服务端保存位置"),
            new PixelDownloadTaskDetailsMessages(
                "下载任务",
                "取消",
                "已请求取消下载任务组。",
                "正在完成安装",
                "暂无下载任务"),
            new PixelDownloadManagerStatsMessages(
                "总进度",
                "下载速度",
                "剩余文件",
                "剩余线程"));
    }

    public static PixelDownloadWindowMessages GetWindowMessages()
    {
        return GetPageMessages().Window;
    }

    public static string GetRefreshStartMessage() => GetWindowMessages().RefreshStartMessage;

    public static string GetInstallCompletedMessage(string instanceName) =>
        GetWindowMessages().GetInstallCompletedMessage(instanceName);

    public static string GetVersionLoadingText() => GetPageMessages().VersionLoadingText;

    public PixelDownloadPageMessages GetPageMessagesSnapshot() => GetPageMessages();

    public PixelDownloadViewModel(
        MinecraftDownloadService downloadService,
        MinecraftInstallService installService,
        MinecraftModLoaderCatalogService loaderCatalogService,
        MinecraftMergedInstallService mergedInstallService,
        MinecraftCorePackageSaver corePackageSaver,
        PixelLoaderChoiceService loaderChoiceService,
        PixelLoaderSelectionService loaderSelectionService,
        PixelDownloadCategoryRefreshService categoryRefreshService,
        IPixelOperationDelayService operationDelayService,
        IUiDispatcher uiDispatcher,
        IPixelCommandBus commandBus,
        PixelDownloadStateMachine? downloadStateMachine = null)
    {
        _downloadService = downloadService;
        _loaderCatalogService = loaderCatalogService;
        _installService = installService;
        _mergedInstallService = mergedInstallService;
        _corePackageSaver = corePackageSaver;
        _loaderChoiceService = loaderChoiceService;
        _loaderSelectionService = loaderSelectionService;
        _categoryRefreshService = categoryRefreshService;
        _commandBus = commandBus;
        _downloadStateMachine = downloadStateMachine;
        _operationDelayService = operationDelayService;
        _uiDispatcher = uiDispatcher;
        _targetFolder = MinecraftInstanceScanner.GetDefaultMinecraftFolders().FirstOrDefault() ??
                        Path.Combine(AppContext.BaseDirectory, ".minecraft");
        Tasks.CollectionChanged += (_, _) => TaskListChanged?.Invoke(this, EventArgs.Empty);
        _downloadService.ProgressChanged += (_, info) =>
        {
            _uiDispatcher.Post(() =>
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

    public event EventHandler? TaskListChanged;

    internal ObservableCollection<MinecraftVersionManifestEntry> Versions { get; } = [];
    internal ObservableCollection<MinecraftVersionManifestEntry> FilteredVersions { get; } = [];
    internal ObservableCollection<MinecraftDownloadTaskInfo> Tasks { get; } = [];
    internal ObservableCollection<PixelLoaderChoiceGroup> LoaderChoiceGroups { get; } = [];
    public PixelLoadingStateController VersionLoadingState { get; } = new() { State = PixelLoadingState.Unloaded };

    public PixelDownloadState DownloadState => _downloadStateMachine?.State ?? PixelDownloadState.Idle;

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

    internal MinecraftVersionManifestEntry? SelectedVersion
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

    internal MinecraftLoaderKind SelectedLoaderKind
    {
        get => _selectedLoaderKind;
        private set
        {
            if (!SetField(ref _selectedLoaderKind, value)) return;
            OnPropertyChanged(nameof(SelectedLoaderLabel));
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    internal string SelectedLoaderVersion
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

    public string SelectedLoaderLabel
    {
        get
        {
            if (SelectedLoaderKind == MinecraftLoaderKind.Vanilla)
                return "原版";

            return MinecraftLoaderCatalog.GetDisplayName(SelectedLoaderKind) +
                   (string.IsNullOrWhiteSpace(SelectedLoaderVersion) ? " 最新版" : " " + SelectedLoaderVersion);
        }
    }

    internal MinecraftMergedLoaderSelection MergedSelection
    {
        get => _mergedSelection;
        private set
        {
            _mergedSelection = value;
            OnPropertyChanged(nameof(MergedSelection));
            OnPropertyChanged(nameof(SelectedLoaderLabel));
        }
    }

    internal IReadOnlyList<MinecraftLoaderOption> LoaderOptions =>
        MinecraftLoaderCatalog.GetOptions(SelectedVersion?.Id);

    public bool CanInstall => !IsBusy &&
                              SelectedVersion is not null &&
                              !string.IsNullOrWhiteSpace(TargetFolder) &&
                              !string.IsNullOrWhiteSpace(InstanceName);

    internal MinecraftDownloadTaskInfo? SelectedTask
    {
        get => _selectedTask;
        private set => SetField(ref _selectedTask, value);
    }

    public int MaxDownloadThreads => _downloadService.Scheduler.MaxConcurrency;

    public event EventHandler<PixelMinecraftInstanceInstalledSnapshot>? InstanceInstalled;

    public PixelDownloadRightPageKind GetRightPageKind(
        int selectedCategory,
        bool isInstallRoute,
        bool isTaskRoute)
    {
        if (isTaskRoute)
            return PixelDownloadRightPageKind.TaskDetails;

        if (selectedCategory == 1)
            return isInstallRoute
                ? PixelDownloadRightPageKind.InstallSelection
                : GetVersionListPageKind(PixelDownloadRightPageKind.InstallVersionList);

        if (selectedCategory == 9)
            return GetVersionListPageKind(PixelDownloadRightPageKind.ClientVersionList);

        return PixelDownloadRightPageKind.Pending;
    }

    public PixelDownloadRightPagePresentation GetRightPagePresentation(
        int selectedCategory,
        bool isInstallRoute,
        bool isTaskRoute)
    {
        var kind = GetRightPageKind(selectedCategory, isInstallRoute, isTaskRoute);
        var refreshAction = ShouldRefreshVersionsBeforeRightPage(kind)
            ? PixelDownloadRightPageRefreshAction.RefreshVersions
            : PixelDownloadRightPageRefreshAction.None;
        return new PixelDownloadRightPagePresentation(kind, refreshAction);
    }

    public Task RefreshRightPageDataAsync(PixelDownloadRightPagePresentation presentation)
    {
        return presentation.RefreshAction switch
        {
            PixelDownloadRightPageRefreshAction.RefreshVersions => RefreshVersionsAsync(),
            _ => Task.CompletedTask
        };
    }

    public bool ShouldRefreshRightPageForProperty(string? propertyName)
    {
        return propertyName is nameof(MergedSelection)
            or nameof(SelectedLoaderKind)
            or nameof(IsBusy)
            or nameof(LoaderChoiceGroups)
            or nameof(IsLoaderChoicesLoading)
            or nameof(LoaderChoicesError)
            or nameof(SelectedTask)
            or nameof(DownloadState);
    }

    public bool ShouldRefreshVersionsBeforeRightPage(PixelDownloadRightPageKind rightPageKind)
    {
        return rightPageKind != PixelDownloadRightPageKind.TaskDetails &&
               Versions.Count == 0 &&
               !IsBusy;
    }

    private PixelDownloadRightPageKind GetVersionListPageKind(PixelDownloadRightPageKind loadedKind)
    {
        return Versions.Count == 0 && (IsBusy || VersionLoadingState.State == PixelLoadingState.Error)
            ? PixelDownloadRightPageKind.Loading
            : loadedKind;
    }

    public IReadOnlyList<PixelDownloadVersionSnapshot> GetTopVersionSnapshots()
    {
        var topVersions = new List<MinecraftVersionManifestEntry>();
        var release = Versions.FirstOrDefault(static version => version.Type == "release");
        var snapshot = Versions.FirstOrDefault(static version => version.Type is "snapshot" or "pending");
        if (release is not null) topVersions.Add(release);
        if (snapshot is not null && (release is null || snapshot.ReleaseTime > release.ReleaseTime))
            topVersions.Add(snapshot);
        return topVersions.Where(MatchesSearch).Select(ToVersionSnapshot).ToArray();
    }

    public IReadOnlyList<PixelDownloadVersionGroupSnapshot> GetVersionGroupSnapshots()
    {
        var source = FilteredVersions.Count == 0 && Versions.Count > 0
            ? Versions
            : FilteredVersions;
        return
        [
            new PixelDownloadVersionGroupSnapshot("正式版", source.Where(static version => version.Type == "release").Select(ToVersionSnapshot).ToArray()),
            new PixelDownloadVersionGroupSnapshot("预览版", source.Where(static version => version.Type is "snapshot" or "pending").Select(ToVersionSnapshot).ToArray()),
            new PixelDownloadVersionGroupSnapshot("愚人节版", source.Where(IsAprilFoolsVersion).Select(ToVersionSnapshot).ToArray()),
            new PixelDownloadVersionGroupSnapshot("远古版", source.Where(static version => version.Type is not ("release" or "snapshot" or "pending") && !IsAprilFoolsVersion(version)).Select(ToVersionSnapshot).ToArray())
        ];
    }

    public PixelDownloadVersionListPageSnapshot GetVersionListPageSnapshot()
    {
        var messages = GetVersionListMessages();
        if (Versions.Count == 0)
        {
            return new PixelDownloadVersionListPageSnapshot(
                [
                    new PixelDownloadVersionListGroupSnapshot(messages.EmptyGroupTitle, [], false)
                ],
                IsBusy ? messages.LoadingText : messages.EmptyText);
        }

        var groups = new List<PixelDownloadVersionListGroupSnapshot>
        {
            new(messages.TopGroupTitle, GetTopVersionSnapshots(), false)
        };
        groups.AddRange(GetVersionGroupSnapshots()
            .Where(static group => group.Versions.Count > 0)
            .Select(static group => new PixelDownloadVersionListGroupSnapshot(
                $"{group.Title} ({group.Versions.Count})",
                group.Versions,
                true)));

        return new PixelDownloadVersionListPageSnapshot(
            groups,
            IsBusy ? messages.LoadingText : messages.EmptyText);
    }

    public PixelDownloadVersionSnapshot? GetSelectedVersionSnapshot() =>
        SelectedVersion is null ? null : ToVersionSnapshot(SelectedVersion);

    public PixelDownloadSidebarSnapshot GetSidebarSnapshot(int selectedCategory)
    {
        return new PixelDownloadSidebarSnapshot(
            GetDownloadSidebarGroups()
                .Select(group => new PixelDownloadSidebarGroupSnapshot(
                    group.Title,
                    group.Items
                        .Select(item => new PixelDownloadSidebarItemSnapshot(
                            item.Category,
                            item.Title,
                            item.Icon,
                            item.Category == selectedCategory,
                            "刷新"))
                        .ToArray()))
                .ToArray());
    }

    public PixelDownloadPendingPageSnapshot GetPendingPageSnapshot(int category)
    {
        var title = GetDownloadSidebarGroups()
                        .SelectMany(static group => group.Items)
                        .FirstOrDefault(item => item.Category == category)
                        ?.Title
                    ?? "下载";
        var description = category switch
        {
            >= 2 and <= 8 => "Plain 中这里是社区资源搜索与详情页；本轮先保留同名入口、刷新按钮和页面占位。",
            >= 10 and <= 18 => "Plain 中这里是独立安装包版本列表；本轮先保留同名入口、刷新按钮和页面占位。",
            _ => "Plain 中这里是下载页。"
        };
        return new PixelDownloadPendingPageSnapshot(
            title,
            description,
            "迁移状态",
            [
                "Plain 侧这个页面包含搜索、筛选、详情页和文件安装流程。",
                "本轮先保留同名入口、侧边栏刷新按钮和右页结构，占位等待后续迁移具体数据源。"
            ],
            "实例管理");
    }

    private static IReadOnlyList<PixelDownloadSidebarGroupDefinition> GetDownloadSidebarGroups() =>
    [
        new PixelDownloadSidebarGroupDefinition(null,
        [
            new PixelDownloadSidebarItemDefinition(1, "Minecraft", "mdi-hammer-screwdriver")
        ]),
        new PixelDownloadSidebarGroupDefinition("社区资源",
        [
            new PixelDownloadSidebarItemDefinition(2, "Mod", "mdi-puzzle-outline"),
            new PixelDownloadSidebarItemDefinition(3, "整合包", "mdi-package-variant-closed"),
            new PixelDownloadSidebarItemDefinition(4, "数据包", "mdi-view-grid-outline"),
            new PixelDownloadSidebarItemDefinition(5, "资源包", "mdi-image-outline"),
            new PixelDownloadSidebarItemDefinition(6, "光影包", "mdi-white-balance-sunny"),
            new PixelDownloadSidebarItemDefinition(7, "世界", "mdi-earth"),
            new PixelDownloadSidebarItemDefinition(8, "收藏夹", "mdi-book-heart-outline")
        ]),
        new PixelDownloadSidebarGroupDefinition("安装包",
        [
            new PixelDownloadSidebarItemDefinition(9, "Minecraft", "mdi-cube-outline"),
            new PixelDownloadSidebarItemDefinition(10, "OptiFine", "mdi-eye-outline"),
            new PixelDownloadSidebarItemDefinition(11, "Forge", "mdi-anvil"),
            new PixelDownloadSidebarItemDefinition(12, "NeoForge", "mdi-anvil"),
            new PixelDownloadSidebarItemDefinition(13, "Cleanroom", "mdi-flask-outline"),
            new PixelDownloadSidebarItemDefinition(14, "Fabric", "mdi-feather"),
            new PixelDownloadSidebarItemDefinition(18, "Legacy Fabric", "mdi-feather"),
            new PixelDownloadSidebarItemDefinition(15, "Quilt", "mdi-grid-large"),
            new PixelDownloadSidebarItemDefinition(17, "LabyMod", "mdi-test-tube"),
            new PixelDownloadSidebarItemDefinition(16, "LiteLoader", "mdi-package-variant")
        ])
    ];

    public PixelInstallSelectionSummarySnapshot GetInstallSelectionSummarySnapshot()
    {
        var version = GetSelectedVersionSnapshot()?.Title ?? "Minecraft";
        var loader = MergedSelection.Loaders.Any() ? SelectedLoaderLabel : "原版";
        return new PixelInstallSelectionSummarySnapshot(version, "Mod Loader: " + loader);
    }

    public PixelDownloadInstallStateSnapshot GetInstallStateSnapshot()
    {
        var operation = GetOperationSnapshot();
        return new PixelDownloadInstallStateSnapshot(
            CanInstall,
            operation.IsBusy,
            operation.StatusText,
            SelectedLoaderLabel,
            operation.IsBusy ? "处理中" : "开始下载",
            operation.IsBusy ? "mdi-progress-download" : "mdi-download",
            (SelectedVersion?.Id ?? "Minecraft") + " 安装",
            operation.HasPendingOperation && SelectedVersion is not null);
    }

    public PixelDownloadOperationSnapshot GetOperationSnapshot()
    {
        var state = DownloadState;
        var hasPendingOperation = IsBusy ||
                                  state is PixelDownloadState.ResolvingVersions
                                      or PixelDownloadState.SelectingLoader
                                      or PixelDownloadState.Downloading
                                      or PixelDownloadState.Saving;
        return new PixelDownloadOperationSnapshot(
            state,
            IsBusy,
            StatusText,
            VersionLoadingState.State == PixelLoadingState.Error ? VersionLoadingState.Error?.Message : null,
            LoaderChoicesError,
            VersionLoadingState.State == PixelLoadingState.Run || state == PixelDownloadState.ResolvingVersions,
            IsLoaderChoicesLoading || state == PixelDownloadState.SelectingLoader,
            hasPendingOperation);
    }

    public PixelDownloadInstallSidebarSnapshot GetInstallSidebarSnapshot()
    {
        var checklist = GetInstallChecklistSnapshots();
        return new PixelDownloadInstallSidebarSnapshot(
            InstanceName,
            TargetFolder,
            checklist,
            checklist.Count <= 1,
            GetInstallStateSnapshot());
    }

    public IReadOnlyList<PixelInstallHintSnapshot> GetInstallHintSnapshots()
    {
        var hints = new List<PixelInstallHintSnapshot>();
        if (MergedSelection.Fabric is not null && MergedSelection.FabricApi is null)
            hints.Add(new PixelInstallHintSnapshot("如果不安装 Fabric API，大多数 Mod 都会无法使用！", PixelInstallHintKind.Error));
        if (MergedSelection.LegacyFabric is not null && MergedSelection.LegacyFabricApi is null)
            hints.Add(new PixelInstallHintSnapshot("如果不安装 Legacy Fabric API，大多数 Mod 都会无法使用！", PixelInstallHintKind.Error));
        if (MergedSelection.Quilt is not null && MergedSelection.Qsl is null && MergedSelection.FabricApi is null)
            hints.Add(new PixelInstallHintSnapshot("如果不安装 QFAPI / QSL，大多数 Mod 都会无法使用！如果 QFAPI / QSL 无可用版本，你可以选择安装 Fabric API。", PixelInstallHintKind.Error));
        if (MergedSelection.Fabric is not null && MergedSelection.OptiFine is not null && MergedSelection.OptiFabric is null)
            hints.Add(new PixelInstallHintSnapshot("必须安装 OptiFabric 才能正常使用 OptiFine！", PixelInstallHintKind.Error));
        if (MergedSelection.OptiFine is not null && (MergedSelection.Forge is not null || MergedSelection.Fabric is not null))
            hints.Add(new PixelInstallHintSnapshot("OptiFine 与一部分 Mod 的兼容性不佳，请谨慎安装。", PixelInstallHintKind.Warning));
        return hints;
    }

    public static PixelDownloadInstallPanelMessages GetInstallPanelMessages()
    {
        return GetPageMessages().InstallPanel;
    }

    public PixelDownloadInstallPanelSnapshot GetInstallPanelSnapshot()
    {
        var loaderChoices = IsLoaderChoicesLoading
            ? new PixelDownloadLoaderChoiceStateSnapshot(PixelDownloadLoaderChoiceStateKind.Loading, null)
            : !string.IsNullOrWhiteSpace(LoaderChoicesError)
                ? new PixelDownloadLoaderChoiceStateSnapshot(PixelDownloadLoaderChoiceStateKind.Error, LoaderChoicesError)
                : LoaderChoiceGroups.Count == 0
                    ? new PixelDownloadLoaderChoiceStateSnapshot(PixelDownloadLoaderChoiceStateKind.Empty, null)
                    : new PixelDownloadLoaderChoiceStateSnapshot(PixelDownloadLoaderChoiceStateKind.Ready, null);

        return new PixelDownloadInstallPanelSnapshot(
            GetInstallSelectionSummarySnapshot(),
            loaderChoices,
            GetInstallHintSnapshots(),
            GetLoaderChoiceSnapshots());
    }

    public static PixelDownloadInstallSidebarMessages GetInstallSidebarMessages()
    {
        return GetPageMessages().InstallSidebar;
    }

    public static PixelDownloadVersionListMessages GetVersionListMessages()
    {
        return GetPageMessages().VersionList;
    }

    public static PixelDownloadTaskDetailsMessages GetTaskDetailsMessages()
    {
        return GetPageMessages().TaskDetails;
    }

    public static PixelDownloadManagerStatsMessages GetManagerStatsMessages()
    {
        return GetPageMessages().ManagerStats;
    }

    public PixelDownloadManagerStatsSnapshot GetManagerStatsSnapshot()
    {
        var tasks = GetVisibleTaskSnapshots();
        var activeTasks = tasks.Where(static task => task.CanCancel).ToArray();
        var rawProgress = tasks.Count == 0 ? 1d : tasks.Average(static task => task.Progress);
        var speed = activeTasks.Sum(static task => task.SpeedBytesPerSecond);
        var remainingFiles = activeTasks.Length;
        var runningThreads = activeTasks.Count(static task => task.Status == PixelDownloadTaskStatus.Running);

        return new PixelDownloadManagerStatsSnapshot(
            FormatPcl2Progress(rawProgress),
            FormatBytes(speed) + "/s",
            remainingFiles.ToString(),
            $"{runningThreads} / {MaxDownloadThreads}");
    }

    public IReadOnlyList<PixelLoaderChoiceSnapshot> GetLoaderChoiceSnapshots()
    {
        return LoaderChoiceGroups.Select(ToLoaderChoiceSnapshot).ToArray();
    }

    public IReadOnlyList<PixelInstallChecklistItemSnapshot> GetInstallChecklistSnapshots()
    {
        var items = new List<PixelInstallChecklistItemSnapshot>();
        if (GetSelectedVersionSnapshot() is { } version)
            items.Add(new PixelInstallChecklistItemSnapshot(null, version.Title, "Minecraft", version.Icon, false));

        AddLoader(MergedSelection.Forge, "Forge", "mdi-anvil", MinecraftLoaderKind.Forge);
        AddLoader(MergedSelection.Cleanroom, "Cleanroom", "mdi-flask-outline", MinecraftLoaderKind.Cleanroom);
        AddLoader(MergedSelection.NeoForge, "NeoForge", "mdi-anvil", MinecraftLoaderKind.NeoForge);
        AddLoader(MergedSelection.Fabric, "Fabric", "mdi-feather", MinecraftLoaderKind.Fabric);
        AddLoader(MergedSelection.LegacyFabric, "Legacy Fabric", "mdi-feather", MinecraftLoaderKind.LegacyFabric);
        AddLoader(MergedSelection.Quilt, "Quilt", "mdi-grid-large", MinecraftLoaderKind.Quilt);
        AddLoader(MergedSelection.LabyMod, "LabyMod", "mdi-test-tube", MinecraftLoaderKind.LabyMod);
        AddLoader(MergedSelection.OptiFine, "OptiFine", "mdi-eye-outline", MinecraftLoaderKind.OptiFine);
        AddLoader(MergedSelection.LiteLoader, "LiteLoader", "mdi-package-variant", MinecraftLoaderKind.LiteLoader);

        AddAddon(MergedSelection.FabricApi, MinecraftAddonKind.FabricApi, "Fabric API", "mdi-feather");
        AddAddon(MergedSelection.LegacyFabricApi, MinecraftAddonKind.LegacyFabricApi, "Legacy Fabric API", "mdi-feather");
        AddAddon(MergedSelection.Qsl, MinecraftAddonKind.Qsl, "QFAPI / QSL", "mdi-grid-large");
        AddAddon(MergedSelection.OptiFabric, MinecraftAddonKind.OptiFabric, "OptiFabric", "mdi-package-variant");
        return items;

        void AddLoader(MinecraftLoaderVersionEntry? entry, string info, string icon, MinecraftLoaderKind kind)
        {
            if (entry is not null)
                items.Add(new PixelInstallChecklistItemSnapshot(GetClearLoaderActionId(kind), entry.DisplayName, info, icon, true));
        }

        void AddAddon(MinecraftAddonFileEntry? entry, MinecraftAddonKind kind, string info, string icon)
        {
            if (entry is not null)
                items.Add(new PixelInstallChecklistItemSnapshot(GetClearAddonActionId(kind), FormatAddonTitle(kind, entry), info, icon, true));
        }
    }

    public void OpenInstallSelection(string versionId)
    {
        if (FindVersion(versionId) is { } version)
            OpenInstallSelection(version);
    }

    public void SelectVersion(string versionId)
    {
        if (FindVersion(versionId) is not { } version)
            return;

        SelectedVersion = version;
        InstanceName = version.Id;
    }

    public async Task InstallVanillaSelectedAsync(bool saveServerJar = false)
    {
        await InstallSelectedAsync(new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla, SaveServerJar: saveServerJar), saveServerJar).ConfigureAwait(false);
    }

    public async Task InstallVanillaVersionAsync(string versionId)
    {
        SelectVersion(versionId);
        await InstallVanillaSelectedAsync().ConfigureAwait(false);
    }

    public async Task RefreshDownloadCategoryAsync(int tag)
    {
        var action = _categoryRefreshService.GetRefreshAction(tag, SelectedVersion is not null);
        switch (action)
        {
            case PixelDownloadCategoryRefreshAction.RefreshVersions:
                await RefreshVersionsAsync().ConfigureAwait(false);
                break;
            case PixelDownloadCategoryRefreshAction.RefreshLoaderChoices:
                await RefreshLoaderChoicesAsync().ConfigureAwait(false);
                break;
        }
    }

    public async Task SaveClientCoreAsync(string versionId, string baseFolder)
    {
        if (FindVersion(versionId) is { } version)
            await SaveClientCoreAsync(version, baseFolder).ConfigureAwait(false);
    }

    public async Task SaveServerJarAsync(string versionId, string baseFolder)
    {
        if (FindVersion(versionId) is { } version)
            await SaveServerJarAsync(version, baseFolder).ConfigureAwait(false);
    }

    private void OpenInstallSelection(MinecraftVersionManifestEntry version)
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

    public string? GetTaskDetailsRouteTaskId()
    {
        return SelectedTask?.Id ?? Tasks.FirstOrDefault()?.Id;
    }

    public bool HasVisibleTasksOrPendingOperation()
    {
        return GetVisibleTaskSnapshots().Count > 0 || HasPendingDownloadOperation();
    }

    public bool HasPendingDownloadOperation()
    {
        return GetOperationSnapshot().HasPendingOperation;
    }

    public bool ShouldReturnFromTaskDetails()
    {
        return !HasVisibleTasksOrPendingOperation();
    }

    public PixelDownloadTaskDetailsPageSnapshot GetTaskDetailsPageSnapshot()
    {
        var tasks = GetVisibleTaskSnapshots();
        var messages = GetTaskDetailsMessages();
        if (tasks.Count > 0)
        {
            return new PixelDownloadTaskDetailsPageSnapshot(
                PixelDownloadTaskDetailsPageKind.Tasks,
                GetTaskGroupTitle(tasks),
                string.Empty,
                tasks,
                tasks.Where(static task => task.CanCancel).Select(static task => task.Id).ToArray());
        }

        var operation = GetOperationSnapshot();
        if (operation.HasPendingOperation)
        {
            return new PixelDownloadTaskDetailsPageSnapshot(
                PixelDownloadTaskDetailsPageKind.Finishing,
                messages.TaskTitle,
                string.IsNullOrWhiteSpace(operation.StatusText) ? messages.FinishingText : operation.StatusText,
                [],
                []);
        }

        return new PixelDownloadTaskDetailsPageSnapshot(
            PixelDownloadTaskDetailsPageKind.Empty,
            messages.TaskTitle,
            messages.EmptyText,
            [],
            []);
    }

    public IReadOnlyList<PixelDownloadTaskSnapshot> GetVisibleTaskSnapshots()
    {
        return Tasks
            .Where(static task => task.State is not (NDlTaskState.Finished or NDlTaskState.Cancelled))
            .Select(ToTaskSnapshot)
            .ToArray();
    }

    public IReadOnlyList<PixelDownloadTaskSnapshot> GetRecentTaskSnapshots(int count)
    {
        return Tasks
            .Take(Math.Max(count, 0))
            .Select(ToTaskSnapshot)
            .ToArray();
    }

    public string GetTaskGroupTitle(IReadOnlyList<PixelDownloadTaskSnapshot> tasks)
    {
        var version = SelectedVersion?.Id;
        if (!string.IsNullOrWhiteSpace(version))
            return "Minecraft " + version + " 下载";
        return tasks.Count == 1 ? tasks[0].Name : "下载任务";
    }

    private void SelectLoader(MinecraftLoaderKind kind, string version = "")
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

    internal static PixelDownloadTaskSnapshot ToTaskSnapshot(MinecraftDownloadTaskInfo task)
    {
        var status = task.State switch
        {
            NDlTaskState.Running => PixelDownloadTaskStatus.Running,
            NDlTaskState.Finished => PixelDownloadTaskStatus.Finished,
            NDlTaskState.Failed => PixelDownloadTaskStatus.Failed,
            NDlTaskState.Cancelled => PixelDownloadTaskStatus.Cancelled,
            _ => PixelDownloadTaskStatus.Waiting
        };
        var text = task.State == NDlTaskState.Failed && !string.IsNullOrWhiteSpace(task.Message)
            ? task.Message
            : task.Name;
        var statusText = task.State switch
        {
            NDlTaskState.Waiting => "等待中",
            NDlTaskState.Running => "下载中",
            NDlTaskState.Finished => "已完成",
            NDlTaskState.Failed => "失败",
            NDlTaskState.Cancelled => "已取消",
            _ => task.State.ToString()
        };
        var info = $"{statusText} · {task.Progress:P0}" +
                   (string.IsNullOrWhiteSpace(task.Message) ? string.Empty : " · " + task.Message);
        var icon = task.State switch
        {
            NDlTaskState.Finished => "mdi-check-circle-outline",
            NDlTaskState.Failed => "mdi-alert-circle-outline",
            NDlTaskState.Cancelled => "mdi-cancel",
            NDlTaskState.Running => "mdi-download-outline",
            _ => "mdi-clock-outline"
        };

        return new PixelDownloadTaskSnapshot(
            task.Id,
            task.Name,
            text,
            info,
            icon,
            Math.Clamp(task.Progress, 0d, 1d),
            FormatTaskProgressText(task.Progress),
            Math.Max(task.SpeedBytesPerSecond, 0),
            status,
            task.State is NDlTaskState.Waiting or NDlTaskState.Running);
    }

    private static string FormatTaskProgressText(double progress)
    {
        return Math.Floor(Math.Clamp(progress, 0d, 1d) * 100) + "%";
    }

    private static string FormatPcl2Progress(double progress)
    {
        var value = Math.Clamp(progress, 0d, 1d);
        if (value > 0.999999)
            return "100 %";
        var percent = value * 100;
        var integer = Math.Floor(percent);
        var decimals = Math.Floor((percent - integer) * 100);
        return $"{integer:0}.{decimals:00} %";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(bytes, 0);
        var size = (double)value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }

    private MinecraftVersionManifestEntry? FindVersion(string versionId) =>
        Versions.FirstOrDefault(version => string.Equals(version.Id, versionId, StringComparison.OrdinalIgnoreCase));

    private bool MatchesSearch(MinecraftVersionManifestEntry version)
    {
        var query = SearchText.Trim();
        return string.IsNullOrWhiteSpace(query) ||
               version.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    internal static PixelDownloadVersionSnapshot ToVersionSnapshot(MinecraftVersionManifestEntry version)
    {
        var title = version.Id.Replace('_', ' ');
        var kind = GetVersionKind(version);
        var icon = kind switch
        {
            PixelDownloadVersionKind.Release => "mdi-cube-outline",
            PixelDownloadVersionKind.Snapshot => "mdi-flask-outline",
            PixelDownloadVersionKind.AprilFools => "mdi-party-popper",
            _ => "mdi-archive-outline"
        };
        return new PixelDownloadVersionSnapshot(
            version.Id,
            title,
            $"{GetVersionKindText(kind)} · 发布于 {version.ReleaseTime:yyyy/MM/dd HH:mm}",
            icon,
            McFormatter.GetWikiUrlSuffix(version.Id),
            kind,
            version.ReleaseTime);
    }

    private static PixelDownloadVersionKind GetVersionKind(MinecraftVersionManifestEntry version)
    {
        if (IsAprilFoolsVersion(version))
            return PixelDownloadVersionKind.AprilFools;
        return version.Type switch
        {
            "release" => PixelDownloadVersionKind.Release,
            "snapshot" or "pending" => PixelDownloadVersionKind.Snapshot,
            _ => PixelDownloadVersionKind.Legacy
        };
    }

    private static string GetVersionKindText(PixelDownloadVersionKind kind) =>
        kind switch
        {
            PixelDownloadVersionKind.Release => "正式版",
            PixelDownloadVersionKind.Snapshot => "预览版",
            PixelDownloadVersionKind.AprilFools => "愚人节版",
            _ => "远古版"
        };

    private static bool IsAprilFoolsVersion(MinecraftVersionManifestEntry version)
    {
        var id = version.Id.ToLowerInvariant();
        return id is "2point0_blue" or "2point0_red" or "2point0_purple" or "2.0_blue" or "2.0_red" or "2.0_purple"
                   or "20w14infinite" or "20w14∞" or "3d shareware v1.34" or "1.rv-pre1" or "15w14a"
                   or "22w13oneblockatatime" or "23w13a_or_b" or "24w14potato" or "25w14craftmine" or "26w14a" ||
               version.ReleaseTime.Month == 4 && version.ReleaseTime.Day == 1;
    }

    private PixelLoaderChoiceSnapshot ToLoaderChoiceSnapshot(PixelLoaderChoiceGroup group)
    {
        var active = GetSelectedText(group) is not null;
        var disabledReason = GetLoaderGroupDisabledReason(group);
        var canSelect = disabledReason is null;
        var status = GetSelectedText(group) ?? disabledReason ?? group.StatusText;
        var items = group.IsAddon
            ? group.AddonFiles.Select(file => new PixelLoaderChoiceItemSnapshot(
                GetAddonItemId(file),
                FormatAddonTitle(group.AddonKind!.Value, file),
                BuildAddonItemInfo(group.AddonKind!.Value, file),
                group.Icon,
                IsSelectedAddon(group.AddonKind.Value, file),
                canSelect)).ToArray()
            : group.LoaderVersions.Select(version => new PixelLoaderChoiceItemSnapshot(
                GetLoaderItemId(version),
                version.DisplayName,
                BuildLoaderItemInfo(version),
                group.Icon,
                IsSelectedLoader(version),
                canSelect)).ToArray();
        var title = active
            ? group.Title + " - " + status.Replace("已选择：", "", StringComparison.Ordinal)
            : canSelect
                ? group.Title
                : group.Title + " - " + status;
        var clearActionId = group.LoaderKind is { } loaderKind
            ? GetClearLoaderActionId(loaderKind)
            : group.AddonKind is { } addonKind
                ? GetClearAddonActionId(addonKind)
                : null;
        return new PixelLoaderChoiceSnapshot(
            group.Title,
            title,
            group.Description,
            status,
            canSelect,
            active,
            group.StatusText,
            clearActionId,
            items);
    }

    private string? GetLoaderGroupDisabledReason(PixelLoaderChoiceGroup group)
    {
        if (!group.CanSelect)
            return group.StatusText;

        var selection = MergedSelection;
        if (group.LoaderKind is { } loaderKind)
        {
            if (loaderKind == MinecraftLoaderKind.OptiFine)
            {
                var blocker = selection.NeoForge ?? selection.Cleanroom ?? selection.Quilt ?? selection.LabyMod;
                if (blocker is not null)
                    return "与 " + MinecraftLoaderCatalog.GetDisplayName(blocker.Kind) + " 不兼容";
                if (selection.Fabric is not null &&
                    !MinecraftLoaderCompatibility.IsOptiFineAllowedWith(MinecraftLoaderKind.Fabric, SelectedVersion?.Id))
                    return "与 Fabric 不兼容";
                if (selection.Forge is not null &&
                    !group.LoaderVersions.Any(version => MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(version, selection.Forge)))
                    return "仅兼容特定版本的 Forge";
            }

            if (selection.OptiFine is not null)
            {
                if (!MinecraftLoaderCompatibility.IsOptiFineAllowedWith(loaderKind, SelectedVersion?.Id))
                    return "与 OptiFine 不兼容";
                if (loaderKind == MinecraftLoaderKind.Forge &&
                    !group.LoaderVersions.Any(version => MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(selection.OptiFine, version)))
                    return "与 OptiFine 不兼容";
            }
        }

        return group.AddonKind switch
        {
            MinecraftAddonKind.FabricApi when selection.Fabric is null && selection.Quilt is null => "需要先选择 Fabric 或 Quilt",
            MinecraftAddonKind.LegacyFabricApi when selection.LegacyFabric is null => "需要先选择 Legacy Fabric",
            MinecraftAddonKind.Qsl when selection.Quilt is null => "需要先选择 Quilt",
            MinecraftAddonKind.OptiFabric when selection.Fabric is null || selection.OptiFine is null => "需要先选择 Fabric 和 OptiFine",
            _ => null
        };
    }

    private string BuildLoaderItemInfo(MinecraftLoaderVersionEntry version)
    {
        var parts = new List<string>
        {
            version.IsRecommended ? "推荐版" : version.IsStable ? "稳定版" : "测试版"
        };
        if (version.ReleaseTime is not null)
            parts.Add("发布于 " + version.ReleaseTime.Value.ToString("yyyy/MM/dd HH:mm"));
        var conflict = GetLoaderConflictText(version.Kind);
        if (!string.IsNullOrWhiteSpace(conflict) && !IsSelectedLoader(version))
            parts.Add(conflict);
        return string.Join(" · ", parts);
    }

    private string BuildAddonItemInfo(MinecraftAddonKind kind, MinecraftAddonFileEntry file)
    {
        var parts = new List<string>
        {
            file.IsStable ? "正式版" : "测试版"
        };
        if (file.ReleaseTime is not null)
            parts.Add("发布于 " + file.ReleaseTime.Value.ToString("yyyy/MM/dd HH:mm"));
        if (kind == MinecraftAddonKind.Qsl && MergedSelection.FabricApi is not null && !IsSelectedAddon(kind, file))
            parts.Add("会替换 Fabric API");
        return string.Join(" · ", parts);
    }

    private string? GetLoaderConflictText(MinecraftLoaderKind kind)
    {
        var conflicts = MinecraftLoaderCompatibility.GetConflictingLoaderKinds(MergedSelection, kind);
        if (conflicts.Count == 0)
            return null;

        return "会替换 " + string.Join(" / ", conflicts.Select(MinecraftLoaderCatalog.GetDisplayName));
    }

    private bool IsSelectedLoader(MinecraftLoaderVersionEntry version)
    {
        return version.Kind switch
        {
            MinecraftLoaderKind.OptiFine => Equals(MergedSelection.OptiFine, version),
            MinecraftLoaderKind.Forge => Equals(MergedSelection.Forge, version),
            MinecraftLoaderKind.NeoForge => Equals(MergedSelection.NeoForge, version),
            MinecraftLoaderKind.Cleanroom => Equals(MergedSelection.Cleanroom, version),
            MinecraftLoaderKind.Fabric => Equals(MergedSelection.Fabric, version),
            MinecraftLoaderKind.LegacyFabric => Equals(MergedSelection.LegacyFabric, version),
            MinecraftLoaderKind.Quilt => Equals(MergedSelection.Quilt, version),
            MinecraftLoaderKind.LiteLoader => Equals(MergedSelection.LiteLoader, version),
            MinecraftLoaderKind.LabyMod => Equals(MergedSelection.LabyMod, version),
            _ => false
        };
    }

    private bool IsSelectedAddon(MinecraftAddonKind kind, MinecraftAddonFileEntry file)
    {
        return kind switch
        {
            MinecraftAddonKind.FabricApi => Equals(MergedSelection.FabricApi, file),
            MinecraftAddonKind.LegacyFabricApi => Equals(MergedSelection.LegacyFabricApi, file),
            MinecraftAddonKind.Qsl => Equals(MergedSelection.Qsl, file),
            MinecraftAddonKind.OptiFabric => Equals(MergedSelection.OptiFabric, file),
            _ => false
        };
    }

    private string? GetSelectedText(PixelLoaderChoiceGroup group)
    {
        if (group.LoaderKind is { } kind)
        {
            var entry = kind switch
            {
                MinecraftLoaderKind.OptiFine => MergedSelection.OptiFine,
                MinecraftLoaderKind.Forge => MergedSelection.Forge,
                MinecraftLoaderKind.NeoForge => MergedSelection.NeoForge,
                MinecraftLoaderKind.Cleanroom => MergedSelection.Cleanroom,
                MinecraftLoaderKind.Fabric => MergedSelection.Fabric,
                MinecraftLoaderKind.LegacyFabric => MergedSelection.LegacyFabric,
                MinecraftLoaderKind.Quilt => MergedSelection.Quilt,
                MinecraftLoaderKind.LiteLoader => MergedSelection.LiteLoader,
                MinecraftLoaderKind.LabyMod => MergedSelection.LabyMod,
                _ => null
            };
            return entry is null ? null : "已选择：" + entry.DisplayName;
        }

        var addon = group.AddonKind switch
        {
            MinecraftAddonKind.FabricApi => MergedSelection.FabricApi,
            MinecraftAddonKind.LegacyFabricApi => MergedSelection.LegacyFabricApi,
            MinecraftAddonKind.Qsl => MergedSelection.Qsl,
            MinecraftAddonKind.OptiFabric => MergedSelection.OptiFabric,
            _ => null
        };
        return addon is null || group.AddonKind is null ? null : "已选择：" + FormatAddonTitle(group.AddonKind.Value, addon);
    }

    private static string FormatAddonTitle(MinecraftAddonKind kind, MinecraftAddonFileEntry file) =>
        kind switch
        {
            MinecraftAddonKind.FabricApi => file.DisplayName.Replace("Fabric API ", "", StringComparison.OrdinalIgnoreCase),
            MinecraftAddonKind.LegacyFabricApi => file.DisplayName.Replace("Legacy Fabric API ", "", StringComparison.OrdinalIgnoreCase),
            MinecraftAddonKind.Qsl => file.DisplayName.Replace(" build ", ".", StringComparison.OrdinalIgnoreCase).Split('+')[0],
            MinecraftAddonKind.OptiFabric => file.DisplayName.ToLowerInvariant().Replace("optifabric-", "", StringComparison.Ordinal).Replace(".jar", "", StringComparison.Ordinal).Trim().TrimStart('v'),
            _ => file.DisplayName
        };

    private static string GetLoaderItemId(MinecraftLoaderVersionEntry version) =>
        "loader-item:" + version.Kind + ":" + version.Version;

    private static string GetAddonItemId(MinecraftAddonFileEntry file) =>
        "addon-item:" + file.Kind + ":" + file.Id;

    private static string GetClearLoaderActionId(MinecraftLoaderKind kind) => "loader:" + kind;

    private static string GetClearAddonActionId(MinecraftAddonKind kind) => "addon:" + kind;

    private void SelectLoaderVersion(MinecraftLoaderVersionEntry entry)
    {
        if (SelectedVersion is null)
            return;

        ApplyLoaderSelectionState(_loaderSelectionService.SelectLoaderVersion(
            MergedSelection,
            entry,
            SelectedVersion.Id,
            LoaderChoiceGroups.ToArray()));
    }

    private void SelectAddon(MinecraftAddonFileEntry entry)
    {
        if (SelectedVersion is null)
            return;

        ApplyLoaderSelectionState(_loaderSelectionService.SelectAddon(
            MergedSelection,
            entry,
            SelectedVersion.Id,
            SelectedLoaderKind,
            SelectedLoaderVersion));
    }

    private void ClearChoice(PixelLoaderChoiceGroup group)
    {
        if (group.LoaderKind is { } loaderKind)
            ClearLoader(loaderKind);
        else if (group.AddonKind is { } addonKind)
            ClearAddon(addonKind);
    }

    public void SelectLoaderChoiceItem(string itemId)
    {
        foreach (var group in LoaderChoiceGroups)
        {
            foreach (var loader in group.LoaderVersions)
            {
                if (string.Equals(GetLoaderItemId(loader), itemId, StringComparison.Ordinal))
                {
                    SelectLoaderVersion(loader);
                    return;
                }
            }

            foreach (var addon in group.AddonFiles)
            {
                if (string.Equals(GetAddonItemId(addon), itemId, StringComparison.Ordinal))
                {
                    SelectAddon(addon);
                    return;
                }
            }
        }
    }

    public void ClearInstallChoice(string actionId)
    {
        if (actionId.StartsWith("loader:", StringComparison.Ordinal) &&
            Enum.TryParse<MinecraftLoaderKind>(actionId["loader:".Length..], out var loaderKind))
        {
            ClearLoader(loaderKind);
            return;
        }

        if (actionId.StartsWith("addon:", StringComparison.Ordinal) &&
            Enum.TryParse<MinecraftAddonKind>(actionId["addon:".Length..], out var addonKind))
        {
            ClearAddon(addonKind);
        }
    }

    private void ClearLoader(MinecraftLoaderKind kind)
    {
        if (SelectedVersion is null)
            return;

        ApplyLoaderSelectionState(_loaderSelectionService.ClearLoader(MergedSelection, kind, SelectedVersion.Id));
    }

    private void ClearAddon(MinecraftAddonKind kind)
    {
        if (SelectedVersion is null)
            return;

        ApplyLoaderSelectionState(_loaderSelectionService.ClearAddon(MergedSelection, kind, SelectedVersion.Id));
    }

    private Progress<MinecraftDownloadTaskInfo> CreateStatusProgress()
    {
        return new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            _uiDispatcher.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
    }

    public async Task RefreshVersionsAsync()
    {
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        BeginDownloadState(PixelDownloadTrigger.StartVersionRefresh);
        IsBusy = true;
        StatusText = "正在获取 Minecraft 版本列表";
        VersionLoadingState.Error = null;
        VersionLoadingState.State = PixelLoadingState.Run;
        try
        {
            var versions = await RequireCommandBus()
                .Send(new RefreshMinecraftVersionsCommand(), _operationCancellation.Token)
                .ConfigureAwait(false);
            _uiDispatcher.Post(() =>
            {
                Versions.Clear();
                foreach (var version in versions)
                    Versions.Add(version);
                SelectedVersion = Versions.FirstOrDefault(static v => v.Type == "release") ?? Versions.FirstOrDefault();
                ApplyFilter();
                TryFireDownloadTrigger(PixelDownloadTrigger.VersionsResolved);
                StatusText = $"已加载 {Versions.Count} 个版本";
                IsBusy = false;
                VersionLoadingState.State = PixelLoadingState.Stop;
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                StatusText = "版本列表加载失败：" + ex.Message;
                TryFireDownloadTrigger(ex is OperationCanceledException
                    ? PixelDownloadTrigger.Cancel
                    : PixelDownloadTrigger.Fail);
                IsBusy = false;
                VersionLoadingState.Error = ex;
                VersionLoadingState.State = PixelLoadingState.Error;
            });
        }
    }

    public async Task InstallSelectedAsync(MinecraftLoaderSelection? loader = null, bool saveServerJar = false)
    {
        if (SelectedVersion is not { } version || IsBusy)
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        BeginDownloadState(PixelDownloadTrigger.StartDownload);
        IsBusy = true;
        StatusText = "正在安装 " + version.Id;
        try
        {
            await _operationDelayService.DelayIfNeededAsync("安装 Minecraft", _operationCancellation.Token).ConfigureAwait(false);
            if (IsInstallSelectionOpen && loader is null)
            {
                await InstallMergedSelectedAsync(version, saveServerJar).ConfigureAwait(false);
                return;
            }

            var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
            {
                _uiDispatcher.Post(() =>
                {
                    StatusText = $"{info.Name} {info.State} {info.Progress:P0}";
                });
            });
            var instance = await RequireCommandBus()
                .Send(
                StartDownloadInstallCommand.ForInstance(
                    version,
                    TargetFolder,
                    string.IsNullOrWhiteSpace(InstanceName) ? null : InstanceName,
                    loader ?? BuildSelectedLoader(saveServerJar),
                    progress),
                _operationCancellation.Token)
                .ConfigureAwait(false);
            _uiDispatcher.Post(() =>
            {
                StatusText = "安装完成：" + instance.Name;
                TryFireDownloadTrigger(PixelDownloadTrigger.Complete);
                IsBusy = false;
                IsInstallSelectionOpen = false;
                InstanceInstalled?.Invoke(this, instance);
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                StatusText = "安装失败：" + ex.Message;
                TryFireDownloadTrigger(ex is OperationCanceledException
                    ? PixelDownloadTrigger.Cancel
                    : PixelDownloadTrigger.Fail);
                IsBusy = false;
            });
        }
    }

    private async Task SaveClientCoreAsync(MinecraftVersionManifestEntry version, string baseFolder)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(baseFolder))
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        BeginDownloadState(PixelDownloadTrigger.StartSave);
        IsBusy = true;
        SelectedVersion = version;
        InstanceName = version.Id;
        StatusText = "正在保存 " + version.Id;
        try
        {
            var progress = CreateStatusProgress();
            await RequireCommandBus()
                .Send(
                    new SaveMinecraftClientCoreCommand(
                        PixelDownloadInstallVersion.FromVersion(version),
                        baseFolder,
                        progress),
                    _operationCancellation.Token)
                .ConfigureAwait(false);

            _uiDispatcher.Post(() =>
            {
                StatusText = "保存完成：" + version.Id;
                TryFireDownloadTrigger(PixelDownloadTrigger.Complete);
                IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                StatusText = "保存失败：" + ex.Message;
                TryFireDownloadTrigger(ex is OperationCanceledException
                    ? PixelDownloadTrigger.Cancel
                    : PixelDownloadTrigger.Fail);
                IsBusy = false;
            });
        }
    }

    private async Task SaveServerJarAsync(MinecraftVersionManifestEntry version, string baseFolder)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(baseFolder))
            return;

        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        BeginDownloadState(PixelDownloadTrigger.StartSave);
        IsBusy = true;
        SelectedVersion = version;
        InstanceName = version.Id;
        StatusText = "正在保存 " + version.Id + " 服务端";
        try
        {
            var progress = CreateStatusProgress();
            await RequireCommandBus()
                .Send(
                    new SaveMinecraftServerJarCommand(
                        PixelDownloadInstallVersion.FromVersion(version),
                        baseFolder,
                        progress),
                    _operationCancellation.Token)
                .ConfigureAwait(false);

            _uiDispatcher.Post(() =>
            {
                StatusText = "服务端保存完成：" + version.Id;
                TryFireDownloadTrigger(PixelDownloadTrigger.Complete);
                IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                StatusText = "服务端保存失败：" + ex.Message;
                TryFireDownloadTrigger(ex is OperationCanceledException
                    ? PixelDownloadTrigger.Cancel
                    : PixelDownloadTrigger.Fail);
                IsBusy = false;
            });
        }
    }

    public async Task RefreshLoaderChoicesAsync()
    {
        if (SelectedVersion is not { } version) return;
        BeginDownloadState(PixelDownloadTrigger.OpenLoaderSelection);
        IsLoaderChoicesLoading = true;
        LoaderChoicesError = null;
        StatusText = "正在获取 Mod Loader 版本列表";
        var groups = new List<PixelLoaderChoiceGroup>();
        try
        {
            groups.AddRange(await RequireCommandBus()
                .Send(
                    new RefreshMinecraftLoaderChoicesCommand(version.Id),
                    _operationCancellation?.Token ?? CancellationToken.None)
                .ConfigureAwait(false));

            _uiDispatcher.Post(() =>
            {
                LoaderChoiceGroups.Clear();
                foreach (var group in groups)
                    LoaderChoiceGroups.Add(group);
                IsLoaderChoicesLoading = false;
                LoaderChoicesError = groups.Count == 0 ? "当前版本暂无可用 Mod Loader" : null;
                TryFireDownloadTrigger(PixelDownloadTrigger.LoaderChoicesResolved);
                StatusText = "选择 Mod Loader 后即可开始下载";
                OnPropertyChanged(nameof(LoaderChoiceGroups));
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                IsLoaderChoicesLoading = false;
                LoaderChoicesError = "Mod Loader 加载失败：" + ex.Message;
                TryFireDownloadTrigger(ex is OperationCanceledException
                    ? PixelDownloadTrigger.Cancel
                    : PixelDownloadTrigger.Fail);
                StatusText = LoaderChoicesError;
                OnPropertyChanged(nameof(LoaderChoiceGroups));
            });
        }
    }

    public void Cancel()
    {
        CancelAsync().GetAwaiter().GetResult();
    }

    public async Task CancelAsync()
    {
        _operationCancellation?.Cancel();
        await RequireCommandBus()
            .Send(new CancelAllMinecraftDownloadsCommand())
            .ConfigureAwait(false);
        TryFireDownloadTrigger(PixelDownloadTrigger.Cancel);
        IsBusy = false;
        IsLoaderChoicesLoading = false;
        StatusText = "已请求取消";
    }

    public Task<bool> CancelSelectedTaskAsync()
    {
        if (SelectedTask is null)
            return Task.FromResult(false);
        return CancelTaskAsync(SelectedTask.Id);
    }

    public bool CancelSelectedTask() => CancelSelectedTaskAsync().GetAwaiter().GetResult();

    public async Task<bool> CancelTaskAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return false;

        var accepted = await RequireCommandBus()
            .Send(new CancelMinecraftDownloadTaskCommand(taskId))
            .ConfigureAwait(false);
        if (accepted)
            StatusText = "已请求取消下载任务";
        return accepted;
    }

    public bool CancelTask(string taskId) => CancelTaskAsync(taskId).GetAwaiter().GetResult();

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
        var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            _uiDispatcher.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
        var stage = new Progress<MinecraftInstallStageInfo>(info =>
        {
            _uiDispatcher.Post(() => StatusText = $"{info.Name} {info.State} {info.Progress:P0}");
        });
        var instance = await RequireCommandBus()
            .Send(
                StartDownloadInstallCommand.ForMerged(
                    version,
                    TargetFolder,
                    string.IsNullOrWhiteSpace(InstanceName) ? version.Id : InstanceName,
                    MergedSelection,
                    saveServerJar,
                    progress,
                    stage),
                _operationCancellation?.Token ?? CancellationToken.None)
            .ConfigureAwait(false);
        _uiDispatcher.Post(() =>
        {
            StatusText = "安装完成：" + instance.Name;
            TryFireDownloadTrigger(PixelDownloadTrigger.Complete);
            IsBusy = false;
            IsInstallSelectionOpen = false;
            InstanceInstalled?.Invoke(this, instance);
        });
    }

    private void ApplyLoaderSelectionState(PixelLoaderSelectionState state)
    {
        MergedSelection = state.Selection;
        SelectedLoaderKind = state.SelectedLoaderKind;
        SelectedLoaderVersion = state.SelectedLoaderVersion;
        InstanceName = state.InstanceName;
    }

    private void BeginDownloadState(PixelDownloadTrigger trigger)
    {
        if (_downloadStateMachine is null)
            return;

        if (_downloadStateMachine.CanFire(trigger))
        {
            TryFireDownloadTrigger(trigger);
            return;
        }

        if (_downloadStateMachine.CanFire(PixelDownloadTrigger.Cancel))
            TryFireDownloadTrigger(PixelDownloadTrigger.Cancel);

        if (_downloadStateMachine.CanFire(PixelDownloadTrigger.Reset))
            TryFireDownloadTrigger(PixelDownloadTrigger.Reset);

        TryFireDownloadTrigger(trigger);
    }

    private void TryFireDownloadTrigger(PixelDownloadTrigger trigger)
    {
        if (_downloadStateMachine?.CanFire(trigger) != true)
            return;

        _downloadStateMachine.Fire(trigger);
        OnPropertyChanged(nameof(DownloadState));
    }

    private IPixelCommandBus RequireCommandBus()
    {
        return _commandBus;
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

public enum PixelDownloadTaskStatus
{
    Waiting,
    Running,
    Finished,
    Failed,
    Cancelled
}

public enum PixelDownloadRightPageKind
{
    TaskDetails,
    InstallSelection,
    InstallVersionList,
    ClientVersionList,
    Loading,
    Pending
}

public enum PixelDownloadRightPageRefreshAction
{
    None,
    RefreshVersions
}

public sealed record PixelDownloadRightPagePresentation(
    PixelDownloadRightPageKind Kind,
    PixelDownloadRightPageRefreshAction RefreshAction);

public enum PixelDownloadVersionKind
{
    Release,
    Snapshot,
    AprilFools,
    Legacy
}

public sealed record PixelDownloadOperationSnapshot(
    PixelDownloadState State,
    bool IsBusy,
    string StatusText,
    string? VersionErrorText,
    string? LoaderChoiceErrorText,
    bool IsVersionLoading,
    bool IsLoaderChoiceLoading,
    bool HasPendingOperation);

public sealed record PixelDownloadVersionGroupSnapshot(
    string Title,
    IReadOnlyList<PixelDownloadVersionSnapshot> Versions);

public sealed record PixelDownloadVersionListGroupSnapshot(
    string CardTitle,
    IReadOnlyList<PixelDownloadVersionSnapshot> Versions,
    bool IsSwapped)
{
    public bool HasVersions => Versions.Count > 0;
}

public sealed record PixelDownloadVersionListPageSnapshot(
    IReadOnlyList<PixelDownloadVersionListGroupSnapshot> Groups,
    string EmptyListText);

public sealed record PixelDownloadVersionSnapshot(
    string Id,
    string Title,
    string Info,
    string Icon,
    string WikiUrlSuffix,
    PixelDownloadVersionKind Kind,
    DateTime ReleaseTime);

public enum PixelInstallHintKind
{
    Error,
    Warning
}

public sealed record PixelInstallSelectionSummarySnapshot(
    string VersionTitle,
    string LoaderInfo);

public enum PixelDownloadLoaderChoiceStateKind
{
    Loading,
    Error,
    Empty,
    Ready
}

public sealed record PixelDownloadLoaderChoiceStateSnapshot(
    PixelDownloadLoaderChoiceStateKind Kind,
    string? ErrorText);

public sealed record PixelDownloadInstallPanelSnapshot(
    PixelInstallSelectionSummarySnapshot Summary,
    PixelDownloadLoaderChoiceStateSnapshot LoaderChoiceState,
    IReadOnlyList<PixelInstallHintSnapshot> Hints,
    IReadOnlyList<PixelLoaderChoiceSnapshot> LoaderChoiceGroups);

public sealed record PixelDownloadInstallPanelMessages(
    string LoaderTitle,
    string EmptyLoaderText,
    string LoadingLoaderTitle,
    string LoadingLoaderText,
    string RetryButtonText,
    string LoaderFailedText,
    string InstallTitle,
    string ClearSelectionButtonText);

public sealed record PixelDownloadInstallSidebarMessages(
    string InstanceNameSectionTitle,
    string ChecklistSectionTitle,
    string NameInputTitle,
    string DirectoryInputTitle,
    string ClearChoiceTooltip,
    string VanillaInstanceText);

public sealed record PixelDownloadVersionListMessages(
    string EmptyGroupTitle,
    string TopGroupTitle,
    string LoadingText,
    string EmptyText,
    string SaveClientTooltip,
    string SaveClientPickerTitle,
    string VersionInfoTooltip,
    string SaveServerTooltip,
    string SaveServerPickerTitle);

public sealed record PixelDownloadTaskDetailsMessages(
    string TaskTitle,
    string CancelTooltip,
    string CancelGroupRequestedMessage,
    string FinishingText,
    string EmptyText);

public sealed record PixelDownloadManagerStatsMessages(
    string ProgressTitle,
    string SpeedTitle,
    string RemainingFilesTitle,
    string RemainingThreadsTitle);

public sealed record PixelDownloadManagerStatsSnapshot(
    string ProgressText,
    string SpeedText,
    string RemainingFilesText,
    string RemainingThreadsText);

public sealed record PixelDownloadPageMessages(
    PixelDownloadWindowMessages Window,
    string VersionLoadingText,
    PixelDownloadInstallPanelMessages InstallPanel,
    PixelDownloadInstallSidebarMessages InstallSidebar,
    PixelDownloadVersionListMessages VersionList,
    PixelDownloadTaskDetailsMessages TaskDetails,
    PixelDownloadManagerStatsMessages ManagerStats);

public sealed record PixelDownloadWindowMessages(
    string RefreshStartMessage,
    string InstallCompletedMessageFormat)
{
    public string GetInstallCompletedMessage(string instanceName) =>
        string.Format(InstallCompletedMessageFormat, instanceName);
}

public sealed record PixelMinecraftInstanceInstalledSnapshot(
    string Name,
    string VersionDirectory)
{
    internal static PixelMinecraftInstanceInstalledSnapshot FromInstance(MinecraftInstanceInfo instance) =>
        new(instance.Name, instance.VersionDirectory);
}

public sealed record PixelDownloadSidebarSnapshot(
    IReadOnlyList<PixelDownloadSidebarGroupSnapshot> Groups);

public sealed record PixelDownloadSidebarGroupSnapshot(
    string? Title,
    IReadOnlyList<PixelDownloadSidebarItemSnapshot> Items);

public sealed record PixelDownloadSidebarItemSnapshot(
    int Category,
    string Title,
    string Icon,
    bool IsSelected,
    string RefreshButtonTooltip);

public sealed record PixelDownloadPendingPageSnapshot(
    string Title,
    string Description,
    string StatusTitle,
    IReadOnlyList<string> StatusLines,
    string InstanceManagementTitle);

public sealed record PixelDownloadInstallStateSnapshot(
    bool CanInstall,
    bool IsBusy,
    string StatusText,
    string SelectedLoaderLabel,
    string PrimaryActionText,
    string PrimaryActionIcon,
    string SecondaryTitle,
    bool HasPendingOperation);

public sealed record PixelDownloadInstallSidebarSnapshot(
    string InstanceName,
    string TargetFolder,
    IReadOnlyList<PixelInstallChecklistItemSnapshot> ChecklistItems,
    bool ShowVanillaInstanceText,
    PixelDownloadInstallStateSnapshot InstallState);

public sealed record PixelInstallHintSnapshot(
    string Text,
    PixelInstallHintKind Kind);

public sealed record PixelLoaderChoiceSnapshot(
    string Title,
    string CardTitle,
    string Description,
    string Status,
    bool CanSelect,
    bool IsActive,
    string EmptyText,
    string? ClearActionId,
    IReadOnlyList<PixelLoaderChoiceItemSnapshot> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record PixelLoaderChoiceItemSnapshot(
    string Id,
    string Title,
    string Info,
    string Icon,
    bool IsChecked,
    bool IsEnabled);

public sealed record PixelInstallChecklistItemSnapshot(
    string? ClearActionId,
    string Title,
    string Info,
    string Icon,
    bool CanClear);

public sealed record PixelDownloadTaskSnapshot(
    string Id,
    string Name,
    string Text,
    string Info,
    string Icon,
    double Progress,
    string ProgressPercentText,
    long SpeedBytesPerSecond,
    PixelDownloadTaskStatus Status,
    bool CanCancel);

public enum PixelDownloadTaskDetailsPageKind
{
    Tasks,
    Finishing,
    Empty
}

public sealed record PixelDownloadTaskDetailsPageSnapshot(
    PixelDownloadTaskDetailsPageKind Kind,
    string Title,
    string BodyText,
    IReadOnlyList<PixelDownloadTaskSnapshot> Tasks,
    IReadOnlyList<string> CancellableTaskIds)
{
    public bool HasCancellableTasks => CancellableTaskIds.Count > 0;
}

internal sealed record PixelDownloadSidebarGroupDefinition(
    string? Title,
    IReadOnlyList<PixelDownloadSidebarItemDefinition> Items);

internal sealed record PixelDownloadSidebarItemDefinition(
    int Category,
    string Title,
    string Icon);
