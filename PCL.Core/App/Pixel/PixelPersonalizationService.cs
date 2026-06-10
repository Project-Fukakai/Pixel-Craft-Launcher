using System.IO;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL.Core.App.Pixel;

public sealed class PixelPersonalizationService(Random? random = null)
{
    private readonly Random _random = random ?? new Random();
    private int _backgroundIndex = -1;
    private bool _isGameRunning;

    public PixelPersonalizationLibrary Library { get; private set; } = new(DefaultBackgroundFolder, [], [], []);

    public bool IsGameRunning => _isGameRunning;

    public string BackgroundFolder => ResolveBackgroundFolder();

    public static string DefaultBackgroundFolder => Path.Combine(Paths.SharedLocalData, "Backgrounds");

    public string EnsureBackgroundFolder()
    {
        var folder = ResolveBackgroundFolder();
        Directory.CreateDirectory(folder);
        return folder;
    }

    public PixelPersonalizationLibrary ReloadLibrary()
    {
        var folder = EnsureBackgroundFolder();
        Library = PixelPersonalizationScanner.Scan(folder);
        _backgroundIndex = -1;
        return Library;
    }

    public TimeSpan? GetCarouselInterval()
    {
        var seconds = GetInt("UiBackgroundCarousel", 1000);
        if (seconds <= 0 || Library.Backgrounds.Count <= 1)
            return null;

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 3600));
    }

    public PixelPersonalizationBackgroundSelection AdvanceBackground()
    {
        var backgrounds = Library.Backgrounds;
        if (backgrounds.Count == 0)
            return new PixelPersonalizationBackgroundSelection(null);

        _backgroundIndex = (_backgroundIndex + 1) % backgrounds.Count;
        return new PixelPersonalizationBackgroundSelection(backgrounds[_backgroundIndex]);
    }

    public async Task<PixelPersonalizationBackgroundPresentation> AdvanceBackgroundPresentationAsync()
    {
        var media = AdvanceBackground().Media;
        var seed = await ApplyBackgroundSeedAsync(media).ConfigureAwait(false);
        var contentKind = media?.Kind switch
        {
            PixelPersonalizationMediaKind.Image => PixelPersonalizationBackgroundContentKind.Image,
            PixelPersonalizationMediaKind.Video => PixelPersonalizationBackgroundContentKind.Video,
            _ => PixelPersonalizationBackgroundContentKind.None
        };
        return new PixelPersonalizationBackgroundPresentation(
            contentKind,
            media?.Path,
            contentKind == PixelPersonalizationBackgroundContentKind.Image ? media?.Path : null,
            GetBackgroundRenderSettings(),
            seed);
    }

    public async Task<PixelPersonalizationBackgroundSeedUpdate> ApplyBackgroundSeedAsync(PixelPersonalizationMedia? media)
    {
        if (media?.Kind != PixelPersonalizationMediaKind.Image)
        {
            ColorSchemeService.SetBackgroundSeed(null);
            return new PixelPersonalizationBackgroundSeedUpdate(null, null);
        }

        var seed = await Task.Run(() => ImageColorExtractor.TryExtractSeed(media.Path, out var extracted)
            ? extracted
            : (uint?)null).ConfigureAwait(false);
        ColorSchemeService.SetBackgroundSeed(seed);
        return new PixelPersonalizationBackgroundSeedUpdate(media.Path, seed);
    }

    public PixelPersonalizationStartupPlan CreateStartupPlan()
    {
        return new PixelPersonalizationStartupPlan(SelectAudioPathForAutoStart());
    }

    public PixelPersonalizationPlaybackPlan SetGameRunning(bool isRunning)
    {
        if (_isGameRunning == isRunning)
            return PixelPersonalizationPlaybackPlan.None;

        _isGameRunning = isRunning;

        if (isRunning)
        {
            return new PixelPersonalizationPlaybackPlan(
                PauseVideo: GetBool("UiAutoPauseVideo", true),
                PlayVideo: false,
                PauseAudio: GetBool("UiMusicStop", false),
                PlayAudioPath: GetBool("UiMusicStart", false) ? SelectAudioPath() : null);
        }

        return new PixelPersonalizationPlaybackPlan(
            PauseVideo: false,
            PlayVideo: GetBool("UiAutoPauseVideo", true),
            PauseAudio: false,
            PlayAudioPath: GetBool("UiMusicAuto", true) ? SelectAudioPath() : null);
    }

    public string? SelectAudioPathForAutoStart()
    {
        return GetBool("UiMusicAuto", true) ? SelectAudioPath() : null;
    }

    public string? SelectAudioPath()
    {
        if (Library.Audio.Count == 0)
            return null;

        var index = GetBool("UiMusicRandom", true) ? _random.Next(Library.Audio.Count) : 0;
        return Library.Audio[index].Path;
    }

    public int GetAudioVolume()
    {
        return Math.Clamp(GetInt("UiMusicVolume", 500) / 10, 0, 100);
    }

    public PixelPersonalizationBackgroundRenderSettings GetBackgroundRenderSettings()
    {
        var suit = GetInt("UiBackgroundSuit", 0);
        var stretch = suit switch
        {
            1 or 4 => PixelPersonalizationBackgroundStretch.None,
            2 => PixelPersonalizationBackgroundStretch.Uniform,
            3 => PixelPersonalizationBackgroundStretch.Fill,
            _ => PixelPersonalizationBackgroundStretch.UniformToFill
        };
        var horizontal = suit switch
        {
            5 or 7 => PixelPersonalizationBackgroundHorizontalAlignment.Left,
            6 or 8 => PixelPersonalizationBackgroundHorizontalAlignment.Right,
            _ => PixelPersonalizationBackgroundHorizontalAlignment.Center
        };
        var vertical = suit switch
        {
            5 or 6 => PixelPersonalizationBackgroundVerticalAlignment.Top,
            7 or 8 => PixelPersonalizationBackgroundVerticalAlignment.Bottom,
            _ => PixelPersonalizationBackgroundVerticalAlignment.Center
        };
        return new PixelPersonalizationBackgroundRenderSettings(
            stretch,
            horizontal,
            vertical,
            Math.Clamp(GetInt("UiBackgroundOpacity", 1000) / 1000d, 0d, 1d),
            Math.Max(0, GetInt("UiBackgroundBlur", 0)),
            GetBool("UiAutoPauseVideo", true));
    }

    public int GetInt(string key, int fallback)
    {
        return PixelSettingsBinder.LoadValue(key) is IConvertible value ? Convert.ToInt32(value) : fallback;
    }

    public bool GetBool(string key, bool fallback)
    {
        return PixelSettingsBinder.LoadValue(key) is bool value ? value : fallback;
    }

    public PixelPersonalizationMessages GetMessages()
    {
        return new PixelPersonalizationMessages("打开背景目录失败：{0}");
    }

    public static string GetOpenBackgroundFolderFailedMessage(Exception exception) =>
        new PixelPersonalizationService().GetMessages().GetOpenBackgroundFolderFailedMessage(exception);

    private static string ResolveBackgroundFolder()
    {
        var configured = PixelSettingsBinder.LoadValue("UiBackgroundFolder")?.ToString();
        return string.IsNullOrWhiteSpace(configured) ? DefaultBackgroundFolder : Environment.ExpandEnvironmentVariables(configured);
    }
}

