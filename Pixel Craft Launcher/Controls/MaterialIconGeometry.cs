using Avalonia.Media;
using Projektanker.Icons.Avalonia;

namespace Pixel_Craft_Launcher.Controls;

internal static class MaterialIconGeometry
{
    public static Geometry? TryGet(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        try
        {
            return Geometry.Parse(IconProvider.Current.GetIcon(icon).Path.ToString());
        }
        catch
        {
            return null;
        }
    }
}
