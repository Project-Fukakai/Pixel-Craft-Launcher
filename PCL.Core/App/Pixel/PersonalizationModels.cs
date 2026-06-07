using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.App.Pixel;

public enum PixelPersonalizationMediaKind
{
    Image,
    Video,
    Audio
}

public sealed record PixelPersonalizationMedia(string Path, PixelPersonalizationMediaKind Kind);

public sealed record PixelPersonalizationLibrary(
    string Folder,
    IReadOnlyList<PixelPersonalizationMedia> Images,
    IReadOnlyList<PixelPersonalizationMedia> Videos,
    IReadOnlyList<PixelPersonalizationMedia> Audio)
{
    public IReadOnlyList<PixelPersonalizationMedia> Backgrounds { get; } =
        Images.Concat(Videos).OrderBy(static item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
}

public static class PixelPersonalizationScanner
{
    public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
    };

    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi"
    };

    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".ogg", ".oga", ".flac", ".wav", ".m4a", ".aac"
    };

    public static PixelPersonalizationLibrary Scan(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new PixelPersonalizationLibrary(folder, [], [], []);

        var images = new List<PixelPersonalizationMedia>();
        var videos = new List<PixelPersonalizationMedia>();
        var audio = new List<PixelPersonalizationMedia>();

        foreach (var file in EnumerateFiles(folder))
        {
            var extension = Path.GetExtension(file);
            if (ImageExtensions.Contains(extension))
                images.Add(new PixelPersonalizationMedia(file, PixelPersonalizationMediaKind.Image));
            else if (VideoExtensions.Contains(extension))
                videos.Add(new PixelPersonalizationMedia(file, PixelPersonalizationMediaKind.Video));
            else if (AudioExtensions.Contains(extension))
                audio.Add(new PixelPersonalizationMedia(file, PixelPersonalizationMediaKind.Audio));
        }

        return new PixelPersonalizationLibrary(
            folder,
            images.OrderBy(static item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            videos.OrderBy(static item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            audio.OrderBy(static item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IEnumerable<string> EnumerateFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return [];
        }
    }
}
