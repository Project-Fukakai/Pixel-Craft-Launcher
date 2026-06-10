using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelInstanceViewModelTest
{
    [TestMethod]
    public void OpenFolderCreatesFolderAndDelegatesToExternalProcess()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PixelInstanceViewModel", Guid.NewGuid().ToString("N"));
        var process = new RecordingExternalProcessService();
        var viewModel = new PixelInstanceViewModel(new MinecraftInstanceService(), process);

        viewModel.OpenFolder(folder);

        Assert.IsTrue(Directory.Exists(folder));
        Assert.AreEqual(folder, process.OpenedPath);
    }

    [TestMethod]
    public void OpenChildFolderCombinesInstanceDirectoryInCore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PixelInstanceViewModel", Guid.NewGuid().ToString("N"));
        var process = new RecordingExternalProcessService();
        var viewModel = new PixelInstanceViewModel(new MinecraftInstanceService(), process);

        viewModel.OpenChildFolder(folder, "mods");

        var expected = Path.Combine(folder, "mods");
        Assert.IsTrue(Directory.Exists(expected));
        Assert.AreEqual(expected, process.OpenedPath);
    }

    [TestMethod]
    public void ManagementSnapshotExposesItemsAndSelectedActionState()
    {
        var process = new RecordingExternalProcessService();
        var viewModel = new PixelInstanceViewModel(new MinecraftInstanceService(), process);
        var normal = CreateSummary("Normal", "/game/normal", hidden: false);
        var hidden = CreateSummary("Hidden", "/game/hidden", hidden: true);
        viewModel.Instances.Add(normal);
        viewModel.Instances.Add(hidden);
        viewModel.SelectInstanceByPath(hidden.VersionDirectory);

        var snapshot = viewModel.GetManagementSnapshot(12);

        Assert.IsFalse(snapshot.IsEmpty);
        Assert.IsTrue(snapshot.CanOpenSelectedChildFolders);
        Assert.AreEqual(2, snapshot.Instances.Count);
        Assert.AreEqual("mdi-cube-outline", snapshot.Instances[0].Icon);
        Assert.IsFalse(snapshot.Instances[0].IsSelected);
        Assert.AreEqual("mdi-eye-off-outline", snapshot.Instances[1].Icon);
        Assert.IsTrue(snapshot.Instances[1].IsSelected);
    }

    [TestMethod]
    public void ManagementSnapshotHandlesEmptyList()
    {
        var viewModel = new PixelInstanceViewModel(new MinecraftInstanceService(), new RecordingExternalProcessService());

        var snapshot = viewModel.GetManagementSnapshot(12);

        Assert.IsTrue(snapshot.IsEmpty);
        Assert.IsFalse(snapshot.CanOpenSelectedChildFolders);
        Assert.AreEqual(0, snapshot.Instances.Count);
        Assert.AreEqual("未找到实例，安装完成后会自动刷新。", snapshot.EmptyText);
        Assert.AreEqual("刷新实例", snapshot.RefreshButtonText);
        Assert.AreEqual("打开 Mods", snapshot.OpenModsButtonText);
        Assert.AreEqual("打开存档", snapshot.OpenSavesButtonText);
        Assert.AreEqual("打开实例文件夹", snapshot.OpenInstanceFolderTip);
    }

    private static MinecraftInstanceSummary CreateSummary(string name, string versionDirectory, bool hidden) =>
        new(
            name,
            "/game",
            versionDirectory,
            Path.Combine(versionDirectory, name + ".json"),
            "release",
            new DateTime(2026, 1, 1),
            false,
            hidden);

    private sealed class RecordingExternalProcessService : IExternalProcessService
    {
        public string? OpenedPath { get; private set; }

        public void OpenPath(string path)
        {
            OpenedPath = path;
        }

        public void OpenUrl(string url)
        {
        }
    }
}
