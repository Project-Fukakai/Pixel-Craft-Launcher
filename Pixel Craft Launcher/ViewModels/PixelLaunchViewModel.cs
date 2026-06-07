using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Services;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace Pixel_Craft_Launcher.ViewModels;

public sealed class PixelLaunchViewModel : ViewModelBase
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

    private readonly MinecraftLaunchService _launchService = new();
    private readonly MinecraftProcessMonitor _processMonitor = new();
    private readonly MinecraftRepairService _repairService = new();
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

    public PixelLaunchViewModel()
    {
        LoadingState = new MyLoadingStateSimulator { LoadingState = MyLoadingState.Stop };
        ProfileService = new MinecraftProfileService();
        ProfileService.ProfilesChanged += (_, _) => Dispatcher.UIThread.Post(RefreshProfileBindings, DispatcherPriority.Background);
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

    public IReadOnlyList<MinecraftInstanceInfo> InstanceModels => _instanceModels;

    public ObservableCollection<string> Profiles { get; } = [];

    public MinecraftProfileService ProfileService { get; }

    public MyLoadingStateSimulator LoadingState { get; }

    public string OfflineSkinName => _offlineSkinName;

    public string SelectedInstancePath
    {
        get => _selectedInstancePath;
        set
        {
            if (SetField(ref _selectedInstancePath, value))
            {
                OnPropertyChanged(nameof(SelectedInstanceName));
                OnPropertyChanged(nameof(SelectedInstance));
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
                _ = ProfileService.SelectProfileAsync(profile);
            LaunchLog = BuildLaunchLog();
            OnPropertyChanged(nameof(CanLaunch));
        }
    }

    public string SelectedProfileMethod => MinecraftProfileService.GetProfileTypeName(ProfileService.SelectedProfile);

    public bool IsLaunching
    {
        get => _isLaunching;
        private set => SetField(ref _isLaunching, value);
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

    public MinecraftProcessExitInfo? LastExitInfo
    {
        get => _lastExitInfo;
        private set => SetField(ref _lastExitInfo, value);
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

    public MinecraftInstanceInfo? SelectedInstance => CurrentInstance;

    public event EventHandler<MinecraftLaunchResult>? LaunchSucceeded;
    public event EventHandler<MinecraftLaunchResult>? LaunchFailed;
    public event EventHandler<MinecraftProcessExitInfo>? GameExited;
    public event EventHandler<MinecraftProcessExitInfo>? GameCrashed;
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

    public void SelectInstance(MinecraftInstanceInfo instance)
    {
        SelectedInstancePath = instance.VersionDirectory;
        StatusText = SelectedInstanceName;
        LaunchLog = BuildLaunchLog();
        OnPropertyChanged(nameof(CanLaunch));
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

    private async Task StartLaunchAsync()
    {
        var instance = CurrentInstance;
        if (instance is null)
            return;

        if (!MinecraftProcessMonitor.IsMacAccessibilityTrusted)
        {
            MinecraftProcessMonitor.RequestMacAccessibilityAccess();
            AccessibilityPermissionRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsLaunching = true;
        _launchingShownAt = DateTimeOffset.Now;
        Stage = "预检测";
        StatusText = SelectedInstanceName;
        LaunchTitleText = "正在启动游戏";
        LaunchProgress = 0;
        LoadingState.LoadingState = MyLoadingState.Loading;
        LoadingState.SetProgress(0);
        LaunchLog = BuildLaunchLog() + "\n[Pixel] 启动流程已交由 PCL.Core。";
        _launchCancellation?.Dispose();
        _launchCancellation = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanLaunch));

        try
        {
            await DebugSettingsService.DelayIfNeededAsync("启动游戏", _launchCancellation.Token).ConfigureAwait(false);
            var request = new MinecraftLaunchRequest(
                instance,
                CreateAccountProvider(),
                Options: new MinecraftLaunchOptions
                {
                    UseDownloadRepairStep = true
                },
                FileRepairer: _repairService);
            var progress = new Progress<MinecraftLaunchProgress>(OnLaunchProgress);
            var result = await _launchService.LaunchAsync(request, progress, _launchCancellation.Token).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
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
                        BeginGameMonitor(result.Process, instance.Name);
                    }
                    else
                    {
                        IsLaunching = false;
                        LoadingState.LoadingState = MyLoadingState.Stop;
                    }
                    LaunchSucceeded?.Invoke(this, result);
                }
                else
                {
                    IsLaunching = false;
                    LoadingState.LoadingState = MyLoadingState.Stop;
                    Stage = result.FailureKind == MinecraftLaunchFailureKind.Cancelled ? "已取消" : "启动失败";
                    LaunchTitleText = Stage;
                    StatusText = result.Message;
                    AppendLog("[Core] " + result.Message);
                    LastCrashAnalysis = AnalyzeLaunchFailure(result.Message);
                    LaunchFailed?.Invoke(this, result);
                }
                OnPropertyChanged(nameof(CanLaunch));
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsLaunching = false;
                LoadingState.LoadingState = MyLoadingState.Stop;
                Stage = "启动失败";
                LaunchTitleText = "启动失败";
                StatusText = ex.Message;
                AppendLog("[Core] " + ex.Message);
                LastCrashAnalysis = AnalyzeLaunchFailure(ex.Message);
                LaunchFailed?.Invoke(this, MinecraftLaunchResult.Failed(MinecraftLaunchFailureKind.Unknown, ex.Message));
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
        if (_runningProcess is { } process)
        {
            try
            {
                _forceCloseRequested = true;
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                AppendLog("[Pixel] 取消启动时结束进程失败：" + ex.Message);
            }
        }
        IsLaunching = false;
        IsGameRunning = false;
        _runningProcess = null;
        Stage = "已取消";
        LaunchTitleText = "已取消";
        LoadingState.LoadingState = MyLoadingState.Stop;
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
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            AppendLog("[Pixel] 强制关闭失败：" + ex.Message);
        }
    }

    public string ExportLaunchLog()
    {
        var directory = Path.Combine(Paths.SharedLocalData, "Launch", "Logs");
        Directory.CreateDirectory(directory);
        var fileName = $"launch-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, LaunchLog);
        LastExportedLogPath = path;
        return path;
    }

    private MinecraftInstanceInfo? CurrentInstance => _instanceModels.FirstOrDefault(instance => instance.VersionDirectory == SelectedInstancePath);

    private IMinecraftAccountProvider CreateAccountProvider()
    {
        return ProfileService.CreateAccountProvider(new LaunchProfileCallbacks(), new Progress<MinecraftProfileLoginProgress>(progress =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Stage = progress.Stage;
                StatusText = progress.Message;
                LaunchProgress = Math.Clamp(progress.Progress, 0, 1);
            }, DispatcherPriority.Background);
        }));
    }

    private void OnLaunchProgress(MinecraftLaunchProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
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
        LoadingState.LoadingState = MyLoadingState.Loading;
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
            var exit = await _processMonitor.MonitorAsync(process, instanceName, detected =>
            {
                _ = StopLaunchingAfterMinimumDisplayAsync(() =>
                {
                    if (!ReferenceEquals(_runningProcess, process) || !IsGameRunning)
                        return;

                    IsGameWindowDetected = detected;
                    IsLaunching = false;
                    LoadingState.LoadingState = MyLoadingState.Stop;
                    Stage = detected ? "游戏运行中" : Stage;
                    LaunchTitleText = "已启动游戏";
                    StatusText = detected ? "已检测到游戏窗口。" : StatusText;
                    AppendLog("[Pixel] 已检测到游戏窗口。");
                });
            }, () =>
            {
                _ = StopLaunchingAfterMinimumDisplayAsync(() =>
                {
                    if (!ReferenceEquals(_runningProcess, process) || !IsGameRunning)
                        return;

                    IsLaunching = false;
                    LoadingState.LoadingState = MyLoadingState.Stop;
                    Stage = "游戏运行中";
                    LaunchTitleText = "已启动游戏";
                    StatusText = "窗口探测超时，游戏进程仍在运行。";
                    AppendLog("[Pixel] 窗口探测超时，已结束启动 Loading 并继续监控进程。");
                });
            }, cancellationToken).ConfigureAwait(false);
            exit = exit with { WasKilled = _forceCloseRequested };
            Dispatcher.UIThread.Post(() => HandleGameExited(exit), DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // A new launch or application shutdown took ownership of monitoring.
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                AppendLog("[Pixel] 进程监控失败：" + ex.Message);
                IsGameRunning = false;
                _runningProcess = null;
            }, DispatcherPriority.Background);
        }
    }

    private async Task StopLaunchingAfterMinimumDisplayAsync(Action stopAction)
    {
        var remaining = TimeSpan.FromMilliseconds(520) - (DateTimeOffset.Now - _launchingShownAt);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining).ConfigureAwait(false);

        Dispatcher.UIThread.Post(stopAction, DispatcherPriority.Background);
    }

    private void HandleGameExited(MinecraftProcessExitInfo exit)
    {
        LastExitInfo = exit;
        IsLaunching = false;
        LoadingState.LoadingState = MyLoadingState.Stop;
        IsGameRunning = false;
        _runningProcess = null;
        Stage = exit.WasKilled ? "已强制关闭" : "游戏已退出";
        StatusText = exit.ExitCode is 0 or null ? "游戏已正常退出。" : $"游戏异常退出，退出码 {exit.ExitCode}。";
        AppendLog($"[Pixel] 游戏进程已退出。ExitCode={exit.ExitCode?.ToString() ?? "未知"} WindowDetected={exit.WindowDetected} Killed={exit.WasKilled}");
        OnPropertyChanged(nameof(CanLaunch));

        GameExited?.Invoke(this, exit);
        if (!exit.WasKilled && exit.ExitCode is not 0 and not null)
        {
            LastCrashAnalysis = AnalyzeCrashLog(LaunchLog, exit.ExitCode);
            GameCrashed?.Invoke(this, exit);
        }
    }

    private static string AnalyzeLaunchFailure(string message)
    {
        return AnalyzeCrashLog(message, null);
    }

    private static string AnalyzeCrashLog(string log, int? exitCode)
    {
        var lower = log.ToLowerInvariant();
        var reasons = new List<string>();
        if (lower.Contains("outofmemoryerror") || lower.Contains("java heap space"))
            reasons.Add("内存不足：尝试提高游戏内存分配，或减少资源包、光影、Mod 数量。");
        if (lower.Contains("unsupportedclassversionerror"))
            reasons.Add("Java 版本不匹配：当前实例可能需要更高或更低版本的 Java。");
        if (lower.Contains("classnotfoundexception") || lower.Contains("noclassdeffounderror"))
            reasons.Add("缺少类或依赖：实例库文件、Mod 前置或加载器可能缺失。");
        if (lower.Contains("mod loading error") || lower.Contains("failed to load mod") || lower.Contains("fabricloader") || lower.Contains("forge mod loader"))
            reasons.Add("Mod 加载失败：检查最近新增或更新的 Mod，并确认前置依赖与游戏版本一致。");
        if (lower.Contains("glfw") || lower.Contains("opengl") || lower.Contains("lwjgl"))
            reasons.Add("图形环境异常：尝试更新显卡驱动，或切换渲染器/关闭光影。");
        if (lower.Contains("access denied") || lower.Contains("另一个程序正在使用此文件") || lower.Contains("being used by another process"))
            reasons.Add("文件被占用或权限不足：关闭占用文件的程序，并检查启动器目录权限。");
        if (lower.Contains("invalid session") || lower.Contains("authentication") || lower.Contains("authlib"))
            reasons.Add("登录或认证异常：重新登录账号，或检查第三方认证服务器配置。");
        if (exitCode is not null && reasons.Count == 0)
            reasons.Add($"游戏进程以非零退出码 {exitCode} 结束，但日志中没有命中常见特征。建议导出日志进一步排查。");
        if (reasons.Count == 0)
            reasons.Add("暂未识别出明确原因。建议导出完整启动日志并检查最新的游戏日志或崩溃报告。");

        return string.Join("\n", reasons.Select(static reason => "- " + reason));
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
