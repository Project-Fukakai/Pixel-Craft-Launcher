using PCL.Core.App.Configuration;

namespace PCL.Core.App.Pixel;

public sealed class PixelSettingValueService
{
    public const string OpenBackgroundFolderActionId = "OpenBackgroundFolder";
    public const string LaunchAdvanceJvmConfigKey = "LaunchAdvanceJvm";
    public const string BackgroundFolderConfigKey = "UiBackgroundFolder";

    public PixelSettingControlMessages GetControlMessages() => PixelSettingControlMessages.Default;

    public object? LoadValue(string key) => PixelSettingsBinder.LoadValue(key);

    public string GetControlTextValue(PixelSettingDescriptor setting) =>
        setting.ConfigKey is null ? string.Empty : FormatControlValue(LoadValue(setting.ConfigKey));

    public PixelSettingComboStateSnapshot GetComboState(PixelSettingDescriptor setting)
    {
        var options = setting.Options ?? [];
        var current = setting.ConfigKey is null ? null : LoadValue(setting.ConfigKey);
        var selectedIndex = 0;
        for (var i = 0; i < options.Count; i++)
        {
            if (!Equals(options[i].Value, current))
                continue;

            selectedIndex = i;
            break;
        }
        return new PixelSettingComboStateSnapshot(selectedIndex, FormatControlValue(current));
    }

    public PixelSettingSliderStateSnapshot GetSliderState(PixelSettingDescriptor setting)
    {
        var maximum = GetRuntimeMaximum(setting);
        var raw = setting.ConfigKey is null ? null : LoadValue(setting.ConfigKey);
        var value = raw is null ? setting.Minimum : Convert.ToDouble(raw);
        value = ClampSliderValue(setting, maximum, value);
        return new PixelSettingSliderStateSnapshot(maximum, value, FormatSettingValue(setting, value));
    }

    public double NormalizeSliderInput(PixelSettingDescriptor setting, double value)
    {
        var maximum = GetRuntimeMaximum(setting);
        return ClampSliderValue(setting, maximum, Math.Round(value));
    }

    public string FormatSettingValue(PixelSettingDescriptor setting, double value)
    {
        var text = setting.ValueFormatter == "RamScale"
            ? FormatRamScale(value)
            : Math.Abs(value - Math.Round(value)) < 0.0001
                ? Math.Round(value).ToString()
                : value.ToString("0.##");
        return string.IsNullOrWhiteSpace(setting.UnitText) ? text : $"{text} {setting.UnitText}";
    }

    public string FormatControlValue(object? value) => value?.ToString() ?? string.Empty;

    public bool SetValue(PixelSettingDescriptor setting, object? value)
    {
        DebugSettingsService.DelayIfNeeded("保存设置 " + (setting.ConfigKey ?? setting.Title));
        return PixelSettingsBinder.SetValue(setting, value);
    }

    public PixelSettingResetResult Reset(string key)
    {
        if (!PixelSettingsBinder.TryGetItem(key, out var item))
            return PixelSettingResetResult.NotFound;

        item.Reset();
        return new PixelSettingResetResult(true, PixelSettingsBinder.LoadValue(key));
    }

    public bool IsControlAvailable(PixelSettingDescriptor setting) => PixelSettingsBinder.IsControlAvailable(setting);

    public bool IsControlEnabled(PixelSettingDescriptor setting) => PixelSettingsBinder.IsControlEnabled(setting);

    public bool IsInteractionEnabled(PixelSettingDescriptor setting) => PixelSettingsBinder.IsInteractionEnabled(setting);

    public string? GetUnavailableReason(PixelSettingDescriptor setting) => PixelSettingsBinder.GetUnavailableReason(setting);

    public double GetRuntimeMaximum(PixelSettingDescriptor setting) => PixelSettingsBinder.GetRuntimeMaximum(setting);

    public double RamScaleToGb(double value) => PixelSettingsBinder.RamScaleToGb(value);

    public bool IsLaunchAdvanceJvmSetting(PixelSettingDescriptor setting) =>
        setting.ConfigKey == LaunchAdvanceJvmConfigKey;

    public bool IsBackgroundFolderSetting(PixelSettingDescriptor setting) =>
        setting.ConfigKey == BackgroundFolderConfigKey;

    public bool IsOpenBackgroundFolderAction(PixelSettingDescriptor setting) =>
        setting.ActionId == OpenBackgroundFolderActionId;

    private double ClampSliderValue(PixelSettingDescriptor setting, double maximum, double value) =>
        Math.Clamp(value, setting.Minimum, maximum);

    private string FormatRamScale(double value)
    {
        var ram = RamScaleToGb(value);
        return $"{ram:0.#} GB";
    }
}

public sealed record PixelSettingResetResult(bool Success, object? Value)
{
    public static PixelSettingResetResult NotFound { get; } = new(false, null);
}

public sealed record PixelSettingControlMessages(
    string MissingConfigDescription,
    string EditableComboHint,
    string RejectedTextInputHint,
    string ResetJvmArgumentsTooltip,
    string ResetJvmArgumentsSuccessHint,
    string SelectBackgroundFolderTooltip,
    string OpenBackgroundFolderTooltip,
    string SelectBackgroundFolderDialogTitle,
    string ReservedActionHint)
{
    public static PixelSettingControlMessages Default { get; } = new(
        "该设置项没有绑定配置键。",
        "默认",
        "输入包含不允许的字符，已忽略。",
        "还原默认 JVM 参数",
        "JVM 参数头部已还原。",
        "选择背景目录",
        "打开背景目录",
        "选择背景目录",
        "操作入口已预留。");
}

public sealed record PixelSettingComboStateSnapshot(int SelectedIndex, string Text);

public sealed record PixelSettingSliderStateSnapshot(double Maximum, double Value, string ValueText);
