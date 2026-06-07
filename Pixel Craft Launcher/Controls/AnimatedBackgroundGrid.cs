using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class AnimatedBackgroundGrid : Grid
{
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<AnimatedBackgroundGrid, IBrush?>(nameof(BackgroundBrush), Brushes.Transparent);

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    protected virtual AvaloniaObject AnimatableElement => this;
    protected virtual AvaloniaProperty AnimatableBrushProperty => BackgroundProperty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BackgroundBrushProperty)
            return;

        if (!IsEffectivelyVisible)
        {
            Background = BackgroundBrush;
            return;
        }

        ModAnimation.AniStart(ModAnimation.AaColor(AnimatableElement, AnimatableBrushProperty,
            BackgroundBrush ?? Brushes.Transparent, 300, ease: new ModAnimation.AniEaseOutFluent()),
            $"AnimatedBackgroundGrid Background {GetHashCode()}");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Background = BackgroundBrush;
    }
}
