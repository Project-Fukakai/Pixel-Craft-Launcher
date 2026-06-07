using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace PCL.Core.UI.Effects;

internal sealed class OptimizedBlurEffect
{
    public double Radius { get; init; } = 16.0;
    public double SamplingRate { get; init; } = 0.7;
    public System.Windows.Media.Effects.RenderingBias RenderingBias { get; init; } =
        System.Windows.Media.Effects.RenderingBias.Performance;

    public System.Windows.Media.Effects.KernelType KernelType { get; init; } =
        System.Windows.Media.Effects.KernelType.Gaussian;

    public WriteableBitmap ApplyBlur(BitmapSource source)
    {
        var scale = Math.Clamp(SamplingRate, 0.1, 1.0);
        var width = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
        var height = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
        var visual = new DrawingVisual
        {
            Effect = new BlurEffect
            {
                Radius = Radius * scale,
                KernelType = KernelType,
                RenderingBias = RenderingBias
            }
        };

        using (var context = visual.RenderOpen())
            context.DrawImage(source, new Rect(0, 0, width, height));

        var small = new RenderTargetBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
        small.Render(visual);

        var scaled = new TransformedBitmap(small, new ScaleTransform(1 / scale, 1 / scale));
        return new WriteableBitmap(scaled);
    }
}
