using System;
using System.Collections.Generic;
using Avalonia.Media;
using MaterialColorUtilities.Blend;
using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Schemes;
using PCL.Core.App;

namespace PCL.Core.UI.Theme;

public static class PixelThemeResources
{
    public const string ColorPrefix = "PixelColor";
    public const string BrushPrefix = "PixelBrush";

    public const string Primary = "Primary";
    public const string PrimaryHover = "PrimaryHover";
    public const string PrimaryContainer = "PrimaryContainer";
    public const string OnPrimaryContainer = "OnPrimaryContainer";
    public const string OnPrimary = "OnPrimary";
    public const string Text = "Text";
    public const string TextSecondary = "TextSecondary";
    public const string TextDisabled = "TextDisabled";
    public const string Background = "Background";
    public const string TransparentBackground = "TransparentBackground";
    public const string Surface = "Surface";
    public const string SurfaceVariant = "SurfaceVariant";
    public const string CardBackground = "CardBackground";
    public const string Container = "Container";
    public const string ContainerHigh = "ContainerHigh";
    public const string Border = "Border";
    public const string BorderStrong = "BorderStrong";
    public const string SidebarBackground = "SidebarBackground";
    public const string SidebarBorder = "SidebarBorder";
    public const string ShellBackground = "ShellBackground";
    public const string InputBackground = "InputBackground";
    public const string InputHoverBackground = "InputHoverBackground";
    public const string InputBorder = "InputBorder";
    public const string InputDisabledBackground = "InputDisabledBackground";
    public const string SliderTrack = "SliderTrack";
    public const string ListItemHover = "ListItemHover";
    public const string ButtonHoverBackground = "ButtonHoverBackground";
    public const string ButtonDangerHoverBackground = "ButtonDangerHoverBackground";
    public const string Overlay = "Overlay";
    public const string HalfWhite = "HalfWhite";
    public const string White = "White";
    public const string Transparent = "Transparent";
    public const string Error = "Error";
    public const string ErrorContainer = "ErrorContainer";
    public const string OnErrorContainer = "OnErrorContainer";
    public const string Warning = "Warning";
    public const string WarningContainer = "WarningContainer";
    public const string Success = "Success";
    public const string SuccessContainer = "SuccessContainer";
    public const string Info = "Info";
    public const string InfoContainer = "InfoContainer";
    public const string Shadow = "Shadow";
    public const string MemoryUsed = "MemoryUsed";
    public const string MemoryGame = "MemoryGame";
    public const string MemoryFree = "MemoryFree";

    public static string ColorKey(string token) => ColorPrefix + token;

    public static string BrushKey(string token) => BrushPrefix + token;
}

public sealed class PixelThemePalette
{
    public PixelThemePalette(IReadOnlyDictionary<string, Color> colors)
    {
        Colors = colors;
        LegacyColors = BuildLegacyColors(colors);
    }

    public IReadOnlyDictionary<string, Color> Colors { get; }

    public IReadOnlyDictionary<string, Color> LegacyColors { get; }

    public Color this[string token] => Colors[token];

    public void Apply()
    {
        var resources = PCL.Core.App.IoC.Lifecycle.CurrentApplication.Resources;
        foreach (var (token, color) in Colors)
        {
            resources[PixelThemeResources.ColorKey(token)] = color;
            ApplyBrushResource(resources, PixelThemeResources.BrushKey(token), color);
        }

        ApplyLegacyCompatibility(resources);
    }

    private void ApplyLegacyCompatibility(Avalonia.Controls.IResourceDictionary resources)
    {
        foreach (var (suffix, color) in LegacyColors)
        {
            resources[$"ColorObject{suffix}"] = color;
            ApplyBrushResource(resources, $"ColorBrush{suffix}", color);
        }
    }

