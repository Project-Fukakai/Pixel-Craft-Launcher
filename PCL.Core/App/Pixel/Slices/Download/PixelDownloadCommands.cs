using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MediatR;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Download;

public sealed record RefreshMinecraftVersionsCommand
    : IRequest<IReadOnlyList<MinecraftVersionManifestEntry>>;

public sealed record RefreshMinecraftLoaderChoicesCommand(string VersionId) : IRequest<IReadOnlyList<PixelLoaderChoiceGroup>>;

internal sealed record PixelDownloadInstallVersion(string Id, string Url)
{
    internal static PixelDownloadInstallVersion FromVersion(MinecraftVersionManifestEntry version) =>
        new(version.Id, version.Url);

    internal MinecraftVersionManifestEntry ToVersionManifestEntry() =>
        new(Id, "release", Url, DateTime.MinValue, DateTime.MinValue);
}

internal sealed record PixelDownloadInstallLoader(
    MinecraftLoaderKind Kind,
    string? Version,
    bool SaveServerJar)
{
    internal static PixelDownloadInstallLoader FromSelection(MinecraftLoaderSelection selection) =>
        new(selection.Kind, selection.Version, selection.SaveServerJar);

    internal MinecraftLoaderSelection ToSelection() => new(Kind, Version, SaveServerJar);
}

internal sealed record PixelDownloadInstallLoaderEntry(
    MinecraftLoaderKind Kind,
    string Version,
    string DisplayName,
    string? MinecraftVersion,
    bool IsStable,
    bool IsRecommended,
    MinecraftRemoteSource Source,
    string? DownloadUrl,
    string? Sha1,
    string? FileName,
    string? Category,
    DateTime? ReleaseTime,
    JsonObject? Metadata)
{
    internal static PixelDownloadInstallLoaderEntry? FromEntry(MinecraftLoaderVersionEntry? entry) =>
        entry is null
            ? null
            : new(
                entry.Kind,
                entry.Version,
                entry.DisplayName,
                entry.MinecraftVersion,
                entry.IsStable,
                entry.IsRecommended,
                entry.Source,
                entry.DownloadUrl,
                entry.Sha1,
                entry.FileName,
                entry.Category,
                entry.ReleaseTime,
                entry.Metadata);

    internal MinecraftLoaderVersionEntry ToEntry() =>
        new(
            Kind,
            Version,
            DisplayName,
            MinecraftVersion,
            IsStable,
            IsRecommended,
            Source,
            DownloadUrl,
            Sha1,
            FileName,
            Category,
            ReleaseTime,
            Metadata);
}

internal sealed record PixelDownloadInstallAddonEntry(
    MinecraftAddonKind Kind,
    string Id,
    string DisplayName,
    string FileName,
    IReadOnlyList<string> DownloadUrls,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<MinecraftLoaderKind> Loaders,
    string? Sha1,
    long? Size,
    bool IsStable,
    DateTime? ReleaseTime,
    MinecraftRemoteSource Source)
{
    internal static PixelDownloadInstallAddonEntry? FromEntry(MinecraftAddonFileEntry? entry) =>
        entry is null
            ? null
            : new(
                entry.Kind,
                entry.Id,
                entry.DisplayName,
                entry.FileName,
                entry.DownloadUrls,
                entry.GameVersions,
                entry.Loaders,
                entry.Sha1,
                entry.Size,
                entry.IsStable,
                entry.ReleaseTime,
                entry.Source);

    internal MinecraftAddonFileEntry ToEntry() =>
        new(
            Kind,
            Id,
            DisplayName,
            FileName,
            DownloadUrls,
            GameVersions,
            Loaders,
            Sha1,
            Size,
            IsStable,
            ReleaseTime,
            Source);
}

