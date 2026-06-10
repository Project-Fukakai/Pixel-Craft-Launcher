using PCL.Core.App;

namespace PCL.Core.App.Pixel;

public sealed class PixelSettingsChangeService
{
    private readonly IPixelRendererHintState _rendererHintState;

    public PixelSettingsChangeService()
        : this(new PixelRendererHintState())
    {
    }

    public PixelSettingsChangeService(IPixelRendererHintState rendererHintState)
    {
        _rendererHintState = rendererHintState;
    }

    public PixelSettingNotificationMessages GetNotificationMessages() => PixelSettingNotificationMessages.Default;

    public PixelSettingChangePresentation CreatePresentation(
        PixelSettingChangeEffect effect,
        bool isRunWaitVisible)
    {
        var nextRunWaitVisible = effect.CheckRunWaitBoundary
            ? !string.IsNullOrWhiteSpace(effect.RunWaitCommand)
            : isRunWaitVisible;
        var refreshForRunWaitBoundary = effect.CheckRunWaitBoundary && nextRunWaitVisible != isRunWaitVisible;
        return new PixelSettingChangePresentation(
            effect.RefreshSetupRightPage || refreshForRunWaitBoundary,
            effect.RefreshShellTheme,
            nextRunWaitVisible,
            effect.Notification);
    }

    public PixelSettingChangeEffect Apply(string? key, object? value)
    {
        return key switch
        {
            "LaunchArgumentWindowType" or "LaunchRamType" or "SystemHttpProxyType" =>
                PixelSettingChangeEffect.RefreshSetup(),
            "SystemDisableHardwareAcceleration" =>
                PixelSettingChangeEffect.Hint("禁用硬件加速将在下次启动启动器时生效。"),
            "UiAcrylic" =>
                PixelSettingChangeEffect.RefreshShell(),
            "LaunchAdvanceRun" =>
                PixelSettingChangeEffect.ForRunWaitBoundary(value?.ToString()),
            "LaunchAdvanceRenderer" =>
                HandleRendererChange(value),
            "LaunchArgumentVisible" =>
                HandleLauncherVisibilityChange(value),
            "LaunchArgumentRam" =>
                value is true
                    ? PixelSettingChangeEffect.Message(
                        "提醒",
                        "内存优化会显著延长启动耗时，建议仅在内存不足时开启。机械硬盘上还可能造成一小段时间的明显卡顿。")
                    : PixelSettingChangeEffect.None,
            _ => PixelSettingChangeEffect.None
        };
    }

    private PixelSettingChangeEffect HandleRendererChange(object? value)
    {
        if (value is not int renderer || renderer == 0 || _rendererHintState.RendererWarningShown)
            return PixelSettingChangeEffect.None;

        _rendererHintState.RendererWarningShown = true;
        return PixelSettingChangeEffect.Message(
            "警告",
            "修改渲染器会显著影响游戏稳定性与性能。只有在明确知道自己需要软渲染、DirectX12 或 Vulkan 兼容层时才建议继续使用。",
            IsWarning: true);
    }

    private static PixelSettingChangeEffect HandleLauncherVisibilityChange(object? value)
    {
        return value is int visibility && visibility == (int)LauncherVisibility.ExitImmediately
            ? PixelSettingChangeEffect.Message(
                "提醒",
                "若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。若想保留这些功能，可以选择让启动器在游戏启动后隐藏。")
            : PixelSettingChangeEffect.None;
    }
}

public sealed record PixelSettingChangeEffect(
    bool RefreshSetupRightPage,
    bool RefreshShellTheme,
    bool CheckRunWaitBoundary,
    string? RunWaitCommand,
    PixelSettingNotification? Notification)
{
    public static PixelSettingChangeEffect None { get; } = new(false, false, false, null, null);

    public static PixelSettingChangeEffect RefreshSetup() => new(true, false, false, null, null);

    public static PixelSettingChangeEffect RefreshShell() => new(false, true, false, null, null);

    public static PixelSettingChangeEffect ForRunWaitBoundary(string? command) => new(false, false, true, command, null);

    public static PixelSettingChangeEffect Hint(string text) =>
        new(false, false, false, null, new PixelSettingNotification(PixelSettingNotificationKind.Hint, null, text, false));

    public static PixelSettingChangeEffect Message(string title, string text, bool IsWarning = false) =>
        new(false, false, false, null, new PixelSettingNotification(PixelSettingNotificationKind.Message, title, text, IsWarning));
}

public sealed record PixelSettingChangePresentation(
    bool RefreshSetupRightPage,
    bool RefreshShellTheme,
    bool RunWaitVisible,
    PixelSettingNotification? Notification);

public enum PixelSettingNotificationKind
{
    Hint,
    Message
}

public sealed record PixelSettingNotification(
    PixelSettingNotificationKind Kind,
    string? Title,
    string Text,
    bool IsWarning);

public sealed record PixelSettingNotificationMessages(string DefaultMessageTitle)
{
    public static PixelSettingNotificationMessages Default { get; } = new("提醒");
}

public interface IPixelRendererHintState
{
    bool RendererWarningShown { get; set; }
}

public sealed class PixelRendererHintState : IPixelRendererHintState
{
    public bool RendererWarningShown
    {
        get => States.Hint.Renderer;
        set => States.Hint.Renderer = value;
    }
}
