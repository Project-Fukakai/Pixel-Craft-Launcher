using PCL.Core.App.Configuration;

namespace PCL.Core.App.Pixel;

public sealed class PixelShellSettingsService
{
    public PixelShellColorSchemeSettings GetColorSchemeSettings()
    {
        return new PixelShellColorSchemeSettings(
            AutoBackgroundEnabled: PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground") is bool enabled && enabled);
    }

    public PixelShellAnimationSettings GetAnimationSettings()
    {
        var debugEnabled = PixelSettingsBinder.LoadValue("SystemDebugMode") is bool enabled && enabled;
        var rawSpeed = PixelSettingsBinder.LoadValue("SystemDebugAnim") is IConvertible value
            ? Convert.ToInt32(value)
            : 9;

        return new PixelShellAnimationSettings(
            IsAnimationEnabled: !debugEnabled || rawSpeed > 0,
            AnimationSpeed: debugEnabled ? DebugSettingsService.ResolveAnimationScale(rawSpeed) : 1d);
    }

    public PixelShellAppearanceSettings GetAppearanceSettings()
    {
        var rawOpacity = PixelSettingsBinder.LoadValue("UiLauncherTransparent") is IConvertible value
            ? Convert.ToDouble(value)
            : 600d;

        return new PixelShellAppearanceSettings(
            AcrylicEnabled: PixelSettingsBinder.LoadValue("UiAcrylic") is bool acrylic && acrylic,
            WindowOpacity: Math.Clamp(rawOpacity / 600d, 0.2d, 1d),
            BackgroundColorful: PixelSettingsBinder.LoadValue("UiBackgroundColorful") is not bool colorful || colorful);
    }

    public PixelShellPersonalizationSettings GetPersonalizationSettings()
    {
        var titleType = PixelSettingsBinder.LoadValue("UiLogoType") is int rawType
            ? (LauncherTitleType)rawType
            : LauncherTitleType.Default;
        var showDefaultLogo = PixelSettingsBinder.LoadValue("UiLauncherLogo") is not bool showLogo || showLogo;
        var titleText = PixelSettingsBinder.LoadValue("UiLogoText")?.ToString();
        return new PixelShellPersonalizationSettings(
            CanResize: PixelSettingsBinder.LoadValue("UiLockWindowSize") is not bool locked || !locked,
            FontName: PixelSettingsBinder.LoadValue("UiFont")?.ToString(),
            ShowTitlePathLogo: titleType == LauncherTitleType.Default && showDefaultLogo,
            ShowTitleText: titleType is LauncherTitleType.Text or LauncherTitleType.Default,
            TitleText: titleType == LauncherTitleType.Text
                ? string.IsNullOrWhiteSpace(titleText) ? "PCL" : titleText
                : showDefaultLogo ? string.Empty : "PCL",
            UseTitleImageLogo: titleType == LauncherTitleType.Image,
            IsTitleLeftAligned: PixelSettingsBinder.LoadValue("UiLogoLeft") is bool left && left);
    }

    public PixelShellMessages GetMessages()
    {
        return new PixelShellMessages(
            "Pixel 主界面 Shell 已载入，业务页面会逐步迁入。",
            "正在关闭启动器……",
            "关闭",
            "确定",
            "取消",
            new PixelAccessibilityPermissionDialogSnapshot(
                "需要辅助功能权限",
                "macOS 需要授予 Pixel Craft Launcher “辅助功能”权限，才能用 Accessibility Observer 监听 Minecraft 窗口出现。\n\n请在系统设置打开后，进入“隐私与安全性 → 辅助功能”，启用 Pixel Craft Launcher。授权完成后请重新点击启动。",
                "稍后",
                "打开设置"));
    }
}

public sealed record PixelShellColorSchemeSettings(bool AutoBackgroundEnabled);

public sealed record PixelShellAnimationSettings(bool IsAnimationEnabled, double AnimationSpeed);

public sealed record PixelShellAppearanceSettings(
    bool AcrylicEnabled,
    double WindowOpacity,
    bool BackgroundColorful);

public sealed record PixelShellPersonalizationSettings(
    bool CanResize,
    string? FontName,
    bool ShowTitlePathLogo,
    bool ShowTitleText,
    string? TitleText,
    bool UseTitleImageLogo,
    bool IsTitleLeftAligned);

public sealed record PixelShellMessages(
    string LoadedMessage,
    string ClosingMessage,
    string CloseButtonText,
    string ConfirmButtonText,
    string CancelButtonText,
    PixelAccessibilityPermissionDialogSnapshot AccessibilityPermissionDialog);

public sealed record PixelAccessibilityPermissionDialogSnapshot(
    string Title,
    string Body,
    string LaterButtonText,
    string OpenSettingsButtonText);