public sealed record PixelPersonalizationBackgroundSelection(PixelPersonalizationMedia? Media);

public sealed record PixelPersonalizationBackgroundSeedUpdate(string? ImagePath, uint? Seed);

public enum PixelPersonalizationBackgroundContentKind
{
    None,
    Image,
    Video
}

public sealed record PixelPersonalizationBackgroundPresentation(
    PixelPersonalizationBackgroundContentKind ContentKind,
    string? MediaPath,
    string? BackgroundImagePath,
    PixelPersonalizationBackgroundRenderSettings RenderSettings,
    PixelPersonalizationBackgroundSeedUpdate SeedUpdate);

public sealed record PixelPersonalizationMessages(string OpenBackgroundFolderFailedMessageFormat)
{
    public string GetOpenBackgroundFolderFailedMessage(Exception exception) =>
        string.Format(OpenBackgroundFolderFailedMessageFormat, exception.Message);
}

public sealed record PixelPersonalizationStartupPlan(string? PlayAudioPath);

public sealed record PixelPersonalizationPlaybackPlan(
    bool PauseVideo,
    bool PlayVideo,
    bool PauseAudio,
    string? PlayAudioPath)
{
    public static PixelPersonalizationPlaybackPlan None { get; } = new(false, false, false, null);
}

public enum PixelPersonalizationBackgroundStretch
{
    None,
    Uniform,
    Fill,
    UniformToFill
}

public enum PixelPersonalizationBackgroundHorizontalAlignment
{
    Left,
    Center,
    Right
}

public enum PixelPersonalizationBackgroundVerticalAlignment
{
    Top,
    Center,
    Bottom
}

public sealed record PixelPersonalizationBackgroundRenderSettings(
    PixelPersonalizationBackgroundStretch Stretch,
    PixelPersonalizationBackgroundHorizontalAlignment HorizontalAlignment,
    PixelPersonalizationBackgroundVerticalAlignment VerticalAlignment,
    double Opacity,
    int BlurRadius,
    bool AutoPauseVideo);
