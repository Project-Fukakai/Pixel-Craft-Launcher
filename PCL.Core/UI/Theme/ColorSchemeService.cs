using System;
using System.Globalization;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.UI.Theme;

public static class ColorSchemeService
{
    public const uint DefaultSeed = 0xFF2078DA;

    private static uint? _backgroundSeed;

    public static uint? BackgroundSeed => _backgroundSeed;

    public static void SetBackgroundSeed(uint? seed)
    {
        if (_backgroundSeed == seed)
            return;

        _backgroundSeed = seed;
        if (Config.Preference.Theme.ColorSchemeAutoBackground)
            ThemeService.RefreshColorScheme();
    }

    public static uint GetEffectiveSeed()
    {
        if (Config.Preference.Theme.ColorSchemeAutoBackground && _backgroundSeed is { } backgroundSeed)
            return backgroundSeed;

        return ParseSeedOrDefault(Config.Preference.Theme.ColorSchemeSeed, GetLegacySeed(Config.Preference.Theme.LightColor));
    }

    public static uint ParseSeedOrDefault(string? value, uint fallback = DefaultSeed)
    {
        return TryParseSeed(value, out var seed) ? seed : fallback;
    }

    public static bool TryParseSeed(string? value, out uint seed)
    {
        seed = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if (text.Length == 6)
            text = "FF" + text;
        if (text.Length != 8)
            return false;

        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed);
    }

    public static string FormatSeed(uint seed) => $"#{seed:X8}";

    public static Color ToColor(uint seed) => Color.FromArgb(
        (byte)((seed >> 24) & 0xFF),
        (byte)((seed >> 16) & 0xFF),
        (byte)((seed >> 8) & 0xFF),
        (byte)(seed & 0xFF));

    public static uint FromColor(Color color) => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    public static uint GetLegacySeed(ColorTheme theme) => PixelThemePaletteBuilder.GetSeed(theme);

    public static void EnsureSeedInitialized()
    {
        if (!string.IsNullOrWhiteSpace(Config.Preference.Theme.ColorSchemeSeed))
            return;

        var seed = GetLegacySeed(Config.Preference.Theme.LightColor);
        Config.Preference.Theme.ColorSchemeSeed = FormatSeed(seed);
        LogWrapper.Debug("Theme", $"已从旧亮色主题迁移配色种子: {Config.Preference.Theme.ColorSchemeSeed}");
    }
}
