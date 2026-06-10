using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelProfileCommandsTest
{
    [TestMethod]
    public async Task SelectProfileByIdSelectsMatchingProfileAndPublishesEvent()
    {
        var service = CreateProfileService();
        var alex = await service.AddOfflineProfileAsync("Alex", OfflineUuidMode.Standard);
        var steve = await service.AddOfflineProfileAsync("Steve", OfflineUuidMode.Standard);
        using var eventBus = new ReactivePixelEventBus();
        MinecraftProfileSelectedEvent? selected = null;
        using var subscription = eventBus.Observe<MinecraftProfileSelectedEvent>().Subscribe(evt => selected = evt);
        var handler = new SelectMinecraftProfileByIdCommandHandler(service, eventBus);

        await handler.Handle(new SelectMinecraftProfileByIdCommand(alex.Id), CancellationToken.None);

        Assert.AreSame(alex, service.SelectedProfile);
        Assert.AreEqual(alex.Id, selected?.Profile.Id);
        Assert.IsTrue(selected?.Profile.IsSelected);
        Assert.AreNotSame(steve, service.SelectedProfile);
    }

    [TestMethod]
    public async Task SaveOfflineProfileByIdCreatesAndUpdatesProfiles()
    {
        var service = CreateProfileService();
        using var eventBus = new ReactivePixelEventBus();
        MinecraftProfileSavedEvent? saved = null;
        using var subscription = eventBus.Observe<MinecraftProfileSavedEvent>().Subscribe(evt => saved = evt);
        var handler = new SaveOfflineMinecraftProfileByIdCommandHandler(service, eventBus);

        var created = await handler.Handle(
            new SaveOfflineMinecraftProfileByIdCommand(null, "Alex", PixelOfflineUuidMode.Standard, null),
            CancellationToken.None);

        Assert.AreEqual("Alex", created.Username);
        Assert.AreEqual(created.Id, saved?.Profile.Id);
        Assert.AreEqual("Alex", saved?.Profile.Username);

        var updated = await handler.Handle(
            new SaveOfflineMinecraftProfileByIdCommand(created.Id, "Steve", PixelOfflineUuidMode.Standard, null),
            CancellationToken.None);

        Assert.AreEqual(created.Id, updated.Id);
        Assert.AreEqual("Steve", updated.Username);
        Assert.AreEqual(updated.Id, saved?.Profile.Id);
        Assert.AreEqual("Steve", saved?.Profile.Username);
    }

    [TestMethod]
    public async Task SaveOfflineProfileByIdMapsPixelUuidMode()
    {
        var service = CreateProfileService();
        using var eventBus = new ReactivePixelEventBus();
        var handler = new SaveOfflineMinecraftProfileByIdCommandHandler(service, eventBus);

        var profile = await handler.Handle(
            new SaveOfflineMinecraftProfileByIdCommand(
                null,
                "Alex",
                PixelOfflineUuidMode.Custom,
                "12345678-1234-1234-1234-1234567890ab"),
            CancellationToken.None);

        Assert.AreEqual("123456781234123412341234567890ab", profile.Uuid);
    }

    [TestMethod]
    public async Task SaveOfflineProfileByIdRejectsMissingExistingProfile()
    {
        var service = CreateProfileService();
        using var eventBus = new ReactivePixelEventBus();
        var handler = new SaveOfflineMinecraftProfileByIdCommandHandler(service, eventBus);

        try
        {
            await handler.Handle(
                new SaveOfflineMinecraftProfileByIdCommand("missing", "Alex", PixelOfflineUuidMode.Standard, null),
                CancellationToken.None);
            Assert.Fail("Expected missing profile to be rejected.");
        }
        catch (MinecraftProfileException ex)
        {
            StringAssert.Contains(ex.Message, "找不到指定档案");
        }
    }

    [TestMethod]
    public async Task RemoveProfileByIdRemovesMatchingProfileAndPublishesEvent()
    {
        var service = CreateProfileService();
        var alex = await service.AddOfflineProfileAsync("Alex", OfflineUuidMode.Standard);
        var steve = await service.AddOfflineProfileAsync("Steve", OfflineUuidMode.Standard);
        using var eventBus = new ReactivePixelEventBus();
        MinecraftProfileRemovedEvent? removed = null;
        using var subscription = eventBus.Observe<MinecraftProfileRemovedEvent>().Subscribe(evt => removed = evt);
        var handler = new RemoveMinecraftProfileByIdCommandHandler(service, eventBus);

        await handler.Handle(new RemoveMinecraftProfileByIdCommand(alex.Id), CancellationToken.None);

        Assert.AreEqual(alex.Id, removed?.Profile.Id);
        CollectionAssert.DoesNotContain(service.Profiles, alex);
        CollectionAssert.Contains(service.Profiles, steve);
    }

    [TestMethod]
    public async Task AddAuthServerPresetSavesPresetAndPublishesEvent()
    {
        var service = CreateProfileService();
        using var eventBus = new ReactivePixelEventBus();
        AuthServerPresetSavedEvent? saved = null;
        using var subscription = eventBus.Observe<AuthServerPresetSavedEvent>().Subscribe(evt => saved = evt);
        var handler = new AddAuthServerPresetCommandHandler(service, eventBus);

        var preset = await handler.Handle(
            new AddAuthServerPresetCommand(
                "LittleSkin",
                "https://littleskin.cn/api/yggdrasil",
                "https://littleskin.cn/auth/register"),
            CancellationToken.None);

        Assert.AreEqual("LittleSkin", preset.Name);
        Assert.AreEqual("https://littleskin.cn/api/yggdrasil", preset.ApiRoot);
        Assert.AreEqual(preset.Id, saved?.Preset.Id);
        Assert.AreEqual(preset.Name, saved?.Preset.Name);
        Assert.AreEqual(preset.ApiRoot, saved?.Preset.ApiRoot);
        Assert.IsTrue(service.AuthServers.Any(server => server.Id == preset.Id));
    }

    [TestMethod]
    public async Task ProfileByIdCommandRejectsMissingProfile()
    {
        var service = CreateProfileService();
        using var eventBus = new ReactivePixelEventBus();
        var handler = new SelectMinecraftProfileByIdCommandHandler(service, eventBus);

        try
        {
            await handler.Handle(new SelectMinecraftProfileByIdCommand("missing"), CancellationToken.None);
            Assert.Fail("Expected missing profile to be rejected.");
        }
        catch (MinecraftProfileException ex)
        {
            StringAssert.Contains(ex.Message, "找不到指定档案");
        }
    }

    private static MinecraftProfileService CreateProfileService()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profile-command-test-" + Guid.NewGuid().ToString("N"), "profiles.json");
        return new MinecraftProfileService(profilePath: path);
    }
}
