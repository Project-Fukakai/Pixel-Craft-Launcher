using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Pixel_Craft_Launcher.Controls.Behaviors;

public static class LazyLoadBehavior
{
    public static readonly AttachedProperty<Action?> ActionProperty =
        AvaloniaProperty.RegisterAttached<LazyLoadBehaviorHost, Control, Action?>("Action");

    static LazyLoadBehavior()
    {
        ActionProperty.Changed.AddClassHandler<Control>(OnActionChanged);
    }

    public static void SetAction(Control element, Action? value)
    {
        element.SetValue(ActionProperty, value);
    }

    public static Action? GetAction(Control element)
    {
        return element.GetValue(ActionProperty);
    }

    private static void OnActionChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.LayoutUpdated -= OnLayoutUpdated;
        control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        if (e.NewValue is null)
            return;

        control.LayoutUpdated += OnLayoutUpdated;
        control.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control)
            return;
        control.LayoutUpdated -= OnLayoutUpdated;
        control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not Control control || !control.IsEffectivelyVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
            return;

        var scrollViewer = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is null)
            return;

        var point = control.TranslatePoint(new Point(0, 0), scrollViewer);
        if (point is null)
            return;

        var elementBounds = new Rect(point.Value, control.Bounds.Size);
        var viewport = new Rect(new Point(scrollViewer.Offset.X, scrollViewer.Offset.Y), scrollViewer.Viewport);
        if (!viewport.Intersects(elementBounds))
            return;

        var action = GetAction(control);
        SetAction(control, null);
        action?.Invoke();
    }

    private sealed class LazyLoadBehaviorHost;
}
