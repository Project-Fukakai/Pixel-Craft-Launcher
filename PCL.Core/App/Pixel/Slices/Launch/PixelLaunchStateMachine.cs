using PCL.Core.App.Pixel.Infrastructure;

namespace PCL.Core.App.Pixel.Slices.Launch;

public enum PixelLaunchState
{
    Idle,
    Prechecking,
    Launching,
    WaitingForWindow,
    Running,
    Cancelling,
    Completed,
    Exited,
    Failed,
    Cancelled
}

public enum PixelLaunchTrigger
{
    Start,
    LaunchCoreStarted,
    ProcessStarted,
    FinishedWithoutProcess,
    WindowDetected,
    WindowProbeTimedOut,
    ProcessExited,
    ForceCloseRequested,
    Cancel,
    Fail
}

public sealed class PixelLaunchStateMachine
{
    private readonly PixelStateMachine<PixelLaunchState, PixelLaunchTrigger> _machine;

    public PixelLaunchStateMachine(IPixelStateMachineFactory factory)
    {
        _machine = factory.Create<PixelLaunchState, PixelLaunchTrigger>("PixelLaunch", PixelLaunchState.Idle);
        Configure();
    }

    public PixelLaunchState State => _machine.State;

    public bool CanFire(PixelLaunchTrigger trigger) => _machine.CanFire(trigger);

    public void Fire(PixelLaunchTrigger trigger) => _machine.Fire(trigger);

    private void Configure()
    {
        _machine.Configure(PixelLaunchState.Idle)
            .Permit(PixelLaunchTrigger.Start, PixelLaunchState.Prechecking);

        _machine.Configure(PixelLaunchState.Prechecking)
            .Permit(PixelLaunchTrigger.LaunchCoreStarted, PixelLaunchState.Launching)
            .Permit(PixelLaunchTrigger.Cancel, PixelLaunchState.Cancelled)
            .Permit(PixelLaunchTrigger.Fail, PixelLaunchState.Failed);

        _machine.Configure(PixelLaunchState.Launching)
            .Permit(PixelLaunchTrigger.ProcessStarted, PixelLaunchState.WaitingForWindow)
            .Permit(PixelLaunchTrigger.FinishedWithoutProcess, PixelLaunchState.Completed)
            .Permit(PixelLaunchTrigger.Cancel, PixelLaunchState.Cancelled)
            .Permit(PixelLaunchTrigger.Fail, PixelLaunchState.Failed);

        _machine.Configure(PixelLaunchState.WaitingForWindow)
            .Permit(PixelLaunchTrigger.WindowDetected, PixelLaunchState.Running)
            .Permit(PixelLaunchTrigger.WindowProbeTimedOut, PixelLaunchState.Running)
            .Permit(PixelLaunchTrigger.ProcessExited, PixelLaunchState.Exited)
            .Permit(PixelLaunchTrigger.ForceCloseRequested, PixelLaunchState.Cancelling)
            .Permit(PixelLaunchTrigger.Cancel, PixelLaunchState.Cancelled)
            .Permit(PixelLaunchTrigger.Fail, PixelLaunchState.Failed);

        _machine.Configure(PixelLaunchState.Running)
            .Permit(PixelLaunchTrigger.ForceCloseRequested, PixelLaunchState.Cancelling)
            .Permit(PixelLaunchTrigger.ProcessExited, PixelLaunchState.Exited)
            .Permit(PixelLaunchTrigger.Fail, PixelLaunchState.Failed);

        _machine.Configure(PixelLaunchState.Cancelling)
            .Permit(PixelLaunchTrigger.ProcessExited, PixelLaunchState.Exited)
            .Permit(PixelLaunchTrigger.Fail, PixelLaunchState.Failed);

        _machine.Configure(PixelLaunchState.Completed)
            .Permit(PixelLaunchTrigger.Start, PixelLaunchState.Prechecking);

        _machine.Configure(PixelLaunchState.Exited)
            .Permit(PixelLaunchTrigger.Start, PixelLaunchState.Prechecking);

        _machine.Configure(PixelLaunchState.Failed)
            .Permit(PixelLaunchTrigger.Start, PixelLaunchState.Prechecking);

        _machine.Configure(PixelLaunchState.Cancelled)
            .Permit(PixelLaunchTrigger.Start, PixelLaunchState.Prechecking);
    }
}
