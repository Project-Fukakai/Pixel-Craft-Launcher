using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchSidebarServiceTest
{
    [TestMethod]
    public void SecondaryActionsExposeEnabledStateForUi()
    {
        var service = new PixelLaunchSidebarService();

        var noInstance = service.GetSecondaryActions(selectedInstance: null, isLaunching: true);
        var hasInstance = service.GetSecondaryActions(
            PixelLaunchInstanceListServiceTest.CreateInstanceForSidebarTest(),
            isLaunching: false);

        Assert.IsFalse(noInstance.IsSelectEnabled);
        Assert.IsFalse(noInstance.IsEditEnabled);
        Assert.IsTrue(hasInstance.IsSelectEnabled);
        Assert.IsTrue(hasInstance.IsEditEnabled);

        var sidebar = service.GetSidebarSnapshot(
            PixelLaunchInstanceListServiceTest.CreateInstanceForSidebarTest(),
            isLaunching: false);
        Assert.AreEqual(hasInstance, sidebar.SecondaryActions);
        Assert.IsTrue(sidebar.IsTestToolVisible);
    }

    [TestMethod]
    public void InstanceActionsExposeVisibilitySnapshot()
    {
        var service = new PixelLaunchSidebarService();

        var snapshot = service.GetInstanceActions();

        Assert.IsTrue(snapshot.CanOpenFolder);
        Assert.IsTrue(snapshot.CanOpenMods);
        Assert.IsTrue(snapshot.CanOpenSaves);
    }

    [TestMethod]
    public void SelectedInstanceFolderExposesOpenStateAndPath()
    {
        var service = new PixelLaunchSidebarService();
        var instance = PixelLaunchInstanceListServiceTest.CreateInstanceForSidebarTest();

        var none = service.GetSelectedInstanceFolder(null);
        var selected = service.GetSelectedInstanceFolder(instance);

        Assert.IsFalse(none.CanOpen);
        Assert.IsNull(none.VersionDirectory);
        Assert.IsTrue(selected.CanOpen);
        Assert.AreEqual(instance.VersionDirectory, selected.VersionDirectory);
    }

    [TestMethod]
    public void PageMessagesExposeLaunchInstanceOperationText()
    {
        var service = new PixelLaunchSidebarService();

        var messages = service.GetPageMessages();

        Assert.AreEqual("实例设置", messages.InstanceSettingsTitle);
        Assert.AreEqual("实例设置页面将在后续迁移到这里。", messages.InstanceSettingsPlaceholder);
        Assert.AreEqual("实例设置", messages.InstanceSettingsButtonText);
        Assert.AreEqual("实例列表已刷新。", messages.InstancesRefreshSuccess);
        Assert.AreEqual("无法读取所选文件夹路径。", messages.FolderPathUnavailable);
        Assert.AreEqual("已添加实例文件夹。", messages.FolderAdded);
        Assert.AreEqual("实例选择", messages.InstanceSelectButtonText);
        Assert.AreEqual("实例列表", messages.InstanceListCardTitle);
        Assert.AreEqual("当前文件夹未找到可用实例。安装 Minecraft 后会显示在这里。", messages.InstanceListEmptyText);
        Assert.AreEqual("刷新", messages.InstanceRefreshButtonText);
        Assert.AreEqual("打开文件夹", messages.InstanceOpenFolderButtonText);
        Assert.AreEqual("档案管理", messages.ProfileManageButtonText);
        Assert.AreEqual("打开实例文件夹", messages.OpenInstanceFolderTip);
        Assert.AreEqual("打开 Mods", messages.OpenModsFolderTip);
        Assert.AreEqual("打开存档", messages.OpenSavesFolderTip);
        Assert.AreEqual("文件夹列表", messages.FolderListCategoryTitle);
        Assert.AreEqual("管理", messages.FolderManageCategoryTitle);
        Assert.AreEqual("添加或导入", messages.AddFolderButtonText);
        Assert.AreEqual("选择已有 .minecraft 文件夹", messages.AddFolderButtonDescription);
        Assert.AreEqual("文件夹操作", messages.FolderActionButtonTooltip);
        Assert.AreEqual("打开", messages.FolderOpenActionText);
        Assert.AreEqual("刷新", messages.FolderRefreshActionText);
        Assert.AreEqual("启动日志", messages.LaunchLogCardTitle);
        Assert.AreEqual("选择 Minecraft 文件夹", messages.AddFolderPickerTitle);
        Assert.AreEqual("当前步骤", messages.LaunchStageLabel);
        Assert.AreEqual("验证方式", messages.LaunchProfileMethodLabel);
        Assert.AreEqual("启动进度", messages.LaunchProgressLabel);
        Assert.AreEqual("取消", messages.CancelLaunchButtonText);
        Assert.AreEqual(
            "打开文件夹失败：boom",
            PixelLaunchSidebarService.GetOpenFolderFailedMessage(new System.InvalidOperationException("boom")));
    }
}
