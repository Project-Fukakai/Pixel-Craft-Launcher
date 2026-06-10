using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Pixel;

namespace Pixel_Craft_Launcher.Services;

public sealed class PersonalizationPlatformBridge : IDisposable
{
    private readonly PixelPersonalizationService _core;
    private readonly DispatcherTimer _backgroundTimer = new();
    private object? _libVlc;
    private object? _videoPlayer;
    private object? _audioPlayer;

    public PersonalizationPlatformBridge(PixelPersonalizationService core)
    {
        _core = core;
        _backgroundTimer.Tick += (_, _) => QueueAdvanceBackground();
    }

    public event Action<Control?>? BackgroundChanged;

    public event Action<string?>? BackgroundImageChanged;

    public string BackgroundFolder => _core.BackgroundFolder;

    public static string DefaultBackgroundFolder => PixelPersonalizationService.DefaultBackgroundFolder;

    public PixelPersonalizationLibrary Library => _core.Library;

    public string EnsureBackgroundFolder() => _core.EnsureBackgroundFolder();

    public void Start()
    {
        _core.ReloadLibrary();
        ApplyBackgroundTimer();
        QueueAdvanceBackground();
        PlayAudio(_core.CreateStartupPlan().PlayAudioPath);
    }

    public void RefreshFromSettings(bool reloadLibrary)
    {
        if (reloadLibrary)
            _core.ReloadLibrary();
        ApplyBackgroundTimer();
        QueueAdvanceBackground();
        ApplyAudioVolume();
    }

    public void SetGameRunning(bool isRunning)
    {
        var plan = _core.SetGameRunning(isRunning);
        if (plan.PauseVideo)
            PausePlayer(_videoPlayer);
        if (plan.PlayVideo)
            PlayPlayer(_videoPlayer);
        if (plan.PauseAudio)
            PausePlayer(_audioPlayer);
        PlayAudio(plan.PlayAudioPath);
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

    private void ApplyBackgroundTimer()
    {
        var interval = _core.GetCarouselInterval();
        if (interval is null)
        {
            _backgroundTimer.Stop();
            return;
        }

        _backgroundTimer.Interval = interval.Value;
        _backgroundTimer.Start();
    }

    private void QueueAdvanceBackground()
    {
        _ = AdvanceBackgroundAsync();
    }

    private async Task AdvanceBackgroundAsync()
    {
        PixelPersonalizationBackgroundPresentation presentation;
        try
        {
            presentation = await _core.AdvanceBackgroundPresentationAsync();
        }
        catch
        {
            BackgroundImageChanged?.Invoke(null);
            BackgroundChanged?.Invoke(null);
            return;
        }

        BackgroundImageChanged?.Invoke(presentation.BackgroundImagePath);
        switch (presentation.ContentKind)
        {
            case PixelPersonalizationBackgroundContentKind.Image when presentation.MediaPath is not null:
                BackgroundChanged?.Invoke(CreateImageControl(presentation.MediaPath, presentation.RenderSettings));
                break;
            case PixelPersonalizationBackgroundContentKind.Video when presentation.MediaPath is not null:
                BackgroundChanged?.Invoke(CreateVideoControl(presentation.MediaPath, presentation.RenderSettings));
                break;
            default:
                BackgroundChanged?.Invoke(null);
                break;
        }
    }

    private static Control? CreateImageControl(string path, PixelPersonalizationBackgroundRenderSettings settings)
    {
        try
        {
            var image = new Image
            {
                Source = new Bitmap(path),
                Stretch = ToAvaloniaStretch(settings.Stretch),
                HorizontalAlignment = ToAvaloniaHorizontalAlignment(settings.HorizontalAlignment),
                VerticalAlignment = ToAvaloniaVerticalAlignment(settings.VerticalAlignment),
                Opacity = settings.Opacity,
                IsHitTestVisible = false
            };
            ApplyBlur(image, settings);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private Control? CreateVideoControl(string path, PixelPersonalizationBackgroundRenderSettings settings)
    {
        var videoViewType = Type.GetType("LibVLCSharp.Avalonia.VideoView, LibVLCSharp.Avalonia", throwOnError: false);
        var mediaPlayer = EnsureVideoPlayer(path);
        if (videoViewType is null || mediaPlayer is null)
            return CreateImageControl(path, settings);

        if (Activator.CreateInstance(videoViewType) is not Control view)
            return null;

        videoViewType.GetProperty("MediaPlayer")?.SetValue(view, mediaPlayer);
        view.Opacity = settings.Opacity;
        ApplyBlur(view, settings);
        if (!_core.IsGameRunning || !settings.AutoPauseVideo)
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

    private void PlayAudio(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

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
        var volume = _core.GetAudioVolume();
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

    private static void ApplyBlur(Control control, PixelPersonalizationBackgroundRenderSettings settings)
    {
        control.Effect = settings.BlurRadius > 0 ? new BlurEffect { Radius = settings.BlurRadius } : null;
    }

    private static Stretch ToAvaloniaStretch(PixelPersonalizationBackgroundStretch stretch) => stretch switch
    {
        PixelPersonalizationBackgroundStretch.None => Stretch.None,
        PixelPersonalizationBackgroundStretch.Uniform => Stretch.Uniform,
        PixelPersonalizationBackgroundStretch.Fill => Stretch.Fill,
        _ => Stretch.UniformToFill
    };

    private static HorizontalAlignment ToAvaloniaHorizontalAlignment(PixelPersonalizationBackgroundHorizontalAlignment alignment) => alignment switch
    {
        PixelPersonalizationBackgroundHorizontalAlignment.Left => HorizontalAlignment.Left,
        PixelPersonalizationBackgroundHorizontalAlignment.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center
    };

    private static VerticalAlignment ToAvaloniaVerticalAlignment(PixelPersonalizationBackgroundVerticalAlignment alignment) => alignment switch
    {
        PixelPersonalizationBackgroundVerticalAlignment.Top => VerticalAlignment.Top,
        PixelPersonalizationBackgroundVerticalAlignment.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center
    };
}
