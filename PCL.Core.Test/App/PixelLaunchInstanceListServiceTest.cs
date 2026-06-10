using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchInstanceListServiceTest
{
    [TestMethod]
    public void GetGroupsFiltersByFolderAndBuildsUiReadySnapshots()
    {
        var service = new PixelLaunchInstanceListService();
        var selected = CreateInstance("1.20.1", "/minecraft-a", "/minecraft-a/versions/1.20.1", "release", new DateTime(2023, 6, 7));
        var snapshot = CreateInstance("23w13a", "/minecraft-a", "/minecraft-a/versions/23w13a", "snapshot", new DateTime(2023, 3, 29));
        var hiddenFolder = CreateInstance("1.19.4", "/minecraft-b", "/minecraft-b/versions/1.19.4", "release", new DateTime(2023, 3, 14));

        var groups = service.GetGroups([snapshot, hiddenFolder, selected], "/minecraft-a", selected.VersionDirectory);
        var page = service.GetSelectionPageSnapshot([snapshot, hiddenFolder, selected], "/minecraft-a", selected.VersionDirectory);

        Assert.AreEqual(2, groups.Count);
        Assert.IsFalse(page.IsEmpty);
        Assert.AreEqual(2, page.Groups.Count);
        Assert.AreEqual("正式版", groups[0].Title);
        Assert.AreEqual("快照版", groups[1].Title);
        Assert.AreEqual("1.20.1", groups[0].Instances.Single().Name);
        Assert.AreEqual("mdi-cube-outline", groups[0].Instances.Single().Icon);
        Assert.IsTrue(groups[0].Instances.Single().IsSelected);
        Assert.IsTrue(groups[0].Instances.Single().Info.Contains("1.20.1 · 2023/06/07 · /minecraft-a/versions/1.20.1", StringComparison.Ordinal));
        Assert.AreEqual("mdi-flask-outline", groups[1].Instances.Single().Icon);
        Assert.IsFalse(groups.SelectMany(group => group.Instances).Any(item => item.Name == "1.19.4"));
    }

    [TestMethod]
    public void GetGroupsOrdersReleaseFirstThenTypeAndReleaseTime()
    {
        var service = new PixelLaunchInstanceListService();
        var oldAlpha = CreateInstance("a1.2.6", "/minecraft", "/minecraft/versions/a1.2.6", "old_alpha", new DateTime(2010, 12, 3));
        var newerRelease = CreateInstance("1.20.1", "/minecraft", "/minecraft/versions/1.20.1", "release", new DateTime(2023, 6, 7));
        var olderRelease = CreateInstance("1.19.4", "/minecraft", "/minecraft/versions/1.19.4", "release", new DateTime(2023, 3, 14));

        var groups = service.GetGroups([oldAlpha, olderRelease, newerRelease], "", "");

        Assert.AreEqual("release", groups[0].Type);
        CollectionAssert.AreEqual(new[] { "1.20.1", "1.19.4" }, groups[0].Instances.Select(item => item.Name).ToArray());
        Assert.AreEqual("old_alpha", groups[1].Type);
        Assert.AreEqual("mdi-archive-outline", groups[1].Instances.Single().Icon);
    }

    [TestMethod]
    public void MissingTypeDefaultsToRelease()
    {
        var service = new PixelLaunchInstanceListService();
        var instance = CreateInstance("custom", "/minecraft", "/minecraft/versions/custom", null, DateTime.MinValue);

        var group = service.GetGroups([instance], "", "").Single();

        Assert.AreEqual("release", group.Type);
        Assert.AreEqual("正式版", group.Title);
        Assert.AreEqual("release · /minecraft/versions/custom", group.Instances.Single().Info);
    }

    [TestMethod]
    public void SelectionPageSnapshotExposesEmptyState()
    {
        var service = new PixelLaunchInstanceListService();

        var page = service.GetSelectionPageSnapshot([], "/minecraft", "");

        Assert.IsTrue(page.IsEmpty);
        Assert.AreEqual(0, page.Groups.Count);
    }

    private static MinecraftInstanceInfo CreateInstance(
        string name,
        string minecraftFolder,
        string versionDirectory,
        string? type,
        DateTime releaseTime)
    {
        var json = new JsonObject();
        if (type is not null)
            json["type"] = type;

        return new MinecraftInstanceInfo(
            name,
            minecraftFolder,
            versionDirectory,
            versionDirectory + "/" + name + ".json",
            json,
            releaseTime,
            MinecraftVersionNumber.TryParse(name));
    }

    public static MinecraftInstanceInfo CreateInstanceForSidebarTest() =>
        CreateInstance("1.20.1", "/minecraft", "/minecraft/versions/1.20.1", "release", new DateTime(2023, 6, 7));
}
