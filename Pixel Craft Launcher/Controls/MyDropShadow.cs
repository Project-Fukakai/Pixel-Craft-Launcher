using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Pixel_Craft_Launcher.Controls;

public class MyDropShadow : Border
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MyDropShadow, Color>(nameof(Color), ThemeBrushes.ShadowColor);

    public static readonly StyledProperty<double> ShadowRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, double>(nameof(ShadowRadius), 12);

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double ShadowRadius
    {
        get => GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty || change.Property == ShadowRadiusProperty)
            BoxShadow = new BoxShadows(new BoxShadow { Blur = ShadowRadius, Color = Color, OffsetY = 2 });
    }
}
