using System;
using PCL.Core.UI.Theme;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelColorSchemeThemeBridge
{
    public bool IsDarkMode => ThemeService.IsDarkMode;

    public void RefreshColorScheme()
    {
        ThemeService.RefreshColorScheme();
    }

    public IDisposable SubscribeColorModeChanged(Action callback)
    {
        ColorModeChangedEvent? handler = null;
        handler = (_, _) => callback();
        ThemeService.ColorModeChanged += handler;
        return new Subscription(() => ThemeService.ColorModeChanged -= handler);
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose()
        {
            dispose();
        }
    }
}
