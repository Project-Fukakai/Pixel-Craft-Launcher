using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Events;

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
    public void SettingsBinderReportsMissingConfigKey()
    {
        var setting = new PixelSettingDescriptor("Missing", PixelSettingControlKind.Text, "DefinitelyMissingPixelConfigKey");

        Assert.IsFalse(PixelSettingsBinder.IsControlAvailable(setting));
        Assert.AreEqual("配置项尚未接入：DefinitelyMissingPixelConfigKey", PixelSettingsBinder.GetUnavailableReason(setting));
    }

    [TestMethod]
    public void SettingsBinderRejectsBlacklistedText()
    {
        var infoSetting = PixelSettingsCatalog.Get(PixelSettingSectionKind.Launch)
            .Groups
            .SelectMany(static group => group.Settings)
            .Single(static setting => setting.ConfigKey == "LaunchArgumentInfo");

        Assert.IsFalse(PixelSettingsBinder.SetValue(infoSetting, "bad \" info"));
    }

    [TestMethod]
    public void SettingsBinderRejectsOutOfRangeNumericValue()
    {
        var memorySetting = new PixelSettingDescriptor(
            "Memory",
            PixelSettingControlKind.Slider,
            "LaunchRamCustom",
            Validation: new PixelSettingValidation(Minimum: 0, Maximum: 49));

        Assert.IsFalse(PixelSettingsBinder.SetValue(memorySetting, 99));
    }

    [TestMethod]
    public void SettingsBinderReportsRuntimeInteractionState()
    {
        var oldMode = PixelSettingsBinder.LoadValue("LaunchRamType");
        try
        {
            var memorySetting = new PixelSettingDescriptor("Memory", PixelSettingControlKind.Slider, "LaunchRamCustom");

            PixelSettingsBinder.SetValue("LaunchRamType", 0);
            Assert.IsFalse(PixelSettingsBinder.IsInteractionEnabled(memorySetting));

            PixelSettingsBinder.SetValue("LaunchRamType", 1);
            Assert.IsTrue(PixelSettingsBinder.IsInteractionEnabled(memorySetting));
        }
        finally
        {
            if (oldMode is not null)
                PixelSettingsBinder.SetValue("LaunchRamType", oldMode);
        }
    }

    [TestMethod]
    public void SettingValueServiceExposesRendererMessagesAndSemanticPredicates()
    {
        var service = new PixelSettingValueService();
        var messages = service.GetControlMessages();
        var jvm = new PixelSettingDescriptor("Jvm", PixelSettingControlKind.Text, PixelSettingValueService.LaunchAdvanceJvmConfigKey);
        var background = new PixelSettingDescriptor("Background", PixelSettingControlKind.Text, PixelSettingValueService.BackgroundFolderConfigKey);
        var action = new PixelSettingDescriptor("Open", PixelSettingControlKind.Action, ActionId: PixelSettingValueService.OpenBackgroundFolderActionId);

        Assert.IsFalse(string.IsNullOrWhiteSpace(messages.MissingConfigDescription));
        Assert.IsTrue(service.IsLaunchAdvanceJvmSetting(jvm));
        Assert.IsTrue(service.IsBackgroundFolderSetting(background));
        Assert.IsTrue(service.IsOpenBackgroundFolderAction(action));
    }

    [TestMethod]
    public void SettingValueServiceBuildsControlValueSnapshots()
    {
        var oldWidth = PixelSettingsBinder.LoadValue("LaunchArgumentWindowWidth");
        var oldMode = PixelSettingsBinder.LoadValue("LaunchArgumentWindowType");
        try
        {
            var service = new PixelSettingValueService();
            var slider = new PixelSettingDescriptor(
                "Width",
                PixelSettingControlKind.Slider,
                "LaunchArgumentWindowWidth",
                Minimum: 100,
                Maximum: 200,
                UnitText: "px");
            var combo = new PixelSettingDescriptor(
                "Mode",
                PixelSettingControlKind.Combo,
                "LaunchArgumentWindowType",
                Options:
                [
                    new PixelSettingOption("默认", (int)GameWindowSizeMode.Default),
                    new PixelSettingOption("自定义", (int)GameWindowSizeMode.Custom)
                ]);

            PixelSettingsBinder.SetValue("LaunchArgumentWindowWidth", 400);
            PixelSettingsBinder.SetValue("LaunchArgumentWindowType", (int)GameWindowSizeMode.Custom);

            var sliderState = service.GetSliderState(slider);
            var comboState = service.GetComboState(combo);

            Assert.AreEqual(200, sliderState.Maximum);
            Assert.AreEqual(200, sliderState.Value);
            Assert.AreEqual("200 px", sliderState.ValueText);
            Assert.AreEqual(100, service.NormalizeSliderInput(slider, 1));
            Assert.AreEqual(1, comboState.SelectedIndex);
            Assert.AreEqual(((int)GameWindowSizeMode.Custom).ToString(), comboState.Text);
            Assert.AreEqual("1.5 px", service.FormatSettingValue(slider, 1.5));
            Assert.AreEqual(string.Empty, service.FormatControlValue(null));
        }
        finally
        {
            if (oldWidth is not null)
                PixelSettingsBinder.SetValue("LaunchArgumentWindowWidth", oldWidth);
            if (oldMode is not null)
                PixelSettingsBinder.SetValue("LaunchArgumentWindowType", oldMode);
        }
    }

    [TestMethod]
    public void SettingsBinderKeepsPlainRamScaleMapping()
    {
        Assert.AreEqual(0.4d, PixelSettingsBinder.RamScaleToGb(1));
        Assert.AreEqual(1.5d, PixelSettingsBinder.RamScaleToGb(12));
        Assert.AreEqual(8d, PixelSettingsBinder.RamScaleToGb(25));
        Assert.AreEqual(16d, PixelSettingsBinder.RamScaleToGb(33));
        Assert.AreEqual(48d, PixelSettingsBinder.RamScaleToGb(49));
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

    [TestMethod]
    public void PersonalizationServiceScansConfiguredFolderAndCyclesBackgrounds()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationService", Guid.NewGuid().ToString("N"));
        var oldFolder = PixelSettingsBinder.LoadValue("UiBackgroundFolder")?.ToString() ?? string.Empty;
        Directory.CreateDirectory(folder);
        try
        {
            var first = Path.Combine(folder, "a.png");
            var second = Path.Combine(folder, "b.mp4");
            File.WriteAllText(first, "");
            File.WriteAllText(second, "");
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);

            var service = new PixelPersonalizationService(new Random(1));
            var library = service.ReloadLibrary();

            Assert.AreEqual(folder, service.BackgroundFolder);
            Assert.AreEqual(2, library.Backgrounds.Count);
            Assert.AreEqual(first, service.AdvanceBackground().Media?.Path);
            Assert.AreEqual(second, service.AdvanceBackground().Media?.Path);
            Assert.AreEqual(first, service.AdvanceBackground().Media?.Path);
        }
        finally
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", oldFolder);
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void PersonalizationServiceEnsuresConfiguredBackgroundFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationEnsure", Guid.NewGuid().ToString("N"));
        var oldFolder = PixelSettingsBinder.LoadValue("UiBackgroundFolder")?.ToString() ?? string.Empty;
        try
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            var service = new PixelPersonalizationService();

            var actual = service.EnsureBackgroundFolder();

            Assert.AreEqual(folder, actual);
            Assert.IsTrue(Directory.Exists(folder));
        }
        finally
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", oldFolder);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ShellSettingsServiceBuildsSnapshotsFromConfig()
    {
        var oldDebugMode = PixelSettingsBinder.LoadValue("SystemDebugMode");
        var oldDebugAnim = PixelSettingsBinder.LoadValue("SystemDebugAnim");
        var oldOpacity = PixelSettingsBinder.LoadValue("UiLauncherTransparent");
        var oldLogoText = PixelSettingsBinder.LoadValue("UiLogoText");
        var oldLogoType = PixelSettingsBinder.LoadValue("UiLogoType");
        try
        {
            PixelSettingsBinder.SetValue("SystemDebugMode", true);
            PixelSettingsBinder.SetValue("SystemDebugAnim", 0);
            PixelSettingsBinder.SetValue("UiLauncherTransparent", 300);
            PixelSettingsBinder.SetValue("UiLogoType", (int)LauncherTitleType.Text);
            PixelSettingsBinder.SetValue("UiLogoText", "Pixel");

            var service = new PixelShellSettingsService();
            var animation = service.GetAnimationSettings();
            var appearance = service.GetAppearanceSettings();
            var personalization = service.GetPersonalizationSettings();
            var messages = service.GetMessages();

            Assert.IsFalse(animation.IsAnimationEnabled);
            Assert.AreEqual(0.5d, appearance.WindowOpacity, 0.0001d);
            Assert.IsFalse(personalization.ShowTitlePathLogo);
            Assert.IsTrue(personalization.ShowTitleText);
            Assert.AreEqual("Pixel", personalization.TitleText);
            Assert.IsFalse(personalization.UseTitleImageLogo);
            Assert.AreEqual("Pixel 主界面 Shell 已载入，业务页面会逐步迁入。", messages.LoadedMessage);
            Assert.AreEqual("正在关闭启动器……", messages.ClosingMessage);
            Assert.AreEqual("关闭", messages.CloseButtonText);
            Assert.AreEqual("确定", messages.ConfirmButtonText);
            Assert.AreEqual("取消", messages.CancelButtonText);
            Assert.AreEqual("需要辅助功能权限", messages.AccessibilityPermissionDialog.Title);
            Assert.AreEqual("稍后", messages.AccessibilityPermissionDialog.LaterButtonText);
            Assert.AreEqual("打开设置", messages.AccessibilityPermissionDialog.OpenSettingsButtonText);
        }
        finally
        {
            if (oldDebugMode is not null)
                PixelSettingsBinder.SetValue("SystemDebugMode", oldDebugMode);
            if (oldDebugAnim is not null)
                PixelSettingsBinder.SetValue("SystemDebugAnim", oldDebugAnim);
            if (oldOpacity is not null)
                PixelSettingsBinder.SetValue("UiLauncherTransparent", oldOpacity);
            if (oldLogoText is not null)
                PixelSettingsBinder.SetValue("UiLogoText", oldLogoText);
            if (oldLogoType is not null)
                PixelSettingsBinder.SetValue("UiLogoType", oldLogoType);
        }
    }

    [TestMethod]
    public void SettingsObservationServicePublishesAndDisposesSettingChanges()
    {
        using var eventBus = new ReactivePixelEventBus();
        var service = new PixelSettingsObservationService(eventBus);
        var oldText = PixelSettingsBinder.LoadValue("UiLogoText")?.ToString() ?? string.Empty;
        var observed = new List<PixelSettingChangedEvent>();
        var published = new List<PixelSettingChangedEvent>();
        using var eventSubscription = eventBus.Observe<PixelSettingChangedEvent>()
            .Subscribe(new RecordingObserver<PixelSettingChangedEvent>(published));
        try
        {
            var subscription = service.Observe("UiLogoText", observed.Add);

            PixelSettingsBinder.SetValue("UiLogoText", "Observed");

            Assert.AreEqual(1, observed.Count);
            Assert.AreEqual("UiLogoText", observed[0].Key);
            Assert.AreEqual("Observed", observed[0].Value);
            Assert.AreEqual(1, published.Count);

            subscription.Dispose();
            PixelSettingsBinder.SetValue("UiLogoText", "Ignored");

            Assert.AreEqual(1, observed.Count);
            Assert.AreEqual(1, published.Count);
        }
        finally
        {
            PixelSettingsBinder.SetValue("UiLogoText", oldText);
        }
    }

    [TestMethod]
    public void SettingValueServiceSetsAndResetsDescriptorValues()
    {
        var setting = PixelSettingsCatalog.Get(PixelSettingSectionKind.Ui)
            .Groups
            .SelectMany(group => group.Settings)
            .First(item => item.ConfigKey == "UiLogoText");
        var oldText = PixelSettingsBinder.LoadValue("UiLogoText")?.ToString() ?? string.Empty;
        try
        {
            var service = new PixelSettingValueService();

            Assert.IsTrue(service.SetValue(setting, "Facade"));
            Assert.AreEqual("Facade", service.LoadValue("UiLogoText"));

            var reset = service.Reset("UiLogoText");

            Assert.IsTrue(reset.Success);
            Assert.AreEqual(service.LoadValue("UiLogoText"), reset.Value);
        }
        finally
        {
            PixelSettingsBinder.SetValue("UiLogoText", oldText);
        }
    }

    [TestMethod]
    public void PersonalizationServiceClampsCarouselInterval()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationCarousel", Guid.NewGuid().ToString("N"));
        var oldFolder = PixelSettingsBinder.LoadValue("UiBackgroundFolder")?.ToString() ?? string.Empty;
        var oldCarousel = PixelSettingsBinder.LoadValue("UiBackgroundCarousel");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "a.png"), "");
            File.WriteAllText(Path.Combine(folder, "b.png"), "");
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            PixelSettingsBinder.SetValue("UiBackgroundCarousel", 1);

            var service = new PixelPersonalizationService();
            service.ReloadLibrary();

            Assert.AreEqual(TimeSpan.FromSeconds(5), service.GetCarouselInterval());
        }
        finally
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", oldFolder);
            if (oldCarousel is not null)
                PixelSettingsBinder.SetValue("UiBackgroundCarousel", oldCarousel);
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class RecordingObserver<T>(ICollection<T> values) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Assert.Fail(error.ToString());
        }

        public void OnNext(T value)
        {
            values.Add(value);
        }
    }
}
