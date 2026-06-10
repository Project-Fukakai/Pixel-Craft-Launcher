using System;
using PCL.Core.App.Pixel.Infrastructure;

namespace Pixel_Craft_Launcher.Controls;

public sealed class PixelLoadingTriggerAdapter : ILoadingTrigger, IDisposable
{
    private readonly PixelLoadingStateController _source;

    public PixelLoadingTriggerAdapter(PixelLoadingStateController source)
    {
        _source = source;
        _source.StateChanged += OnStateChanged;
        _source.ProgressChanged += OnProgressChanged;
    }

    public MyLoadingState LoadingState => Map(_source.State);

    public double Progress => _source.Progress;

    public Exception? Error => _source.Error;

    public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;

    public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

    public void Dispose()
    {
        _source.StateChanged -= OnStateChanged;
        _source.ProgressChanged -= OnProgressChanged;
    }

    private void OnStateChanged(object? sender, PixelLoadingStateChangedEventArgs e)
    {
        LoadingStateChanged?.Invoke(this, Map(e.State), Map(e.OldState));
    }

    private void OnProgressChanged(object? sender, double progress)
    {
        ProgressChanged?.Invoke(this, progress);
    }

    private static MyLoadingState Map(PixelLoadingState state)
    {
        return state switch
        {
            PixelLoadingState.Loading => MyLoadingState.Loading,
            PixelLoadingState.Run => MyLoadingState.Run,
            PixelLoadingState.Stop => MyLoadingState.Stop,
            PixelLoadingState.Error => MyLoadingState.Error,
            _ => MyLoadingState.Unloaded
        };
    }
}
