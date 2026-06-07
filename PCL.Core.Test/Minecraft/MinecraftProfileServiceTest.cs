using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class MinecraftProfileServiceTest
{
    [TestMethod]
    public async Task OfflineProfileIsSavedAndSelected()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profile-test-" + Guid.NewGuid().ToString("N"), "profiles.json");
        var service = new MinecraftProfileService(profilePath: path);

        var profile = await service.AddOfflineProfileAsync("Alex", OfflineUuidMode.Standard);

        Assert.AreEqual("Alex", profile.Username);
        Assert.AreSame(profile, service.SelectedProfile);
        Assert.AreEqual(1, service.Profiles.Count);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task SensitiveFieldsAreEncryptedOnDiskAndReadable()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profile-test-" + Guid.NewGuid().ToString("N"), "profiles.json");
        var service = new MinecraftProfileService(profilePath: path);
        await service.InitializeAsync();
        var profile = new MinecraftProfile
        {
            Type = MinecraftProfileType.AuthlibInjector,
            Username = "Player",
            Uuid = MinecraftOfflineUuidHelper.Create("Player", OfflineUuidMode.Standard),
            Server = "https://example.com/api/yggdrasil/authserver",
            LoginName = "mail@example.com",
            Password = "secret-password",
            AccessToken = "access-token",
            ClientToken = "client-token"
        };
        service.Profiles.Add(profile);
        await service.SelectProfileAsync(profile);

        var raw = File.ReadAllText(path);
        Assert.IsFalse(raw.Contains("secret-password", StringComparison.Ordinal));
        Assert.IsFalse(raw.Contains("access-token", StringComparison.Ordinal));

        var reloaded = new MinecraftProfileService(profilePath: path);
        await reloaded.InitializeAsync();
        Assert.AreEqual("secret-password", reloaded.Profiles[0].Password);
        Assert.AreEqual("access-token", reloaded.Profiles[0].AccessToken);
    }

    [TestMethod]
    public void OfflineUuidModesAreStable()
    {
        Assert.AreEqual("36532b5e-c442-3dbb-a24c-c7e55d0f979a".Replace("-", ""), MinecraftOfflineUuidHelper.Create("Alex", OfflineUuidMode.Standard));
        Assert.AreEqual(MinecraftOfflineUuidHelper.Create("Alex", OfflineUuidMode.Legacy), MinecraftOfflineUuidHelper.Create("Alex", OfflineUuidMode.Legacy));
        Assert.AreEqual("0123456789abcdef0123456789abcdef", MinecraftOfflineUuidHelper.Create("Alex", OfflineUuidMode.Custom, "01234567-89ab-cdef-0123-456789abcdef"));
    }

    [TestMethod]
    public async Task AuthServerPresetIsPersisted()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profile-test-" + Guid.NewGuid().ToString("N"), "profiles.json");
        var service = new MinecraftProfileService(profilePath: path);
        await service.AddAuthServerAsync("Example", "https://example.com/api/yggdrasil/", "https://example.com/register");

        var json = JsonNode.Parse(File.ReadAllText(path));
        var servers = json?["authServers"]?.AsArray();
        Assert.IsTrue(servers?.Count >= 1);
        Assert.IsTrue(servers!.Any(node => node?["apiRoot"]?.GetValue<string>() == "https://example.com/api/yggdrasil"));
    }
}
