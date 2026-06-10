using System;
using System.Collections.Generic;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private void WireThemeEvents()
    {
        _settingsSubscriptions.Add(_themeBridge.SubscribeThemeChanged(
            () => Dispatcher.UIThread.Post(RefreshShellTheme)));
        ObserveSettings(["UiAcrylic"], _ => Dispatcher.UIThread.Post(RefreshShellTheme));
        ObserveSettings([
            "UiColorSchemeMode", "UiColorSchemeSeed", "UiColorSchemeImage", "UiColorSchemeAutoBackground"
        ], _ => Dispatcher.UIThread.Post(() =>
            {
                RefreshShellTheme();
            }));
    }

    private void WirePersonalizationEvents()
    {
        _personalization.BackgroundChanged += control =>
            Dispatcher.UIThread.Post(() =>
            {
                BackgroundMediaHost.Content = control;
                _shellThemePresenter.ApplyBackgroundOverlay();
            });
        _personalization.BackgroundImageChanged += _ =>
            Dispatcher.UIThread.Post(() =>
            {
                if (_shellSettingsService.GetColorSchemeSettings().AutoBackgroundEnabled)
                    RefreshShellTheme();
            });

        ObserveSettings([
            "UiLauncherTransparent", "UiLockWindowSize", "UiFont", "UiLogoType", "UiLogoText", "UiLogoLeft",
            "UiLauncherLogo", "UiBackgroundFolder", "UiBackgroundSuit", "UiBackgroundOpacity",
            "UiBackgroundCarousel", "UiBackgroundBlur", "UiBackgroundColorful", "UiAutoPauseVideo",
            "UiMusicVolume", "UiMusicRandom", "UiMusicAuto", "UiMusicStart", "UiMusicStop", "UiMusicSMTC",
            "UiCustomType", "UiCustomPreset", "UiCustomNet"
        ], change => Dispatcher.UIThread.Post(() =>
            {
                var reloadLibrary = change.Key == "UiBackgroundFolder";
                ApplyWindowPersonalization();
                _personalization.RefreshFromSettings(reloadLibrary);
                if (SelectedMainPage == MainPageKind.Launch)
                    SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
            }));
    }

    private void WireLauncherMiscEvents()
    {
        ObserveSettings([
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
        ], change => Dispatcher.UIThread.Post(() =>
            {
                var key = change.Key;
                var effect = ApplySettingChangeEffect(change);
                ApplyLauncherMiscSettings();
                if (key.StartsWith("UiHidden", StringComparison.Ordinal))
                    RefreshSetupVisibility();
                if (SelectedMainPage == MainPageKind.Tools && key.StartsWith("UiHiddenTools", StringComparison.Ordinal))
                    SetPageHostContent(RightContentHost, BuildControlsPreviewPage(), PageHostUpdateMode.SilentRefresh);
                if (SelectedMainPage == MainPageKind.Launch && key.StartsWith("UiHiddenVersion", StringComparison.Ordinal))
                    RefreshLaunchPage(null);
                if (effect.RefreshSetupRightPage || key == "SystemHttpProxyType")
                    RefreshSetupRightPage();
                if (effect.RefreshShellTheme)
                    RefreshShellTheme();
            }));
    }

    private PixelSettingChangeEffect ApplySettingChangeEffect(PixelSettingChangedEvent change)
    {
        var effect = _settingsChangeService.Apply(change.Key, change.Value);
        if (effect.Notification is { } notification)
            GetSetupSettingsBridge().ShowSettingNotification(notification);
        return effect;
    }

    private void ApplyLauncherMiscSettings()
    {
        ApplyFeatureVisibility();
        ApplyShellAnimationSettings();
    }

    private void ApplyFeatureVisibility()
    {
        var visibility = _shellVisibilityService.GetMainPageVisibility();
        BtnDownload.IsVisible = visibility.IsDownloadVisible;
        BtnSetup.IsVisible = visibility.IsSetupVisible;
        BtnTools.IsVisible = visibility.IsToolsVisible;

        if (!visibility.IsVisible(SelectedMainPage))
            _shellViewModel.NavigateMainPage(MainPageKind.Launch);
    }

    private void ApplyShellAnimationSettings()
    {
        var settings = _shellSettingsService.GetAnimationSettings();
        ModAnimation.IsEnabled = settings.IsAnimationEnabled;
        ModAnimation.AniSpeed = settings.AnimationSpeed;
    }

    private void RefreshSetupVisibility()
    {
        if (!_shellVisibilityService.IsSetupSectionVisible(_selectedSetupSection))
            LoadInitialSetupSection();
        if (SelectedMainPage == MainPageKind.Setup)
        {
            SetPageHostContent(LeftContentHost, BuildSetupLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
        }
    }

    private void RefreshShellTheme()
    {
        _shellThemePresenter.RefreshShellTheme();
        RefreshCurrentPageTheme();
    }

    private void ApplyWindowPersonalization()
    {
        _shellThemePresenter.ApplyWindowPersonalization();
    }

    private void ObserveSettings(IEnumerable<string> keys, Action<PixelSettingChangedEvent> handler)
    {
        _settingsSubscriptions.Add(_settingsObservationService.Observe(keys, handler));
    }

    private void DisposeSettingsSubscriptions()
    {
        foreach (var subscription in _settingsSubscriptions)
            subscription.Dispose();
        _settingsSubscriptions.Clear();
    }

    private void RefreshCurrentPageTheme()
    {
        if (!_isMainPageReady)
            return;

        var route = _shellViewModel.CurrentRoute;
        var page = SelectedMainPage;
        SetPageHostContent(LeftContentHost, BuildRouteLeftPage(route, page), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildRouteRightPage(route, page), PageHostUpdateMode.SilentRefresh);
    }
}
