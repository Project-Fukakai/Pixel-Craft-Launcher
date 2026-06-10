namespace PCL.Core.App.Pixel.Navigation;

public interface IPixelNavigationService
{
    RouteNode CurrentRoute { get; }

    bool CanGoBack { get; }

    event EventHandler<PixelRouteChangedEventArgs>? CurrentRouteChanged;

    bool Navigate(RouteNode route);

    bool Back();
}

public sealed class PixelRouteChangedEventArgs(RouteNode oldRoute, RouteNode newRoute, bool isInitial)
    : EventArgs
{
    public RouteNode OldRoute { get; } = oldRoute;

    public RouteNode NewRoute { get; } = newRoute;

    public bool IsInitial { get; } = isInitial;
}

