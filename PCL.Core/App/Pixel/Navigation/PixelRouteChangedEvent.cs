namespace PCL.Core.App.Pixel.Navigation;

public sealed record PixelRouteChangedEvent(RouteNode OldRoute, RouteNode NewRoute, bool IsInitial);

