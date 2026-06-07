using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App.Configuration;

public static class DebugSettingsService
{
    private static readonly Random Random = new();

    public static bool IsDebugFeatureEnabled => Config.Debug.Enabled;

    public static bool IsRandomDelayEnabled => Config.Debug.Enabled && Config.Debug.AddRandomDelay;

    public static bool IsSkipCopyEnabled => Config.Debug.Enabled && Config.Debug.DontCopy;

    public static bool IsRestrictedFeatureAllowed => Config.Debug.AllowRestrictedFeature;

    public static double ResolveAnimationScale(int value)
    {
        return value switch
        {
            <= 0 => 1000d,
            9 => 1d,
            < 9 => 1d + (9 - value) * 0.35d,
            _ => Math.Max(0.15d, 1d - (value - 9) * 0.07d)
        };
    }

    public static async Task DelayIfNeededAsync(string operation, CancellationToken cancellationToken = default)
    {
        if (!IsRandomDelayEnabled)
            return;

        var delay = Random.Next(120, 900);
        LogWrapper.Debug("Debug", $"随机延迟 {delay} ms: {operation}");
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    public static void DelayIfNeeded(string operation)
    {
        if (!IsRandomDelayEnabled)
            return;

        DelayIfNeededAsync(operation).GetAwaiter().GetResult();
    }

    public static bool ShouldSkipCopy(string from, string to)
    {
        if (!IsSkipCopyEnabled)
            return false;

        LogWrapper.Warn("Debug", $"已跳过复制: {from} -> {to}");
        return true;
    }
}
