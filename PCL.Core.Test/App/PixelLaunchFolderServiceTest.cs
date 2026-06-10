using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchFolderServiceTest
{
    private string _selectedFolder = string.Empty;
    private string _folders = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _selectedFolder = States.Game.SelectedFolder;
        _folders = States.Game.Folders;
        States.Game.SelectedFolder = string.Empty;
        States.Game.Folders = string.Empty;
    }

    [TestCleanup]
    public void Cleanup()
    {
        States.Game.SelectedFolder = _selectedFolder;
        States.Game.Folders = _folders;
    }

    [TestMethod]
    public void EnsureSelectedFolderKeepsKnownSelection()
    {
        var service = new PixelLaunchFolderService();
        var folder = CreateFolderInfo("known");
        service.SelectedFolder = folder.Path;

        service.EnsureSelectedFolder([folder, CreateFolderInfo("other")]);

        Assert.AreEqual(folder.Path, service.SelectedFolder);
    }

    [TestMethod]
    public void EnsureSelectedFolderFallsBackToFirstFolder()
    {
        var service = new PixelLaunchFolderService();
        var folder = CreateFolderInfo("first");
        service.SelectedFolder = "/missing";

        service.EnsureSelectedFolder([folder]);

        Assert.AreEqual(folder.Path, service.SelectedFolder);
    }

    [TestMethod]
    public void AddCustomFolderNormalizesAndDeduplicates()
    {
        var service = new PixelLaunchFolderService();
        var path = Path.Combine(Path.GetTempPath(), "pcl-pixel-folder", Guid.NewGuid().ToString("N"));

        var first = service.AddCustomFolder(path);
        var second = service.AddCustomFolder(path);

        Assert.AreEqual(first, second);
        Assert.AreEqual(1, service.GetCustomFolders().Count);
        Assert.AreEqual(Path.GetFullPath(path), service.GetCustomFolders()[0]);
    }

    [TestMethod]
    public void FolderTitleUsesCustomFolderName()
    {
        var service = new PixelLaunchFolderService();
        var folder = CreateFolderInfo("MyMinecraft");

        Assert.AreEqual("MyMinecraft", service.GetFolderTitle(folder));
    }

    [TestMethod]
    public void FolderSnapshotsContainDisplayState()
    {
        var service = new PixelLaunchFolderService();
        var first = CreateFolderInfo("First");
        var second = CreateFolderInfo("Second");
        service.SelectedFolder = second.Path;

        var snapshots = service.GetFolderSnapshots([first, second]);

        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual("First", snapshots[0].Title);
        Assert.AreEqual(first.Path, snapshots[0].Path);
        Assert.AreEqual("mdi-folder-outline", snapshots[0].Icon);
        Assert.IsFalse(snapshots[0].IsSelected);
        Assert.IsTrue(snapshots[1].IsSelected);
    }

    private static MinecraftFolderInfo CreateFolderInfo(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-pixel-folder", name);
        return new MinecraftFolderInfo(Path.GetFullPath(path), IsDefault: false, IsCustom: true, InstanceCount: 0);
    }
}
