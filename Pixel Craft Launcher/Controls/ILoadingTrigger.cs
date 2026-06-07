using System;

namespace Pixel_Craft_Launcher.Controls;

public interface ILoadingTrigger
{
    delegate void LoadingStateChangedEventHandler(ILoadingTrigger sender, MyLoadingState newState, MyLoadingState oldState);

    delegate void ProgressChangedEventHandler(ILoadingTrigger sender, double progress);

    MyLoadingState LoadingState { get; }

    double Progress { get; }

    Exception? Error { get; }

    event LoadingStateChangedEventHandler? LoadingStateChanged;

    event ProgressChangedEventHandler? ProgressChanged;
}
