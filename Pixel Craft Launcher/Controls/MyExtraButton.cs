using Avalonia;

namespace Pixel_Craft_Launcher.Controls;

public class MyExtraButton : MyIconButton
{
    public MyExtraButton()
    {
        Variant = MyButtonVariant.Flat;
        Width = 36;
        Height = 36;
        IconSize = 16;
        Icon = "mdi-star";
    }
}
