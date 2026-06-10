using PCL.Core.App.Pixel.Infrastructure;

namespace PCL.Core.App.Pixel.Slices.GameLink;

public enum PixelGameLinkState
{
    Ready,
    EulaRequired,
    InstallingDependency,
    Authenticating,
    CheckingNetwork,
    Hosting,
    Joining,
    Connected,
    Leaving,
    Failed
}

public enum PixelGameLinkTrigger
{
    RequireEula,
    AcceptEula,
    StartDependencyInstall,
    DependencyReady,
    StartLogin,
    LoginSucceeded,
    LoginFailed,
    StartNetworkCheck,
    NetworkCheckSucceeded,
    NetworkCheckFailed,
    StartHosting,
    StartJoining,
    OperationSucceeded,
    OperationFailed,
    Leave,
    Left,
    Fail,
    Reset
}

public sealed class PixelGameLinkStateMachine
{
    private readonly PixelStateMachine<PixelGameLinkState, PixelGameLinkTrigger> _machine;

    public PixelGameLinkStateMachine(IPixelStateMachineFactory factory)
    {
        _machine = factory.Create<PixelGameLinkState, PixelGameLinkTrigger>("PixelGameLink", PixelGameLinkState.Ready);
        Configure();
    }

    public PixelGameLinkState State => _machine.State;

    public bool CanFire(PixelGameLinkTrigger trigger) => _machine.CanFire(trigger);

    public void Fire(PixelGameLinkTrigger trigger) => _machine.Fire(trigger);

    private void Configure()
    {
        _machine.Configure(PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.RequireEula, PixelGameLinkState.EulaRequired)
            .Permit(PixelGameLinkTrigger.StartDependencyInstall, PixelGameLinkState.InstallingDependency)
            .Permit(PixelGameLinkTrigger.StartLogin, PixelGameLinkState.Authenticating)
            .Permit(PixelGameLinkTrigger.StartNetworkCheck, PixelGameLinkState.CheckingNetwork)
            .Permit(PixelGameLinkTrigger.StartHosting, PixelGameLinkState.Hosting)
            .Permit(PixelGameLinkTrigger.StartJoining, PixelGameLinkState.Joining)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.EulaRequired)
            .Permit(PixelGameLinkTrigger.AcceptEula, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.Reset, PixelGameLinkState.Ready);

        _machine.Configure(PixelGameLinkState.InstallingDependency)
            .Permit(PixelGameLinkTrigger.DependencyReady, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.OperationFailed, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.Authenticating)
            .Permit(PixelGameLinkTrigger.LoginSucceeded, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.LoginFailed, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.CheckingNetwork)
            .Permit(PixelGameLinkTrigger.NetworkCheckSucceeded, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.NetworkCheckFailed, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.Hosting)
            .Permit(PixelGameLinkTrigger.OperationSucceeded, PixelGameLinkState.Connected)
            .Permit(PixelGameLinkTrigger.RequireEula, PixelGameLinkState.EulaRequired)
            .Permit(PixelGameLinkTrigger.OperationFailed, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.Joining)
            .Permit(PixelGameLinkTrigger.OperationSucceeded, PixelGameLinkState.Connected)
            .Permit(PixelGameLinkTrigger.RequireEula, PixelGameLinkState.EulaRequired)
            .Permit(PixelGameLinkTrigger.OperationFailed, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.Connected)
            .Permit(PixelGameLinkTrigger.Leave, PixelGameLinkState.Leaving)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Reset, PixelGameLinkState.Ready);

        _machine.Configure(PixelGameLinkState.Leaving)
            .Permit(PixelGameLinkTrigger.Left, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.Fail, PixelGameLinkState.Failed);

        _machine.Configure(PixelGameLinkState.Failed)
            .Permit(PixelGameLinkTrigger.Reset, PixelGameLinkState.Ready)
            .Permit(PixelGameLinkTrigger.RequireEula, PixelGameLinkState.EulaRequired)
            .Permit(PixelGameLinkTrigger.StartDependencyInstall, PixelGameLinkState.InstallingDependency)
            .Permit(PixelGameLinkTrigger.StartLogin, PixelGameLinkState.Authenticating)
            .Permit(PixelGameLinkTrigger.StartNetworkCheck, PixelGameLinkState.CheckingNetwork)
            .Permit(PixelGameLinkTrigger.StartHosting, PixelGameLinkState.Hosting)
            .Permit(PixelGameLinkTrigger.StartJoining, PixelGameLinkState.Joining);
    }
}
