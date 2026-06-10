using PCL.Core.App.Pixel.Infrastructure;

namespace PCL.Core.App.Pixel.Slices.Profiles;

public enum PixelProfileState
{
    Ready,
    SavingOfflineProfile,
    AuthenticatingMicrosoft,
    AuthenticatingAuthlib,
    SavingAuthServer,
    SelectingProfile,
    RemovingProfile,
    Completed,
    Failed
}

public enum PixelProfileTrigger
{
    StartOfflineSave,
    StartMicrosoftLogin,
    StartAuthlibLogin,
    StartAuthServerSave,
    StartSelect,
    StartRemove,
    Succeed,
    Fail,
    Reset
}

public sealed class PixelProfileStateMachine
{
    private readonly PixelStateMachine<PixelProfileState, PixelProfileTrigger> _machine;

    public PixelProfileStateMachine(IPixelStateMachineFactory factory)
    {
        _machine = factory.Create<PixelProfileState, PixelProfileTrigger>("PixelProfile", PixelProfileState.Ready);
        Configure();
    }

    public PixelProfileState State => _machine.State;

    public bool CanFire(PixelProfileTrigger trigger) => _machine.CanFire(trigger);

    public void Fire(PixelProfileTrigger trigger) => _machine.Fire(trigger);

    private void Configure()
    {
        ConfigureStartable(PixelProfileState.Ready);
        ConfigureStartable(PixelProfileState.Completed);
        ConfigureStartable(PixelProfileState.Failed);

        ConfigureOperation(PixelProfileState.SavingOfflineProfile);
        ConfigureOperation(PixelProfileState.AuthenticatingMicrosoft);
        ConfigureOperation(PixelProfileState.AuthenticatingAuthlib);
        ConfigureOperation(PixelProfileState.SavingAuthServer);
        ConfigureOperation(PixelProfileState.SelectingProfile);
        ConfigureOperation(PixelProfileState.RemovingProfile);
    }

    private void ConfigureStartable(PixelProfileState state)
    {
        var configuration = _machine.Configure(state)
            .Permit(PixelProfileTrigger.StartOfflineSave, PixelProfileState.SavingOfflineProfile)
            .Permit(PixelProfileTrigger.StartMicrosoftLogin, PixelProfileState.AuthenticatingMicrosoft)
            .Permit(PixelProfileTrigger.StartAuthlibLogin, PixelProfileState.AuthenticatingAuthlib)
            .Permit(PixelProfileTrigger.StartAuthServerSave, PixelProfileState.SavingAuthServer)
            .Permit(PixelProfileTrigger.StartSelect, PixelProfileState.SelectingProfile)
            .Permit(PixelProfileTrigger.StartRemove, PixelProfileState.RemovingProfile);

        if (state != PixelProfileState.Ready)
            configuration.Permit(PixelProfileTrigger.Reset, PixelProfileState.Ready);
    }

    private void ConfigureOperation(PixelProfileState state)
    {
        _machine.Configure(state)
            .Permit(PixelProfileTrigger.Succeed, PixelProfileState.Completed)
            .Permit(PixelProfileTrigger.Fail, PixelProfileState.Failed);
    }
}
