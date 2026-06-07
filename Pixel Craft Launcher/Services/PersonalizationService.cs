using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Settings;

namespace Pixel_Craft_Launcher.Services;

public sealed class PersonalizationService : IDisposable
{
    private readonly DispatcherTimer _backgroundTimer = new();
    private readonly Random _random = new();
    private PixelPersonalizationLibrary _library = new(DefaultBackgroundFolder, [], [], []);
    private int _backgroundIndex = -1;
    private object? _libVlc;
    private object? _videoPlayer;
    private object? _audioPlayer;
    private bool _isGameRunning;

    public PersonalizationService()
    {
        _backgroundTimer.Tick += (_, _) => AdvanceBackground();
    }

    public event Action<Control?>? BackgroundChanged;

    public event Action<string?>? BackgroundImageChanged;

    public string BackgroundFolder => ResolveBackgroundFolder();

    public static string DefaultBackgroundFolder => Path.Combine(Paths.SharedLocalData, "Backgrounds");

    public PixelPersonalizationLibrary Library => _library;

    public void Start()
    {
        ReloadLibrary();
        ApplyBackgroundTimer();
        AdvanceBackground();
        ApplyMusicStartup();
    }

    public void RefreshFromSettings(bool reloadLibrary)
    {
        if (reloadLibrary)
            ReloadLibrary();
        ApplyBackgroundTimer();
        AdvanceBackground();
        ApplyAudioVolume();
    }

    public void SetGameRunning(bool isRunning)
    {
        if (_isGameRunning == isRunning)
            return;
        _isGameRunning = isRunning;

        if (isRunning)
        {
            if (GetBool("UiAutoPauseVideo", true))
                PausePlayer(_videoPlayer);
            if (GetBool("UiMusicStop", false))
                PausePlayer(_audioPlayer);
            if (GetBool("UiMusicStart", false))
                PlayAudio();
        }
        else
        {
            if (GetBool("UiAutoPauseVideo", true))
                PlayPlayer(_videoPlayer);
            if (GetBool("UiMusicAuto", true))
                PlayAudio();
        }
    }

    public void Dispose()
    {
        _backgroundTimer.Stop();
        StopPlayer(_videoPlayer);
        StopPlayer(_audioPlayer);
        DisposeObject(_videoPlayer);
        DisposeObject(_audioPlayer);
        DisposeObject(_libVlc);
    }

    private void ReloadLibrary()
    {
        var folder = ResolveBackgroundFolder();
        Directory.CreateDirectory(folder);
        _library = PixelPersonalizationScanner.Scan(folder);
        _backgroundIndex = -1;
    }

    private string ResolveBackgroundFolder()
    {
        var configured = PixelSettingsBinder.LoadValue("UiBackgroundFolder")?.ToString();
        return string.IsNullOrWhiteSpace(configured) ? DefaultBackgroundFolder : Environment.ExpandEnvironmentVariables(configured);
    }

