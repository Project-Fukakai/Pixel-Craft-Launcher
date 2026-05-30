using System;
using Avalonia.Threading;

namespace PCL.Core.UI.Animation.Clock;

public class WpfCompositionTargetRenderingClock(int fps = 60) : IUIClock, IDisposable
{
    private DispatcherTimer? _timer;
    private long _lastFrame;

    public event EventHandler<long>? Tick;

    public int Fps { get; set; } = fps;

    public bool IsRunning => _timer?.IsEnabled == true;

    public void Start()
    {
        if (IsRunning) return;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000d / Math.Max(1, Fps == int.MaxValue ? 60 : Fps))
        };
        _timer.Tick += (_, _) => Tick?.Invoke(this, ++_lastFrame);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
