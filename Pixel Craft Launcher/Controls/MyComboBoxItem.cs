using System;
using Avalonia.Controls;

namespace Pixel_Craft_Launcher.Controls;

public class MyComboBoxItem : ComboBoxItem
{
    protected override Type StyleKeyOverride => typeof(ComboBoxItem);
}
