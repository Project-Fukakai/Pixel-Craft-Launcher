using System.Collections.Generic;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.ViewModels;

namespace PCL.Core.App.Pixel.Slices.GameLink;

public sealed record PixelGameLinkAnnouncementLoadResult(bool Started, bool IsSuccess, string? ErrorMessage);

public enum PixelGameLinkNotificationKind
{
    Info,
    Success,
    Critical
}

public sealed record PixelGameLinkNotificationSnapshot(
    string Message,
    PixelGameLinkNotificationKind Kind);

public sealed record PixelGameLinkLoginToggleResult(
    bool IsLoginOperation,
    bool IsSuccess,
    string Message,
    PixelGameLinkNotificationSnapshot Notification);

public sealed record PixelGameLinkDependencyInstallResult(
    bool IsSuccess,
    string Message,
    PixelGameLinkNotificationSnapshot Notification);

public sealed record PixelGameLinkAuthorizationResetResult(
    bool IsSuccess,
    PixelGameLinkNotificationSnapshot Notification);

public sealed record PixelGameLinkNatTestPresentation(
    bool IsSuccess,
    string Message,
    PixelGameLinkNatDialogSnapshot? Dialog);

public sealed record PixelGameLinkNetworkTestPanelSnapshot(
    string PlatformText,
    bool CanRun,
    PixelGameLinkNatStatusSnapshot InitialStatus,
    PixelGameLinkNetworkTestMessages Messages);

public sealed record PixelGameLinkSetupNatTestResult(
    bool IsSuccess,
    string NotificationMessage,
    PixelGameLinkNatStatusSnapshot? Status);

