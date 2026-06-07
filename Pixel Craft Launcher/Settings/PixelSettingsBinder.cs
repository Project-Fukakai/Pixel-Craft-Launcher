using System;
using System.Collections.Generic;
using PCL.Core.App.Pixel;
using PCL.Core.App.Configuration;

namespace Pixel_Craft_Launcher.Settings;

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
