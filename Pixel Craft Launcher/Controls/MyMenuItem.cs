using Avalonia.Controls;

namespace Pixel_Craft_Launcher.Controls;

public class MyMenuItem : MenuItem
{
    public MyMenuItem()
    {
        Padding = new Avalonia.Thickness(10, 6);
        Foreground = ThemeBrushes.Text;
    }
}
