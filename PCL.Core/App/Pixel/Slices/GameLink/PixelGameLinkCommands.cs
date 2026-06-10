using System.Collections.Generic;
using MediatR;
using PCL.Core.App;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.ViewModels;

namespace PCL.Core.App.Pixel.Slices.GameLink;

public sealed record LoadGameLinkAnnouncementsCommand : IRequest<IReadOnlyList<PixelGameLinkAnnouncementSnapshot>>;

public sealed record EnsureEasyTierInstalledCommand : IRequest<bool>;

public sealed record RunGameLinkPrecheckCommand : IRequest<PixelGameLinkPrecheckResult>;

public sealed record CreateGameLinkLobbyCommand(int Port) : IRequest<PixelGameLinkOperationResult>;

public sealed record JoinGameLinkLobbyCommand(string LobbyCode) : IRequest<PixelGameLinkOperationResult>;

public sealed record LeaveGameLinkLobbyCommand : IRequest;

public sealed record DiscoverGameLinkWorldsCommand : IRequest;

public sealed record RunGameLinkNatTestCommand : IRequest<PixelGameLinkNatTestResult>;

public sealed record LoginNatayarkCommand : IRequest<bool>;

public sealed record LogoutNatayarkCommand : IRequest;

public sealed record AcceptGameLinkEulaCommand : IRequest;

public sealed record ResetGameLinkAuthorizationCommand : IRequest;

public sealed record GameLinkAnnouncementsLoadedEvent(
    IReadOnlyList<PixelGameLinkAnnouncementSnapshot> Announcements,
    bool IsLobbyAvailable);

public sealed record EasyTierInstallCheckedEvent(bool IsInstalled);

public sealed record GameLinkPrecheckCompletedEvent(PixelGameLinkPrecheckResult Result);

public sealed record GameLinkLobbyOperationCompletedEvent(string Operation, PixelGameLinkOperationResult Result);

public sealed record GameLinkNatTestCompletedEvent(PixelGameLinkNatTestResult Result);

public sealed record NatayarkLoginCompletedEvent(bool IsSuccess);

public sealed record NatayarkLoggedOutEvent;

public sealed record GameLinkEulaAcceptedEvent;

public sealed record GameLinkAuthorizationResetEvent;

public sealed class LoadGameLinkAnnouncementsCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<LoadGameLinkAnnouncementsCommand, IReadOnlyList<PixelGameLinkAnnouncementSnapshot>>
{
    public async Task<IReadOnlyList<PixelGameLinkAnnouncementSnapshot>> Handle(
        LoadGameLinkAnnouncementsCommand request,
        CancellationToken cancellationToken)
    {
        var announces = await gameLinkService.LoadAnnouncementsAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = PixelGameLinkViewModel.GetAnnouncementSnapshots(announces);
        eventBus.Publish(new GameLinkAnnouncementsLoadedEvent(snapshots, gameLinkService.IsLobbyAvailable));
        return snapshots;
    }
}

public sealed class RunGameLinkPrecheckCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<RunGameLinkPrecheckCommand, PixelGameLinkPrecheckResult>
{
    public async Task<PixelGameLinkPrecheckResult> Handle(
        RunGameLinkPrecheckCommand request,
        CancellationToken cancellationToken)
    {
        var result = await gameLinkService.RunPrecheckAsync(cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new GameLinkPrecheckCompletedEvent(result));
        return result;
    }
}

public sealed class CreateGameLinkLobbyCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<CreateGameLinkLobbyCommand, PixelGameLinkOperationResult>
{
    public async Task<PixelGameLinkOperationResult> Handle(
        CreateGameLinkLobbyCommand request,
        CancellationToken cancellationToken)
    {
        var result = await gameLinkService.CreateLobbyAsync(request.Port, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new GameLinkLobbyOperationCompletedEvent("Create", result));
        return result;
    }
}

public sealed class JoinGameLinkLobbyCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<JoinGameLinkLobbyCommand, PixelGameLinkOperationResult>
{
    public async Task<PixelGameLinkOperationResult> Handle(
        JoinGameLinkLobbyCommand request,
        CancellationToken cancellationToken)
    {
        var result = await gameLinkService.JoinLobbyAsync(request.LobbyCode, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new GameLinkLobbyOperationCompletedEvent("Join", result));
        return result;
    }
}

public sealed class LeaveGameLinkLobbyCommandHandler(PixelGameLinkService gameLinkService)
    : IRequestHandler<LeaveGameLinkLobbyCommand>
{
    public async Task Handle(LeaveGameLinkLobbyCommand request, CancellationToken cancellationToken)
    {
        await gameLinkService.LeaveLobbyAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DiscoverGameLinkWorldsCommandHandler(PixelGameLinkService gameLinkService)
    : IRequestHandler<DiscoverGameLinkWorldsCommand>
{
    public async Task Handle(DiscoverGameLinkWorldsCommand request, CancellationToken cancellationToken)
    {
        await gameLinkService.DiscoverWorldsAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class RunGameLinkNatTestCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<RunGameLinkNatTestCommand, PixelGameLinkNatTestResult>
{
    public async Task<PixelGameLinkNatTestResult> Handle(
        RunGameLinkNatTestCommand request,
        CancellationToken cancellationToken)
    {
        var result = await gameLinkService.RunNatTestAsync(cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new GameLinkNatTestCompletedEvent(result));
        return result;
    }
}

public sealed class LoginNatayarkCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<LoginNatayarkCommand, bool>
{
    public async Task<bool> Handle(LoginNatayarkCommand request, CancellationToken cancellationToken)
    {
        var ok = await NatayarkOAuthService.StartLoginAsync(cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new NatayarkLoginCompletedEvent(ok));
        return ok;
    }
}

public sealed class LogoutNatayarkCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<LogoutNatayarkCommand>
{
    public Task Handle(LogoutNatayarkCommand request, CancellationToken cancellationToken)
    {
        NatayarkOAuthService.Logout();
        eventBus.Publish(new NatayarkLoggedOutEvent());
        return Task.CompletedTask;
    }
}

public sealed class EnsureEasyTierInstalledCommandHandler(
    PixelGameLinkService gameLinkService,
    IPixelEventBus eventBus)
    : IRequestHandler<EnsureEasyTierInstalledCommand, bool>
{
    public async Task<bool> Handle(EnsureEasyTierInstalledCommand request, CancellationToken cancellationToken)
    {
        var ok = await gameLinkService.EnsureEasyTierInstalledAsync(cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new EasyTierInstallCheckedEvent(ok));
        return ok;
    }
}

public sealed class AcceptGameLinkEulaCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<AcceptGameLinkEulaCommand>
{
    public Task Handle(AcceptGameLinkEulaCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        States.Link.LinkEula = true;
        eventBus.Publish(new GameLinkEulaAcceptedEvent());
        return Task.CompletedTask;
    }
}

public sealed class ResetGameLinkAuthorizationCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<ResetGameLinkAuthorizationCommand>
{
    public Task Handle(ResetGameLinkAuthorizationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        States.Link.NaidRefreshToken = string.Empty;
        States.Link.LinkEula = false;
        eventBus.Publish(new GameLinkAuthorizationResetEvent());
        return Task.CompletedTask;
    }
}
