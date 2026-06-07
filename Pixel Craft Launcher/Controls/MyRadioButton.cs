using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyRadioButton : RadioButton
{
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, MyRouteEventArgs e);

    private const int AnimationTimeOfMouseIn = 90;
    private const int AnimationTimeOfMouseOut = 150;
    private const int AnimationTimeOfCheck = 120;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(Text));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(Icon));

    public static readonly StyledProperty<Geometry?> LogoProperty =
        AvaloniaProperty.Register<MyRadioButton, Geometry?>(nameof(Logo));

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyRadioButton, double>(nameof(LogoScale), 1);

    public static readonly StyledProperty<MyButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MyRadioButton, MyButtonVariant>(nameof(Variant), MyButtonVariant.Text);

    private bool _suppressCheckedEvent;
    private bool _isPointerPressed;

    private readonly Path _logo = new()
    {
        Width = 16,
        Height = 16,
        Stretch = Stretch.Uniform,
        Fill = ThemeBrushes.OnPrimaryContainer,
        Margin = new Thickness(12, 0, 0, 0)
    };

    private readonly TextBlock _label = new()
    {
        Foreground = ThemeBrushes.OnPrimaryContainer,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 12, 0)
    };

    public MyRadioButton()
    {
        MinHeight = 27;
        MaxHeight = 27;
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(13.5);
        Background = Brushes.Transparent;

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _logo, _label }
        };
        UpdateVisuals();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public bool Checked
    {
        get => IsChecked == true;
        set => SetChecked(value, false, true);
    }

    public MyButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public event CheckEventHandler? Check;

    public event ChangeEventHandler? Change;

    public event PreviewClickEventHandler? PreviewClick;

    public void RefreshTheme()
    {
        ModAnimation.AniStop(AnimationName);
        UpdateVisuals();
    }

    public void SetChecked(bool value, bool raiseByMouse, bool anime = true)
    {
        if (value == Checked)
            return;

        if (value && raiseByMouse)
        {
            var preview = new MyRouteEventArgs(true);
            PreviewClick?.Invoke(this, preview);
            if (preview.Handled)
                return;
        }

        _suppressCheckedEvent = true;
        IsChecked = value;
        if (value)
            UncheckSiblingRadios();
        _suppressCheckedEvent = false;
        UpdateVisuals(anime);
        if (value)
            Check?.Invoke(this, raiseByMouse);
        Change?.Invoke(this, raiseByMouse);
    }

    protected override void OnClick()
    {
        SetChecked(true, true, true);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateVisuals(true);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Checked)
            return;
        _isPointerPressed = true;
        UpdateVisuals(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPointerPressed = false;
        UpdateVisuals(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerPressed = false;
        UpdateVisuals(true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty ||
            change.Property == IconProperty ||
            change.Property == LogoProperty ||
            change.Property == IsCheckedProperty ||
            change.Property == LogoScaleProperty ||
            change.Property == VariantProperty)
        {
            UpdateVisuals();
            if (change.Property == IsCheckedProperty && !_suppressCheckedEvent)
            {
                if (Checked)
                    UncheckSiblingRadios();
                if (Checked)
                    Check?.Invoke(this, false);
                Change?.Invoke(this, false);
            }
        }
    }

    private void UpdateVisuals(bool anime = false)
    {
        _label.Text = Text;
        var hasIcon = !string.IsNullOrWhiteSpace(Icon);
        _logo.Data = hasIcon ? MaterialIconGeometry.TryGet(Icon) : Logo;
        _logo.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
        var target = GetBackgroundBrush();
        var foreground = GetForegroundBrush();
        if (anime &&
            (!BrushMatches(Background, target) ||
             !BrushMatches(_logo.Fill, foreground) ||
             !BrushMatches(_label.Foreground, foreground)))
        {
            ModAnimation.AniStop(AnimationName);
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaColor(this, BackgroundProperty, target, Checked ? AnimationTimeOfCheck : AnimationTimeOfMouseOut),
                ModAnimation.AaColor(_logo, Shape.FillProperty, foreground, Checked ? AnimationTimeOfCheck : AnimationTimeOfMouseIn),
                ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, foreground, Checked ? AnimationTimeOfCheck : AnimationTimeOfMouseIn)
            }, AnimationName);
        }
        else
        {
            ModAnimation.AniStop(AnimationName);
            Background = target;
            _logo.Fill = foreground;
            _label.Foreground = foreground;
        }
    }

    private IBrush GetBackgroundBrush()
    {
        if (Variant == MyButtonVariant.Flat)
            return Checked
                ? ThemeBrushes.Primary
                : _isPointerPressed
                    ? ThemeBrushes.Border
                    : IsPointerOver
                        ? ThemeBrushes.ListItemHover
                        : Brushes.Transparent;

        return Checked
            ? ThemeBrushes.OnPrimaryContainer
            : _isPointerPressed
                ? ThemeBrushes.OnPrimaryContainerPressed
                : IsPointerOver
                    ? ThemeBrushes.OnPrimaryContainerHover
                    : Brushes.Transparent;
    }

    private IBrush GetForegroundBrush()
    {
        if (Variant == MyButtonVariant.Flat)
            return Checked ? ThemeBrushes.OnPrimary : ThemeBrushes.Primary;

        return Checked ? ThemeBrushes.PrimaryContainer : ThemeBrushes.OnPrimaryContainer;
    }

    private string AnimationName => $"MyRadioButton Color {GetHashCode()}";

    private static bool BrushMatches(IBrush? left, IBrush? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is ISolidColorBrush leftSolid && right is ISolidColorBrush rightSolid)
            return leftSolid.Color == rightSolid.Color;
        return false;
    }

    private void UncheckSiblingRadios()
    {
        if (!string.IsNullOrWhiteSpace(GroupName) || Parent is not Panel panel)
            return;

        foreach (var child in panel.Children.OfType<MyRadioButton>())
        {
            if (ReferenceEquals(child, this) || !child.Checked)
                continue;
            child.SetChecked(false, false, false);
        }
    }
}