    private void ApplyBackgroundTimer()
    {
        var seconds = GetInt("UiBackgroundCarousel", 1000);
        if (seconds <= 0 || _library.Backgrounds.Count <= 1)
        {
            _backgroundTimer.Stop();
            return;
        }

        _backgroundTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 3600));
        _backgroundTimer.Start();
    }

    private void AdvanceBackground()
    {
        var backgrounds = _library.Backgrounds;
        if (backgrounds.Count == 0)
        {
            ColorSchemeService.SetBackgroundSeed(null);
            BackgroundImageChanged?.Invoke(null);
            BackgroundChanged?.Invoke(null);
            return;
        }

        _backgroundIndex = (_backgroundIndex + 1) % backgrounds.Count;

        var media = backgrounds[_backgroundIndex];
        if (media.Kind == PixelPersonalizationMediaKind.Video)
        {
            ColorSchemeService.SetBackgroundSeed(null);
            BackgroundImageChanged?.Invoke(null);
            BackgroundChanged?.Invoke(CreateVideoControl(media.Path));
            return;
        }

        _ = UpdateBackgroundSeedAsync(media.Path);
        BackgroundImageChanged?.Invoke(media.Path);
        BackgroundChanged?.Invoke(CreateImageControl(media.Path));
    }

    private static async Task UpdateBackgroundSeedAsync(string path)
    {
        var seed = await Task.Run(() => ImageColorExtractor.TryExtractSeed(path, out var extracted)
            ? extracted
            : (uint?)null).ConfigureAwait(false);
        ColorSchemeService.SetBackgroundSeed(seed);
    }

    private Control? CreateImageControl(string path)
    {
        try
        {
            var image = new Image
            {
                Source = new Bitmap(path),
                Stretch = GetImageStretch(),
                HorizontalAlignment = GetHorizontalAlignment(),
                VerticalAlignment = GetVerticalAlignment(),
                Opacity = GetOpacity(),
                IsHitTestVisible = false
            };
            ApplyBlur(image);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private Control? CreateVideoControl(string path)
    {
        var videoViewType = Type.GetType("LibVLCSharp.Avalonia.VideoView, LibVLCSharp.Avalonia", throwOnError: false);
        var mediaPlayer = EnsureVideoPlayer(path);
        if (videoViewType is null || mediaPlayer is null)
            return CreateImageControl(path);

        if (Activator.CreateInstance(videoViewType) is not Control view)
            return null;

        videoViewType.GetProperty("MediaPlayer")?.SetValue(view, mediaPlayer);
        view.Opacity = GetOpacity();
        ApplyBlur(view);
        if (!_isGameRunning || !GetBool("UiAutoPauseVideo", true))
            PlayPlayer(mediaPlayer);
        return view;
    }

    private object? EnsureVideoPlayer(string path)
    {
        try
        {
            var player = _videoPlayer ??= CreateMediaPlayer();
            if (player is null)
                return null;
            PlayPath(player, path, video: true);
            return player;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyMusicStartup()
    {
        if (GetBool("UiMusicAuto", true))
            PlayAudio();
    }

    private void PlayAudio()
    {
        if (_library.Audio.Count == 0)
            return;

        var index = GetBool("UiMusicRandom", true) ? _random.Next(_library.Audio.Count) : 0;
        var path = _library.Audio[index].Path;
        try
        {
            var player = _audioPlayer ??= CreateMediaPlayer();
            if (player is null)
                return;
            PlayPath(player, path, video: false);
            ApplyAudioVolume();
        }
        catch
        {
            StopPlayer(_audioPlayer);
        }
    }

    private object? CreateMediaPlayer()
    {
        var core = Type.GetType("LibVLCSharp.Shared.Core, LibVLCSharp", throwOnError: false);
        core?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, binder: null, types: Type.EmptyTypes, modifiers: null)?.Invoke(null, null);

        var libVlcType = Type.GetType("LibVLCSharp.Shared.LibVLC, LibVLCSharp", throwOnError: false);
        var playerType = Type.GetType("LibVLCSharp.Shared.MediaPlayer, LibVLCSharp", throwOnError: false);
        if (libVlcType is null || playerType is null)
            return null;

        _libVlc ??= Activator.CreateInstance(libVlcType);
        return Activator.CreateInstance(playerType, _libVlc);
    }

    private void PlayPath(object player, string path, bool video)
    {
        var mediaType = Type.GetType("LibVLCSharp.Shared.Media, LibVLCSharp", throwOnError: false);
        var fromType = Type.GetType("LibVLCSharp.Shared.FromType, LibVLCSharp", throwOnError: false);
        if (mediaType is null || fromType is null || _libVlc is null)
            return;

        var fromPath = Enum.Parse(fromType, "FromPath");
        using var media = Activator.CreateInstance(mediaType, _libVlc, path, fromPath) as IDisposable;
        var mediaObj = media!;
        if (video)
            player.GetType().GetProperty("Mute")?.SetValue(player, true);
        player.GetType().GetMethod("Play", [mediaType])?.Invoke(player, [mediaObj]);
    }

    private void ApplyAudioVolume()
    {
        if (_audioPlayer is null)
            return;
        var volume = Math.Clamp(GetInt("UiMusicVolume", 500) / 10, 0, 100);
        _audioPlayer.GetType().GetProperty("Volume")?.SetValue(_audioPlayer, volume);
    }

    private static void PlayPlayer(object? player) => player?.GetType().GetMethod("Play", Type.EmptyTypes)?.Invoke(player, null);

    private static void PausePlayer(object? player) => player?.GetType().GetMethod("Pause", Type.EmptyTypes)?.Invoke(player, null);

    private static void StopPlayer(object? player) => player?.GetType().GetMethod("Stop", Type.EmptyTypes)?.Invoke(player, null);

    private static void DisposeObject(object? value)
    {
        if (value is IDisposable disposable)
            disposable.Dispose();
    }

    private static void ApplyBlur(Control control)
    {
        var radius = GetInt("UiBackgroundBlur", 0);
        control.Effect = radius > 0 ? new BlurEffect { Radius = radius } : null;
    }

    private static Stretch GetImageStretch() => GetInt("UiBackgroundSuit", 0) switch
    {
        1 => Stretch.None,
        2 => Stretch.Uniform,
        3 => Stretch.Fill,
        4 => Stretch.None,
        _ => Stretch.UniformToFill
    };

    private static HorizontalAlignment GetHorizontalAlignment() => GetInt("UiBackgroundSuit", 0) switch
    {
        5 or 7 => HorizontalAlignment.Left,
        6 or 8 => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center
    };

    private static VerticalAlignment GetVerticalAlignment() => GetInt("UiBackgroundSuit", 0) switch
    {
        5 or 6 => VerticalAlignment.Top,
        7 or 8 => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center
    };

    private static double GetOpacity() => Math.Clamp(GetInt("UiBackgroundOpacity", 1000) / 1000d, 0d, 1d);

    private static bool GetBool(string key, bool fallback)
    {
        return PixelSettingsBinder.LoadValue(key) is bool value ? value : fallback;
    }

    private static int GetInt(string key, int fallback)
    {
        return PixelSettingsBinder.LoadValue(key) is IConvertible value ? Convert.ToInt32(value) : fallback;
    }
}
