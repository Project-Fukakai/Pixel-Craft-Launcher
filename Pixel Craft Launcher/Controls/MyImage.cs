using Avalonia;
using Avalonia.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyImage : Image
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty && Source is not null)
        {
            Opacity = 0;
            ModAnimation.AniStart(ModAnimation.AaOpacity(this, 1, 180, ease: new ModAnimation.AniEaseOutFluent()), $"MyImage Fade {GetHashCode()}");
        }
    }
}
