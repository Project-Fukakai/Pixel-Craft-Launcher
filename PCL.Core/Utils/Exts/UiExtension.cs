namespace PCL.Core.Utils.Exts;

using System;
using Avalonia.Controls;
using Avalonia.Media;

public static class UiExtension
{
    public static bool IsVisibleInWindow(this FrameworkElement element, Window mainWindow)
    {
        if (!element.IsVisible) return false;
        var bounds = element.Bounds;
        var windowBounds = mainWindow.Bounds;
        return bounds.Width > 0 && bounds.Height > 0 && windowBounds.Width > 0 && windowBounds.Height > 0;
    }

    public static bool IsTextTrimmed(this TextBlock textBlock)
    {
        return false;
    }
}
