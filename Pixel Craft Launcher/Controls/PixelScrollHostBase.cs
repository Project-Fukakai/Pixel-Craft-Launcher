using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Controls;

public abstract class PixelScrollHostBase : Panel
{
    private const double WheelScrollStep = 56d;
    private const double ThinThumbWidth = 3d;
    private const double NormalThumbWidth = 7d;
    private const double ThumbRightPadding = 3d;
    private const double MinThumbHeight = 28d;
    private const double NearHitWidth = 18d;
    private const int ScrollVisibleHoldMs = 700;
    private const double TransitionDurationMs = 160d;

    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _scrollHoldTimer;
    private readonly ScrollBarOverlay _scrollBarOverlay;

    private double _extentHeight;
    private double _offsetY;
    private double _dragStartOffsetY;
    private double _dragStartPointerY;
    private double _animationStartTime;
    private double _visualOpacity;
    private double _visualWidth;
    private Color _visualColor;
    private ScrollBarVisualState _targetState = ScrollBarVisualState.Hidden;
    private ScrollBarVisualState _animationStartState = ScrollBarVisualState.Hidden;
    private Point? _lastPointerPosition;
    private bool _isDraggingThumb;
    private bool _isPointerInside;
    private bool _isScrollHoldActive;

    protected PixelScrollHostBase()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        _scrollBarOverlay = new ScrollBarOverlay(this)
        {
            IsHitTestVisible = false
        };
        Children.Add(_scrollBarOverlay);
        _visualColor = GetMutedThumbColor();
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000d / 60), DispatcherPriority.Render, OnAnimationTick);
        _scrollHoldTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(ScrollVisibleHoldMs), DispatcherPriority.Background, OnScrollHoldElapsed);
        PointerWheelChanged += OnPointerWheelChanged;
        AddHandler(PointerMovedEvent, OnPointerMovedRouted, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerPressedRouted, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedRouted, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureScrollBarOverlayOnTop();
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        _extentHeight = 0;

        foreach (var child in Children)
        {
            if (child == _scrollBarOverlay)
                continue;

            child.Measure(new Size(width, double.PositiveInfinity));
            _extentHeight = Math.Max(_extentHeight, child.DesiredSize.Height);
        }

        _scrollBarOverlay.Measure(availableSize);
        ClampOffset(availableSize.Height);
        UpdateVisualState();
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureScrollBarOverlayOnTop();
        ClampOffset(finalSize.Height);

        foreach (var child in Children)
        {
            if (child == _scrollBarOverlay)
                continue;

            child.Arrange(new Rect(0, -_offsetY, finalSize.Width, Math.Max(_extentHeight, child.DesiredSize.Height)));
        }

        _scrollBarOverlay.Arrange(new Rect(finalSize));
        UpdateVisualState();
        return finalSize;
    }

    private void RenderScrollBar(DrawingContext context)
    {
        if (_visualOpacity <= 0.01 || !CanScroll)
            return;

        var thumb = GetThumbRect(Bounds.Size, _visualWidth);
        if (thumb.Height <= 0 || thumb.Width <= 0)
            return;

        var color = Color.FromArgb(
            (byte)Math.Round(_visualColor.A * _visualOpacity),
            _visualColor.R,
            _visualColor.G,
            _visualColor.B);
        context.DrawRectangle(new SolidColorBrush(color), null, thumb, thumb.Width / 2d, thumb.Width / 2d);
    }

    public bool ScrollByWheel(double deltaY)
    {
        var maxOffset = GetMaxOffset(Bounds.Height);
        if (maxOffset <= 0)
            return false;

        var nextOffset = Math.Clamp(_offsetY - deltaY * WheelScrollStep, 0, maxOffset);
        if (Math.Abs(nextOffset - _offsetY) < 0.1)
            return false;

        _offsetY = nextOffset;
        _isScrollHoldActive = true;
        _scrollHoldTimer.Stop();
        _scrollHoldTimer.Start();
        InvalidateArrange();
        UpdateVisualState();
        return true;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerInside = true;
        _lastPointerPosition = e.GetPosition(this);
        UpdateVisualState();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HandlePointerMoved(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerInside = false;
        _lastPointerPosition = null;
        UpdateVisualState();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        TryStartDragging(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        StopDragging(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDraggingThumb = false;
        UpdateVisualState();
    }

    private void OnPointerMovedRouted(object? sender, PointerEventArgs e)
    {
        HandlePointerMoved(e);
    }

    private void OnPointerPressedRouted(object? sender, PointerPressedEventArgs e)
    {
        TryStartDragging(e);
    }

    private void OnPointerReleasedRouted(object? sender, PointerReleasedEventArgs e)
    {
        StopDragging(e.Pointer);
    }

    private void HandlePointerMoved(PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        _lastPointerPosition = point;

        if (_isDraggingThumb)
        {
            ScrollByDrag(point.Y);
            e.Handled = true;
        }

        UpdateVisualState();
    }

    private void TryStartDragging(PointerPressedEventArgs e)
    {
        if (!CanScroll || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var point = e.GetPosition(this);
        _lastPointerPosition = point;
        if (!IsPointInThumb(point))
            return;

        _isDraggingThumb = true;
        _dragStartPointerY = point.Y;
        _dragStartOffsetY = _offsetY;
        e.Pointer.Capture(this);
        UpdateVisualState();
        e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (ScrollByWheel(e.Delta.Y))
            e.Handled = true;
    }

    private void StopDragging(IPointer pointer)
    {
        if (!_isDraggingThumb)
            return;

        _isDraggingThumb = false;
        pointer.Capture(null);
        UpdateVisualState();
    }

    private void ScrollByDrag(double pointerY)
    {
        var viewportHeight = Bounds.Height;
        var maxOffset = GetMaxOffset(viewportHeight);
        var trackHeight = Math.Max(1, viewportHeight);
        var thumbHeight = GetThumbHeight(viewportHeight);
        var travel = Math.Max(1, trackHeight - thumbHeight);
        var scrollRange = Math.Max(1, maxOffset);
        var deltaOffset = (pointerY - _dragStartPointerY) / travel * scrollRange;
        _offsetY = Math.Clamp(_dragStartOffsetY + deltaOffset, 0, maxOffset);
        InvalidateArrange();
        _scrollBarOverlay.InvalidateVisual();
    }

    private bool CanScroll => GetMaxOffset(Bounds.Height) > 0;

    private double GetMaxOffset(double viewportHeight)
    {
        if (double.IsInfinity(viewportHeight) || double.IsNaN(viewportHeight))
            return 0;

        return Math.Max(0, _extentHeight - viewportHeight);
    }

    private void ClampOffset(double viewportHeight)
    {
        _offsetY = Math.Clamp(_offsetY, 0, GetMaxOffset(viewportHeight));
    }

    private Rect GetThumbRect(Size viewport, double width)
    {
        var height = GetThumbHeight(viewport.Height);
        var maxOffset = GetMaxOffset(viewport.Height);
        var travel = Math.Max(0, viewport.Height - height);
        var top = maxOffset <= 0 ? 0 : _offsetY / maxOffset * travel;
        var left = Math.Max(0, viewport.Width - ThumbRightPadding - width);
        return new Rect(left, top, width, height);
    }

    private double GetThumbHeight(double viewportHeight)
    {
        if (_extentHeight <= 0 || viewportHeight <= 0)
            return 0;

        return Math.Clamp(viewportHeight / _extentHeight * viewportHeight, MinThumbHeight, viewportHeight);
    }

    private bool IsPointNearScrollbar(Point point)
    {
        return CanScroll && point.X >= Bounds.Width - NearHitWidth;
    }

    private bool IsPointInThumb(Point point)
    {
        if (!CanScroll)
            return false;

        var thumb = GetThumbRect(Bounds.Size, NormalThumbWidth);
        var hitRect = new Rect(Bounds.Width - NearHitWidth, thumb.Y, NearHitWidth, thumb.Height);
        return hitRect.Contains(point);
    }

    private void UpdateVisualState()
    {
        var nextState = ResolveTargetState();
        if (nextState == _targetState && _animationTimer.IsEnabled)
            return;

        if (nextState == _targetState)
        {
            _scrollBarOverlay.InvalidateVisual();
            return;
        }

        _animationStartState = new ScrollBarVisualState(_visualOpacity, _visualWidth, _visualColor);
        _targetState = nextState;
        _animationStartTime = Environment.TickCount64;
        if (!_animationTimer.IsEnabled)
            _animationTimer.Start();
    }

    private ScrollBarVisualState ResolveTargetState()
    {
        if (!CanScroll)
            return ScrollBarVisualState.Hidden;

        if (_isDraggingThumb)
            return ScrollBarVisualState.Highlight;

        if (_lastPointerPosition is { } point)
        {
            if (IsPointInThumb(point))
                return ScrollBarVisualState.Highlight;

            if (IsPointNearScrollbar(point))
                return ScrollBarVisualState.Normal;
        }

        if (_isPointerInside || _isScrollHoldActive)
            return ScrollBarVisualState.Thin;

        return ScrollBarVisualState.Hidden;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var elapsed = Environment.TickCount64 - _animationStartTime;
        var progress = Math.Clamp(elapsed / TransitionDurationMs, 0d, 1d);
        var eased = 1d - Math.Pow(1d - progress, 3d);
        var target = _targetState;

        _visualOpacity = Lerp(_animationStartState.Opacity, target.Opacity, eased);
        _visualWidth = Lerp(_animationStartState.Width, target.Width, eased);
        _visualColor = Lerp(_animationStartState.Color, target.Color, eased);
        _scrollBarOverlay.InvalidateVisual();

        if (progress >= 1)
            _animationTimer.Stop();
    }

    private void OnScrollHoldElapsed(object? sender, EventArgs e)
    {
        _scrollHoldTimer.Stop();
        _isScrollHoldActive = false;
        UpdateVisualState();
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

    private static Color GetMutedThumbColor()
    {
        var baseColor = ToColor(ThemeBrushes.TextSecondary, Color.Parse("#737373"));
        return Color.FromArgb(115, baseColor.R, baseColor.G, baseColor.B);
    }

    private static Color GetHighlightThumbColor()
    {
        var baseColor = ToColor(ThemeBrushes.PrimaryHover, Color.Parse("#0b5bcb"));
        return Color.FromArgb(210, baseColor.R, baseColor.G, baseColor.B);
    }

    private static Color ToColor(IBrush? brush, Color fallback)
    {
        return brush is ISolidColorBrush solid ? solid.Color : fallback;
    }

    private void EnsureScrollBarOverlayOnTop()
    {
        if (Children.Count == 0 || Children[^1] == _scrollBarOverlay)
            return;

        Children.Remove(_scrollBarOverlay);
        Children.Add(_scrollBarOverlay);
    }

    private sealed class ScrollBarOverlay : Control
    {
        private readonly PixelScrollHostBase _owner;

        public ScrollBarOverlay(PixelScrollHostBase owner)
        {
            _owner = owner;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            _owner.RenderScrollBar(context);
        }
    }

    private readonly record struct ScrollBarVisualState(double Opacity, double Width, Color Color)
    {
        public static ScrollBarVisualState Hidden => new(0d, ThinThumbWidth, GetMutedThumbColor());
        public static ScrollBarVisualState Thin => new(1d, ThinThumbWidth, GetMutedThumbColor());
        public static ScrollBarVisualState Normal => new(1d, NormalThumbWidth, GetMutedThumbColor());
        public static ScrollBarVisualState Highlight => new(1d, NormalThumbWidth, GetHighlightThumbColor());
    }
}
