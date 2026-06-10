using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Slices.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelMemoryPreviewServiceTest
{
    private int _memoryMode;
    private int _customMemory;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _memoryMode = Config.Launch.MemoryAllocationMode;
        _customMemory = Config.Launch.CustomMemorySize;
    }

    [TestCleanup]
    public void Cleanup()
    {
        Config.Launch.MemoryAllocationMode = _memoryMode;
        Config.Launch.CustomMemorySize = _customMemory;
    }

    [TestMethod]
    public void CreateSnapshotCalculatesUsedActualAndFreeMemory()
    {
        var snapshot = PixelMemoryPreviewService.CreateSnapshot(totalGb: 16, availableGb: 6, gameGb: 8);

        Assert.AreEqual(10, snapshot.UsedGb);
        Assert.AreEqual(8, snapshot.GameGb);
        Assert.AreEqual(6, snapshot.GameActualGb);
        Assert.AreEqual(0, snapshot.FreeAfterLaunchGb);

        var text = PixelMemoryPreviewService.CreateTextSnapshot(snapshot);

        Assert.AreEqual("总内存 16 GB", text.TotalText);
        Assert.AreEqual("已用 10 GB", text.UsedText);
        Assert.AreEqual("游戏预估 8 GB", text.GameText);
        Assert.AreEqual("启动后空闲 0 GB", text.FreeText);
        Assert.AreEqual("当前可用内存只有 6 GB，游戏可实际分配约 6 GB。", text.WarningText);
        Assert.IsTrue(text.HasWarning);
    }

    [TestMethod]
    public void TextSnapshotHidesWarningWhenGameMemoryFits()
    {
        var snapshot = PixelMemoryPreviewService.CreateSnapshot(totalGb: 16, availableGb: 10, gameGb: 4);

        var text = PixelMemoryPreviewService.CreateTextSnapshot(snapshot);

        Assert.AreEqual(string.Empty, text.WarningText);
        Assert.IsFalse(text.HasWarning);
    }

    [TestMethod]
    public void EstimateGameMemoryUsesCustomMemoryScale()
    {
        Config.Launch.MemoryAllocationMode = 1;
        Config.Launch.CustomMemorySize = 25;

        var estimate = PixelMemoryPreviewService.EstimateGameMemoryGb(availableGb: 12);

        Assert.AreEqual(8d, estimate);
    }
}
