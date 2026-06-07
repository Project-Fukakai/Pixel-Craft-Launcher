using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Controls;

public class MySlider : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<MySlider, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MySlider, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> TickFrequencyProperty =
        AvaloniaProperty.Register<MySlider, double>(nameof(TickFrequency), 1);

    public static readonly StyledProperty<bool> IsSnapToTickEnabledProperty =
        AvaloniaProperty.Register<MySlider, bool>(nameof(IsSnapToTickEnabled), true);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MySlider, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<MySlider, IBrush?>(nameof(Background), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MySlider, IBrush?>(nameof(Foreground), null);

    private const double RestThumbWidth = 5;
    private const double DragThumbWidth = 3;
    private const double ThumbHitWidth = 20;
    private const double ThumbHeight = 46;
    private const double TrackHeight = 18;
    private const double TrackGap = 7;
    private const double EndDotSize = 4.5;
    private const double EndDotInset = 10;
    private const double AnimationDurationMs = 160;

    private bool _isDragging;
    private double _visualThumbWidth = RestThumbWidth;
    private Color? _visualActiveColor;
    private Color _animationStartColor;
    private Color _animationTargetColor;
    private double _animationStartThumbWidth;
    private double _animationTargetThumbWidth = RestThumbWidth;
    private long _animationStartTime;
    private DispatcherTimer? _animationTimer;

    static MySlider()
    {
        AffectsRender<MySlider>(
            MinimumProperty,
            MaximumProperty,
            TickFrequencyProperty,
            IsSnapToTickEnabledProperty,
            ValueProperty,
            BackgroundProperty,
            ForegroundProperty,
            IsEnabledProperty,
            BoundsProperty);
        AffectsMeasure<MySlider>(MinimumProperty, MaximumProperty);
    }

    public MySlider()
    {
        MinHeight = 36;
        MinWidth = 120;
        Focusable = true;
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double TickFrequency
    {
        get => GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public bool IsSnapToTickEnabled
    {
        get => GetValue(IsSnapToTickEnabledProperty);
        set => SetValue(IsSnapToTickEnabledProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, NormalizeValue(value));
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (Background is not null)
            context.FillRectangle(Background, new Rect(Bounds.Size));

        var opacity = IsEnabled ? 1d : 0.55d;
        using var _ = context.PushOpacity(opacity);
        EnsureVisualState();

        var centerY = Bounds.Height / 2d;
        var thumbCenterX = GetThumbCenterX();
        var percent = GetValuePercent();
        var leftEnd = 0d;
        var rightEnd = Bounds.Width;
        var leftRight = Math.Max(leftEnd, thumbCenterX - _visualThumbWidth / 2d - TrackGap);
        var rightLeft = Math.Min(rightEnd, thumbCenterX + _visualThumbWidth / 2d + TrackGap);
        var activeBrush = new SolidColorBrush(_visualActiveColor ?? ResolveTargetActiveColor());

        if (percent > 0.001 && leftRight - leftEnd > TrackHeight / 2d)
        {
            var leftRect = new Rect(leftEnd, centerY - TrackHeight / 2d, leftRight - leftEnd, TrackHeight);
            DrawRoundedRect(context, activeBrush, leftRect, TrackHeight / 2d, 3, 3, TrackHeight / 2d);
        }

        if (percent < 0.999 && rightEnd - rightLeft > TrackHeight / 2d)
        {
            var rightRect = new Rect(rightLeft, centerY - TrackHeight / 2d, rightEnd - rightLeft, TrackHeight);
            DrawRoundedRect(context, ThemeBrushes.PrimaryContainer, rightRect, 3, TrackHeight / 2d, TrackHeight / 2d, 3);
        }

        var dotX = Math.Max(EndDotInset, rightEnd - EndDotInset);
        context.DrawEllipse(ThemeBrushes.OnPrimaryContainer, null, new Point(dotX, centerY), EndDotSize / 2d, EndDotSize / 2d);

        var thumbRect = new Rect(thumbCenterX - _visualThumbWidth / 2d, centerY - ThumbHeight / 2d, _visualThumbWidth, ThumbHeight);
        context.DrawRectangle(activeBrush, null, thumbRect, _visualThumbWidth / 2d, _visualThumbWidth / 2d);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinWidth : Math.Max(MinWidth, availableSize.Width);
        var height = Math.Max(MinHeight, ThumbHeight);
        return new Size(width, height);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();
        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateValueFromPoint(e.GetPosition(this).X);
        StartVisualTransition();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging || !IsEnabled)
            return;

        UpdateValueFromPoint(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        StopDrag(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        StartVisualTransition();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        StartVisualTransition();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        StartVisualTransition();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled)
            return;

        var step = TickFrequency > 0 ? TickFrequency : Math.Max(1, (Maximum - Minimum) / 20d);
        if (e.Key == Key.Left || e.Key == Key.Down)
        {
            Value -= step;
            e.Handled = true;
        }
        else if (e.Key == Key.Right || e.Key == Key.Up)
        {
            Value += step;
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            Value = Minimum;
            e.Handled = true;
        }
        else if (e.Key == Key.End)
        {
            Value = Maximum;
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MinimumProperty ||
            change.Property == MaximumProperty ||
            change.Property == TickFrequencyProperty ||
            change.Property == IsSnapToTickEnabledProperty)
        {
            Value = NormalizeValue(Value);
        }
    }

    private void StopDrag(IPointer pointer)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        pointer.Capture(null);
        StartVisualTransition();
    }

    private void EnsureVisualState()
    {
        _visualActiveColor ??= ResolveTargetActiveColor();
    }

    private void StartVisualTransition()
    {
        EnsureVisualState();
        _animationStartColor = _visualActiveColor!.Value;
        _animationTargetColor = ResolveTargetActiveColor();
        _animationStartThumbWidth = _visualThumbWidth;
        _animationTargetThumbWidth = _isDragging ? DragThumbWidth : RestThumbWidth;
        _animationStartTime = Environment.TickCount64;
        _animationTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnAnimationTick);
        if (!_animationTimer.IsEnabled)
            _animationTimer.Start();
        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var elapsed = Environment.TickCount64 - _animationStartTime;
        var progress = Math.Clamp(elapsed / AnimationDurationMs, 0, 1);
        var eased = 1d - Math.Pow(1d - progress, 3d);
        _visualThumbWidth = Lerp(_animationStartThumbWidth, _animationTargetThumbWidth, eased);
        _visualActiveColor = Lerp(_animationStartColor, _animationTargetColor, eased);
        InvalidateVisual();

        if (progress >= 1)
            _animationTimer?.Stop();
    }

    private Color ResolveTargetActiveColor()
    {
        var brush = IsPointerOver || _isDragging ? ThemeBrushes.PrimaryHover : Foreground ?? ThemeBrushes.Primary;
        return brush is ISolidColorBrush solid ? solid.Color : Color.Parse("#1370f3");
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;

    private static Color Lerp(Color from, Color to, double progress)
    {
        return Color.FromArgb(
            (byte)Math.Round(Lerp(from.A, to.A, progress)),
            (byte)Math.Round(Lerp(from.R, to.R, progress)),
            (byte)Math.Round(Lerp(from.G, to.G, progress)),
            (byte)Math.Round(Lerp(from.B, to.B, progress)));
    }

    private void UpdateValueFromPoint(double x)
    {
        var width = Math.Max(1, Bounds.Width - ThumbHitWidth);
        var percent = Math.Clamp((x - ThumbHitWidth / 2d) / width, 0, 1);
        Value = Minimum + (Maximum - Minimum) * percent;
    }

    private double GetThumbCenterX()
    {
        var percent = GetValuePercent();
        return ThumbHitWidth / 2d + percent * Math.Max(1, Bounds.Width - ThumbHitWidth);
    }

    private double GetValuePercent()
    {
        var min = Math.Min(Minimum, Maximum);
        var max = Math.Max(Minimum, Maximum);
        var range = Math.Max(1, max - min);
        return Math.Clamp((Value - min) / range, 0, 1);
    }

    private double NormalizeValue(double value)
    {
        var min = Math.Min(Minimum, Maximum);
        var max = Math.Max(Minimum, Maximum);
        value = Math.Clamp(value, min, max);

        if (IsSnapToTickEnabled && TickFrequency > 0)
            value = Math.Round((value - Minimum) / TickFrequency) * TickFrequency + Minimum;

        return Math.Clamp(value, min, max);
    }

    private static void DrawRoundedRect(
        DrawingContext context,
        IBrush brush,
        Rect rect,
        double topLeft,
        double topRight,
        double bottomRight,
        double bottomLeft)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(rect.Left + topLeft, rect.Top), true);
            ctx.LineTo(new Point(rect.Right - topRight, rect.Top));
            ctx.ArcTo(new Point(rect.Right, rect.Top + topRight), new Size(topRight, topRight), 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(rect.Right, rect.Bottom - bottomRight));
            ctx.ArcTo(new Point(rect.Right - bottomRight, rect.Bottom), new Size(bottomRight, bottomRight), 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(rect.Left + bottomLeft, rect.Bottom));
            ctx.ArcTo(new Point(rect.Left, rect.Bottom - bottomLeft), new Size(bottomLeft, bottomLeft), 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(rect.Left, rect.Top + topLeft));
            ctx.ArcTo(new Point(rect.Left + topLeft, rect.Top), new Size(topLeft, topLeft), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geometry);
    }
}
