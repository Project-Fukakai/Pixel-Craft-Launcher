using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyListItem : Button, IMyRadio
{
    public delegate void ClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void LogoClickEventHandler(object sender, PointerReleasedEventArgs e);

    public enum CheckType
    {
        None,
        Clickable,
        RadioBox,
        CheckBox
    }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> InfoProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Info));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Icon));

    public static readonly StyledProperty<Geometry?> LogoProperty =
        AvaloniaProperty.Register<MyListItem, Geometry?>(nameof(Logo));

    public static readonly StyledProperty<double> LogoSizeProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(LogoSize), 20d);

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<MyListItem, Thickness>(nameof(ContentPadding), new Thickness(8, 0));

    public static readonly StyledProperty<double> LogoTextSpacingProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(LogoTextSpacing), 6d);

    public static readonly StyledProperty<CheckType> TypeProperty =
        AvaloniaProperty.Register<MyListItem, CheckType>(nameof(Type));

    public static readonly StyledProperty<bool> IsSidebarItemProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(IsSidebarItem));

    private readonly ColumnDefinition _checkColumn = new(new GridLength(2));
    private readonly Border _hoverBack = new()
    {
        CornerRadius = new CornerRadius(4),
        Background = ThemeBrushes.ListItemHover,
        BorderBrush = ThemeBrushes.Border,
        BorderThickness = new Thickness(1),
        Opacity = 0,
        IsHitTestVisible = false
    };

    private readonly Border _checkStrip = new()
    {
        Width = 5,
        Margin = new Thickness(-1, 6, 0, 6),
        CornerRadius = new CornerRadius(2),
        Background = ThemeBrushes.PrimaryHover,
        Opacity = 0,
        IsVisible = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    private readonly Path _logo = new() { Width = 20, Height = 20, Stretch = Stretch.Uniform, Fill = ThemeBrushes.PrimaryHover, IsVisible = false };
    private readonly Image _logoImage = new() { Width = 20, Height = 20, Stretch = Stretch.Uniform, IsVisible = false };
    private readonly TextBlock _title = new()
    {
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontSize = 13,
        Foreground = ThemeBrushes.Text
    };

    private readonly TextBlock _info = new()
    {
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontSize = 12,
        Foreground = ThemeBrushes.TextSecondary
    };

    private readonly StackPanel _textPanel = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly StackPanel _buttonStack = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TranslateTransform _checkStripTranslate = new();

    private bool _checked;
    private bool _autoHeightApplied = true;
    private bool _updatingAutoHeight;
    private double? _pendingMarkerSlideOffset;

    public MyListItem()
    {
        Height = 42;
        Padding = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        _checkStrip.RenderTransform = _checkStripTranslate;
        Content = BuildContent();
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _logo.PointerReleased += (sender, e) =>
        {
            LogoClick?.Invoke(sender ?? this, e);
            e.Handled = true;
        };
        _logoImage.PointerReleased += (sender, e) =>
        {
            LogoClick?.Invoke(sender ?? this, e);
            e.Handled = true;
        };
        UpdateVisuals();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
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

    public double LogoSize
    {
        get => GetValue(LogoSizeProperty);
        set => SetValue(LogoSizeProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public double LogoTextSpacing
    {
        get => GetValue(LogoTextSpacingProperty);
        set => SetValue(LogoTextSpacingProperty, value);
    }

    public CheckType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool IsSidebarItem
    {
        get => GetValue(IsSidebarItemProperty);
        set => SetValue(IsSidebarItemProperty, value);
    }

    public bool Checked
    {
        get => _checked;
        set => SetChecked(value, false);
    }

    public IEnumerable<MyIconButton> Buttons => _buttonStack.Children.OfType<MyIconButton>();

    public event IMyRadio.CheckEventHandler? Check;

    public event IMyRadio.ChangedEventHandler? Changed;

    public new event ClickEventHandler? Click;

    public event LogoClickEventHandler? LogoClick;

    public void SetChecked(bool value, bool user)
    {
        if (_checked == value)
            return;
        if (value && Type == CheckType.RadioBox)
            _pendingMarkerSlideOffset = GetCheckedSiblingOffsetY();
        _checked = value;
        if (_checked && Type == CheckType.RadioBox)
            UncheckSiblingItems();
        UpdateVisuals();
        var e = new MyRouteEventArgs(user);
        if (_checked)
            Check?.Invoke(this, e);
        Changed?.Invoke(this, e);
    }

    public void AddButton(MyIconButton button)
    {
        _buttonStack.Children.Add(button);
        _buttonStack.Opacity = 0;
        _buttonStack.IsVisible = _buttonStack.Children.Count > 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty ||
            change.Property == InfoProperty ||
            change.Property == IconProperty ||
            change.Property == LogoProperty ||
            change.Property == LogoSizeProperty ||
            change.Property == ContentPaddingProperty ||
            change.Property == LogoTextSpacingProperty ||
            change.Property == TypeProperty ||
            change.Property == IsSidebarItemProperty ||
            change.Property == IsEnabledProperty)
            UpdateVisuals();
        else if (change.Property == HeightProperty && !_updatingAutoHeight)
        {
            _autoHeightApplied = false;
            ApplyTextDensity();
        }
    }

    private Control BuildContent()
    {
        _textPanel.Children.Add(_title);
        _textPanel.Children.Add(_info);

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                _checkColumn,
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        contentGrid.Children.Add(_checkStrip);
        Grid.SetColumn(_logo, 1);
        contentGrid.Children.Add(_logo);
        Grid.SetColumn(_logoImage, 1);
        contentGrid.Children.Add(_logoImage);
        Grid.SetColumn(_textPanel, 2);
        contentGrid.Children.Add(_textPanel);
        Grid.SetColumn(_buttonStack, 3);
        _buttonStack.Margin = new Thickness(0, 0, 8, 0);
        contentGrid.Children.Add(_buttonStack);

        var root = new Grid();
        root.Children.Add(_hoverBack);
        root.Children.Add(contentGrid);
        return root;
    }

    private void UpdateVisuals()
    {
        _title.Text = Title;
        _info.Text = Info;
        _title.Foreground = IsEnabled ? ThemeBrushes.Text : ThemeBrushes.TextDisabled;
        _info.Foreground = IsEnabled ? ThemeBrushes.TextSecondary : ThemeBrushes.TextDisabled;
        var hasInfo = !string.IsNullOrWhiteSpace(Info);
        _info.IsVisible = hasInfo;
        ApplyTextDensity();
        var hasIcon = !string.IsNullOrWhiteSpace(Icon);
        var hasBitmapIcon = hasIcon && TrySetBitmapIcon(Icon);
        _logo.Data = !hasBitmapIcon && hasIcon ? MaterialIconGeometry.TryGet(Icon) : Logo;
        _logo.IsVisible = !hasBitmapIcon && _logo.Data is not null;
        _logoImage.IsVisible = hasBitmapIcon;
        _logo.Width = Math.Max(0, LogoSize);
        _logo.Height = Math.Max(0, LogoSize);
        _logoImage.Width = Math.Max(0, LogoSize);
        _logoImage.Height = Math.Max(0, LogoSize);
        _logo.Margin = new Thickness(ContentPadding.Left, 0, Math.Max(0, LogoTextSpacing), 0);
        _logoImage.Margin = _logo.Margin;
        _buttonStack.Margin = new Thickness(0, 0, ContentPadding.Right, 0);
        _logo.Fill = IsEnabled ? ThemeBrushes.PrimaryHover : ThemeBrushes.TextDisabled;
        _logoImage.Opacity = IsEnabled ? 1 : 0.48;
        Opacity = IsEnabled ? 1 : 0.72;
        var hasCheckMarker = Type is CheckType.RadioBox or CheckType.CheckBox;
        _checkColumn.Width = hasCheckMarker ? new GridLength(6) : new GridLength(2);
        _checkStrip.IsVisible = hasCheckMarker;
        RefreshCheckMarker();
        _buttonStack.IsVisible = _buttonStack.Children.Count > 0;
        ApplyChromeStyle();
    }

    private bool TrySetBitmapIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon) ||
            !icon.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            !icon.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !icon.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            _logoImage.Source = null;
            return false;
        }

        try
        {
            var uri = icon.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(icon)
                : new Uri($"avares://{Uri.EscapeDataString(typeof(MyListItem).Assembly.GetName().Name ?? "Pixel Craft Launcher")}/{icon.TrimStart('/')}");
            using var stream = AssetLoader.Open(uri);
            _logoImage.Source = new Bitmap(stream);
            return true;
        }
        catch
        {
            _logoImage.Source = null;
            return false;
        }
    }

    private void ApplyChromeStyle()
    {
        _hoverBack.CornerRadius = IsSidebarItem ? new CornerRadius(0) : new CornerRadius(4);
        _hoverBack.BorderThickness = IsSidebarItem ? new Thickness(0) : new Thickness(1);
        _checkStrip.CornerRadius = IsSidebarItem ? new CornerRadius(0, 2.5, 2.5, 0) : new CornerRadius(2);
    }

    private void RefreshCheckMarker()
    {
        var targetOpacity = Checked ? 1d : 0d;
        var animationName = $"MyListItem Check {GetHashCode()}";
        ModAnimation.AniStop(animationName);

        if (Parent is null || Type is not (CheckType.RadioBox or CheckType.CheckBox))
        {
            _checkStrip.Opacity = targetOpacity;
            _checkStripTranslate.Y = 0;
            _pendingMarkerSlideOffset = null;
            return;
        }

        if (Checked)
        {
            if (_pendingMarkerSlideOffset is { } offset && Math.Abs(offset) > 0.5)
                _checkStripTranslate.Y = offset;
            else if (_checkStrip.Opacity <= 0)
                _checkStripTranslate.Y = -6;

            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaTranslateY(_checkStripTranslate, -_checkStripTranslate.Y, 260, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                ModAnimation.AaOpacity(_checkStrip, 1 - _checkStrip.Opacity, 120, ease: new ModAnimation.AniEaseOutFluent())
            }, animationName);
        }
        else
        {
            ModAnimation.AniStart(ModAnimation.AaOpacity(_checkStrip, -_checkStrip.Opacity, 90, ease: new ModAnimation.AniEaseInFluent()), animationName);
        }

        _pendingMarkerSlideOffset = null;
    }

    private double? GetCheckedSiblingOffsetY()
    {
        if (Parent is not Panel panel)
            return null;

        var previous = panel.Children
            .OfType<MyListItem>()
            .FirstOrDefault(child => !ReferenceEquals(child, this) && child.Type == CheckType.RadioBox && child.Checked);
        if (previous is null)
            return null;

        var offset = previous.Bounds.Y - Bounds.Y;
        return Math.Abs(offset) < 0.5 ? null : offset;
    }

    private void ApplyTextDensity()
    {
        var hasInfo = !string.IsNullOrWhiteSpace(Info);
        _textPanel.Margin = hasInfo ? new Thickness(4, 6, 8, 6) : new Thickness(4, 0, 8, 0);

        var targetHeight = hasInfo ? 52d : 42d;
        if (!_autoHeightApplied && (!hasInfo || Height >= targetHeight))
            return;

        try
        {
            _updatingAutoHeight = true;
            Height = targetHeight;
            _autoHeightApplied = true;
        }
        finally
        {
            _updatingAutoHeight = false;
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        var animations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaColor(_hoverBack, Border.BackgroundProperty, ThemeBrushes.ListItemHover, 120),
            ModAnimation.AaColor(_hoverBack, Border.BorderBrushProperty, ThemeBrushes.Border, 120),
            ModAnimation.AaOpacity(_hoverBack, 1 - _hoverBack.Opacity, 120, ease: new ModAnimation.AniEaseOutFluent())
        };
        if (_buttonStack.Children.Count > 0)
            animations.Add(ModAnimation.AaOpacity(_buttonStack, 1 - _buttonStack.Opacity, 90));
        ModAnimation.AniStart(animations, $"MyListItem Hover {GetHashCode()}");
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        var animations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaOpacity(_hoverBack, -_hoverBack.Opacity, 160, ease: new ModAnimation.AniEaseOutFluent())
        };
        if (_buttonStack.Children.Count > 0)
            animations.Add(ModAnimation.AaOpacity(_buttonStack, -_buttonStack.Opacity, 120));
        ModAnimation.AniStart(animations, $"MyListItem Hover {GetHashCode()}");
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsNestedInteractiveSource(e.Source as Visual))
            return;
        Click?.Invoke(this, e);
    }

    private bool IsNestedInteractiveSource(Visual? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, _logo) || current is Button)
                return true;
        }

        return false;
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Type == CheckType.CheckBox)
            SetChecked(!Checked, true);
        else if (Type == CheckType.RadioBox)
            SetChecked(true, true);
    }

    private void UncheckSiblingItems()
    {
        if (Parent is not Panel panel)
            return;
        foreach (var child in panel.Children.OfType<MyListItem>())
        {
            if (ReferenceEquals(child, this) || child.Type != CheckType.RadioBox || !child.Checked)
                continue;
            child.SetChecked(false, false);
        }
    }
}
