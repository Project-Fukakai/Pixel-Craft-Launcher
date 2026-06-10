using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Link.Lobby;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelGameLinkToolsPageControllerTest
{
    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
    }

    [TestMethod]
    public async Task LoadAnnouncementsUpdatesViewModel()
    {
        var originalAvailable = LobbyInfoProvider.IsLobbyAvailable;
        LobbyInfoProvider.IsLobbyAvailable = false;
        var bus = new RecordingCommandBus
        {
            AnnouncementResult =
            [
                new PixelGameLinkAnnouncementSnapshot(PixelGameLinkAnnouncementSeverity.Notice, "[提示] hello")
            ]
        };
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        try
        {
            var result = await controller.LoadAnnouncementsAsync();

            Assert.IsTrue(result.Started);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(viewModel.IsAnnouncementLoading);
            Assert.IsFalse(viewModel.IsLobbyAvailable);
            Assert.AreEqual("[提示] hello", viewModel.AnnouncementSnapshot.Text);
            Assert.IsInstanceOfType(bus.LastRequest, typeof(LoadGameLinkAnnouncementsCommand));
        }
        finally
        {
            LobbyInfoProvider.IsLobbyAvailable = originalAvailable;
        }
    }

    [TestMethod]
    public async Task LoadAnnouncementsStoresErrorInViewModel()
    {
        var bus = new RecordingCommandBus
        {
            AnnouncementException = new InvalidOperationException("offline")
        };
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        var result = await controller.LoadAnnouncementsAsync();

        Assert.IsTrue(result.Started);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsFalse(viewModel.IsAnnouncementLoading);
        Assert.IsFalse(viewModel.IsLobbyAvailable);
        StringAssert.Contains(viewModel.AnnouncementSnapshot.Text, "offline");
    }

    [TestMethod]
    public async Task GameLinkOperationsUseCommandBus()
    {
        var bus = new RecordingCommandBus
        {
            EasyTierResult = true,
            NatTestResult = PixelGameLinkNatTestResult.Success("Full Cone", "Open", true)
        };
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), CreateViewModel());

        var install = await controller.EnsureEasyTierInstalledAsync();
        Assert.IsTrue(install.IsSuccess);
        Assert.AreEqual(PixelGameLinkViewModel.GetEasyTierInstallResultMessage(true), install.Message);
        Assert.AreEqual(PixelGameLinkNotificationKind.Success, install.Notification.Kind);
        Assert.AreEqual(install.Message, install.Notification.Message);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(EnsureEasyTierInstalledCommand));

        var nat = await controller.RunNatTestAsync();
        Assert.IsTrue(nat.IsSuccess);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(RunGameLinkNatTestCommand));

        await controller.DiscoverWorldsAsync();
        Assert.IsInstanceOfType(bus.LastRequest, typeof(DiscoverGameLinkWorldsCommand));
    }

    [TestMethod]
    public async Task EasyTierInstallResultCarriesPresentationMessage()
    {
        var controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus { EasyTierResult = false },
            new PixelGameLinkService(),
            CreateViewModel());

        var result = await controller.EnsureEasyTierInstalledAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkViewModel.GetEasyTierInstallResultMessage(false), result.Message);
        Assert.AreEqual(PixelGameLinkNotificationKind.Critical, result.Notification.Kind);
        Assert.AreEqual(result.Message, result.Notification.Message);
    }

    [TestMethod]
    public async Task ToolsNatTestResultCarriesPresentationIntent()
    {
        var controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus
            {
                NatTestResult = PixelGameLinkNatTestResult.Success("Full Cone", "Open", true)
            },
            new PixelGameLinkService(),
            CreateViewModel());

        var success = await controller.RunToolsNatTestAsync();

        Assert.IsTrue(success.IsSuccess);
        Assert.IsNotNull(success.Dialog);
        Assert.AreEqual("NAT 类型", success.Dialog.Title);
        StringAssert.Contains(success.Dialog.Body, "Full Cone");

        controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus
            {
                NatTestResult = PixelGameLinkNatTestResult.Failed("failed")
            },
            new PixelGameLinkService(),
            CreateViewModel());

        var failed = await controller.RunToolsNatTestAsync();

        Assert.IsFalse(failed.IsSuccess);
        Assert.IsNull(failed.Dialog);
        Assert.AreEqual("failed", failed.Message);
    }

    [TestMethod]
    public async Task SetupNatTestResultCarriesPanelSnapshotAndStatusIntent()
    {
        var controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus
            {
                NatTestResult = PixelGameLinkNatTestResult.Success("Full Cone", "Open", true)
            },
            new PixelGameLinkService(),
            CreateViewModel());
        var panel = controller.GetNetworkTestPanelSnapshot();

        Assert.IsFalse(string.IsNullOrWhiteSpace(panel.PlatformText));
        Assert.AreEqual(PixelGameLinkViewModel.GetNetworkTestMessages().StartButtonText, panel.Messages.StartButtonText);
        Assert.AreEqual(PixelGameLinkViewModel.GetInitialNatStatusSnapshot(), panel.InitialStatus);

        var success = await controller.RunSetupNatTestAsync();

        Assert.IsTrue(success.IsSuccess);
        Assert.AreEqual(panel.Messages.CompletedMessage, success.NotificationMessage);
        Assert.IsNotNull(success.Status);
        StringAssert.Contains(success.Status.UdpText, "Full Cone");

        controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus
            {
                NatTestResult = PixelGameLinkNatTestResult.Failed("failed")
            },
            new PixelGameLinkService(),
            CreateViewModel());

        var failed = await controller.RunSetupNatTestAsync();

        Assert.IsFalse(failed.IsSuccess);
        Assert.AreEqual("failed", failed.NotificationMessage);
        Assert.IsNull(failed.Status);
    }

    [TestMethod]
    public async Task LobbyOperationsUpdateViewModelState()
    {
        var bus = new RecordingCommandBus
        {
            CreateResult = PixelGameLinkOperationResult.Success("ok"),
            JoinResult = PixelGameLinkOperationResult.PrecheckFailed(
                PixelGameLinkPrecheckResult.Failed(PixelGameLinkPrecheckFailureKind.LoginRequired, "login"))
        };
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        var create = await controller.CreateLobbyAsync(25565);

        Assert.IsTrue(create.IsSuccess);
        Assert.AreEqual(PixelGameLinkSubpage.Finish, viewModel.Subpage);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(CreateGameLinkLobbyCommand));

        var join = await controller.JoinLobbyAsync("ABCD");

        Assert.IsFalse(join.IsSuccess);
        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(JoinGameLinkLobbyCommand));
        var failure = controller.GetOperationFailureNotification(join);
        Assert.IsNotNull(failure);
        Assert.AreEqual("login", failure!.Message);
        Assert.AreEqual(PixelGameLinkNotificationKind.Critical, failure.Kind);

        await controller.LeaveLobbyAsync();

        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(LeaveGameLinkLobbyCommand));
    }

    [TestMethod]
    public async Task OperationsDriveGameLinkStateMachineEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var stateMachine = new PixelGameLinkStateMachine(new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance));
        var bus = new RecordingCommandBus
        {
            EasyTierResult = true,
            NatTestResult = PixelGameLinkNatTestResult.Success("Full Cone", "Open", true),
            CreateResult = PixelGameLinkOperationResult.Success("created")
        };
        var controller = new PixelGameLinkToolsPageController(
            bus,
            new PixelGameLinkService(),
            CreateViewModel(),
            stateMachine);

        Assert.IsTrue((await controller.EnsureEasyTierInstalledAsync()).IsSuccess);
        Assert.IsTrue((await controller.RunNatTestAsync()).IsSuccess);
        Assert.IsTrue((await controller.CreateLobbyAsync(25565)).IsSuccess);
        await controller.LeaveLobbyAsync();

        Assert.AreEqual(PixelGameLinkState.Ready, controller.State);
        var actualTriggers = string.Join(
            ",",
            events.Where(@event => @event.MachineName == "PixelGameLink").Select(@event => @event.Trigger));
        Assert.AreEqual(
            string.Join(
                ",",
                [
                    nameof(PixelGameLinkTrigger.StartDependencyInstall),
                    nameof(PixelGameLinkTrigger.DependencyReady),
                    nameof(PixelGameLinkTrigger.StartNetworkCheck),
                    nameof(PixelGameLinkTrigger.NetworkCheckSucceeded),
                    nameof(PixelGameLinkTrigger.StartHosting),
                    nameof(PixelGameLinkTrigger.OperationSucceeded),
                    nameof(PixelGameLinkTrigger.Leave),
                    nameof(PixelGameLinkTrigger.Left)
                ]),
            actualTriggers);
    }

    [TestMethod]
    public async Task JoinLobbyWithoutCodeDoesNotUseCommandBus()
    {
        var bus = new RecordingCommandBus();
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        var result = await controller.JoinLobbyAsync("");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkPrecheckFailureKind.MissingLobbyCode, result.Precheck?.FailureKind);
        Assert.IsNull(bus.LastRequest);
        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);
    }

    [TestMethod]
    public void NatayarkLoginStartMessageDependsOnControllerState()
    {
        var oldToken = States.Link.NaidRefreshToken;
        try
        {
            States.Link.NaidRefreshToken = string.Empty;
            var controller = new PixelGameLinkToolsPageController(
                new RecordingCommandBus(),
                new PixelGameLinkService(),
                CreateViewModel());

            Assert.AreEqual(PixelGameLinkViewModel.GetNatayarkLoginStartMessage(), controller.GetNatayarkLoginStartMessage());

            States.Link.NaidRefreshToken = "token";

            Assert.IsNull(controller.GetNatayarkLoginStartMessage());
        }
        finally
        {
            States.Link.NaidRefreshToken = oldToken;
        }
    }

    [TestMethod]
    public async Task NatayarkLoginExceptionReturnsFailureResult()
    {
        var oldToken = States.Link.NaidRefreshToken;
        try
        {
            States.Link.NaidRefreshToken = string.Empty;
            var controller = new PixelGameLinkToolsPageController(
                new RecordingCommandBus { LoginException = new InvalidOperationException("boom") },
                new PixelGameLinkService(),
                CreateViewModel());

            var result = await controller.ToggleNatayarkLoginAsync();

            Assert.IsTrue(result.IsLoginOperation);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(
                PixelGameLinkViewModel.GetNatayarkLoginExceptionMessage(new InvalidOperationException("boom")),
                result.Message);
            Assert.AreEqual(PixelGameLinkNotificationKind.Critical, result.Notification.Kind);
            Assert.AreEqual(result.Message, result.Notification.Message);
        }
        finally
        {
            States.Link.NaidRefreshToken = oldToken;
        }
    }

    [TestMethod]
    public async Task ResetAuthorizationUsesCommandBusAndReturnsNotification()
    {
        States.Link.LinkEula = true;
        States.Link.NaidRefreshToken = "token";
        var bus = new RecordingCommandBus();
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        var result = await controller.ResetAuthorizationAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkNotificationKind.Success, result.Notification.Kind);
        Assert.AreEqual("已撤销联机授权。", result.Notification.Message);
        Assert.AreEqual(PixelGameLinkSubpage.Eula, viewModel.Subpage);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(ResetGameLinkAuthorizationCommand));
    }

    [TestMethod]
    public async Task ResetAuthorizationExceptionReturnsFailureNotification()
    {
        var controller = new PixelGameLinkToolsPageController(
            new RecordingCommandBus { ResetException = new InvalidOperationException("boom") },
            new PixelGameLinkService(),
            CreateViewModel());

        var result = await controller.ResetAuthorizationAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkNotificationKind.Critical, result.Notification.Kind);
        StringAssert.Contains(result.Notification.Message, "boom");
    }

    [TestMethod]
    public async Task EulaPrecheckFailureSwitchesToEulaInController()
    {
        var bus = new RecordingCommandBus
        {
            CreateResult = PixelGameLinkOperationResult.PrecheckFailed(
                PixelGameLinkPrecheckResult.Failed(PixelGameLinkPrecheckFailureKind.EulaRequired))
        };
        var viewModel = CreateViewModel();
        var controller = new PixelGameLinkToolsPageController(bus, new PixelGameLinkService(), viewModel);

        var result = await controller.CreateLobbyAsync(25565);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkSubpage.Eula, viewModel.Subpage);
        Assert.IsInstanceOfType(bus.LastRequest, typeof(CreateGameLinkLobbyCommand));
    }

    private static PixelGameLinkViewModel CreateViewModel()
    {
        return new PixelGameLinkViewModel(new RecordingCommandBus());
    }

    private sealed class RecordingCommandBus : IPixelCommandBus
    {
        public object? LastRequest { get; private set; }
        public IReadOnlyList<PixelGameLinkAnnouncementSnapshot> AnnouncementResult { get; init; } = [];
        public Exception? AnnouncementException { get; init; }
        public Exception? LoginException { get; init; }
        public Exception? ResetException { get; init; }
        public bool EasyTierResult { get; init; }
        public PixelGameLinkOperationResult CreateResult { get; init; } =
            PixelGameLinkOperationResult.Success("created");
        public PixelGameLinkOperationResult JoinResult { get; init; } =
            PixelGameLinkOperationResult.Success("joined");
        public PixelGameLinkNatTestResult NatTestResult { get; init; } =
            PixelGameLinkNatTestResult.Failed("not configured");

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is LoadGameLinkAnnouncementsCommand)
            {
                if (AnnouncementException is not null)
                    throw AnnouncementException;

                return Task.FromResult((TResponse)AnnouncementResult);
            }

            if (request is EnsureEasyTierInstalledCommand)
                return Task.FromResult((TResponse)(object)EasyTierResult);

            if (request is RunGameLinkNatTestCommand)
                return Task.FromResult((TResponse)(object)NatTestResult);

            if (request is CreateGameLinkLobbyCommand)
                return Task.FromResult((TResponse)(object)CreateResult);

            if (request is JoinGameLinkLobbyCommand)
                return Task.FromResult((TResponse)(object)JoinResult);

            if (request is LoginNatayarkCommand)
            {
                if (LoginException is not null)
                    throw LoginException;

                return Task.FromResult((TResponse)(object)true);
            }

            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ResetGameLinkAuthorizationCommand && ResetException is not null)
                throw ResetException;

            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ListObserver<T>(ICollection<T> values) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Assert.Fail(error.ToString());
        }

        public void OnNext(T value)
        {
            values.Add(value);
        }
    }
}
