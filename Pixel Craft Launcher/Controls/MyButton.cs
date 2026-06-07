using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public enum MyButtonVariant
{
    Tonal,
    Text,
    Outlined,
    Elevated,
    Flat
}

public enum MyButtonSize
{
    Small,
    Medium,
    Large,
    XLarge
}

public class MyButton : Button, ICustomHitTest
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(Text));

    public static readonly StyledProperty<MyButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MyButton, MyButtonVariant>(nameof(Variant), MyButtonVariant.Tonal);

    public static readonly StyledProperty<MyButtonSize> SizeProperty =
        AvaloniaProperty.Register<MyButton, MyButtonSize>(nameof(Size), MyButtonSize.Medium);

    public static readonly StyledProperty<bool> IsBlockProperty =
        AvaloniaProperty.Register<MyButton, bool>(nameof(IsBlock));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MyButton, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<bool> IsDangerProperty =
        AvaloniaProperty.Register<MyButton, bool>(nameof(IsDanger));

    public static readonly StyledProperty<bool> PreserveForegroundProperty =
        AvaloniaProperty.Register<MyButton, bool>(nameof(PreserveForeground));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(Icon));

    public static readonly StyledProperty<Geometry?> LogoProperty =
        AvaloniaProperty.Register<MyButton, Geometry?>(nameof(Logo));

    public static readonly StyledProperty<string?> PrependIconProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(PrependIcon));

    public static readonly StyledProperty<Geometry?> PrependLogoProperty =
        AvaloniaProperty.Register<MyButton, Geometry?>(nameof(PrependLogo));

    public static readonly StyledProperty<string?> AppendIconProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(AppendIcon));

    public static readonly StyledProperty<Geometry?> AppendLogoProperty =
        AvaloniaProperty.Register<MyButton, Geometry?>(nameof(AppendLogo));

    public static readonly StyledProperty<bool> StackedProperty =
        AvaloniaProperty.Register<MyButton, bool>(nameof(Stacked));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MyButton, double>(nameof(IconSize), double.NaN);

    private readonly StackPanel _contentPanel = new();
    private readonly Avalonia.Controls.Shapes.Path _prependIcon = CreateIconPath();
    private readonly Avalonia.Controls.Shapes.Path _appendIcon = CreateIconPath();
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private Border? _backgroundLayer;
    private bool _isHovering;
    private bool _isUpdatingAutoContent;
    private bool _isApplyingSize;
    private bool _hasExplicitHeight;
    private bool _hasExplicitWidth;
    private HorizontalAlignment? _nonBlockHorizontalAlignment;
    private bool _useAutoContent = true;

    public MyButton()
    {
        RenderTransform = new ScaleTransform();
        _isUpdatingAutoContent = true;
        Content = _contentPanel;
        _isUpdatingAutoContent = false;
        RebuildContent();
        ApplySize();
        UpdateBrushes();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MyButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public MyButtonSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsBlock
    {
        get => GetValue(IsBlockProperty);
        set => SetValue(IsBlockProperty, value);
    }

    public bool Block
    {
        get => IsBlock;
        set => IsBlock = value;
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Geometry? Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public string? PrependIcon
    {
        get => GetValue(PrependIconProperty);
        set => SetValue(PrependIconProperty, value);
    }

    public Geometry? PrependLogo
    {
        get => GetValue(PrependLogoProperty);
        set => SetValue(PrependLogoProperty, value);
    }

    public string? AppendIcon
    {
        get => GetValue(AppendIconProperty);
        set => SetValue(AppendIconProperty, value);
    }

    public Geometry? AppendLogo
    {
        get => GetValue(AppendLogoProperty);
        set => SetValue(AppendLogoProperty, value);
    }

    public bool Stacked
    {
        get => GetValue(StackedProperty);
        set => SetValue(StackedProperty, value);
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

    public bool PreserveForeground
    {
        get => GetValue(PreserveForegroundProperty);
        set => SetValue(PreserveForegroundProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _backgroundLayer = e.NameScope.Find<Border>("PART_BackgroundLayer");
        ApplySize();
        UpdateBrushes(animate: false);
    }

    protected override void OnClick()
    {
        if (IsReadOnly)
            return;
        try
        {
            base.OnClick();
        }
        finally
        {
            ResetTransientVisualState();
        }
    }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!_isApplyingSize)
        {
            if (change.Property == HeightProperty)
                _hasExplicitHeight = change.NewValue is double value && !double.IsNaN(value) && value > 0;
            else if (change.Property == WidthProperty)
                _hasExplicitWidth = change.NewValue is double value && !double.IsNaN(value) && value > 0;
            else if (change.Property == HorizontalAlignmentProperty && !IsBlock)
                _nonBlockHorizontalAlignment = (HorizontalAlignment)change.NewValue!;
        }

        if (change.Property == ContentProperty && !_isUpdatingAutoContent && !ReferenceEquals(change.NewValue, _contentPanel))
        {
            _useAutoContent = false;
            return;
        }

        if (change.Property == TextProperty ||
            change.Property == IconProperty ||
            change.Property == LogoProperty ||
            change.Property == PrependIconProperty ||
            change.Property == PrependLogoProperty ||
            change.Property == AppendIconProperty ||
            change.Property == AppendLogoProperty ||
            change.Property == StackedProperty ||
            change.Property == IconSizeProperty ||
            change.Property == ForegroundProperty)
            RebuildContent();

        if (change.Property == SizeProperty ||
            change.Property == IsBlockProperty ||
            change.Property == StackedProperty ||
            change.Property == IconProperty ||
            change.Property == LogoProperty ||
            change.Property == TextProperty)
            ApplySize();

        if (change.Property == VariantProperty ||
            change.Property == IsDangerProperty ||
            change.Property == PreserveForegroundProperty ||
            change.Property == IsEnabledProperty ||
            change.Property == IsReadOnlyProperty)
            UpdateBrushes(_isHovering);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovering = true;
        UpdateBrushes(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ResetTransientVisualState();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsReadOnly)
            SetPressedScale(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        SetPressedScale(false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ResetTransientVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResetTransientVisualState(animate: false);
    }

    protected void UseAutoContent()
    {
        _useAutoContent = true;
        RebuildContent();
    }

    private void RebuildContent()
    {
        if (!_useAutoContent)
            return;

        var text = Text ?? string.Empty;
        var hasText = !string.IsNullOrWhiteSpace(text);
        var prependGeometry = ResolveIcon(PrependIcon, PrependLogo) ?? ResolveIcon(Icon, Logo);
        var appendGeometry = ResolveIcon(AppendIcon, AppendLogo);
        var iconOnly = !hasText && prependGeometry is not null && appendGeometry is null;
        var iconSize = GetEffectiveIconSize(iconOnly);

        _contentPanel.Children.Clear();
        _contentPanel.Orientation = Stacked && hasText ? Orientation.Vertical : Orientation.Horizontal;
        _contentPanel.Spacing = Stacked && hasText ? 3 : 8;
        _contentPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _contentPanel.VerticalAlignment = VerticalAlignment.Center;

        if (prependGeometry is not null)
        {
            ApplyIcon(_prependIcon, prependGeometry, iconSize);
            _contentPanel.Children.Add(_prependIcon);
        }

        if (hasText)
        {
            _label.Text = text;
            _label.Foreground = Foreground;
            _label.FontSize = GetLabelFontSize();
            _contentPanel.Children.Add(_label);
        }

        if (appendGeometry is not null && hasText)
        {
            ApplyIcon(_appendIcon, appendGeometry, iconSize);
            _contentPanel.Children.Add(_appendIcon);
        }

        if (!ReferenceEquals(Content, _contentPanel))
        {
            _isUpdatingAutoContent = true;
            Content = _contentPanel;
            _isUpdatingAutoContent = false;
        }
    }

    private void ApplySize()
    {
        _isApplyingSize = true;
        try
        {
            var iconOnly = string.IsNullOrWhiteSpace(Text) && (ResolveIcon(Icon, Logo) is not null || ResolveIcon(PrependIcon, PrependLogo) is not null);
            var (height, horizontalPadding, verticalPadding) = Size switch
            {
                MyButtonSize.Small => (32d, 12d, 6d),
                MyButtonSize.Large => (48d, 24d, 12d),
                MyButtonSize.XLarge => (56d, 28d, 16d),
                _ => (40d, 18d, 10d)
            };

            if (Stacked && !iconOnly)
                height += Size is MyButtonSize.Small ? 8 : 12;

            MinHeight = height;
            if (!_hasExplicitHeight)
                Height = height;

            if (iconOnly)
            {
                if (!_hasExplicitWidth)
                    Width = height;
                MinWidth = height;
                Padding = new Thickness(0);
            }
            else
            {
                if (!_hasExplicitWidth)
                    Width = double.NaN;
                MinWidth = Size switch
                {
                    MyButtonSize.Small => 64,
                    MyButtonSize.Large => 92,
                    MyButtonSize.XLarge => 104,
                    _ => 76
                };
                Padding = new Thickness(horizontalPadding, verticalPadding);
            }

            CornerRadius = new CornerRadius(height / 2);
            if (_backgroundLayer is not null)
                _backgroundLayer.CornerRadius = CornerRadius;
            HorizontalAlignment = IsBlock ? HorizontalAlignment.Stretch : _nonBlockHorizontalAlignment ?? HorizontalAlignment.Stretch;
            InvalidateMeasure();
        }
        finally
        {
            _isApplyingSize = false;
        }
    }

    private void SetPressedScale(bool pressed)
    {
        if (IsReadOnly || !IsEnabled)
            return;
        if (RenderTransform is ScaleTransform scale)
            ModAnimation.AniStart(ModAnimation.AaScaleTransform(scale, pressed ? 0.985 : 1, pressed ? 80 : 180,
                ease: pressed
                    ? new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)
                    : new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak),
                absolute: true), $"MyButton Scale {GetHashCode()}");
    }

    private void ResetTransientVisualState(bool animate = true)
    {
        SetPressedScale(false);
        if (!_isHovering)
            return;
        _isHovering = false;
        UpdateBrushes(animate: animate);
    }

    private void UpdateBrushes(bool hover = false, bool animate = true)
    {
        var state = GetVisualState();
        ModAnimation.AniStop($"MyButton Color {GetHashCode()}");
        BorderThickness = state.BorderThickness;
        Opacity = IsReadOnly && IsEnabled ? 0.72 : 1;
        Cursor = IsReadOnly ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand);
        if (!PreserveForeground)
            Foreground = state.Foreground;
        BorderBrush = state.Border;
        ApplyBackgroundLayer(state.Background, animate);
        RebuildContent();
    }

    private void ApplyBackgroundLayer(IBrush background, bool animate)
    {
        if (_backgroundLayer is null)
            return;

        if (animate)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(_backgroundLayer, Border.BackgroundProperty, background, 140,
                    ease: new ModAnimation.AniEaseOutFluent()),
                $"MyButton Color {GetHashCode()}");
            return;
        }

        _backgroundLayer.Background = background;
    }

    private ButtonVisualState GetVisualState()
    {
        if (!IsEnabled)
            return new ButtonVisualState(
                ThemeBrushes.TextDisabled,
                ThemeBrushes.InputDisabledBackground,
                Brushes.Transparent,
                new Thickness(0));

        var variant = Variant;
        var accent = GetAccentBrush();
        var hover = _isHovering && !IsReadOnly;
        var stateLayer = IsDanger ? ThemeBrushes.ErrorContainer : ThemeBrushes.ButtonHoverBackground;

        return variant switch
        {
            MyButtonVariant.Text => new ButtonVisualState(accent, hover ? stateLayer : Brushes.Transparent, Brushes.Transparent, new Thickness(0)),
            MyButtonVariant.Outlined => new ButtonVisualState(accent, hover ? stateLayer : Brushes.Transparent, IsDanger ? ThemeBrushes.Error : ThemeBrushes.BorderStrong, new Thickness(1)),
            MyButtonVariant.Elevated => new ButtonVisualState(accent, hover ? ThemeBrushes.ContainerHigh : ThemeBrushes.Surface, Brushes.Transparent, new Thickness(0)),
            MyButtonVariant.Flat => new ButtonVisualState(ThemeBrushes.OnPrimary, hover ? GetFlatHoverBrush() : GetFlatBrush(), Brushes.Transparent, new Thickness(0)),
            _ => new ButtonVisualState(ThemeBrushes.OnPrimaryContainer, hover ? ThemeBrushes.ButtonHoverBackground : ThemeBrushes.PrimaryContainer, Brushes.Transparent, new Thickness(0))
        };
    }

    private IBrush GetAccentBrush() => IsDanger ? ThemeBrushes.Error : ThemeBrushes.PrimaryHover;

    private IBrush GetFlatBrush() => IsDanger ? ThemeBrushes.Error : ThemeBrushes.Primary;

    private IBrush GetFlatHoverBrush() => IsDanger ? ThemeBrushes.ErrorContainer : ThemeBrushes.PrimaryHover;

    private double GetEffectiveIconSize(bool iconOnly)
    {
        if (!double.IsNaN(IconSize) && IconSize > 0)
            return IconSize;
        return Size switch
        {
            MyButtonSize.Small => iconOnly ? 18 : 16,
            MyButtonSize.Large => iconOnly ? 24 : 20,
            MyButtonSize.XLarge => iconOnly ? 28 : 22,
            _ => iconOnly ? 20 : 18
        };
    }

    private double GetLabelFontSize() => Size switch
    {
        MyButtonSize.Small => 12,
        MyButtonSize.Large => 14,
        MyButtonSize.XLarge => 15,
        _ => 13
    };

    private static Geometry? ResolveIcon(string? icon, Geometry? logo)
    {
        if (!string.IsNullOrWhiteSpace(icon))
            return MaterialIconGeometry.TryGet(icon);
        return logo;
    }

    private void ApplyIcon(Avalonia.Controls.Shapes.Path path, Geometry geometry, double size)
    {
        path.Data = geometry;
        path.Width = size;
        path.Height = size;
        path.Fill = Foreground;
    }

    private static Avalonia.Controls.Shapes.Path CreateIconPath()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private readonly record struct ButtonVisualState(
        IBrush Foreground,
        IBrush Background,
        IBrush Border,
        Thickness BorderThickness);
}