internal sealed record PixelDownloadInstallMergedSelection(
    PixelDownloadInstallLoaderEntry? OptiFine = null,
    PixelDownloadInstallLoaderEntry? Forge = null,
    PixelDownloadInstallLoaderEntry? NeoForge = null,
    PixelDownloadInstallLoaderEntry? Cleanroom = null,
    PixelDownloadInstallLoaderEntry? Fabric = null,
    PixelDownloadInstallLoaderEntry? LegacyFabric = null,
    PixelDownloadInstallLoaderEntry? Quilt = null,
    PixelDownloadInstallLoaderEntry? LiteLoader = null,
    PixelDownloadInstallLoaderEntry? LabyMod = null,
    PixelDownloadInstallAddonEntry? FabricApi = null,
    PixelDownloadInstallAddonEntry? LegacyFabricApi = null,
    PixelDownloadInstallAddonEntry? Qsl = null,
    PixelDownloadInstallAddonEntry? OptiFabric = null)
{
    internal static PixelDownloadInstallMergedSelection FromSelection(MinecraftMergedLoaderSelection selection) =>
        new(
            PixelDownloadInstallLoaderEntry.FromEntry(selection.OptiFine),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.Forge),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.NeoForge),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.Cleanroom),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.Fabric),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.LegacyFabric),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.Quilt),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.LiteLoader),
            PixelDownloadInstallLoaderEntry.FromEntry(selection.LabyMod),
            PixelDownloadInstallAddonEntry.FromEntry(selection.FabricApi),
            PixelDownloadInstallAddonEntry.FromEntry(selection.LegacyFabricApi),
            PixelDownloadInstallAddonEntry.FromEntry(selection.Qsl),
            PixelDownloadInstallAddonEntry.FromEntry(selection.OptiFabric));

    internal MinecraftMergedLoaderSelection ToSelection() =>
        new(
            OptiFine?.ToEntry(),
            Forge?.ToEntry(),
            NeoForge?.ToEntry(),
            Cleanroom?.ToEntry(),
            Fabric?.ToEntry(),
            LegacyFabric?.ToEntry(),
            Quilt?.ToEntry(),
            LiteLoader?.ToEntry(),
            LabyMod?.ToEntry(),
            FabricApi?.ToEntry(),
            LegacyFabricApi?.ToEntry(),
            Qsl?.ToEntry(),
            OptiFabric?.ToEntry());
}

internal sealed record StartDownloadInstallCommand(
    PixelDownloadInstallVersion Version,
    string TargetFolder,
    string? InstanceName,
    PixelDownloadInstallLoader? Loader,
    PixelDownloadInstallMergedSelection? MergedSelection,
    bool SaveServerJar,
    IProgress<MinecraftDownloadTaskInfo>? Progress = null,
    IProgress<MinecraftInstallStageInfo>? StageProgress = null)
    : IRequest<PixelMinecraftInstanceInstalledSnapshot>
{
    internal static StartDownloadInstallCommand ForInstance(
        MinecraftVersionManifestEntry version,
        string targetFolder,
        string? instanceName,
        MinecraftLoaderSelection loader,
        IProgress<MinecraftDownloadTaskInfo>? progress = null) =>
        new(
            PixelDownloadInstallVersion.FromVersion(version),
            targetFolder,
            instanceName,
            PixelDownloadInstallLoader.FromSelection(loader),
            null,
            loader.SaveServerJar,
            progress,
            null);

    internal static StartDownloadInstallCommand ForMerged(
        MinecraftVersionManifestEntry version,
        string targetFolder,
        string instanceName,
        MinecraftMergedLoaderSelection selection,
        bool saveServerJar,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        IProgress<MinecraftInstallStageInfo>? stageProgress = null) =>
        new(
            PixelDownloadInstallVersion.FromVersion(version),
            targetFolder,
            instanceName,
            null,
            PixelDownloadInstallMergedSelection.FromSelection(selection),
            saveServerJar,
            progress,
            stageProgress);
}

internal sealed record SaveMinecraftClientCoreCommand(
    PixelDownloadInstallVersion Version,
    string BaseFolder,
    IProgress<MinecraftDownloadTaskInfo>? Progress = null)
    : IRequest<string>;

internal sealed record SaveMinecraftServerJarCommand(
    PixelDownloadInstallVersion Version,
    string BaseFolder,
    IProgress<MinecraftDownloadTaskInfo>? Progress = null)
    : IRequest<string>;

public sealed record CancelMinecraftDownloadTaskCommand(string TaskId) : IRequest<bool>;

