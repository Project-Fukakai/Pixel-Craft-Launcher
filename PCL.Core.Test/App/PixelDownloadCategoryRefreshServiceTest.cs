using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Download;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelDownloadCategoryRefreshServiceTest
{
    [TestMethod]
    public void VersionCategoriesRefreshVersionManifest()
    {
        var service = new PixelDownloadCategoryRefreshService();

        Assert.AreEqual(PixelDownloadCategoryRefreshAction.RefreshVersions, service.GetRefreshAction(1, false));
        Assert.AreEqual(PixelDownloadCategoryRefreshAction.RefreshVersions, service.GetRefreshAction(9, true));
    }

    [TestMethod]
    public void LoaderCategoriesRefreshLoadersWhenVersionIsSelected()
    {
        var service = new PixelDownloadCategoryRefreshService();

        Assert.AreEqual(PixelDownloadCategoryRefreshAction.RefreshVersions, service.GetRefreshAction(10, false));
        Assert.AreEqual(PixelDownloadCategoryRefreshAction.RefreshLoaderChoices, service.GetRefreshAction(10, true));
        Assert.AreEqual(PixelDownloadCategoryRefreshAction.RefreshLoaderChoices, service.GetRefreshAction(18, true));
    }

    [TestMethod]
    public void UnknownCategoriesDoNotRefresh()
    {
        var service = new PixelDownloadCategoryRefreshService();

        Assert.AreEqual(PixelDownloadCategoryRefreshAction.None, service.GetRefreshAction(0, false));
        Assert.AreEqual(PixelDownloadCategoryRefreshAction.None, service.GetRefreshAction(19, true));
    }
}
