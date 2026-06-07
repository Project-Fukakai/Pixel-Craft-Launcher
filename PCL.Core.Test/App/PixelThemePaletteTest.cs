using System;
using System.Linq;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelThemePaletteTest
{
    [TestMethod]
    public void AllThemesGenerateRequiredTokens()
    {
        foreach (var theme in Enum.GetValues<ColorTheme>())
        {
            foreach (var isDarkMode in new[] { false, true })
            {
                var palette = PixelThemePaletteBuilder.Build(theme, isDarkMode);

                AssertHasToken(palette, PixelThemeResources.Primary);
                AssertHasToken(palette, PixelThemeResources.Text);
                AssertHasToken(palette, PixelThemeResources.TextSecondary);
                AssertHasToken(palette, PixelThemeResources.Background);
                AssertHasToken(palette, PixelThemeResources.ShellBackground);
                AssertHasToken(palette, PixelThemeResources.InputBackground);
                AssertHasToken(palette, PixelThemeResources.ButtonHoverBackground);
                AssertHasToken(palette, PixelThemeResources.Error);
                AssertHasToken(palette, PixelThemeResources.Warning);
                AssertHasToken(palette, PixelThemeResources.Success);
                AssertHasToken(palette, PixelThemeResources.MemoryUsed);
            }
        }
    }

    [TestMethod]
    public void KeyForegroundAndBackgroundTokensKeepReadableContrast()
    {
        foreach (var theme in Enum.GetValues<ColorTheme>())
        {
            foreach (var isDarkMode in new[] { false, true })
            {
                var palette = PixelThemePaletteBuilder.Build(theme, isDarkMode);

                AssertContrast(palette[PixelThemeResources.Text], palette[PixelThemeResources.Background], 3.0);
                AssertContrast(palette[PixelThemeResources.Text], palette[PixelThemeResources.CardBackground], 2.2);
                AssertContrast(palette[PixelThemeResources.OnPrimary], palette[PixelThemeResources.Primary], 3.0);
            }
        }
    }

    [TestMethod]
    public void LegacyCompatibilityColorsAreMapped()
    {
        var palette = PixelThemePaletteBuilder.Build(ColorTheme.SkyBlue, false);
        var requiredLegacyKeys = new[]
        {
            "1", "2", "3", "6", "Gray1", "Gray2", "Gray6", "HalfWhite",
            "TransparentBackground", "Background", "RedBack", "RedLight", "Memory"
        };

        var missing = requiredLegacyKeys.Where(key => !palette.LegacyColors.ContainsKey(key)).ToArray();

        Assert.AreEqual(0, missing.Length, "Missing legacy color keys: " + string.Join(", ", missing));
    }

    private static void AssertHasToken(PixelThemePalette palette, string token)
    {
        Assert.IsTrue(palette.Colors.ContainsKey(token), $"Missing token: {token}");
        Assert.AreNotEqual(default, palette[token], $"Token has default color: {token}");
    }

    private static void AssertContrast(Color foreground, Color background, double minimum)
    {
        var contrast = ContrastRatio(foreground, background);
        Assert.IsTrue(contrast >= minimum, $"Contrast {contrast:0.00} is lower than {minimum:0.00} for {foreground} on {background}");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var light = Math.Max(foregroundLuminance, backgroundLuminance);
        var dark = Math.Min(foregroundLuminance, backgroundLuminance);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Channel(byte value)
    {
        var normalized = value / 255d;
        return normalized <= 0.03928
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}
