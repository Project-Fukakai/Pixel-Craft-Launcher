using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Download;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLoaderSelectionServiceTest
{
    [TestMethod]
    public void SelectLoaderVersionAutoSelectsCompatibleAddon()
    {
        var service = new PixelLoaderSelectionService();
        var fabric = CreateLoader(MinecraftLoaderKind.Fabric, "0.16.14");
        var fabricApi = CreateAddon(MinecraftAddonKind.FabricApi);
        PixelLoaderChoiceGroup[] groups =
        [
            new("Fabric API", "", "", null, MinecraftAddonKind.FabricApi, [], [fabricApi], "可以添加", true)
        ];

        var state = service.SelectLoaderVersion(new MinecraftMergedLoaderSelection(), fabric, "1.21.8", groups);

        Assert.AreEqual(fabric, state.Selection.Fabric);
        Assert.AreEqual(fabricApi, state.Selection.FabricApi);
        Assert.AreEqual(MinecraftLoaderKind.Fabric, state.SelectedLoaderKind);
        Assert.AreEqual("0.16.14", state.SelectedLoaderVersion);
        StringAssert.Contains(state.InstanceName, "Fabric");
    }

    [TestMethod]
    public void ClearLoaderRemovesDependentAddons()
    {
        var service = new PixelLoaderSelectionService();
        var fabric = CreateLoader(MinecraftLoaderKind.Fabric, "0.16.14");
        var fabricApi = CreateAddon(MinecraftAddonKind.FabricApi);
        var current = new MinecraftMergedLoaderSelection(Fabric: fabric, FabricApi: fabricApi);

        var state = service.ClearLoader(current, MinecraftLoaderKind.Fabric, "1.21.8");

        Assert.IsNull(state.Selection.Fabric);
        Assert.IsNull(state.Selection.FabricApi);
        Assert.AreEqual(MinecraftLoaderKind.Vanilla, state.SelectedLoaderKind);
        Assert.AreEqual(string.Empty, state.SelectedLoaderVersion);
        Assert.AreEqual("1.21.8", state.InstanceName);
    }

    private static MinecraftLoaderVersionEntry CreateLoader(MinecraftLoaderKind kind, string version)
    {
        return new MinecraftLoaderVersionEntry(
            kind,
            version,
            version,
            "1.21.8",
            true,
            true,
            MinecraftRemoteSource.Official);
    }

    private static MinecraftAddonFileEntry CreateAddon(MinecraftAddonKind kind)
    {
        return new MinecraftAddonFileEntry(
            kind,
            kind.ToString(),
            kind.ToString(),
            kind + ".jar",
            ["https://example.invalid/" + kind + ".jar"],
            ["1.21.8"],
            [MinecraftLoaderKind.Fabric],
            null,
            null,
            true,
            DateTime.UnixEpoch,
            MinecraftRemoteSource.Generated);
    }
}
