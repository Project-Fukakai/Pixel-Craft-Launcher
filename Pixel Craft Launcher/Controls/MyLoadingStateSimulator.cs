using System;

namespace Pixel_Craft_Launcher.Controls;

public class MyLoadingStateSimulator : ILoadingTrigger
{
    private MyLoadingState _loadingState;

    public MyLoadingState LoadingState
    {
        get => _loadingState;
        set
        {
            if (_loadingState == value)
                return;
            var old = _loadingState;
            _loadingState = value;
            LoadingStateChanged?.Invoke(this, _loadingState, old);
        }
    }

    public Exception? Error { get; set; }

    public double Progress { get; private set; }

    public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;

    public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

    public void SetProgress(double progress)
    {
        Progress = Math.Clamp(progress, 0, 1);
        ProgressChanged?.Invoke(this, Progress);
    }
}
