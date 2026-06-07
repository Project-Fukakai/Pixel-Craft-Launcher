using System.Collections.Generic;

namespace PCL.Core.App.Pixel;

public enum PixelSettingSectionKind
{
    Launch,
    Java,
    GameManage,
    GameLink,
    Ui,
    LauncherMisc,
    About,
    Update,
    Feedback,
    Log
}

public enum PixelSettingControlKind
{
    Toggle,
    Combo,
    Slider,
    Text,
    Font,
    Info,
    Action,
    MemoryPreview,
    ColorScheme
}

public enum PixelSettingPlatformAvailability
{
    All,
    WindowsOnly
}

public sealed record PixelSettingValidation(double? Minimum = null, double? Maximum = null, IReadOnlyList<string>? Blacklist = null);

public sealed record PixelSettingOption(string Text, object Value, string? ToolTip = null);

public sealed record PixelSettingDescriptor(
    string Title,
    PixelSettingControlKind Control,
    string? ConfigKey = null,
    string? Description = null,
    IReadOnlyList<PixelSettingOption>? Options = null,
    double Minimum = 0,
    double Maximum = 100,
    double TickFrequency = 1,
    bool IsAvailable = true,
    string? UnavailableReason = null,
    PixelSettingPlatformAvailability PlatformAvailability = PixelSettingPlatformAvailability.All,
    PixelSettingValidation? Validation = null,
    bool IsEditableCombo = false,
    string? UnitText = null,
    string? DisabledReason = null,
    string? ValueFormatter = null);

public sealed record PixelSettingGroup(string Title, IReadOnlyList<PixelSettingDescriptor> Settings);

public sealed record PixelSettingSection(
    PixelSettingSectionKind Kind,
    string Title,
    string Info,
    string Icon,
    string? HiddenConfigKey,
    IReadOnlyList<PixelSettingGroup> Groups);
