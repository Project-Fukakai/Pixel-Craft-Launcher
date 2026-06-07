using Avalonia.Controls;

namespace Pixel_Craft_Launcher.Controls;

public class MyResizer : GridSplitter
{
    public MyResizer()
    {
        Width = 4;
        Background = ThemeBrushes.Border;
    }
}
