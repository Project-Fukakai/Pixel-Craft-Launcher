using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel.Navigation;

public sealed class PixelRouter : IPixelNavigationService
{
    private readonly IPixelEventBus? _eventBus;
    private readonly ILogger<PixelRouter>? _logger;
    private readonly Stack<RouteNode> _backStack = [];
    private bool _hasNavigated = true;

    public PixelRouter()
        : this(PixelRoutes.Launch())
    {
    }

    public PixelRouter(RouteNode initialRoute, IPixelEventBus? eventBus = null, ILogger<PixelRouter>? logger = null)
    {
        CurrentRoute = initialRoute;
        _eventBus = eventBus;
        _logger = logger;
    }

    public RouteNode CurrentRoute { get; private set; }

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
        PublishRouteChanged(oldRoute, route, isInitial);
        return true;
    }

    public bool Back()
    {
        if (_backStack.Count == 0)
            return false;

        var oldRoute = CurrentRoute;
        CurrentRoute = _backStack.Pop();
        _hasNavigated = true;
        PublishRouteChanged(oldRoute, CurrentRoute, false);
        return true;
    }

    private void PublishRouteChanged(RouteNode oldRoute, RouteNode newRoute, bool isInitial)
    {
        _logger?.LogDebug("Pixel route changed {OldRoute} -> {NewRoute}", oldRoute, newRoute);
        var args = new PixelRouteChangedEventArgs(oldRoute, newRoute, isInitial);
        CurrentRouteChanged?.Invoke(this, args);
        _eventBus?.Publish(new PixelRouteChangedEvent(oldRoute, newRoute, isInitial));
    }
}

