using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;
using MaterialColorUtilities.Quantize;
using MaterialColorUtilities.Score;
using PCL.Core.Logging;

namespace PCL.Core.UI.Theme;

public static class ImageColorExtractor
{
    public static bool TryExtractSeed(string? path, out uint seed)
    {
        seed = 0;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var bitmap = Bitmap.DecodeToWidth(stream, 96, BitmapInterpolationMode.MediumQuality);
            var pixels = ReadPixels(bitmap);
            if (pixels.Length == 0)
                return false;

            var quantized = QuantizerCelebi.Quantize(pixels, 128);
            var scored = Scorer.Score(quantized);
            if (scored.Count == 0)
                return false;

            seed = scored[0];
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Theme", $"图片取色失败: {path}");
            return false;
        }
    }

    private static uint[] ReadPixels(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
            return [];

        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = Marshal.AllocHGlobal(buffer.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle, buffer.Length, stride);
            Marshal.Copy(handle, buffer, 0, buffer.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }

        var pixels = new uint[size.Width * size.Height];
        var count = 0;
        for (var i = 0; i + 3 < buffer.Length; i += 4)
        {
            var b = buffer[i];
            var g = buffer[i + 1];
            var r = buffer[i + 2];
            var a = buffer[i + 3];
            if (a < 16)
                continue;
            pixels[count++] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }

        if (count == pixels.Length)
            return pixels;

        Array.Resize(ref pixels, count);
        return pixels;
    }
}