    private static void ApplyBrushResource(Avalonia.Controls.IResourceDictionary resources, string key, Color color)
    {
        if (resources.TryGetResource(key, null, out var value) && value is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static IReadOnlyDictionary<string, Color> BuildLegacyColors(IReadOnlyDictionary<string, Color> colors)
    {
        var legacyColors = new Dictionary<string, Color>(StringComparer.Ordinal);
        Add("1", PixelThemeResources.Text);
        Add("2", PixelThemeResources.Primary);
        Add("3", PixelThemeResources.Info);
        Add("4", PixelThemeResources.PrimaryHover);
        Add("5", PixelThemeResources.PrimaryContainer);
        Add("6", PixelThemeResources.Border);
        Add("7", PixelThemeResources.ListItemHover);
        Add("8", PixelThemeResources.Container);
        Add("Bg0", PixelThemeResources.PrimaryContainer);
        Add("Bg1", PixelThemeResources.InfoContainer);
        Add("Gray1", PixelThemeResources.Text);
        Add("Gray2", PixelThemeResources.TextSecondary);
        Add("Gray3", PixelThemeResources.TextDisabled);
        Add("Gray4", PixelThemeResources.TextDisabled);
        Add("Gray5", PixelThemeResources.BorderStrong);
        Add("Gray6", PixelThemeResources.Border);
        Add("Gray7", PixelThemeResources.ContainerHigh);
        Add("Gray8", PixelThemeResources.Container);
        Add("HalfWhite", PixelThemeResources.HalfWhite);
        Add("SemiWhite", PixelThemeResources.CardBackground);
        Add("White", PixelThemeResources.White);
        Add("Transparent", PixelThemeResources.Transparent);
        Add("TransparentBackground", PixelThemeResources.TransparentBackground);
        Add("Background", PixelThemeResources.Background);
        Add("ToolTip", PixelThemeResources.CardBackground);
        Add("RedBack", PixelThemeResources.ErrorContainer);
        Add("RedLight", PixelThemeResources.Error);
        Add("RedDark", PixelThemeResources.Error);
        Add("Memory", PixelThemeResources.Text);
        Add("MsgBoxShadow", PixelThemeResources.Shadow);
        return legacyColors;

        void Add(string suffix, string token)
        {
            if (colors.TryGetValue(token, out var color))
            {
                legacyColors[suffix] = color;
            }
        }
    }
}

public static class PixelThemePaletteBuilder
{
    public static PixelThemePalette Build(ColorTheme theme, bool isDarkMode)
        => BuildFromSeed(GetSeed(theme), isDarkMode);

    public static PixelThemePalette BuildFromSeed(uint seed, bool isDarkMode)
    {
        var core = CorePalette.Of(seed, Style.TonalSpot);
        var scheme = isDarkMode
            ? new DarkSchemeMapper().Map(core)
            : new LightSchemeMapper().Map(core);

        var success = TonalPalette.FromInt(Blender.Harmonize(0xFF218E21, seed));
        var warning = TonalPalette.FromInt(Blender.Harmonize(0xFFB26A00, seed));

        var colors = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            [PixelThemeResources.Primary] = ToColor(scheme.Primary),
            [PixelThemeResources.PrimaryHover] = ToColor(isDarkMode ? core.Primary[90] : core.Primary[30]),
            [PixelThemeResources.PrimaryContainer] = ToColor(scheme.PrimaryContainer),
            [PixelThemeResources.OnPrimaryContainer] = ToColor(scheme.OnPrimaryContainer),
            [PixelThemeResources.OnPrimary] = ToColor(scheme.OnPrimary),
            [PixelThemeResources.Text] = ToColor(scheme.OnSurface),
            [PixelThemeResources.TextSecondary] = ToColor(scheme.OnSurfaceVariant),
            [PixelThemeResources.TextDisabled] = ToColor(isDarkMode ? core.NeutralVariant[60] : core.NeutralVariant[50]),
            [PixelThemeResources.Background] = ToColor(scheme.Background),
            [PixelThemeResources.TransparentBackground] = WithAlpha(ToColor(scheme.Surface), isDarkMode ? (byte)220 : (byte)210),
            [PixelThemeResources.Surface] = ToColor(scheme.Surface),
            [PixelThemeResources.SurfaceVariant] = ToColor(scheme.SurfaceVariant),
            [PixelThemeResources.CardBackground] = WithAlpha(ToColor(isDarkMode ? scheme.SurfaceContainerHigh : scheme.Surface), isDarkMode ? (byte)230 : (byte)235),
            [PixelThemeResources.Container] = ToColor(scheme.SurfaceContainer),
            [PixelThemeResources.ContainerHigh] = ToColor(scheme.SurfaceContainerHigh),
            [PixelThemeResources.Border] = ToColor(scheme.OutlineVariant),
            [PixelThemeResources.BorderStrong] = ToColor(scheme.Outline),
            [PixelThemeResources.SidebarBackground] = ToColor(isDarkMode ? scheme.SurfaceContainerLowest : scheme.Surface),
            [PixelThemeResources.SidebarBorder] = ToColor(scheme.OutlineVariant),
            [PixelThemeResources.ShellBackground] = ToColor(isDarkMode ? scheme.SurfaceDim : scheme.SurfaceContainerLowest),
            [PixelThemeResources.InputBackground] = ToColor(isDarkMode ? scheme.SurfaceContainerLow : scheme.Surface),
            [PixelThemeResources.InputHoverBackground] = ToColor(isDarkMode ? scheme.SurfaceContainerHigh : scheme.PrimaryContainer),
            [PixelThemeResources.InputBorder] = ToColor(scheme.OutlineVariant),
            [PixelThemeResources.InputDisabledBackground] = ToColor(isDarkMode ? scheme.SurfaceContainerLowest : scheme.SurfaceContainer),
            [PixelThemeResources.SliderTrack] = ToColor(isDarkMode ? scheme.SurfaceContainerHighest : scheme.SurfaceContainerHigh),
            [PixelThemeResources.ListItemHover] = ToColor(isDarkMode ? scheme.SurfaceContainerHigh : scheme.PrimaryContainer),
            [PixelThemeResources.ButtonHoverBackground] = WithAlpha(ToColor(scheme.PrimaryContainer), isDarkMode ? (byte)76 : (byte)255),
            [PixelThemeResources.ButtonDangerHoverBackground] = WithAlpha(ToColor(scheme.ErrorContainer), isDarkMode ? (byte)96 : (byte)255),
            [PixelThemeResources.Overlay] = Color.FromArgb(isDarkMode ? (byte)160 : (byte)102, 0, 0, 0),
            [PixelThemeResources.HalfWhite] = Color.FromArgb(isDarkMode ? (byte)36 : (byte)85, 255, 255, 255),
            [PixelThemeResources.White] = Colors.White,
            [PixelThemeResources.Transparent] = Colors.Transparent,
            [PixelThemeResources.Error] = ToColor(scheme.Error),
            [PixelThemeResources.ErrorContainer] = ToColor(scheme.ErrorContainer),
            [PixelThemeResources.OnErrorContainer] = ToColor(scheme.OnErrorContainer),
            [PixelThemeResources.Warning] = ToColor(warning[isDarkMode ? 80u : 40u]),
            [PixelThemeResources.WarningContainer] = ToColor(warning[isDarkMode ? 30u : 90u]),
            [PixelThemeResources.Success] = ToColor(success[isDarkMode ? 80u : 40u]),
            [PixelThemeResources.SuccessContainer] = ToColor(success[isDarkMode ? 30u : 90u]),
            [PixelThemeResources.Info] = ToColor(scheme.Primary),
            [PixelThemeResources.InfoContainer] = ToColor(scheme.PrimaryContainer),
            [PixelThemeResources.Shadow] = Color.FromArgb(isDarkMode ? (byte)96 : (byte)32, 31, 45, 64),
            [PixelThemeResources.MemoryUsed] = ToColor(scheme.Primary),
            [PixelThemeResources.MemoryGame] = ToColor(scheme.Tertiary),
            [PixelThemeResources.MemoryFree] = ToColor(scheme.SecondaryContainer)
        };

        return new PixelThemePalette(colors);
    }

    public static (Color Primary, Color Secondary, Color Tertiary) BuildPreviewColors(uint seed, bool isDarkMode)
    {
        var core = CorePalette.Of(seed, Style.TonalSpot);
        var scheme = isDarkMode
            ? new DarkSchemeMapper().Map(core)
            : new LightSchemeMapper().Map(core);
        return (ToColor(scheme.Primary), ToColor(scheme.Secondary), ToColor(scheme.Tertiary));
    }

    public static uint GetSeed(ColorTheme theme) => theme switch
    {
        ColorTheme.SkyBlue => 0xFF1370F3,
        ColorTheme.CatBlue => 0xFF2078DA,
        ColorTheme.DeathBlue => 0xFF4263DE,
        ColorTheme.HmclBlue => 0xFF5D6AC4,
        _ => 0xFF1370F3
    };

    public static Color ToColor(uint argb)
    {
        return Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
