using PCL.Core.App.Pixel.Infrastructure;

namespace PCL.Core.App.Pixel.Slices.Download;

public enum PixelDownloadState
{
    Idle,
    ResolvingVersions,
    SelectingLoader,
    Downloading,
    Saving,
    Completed,
    Failed,
    Cancelled
}

public enum PixelDownloadTrigger
{
    StartVersionRefresh,
    VersionsResolved,
    OpenLoaderSelection,
    LoaderChoicesResolved,
    StartDownload,
    StartSave,
    Complete,
    Fail,
    Cancel,
    Reset
}

public sealed class PixelDownloadStateMachine
{
    private readonly PixelStateMachine<PixelDownloadState, PixelDownloadTrigger> _machine;

    public PixelDownloadStateMachine(IPixelStateMachineFactory factory)
    {
        _machine = factory.Create<PixelDownloadState, PixelDownloadTrigger>("PixelDownload", PixelDownloadState.Idle);
        Configure();
    }

    public PixelDownloadState State => _machine.State;

    public bool CanFire(PixelDownloadTrigger trigger) => _machine.CanFire(trigger);

    public void Fire(PixelDownloadTrigger trigger) => _machine.Fire(trigger);

    private void Configure()
    {
        _machine.Configure(PixelDownloadState.Idle)
            .Permit(PixelDownloadTrigger.StartVersionRefresh, PixelDownloadState.ResolvingVersions)
            .Permit(PixelDownloadTrigger.OpenLoaderSelection, PixelDownloadState.SelectingLoader)
            .Permit(PixelDownloadTrigger.StartDownload, PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.StartSave, PixelDownloadState.Saving);

        _machine.Configure(PixelDownloadState.ResolvingVersions)
            .Permit(PixelDownloadTrigger.VersionsResolved, PixelDownloadState.Completed)
            .Permit(PixelDownloadTrigger.Fail, PixelDownloadState.Failed)
            .Permit(PixelDownloadTrigger.Cancel, PixelDownloadState.Cancelled);

        _machine.Configure(PixelDownloadState.SelectingLoader)
            .Permit(PixelDownloadTrigger.LoaderChoicesResolved, PixelDownloadState.Completed)
            .Permit(PixelDownloadTrigger.StartDownload, PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.Fail, PixelDownloadState.Failed)
            .Permit(PixelDownloadTrigger.Cancel, PixelDownloadState.Cancelled);

        _machine.Configure(PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.Complete, PixelDownloadState.Completed)
            .Permit(PixelDownloadTrigger.Fail, PixelDownloadState.Failed)
            .Permit(PixelDownloadTrigger.Cancel, PixelDownloadState.Cancelled);

        _machine.Configure(PixelDownloadState.Saving)
            .Permit(PixelDownloadTrigger.Complete, PixelDownloadState.Completed)
            .Permit(PixelDownloadTrigger.Fail, PixelDownloadState.Failed)
            .Permit(PixelDownloadTrigger.Cancel, PixelDownloadState.Cancelled);

        _machine.Configure(PixelDownloadState.Completed)
            .Permit(PixelDownloadTrigger.Reset, PixelDownloadState.Idle)
            .Permit(PixelDownloadTrigger.StartVersionRefresh, PixelDownloadState.ResolvingVersions)
            .Permit(PixelDownloadTrigger.OpenLoaderSelection, PixelDownloadState.SelectingLoader)
            .Permit(PixelDownloadTrigger.StartDownload, PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.StartSave, PixelDownloadState.Saving);

        _machine.Configure(PixelDownloadState.Failed)
            .Permit(PixelDownloadTrigger.Reset, PixelDownloadState.Idle)
            .Permit(PixelDownloadTrigger.StartVersionRefresh, PixelDownloadState.ResolvingVersions)
            .Permit(PixelDownloadTrigger.OpenLoaderSelection, PixelDownloadState.SelectingLoader)
            .Permit(PixelDownloadTrigger.StartDownload, PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.StartSave, PixelDownloadState.Saving);

        _machine.Configure(PixelDownloadState.Cancelled)
            .Permit(PixelDownloadTrigger.Reset, PixelDownloadState.Idle)
            .Permit(PixelDownloadTrigger.StartVersionRefresh, PixelDownloadState.ResolvingVersions)
            .Permit(PixelDownloadTrigger.OpenLoaderSelection, PixelDownloadState.SelectingLoader)
            .Permit(PixelDownloadTrigger.StartDownload, PixelDownloadState.Downloading)
            .Permit(PixelDownloadTrigger.StartSave, PixelDownloadState.Saving);
    }
}
