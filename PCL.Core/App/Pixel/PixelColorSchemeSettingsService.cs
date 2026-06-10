using PCL.Core.App;
using PCL.Core.UI.Theme;
using Avalonia.Media;
using System.Collections.Generic;

namespace PCL.Core.App.Pixel;

public sealed class PixelColorSchemeSettingsService
{
    public PixelColorSchemePageMessages GetPageMessages() => PixelColorSchemePageMessages.Default;

    public IReadOnlyList<PixelColorSchemePreset> GetPresets() => PixelColorSchemePageMessages.Default.Presets;

    public uint GetEffectiveSeed()
    {
        return ColorSchemeService.GetEffectiveSeed();
    }

    public Color ToColor(uint seed)
    {
        return ColorSchemeService.ToColor(seed);
    }

    public uint FromColor(Color color)
    {
        return ColorSchemeService.FromColor(color);
    }

    public int NormalizeChannel(double value)
    {
        return (int)Math.Clamp(Math.Round(value), 0, 255);
    }

    public string FormatChannel(byte value) => value.ToString();

    public string FormatChannel(int value) => NormalizeChannel(value).ToString();

    public Color WithRedChannel(Color color, int value)
    {
        return Color.FromArgb(color.A, (byte)NormalizeChannel(value), color.G, color.B);
    }

    public Color WithGreenChannel(Color color, int value)
    {
        return Color.FromArgb(color.A, color.R, (byte)NormalizeChannel(value), color.B);
    }

    public Color WithBlueChannel(Color color, int value)
    {
        return Color.FromArgb(color.A, color.R, color.G, (byte)NormalizeChannel(value));
    }

    public string FormatSeed(uint seed)
    {
        return ColorSchemeService.FormatSeed(seed);
    }

    public bool TryParseSeed(string? value, out uint seed)
    {
        return ColorSchemeService.TryParseSeed(value, out seed);
    }

    public PixelColorSchemePreviewColors GetPreviewColors(uint seed, bool isDarkMode)
    {
        var previewColors = PixelThemePaletteBuilder.BuildPreviewColors(seed, isDarkMode);
        return new PixelColorSchemePreviewColors(
            previewColors.Primary,
            previewColors.Secondary,
            previewColors.Tertiary);
    }

    public bool IsAutoBackgroundEnabled()
    {
        return PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground") is bool enabled && enabled;
    }

    public PixelColorSchemeChange ApplySeed(uint seed, ColorSchemeMode mode)
    {
        var formatted = ColorSchemeService.FormatSeed(seed);
        var oldMode = PixelSettingsBinder.LoadValue("UiColorSchemeMode") is int modeValue ? modeValue : -1;
        var oldSeed = PixelSettingsBinder.LoadValue("UiColorSchemeSeed")?.ToString();
        var changed = oldMode != (int)mode || !string.Equals(oldSeed, formatted, StringComparison.OrdinalIgnoreCase);

        if (oldMode != (int)mode)
            PixelSettingsBinder.SetValue("UiColorSchemeMode", (int)mode);
        if (!string.Equals(oldSeed, formatted, StringComparison.OrdinalIgnoreCase))
            PixelSettingsBinder.SetValue("UiColorSchemeSeed", formatted);

        return new PixelColorSchemeChange(changed);
    }

    public PixelImageSeedResult ApplyImageSeed(string path)
    {
        if (!ImageColorExtractor.TryExtractSeed(path, out var imageSeed))
            return PixelImageSeedResult.Failed;

        PixelSettingsBinder.SetValue("UiColorSchemeImage", path);
        var change = ApplySeed(imageSeed, ColorSchemeMode.Image);
        return new PixelImageSeedResult(true, change.RefreshTheme, imageSeed);
    }

    public PixelColorSchemeChange SetAutoBackground(bool enabled)
    {
        var oldEnabled = IsAutoBackgroundEnabled();
        var oldMode = PixelSettingsBinder.LoadValue("UiColorSchemeMode") is int modeValue ? modeValue : -1;
        var nextMode = enabled ? ColorSchemeMode.AutoBackground : ColorSchemeMode.Manual;
        PixelSettingsBinder.SetValue("UiColorSchemeAutoBackground", enabled);
        PixelSettingsBinder.SetValue("UiColorSchemeMode", (int)nextMode);
        return new PixelColorSchemeChange(oldEnabled != enabled || oldMode != (int)nextMode);
    }
}

public sealed record PixelColorSchemeChange(bool RefreshTheme);

public sealed record PixelColorSchemePreviewColors(Color Primary, Color Secondary, Color Tertiary);

public sealed record PixelImageSeedResult(bool Success, bool RefreshTheme, uint? Seed)
{
    public static PixelImageSeedResult Failed { get; } = new(false, false, null);
}

public sealed record PixelColorSchemePreset(string Name, uint Seed);

public sealed record PixelColorSchemePageMessages(
    string ManualPickerTooltip,
    string ImagePickerTooltip,
    string ImagePickerTitle,
    string ImageFileTypeName,
    IReadOnlyList<string> ImageFilePatterns,
    string ImageExtractionFailedHint,
    string ImageExtractionSuccessHint,
    string AutoBackgroundText,
    string HexValidationMessage,
    string ManualDialogTitle,
    string InvalidColorHint,
    IReadOnlyList<PixelColorSchemePreset> Presets)
{
    public static PixelColorSchemePageMessages Default { get; } = new(
        "手动取色",
        "图片取色",
        "选择取色图片",
        "图片",
        ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"],
        "无法从该图片提取配色。",
        "已从图片提取配色。",
        "显示背景图片时自动使用图片颜色",
        "请输入 #RRGGBB 或 #AARRGGBB",
        "手动取色",
        "请输入有效颜色。",
        [
            new PixelColorSchemePreset("天空蓝", 0xFF1370F3),
            new PixelColorSchemePreset("猫猫蓝", 0xFF2078DA),
            new PixelColorSchemePreset("死亡蓝", 0xFF4263DE),
            new PixelColorSchemePreset("HMCL 蓝", 0xFF5D6AC4),
            new PixelColorSchemePreset("青绿", 0xFF008C7A),
            new PixelColorSchemePreset("草绿", 0xFF3F7E2F),
            new PixelColorSchemePreset("琥珀", 0xFFB26A00),
            new PixelColorSchemePreset("玫红", 0xFFC22F6A)
        ]);
}
