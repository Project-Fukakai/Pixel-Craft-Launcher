using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.App.Pixel.ViewModels;

public sealed class PixelLaunchViewModel : PixelViewModelBase
{
    private static readonly string[] DefaultOfflineSkins =
    [
        "Alex",
        "Ari",
        "Efe",
        "Kai",
        "Makena",
        "Noor",
        "Steve",
        "Sunny",
        "Zuri"
    ];

    private readonly MinecraftLaunchService _launchService;
    private readonly MinecraftProcessMonitor _processMonitor;
    private readonly MinecraftRepairService _repairService;
    private readonly PixelLaunchSummaryService _summaryService;
    private readonly PixelLaunchDialogService _dialogService;
    private readonly IPixelCommandBus _commandBus;
    private readonly PixelLaunchStateMachine? _launchStateMachine;
    private readonly IPixelOperationDelayService _operationDelayService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly List<MinecraftInstanceInfo> _instanceModels = [];
    private readonly string _offlineSkinName = DefaultOfflineSkins[Random.Shared.Next(DefaultOfflineSkins.Length)];
    private CancellationTokenSource? _launchCancellation;
    private CancellationTokenSource? _gameMonitorCancellation;
    private Process? _runningProcess;
    private string _selectedInstancePath = string.Empty;
    private bool _isLaunching;
    private bool _isGameRunning;
    private bool _isGameWindowDetected;
    private bool _forceCloseRequested;
    private string _stage = "初始化";
    private string _statusText = "正在加载版本列表，请稍候";
    private string _launchLog = "等待启动器核心初始化...";
    private double _launchProgress;
    private string _launchProgressText = "0.00 %";
    private string _launchTitleText = "正在启动游戏";
    private MinecraftProcessExitInfo? _lastExitInfo;
    private string _lastCrashAnalysis = string.Empty;
    private string _lastExportedLogPath = string.Empty;
    private DateTimeOffset _launchingShownAt;

    public PixelLaunchViewModel(
        MinecraftLaunchService launchService,
        MinecraftProcessMonitor processMonitor,
        MinecraftRepairService repairService,
        MinecraftProfileService profileService,
        PixelLaunchSummaryService summaryService,
        PixelLaunchDialogService dialogService,
        IPixelOperationDelayService operationDelayService,
        IUiDispatcher uiDispatcher,
        IPixelCommandBus commandBus,
        PixelLaunchStateMachine? launchStateMachine = null)
    {
        _launchService = launchService;
        _processMonitor = processMonitor;
        _repairService = repairService;
        _summaryService = summaryService;
        _dialogService = dialogService;
        _commandBus = commandBus;
        _launchStateMachine = launchStateMachine;
        _operationDelayService = operationDelayService;
        _uiDispatcher = uiDispatcher;
        LoadingState = new PixelLoadingStateController { State = PixelLoadingState.Stop };
        ProfileService = profileService;
        ProfileService.ProfilesChanged += (_, _) => _uiDispatcher.Post(RefreshProfileBindings);
        try
        {
            ProfileService.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _launchLog = "档案系统初始化失败：" + ex.Message;
        }
        RefreshProfileBindings();
        RefreshInstances();
    }

    public ObservableCollection<string> Instances { get; } = [];

    internal IReadOnlyList<MinecraftInstanceInfo> InstanceModels => _instanceModels;

    public ObservableCollection<string> Profiles { get; } = [];

    private MinecraftProfileService ProfileService { get; }

    public PixelLoadingStateController LoadingState { get; }

    public string OfflineSkinName => _offlineSkinName;

