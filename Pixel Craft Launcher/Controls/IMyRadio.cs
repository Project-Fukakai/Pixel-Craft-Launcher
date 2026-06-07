namespace Pixel_Craft_Launcher.Controls;

public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, MyRouteEventArgs e);

    delegate void CheckEventHandler(object sender, MyRouteEventArgs e);

    event CheckEventHandler? Check;

    event ChangedEventHandler? Changed;
}
