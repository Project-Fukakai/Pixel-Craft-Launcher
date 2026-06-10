using PCL.Core.App.Pixel;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record PixelLaunchSecondaryActionSnapshot(
    bool IsSelectVisible,
    bool IsSelectEnabled,
    bool IsEditVisible,
    bool IsEditEnabled);

public sealed record PixelLaunchSidebarSnapshot(
    PixelLaunchSecondaryActionSnapshot SecondaryActions,
    bool IsTestToolVisible);

public sealed record PixelLaunchInstanceActionVisibilitySnapshot(
    bool CanOpenFolder,
    bool CanOpenMods,
    bool CanOpenSaves);

public sealed record PixelLaunchSelectedInstanceFolderSnapshot(
    bool CanOpen,
    string? VersionDirectory);

public sealed record PixelLaunchPageMessages(
    string InstanceSettingsTitle,
    string InstanceSettingsPlaceholder,
    string InstanceSettingsButtonText,
    string InstancesRefreshSuccess,
    string FolderPathUnavailable,
    string FolderAdded,
    string InstanceSelectButtonText,
    string InstanceListCardTitle,
    string InstanceListEmptyText,
    string InstanceRefreshButtonText,
    string InstanceOpenFolderButtonText,
    string ProfileManageButtonText,
    string OpenInstanceFolderTip,
    string OpenModsFolderTip,
    string OpenSavesFolderTip,
    string FolderListCategoryTitle,
    string FolderManageCategoryTitle,
    string AddFolderButtonText,
    string AddFolderButtonDescription,
    string FolderActionButtonTooltip,
    string FolderOpenActionText,
    string FolderRefreshActionText,
    string LaunchLogCardTitle,
    string AddFolderPickerTitle,
    string LaunchStageLabel,
    string LaunchProfileMethodLabel,
    string LaunchProgressLabel,
    string CancelLaunchButtonText);

public sealed class PixelLaunchSidebarService
{
    internal PixelLaunchSecondaryActionSnapshot GetSecondaryActions(
        MinecraftInstanceInfo? selectedInstance,
        bool isLaunching)
    {
        return new PixelLaunchSecondaryActionSnapshot(
            FeatureVisibilityService.IsFunctionVisible(PixelFunctionFeature.Select),
            !isLaunching,
            FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Edit),
            selectedInstance is not null);
    }

    internal PixelLaunchSidebarSnapshot GetSidebarSnapshot(
        MinecraftInstanceInfo? selectedInstance,
        bool isLaunching)
    {
        return new PixelLaunchSidebarSnapshot(
            GetSecondaryActions(selectedInstance, isLaunching),
            IsTestToolVisible());
    }

    public PixelLaunchInstanceActionVisibilitySnapshot GetInstanceActions()
    {
        return new PixelLaunchInstanceActionVisibilitySnapshot(
            FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Edit),
            FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Mod),
            FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Save));
    }

    internal PixelLaunchSelectedInstanceFolderSnapshot GetSelectedInstanceFolder(MinecraftInstanceInfo? selectedInstance)
    {
        return selectedInstance is null
            ? new PixelLaunchSelectedInstanceFolderSnapshot(false, null)
            : new PixelLaunchSelectedInstanceFolderSnapshot(true, selectedInstance.VersionDirectory);
    }

    public bool IsTestToolVisible()
    {
        return FeatureVisibilityService.IsToolVisible(PixelToolFeature.Test);
    }

    public PixelLaunchPageMessages GetPageMessages()
    {
        return new PixelLaunchPageMessages(
            "实例设置",
            "实例设置页面将在后续迁移到这里。",
            "实例设置",
            "实例列表已刷新。",
            "无法读取所选文件夹路径。",
            "已添加实例文件夹。",
            "实例选择",
            "实例列表",
            "当前文件夹未找到可用实例。安装 Minecraft 后会显示在这里。",
            "刷新",
            "打开文件夹",
            "档案管理",
            "打开实例文件夹",
            "打开 Mods",
            "打开存档",
            "文件夹列表",
            "管理",
            "添加或导入",
            "选择已有 .minecraft 文件夹",
            "文件夹操作",
            "打开",
            "刷新",
            "启动日志",
            "选择 Minecraft 文件夹",
            "当前步骤",
            "验证方式",
            "启动进度",
            "取消");
    }

    public static string GetOpenFolderFailedMessage(Exception exception) =>
        "打开文件夹失败：" + exception.Message;
}
