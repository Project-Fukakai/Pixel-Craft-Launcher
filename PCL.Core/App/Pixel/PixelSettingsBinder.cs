using System.Collections.Generic;
using PCL.Core.App.Configuration;
using PCL.Core.Utils.OS;

namespace PCL.Core.App.Pixel;

public static class PixelSettingsBinder
{
    public static object? LoadValue(string key)
    {
        return TryGetItem(key, out var item) ? NormalizeForControl(item.GetValueNoType(), item.Type) : null;
    }

    public static bool SetValue(string key, object? value)
    {
        if (!TryGetItem(key, out var item) || value is null)
            return false;
        item.SetValueNoType(ConvertForConfig(value, item.Type));
        return true;
    }

    public static bool SetValue(PixelSettingDescriptor setting, object? value)
    {
        if (setting.ConfigKey is null || !IsControlAvailable(setting))
            return false;
        if (!Validate(setting, value))
            return false;
        return SetValue(setting.ConfigKey, value);
    }

    public static void ResetSection(PixelSettingSection section)
    {
        foreach (var descriptor in EnumerateConfigDescriptors(section))
        {
            if (descriptor.ConfigKey is not { } key || !TryGetItem(key, out var item))
                continue;
            item.Reset();
        }
    }

    public static ConfigObserver? ObserveChanged(string key, Action<object?> handler)
    {
        if (!TryGetItem(key, out var item))
            return null;
        var observer = new ConfigObserver(ConfigEvent.Changed, e => handler(e.Value is null ? null : NormalizeForControl(e.Value, item.Type)));
        item.Observe(observer);
        return observer;
    }

    public static bool IsHidden(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        var value = LoadValue(key);
        return value is bool b && b;
    }

    public static bool TryGetItem(string key, out ConfigItem item)
    {
        var result = ConfigService.TryGetConfigItemNoType(key, out var configItem);
        item = configItem!;
        return result;
    }

    public static bool IsControlAvailable(PixelSettingDescriptor setting)
    {
        return GetUnavailableReason(setting) is null;
    }

    public static bool IsControlEnabled(PixelSettingDescriptor setting)
    {
        return IsControlAvailable(setting) && IsInteractionEnabled(setting);
    }

    public static bool IsInteractionEnabled(PixelSettingDescriptor setting)
    {
        return setting.ConfigKey switch
        {
            "LaunchRamCustom" => LoadValue("LaunchRamType") is int mode && mode == 1,
            "SystemHttpProxy" or "SystemHttpProxyCustomUsername" or "SystemHttpProxyCustomPassword"
                => LoadValue("SystemHttpProxyType") is int proxyMode && proxyMode == 2,
            _ => true
        };
    }

    public static double GetRuntimeMaximum(PixelSettingDescriptor setting)
    {
        if (setting.ConfigKey != "LaunchRamCustom")
            return setting.Maximum;

        var totalGb = KernelInterop.GetPhysicalMemoryBytes().Total / 1024d / 1024d / 1024d;
        return Math.Clamp(GetRamScaleMaximum(totalGb), setting.Minimum, setting.Maximum);
    }

    public static double RamScaleToGb(double value)
    {
        return value switch
        {
            <= 12 => Math.Round(value * 0.1 + 0.3, 1),
            <= 25 => Math.Round(1.5 + (value - 12) * 0.5, 1),
            <= 33 => Math.Round(8 + (value - 25), 1),
            _ => Math.Round(16 + (value - 33) * 2, 1)
        };
    }

    public static string? GetUnavailableReason(PixelSettingDescriptor setting)
    {
        if (!setting.IsAvailable)
            return setting.DisabledReason ?? setting.UnavailableReason ?? "此设置暂不可用。";

        if (setting.PlatformAvailability == PixelSettingPlatformAvailability.WindowsOnly && !OperatingSystem.IsWindows())
            return setting.DisabledReason ?? "此功能仅 Windows 可用，当前平台不可设置。";

        if (setting.ConfigKey is { } key && !TryGetItem(key, out _))
            return $"配置项尚未接入：{key}";

        return null;
    }

    private static IEnumerable<PixelSettingDescriptor> EnumerateConfigDescriptors(PixelSettingSection section)
    {
        foreach (var group in section.Groups)
        foreach (var descriptor in group.Settings)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.ConfigKey))
                yield return descriptor;
        }
    }

    private static object NormalizeForControl(object value, Type targetType)
    {
        if (targetType.IsEnum)
            return Convert.ToInt32(value);
        return value;
    }

    private static object ConvertForConfig(object value, Type targetType)
    {
        if (targetType.IsEnum)
            return value is string s ? Enum.Parse(targetType, s) : Enum.ToObject(targetType, Convert.ToInt32(value));
        if (targetType == typeof(bool))
            return value is bool b ? b : Convert.ToBoolean(value);
        if (targetType == typeof(int))
            return value is double d ? Convert.ToInt32(Math.Round(d)) : Convert.ToInt32(value);
        if (targetType == typeof(double))
            return Convert.ToDouble(value);
        if (targetType == typeof(string))
            return value.ToString() ?? string.Empty;
        return value;
    }

    private static int GetRamScaleMaximum(double totalGb)
    {
        if (totalGb <= 1.5d)
            return (int)Math.Round(Math.Max(Math.Floor((totalGb - 0.3d) / 0.1d), 1d));
        if (totalGb <= 8d)
            return (int)Math.Round(Math.Floor((totalGb - 1.5d) / 0.5d) + 12d);
        if (totalGb <= 16d)
            return (int)Math.Round(Math.Floor(totalGb - 8d) + 25d);
        return (int)Math.Round(Math.Floor((totalGb - 16d) / 2d) + 33d);
    }

    private static bool Validate(PixelSettingDescriptor setting, object? value)
    {
        if (setting.Validation is null || value is null)
            return true;

        if (value is string text && setting.Validation.Blacklist is { Count: > 0 })
        {
            foreach (var item in setting.Validation.Blacklist)
            {
                if (!string.IsNullOrEmpty(item) && text.Contains(item, StringComparison.Ordinal))
                    return false;
            }
        }

        if (value is IConvertible && (setting.Validation.Minimum is not null || setting.Validation.Maximum is not null))
        {
            try
            {
                var number = Convert.ToDouble(value);
                if (setting.Validation.Minimum is { } min && number < min)
                    return false;
                if (setting.Validation.Maximum is { } max && number > max)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}
