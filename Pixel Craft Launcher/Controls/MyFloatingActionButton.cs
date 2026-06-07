using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyFloatingActionButton : Button
{
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<MyFloatingActionButton, string?>(nameof(Icon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MyFloatingActionButton, double>(nameof(IconSize), 20);

    public static readonly StyledProperty<bool> IsDangerProperty =
        AvaloniaProperty.Register<MyFloatingActionButton, bool>(nameof(IsDanger), true);

    public static readonly StyledProperty<double> IconOffsetXProperty =
        AvaloniaProperty.Register<MyFloatingActionButton, double>(nameof(IconOffsetX));

    private readonly Path _icon = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private bool _isHovering;

    public MyFloatingActionButton()
    {
        Width = 44;
        Height = 44;
        Padding = new Thickness(12);
        CornerRadius = new CornerRadius(22);
        BorderThickness = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        var transform = new TransformGroup();
        transform.Children.Add(_scale);
        transform.Children.Add(_translate);
        RenderTransform = transform;
        Content = _icon;
        UpdateVisuals();
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool IsDanger
    {
        get => GetValue(IsDangerProperty);
        set => SetValue(IsDangerProperty, value);
    }

    internal ScaleTransform ScaleTransform => _scale;

    internal TranslateTransform TranslateTransform => _translate;

    public double IconOffsetX
    {
        get => GetValue(IconOffsetXProperty);
        set => SetValue(IconOffsetXProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty ||
            change.Property == IconSizeProperty ||
            change.Property == IsDangerProperty ||
            change.Property == IconOffsetXProperty)
            UpdateVisuals();
        else if (change.Property == ForegroundProperty)
            _icon.Fill = Foreground;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovering = true;
        AnimateBrushes(IsDanger ? ThemeBrushes.Error : ThemeBrushes.PrimaryHover, ThemeBrushes.OnPrimary);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ResetTransientVisualState();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SetPressed(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        SetPressed(false);
    }

    protected override void OnClick()
    {
        try
        {
            base.OnClick();
        }
        finally
        {
            ResetTransientVisualState();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ResetTransientVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResetTransientVisualState();
    }

    private void SetPressed(bool pressed)
    {
        ModAnimation.AniStart(ModAnimation.AaScaleTransform(_scale, pressed ? 0.94 : 1, pressed ? 80 : 220,
            ease: pressed ? new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong) : new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak),
            absolute: true), $"MyFloatingActionButton Scale {GetHashCode()}");
    }

    private void ResetTransientVisualState()
    {
        SetPressed(false);
        if (!_isHovering)
            return;
        _isHovering = false;
        AnimateBrushes(GetNormalBackground(), GetNormalForeground());
    }

    private void AnimateBrushes(IBrush background, IBrush foreground)
    {
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaColor(this, BackgroundProperty, background, 120),
            ModAnimation.AaColor(this, ForegroundProperty, foreground, 120)
        }, $"MyFloatingActionButton Color {GetHashCode()}");
    }

    private void UpdateVisuals()
    {
        Background = GetNormalBackground();
        Foreground = GetNormalForeground();
        _icon.Data = string.IsNullOrWhiteSpace(Icon) ? null : MaterialIconGeometry.TryGet(Icon);
        _icon.Width = IconSize;
        _icon.Height = IconSize;
        _icon.Fill = Foreground;
        _icon.RenderTransform = new TranslateTransform(IconOffsetX, 0);
    }

    private IBrush GetNormalBackground() => IsDanger ? ThemeBrushes.ErrorContainer : ThemeBrushes.PrimaryContainer;

    private IBrush GetNormalForeground() => IsDanger ? ThemeBrushes.Error : ThemeBrushes.OnPrimaryContainer;
}
