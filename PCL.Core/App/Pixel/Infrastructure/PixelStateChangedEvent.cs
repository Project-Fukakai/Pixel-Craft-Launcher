namespace PCL.Core.App.Pixel.Infrastructure;

public sealed record PixelStateChangedEvent(
    string MachineName,
    string SourceState,
    string DestinationState,
    string Trigger);

