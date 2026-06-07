using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelSettingsCatalogTest
{
    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
    }

    [TestMethod]
    public void CatalogConfigKeysExist()
    {
        var knownKeys = ConfigService.KeySet;

        var missingKeys = PixelSettingsCatalog.Sections
            .SelectMany(section => section.Groups)
            .SelectMany(group => group.Settings)
            .Select(setting => setting.ConfigKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Where(key => !knownKeys.Contains(key!))
            .Distinct()
            .Order()
            .ToArray();

        Assert.AreEqual(0, missingKeys.Length, "Unknown Pixel setting config keys: " + string.Join(", ", missingKeys));
    }

    [TestMethod]
    public void CatalogComboOptionsMatchConfigTypes()
    {
        var errors = new List<string>();
        foreach (var setting in PixelSettingsCatalog.Sections.SelectMany(section => section.Groups).SelectMany(group => group.Settings))
        {
            if (setting.ConfigKey is null || setting.Options is null ||
                !ConfigService.TryGetConfigItemNoType(setting.ConfigKey, out var item))
                continue;

            foreach (var option in setting.Options)
            {
                var type = item.Type;
                if (type == typeof(string) && option.Value is string)
                    continue;
                if ((type == typeof(int) || type.IsEnum) && option.Value is int)
                    continue;
                errors.Add($"{setting.ConfigKey}:{option.Value.GetType().Name} cannot bind to {type.Name}");
            }
        }

        Assert.AreEqual(0, errors.Count, "Invalid Pixel setting option value types: " + string.Join(", ", errors));
    }

    [TestMethod]
    public void WindowsOnlySettingsDeclareDisabledReason()
    {
        var missingReason = PixelSettingsCatalog.Sections
            .SelectMany(section => section.Groups)
            .SelectMany(group => group.Settings)
            .Where(setting => setting.PlatformAvailability == PixelSettingPlatformAvailability.WindowsOnly)
            .Where(setting => string.IsNullOrWhiteSpace(setting.Description) && string.IsNullOrWhiteSpace(setting.DisabledReason))
            .Select(setting => setting.ConfigKey ?? setting.Title)
            .ToArray();

        Assert.AreEqual(0, missingReason.Length, "Windows-only settings need a description or disabled reason: " + string.Join(", ", missingReason));
    }

    [TestMethod]
    public void HiddenSetupSectionsAreBackedByConfig()
    {
        var knownKeys = ConfigService.KeySet;

        var missingKeys = PixelSettingsCatalog.Sections
            .Select(section => section.HiddenConfigKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Where(key => !knownKeys.Contains(key!))
            .ToArray();

        Assert.AreEqual(0, missingKeys.Length, "Unknown hidden setup config keys: " + string.Join(", ", missingKeys));
    }

    [TestMethod]
    public void LauncherMiscExposesAllMiscSettings()
    {
        var expectedKeys = new[]
        {
            "SystemDisableHardwareAcceleration", "SystemTelemetry", "SystemMaxLog", "UiAniFPS",
            "SystemNetEnableDoH", "SystemHttpProxyType", "SystemHttpProxy", "SystemHttpProxyCustomUsername",
            "SystemHttpProxyCustomPassword", "SystemDebugMode", "SystemDebugAnim", "SystemDebugDelay",
            "SystemDebugSkipCopy", "SystemDebugAllowRestrictedFeature", "UiHiddenPageDownload",
            "UiHiddenPageSetup", "UiHiddenPageTools", "UiHiddenSetupLaunch", "UiHiddenSetupJava",
            "UiHiddenSetupGameManage", "UiHiddenSetupGameLink", "UiHiddenSetupUi", "UiHiddenSetupLauncherMisc",
            "UiHiddenSetupAbout", "UiHiddenSetupUpdate", "UiHiddenSetupFeedback", "UiHiddenSetupLog",
            "UiHiddenToolsGameLink", "UiHiddenToolsHelp", "UiHiddenToolsTest", "UiHiddenVersionEdit",
            "UiHiddenVersionExport", "UiHiddenVersionSave", "UiHiddenVersionScreenshot", "UiHiddenVersionMod",
            "UiHiddenVersionResourcePack", "UiHiddenVersionShader", "UiHiddenVersionSchematic",
            "UiHiddenVersionServer", "UiHiddenFunctionSelect", "UiHiddenFunctionModUpdate", "UiHiddenFunctionHidden"
        };
        var actualKeys = PixelSettingsCatalog.Get(PixelSettingSectionKind.LauncherMisc)
            .Groups
            .SelectMany(static group => group.Settings)
            .Select(static setting => setting.ConfigKey)
            .Where(static key => key is not null)
            .ToHashSet();
        var missingKeys = expectedKeys.Where(key => !actualKeys.Contains(key)).ToArray();

        Assert.AreEqual(0, missingKeys.Length, "LauncherMisc is missing settings: " + string.Join(", ", missingKeys));
    }

    [TestMethod]
    public void UiCatalogDoesNotExposeAdvancedMaterialSettings()
    {
        var removedKeys = new[] { "UiBlur", "UiBlurValue", "UiBlurSamplingRate", "UiBlurType" };
        var actualKeys = PixelSettingsCatalog.Get(PixelSettingSectionKind.Ui)
            .Groups
            .SelectMany(static group => group.Settings)
            .Select(static setting => setting.ConfigKey)
            .Where(static key => key is not null)
            .ToArray();

        CollectionAssert.DoesNotContain(actualKeys, removedKeys[0]);
        CollectionAssert.DoesNotContain(actualKeys, removedKeys[1]);
        CollectionAssert.DoesNotContain(actualKeys, removedKeys[2]);
        CollectionAssert.DoesNotContain(actualKeys, removedKeys[3]);
    }

    [TestMethod]
    public void UiCatalogUsesColorSchemeInsteadOfLightDarkThemeCombos()
    {
        var settings = PixelSettingsCatalog.Get(PixelSettingSectionKind.Ui)
            .Groups
            .SelectMany(static group => group.Settings)
            .ToArray();
        var keys = settings.Select(static setting => setting.ConfigKey).Where(static key => key is not null).ToArray();

        CollectionAssert.DoesNotContain(keys, "UiLightColor");
        CollectionAssert.DoesNotContain(keys, "UiDarkColor");
        Assert.AreEqual(1, settings.Count(static setting => setting.Control == PixelSettingControlKind.ColorScheme));
    }

    [TestMethod]
    public void PersonalizationScannerClassifiesSupportedMedia()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationScanner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "a.png"), "");
            File.WriteAllText(Path.Combine(folder, "b.MP4"), "");
            File.WriteAllText(Path.Combine(folder, "c.flac"), "");
            File.WriteAllText(Path.Combine(folder, "d.txt"), "");

            var library = PixelPersonalizationScanner.Scan(folder);

            Assert.AreEqual(1, library.Images.Count);
            Assert.AreEqual(1, library.Videos.Count);
            Assert.AreEqual(1, library.Audio.Count);
            Assert.AreEqual(2, library.Backgrounds.Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void PersonalizationScannerHandlesMissingFolder()
    {
        var library = PixelPersonalizationScanner.Scan(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.AreEqual(0, library.Images.Count);
        Assert.AreEqual(0, library.Videos.Count);
        Assert.AreEqual(0, library.Audio.Count);
        Assert.AreEqual(0, library.Backgrounds.Count);
    }
}
