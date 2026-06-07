using System;

namespace Pixel_Craft_Launcher.Controls;

public class MyRouteEventArgs : EventArgs
{
    public MyRouteEventArgs(bool user = false)
    {
        User = user;
    }

    public bool User { get; }

    public bool Handled { get; set; }
}
