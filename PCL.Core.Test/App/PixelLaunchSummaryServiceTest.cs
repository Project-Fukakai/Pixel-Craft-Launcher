using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchSummaryServiceTest
{
    [TestMethod]
    public void GetSummaryBuildsUiReadyProfileAndInstanceText()
    {
        var service = new PixelLaunchSummaryService();
        var instance = PixelLaunchInstanceListServiceTest.CreateInstanceForSidebarTest();
        var profile = new MinecraftProfile
        {
            Id = "profile",
            Type = MinecraftProfileType.AuthlibInjector,
            Username = "Alex",
            Uuid = "uuid",
            ServerName = "LittleSkin"
        };

        var snapshot = service.GetSummary(instance, instance.Name, profile, canLaunch: true);

        Assert.AreEqual("Alex", snapshot.ProfileName);
        Assert.AreEqual("LittleSkin", snapshot.ProfileMethod);
        Assert.AreEqual(instance.Name, snapshot.InstanceName);
        Assert.AreEqual(instance.VersionDirectory, snapshot.InstancePath);
        Assert.IsTrue(snapshot.HasProfile);
        Assert.IsTrue(snapshot.HasInstance);
        Assert.IsTrue(snapshot.CanLaunch);
    }

    [TestMethod]
    public void GetSummaryBuildsEmptyStateWhenProfileOrInstanceMissing()
    {
        var service = new PixelLaunchSummaryService();

        var snapshot = service.GetSummary(null, "未找到可用的游戏实例", null, canLaunch: false);

        Assert.AreEqual("未选择档案", snapshot.ProfileName);
        Assert.AreEqual("未选择档案", snapshot.ProfileMethod);
        Assert.AreEqual("未找到可用的游戏实例", snapshot.InstanceName);
        Assert.IsNull(snapshot.InstancePath);
        Assert.IsFalse(snapshot.HasProfile);
        Assert.IsFalse(snapshot.HasInstance);
        Assert.IsFalse(snapshot.CanLaunch);
    }
}
