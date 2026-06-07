using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI.Theme;

namespace PCL.Core.Test.App;

[TestClass]
public class ColorSchemeServiceTest
{
    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        Config.Preference.Theme.ColorSchemeAutoBackground = false;
        Config.Preference.Theme.ColorSchemeSeed = string.Empty;
        ColorSchemeService.SetBackgroundSeed(null);
    }

    [TestMethod]
    public void ParseSeedSupportsRgbAndArgb()
    {
        Assert.IsTrue(ColorSchemeService.TryParseSeed("#123456", out var rgb));
        Assert.AreEqual(0xFF123456u, rgb);

        Assert.IsTrue(ColorSchemeService.TryParseSeed("#80123456", out var argb));
        Assert.AreEqual(0x80123456u, argb);

        Assert.IsFalse(ColorSchemeService.TryParseSeed("#XYZ", out _));
    }

    [TestMethod]
    public void EffectiveSeedPrefersBackgroundWhenEnabled()
    {
        Config.Preference.Theme.ColorSchemeSeed = "#FF123456";
        ColorSchemeService.SetBackgroundSeed(0xFFABCDEF);

        Config.Preference.Theme.ColorSchemeAutoBackground = false;
        Assert.AreEqual(0xFF123456u, ColorSchemeService.GetEffectiveSeed());

        Config.Preference.Theme.ColorSchemeAutoBackground = true;
        Assert.AreEqual(0xFFABCDEFu, ColorSchemeService.GetEffectiveSeed());
    }

    [TestMethod]
    public void ImageExtractorHandlesInvalidPath()
    {
        Assert.IsFalse(ImageColorExtractor.TryExtractSeed(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), out _));
    }

    [TestMethod]
    public void ImageExtractorHandlesNonImageFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "PCLTest", "ColorScheme", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "not an image");

            Assert.IsFalse(ImageColorExtractor.TryExtractSeed(path, out _));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