public sealed class PixelGameLinkToolsPageController(
    IPixelCommandBus commandBus,
    PixelGameLinkService gameLinkService,
    PixelGameLinkViewModel viewModel,
    PixelGameLinkStateMachine? stateMachine = null)
{
    public PixelGameLinkState State => stateMachine?.State ?? PixelGameLinkState.Ready;

    public IDisposable SubscribeRuntimeEvents(PixelGameLinkRuntimeCallbacks callbacks)
    {
        return gameLinkService.SubscribeRuntimeEvents(callbacks);
    }

    public Task InitializeLobbyAsync(CancellationToken cancellationToken = default)
    {
        return gameLinkService.InitializeLobbyAsync(cancellationToken);
    }

    public string? GetNatayarkLoginStartMessage()
    {
        return viewModel.IsNatayarkLoggedIn
            ? null
            : PixelGameLinkViewModel.GetNatayarkLoginStartMessage();
    }

    public PixelGameLinkNetworkTestPanelSnapshot GetNetworkTestPanelSnapshot()
    {
        var easyTier = viewModel.EasyTierSnapshot;
        return new PixelGameLinkNetworkTestPanelSnapshot(
            easyTier.Details,
            easyTier.IsPlatformSupported,
            PixelGameLinkViewModel.GetInitialNatStatusSnapshot(),
            PixelGameLinkViewModel.GetNetworkTestMessages());
    }

    public async Task<PixelGameLinkAnnouncementLoadResult> LoadAnnouncementsAsync(
        CancellationToken cancellationToken = default)
    {
        if (viewModel.IsAnnouncementLoading)
            return new PixelGameLinkAnnouncementLoadResult(false, true, null);

        viewModel.BeginAnnouncementLoading();
        try
        {
            var announcements = await commandBus.Send(new LoadGameLinkAnnouncementsCommand(), cancellationToken)
                .ConfigureAwait(false);
            viewModel.SetLobbyAvailability(gameLinkService.IsLobbyAvailable);
            viewModel.SetAnnouncementSnapshots(announcements);
            return new PixelGameLinkAnnouncementLoadResult(true, true, null);
        }
        catch (Exception ex)
        {
            var message = "连接大厅服务器失败：" + ex.Message;
            viewModel.SetLobbyAvailability(false);
            viewModel.SetAnnouncementError(message);
            return new PixelGameLinkAnnouncementLoadResult(true, false, message);
        }
        finally
        {
            viewModel.EndAnnouncementLoading();
        }
    }

    public async Task<PixelGameLinkLoginToggleResult> ToggleNatayarkLoginAsync(
        CancellationToken cancellationToken = default)
    {
        var isLoginOperation = !viewModel.IsNatayarkLoggedIn;
        try
        {
            if (isLoginOperation)
            {
                BeginGameLinkState(PixelGameLinkTrigger.StartLogin);
                var ok = await commandBus.Send(new LoginNatayarkCommand(), cancellationToken).ConfigureAwait(false);
                TryFireGameLinkTrigger(ok
                    ? PixelGameLinkTrigger.LoginSucceeded
                    : PixelGameLinkTrigger.LoginFailed);
                return new PixelGameLinkLoginToggleResult(
                    true,
                    ok,
                    ok ? "已完成登录操作。" : "Natayark 登录未完成。",
                    CreateNotification(
                        ok ? "已完成登录操作。" : "Natayark 登录未完成。",
                        ok ? PixelGameLinkNotificationKind.Success : PixelGameLinkNotificationKind.Critical));
            }

            await commandBus.Send(new LogoutNatayarkCommand(), cancellationToken).ConfigureAwait(false);
            BeginGameLinkState(PixelGameLinkTrigger.Reset);
            return new PixelGameLinkLoginToggleResult(
                false,
                true,
                "已退出 Natayark 登录。",
                CreateNotification("已退出 Natayark 登录。", PixelGameLinkNotificationKind.Success));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (isLoginOperation)
                TryFireGameLinkTrigger(PixelGameLinkTrigger.LoginFailed);
            else
                TryFireGameLinkTrigger(PixelGameLinkTrigger.OperationFailed);

            var message = PixelGameLinkViewModel.GetNatayarkLoginExceptionMessage(ex);
            return new PixelGameLinkLoginToggleResult(
                isLoginOperation,
                false,
                message,
                CreateNotification(message, PixelGameLinkNotificationKind.Critical));
        }
    }

    public async Task<PixelGameLinkDependencyInstallResult> EnsureEasyTierInstalledAsync(CancellationToken cancellationToken = default)
    {
        BeginGameLinkState(PixelGameLinkTrigger.StartDependencyInstall);
        var ok = await commandBus.Send(new EnsureEasyTierInstalledCommand(), cancellationToken).ConfigureAwait(false);
        TryFireGameLinkTrigger(ok
            ? PixelGameLinkTrigger.DependencyReady
            : PixelGameLinkTrigger.OperationFailed);
        var message = PixelGameLinkViewModel.GetEasyTierInstallResultMessage(ok);
        return new PixelGameLinkDependencyInstallResult(
            ok,
            message,
            CreateNotification(
                message,
                ok ? PixelGameLinkNotificationKind.Success : PixelGameLinkNotificationKind.Critical));
    }

    public async Task<PixelGameLinkNatTestResult> RunNatTestAsync(CancellationToken cancellationToken = default)
    {
        BeginGameLinkState(PixelGameLinkTrigger.StartNetworkCheck);
        var result = await commandBus.Send(new RunGameLinkNatTestCommand(), cancellationToken).ConfigureAwait(false);
        TryFireGameLinkTrigger(result.IsSuccess
            ? PixelGameLinkTrigger.NetworkCheckSucceeded
            : PixelGameLinkTrigger.NetworkCheckFailed);
        return result;
    }

    public async Task<PixelGameLinkNatTestPresentation> RunToolsNatTestAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await RunNatTestAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? new PixelGameLinkNatTestPresentation(true, string.Empty, PixelGameLinkViewModel.GetNatDialogSnapshot(result))
            : new PixelGameLinkNatTestPresentation(false, result.Message, null);
    }

    public async Task<PixelGameLinkSetupNatTestResult> RunSetupNatTestAsync(
        CancellationToken cancellationToken = default)
    {
        var messages = PixelGameLinkViewModel.GetNetworkTestMessages();
        try
        {
            var result = await RunNatTestAsync(cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? new PixelGameLinkSetupNatTestResult(
                    true,
                    messages.CompletedMessage,
                    PixelGameLinkViewModel.GetNatStatusSnapshot(result))
                : new PixelGameLinkSetupNatTestResult(false, result.Message, null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new PixelGameLinkSetupNatTestResult(
                false,
                messages.GetFailureMessage(ex),
                null);
        }
    }

    public Task DiscoverWorldsAsync(CancellationToken cancellationToken = default)
    {
        return commandBus.Send(new DiscoverGameLinkWorldsCommand(), cancellationToken);
    }

    public async Task<PixelGameLinkAuthorizationResetResult> ResetAuthorizationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await commandBus.Send(new ResetGameLinkAuthorizationCommand(), cancellationToken)
                .ConfigureAwait(false);
            viewModel.ApplyAuthorizationReset();
            return new PixelGameLinkAuthorizationResetResult(
                true,
                CreateNotification("已撤销联机授权。", PixelGameLinkNotificationKind.Success));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new PixelGameLinkAuthorizationResetResult(
                false,
                CreateNotification(
                    "撤销联机授权失败：" + ex.Message,
                    PixelGameLinkNotificationKind.Critical));
        }
    }

    public async Task<PixelGameLinkOperationResult> CreateLobbyAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        viewModel.ShowFinish();
        BeginGameLinkState(PixelGameLinkTrigger.StartHosting);
        var result = await commandBus.Send(new CreateGameLinkLobbyCommand(port), cancellationToken)
            .ConfigureAwait(false);
        ApplyStateResult(result);
        ApplyOperationResult(result);
        return result;
    }

    public async Task<PixelGameLinkOperationResult> JoinLobbyAsync(
        string lobbyCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            var missingCode = PixelGameLinkOperationResult.PrecheckFailed(
                PixelGameLinkPrecheckResult.Failed(PixelGameLinkPrecheckFailureKind.MissingLobbyCode, "请输入大厅编号。"));
            ApplyOperationResult(missingCode);
            return missingCode;
        }

        viewModel.ShowFinish();
        BeginGameLinkState(PixelGameLinkTrigger.StartJoining);
        var result = await commandBus.Send(new JoinGameLinkLobbyCommand(lobbyCode), cancellationToken)
            .ConfigureAwait(false);
        ApplyStateResult(result);
        ApplyOperationResult(result);
        return result;
    }

    public async Task LeaveLobbyAsync(CancellationToken cancellationToken = default)
    {
        viewModel.ShowSelect();
        BeginGameLinkState(PixelGameLinkTrigger.Leave);
        await commandBus.Send(new LeaveGameLinkLobbyCommand(), cancellationToken).ConfigureAwait(false);
        TryFireGameLinkTrigger(PixelGameLinkTrigger.Left);
    }

    public PixelGameLinkNotificationSnapshot? GetOperationFailureNotification(PixelGameLinkOperationResult result)
    {
        return result.IsSuccess || string.IsNullOrWhiteSpace(result.Message)
            ? null
            : CreateNotification(result.Message, PixelGameLinkNotificationKind.Critical);
    }

    private void ApplyStateResult(PixelGameLinkOperationResult result)
    {
        if (result.IsSuccess)
        {
            TryFireGameLinkTrigger(PixelGameLinkTrigger.OperationSucceeded);
            return;
        }

        TryFireGameLinkTrigger(result.Precheck?.FailureKind == PixelGameLinkPrecheckFailureKind.EulaRequired
            ? PixelGameLinkTrigger.RequireEula
            : PixelGameLinkTrigger.OperationFailed);
    }

    private void ApplyOperationResult(PixelGameLinkOperationResult result)
    {
        if (result.IsSuccess)
            return;

        if (result.Precheck?.FailureKind == PixelGameLinkPrecheckFailureKind.EulaRequired)
        {
            viewModel.ShowEula();
            return;
        }

        viewModel.ShowSelect();
    }

    private void BeginGameLinkState(PixelGameLinkTrigger trigger)
    {
        if (stateMachine is null)
            return;

        if (stateMachine.CanFire(trigger))
        {
            TryFireGameLinkTrigger(trigger);
            return;
        }

        if (stateMachine.CanFire(PixelGameLinkTrigger.Reset))
            TryFireGameLinkTrigger(PixelGameLinkTrigger.Reset);

        TryFireGameLinkTrigger(trigger);
    }

    private void TryFireGameLinkTrigger(PixelGameLinkTrigger trigger)
    {
        if (stateMachine?.CanFire(trigger) != true)
            return;

        stateMachine.Fire(trigger);
    }

    private static PixelGameLinkNotificationSnapshot CreateNotification(
        string message,
        PixelGameLinkNotificationKind kind) =>
        new(message, kind);
}
