using System;
using System.Collections.Generic;

namespace Pixel_Craft_Launcher.Routing;

public interface INavigationService
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

public sealed class PixelRouter(RouteNode initialRoute) : INavigationService
{
    private readonly Stack<RouteNode> _backStack = [];
    private bool _hasNavigated = true;

    public RouteNode CurrentRoute { get; private set; } = initialRoute;

    public bool CanGoBack => _backStack.Count > 0;

    public event EventHandler<PixelRouteChangedEventArgs>? CurrentRouteChanged;

    public bool Navigate(RouteNode route)
    {
        if (route.Key == CurrentRoute.Key && route.ToString() == CurrentRoute.ToString())
            return false;

        var oldRoute = CurrentRoute;
        if (_hasNavigated)
            _backStack.Push(oldRoute);
        CurrentRoute = route;
        var isInitial = !_hasNavigated;
        _hasNavigated = true;
        CurrentRouteChanged?.Invoke(this, new PixelRouteChangedEventArgs(oldRoute, route, isInitial));
        return true;
    }

    public bool Back()
    {
        if (_backStack.Count == 0)
            return false;

        var oldRoute = CurrentRoute;
        CurrentRoute = _backStack.Pop();
        _hasNavigated = true;
        CurrentRouteChanged?.Invoke(this, new PixelRouteChangedEventArgs(oldRoute, CurrentRoute, false));
        return true;
    }
}
