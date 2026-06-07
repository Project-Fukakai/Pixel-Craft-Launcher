using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;

namespace PCL.Core.Test.App;

[TestClass]
public class LauncherSettingsServiceTest
{
    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
    }

    [TestMethod]
    public void DebugSkipCopyRequiresDebugMode()
    {
        Config.Debug.Enabled = false;
        Config.Debug.DontCopy = true;
        Assert.IsFalse(DebugSettingsService.ShouldSkipCopy("a", "b"));

        Config.Debug.Enabled = true;
        Config.Debug.DontCopy = true;
        Assert.IsTrue(DebugSettingsService.ShouldSkipCopy("a", "b"));
    }

    [TestMethod]
    public void DebugAnimationScaleMapsDefaultAndDisabled()
    {
        Assert.AreEqual(1d, DebugSettingsService.ResolveAnimationScale(9));
        Assert.IsGreaterThan(100d, DebugSettingsService.ResolveAnimationScale(0));
        Assert.IsLessThan(1d, DebugSettingsService.ResolveAnimationScale(20));
    }

    [TestMethod]
    public void ProxySettingsApplyCustomProxy()
    {
        Config.Network.HttpProxy.Type = (int)HttpProxyManager.ProxyMode.CustomProxy;
        Config.Network.HttpProxy.CustomAddress = "127.0.0.1:8080";
        Config.Network.HttpProxy.CustomUsername = "user";
        Config.Network.HttpProxy.CustomPassword = "pass";

        LauncherSettingsService.ApplyProxy();

        var proxy = HttpProxyManager.Instance.GetProxy(new Uri("http://example.com"));
        Assert.AreEqual(new Uri("http://127.0.0.1:8080/"), proxy);
        Assert.IsInstanceOfType<NetworkCredential>(HttpProxyManager.Instance.Credentials);
    }

    [TestMethod]
    public void NetworkReloadCreatesClientAfterDohChange()
    {
        Config.Network.EnableDoH = true;
        NetworkService.ReloadDefaultClientFactory();
        using var dohClient = NetworkService.GetClient();

        Config.Network.EnableDoH = false;
        NetworkService.ReloadDefaultClientFactory();
        using var systemClient = NetworkService.GetClient();

        Assert.IsNotNull(dohClient);
        Assert.IsNotNull(systemClient);
    }

    [TestMethod]
    public void RestrictedFeatureCanBeAllowedByDebugConfig()
    {
        Config.Debug.AllowRestrictedFeature = true;

        Assert.IsTrue(RegionUtils.IsRestrictedFeatAllowed);
    }
}
