using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Media;
using Avalonia.Threading;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyCard : UserControl
{
    public delegate void PreviewSwapEventHandler(object sender, MyRouteEventArgs e);

    public delegate void SwapEventHandler(object sender, MyRouteEventArgs e);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyCard, string?>(nameof(Title));

    public static readonly StyledProperty<bool> CanSwapProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(CanSwap));

    public static readonly StyledProperty<bool> IsSwappedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsSwapped));

    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<MyCard, object?>(nameof(CardContent));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsLoading));

    private readonly Border _chrome = new()
    {
        CornerRadius = new CornerRadius(5),
        Background = ThemeBrushes.TransparentBackground,
        BorderBrush = ThemeBrushes.Border,
        BorderThickness = new Thickness(1),
        BoxShadow = new BoxShadows(new BoxShadow { Blur = 12, Spread = 0, OffsetY = 2, Color = ThemeBrushes.ShadowColor })
    };

    private readonly TextBlock _title = new()
    {
        FontWeight = FontWeight.Bold,
        FontSize = 13,
        Foreground = ThemeBrushes.Text
    };

    private readonly Path _swapIcon = new()
    {
        Width = 10,
        Height = 6,
        Stretch = Stretch.Uniform,
        Fill = ThemeBrushes.Text,
        Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z")
    };

    private readonly Path _loadingIcon = new()
    {
        Width = 15,
        Height = 15,
        Stretch = Stretch.Uniform,
        Stroke = ThemeBrushes.PrimaryHover,
        StrokeThickness = 2,
        StrokeLineCap = PenLineCap.Round,
        Data = Geometry.Parse("M 8 1 A 7 7 0 1 1 1 8")
    };

    private readonly ContentPresenter _presenter = new();
    private const double CollapsedHeight = 45d;
    private double _expandedHeight;
    private bool _hasAppliedInitialSwap;
    private bool _hasInstalledStack;
    private DispatcherTimer? _loadingTimer;

    public MyCard()
    {
        Content = BuildChrome();
        ClipToBounds = true;
        PointerEntered += (_, _) =>
        {
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaColor(_title, TextBlock.ForegroundProperty, ThemeBrushes.PrimaryHover, 120),
                ModAnimation.AaColor(_swapIcon, Shape.FillProperty, ThemeBrushes.PrimaryHover, 120),
                ModAnimation.AaColor(_loadingIcon, Shape.StrokeProperty, ThemeBrushes.PrimaryHover, 120)
            }, $"MyCard Hover {GetHashCode()}");
        };
        PointerExited += (_, _) =>
        {
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaColor(_title, TextBlock.ForegroundProperty, ThemeBrushes.Text, 160),
                ModAnimation.AaColor(_swapIcon, Shape.FillProperty, ThemeBrushes.Text, 160),
                ModAnimation.AaColor(_loadingIcon, Shape.StrokeProperty, ThemeBrushes.PrimaryHover, 160)
            }, $"MyCard Hover {GetHashCode()}");
        };
        UpdateVisuals();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool CanSwap
    {
        get => GetValue(CanSwapProperty);
        set => SetValue(CanSwapProperty, value);
    }

    public bool IsSwapped
    {
        get => GetValue(IsSwappedProperty);
        set => SetValue(IsSwappedProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    [Content]
    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public object? SwapControl
    {
        get => CardContent;
        set => CardContent = value;
    }

    public Action<StackPanel>? InstallMethod { get; set; }

    public event PreviewSwapEventHandler? PreviewSwap;

    public event SwapEventHandler? Swap;

    public void StackInstall()
    {
        if (_hasInstalledStack || CardContent is not StackPanel stack || InstallMethod is null)
            return;

        _hasInstalledStack = true;
        InstallMethod(stack);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty ||
            change.Property == CanSwapProperty ||
            change.Property == IsSwappedProperty ||
            change.Property == IsLoadingProperty)
        {
            UpdateVisuals();
            if (change.Property == IsSwappedProperty && change.OldValue is bool)
                Swap?.Invoke(this, new MyRouteEventArgs(false));
        }
        else if (change.Property == CardContentProperty)
        {
            _hasInstalledStack = false;
            _presenter.Content = CardContent;
        }
    }

    private Control BuildChrome()
    {
        var root = new Grid();
        root.Children.Add(_chrome);

        var body = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Margin = new Thickness(15, 12, 15, 14)
        };

        var header = new Grid
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Children.Add(_title);
        Grid.SetColumn(_swapIcon, 1);
        header.Children.Add(_swapIcon);
        Grid.SetColumn(_loadingIcon, 1);
        header.Children.Add(_loadingIcon);
        header.PointerPressed += (_, e) =>
        {
            if (!CanSwap || IsLoading)
                return;
            var preview = new MyRouteEventArgs(true);
            PreviewSwap?.Invoke(this, preview);
            if (preview.Handled)
                return;
            IsSwapped = !IsSwapped;
            e.Handled = true;
        };

        Grid.SetRow(_presenter, 1);
        _presenter.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(header);
        body.Children.Add(_presenter);
        root.Children.Add(body);
        return root;
    }

    private void UpdateVisuals()
    {
        if (IsLoading && !IsSwapped)
        {
            IsSwapped = true;
            return;
        }

        _title.Text = Title;
        _swapIcon.IsVisible = CanSwap && !IsLoading;
        _loadingIcon.IsVisible = IsLoading;
        if (_swapIcon.RenderTransform is not RotateTransform rotate)
        {
            rotate = new RotateTransform();
            _swapIcon.RenderTransform = rotate;
        }

        ModAnimation.AniStart(ModAnimation.AaRotateTransform(rotate, IsSwapped ? 180 : 0, 180,
            ease: new ModAnimation.AniEaseOutFluent(), absolute: true), $"MyCard SwapIcon {GetHashCode()}");
        if (IsLoading)
        {
            StartLoadingSpin();
        }
        else
        {
            StopLoadingSpin();
        }

        if (IsSwapped && !_hasAppliedInitialSwap && _presenter.IsVisible && Bounds.Height <= 0 && DesiredSize.Height <= 0)
        {
            _hasAppliedInitialSwap = true;
            _presenter.IsVisible = false;
            Height = CollapsedHeight;
            return;
        }
        _hasAppliedInitialSwap = true;

        if (IsSwapped && _presenter.IsVisible)
        {
            var currentHeight = Bounds.Height > 0 ? Bounds.Height : DesiredSize.Height;
            if (currentHeight > CollapsedHeight)
            {
                _expandedHeight = currentHeight;
                Height = currentHeight;
            }
            else
            {
                Height = CollapsedHeight;
            }

            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(_presenter, -1, 100, ease: new ModAnimation.AniEaseInFluent()),
                ModAnimation.AaHeight(this, CollapsedHeight - Math.Max(CollapsedHeight, currentHeight), 180,
                    ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaCode(() =>
                {
                    _presenter.IsVisible = false;
                    Height = CollapsedHeight;
                }, 180)
            }, $"MyCard Swap {GetHashCode()}");
        }
        else if (!IsSwapped && !_presenter.IsVisible)
        {
            StackInstall();
            _presenter.Opacity = 0;
            _presenter.IsVisible = true;
            var targetHeight = _expandedHeight > CollapsedHeight ? _expandedHeight : 0;
            Height = CollapsedHeight;

            if (targetHeight > 0)
                ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaHeight(this, targetHeight - CollapsedHeight, 180, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaOpacity(_presenter, 1, 140, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaCode(() => Height = double.NaN, 180)
                }, $"MyCard Swap {GetHashCode()}");
            else
            {
                Height = double.NaN;
                ModAnimation.AniStart(ModAnimation.AaOpacity(_presenter, 1, 140, ease: new ModAnimation.AniEaseOutFluent()),
                    $"MyCard Swap {GetHashCode()}");
            }
        }
    }

    private RotateTransform EnsureLoadingRotate()
    {
        if (_loadingIcon.RenderTransform is RotateTransform rotate)
            return rotate;
        rotate = new RotateTransform();
        _loadingIcon.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _loadingIcon.RenderTransform = rotate;
        return rotate;
    }

    private void StartLoadingSpin()
    {
        var rotate = EnsureLoadingRotate();
        if (_loadingTimer is not null)
            return;
        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _loadingTimer.Tick += (_, _) => rotate.Angle = (rotate.Angle + 8) % 360;
        _loadingTimer.Start();
    }

    private void StopLoadingSpin()
    {
        _loadingTimer?.Stop();
        _loadingTimer = null;
        if (_loadingIcon.RenderTransform is RotateTransform rotate)
            rotate.Angle = 0;
    }
}
