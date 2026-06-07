using Avalonia.Media;
using PCL.Core.UI.Theme;

namespace Pixel_Craft_Launcher.Controls;

internal static class ThemeBrushes
{
    public static IBrush Primary => Brush(PixelThemeResources.Primary, "#1370f3");
    public static IBrush PrimaryHover => Brush(PixelThemeResources.PrimaryHover, "#0b5bcb");
    public static IBrush PrimaryContainer => Brush(PixelThemeResources.PrimaryContainer, "#d5e6fd");
    public static IBrush OnPrimaryContainer => Brush(PixelThemeResources.OnPrimaryContainer, "#001b3d");
    public static IBrush OnPrimaryContainerHover => BrushWithAlpha(PixelThemeResources.OnPrimaryContainer, "#001b3d", 38);
    public static IBrush OnPrimaryContainerPressed => BrushWithAlpha(PixelThemeResources.OnPrimaryContainer, "#001b3d", 64);
    public static IBrush OnPrimary => Brush(PixelThemeResources.OnPrimary, "#ffffff");
    public static IBrush Text => Brush(PixelThemeResources.Text, "#343d4a");
    public static IBrush TextSecondary => Brush(PixelThemeResources.TextSecondary, "#737373");
    public static IBrush TextDisabled => Brush(PixelThemeResources.TextDisabled, "#a6a6a6");
    public static IBrush Background => Brush(PixelThemeResources.Background, "#fbfbfb");
    public static IBrush TransparentBackground => Brush(PixelThemeResources.TransparentBackground, "#d2fbfbfb");
    public static IBrush Surface => Brush(PixelThemeResources.Surface, "#ffffff");
    public static IBrush SurfaceVariant => Brush(PixelThemeResources.SurfaceVariant, "#eaf2fe");
    public static IBrush CardBackground => Brush(PixelThemeResources.CardBackground, "#d2fbfbfb");
    public static IBrush Container => Brush(PixelThemeResources.Container, "#f5f5f5");
    public static IBrush ContainerHigh => Brush(PixelThemeResources.ContainerHigh, "#f0f0f0");
    public static IBrush Border => Brush(PixelThemeResources.Border, "#d5e6fd");
    public static IBrush BorderStrong => Brush(PixelThemeResources.BorderStrong, "#cccccc");
    public static IBrush SidebarBackground => Brush(PixelThemeResources.SidebarBackground, "#ffffff");
    public static IBrush SidebarBorder => Brush(PixelThemeResources.SidebarBorder, "#d5e6fd");
    public static IBrush ShellBackground => Brush(PixelThemeResources.ShellBackground, "#f5f9ff");
    public static IBrush InputBackground => Brush(PixelThemeResources.InputBackground, "#ffffff");
    public static IBrush InputHoverBackground => Brush(PixelThemeResources.InputHoverBackground, "#e0eafd");
    public static IBrush InputBorder => Brush(PixelThemeResources.InputBorder, "#d5e6fd");
    public static IBrush InputDisabledBackground => Brush(PixelThemeResources.InputDisabledBackground, "#ebebeb");
    public static IBrush SliderTrack => Brush(PixelThemeResources.SliderTrack, "#e0eafd");
    public static IBrush ListItemHover => Brush(PixelThemeResources.ListItemHover, "#e0eafd");
    public static IBrush ButtonHoverBackground => Brush(PixelThemeResources.ButtonHoverBackground, "#e0eafd");
    public static IBrush ButtonDangerHoverBackground => Brush(PixelThemeResources.ButtonDangerHoverBackground, "#80fbdddd");
    public static IBrush Overlay => Brush(PixelThemeResources.Overlay, "#66000000");
    public static IBrush HalfWhite => Brush(PixelThemeResources.HalfWhite, "#55ffffff");
    public static IBrush White => Brush(PixelThemeResources.White, "#ffffff");
    public static IBrush Transparent => Brush(PixelThemeResources.Transparent, "#00ffffff");
    public static IBrush Error => Brush(PixelThemeResources.Error, "#ce2111");
    public static IBrush ErrorContainer => Brush(PixelThemeResources.ErrorContainer, "#80fbdddd");
    public static IBrush Warning => Brush(PixelThemeResources.Warning, "#9b6400");
    public static IBrush WarningContainer => Brush(PixelThemeResources.WarningContainer, "#fff1bf");
    public static IBrush Success => Brush(PixelThemeResources.Success, "#218e21");
    public static IBrush SuccessContainer => Brush(PixelThemeResources.SuccessContainer, "#d7f5d7");
    public static IBrush Info => Brush(PixelThemeResources.Info, "#1370f3");
    public static IBrush InfoContainer => Brush(PixelThemeResources.InfoContainer, "#d5e6fd");
    public static IBrush MemoryUsed => Brush(PixelThemeResources.MemoryUsed, "#0b5bcb");
    public static IBrush MemoryGame => Brush(PixelThemeResources.MemoryGame, "#1370f3");
    public static IBrush MemoryFree => Brush(PixelThemeResources.MemoryFree, "#d5e6fd");

    public static Color PrimaryColor => Color(PixelThemeResources.Primary, "#1370f3");
    public static Color PrimaryHoverColor => Color(PixelThemeResources.PrimaryHover, "#0b5bcb");
    public static Color TextDisabledColor => Color(PixelThemeResources.TextDisabled, "#a6a6a6");
    public static Color SidebarBackgroundColor => Color(PixelThemeResources.SidebarBackground, "#ffffff");
    public static Color InfoColor => Color(PixelThemeResources.Info, "#1370f3");
    public static Color SuccessColor => Color(PixelThemeResources.Success, "#218e21");
    public static Color SuccessContainerColor => Color(PixelThemeResources.SuccessContainer, "#d7f5d7");
    public static Color ErrorColor => Color(PixelThemeResources.Error, "#ce2111");
    public static Color ErrorContainerColor => Color(PixelThemeResources.ErrorContainer, "#80fbdddd");
    public static Color ShadowColor => Color(PixelThemeResources.Shadow, "#201f2d40");

    private static IBrush Brush(string token, string fallback)
    {
        var key = PixelThemeResources.BrushKey(token);
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Avalonia.Media.Color.Parse(fallback));
    }

    private static Color Color(string token, string fallback)
    {
        var key = PixelThemeResources.ColorKey(token);
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true &&
            value is Color color)
        {
            return color;
        }

        return Avalonia.Media.Color.Parse(fallback);
    }

    private static IBrush BrushWithAlpha(string token, string fallback, byte alpha)
    {
        var color = Color(token, fallback);
        return new SolidColorBrush(Avalonia.Media.Color.FromArgb(alpha, color.R, color.G, color.B));
    }
}
