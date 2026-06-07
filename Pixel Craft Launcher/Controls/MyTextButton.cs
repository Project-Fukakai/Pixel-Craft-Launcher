using Avalonia;

namespace Pixel_Craft_Launcher.Controls;

public class MyTextButton : MyButton
{
    public MyTextButton()
    {
        Variant = MyButtonVariant.Text;
        Size = MyButtonSize.Small;
    }
}
