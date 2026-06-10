using System.Linq;
using MediatR;
using PCL.Core.App.Pixel.Events;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.App.Pixel.Slices.Profiles;

public enum PixelOfflineUuidMode
{
    Standard,
    Legacy,
    Custom
}

public sealed record SaveOfflineMinecraftProfileCommand(
    MinecraftProfile? ExistingProfile,
    string Username,
    OfflineUuidMode UuidMode,
    string? CustomUuid)
    : IRequest<PixelProfileItemSnapshot>;

public sealed record SaveOfflineMinecraftProfileByIdCommand(
    string? ExistingProfileId,
    string Username,
    PixelOfflineUuidMode UuidMode,
    string? CustomUuid)
    : IRequest<PixelProfileItemSnapshot>;

public sealed record AddMicrosoftMinecraftProfileCommand(
    IMinecraftProfileUiCallbacks Callbacks,
    IProgress<MinecraftProfileLoginProgress>? Progress = null)
    : IRequest<PixelProfileItemSnapshot>;

public sealed record AddAuthlibMinecraftProfileCommand(
    string ApiRoot,
    string LoginName,
    string Password,
    IMinecraftProfileUiCallbacks Callbacks,
    IProgress<MinecraftProfileLoginProgress>? Progress = null)
    : IRequest<PixelProfileItemSnapshot>;

public sealed record AddAuthServerPresetCommand(
    string Name,
    string ApiRoot,
    string RegisterUrl)
    : IRequest<PixelAuthServerSnapshot>;

public sealed record SelectMinecraftProfileByIdCommand(string ProfileId) : IRequest;

public sealed record RemoveMinecraftProfileByIdCommand(string ProfileId) : IRequest;

public sealed record MinecraftProfileSavedEvent(PixelProfileItemSnapshot Profile);

public sealed record MinecraftProfileSelectedEvent(PixelProfileItemSnapshot Profile);

public sealed record MinecraftProfileRemovedEvent(PixelProfileItemSnapshot Profile);

public sealed record AuthServerPresetSavedEvent(PixelAuthServerSnapshot Preset);

public sealed class SaveOfflineMinecraftProfileCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<SaveOfflineMinecraftProfileCommand, PixelProfileItemSnapshot>
{
    public async Task<PixelProfileItemSnapshot> Handle(SaveOfflineMinecraftProfileCommand request, CancellationToken cancellationToken)
    {
        MinecraftProfile profile;
        if (request.ExistingProfile is null)
        {
            profile = await profileService.AddOfflineProfileAsync(
                request.Username,
                request.UuidMode,
                request.CustomUuid,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await profileService.UpdateOfflineProfileAsync(
                request.ExistingProfile,
                request.Username,
                request.UuidMode,
                request.CustomUuid,
                cancellationToken).ConfigureAwait(false);
            profile = request.ExistingProfile;
        }

        var snapshot = PixelProfileListService.GetProfileSnapshot(profile);
        eventBus.Publish(new MinecraftProfileSavedEvent(snapshot));
        return snapshot;
    }
}

public sealed class SaveOfflineMinecraftProfileByIdCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<SaveOfflineMinecraftProfileByIdCommand, PixelProfileItemSnapshot>
{
    public async Task<PixelProfileItemSnapshot> Handle(
        SaveOfflineMinecraftProfileByIdCommand request,
        CancellationToken cancellationToken)
    {
        MinecraftProfile profile;
        if (string.IsNullOrWhiteSpace(request.ExistingProfileId))
        {
            profile = await profileService.AddOfflineProfileAsync(
                request.Username,
                PixelProfileCommandHelpers.ToMinecraftUuidMode(request.UuidMode),
                request.CustomUuid,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            profile = await PixelProfileCommandHelpers.FindProfileAsync(
                profileService,
                request.ExistingProfileId,
                cancellationToken).ConfigureAwait(false);
            await profileService.UpdateOfflineProfileAsync(
                profile,
                request.Username,
                PixelProfileCommandHelpers.ToMinecraftUuidMode(request.UuidMode),
                request.CustomUuid,
                cancellationToken).ConfigureAwait(false);
        }

        var snapshot = PixelProfileListService.GetProfileSnapshot(profile);
        eventBus.Publish(new MinecraftProfileSavedEvent(snapshot));
        return snapshot;
    }
}

public sealed class AddMicrosoftMinecraftProfileCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<AddMicrosoftMinecraftProfileCommand, PixelProfileItemSnapshot>
{
    public async Task<PixelProfileItemSnapshot> Handle(AddMicrosoftMinecraftProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await profileService.AddMicrosoftProfileAsync(
            request.Callbacks,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        var snapshot = PixelProfileListService.GetProfileSnapshot(profile);
        eventBus.Publish(new MinecraftProfileSavedEvent(snapshot));
        return snapshot;
    }
}

public sealed class AddAuthlibMinecraftProfileCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<AddAuthlibMinecraftProfileCommand, PixelProfileItemSnapshot>
{
    public async Task<PixelProfileItemSnapshot> Handle(AddAuthlibMinecraftProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await profileService.AddAuthlibProfileAsync(
            request.ApiRoot,
            request.LoginName,
            request.Password,
            request.Callbacks,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        var snapshot = PixelProfileListService.GetProfileSnapshot(profile);
        eventBus.Publish(new MinecraftProfileSavedEvent(snapshot));
        return snapshot;
    }
}

public sealed class AddAuthServerPresetCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<AddAuthServerPresetCommand, PixelAuthServerSnapshot>
{
    public async Task<PixelAuthServerSnapshot> Handle(AddAuthServerPresetCommand request, CancellationToken cancellationToken)
    {
        var preset = await profileService.AddAuthServerAsync(
            request.Name,
            request.ApiRoot,
            request.RegisterUrl,
            cancellationToken).ConfigureAwait(false);
        var snapshot = PixelProfileListService.GetAuthServerSnapshot(preset);
        eventBus.Publish(new AuthServerPresetSavedEvent(snapshot));
        return snapshot;
    }
}

public sealed class SelectMinecraftProfileByIdCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<SelectMinecraftProfileByIdCommand>
{
    public async Task Handle(SelectMinecraftProfileByIdCommand request, CancellationToken cancellationToken)
    {
        var profile = await PixelProfileCommandHelpers.FindProfileAsync(profileService, request.ProfileId, cancellationToken).ConfigureAwait(false);
        await profileService.SelectProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftProfileSelectedEvent(PixelProfileListService.GetProfileSnapshot(profile, profile.Id)));
    }
}

public sealed class RemoveMinecraftProfileByIdCommandHandler(
    MinecraftProfileService profileService,
    IPixelEventBus eventBus)
    : IRequestHandler<RemoveMinecraftProfileByIdCommand>
{
    public async Task Handle(RemoveMinecraftProfileByIdCommand request, CancellationToken cancellationToken)
    {
        var profile = await PixelProfileCommandHelpers.FindProfileAsync(profileService, request.ProfileId, cancellationToken).ConfigureAwait(false);
        await profileService.RemoveProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new MinecraftProfileRemovedEvent(PixelProfileListService.GetProfileSnapshot(profile)));
    }
}

file static class PixelProfileCommandHelpers
{
    internal static OfflineUuidMode ToMinecraftUuidMode(PixelOfflineUuidMode mode) =>
        mode switch
        {
            PixelOfflineUuidMode.Legacy => OfflineUuidMode.Legacy,
            PixelOfflineUuidMode.Custom => OfflineUuidMode.Custom,
            _ => OfflineUuidMode.Standard
        };

    internal static async Task<MinecraftProfile> FindProfileAsync(
        MinecraftProfileService profileService,
        string profileId,
        CancellationToken cancellationToken)
    {
        await profileService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var profile = profileService.Profiles.FirstOrDefault(item =>
            string.Equals(item.Id, profileId, StringComparison.Ordinal));
        return profile ?? throw new MinecraftProfileException("找不到指定档案。");
    }
}
