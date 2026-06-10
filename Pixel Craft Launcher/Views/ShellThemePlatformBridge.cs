using System;
using PCL.Core.UI.Theme;

namespace Pixel_Craft_Launcher.Views;

internal sealed class ShellThemePlatformBridge
{
    public bool IsDarkMode => ThemeService.IsDarkMode;

    public IDisposable SubscribeThemeChanged(Action callback)
    {
        ColorModeChangedEvent colorModeHandler = (_, _) => callback();
        ColorThemeChangedEvent colorThemeHandler = _ => callback();
        ThemeService.ColorModeChanged += colorModeHandler;
        ThemeService.ColorThemeChanged += colorThemeHandler;
        return new Subscription(() =>
        {
            ThemeService.ColorModeChanged -= colorModeHandler;
            ThemeService.ColorThemeChanged -= colorThemeHandler;
        });
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose()
        {
            dispose();
        }
    }
}
