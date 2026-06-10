using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelPersonalizationServiceTest
{
    private object? _oldSuit;
    private object? _oldOpacity;
    private object? _oldBlur;
    private object? _oldAutoPauseVideo;
    private object? _oldBackgroundFolder;
    private object? _oldMusicVolume;
    private object? _oldMusicRandom;
    private object? _oldMusicAuto;
    private object? _oldMusicStart;
    private object? _oldMusicStop;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _oldSuit = PixelSettingsBinder.LoadValue("UiBackgroundSuit");
        _oldOpacity = PixelSettingsBinder.LoadValue("UiBackgroundOpacity");
        _oldBlur = PixelSettingsBinder.LoadValue("UiBackgroundBlur");
        _oldAutoPauseVideo = PixelSettingsBinder.LoadValue("UiAutoPauseVideo");
        _oldBackgroundFolder = PixelSettingsBinder.LoadValue("UiBackgroundFolder");
        _oldMusicVolume = PixelSettingsBinder.LoadValue("UiMusicVolume");
        _oldMusicRandom = PixelSettingsBinder.LoadValue("UiMusicRandom");
        _oldMusicAuto = PixelSettingsBinder.LoadValue("UiMusicAuto");
        _oldMusicStart = PixelSettingsBinder.LoadValue("UiMusicStart");
        _oldMusicStop = PixelSettingsBinder.LoadValue("UiMusicStop");
        ColorSchemeService.SetBackgroundSeed(null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_oldSuit is not null)
            PixelSettingsBinder.SetValue("UiBackgroundSuit", _oldSuit);
        if (_oldOpacity is not null)
            PixelSettingsBinder.SetValue("UiBackgroundOpacity", _oldOpacity);
        if (_oldBlur is not null)
            PixelSettingsBinder.SetValue("UiBackgroundBlur", _oldBlur);
        if (_oldAutoPauseVideo is not null)
            PixelSettingsBinder.SetValue("UiAutoPauseVideo", _oldAutoPauseVideo);
        if (_oldBackgroundFolder is not null)
            PixelSettingsBinder.SetValue("UiBackgroundFolder", _oldBackgroundFolder);
        if (_oldMusicVolume is not null)
            PixelSettingsBinder.SetValue("UiMusicVolume", _oldMusicVolume);
        if (_oldMusicRandom is not null)
            PixelSettingsBinder.SetValue("UiMusicRandom", _oldMusicRandom);
        if (_oldMusicAuto is not null)
            PixelSettingsBinder.SetValue("UiMusicAuto", _oldMusicAuto);
        if (_oldMusicStart is not null)
            PixelSettingsBinder.SetValue("UiMusicStart", _oldMusicStart);
        if (_oldMusicStop is not null)
            PixelSettingsBinder.SetValue("UiMusicStop", _oldMusicStop);
        ColorSchemeService.SetBackgroundSeed(null);
    }

    [TestMethod]
    public async Task BackgroundSeedIsClearedWhenNoBackgroundSelected()
    {
        ColorSchemeService.SetBackgroundSeed(0xFF123456);
        var service = new PixelPersonalizationService();

        var result = await service.ApplyBackgroundSeedAsync(null);

        Assert.IsNull(result.ImagePath);
        Assert.IsNull(result.Seed);
        Assert.IsNull(ColorSchemeService.BackgroundSeed);
    }

    [TestMethod]
    public async Task BackgroundSeedIsClearedForVideoBackground()
    {
        ColorSchemeService.SetBackgroundSeed(0xFF123456);
        var service = new PixelPersonalizationService();

        var result = await service.ApplyBackgroundSeedAsync(
            new PixelPersonalizationMedia("/tmp/background.mp4", PixelPersonalizationMediaKind.Video));

        Assert.IsNull(result.ImagePath);
        Assert.IsNull(result.Seed);
        Assert.IsNull(ColorSchemeService.BackgroundSeed);
    }

    [TestMethod]
    public async Task InvalidImageKeepsImagePathButClearsSeed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationSeed", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "invalid.png");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(path, "not an image");
            ColorSchemeService.SetBackgroundSeed(0xFF123456);
            var service = new PixelPersonalizationService();

            var result = await service.ApplyBackgroundSeedAsync(
                new PixelPersonalizationMedia(path, PixelPersonalizationMediaKind.Image));

            Assert.AreEqual(path, result.ImagePath);
            Assert.IsNull(result.Seed);
            Assert.IsNull(ColorSchemeService.BackgroundSeed);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void OpenBackgroundFolderFailedMessageComesFromCore()
    {
        var service = new PixelPersonalizationService();
        var messages = service.GetMessages();
        var exception = new InvalidOperationException("boom");

        Assert.AreEqual("打开背景目录失败：boom", messages.GetOpenBackgroundFolderFailedMessage(exception));
        Assert.AreEqual(
            messages.GetOpenBackgroundFolderFailedMessage(exception),
            PixelPersonalizationService.GetOpenBackgroundFolderFailedMessage(exception));
    }

    [TestMethod]
    public void BackgroundRenderSettingsComeFromCore()
    {
        PixelSettingsBinder.SetValue("UiBackgroundSuit", 8);
        PixelSettingsBinder.SetValue("UiBackgroundOpacity", 1250);
        PixelSettingsBinder.SetValue("UiBackgroundBlur", 12);
        PixelSettingsBinder.SetValue("UiAutoPauseVideo", false);
        var service = new PixelPersonalizationService();

        var settings = service.GetBackgroundRenderSettings();

        Assert.AreEqual(PixelPersonalizationBackgroundStretch.UniformToFill, settings.Stretch);
        Assert.AreEqual(PixelPersonalizationBackgroundHorizontalAlignment.Right, settings.HorizontalAlignment);
        Assert.AreEqual(PixelPersonalizationBackgroundVerticalAlignment.Bottom, settings.VerticalAlignment);
        Assert.AreEqual(1d, settings.Opacity);
        Assert.AreEqual(12, settings.BlurRadius);
        Assert.IsFalse(settings.AutoPauseVideo);
    }

    [TestMethod]
    public async Task BackgroundPresentationDescribesEmptyLibraryForPlatformBridge()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationEmpty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            var service = new PixelPersonalizationService();
            service.ReloadLibrary();

            var presentation = await service.AdvanceBackgroundPresentationAsync();

            Assert.AreEqual(PixelPersonalizationBackgroundContentKind.None, presentation.ContentKind);
            Assert.IsNull(presentation.MediaPath);
            Assert.IsNull(presentation.BackgroundImagePath);
            Assert.IsNull(presentation.SeedUpdate.ImagePath);
            Assert.IsNull(presentation.SeedUpdate.Seed);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundPresentationDescribesImageAndVideoForPlatformBridge()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationPresentation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "a.png");
        var videoPath = Path.Combine(folder, "b.mp4");
        try
        {
            File.WriteAllText(imagePath, "not an image");
            File.WriteAllText(videoPath, "not a video");
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            PixelSettingsBinder.SetValue("UiBackgroundOpacity", 640);
            var service = new PixelPersonalizationService();
            service.ReloadLibrary();

            var image = await service.AdvanceBackgroundPresentationAsync();
            var video = await service.AdvanceBackgroundPresentationAsync();

            Assert.AreEqual(PixelPersonalizationBackgroundContentKind.Image, image.ContentKind);
            Assert.AreEqual(imagePath, image.MediaPath);
            Assert.AreEqual(imagePath, image.BackgroundImagePath);
            Assert.AreEqual(imagePath, image.SeedUpdate.ImagePath);
            Assert.AreEqual(0.64d, image.RenderSettings.Opacity);

            Assert.AreEqual(PixelPersonalizationBackgroundContentKind.Video, video.ContentKind);
            Assert.AreEqual(videoPath, video.MediaPath);
            Assert.IsNull(video.BackgroundImagePath);
            Assert.IsNull(video.SeedUpdate.ImagePath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void AudioVolumeIsClampedByCorePolicy()
    {
        var service = new PixelPersonalizationService();

        PixelSettingsBinder.SetValue("UiMusicVolume", 1500);
        Assert.AreEqual(100, service.GetAudioVolume());

        PixelSettingsBinder.SetValue("UiMusicVolume", -50);
        Assert.AreEqual(0, service.GetAudioVolume());
    }

    [TestMethod]
    public void StartupPlanHonorsAutoMusicAndAudioLibrary()
    {
        var folder = CreateTempPersonalizationFolder(out var audioPath);
        try
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            PixelSettingsBinder.SetValue("UiMusicRandom", false);
            PixelSettingsBinder.SetValue("UiMusicAuto", true);
            var service = new PixelPersonalizationService();
            service.ReloadLibrary();

            Assert.AreEqual(audioPath, service.CreateStartupPlan().PlayAudioPath);

            PixelSettingsBinder.SetValue("UiMusicAuto", false);
            Assert.IsNull(service.CreateStartupPlan().PlayAudioPath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void PlaybackPlanForGameRunningUsesCoreSettings()
    {
        var folder = CreateTempPersonalizationFolder(out var audioPath);
        try
        {
            PixelSettingsBinder.SetValue("UiBackgroundFolder", folder);
            PixelSettingsBinder.SetValue("UiMusicRandom", false);
            PixelSettingsBinder.SetValue("UiMusicAuto", true);
            PixelSettingsBinder.SetValue("UiMusicStart", true);
            PixelSettingsBinder.SetValue("UiMusicStop", true);
            PixelSettingsBinder.SetValue("UiAutoPauseVideo", true);
            var service = new PixelPersonalizationService();
            service.ReloadLibrary();

            var running = service.SetGameRunning(true);

            Assert.IsTrue(running.PauseVideo);
            Assert.IsFalse(running.PlayVideo);
            Assert.IsTrue(running.PauseAudio);
            Assert.AreEqual(audioPath, running.PlayAudioPath);
            Assert.AreEqual(PixelPersonalizationPlaybackPlan.None, service.SetGameRunning(true));

            var stopped = service.SetGameRunning(false);

            Assert.IsFalse(stopped.PauseVideo);
            Assert.IsTrue(stopped.PlayVideo);
            Assert.IsFalse(stopped.PauseAudio);
            Assert.AreEqual(audioPath, stopped.PlayAudioPath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string CreateTempPersonalizationFolder(out string audioPath)
    {
        var folder = Path.Combine(Path.GetTempPath(), "PCLTest", "PersonalizationPlayback", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        audioPath = Path.Combine(folder, "track.mp3");
        File.WriteAllText(audioPath, "not real audio");
        return folder;
    }
}