public sealed record CancelAllMinecraftDownloadsCommand : IRequest;

public sealed record MinecraftVersionsRefreshedEvent(IReadOnlyList<PixelDownloadVersionSnapshot> Versions);

public sealed record MinecraftLoaderChoicesRefreshedEvent(
    string VersionId,
    IReadOnlyList<PixelLoaderChoiceGroup> Groups);

public sealed record MinecraftInstallStartedEvent(string VersionId, string TargetFolder, string? InstanceName);

public sealed record MinecraftInstallProgressEvent(PixelDownloadTaskSnapshot Progress);

public sealed record MinecraftInstallStageChangedEvent(MinecraftInstallStageInfo Stage);

public sealed record MinecraftInstallCompletedEvent(PixelMinecraftInstanceInstalledSnapshot Instance);

public sealed record MinecraftCorePackageSavedEvent(string VersionId, string FolderPath, bool IsServer);

public sealed record MinecraftServerLaunchScriptSavedEvent(string VersionId, string FolderPath);

public sealed record MinecraftDownloadTaskCancelRequestedEvent(string TaskId, bool Accepted);

public sealed record MinecraftDownloadAllTasksCancelRequestedEvent;

public sealed record PixelLoaderChoiceGroup(
    string Title,
    string Description,
    string Icon,
    MinecraftLoaderKind? LoaderKind,
    MinecraftAddonKind? AddonKind,
    IReadOnlyList<MinecraftLoaderVersionEntry> LoaderVersions,
    IReadOnlyList<MinecraftAddonFileEntry> AddonFiles,
    string StatusText,
    bool CanSelect)
{
    public bool IsAddon => AddonKind is not null;
}

public sealed class RefreshMinecraftVersionsCommandHandler(
    MinecraftDownloadService downloadService,
    IPixelEventBus eventBus)
    : IRequestHandler<RefreshMinecraftVersionsCommand, IReadOnlyList<MinecraftVersionManifestEntry>>
{
    async Task<IReadOnlyList<MinecraftVersionManifestEntry>> IRequestHandler<RefreshMinecraftVersionsCommand, IReadOnlyList<MinecraftVersionManifestEntry>>.Handle(
        RefreshMinecraftVersionsCommand request,
        CancellationToken cancellationToken)
    {
        var versions = await downloadService.GetVersionManifestAsync(cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftVersionsRefreshedEvent(
            versions.Select(PixelDownloadViewModel.ToVersionSnapshot).ToArray()));
        return versions;
    }
}

public sealed class RefreshMinecraftLoaderChoicesCommandHandler(
    PixelLoaderChoiceService choiceService,
    IPixelEventBus eventBus)
    : IRequestHandler<RefreshMinecraftLoaderChoicesCommand, IReadOnlyList<PixelLoaderChoiceGroup>>
{
    public async Task<IReadOnlyList<PixelLoaderChoiceGroup>> Handle(
        RefreshMinecraftLoaderChoicesCommand request,
        CancellationToken cancellationToken)
    {
        var groups = await choiceService.RefreshAsync(request.VersionId, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftLoaderChoicesRefreshedEvent(request.VersionId, groups));
        return groups;
    }
}

