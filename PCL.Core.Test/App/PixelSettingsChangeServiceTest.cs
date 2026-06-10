using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelSettingsChangeServiceTest
{
    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
    }

    [TestMethod]
    public void RefreshSettingsReturnRefreshEffects()
    {
        var service = new PixelSettingsChangeService();

        Assert.IsTrue(service.Apply("LaunchRamType", 1).RefreshSetupRightPage);
        Assert.IsTrue(service.Apply("SystemHttpProxyType", 2).RefreshSetupRightPage);
        Assert.IsTrue(service.Apply("UiAcrylic", true).RefreshShellTheme);
    }

    [TestMethod]
    public void HardwareAccelerationReturnsHint()
    {
        var effect = new PixelSettingsChangeService().Apply("SystemDisableHardwareAcceleration", true);

        Assert.AreEqual(PixelSettingNotificationKind.Hint, effect.Notification?.Kind);
        StringAssert.Contains(effect.Notification!.Text, "下次启动");
    }

    [TestMethod]
    public void NotificationMessagesExposeFallbackTitle()
    {
        var messages = new PixelSettingsChangeService().GetNotificationMessages();

        Assert.AreEqual("提醒", messages.DefaultMessageTitle);
    }

    [TestMethod]
    public void RendererWarningIsOnlyShownOnce()
    {
        var rendererHint = new InMemoryRendererHintState();
        var service = new PixelSettingsChangeService(rendererHint);

        var first = service.Apply("LaunchAdvanceRenderer", 1);
        var second = service.Apply("LaunchAdvanceRenderer", 2);

        Assert.AreEqual(PixelSettingNotificationKind.Message, first.Notification?.Kind);
        Assert.IsTrue(first.Notification!.IsWarning);
        Assert.IsNull(second.Notification);
        Assert.IsTrue(rendererHint.RendererWarningShown);
    }

    [TestMethod]
    public void PreLaunchCommandRequestsBoundaryCheck()
    {
        var effect = new PixelSettingsChangeService().Apply("LaunchAdvanceRun", "echo ok");

        Assert.IsTrue(effect.CheckRunWaitBoundary);
        Assert.AreEqual("echo ok", effect.RunWaitCommand);
    }

    [TestMethod]
    public void PresentationRefreshesWhenRunWaitVisibilityChanges()
    {
        var service = new PixelSettingsChangeService();

        var visible = service.CreatePresentation(service.Apply("LaunchAdvanceRun", "echo ok"), isRunWaitVisible: false);
        var stillVisible = service.CreatePresentation(service.Apply("LaunchAdvanceRun", "echo ok"), isRunWaitVisible: true);
        var hidden = service.CreatePresentation(service.Apply("LaunchAdvanceRun", string.Empty), isRunWaitVisible: true);

        Assert.IsTrue(visible.RefreshSetupRightPage);
        Assert.IsTrue(visible.RunWaitVisible);
        Assert.IsFalse(stillVisible.RefreshSetupRightPage);
        Assert.IsTrue(stillVisible.RunWaitVisible);
        Assert.IsTrue(hidden.RefreshSetupRightPage);
        Assert.IsFalse(hidden.RunWaitVisible);
    }

    [TestMethod]
    public void PresentationCarriesRefreshAndNotificationEffects()
    {
        var service = new PixelSettingsChangeService();

        var setup = service.CreatePresentation(service.Apply("LaunchRamType", 1), isRunWaitVisible: false);
        var shell = service.CreatePresentation(service.Apply("UiAcrylic", true), isRunWaitVisible: false);
        var notification = service.CreatePresentation(
            service.Apply("SystemDisableHardwareAcceleration", true),
            isRunWaitVisible: false);

        Assert.IsTrue(setup.RefreshSetupRightPage);
        Assert.IsFalse(setup.RefreshShellTheme);
        Assert.IsFalse(shell.RefreshSetupRightPage);
        Assert.IsTrue(shell.RefreshShellTheme);
        Assert.AreEqual(PixelSettingNotificationKind.Hint, notification.Notification?.Kind);
    }

    private sealed class InMemoryRendererHintState : IPixelRendererHintState
    {
        public bool RendererWarningShown { get; set; }
    }
}
