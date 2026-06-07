using System;
using System.Collections.Generic;
using System.Net;
using PCL.Core.App.Essentials;
using PCL.Core.UI.Animation.Core;
using PCL.Core.App.IoC;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Core.App.Configuration;

[LifecycleService(LifecycleState.Loaded)]
[LifecycleScope("launcher-settings", "启动器设置业务")]
public sealed partial class LauncherSettingsService
{
    private static readonly List<(ConfigItem Item, ConfigObserver Observer)> _Observers = [];
    public static LauncherDownloadSettings CurrentDownloadSettings { get; private set; } = ResolveDownloadSettings();

    [LifecycleStart]
    private static void _Start()
    {
        ApplyAll();
        Observe("SystemDisableHardwareAcceleration", _ => ApplySystemSettings());
        Observe("SystemTelemetry", _ => ApplySystemSettings());
        Observe("SystemMaxLog", _ => ApplySystemSettings());
        Observe("UiAniFPS", _ => ApplySystemSettings());
        Observe("SystemNetEnableDoH", _ => ApplyNetworkSettings());
        Observe("SystemHttpProxy", _ => ApplyProxy());
        Observe("SystemHttpProxyType", _ => ApplyProxy());
        Observe("SystemHttpProxyCustomUsername", _ => ApplyProxy());
        Observe("SystemHttpProxyCustomPassword", _ => ApplyProxy());
        Observe("SystemDebugMode", _ => ApplyDebug());
        Observe("SystemDebugAnim", _ => ApplyDebug());
        Observe("SystemDebugDelay", _ => ApplyDebug());
        Observe("SystemDebugSkipCopy", _ => ApplyDebug());
        Observe("SystemDebugAllowRestrictedFeature", _ => ApplyDebug());
        Observe("ToolDownloadThread", _ => ApplyDownloadSettings());
        Observe("ToolDownloadSpeed", _ => ApplyDownloadSettings());
        Context.Info("启动器设置业务适配已启动");
    }

    [LifecycleStop]
    private static void _Stop()
    {
        foreach (var (item, observer) in _Observers)
            item.Unobserve(observer);
        _Observers.Clear();
    }

    public static void ApplyAll()
    {
        ApplySystemSettings();
        ApplyNetworkSettings();
        ApplyDebugSettings();
        ApplyDownloadSettings();
    }

    public static void ApplySystemSettings()
    {
        AnimationService.SetFps(Math.Clamp(Config.System.AnimationFpsLimit, 1, 240));
        LogWrapper.Debug("Settings",
            $"系统设置已应用: DisableHardwareAcceleration={Config.System.DisableHardwareAcceleration}, Telemetry={Config.System.Telemetry}, MaxLog={Config.System.MaxGameLog}, Fps={Config.System.AnimationFpsLimit}");
        TelemetryService.ApplyTelemetrySetting();
    }

    public static void ApplyNetworkSettings()
    {
        ApplyProxy();
        NetworkService.ReloadDefaultClientFactory();
    }

    public static void ApplyDebugSettings()
    {
        ApplyDebug();
    }

    public static void ApplyVisibilitySettings()
    {
        LogWrapper.Debug("Settings", "功能隐藏设置已更新");
    }

    public static void ApplyProxy()
    {
        try
        {
            var proxy = HttpProxyManager.Instance;
            var configuredMode = (HttpProxyManager.ProxyMode)Config.Network.HttpProxy.Type;
            if (Enum.IsDefined(configuredMode))
                proxy.Mode = configuredMode;

            var address = Config.Network.HttpProxy.CustomAddress;
            if (!string.IsNullOrWhiteSpace(address))
            {
                proxy.CustomProxyAddress = Uri.TryCreate(address, UriKind.Absolute, out var uri)
                    ? uri
                    : new Uri("http://" + address);
            }
            else
            {
                proxy.CustomProxyAddress = null;
            }

            var username = Config.Network.HttpProxy.CustomUsername;
            proxy.Credentials = string.IsNullOrEmpty(username)
                ? null
                : new NetworkCredential(username, Config.Network.HttpProxy.CustomPassword);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Settings", "应用 HTTP 代理设置失败");
        }
    }

    public static void ApplyDebug()
    {
        AnimationService.Scale = DebugSettingsService.ResolveAnimationScale(Config.Debug.AnimationSpeed);
        LogWrapper.Debug("Settings",
            $"调试设置已应用: Enabled={Config.Debug.Enabled}, AnimationSpeed={Config.Debug.AnimationSpeed}, RandomDelay={Config.Debug.AddRandomDelay}, SkipCopy={Config.Debug.DontCopy}, AllowRestricted={Config.Debug.AllowRestrictedFeature}");
    }

    public static LauncherDownloadSettings ApplyDownloadSettings()
    {
        CurrentDownloadSettings = ResolveDownloadSettings();
        LogWrapper.Debug("Settings",
            $"下载设置已应用: Threads={CurrentDownloadSettings.ThreadLimit}, SpeedLimit={CurrentDownloadSettings.SpeedLimitBytesPerSecond}");
        return CurrentDownloadSettings;
    }

    public static LauncherDownloadSettings ResolveDownloadSettings()
    {
        var speedValue = Config.Download.SpeedLimit;
        var speedLimit = speedValue switch
        {
            <= 14 => (long)Math.Round((speedValue + 1) * 0.1d * 1024d * 1024d),
            <= 31 => (long)Math.Round((speedValue - 11) * 0.5d * 1024d * 1024d),
            <= 41 => (speedValue - 21) * 1024L * 1024L,
            _ => -1L
        };

        return new LauncherDownloadSettings(
            Math.Max(1, Config.Download.ThreadLimit + 1),
            speedLimit,
            Config.Download.FileSource,
            Config.Download.VersionListSource);
    }

    private static void Observe(string key, Action<ConfigEventArgs> handler)
    {
        if (!ConfigService.TryGetConfigItemNoType(key, out var item) || item is null)
            return;
        var observer = new ConfigObserver(ConfigEvent.Changed, e => handler(e));
        item.Observe(observer);
        _Observers.Add((item, observer));
    }
}

public sealed record LauncherDownloadSettings(
    int ThreadLimit,
    long SpeedLimitBytesPerSecond,
    int FileSource,
    int VersionListSource);