internal sealed class StartDownloadInstallCommandHandler(
    MinecraftInstallService installService,
    MinecraftMergedInstallService mergedInstallService,
    IPixelEventBus eventBus)
    : IRequestHandler<StartDownloadInstallCommand, PixelMinecraftInstanceInstalledSnapshot>
{
    public async Task<PixelMinecraftInstanceInstalledSnapshot> Handle(
        StartDownloadInstallCommand request,
        CancellationToken cancellationToken)
    {
        eventBus.Publish(new MinecraftInstallStartedEvent(
            request.Version.Id,
            request.TargetFolder,
            request.InstanceName));

        var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            request.Progress?.Report(info);
            eventBus.Publish(new MinecraftInstallProgressEvent(PixelDownloadViewModel.ToTaskSnapshot(info)));
        });

        if (request.MergedSelection is { } selection)
        {
            var stageProgress = new Progress<MinecraftInstallStageInfo>(info =>
            {
                request.StageProgress?.Report(info);
                eventBus.Publish(new MinecraftInstallStageChangedEvent(info));
            });
            var mergedInstance = await mergedInstallService.InstallAsync(
                new MinecraftMergedInstallRequest(
                    request.Version.Id,
                    request.Version.Url,
                    request.TargetFolder,
                    string.IsNullOrWhiteSpace(request.InstanceName) ? request.Version.Id : request.InstanceName,
                    selection.ToSelection(),
                    request.SaveServerJar),
                progress,
                stageProgress,
                cancellationToken).ConfigureAwait(false);

            var mergedSnapshot = PixelMinecraftInstanceInstalledSnapshot.FromInstance(mergedInstance);
            eventBus.Publish(new MinecraftInstallCompletedEvent(mergedSnapshot));
            return mergedSnapshot;
        }

        var loader = request.Loader?.ToSelection() ??
                     throw new InvalidOperationException("Download install loader selection is required.");
        var instance = await installService.InstallAsync(
            new MinecraftInstallRequest(
                request.Version.Id,
                request.Version.Url,
                request.TargetFolder,
                request.InstanceName,
                loader),
            progress,
            cancellationToken).ConfigureAwait(false);

        var snapshot = PixelMinecraftInstanceInstalledSnapshot.FromInstance(instance);
        eventBus.Publish(new MinecraftInstallCompletedEvent(snapshot));
        return snapshot;
    }
}

internal sealed class SaveMinecraftClientCoreCommandHandler(
    MinecraftCorePackageSaver packageSaver,
    IPixelEventBus eventBus)
    : IRequestHandler<SaveMinecraftClientCoreCommand, string>
{
    public async Task<string> Handle(SaveMinecraftClientCoreCommand request, CancellationToken cancellationToken)
    {
        var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            request.Progress?.Report(info);
            eventBus.Publish(new MinecraftInstallProgressEvent(PixelDownloadViewModel.ToTaskSnapshot(info)));
        });
        var folder = await packageSaver.SaveClientCoreAsync(
            request.Version.ToVersionManifestEntry(),
            request.BaseFolder,
            progress,
            cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftCorePackageSavedEvent(request.Version.Id, folder, false));
        return folder;
    }
}

internal sealed class SaveMinecraftServerJarCommandHandler(
    MinecraftCorePackageSaver packageSaver,
    IPixelEventBus eventBus)
    : IRequestHandler<SaveMinecraftServerJarCommand, string>
{
    public async Task<string> Handle(SaveMinecraftServerJarCommand request, CancellationToken cancellationToken)
    {
        var progress = new Progress<MinecraftDownloadTaskInfo>(info =>
        {
            request.Progress?.Report(info);
            eventBus.Publish(new MinecraftInstallProgressEvent(PixelDownloadViewModel.ToTaskSnapshot(info)));
        });
        var folder = await packageSaver.SaveServerJarAsync(
            request.Version.ToVersionManifestEntry(),
            request.BaseFolder,
            progress,
            cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftServerLaunchScriptSavedEvent(request.Version.Id, folder));
        eventBus.Publish(new MinecraftCorePackageSavedEvent(request.Version.Id, folder, true));
        return folder;
    }
}

public sealed class CancelMinecraftDownloadTaskCommandHandler(
    MinecraftDownloadService downloadService,
    IPixelEventBus eventBus)
    : IRequestHandler<CancelMinecraftDownloadTaskCommand, bool>
{
    public Task<bool> Handle(CancelMinecraftDownloadTaskCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accepted = downloadService.CancelTask(request.TaskId);
        eventBus.Publish(new MinecraftDownloadTaskCancelRequestedEvent(request.TaskId, accepted));
        return Task.FromResult(accepted);
    }
}

public sealed class CancelAllMinecraftDownloadsCommandHandler(
    MinecraftDownloadService downloadService,
    IPixelEventBus eventBus)
    : IRequestHandler<CancelAllMinecraftDownloadsCommand>
{
    public Task Handle(CancelAllMinecraftDownloadsCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        downloadService.Scheduler.CancelAll();
        eventBus.Publish(new MinecraftDownloadAllTasksCancelRequestedEvent());
        return Task.CompletedTask;
    }
}
