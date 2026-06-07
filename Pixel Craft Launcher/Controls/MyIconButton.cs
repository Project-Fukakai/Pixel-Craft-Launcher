using Avalonia;

namespace Pixel_Craft_Launcher.Controls;

public class MyIconButton : MyButton
{
    public MyIconButton()
    {
        Variant = MyButtonVariant.Text;
        PreserveForeground = true;
        Width = 40;
        Height = 40;
        Size = MyButtonSize.Medium;
        IconSize = 20;
        UseAutoContent();
    }
}
