using Avalonia;

namespace PCL.Core.Utils;



public static class WpfUtils
{
    public static bool IsDependencyPropertySet(DependencyObject obj, DependencyProperty dp)
    {
        return obj.IsSet(dp);
    }
}
