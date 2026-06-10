namespace PCL.Core.App.Pixel.Infrastructure;

public enum PixelLoadingState
{
    Unloaded,
    Loading,
    Run,
    Stop,
    Error
}

public sealed class PixelLoadingStateController
{
    private PixelLoadingState _state;
    private double _progress;

    public PixelLoadingState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;
            var old = _state;
            _state = value;
            StateChanged?.Invoke(this, new PixelLoadingStateChangedEventArgs(_state, old));
        }
    }

    public Exception? Error { get; set; }

    public double Progress
    {
        get => _progress;
        private set
        {
            var next = Math.Clamp(value, 0, 1);
            if (Math.Abs(_progress - next) < 0.0001)
                return;
            _progress = next;
            ProgressChanged?.Invoke(this, _progress);
        }
    }

    public event EventHandler<PixelLoadingStateChangedEventArgs>? StateChanged;

    public event EventHandler<double>? ProgressChanged;

    public void SetProgress(double progress)
    {
        Progress = progress;
    }
}

public sealed class PixelLoadingStateChangedEventArgs : EventArgs
{
    public PixelLoadingStateChangedEventArgs(PixelLoadingState state, PixelLoadingState oldState)
    {
        State = state;
        OldState = oldState;
    }

    public PixelLoadingState State { get; }

    public PixelLoadingState OldState { get; }
}
