using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelColorSchemeSettingsServiceTest
{
    private object? _oldMode;
    private object? _oldSeed;
    private object? _oldAutoBackground;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _oldMode = PixelSettingsBinder.LoadValue("UiColorSchemeMode");
        _oldSeed = PixelSettingsBinder.LoadValue("UiColorSchemeSeed");
        _oldAutoBackground = PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_oldMode is not null)
            PixelSettingsBinder.SetValue("UiColorSchemeMode", _oldMode);
        if (_oldSeed is not null)
            PixelSettingsBinder.SetValue("UiColorSchemeSeed", _oldSeed);
        if (_oldAutoBackground is not null)
            PixelSettingsBinder.SetValue("UiColorSchemeAutoBackground", _oldAutoBackground);
    }

    [TestMethod]
    public void ApplySeedPersistsModeAndSeed()
    {
        var service = new PixelColorSchemeSettingsService();
        service.ApplySeed(0xFF2078DA, ColorSchemeMode.Manual);

        var result = service.ApplySeed(0xFF1370F3, ColorSchemeMode.Preset);

        Assert.IsTrue(result.RefreshTheme);
        Assert.AreEqual((int)ColorSchemeMode.Preset, PixelSettingsBinder.LoadValue("UiColorSchemeMode"));
        Assert.AreEqual(ColorSchemeService.FormatSeed(0xFF1370F3), PixelSettingsBinder.LoadValue("UiColorSchemeSeed"));
    }

    [TestMethod]
    public void ApplySeedDoesNotRefreshWhenUnchanged()
    {
        var service = new PixelColorSchemeSettingsService();
        service.ApplySeed(0xFF1370F3, ColorSchemeMode.Preset);

        var result = service.ApplySeed(0xFF1370F3, ColorSchemeMode.Preset);

        Assert.IsFalse(result.RefreshTheme);
    }

    [TestMethod]
    public void SetAutoBackgroundPersistsMode()
    {
        var service = new PixelColorSchemeSettingsService();

        var result = service.SetAutoBackground(true);

        Assert.IsTrue(result.RefreshTheme);
        Assert.AreEqual(true, PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground"));
        Assert.AreEqual((int)ColorSchemeMode.AutoBackground, PixelSettingsBinder.LoadValue("UiColorSchemeMode"));
    }

    [TestMethod]
    public void FormatsAndParsesSeedText()
    {
        var service = new PixelColorSchemeSettingsService();

        Assert.AreEqual("#FF1370F3", service.FormatSeed(0xFF1370F3));
        Assert.IsTrue(service.TryParseSeed("#1370F3", out var parsed));
        Assert.AreEqual(0xFF1370F3, parsed);
    }

    [TestMethod]
    public void ConvertsSeedToColorAndBack()
    {
        var service = new PixelColorSchemeSettingsService();

        var color = service.ToColor(0xFF2078DA);
        var seed = service.FromColor(color);

        Assert.AreEqual(0xFF2078DA, seed);
    }

    [TestMethod]
    public void NormalizesAndAppliesManualColorChannels()
    {
        var service = new PixelColorSchemeSettingsService();
        var color = service.ToColor(0xFF102030);

        Assert.AreEqual(0, service.NormalizeChannel(-1));
        Assert.AreEqual(255, service.NormalizeChannel(300));
        Assert.AreEqual(128, service.NormalizeChannel(127.6));
        Assert.AreEqual("16", service.FormatChannel(color.R));
        Assert.AreEqual("255", service.FormatChannel(300));
        Assert.AreEqual(0xFF7F2030, service.FromColor(service.WithRedChannel(color, 127)));
        Assert.AreEqual(0xFF107F30, service.FromColor(service.WithGreenChannel(color, 127)));
        Assert.AreEqual(0xFF10207F, service.FromColor(service.WithBlueChannel(color, 127)));
    }

    [TestMethod]
    public void GetsPreviewColors()
    {
        var service = new PixelColorSchemeSettingsService();

        var preview = service.GetPreviewColors(0xFF2078DA, isDarkMode: false);

        Assert.AreNotEqual(default, preview.Primary);
        Assert.AreNotEqual(default, preview.Secondary);
        Assert.AreNotEqual(default, preview.Tertiary);
    }

    [TestMethod]
    public void ExposesPageMessagesAndPresetColors()
    {
        var service = new PixelColorSchemeSettingsService();
        var messages = service.GetPageMessages();
        var presets = service.GetPresets();

        Assert.IsFalse(string.IsNullOrWhiteSpace(messages.ManualPickerTooltip));
        Assert.IsTrue(messages.ImageFilePatterns.Contains("*.png"));
        Assert.AreEqual(8, presets.Count);
        Assert.AreEqual(0xFF1370F3, presets[0].Seed);
    }
}
