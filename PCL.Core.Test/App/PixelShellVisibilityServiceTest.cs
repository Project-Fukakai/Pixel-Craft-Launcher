using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelShellVisibilityServiceTest
{
    [TestMethod]
    public void MainPageVisibilityDefaultsToVisible()
    {
        var service = new PixelShellVisibilityService();

        var snapshot = service.GetMainPageVisibility();

        Assert.IsTrue(snapshot.IsVisible(MainPageKind.Launch));
        Assert.IsTrue(snapshot.IsDownloadVisible);
        Assert.IsTrue(snapshot.IsSetupVisible);
        Assert.IsTrue(snapshot.IsToolsVisible);
    }

    [TestMethod]
    public void SetupLauncherMiscSectionIsAlwaysVisible()
    {
        var service = new PixelShellVisibilityService();

        Assert.IsTrue(service.IsSetupSectionVisible(PixelSettingSectionKind.LauncherMisc));
    }

    [TestMethod]
    public void ToolsDefaultToVisible()
    {
        var service = new PixelShellVisibilityService();

        Assert.IsTrue(service.IsToolVisible(PixelToolFeature.GameLink));
        Assert.IsTrue(service.IsToolVisible(PixelToolFeature.Test));
    }

    [TestMethod]
    public void ToolHiddenMessagesComeFromShellVisibilityService()
    {
        var service = new PixelShellVisibilityService();

        var gameLink = service.GetToolHiddenMessage(PixelToolFeature.GameLink);
        var test = service.GetToolHiddenMessage(PixelToolFeature.Test);

        Assert.AreEqual("工具", gameLink.Title);
        Assert.AreEqual("联机工具入口已隐藏。", gameLink.Message);
        Assert.AreEqual("工具", test.Title);
        Assert.AreEqual("控件验收入口已隐藏。", test.Message);
    }
}