    public string SelectedInstancePath
    {
        get => _selectedInstancePath;
        set
        {
            if (SetField(ref _selectedInstancePath, value))
            {
                OnPropertyChanged(nameof(SelectedInstanceName));
                OnPropertyChanged(nameof(SelectedInstancePath));
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string SelectedInstanceName => string.IsNullOrWhiteSpace(SelectedInstancePath)
        ? "未找到可用的游戏实例"
        : _instanceModels.FirstOrDefault(instance => instance.VersionDirectory == SelectedInstancePath)?.Name
          ?? Path.GetFileName(SelectedInstancePath);

    public string SelectedProfile
    {
        get => ProfileService.SelectedProfile?.Username ?? "未选择档案";
        set
        {
            var profile = ProfileService.Profiles.FirstOrDefault(item => string.Equals(item.Username, value, StringComparison.Ordinal));
            if (profile is not null)
            {
                _ = RequireCommandBus().Send(new SelectMinecraftProfileByIdCommand(profile.Id));
            }
            LaunchLog = BuildLaunchLog();
            OnPropertyChanged(nameof(CanLaunch));
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string SelectedProfileMethod => PixelProfileListService.GetProfileTypeName(ProfileService.SelectedProfile);

    public bool IsLaunching
    {
        get => _isLaunching;
        private set
        {
            if (!SetField(ref _isLaunching, value)) return;
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Stage
    {
        get => _stage;
        private set => SetField(ref _stage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string LaunchLog
    {
        get => _launchLog;
        private set => SetField(ref _launchLog, value);
    }

    public double LaunchProgress
    {
        get => _launchProgress;
        private set
        {
            if (!SetField(ref _launchProgress, Math.Clamp(value, 0, 1))) return;
            LaunchProgressText = $"{_launchProgress * 100:0.00} %";
        }
    }

    public string LaunchProgressText
    {
        get => _launchProgressText;
        private set => SetField(ref _launchProgressText, value);
    }

    public string LaunchTitleText
    {
        get => _launchTitleText;
        private set => SetField(ref _launchTitleText, value);
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set
        {
            if (!SetField(ref _isGameRunning, value)) return;
            OnPropertyChanged(nameof(CanLaunch));
        }
    }

    public bool IsGameWindowDetected
    {
        get => _isGameWindowDetected;
        private set => SetField(ref _isGameWindowDetected, value);
    }

    private MinecraftProcessExitInfo? LastExitInfo
    {
        get => _lastExitInfo;
        set => SetField(ref _lastExitInfo, value);
    }

    public string LastCrashAnalysis
    {
        get => _lastCrashAnalysis;
        private set => SetField(ref _lastCrashAnalysis, value);
    }

    public string LastExportedLogPath
    {
        get => _lastExportedLogPath;
        private set => SetField(ref _lastExportedLogPath, value);
    }

    public string LaunchButtonText => Instances.Count == 0 ? "下载游戏" : "启动游戏";

    public bool CanLaunch => !IsLaunching && CurrentInstance is not null && ProfileService.SelectedProfile is not null;

    public PixelLaunchMainButtonSnapshot GetMainButtonSnapshot()
    {
        var action = Instances.Count == 0
            ? PixelLaunchPrimaryAction.DownloadGame
            : PixelLaunchPrimaryAction.LaunchGame;
        return new PixelLaunchMainButtonSnapshot(
            LaunchButtonText,
            SelectedInstanceName,
            action,
            action == PixelLaunchPrimaryAction.DownloadGame || CanLaunch);
    }

    public PixelLaunchLaunchingPanelSnapshot GetLaunchingPanelSnapshot()
    {
        return new PixelLaunchLaunchingPanelSnapshot(
            IsLaunching,
            LaunchTitleText,
            SelectedInstanceName,
            Stage,
            Summary.ProfileMethod,
            LaunchProgress,
            LaunchProgressText);
    }

    private MinecraftInstanceInfo? SelectedInstance => CurrentInstance;

    public PixelLaunchSummarySnapshot Summary => _summaryService.GetSummary(
        CurrentInstance,
        SelectedInstanceName,
        ProfileService.SelectedProfile,
        CanLaunch);

    public PixelLaunchSecondaryActionSnapshot GetSecondaryActionSnapshot(PixelLaunchSidebarService sidebarService) =>
        sidebarService.GetSecondaryActions(CurrentInstance, IsLaunching);

    public PixelLaunchSidebarSnapshot GetSidebarSnapshot(PixelLaunchSidebarService sidebarService) =>
        sidebarService.GetSidebarSnapshot(CurrentInstance, IsLaunching);

    public PixelLaunchInstanceSelectionPageSnapshot GetInstanceSelectionPageSnapshot(
        PixelLaunchInstanceListService instanceListService,
        string? selectedFolder) =>
        instanceListService.GetSelectionPageSnapshot(
            _instanceModels,
            selectedFolder,
            Summary.InstancePath ?? string.Empty);

    public PixelLaunchSelectedInstanceFolderSnapshot GetSelectedInstanceFolderSnapshot(
        PixelLaunchSidebarService sidebarService) =>
        sidebarService.GetSelectedInstanceFolder(CurrentInstance);

    public PixelMemoryPreviewSnapshot GetMemoryPreviewSnapshot(PixelMemoryPreviewService memoryPreviewService) =>
        memoryPreviewService.GetSnapshot(CurrentInstance);

    public PixelLaunchState LaunchState => _launchStateMachine?.State ?? PixelLaunchState.Idle;

    public event EventHandler<PixelLaunchResultSnapshot>? LaunchSucceeded;
    public event EventHandler<PixelLaunchResultSnapshot>? LaunchFailed;
    public event EventHandler<PixelGameExitedSnapshot>? GameExited;
    public event EventHandler<PixelGameExitedSnapshot>? GameCrashed;
    public event EventHandler<PixelLaunchIssueDialogSnapshot>? LaunchIssueDialogRequested;
    public event EventHandler? AccessibilityPermissionRequired;

    public void RefreshInstances()
    {
        Instances.Clear();
        _instanceModels.Clear();
        foreach (var instance in MinecraftInstanceScanner.ScanDefaultFolders())
        {
            _instanceModels.Add(instance);
            Instances.Add(instance.Name);
        }

        SelectedInstancePath = _instanceModels.FirstOrDefault()?.VersionDirectory ?? string.Empty;
        StatusText = Instances.Count == 0 ? "未找到可用的游戏实例" : SelectedInstanceName;
        LaunchLog = BuildLaunchLog();
        OnPropertyChanged(nameof(InstanceModels));
        OnPropertyChanged(nameof(LaunchButtonText));
        OnPropertyChanged(nameof(CanLaunch));
    }

    private void SelectInstance(MinecraftInstanceInfo instance)
    {
        SelectedInstancePath = instance.VersionDirectory;
        StatusText = SelectedInstanceName;
        LaunchLog = BuildLaunchLog();
        OnPropertyChanged(nameof(CanLaunch));
    }

    public void SelectInstanceByPath(string versionDirectory)
    {
        var instance = _instanceModels.FirstOrDefault(item =>
            string.Equals(item.VersionDirectory, versionDirectory, StringComparison.OrdinalIgnoreCase));
        if (instance is null)
            return;

        SelectInstance(instance);
    }

    public string CheckAutoModpack()
    {
        var baseDir = AppContext.BaseDirectory;
        var zip = Path.Combine(baseDir, "modpack.zip");
        var mrpack = Path.Combine(baseDir, "modpack.mrpack");
        if (File.Exists(zip))
            return zip;
        return File.Exists(mrpack) ? mrpack : string.Empty;
    }

    public void StartLaunch()
    {
        if (!CanLaunch)
            return;

        _ = StartLaunchAsync();
    }

    public void RequestAccessibilityPermission()
    {
        MinecraftProcessMonitor.RequestMacAccessibilityAccess();
    }

    private async Task StartLaunchAsync()
    {
        var instance = CurrentInstance;
        if (instance is null)
            return;

        if (!MinecraftProcessMonitor.IsMacAccessibilityTrusted)
        {
            RequestAccessibilityPermission();
            AccessibilityPermissionRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsLaunching = true;
        TryFireLaunchTrigger(PixelLaunchTrigger.Start);
        _launchingShownAt = DateTimeOffset.Now;
        Stage = "预检测";
        StatusText = SelectedInstanceName;
        LaunchTitleText = "正在启动游戏";
        LaunchProgress = 0;
        LoadingState.State = PixelLoadingState.Loading;
        LoadingState.SetProgress(0);
        LaunchLog = BuildLaunchLog() + "\n[Pixel] 启动流程已交由 PCL.Core。";
        _launchCancellation?.Dispose();
        _launchCancellation = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanLaunch));

        try
        {
            await _operationDelayService.DelayIfNeededAsync("启动游戏", _launchCancellation.Token).ConfigureAwait(false);
            TryFireLaunchTrigger(PixelLaunchTrigger.LaunchCoreStarted);
            var progress = new Progress<MinecraftLaunchProgress>(OnLaunchProgress);
            var result = await RequireCommandBus()
                .Send(
                    new StartMinecraftLaunchCommand(
                        instance,
                        CreateAccountProvider(),
                        progress,
                        new MinecraftLaunchOptions
                        {
                            UseDownloadRepairStep = true
                        }),
                    _launchCancellation.Token)
                .ConfigureAwait(false);
            _uiDispatcher.Post(() =>
            {
                if (result.Success)
                {
                    Stage = result.Process is null ? "已完成" : "等待游戏窗口出现";
                    LaunchTitleText = result.Process is null ? "已完成" : "正在启动游戏";
                    LaunchProgress = 1;
                    StatusText = result.Message;
                    AppendLog("[Core] " + result.Message);
                    if (result.Process is not null)
                    {
                        TryFireLaunchTrigger(PixelLaunchTrigger.ProcessStarted);
                        BeginGameMonitor(result.Process, instance.Name);
                    }
                    else
                    {
                        TryFireLaunchTrigger(PixelLaunchTrigger.FinishedWithoutProcess);
                        IsLaunching = false;
                        LoadingState.State = PixelLoadingState.Stop;
                    }
                    LaunchSucceeded?.Invoke(this, PixelLaunchResultSnapshot.FromResult(result));
                }
                else
                {
                    TryFireLaunchTrigger(result.FailureKind == MinecraftLaunchFailureKind.Cancelled
                        ? PixelLaunchTrigger.Cancel
                        : PixelLaunchTrigger.Fail);
                    IsLaunching = false;
                    LoadingState.State = PixelLoadingState.Stop;
                    Stage = result.FailureKind == MinecraftLaunchFailureKind.Cancelled ? "已取消" : "启动失败";
                    LaunchTitleText = Stage;
                    StatusText = result.Message;
                    AppendLog("[Core] " + result.Message);
                    LastCrashAnalysis = MinecraftLaunchLogAnalyzer.AnalyzeLaunchFailure(result.Message);
                    LaunchFailed?.Invoke(this, PixelLaunchResultSnapshot.FromResult(result));
                    LaunchIssueDialogRequested?.Invoke(this, _dialogService.GetLaunchFailureDialog(result, LastCrashAnalysis));
                }
                OnPropertyChanged(nameof(CanLaunch));
            });
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                TryFireLaunchTrigger(ex is OperationCanceledException ? PixelLaunchTrigger.Cancel : PixelLaunchTrigger.Fail);
                IsLaunching = false;
                LoadingState.State = PixelLoadingState.Stop;
                Stage = "启动失败";
                LaunchTitleText = "启动失败";
                StatusText = ex.Message;
                AppendLog("[Core] " + ex.Message);
                LastCrashAnalysis = MinecraftLaunchLogAnalyzer.AnalyzeLaunchFailure(ex.Message);
                var result = MinecraftLaunchResult.Failed(MinecraftLaunchFailureKind.Unknown, ex.Message);
                LaunchFailed?.Invoke(this, PixelLaunchResultSnapshot.FromResult(result));
                LaunchIssueDialogRequested?.Invoke(this, _dialogService.GetLaunchFailureDialog(result, LastCrashAnalysis));
                OnPropertyChanged(nameof(CanLaunch));
            });
        }
    }

    public void CancelLaunch()
    {
        if (!IsLaunching && !IsGameRunning)
            return;

        _launchCancellation?.Cancel();
        _gameMonitorCancellation?.Cancel();
        TryFireLaunchTrigger(_runningProcess is null ? PixelLaunchTrigger.Cancel : PixelLaunchTrigger.ForceCloseRequested);
        if (_runningProcess is { } process)
        {
            _forceCloseRequested = true;
            TryKillRunningProcess(process, "CancelLaunch", "[Pixel] 取消启动时结束进程失败：");
        }
        IsLaunching = false;
        IsGameRunning = false;
        _runningProcess = null;
        Stage = "已取消";
        LaunchTitleText = "已取消";
        LoadingState.State = PixelLoadingState.Stop;
        AppendLog("[Pixel] 已请求取消启动。");
        OnPropertyChanged(nameof(CanLaunch));
    }

    public void ForceCloseGame()
    {
        var process = _runningProcess;
        if (process is null)
            return;

        try
        {
            _forceCloseRequested = true;
            AppendLog("[Pixel] 已请求强制关闭游戏进程。");
            TryFireLaunchTrigger(PixelLaunchTrigger.ForceCloseRequested);
            TryKillRunningProcess(process, "ForceCloseGame", "[Pixel] 强制关闭失败：");
        }
        catch (Exception ex)
        {
            AppendLog("[Pixel] 强制关闭失败：" + ex.Message);
        }
    }

    private void TryKillRunningProcess(Process process, string reason, string failurePrefix)
    {
        try
        {
            _ = RequireCommandBus()
                .Send(new KillMinecraftProcessCommand(process, reason))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            AppendLog(failurePrefix + ex.Message);
        }
    }

    public string ExportLaunchLog()
    {
        var path = RequireCommandBus()
            .Send(new ExportLaunchLogCommand(LaunchLog))
            .GetAwaiter()
            .GetResult();
        LastExportedLogPath = path;
        return path;
    }

    public static PixelLaunchWindowMessages GetWindowMessages()
    {
        return new PixelLaunchWindowMessages(
            "已强制关闭 Minecraft。",
            "{0} 已退出。",
            "启动日志已导出：{0}");
    }

    public static string GetGameKilledMessage() => GetWindowMessages().GameKilledMessage;

    public static string GetGameExitedMessage(PixelGameExitedSnapshot exit) =>
        GetWindowMessages().GetGameExitedMessage(exit);

    public static string GetLaunchLogExportedMessage(string path) =>
        GetWindowMessages().GetLaunchLogExportedMessage(path);

    public PixelLaunchWindowMessages GetWindowMessagesSnapshot() => GetWindowMessages();

    private MinecraftInstanceInfo? CurrentInstance => _instanceModels.FirstOrDefault(instance => instance.VersionDirectory == SelectedInstancePath);

    private IMinecraftAccountProvider CreateAccountProvider()
    {
        return ProfileService.CreateAccountProvider(new LaunchProfileCallbacks(), new Progress<MinecraftProfileLoginProgress>(progress =>
        {
            _uiDispatcher.Post(() =>
            {
                Stage = progress.Stage;
                StatusText = progress.Message;
                LaunchProgress = Math.Clamp(progress.Progress, 0, 1);
            });
        }));
    }

    private void OnLaunchProgress(MinecraftLaunchProgress progress)
    {
        _uiDispatcher.Post(() =>
        {
            var normalizedProgress = Math.Clamp(progress.Progress, 0, 1);
            LoadingState.SetProgress(normalizedProgress);
            LaunchProgress = normalizedProgress;
            Stage = progress.Stage switch
            {
                MinecraftLaunchStage.Precheck => "预检测",
                MinecraftLaunchStage.SelectJava => "获取 Java",
                MinecraftLaunchStage.Account => "登录档案",
                MinecraftLaunchStage.PrepareFiles => progress.Message.Contains("补全", StringComparison.OrdinalIgnoreCase) ? "补全启动文件" : "检查启动文件",
                MinecraftLaunchStage.BuildArguments => "获取启动参数",
                MinecraftLaunchStage.ExtractNatives => "解压文件",
                MinecraftLaunchStage.PreLaunchCommand => "执行自定义命令",
                MinecraftLaunchStage.StartProcess => "启动进程",
                MinecraftLaunchStage.Finished => "等待游戏窗口出现",
                MinecraftLaunchStage.Cancelled => "已取消",
                MinecraftLaunchStage.Failed => "启动失败",
                _ => progress.Message
            };
            if (progress.Stage == MinecraftLaunchStage.Finished)
            {
                LaunchTitleText = "正在启动游戏";
                LaunchProgress = 1;
            }
            StatusText = progress.Message;
            if (!string.IsNullOrWhiteSpace(progress.LogLine))
                AppendLog("[Core] " + progress.LogLine);
        });
    }

    private void AppendLog(string line)
    {
        var maxLines = Math.Clamp(Config.System.MaxGameLog, 1, 200);
        var lines = (LaunchLog + "\n" + line)
            .Split('\n', StringSplitOptions.None);
        if (lines.Length > maxLines)
            lines = lines[^maxLines..];
        LaunchLog = string.Join('\n', lines);
    }

    private void BeginGameMonitor(Process process, string instanceName)
    {
        _gameMonitorCancellation?.Cancel();
        _gameMonitorCancellation?.Dispose();
        _gameMonitorCancellation = new CancellationTokenSource();
        _runningProcess = process;
        _forceCloseRequested = false;
        IsGameRunning = true;
        IsGameWindowDetected = false;
        IsLaunching = true;
        _launchingShownAt = DateTimeOffset.Now;
        LoadingState.State = PixelLoadingState.Loading;
        LoadingState.SetProgress(1);
        Stage = "等待游戏窗口出现";
        LaunchTitleText = "正在启动游戏";
        StatusText = "游戏进程已启动，正在监控窗口和进程。";
        OnPropertyChanged(nameof(CanLaunch));
        _ = MonitorGameProcessAsync(process, instanceName, _gameMonitorCancellation.Token);
    }

    private async Task MonitorGameProcessAsync(Process process, string instanceName, CancellationToken cancellationToken)
    {
        try
        {
            void OnWindowDetected(bool detected)
            {
                _ = StopLaunchingAfterMinimumDisplayAsync(() =>
                {
                    if (!ReferenceEquals(_runningProcess, process) || !IsGameRunning)
                        return;

                    IsGameWindowDetected = detected;
                    TryFireLaunchTrigger(PixelLaunchTrigger.WindowDetected);
                    IsLaunching = false;
                    LoadingState.State = PixelLoadingState.Stop;
                    Stage = detected ? "游戏运行中" : Stage;
                    LaunchTitleText = "已启动游戏";
                    StatusText = detected ? "已检测到游戏窗口。" : StatusText;
                    AppendLog("[Pixel] 已检测到游戏窗口。");
                });
            }

            void OnWindowProbeTimedOut()
            {
                _ = StopLaunchingAfterMinimumDisplayAsync(() =>
                {
                    if (!ReferenceEquals(_runningProcess, process) || !IsGameRunning)
                        return;

                    IsLaunching = false;
                    TryFireLaunchTrigger(PixelLaunchTrigger.WindowProbeTimedOut);
                    LoadingState.State = PixelLoadingState.Stop;
                    Stage = "游戏运行中";
                    LaunchTitleText = "已启动游戏";
                    StatusText = "窗口探测超时，游戏进程仍在运行。";
                    AppendLog("[Pixel] 窗口探测超时，已结束启动 Loading 并继续监控进程。");
                });
            }

            var exit = await RequireCommandBus()
                .Send(
                    new MonitorMinecraftProcessCommand(
                        process,
                        instanceName,
                        OnWindowDetected,
                        OnWindowProbeTimedOut),
                    cancellationToken)
                .ConfigureAwait(false);
            exit = exit with { WasKilled = _forceCloseRequested };
            _uiDispatcher.Post(() => HandleGameExited(exit));
        }
        catch (OperationCanceledException)
        {
            // A new launch or application shutdown took ownership of monitoring.
        }
        catch (Exception ex)
        {
            _uiDispatcher.Post(() =>
            {
                AppendLog("[Pixel] 进程监控失败：" + ex.Message);
                TryFireLaunchTrigger(PixelLaunchTrigger.Fail);
                IsGameRunning = false;
                _runningProcess = null;
            });
        }
    }

    private async Task StopLaunchingAfterMinimumDisplayAsync(Action stopAction)
    {
        var remaining = TimeSpan.FromMilliseconds(520) - (DateTimeOffset.Now - _launchingShownAt);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining).ConfigureAwait(false);

        _uiDispatcher.Post(stopAction);
    }

    private void HandleGameExited(MinecraftProcessExitInfo exit)
    {
        LastExitInfo = exit;
        TryFireLaunchTrigger(PixelLaunchTrigger.ProcessExited);
        IsLaunching = false;
        LoadingState.State = PixelLoadingState.Stop;
        IsGameRunning = false;
        _runningProcess = null;
        Stage = exit.WasKilled ? "已强制关闭" : "游戏已退出";
        StatusText = exit.ExitCode is 0 or null ? "游戏已正常退出。" : $"游戏异常退出，退出码 {exit.ExitCode}。";
        AppendLog($"[Pixel] 游戏进程已退出。ExitCode={exit.ExitCode?.ToString() ?? "未知"} WindowDetected={exit.WindowDetected} Killed={exit.WasKilled}");
        OnPropertyChanged(nameof(CanLaunch));

        var snapshot = PixelGameExitedSnapshot.FromExitInfo(exit);
        GameExited?.Invoke(this, snapshot);
        if (!exit.WasKilled && exit.ExitCode is not 0 and not null)
        {
            LastCrashAnalysis = MinecraftLaunchLogAnalyzer.AnalyzeCrashLog(LaunchLog, exit.ExitCode);
            GameCrashed?.Invoke(this, snapshot);
            LaunchIssueDialogRequested?.Invoke(this, _dialogService.GetCrashDialog(exit, LastCrashAnalysis));
        }
    }

    private void TryFireLaunchTrigger(PixelLaunchTrigger trigger)
    {
        if (_launchStateMachine?.CanFire(trigger) != true)
            return;

        _launchStateMachine.Fire(trigger);
        OnPropertyChanged(nameof(LaunchState));
    }

    private IPixelCommandBus RequireCommandBus()
    {
        return _commandBus;
    }

    private string BuildLaunchLog()
    {
        var modpack = CheckAutoModpack();
        var modpackLine = string.IsNullOrWhiteSpace(modpack)
            ? "未检测到自动安装整合包。"
            : $"检测到自动安装整合包：{modpack}";
        var pathLine = CurrentInstance is null ? "实例路径：无" : $"实例路径：{CurrentInstance.VersionDirectory}";
        return $"实例：{SelectedInstanceName}\n{pathLine}\n登录档案：{SelectedProfile}（{SelectedProfileMethod}）\n{modpackLine}";
    }

    public void RefreshProfileBindings()
    {
        Profiles.Clear();
        foreach (var profile in ProfileService.Profiles)
            Profiles.Add(profile.Username);
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(SelectedProfileMethod));
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(Summary));
        LaunchLog = BuildLaunchLog();
    }

    private sealed class LaunchProfileCallbacks : IMinecraftProfileUiCallbacks
    {
        public Task ShowDeviceCodeAsync(DeviceCodePrompt prompt, CancellationToken cancellationToken) =>
            throw new MinecraftProfileException("微软账号需要重新登录，请在档案管理中重新添加或刷新账号。");

        public Task<int?> SelectAuthlibProfileAsync(IReadOnlyList<(string Id, string Name)> profiles, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(0);
    }

}

public sealed record PixelLaunchWindowMessages(
    string GameKilledMessage,
    string GameExitedMessageFormat,
    string LaunchLogExportedMessageFormat)
{
    public string GetGameExitedMessage(PixelGameExitedSnapshot exit) =>
        string.Format(GameExitedMessageFormat, exit.InstanceName);

    public string GetLaunchLogExportedMessage(string path) =>
        string.Format(LaunchLogExportedMessageFormat, path);
}

public sealed record PixelLaunchResultSnapshot(string Message)
{
    internal static PixelLaunchResultSnapshot FromResult(MinecraftLaunchResult result) =>
        new(result.Message);
}

public sealed record PixelGameExitedSnapshot(
    string InstanceName,
    int? ExitCode,
    bool WasKilled)
{
    internal static PixelGameExitedSnapshot FromExitInfo(MinecraftProcessExitInfo exit) =>
        new(exit.InstanceName, exit.ExitCode, exit.WasKilled);
}

public enum PixelLaunchPrimaryAction
{
    DownloadGame,
    LaunchGame
}

public sealed record PixelLaunchMainButtonSnapshot(
    string PrimaryText,
    string SecondaryText,
    PixelLaunchPrimaryAction PrimaryAction,
    bool IsEnabled);

public sealed record PixelLaunchLaunchingPanelSnapshot(
    bool IsVisible,
    string Title,
    string InstanceName,
    string Stage,
    string ProfileMethod,
    double Progress,
    string ProgressText);
