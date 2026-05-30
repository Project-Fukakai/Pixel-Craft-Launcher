using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.UI.Effects;

namespace PCL.Core.UI.Controls;

public class BlurBorder : Border
{
    public static readonly StyledProperty<Geometry?> ContentClipProperty =
        AvaloniaProperty.Register<BlurBorder, Geometry?>(nameof(ContentClip));

    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<BlurBorder, int>(nameof(MaxDepth), 2);

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(BlurRadius), 16.0);

    public static readonly StyledProperty<KernelType> BlurKernelTypeProperty =
        AvaloniaProperty.Register<BlurBorder, KernelType>(nameof(BlurKernelType), KernelType.Gaussian);

    public static readonly StyledProperty<RenderingBias> BlurRenderingBiasProperty =
        AvaloniaProperty.Register<BlurBorder, RenderingBias>(nameof(BlurRenderingBias), RenderingBias.Performance);

    public static readonly StyledProperty<double> BlurSamplingRateProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(BlurSamplingRate), 0.9);

    public BlurBorder()
    {
        UpdateBlurEffect();
    }

    public Geometry? ContentClip
    {
        get => GetValue(ContentClipProperty);
        set => SetValue(ContentClipProperty, value);
    }

    public int MaxDepth
    {
        get => GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public KernelType BlurKernelType
    {
        get => GetValue(BlurKernelTypeProperty);
        set => SetValue(BlurKernelTypeProperty, value);
    }

    public RenderingBias BlurRenderingBias
    {
        get => GetValue(BlurRenderingBiasProperty);
        set => SetValue(BlurRenderingBiasProperty, value);
    }

    public double BlurSamplingRate
    {
        get => GetValue(BlurSamplingRateProperty);
        set => SetValue(BlurSamplingRateProperty, Math.Clamp(value, 0.1, 1.0));
    }

    private void UpdateBlurEffect()
    {
        Effect = BlurRadius > 0
            ? new BlurEffect { Radius = BlurRadius }
            : null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BlurRadiusProperty)
            UpdateBlurEffect();
        else if (change.Property == ContentClipProperty)
            Clip = ContentClip;
    }
}
