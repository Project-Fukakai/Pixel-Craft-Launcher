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
using PCL.Core.Link;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelGameLinkViewModelTest
{
    private bool _linkEula;
    private string _refreshToken = string.Empty;
    private FoundWorld[] _discoveredWorlds = [];
    private PlayerProfile[] _players = [];

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _linkEula = States.Link.LinkEula;
        _refreshToken = States.Link.NaidRefreshToken;
        _discoveredWorlds = LobbyService.DiscoveredWorlds.ToArray();
        _players = LobbyService.Players.ToArray();
    }

    [TestCleanup]
    public void Cleanup()
    {
        States.Link.LinkEula = _linkEula;
        States.Link.NaidRefreshToken = _refreshToken;
        LobbyService.DiscoveredWorlds.Clear();
        foreach (var world in _discoveredWorlds)
            LobbyService.DiscoveredWorlds.Add(world);
        LobbyService.Players.Clear();
        foreach (var player in _players)
            LobbyService.Players.Add(player);
    }

    [TestMethod]
    public void InitializationCanOnlyBeginOnce()
    {
        var viewModel = CreateViewModel();

        Assert.IsTrue(viewModel.TryBeginInitialization());
        Assert.IsFalse(viewModel.TryBeginInitialization());
        Assert.IsTrue(viewModel.IsInitialized);
    }

    [TestMethod]
    public void AnnouncementsResetCurrentAndCanAdvance()
    {
        var viewModel = CreateViewModel();
        var first = new LinkAnnounceInfo(LinkAnnounceType.Notice, "first");
        var second = new LinkAnnounceInfo(LinkAnnounceType.Warning, "second");

        viewModel.SetAnnouncements([first, second]);

        Assert.AreEqual("[提示] first", viewModel.AnnouncementSnapshot.Text);
        Assert.IsTrue(viewModel.AdvanceAnnouncement());
        Assert.AreEqual("[注意] second", viewModel.AnnouncementSnapshot.Text);
        Assert.IsTrue(viewModel.AdvanceAnnouncement());
        Assert.AreEqual("[提示] first", viewModel.AnnouncementSnapshot.Text);
    }

    [TestMethod]
    public async Task AcceptAndResetAuthorizationUpdatesState()
    {
        States.Link.LinkEula = false;
        States.Link.NaidRefreshToken = "token";
        var bus = new RecordingCommandBus();
        var viewModel = new PixelGameLinkViewModel(bus);

        viewModel.AcceptEula();

        Assert.IsTrue(States.Link.LinkEula);
        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);

        await viewModel.ResetAuthorizationAsync();

        Assert.IsFalse(States.Link.LinkEula);
        Assert.AreEqual(string.Empty, States.Link.NaidRefreshToken);
        Assert.AreEqual(PixelGameLinkSubpage.Eula, viewModel.Subpage);
        Assert.IsTrue(bus.SentRequests.Any(static request => request is AcceptGameLinkEulaCommand));
        Assert.IsTrue(bus.SentRequests.Any(static request => request is ResetGameLinkAuthorizationCommand));
    }

    [TestMethod]
    public async Task AuthorizationCommandHandlersPublishEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var acceptedEvents = new List<GameLinkEulaAcceptedEvent>();
        var resetEvents = new List<GameLinkAuthorizationResetEvent>();
        using var acceptedSubscription = eventBus.Observe<GameLinkEulaAcceptedEvent>()
            .Subscribe(new ListObserver<GameLinkEulaAcceptedEvent>(acceptedEvents));
        using var resetSubscription = eventBus.Observe<GameLinkAuthorizationResetEvent>()
            .Subscribe(new ListObserver<GameLinkAuthorizationResetEvent>(resetEvents));
        var acceptHandler = new AcceptGameLinkEulaCommandHandler(eventBus);
        var resetHandler = new ResetGameLinkAuthorizationCommandHandler(eventBus);

        States.Link.LinkEula = false;
        States.Link.NaidRefreshToken = "token";
        await acceptHandler.Handle(new AcceptGameLinkEulaCommand(), CancellationToken.None);
        await resetHandler.Handle(new ResetGameLinkAuthorizationCommand(), CancellationToken.None);

        Assert.IsFalse(States.Link.LinkEula);
        Assert.AreEqual(string.Empty, States.Link.NaidRefreshToken);
        Assert.AreEqual(1, acceptedEvents.Count);
        Assert.AreEqual(1, resetEvents.Count);
    }

    [TestMethod]
    public void RuntimeEventsUpdateViewModelSnapshot()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var viewModel = CreateViewModel(eventBus: eventBus);

        eventBus.Publish(new PixelGameLinkRuntimePageRequestedEvent(PixelGameLinkRuntimePage.Finish));
        eventBus.Publish(new PixelGameLinkRuntimeClientPingEvent(42));
        eventBus.Publish(new PixelGameLinkRuntimeServerExceptionEvent("boom"));

        Assert.AreEqual(PixelGameLinkSubpage.Finish, viewModel.Subpage);
        Assert.IsTrue(viewModel.RuntimeSnapshot.HasRefreshRequest);
        Assert.IsFalse(viewModel.RuntimeSnapshot.Rebuild);
        Assert.AreEqual(42, viewModel.RuntimeSnapshot.LastClientPing);
        Assert.AreEqual("boom", viewModel.RuntimeSnapshot.Message);
        Assert.AreEqual(PixelGameLinkRuntimeNotificationKind.CriticalHint, viewModel.RuntimeNotification?.Kind);
        Assert.AreEqual("boom", viewModel.RuntimeNotification?.Message);
        Assert.IsNotNull(viewModel.ConsumeRuntimeNotification());
        Assert.IsNull(viewModel.RuntimeNotification);

        viewModel.ClearRuntimeRefreshRequest();
        Assert.IsFalse(viewModel.RuntimeSnapshot.HasRefreshRequest);

        eventBus.Publish(new PixelGameLinkRuntimePageRequestedEvent(PixelGameLinkRuntimePage.Select));
        eventBus.Publish(new PixelGameLinkRuntimeRefreshRequestedEvent(true));

        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);
        Assert.IsTrue(viewModel.RuntimeSnapshot.HasRefreshRequest);
        Assert.IsTrue(viewModel.RuntimeSnapshot.Rebuild);

        eventBus.Publish(new PixelGameLinkRuntimeUserStoppedGameEvent());

        Assert.AreEqual(PixelGameLinkRuntimeNotificationKind.Message, viewModel.RuntimeNotification?.Kind);
        Assert.AreEqual("大厅已解散", viewModel.RuntimeNotification?.Title);
    }

    [TestMethod]
    public void RuntimeInstallRequestMarksEasyTierStarted()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var viewModel = CreateViewModel(eventBus: eventBus);

        eventBus.Publish(new PixelGameLinkRuntimeEasyTierInstallRequestedEvent());

        Assert.IsTrue(viewModel.IsEasyTierAutoInstallStarted);
        Assert.IsTrue(viewModel.RuntimeSnapshot.HasRefreshRequest);
        Assert.IsTrue(viewModel.RuntimeSnapshot.Rebuild);
    }

    [TestMethod]
    public void RuntimeRefreshPresentationConsumesNotificationAndClearsRequest()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var viewModel = CreateViewModel(eventBus: eventBus);

        eventBus.Publish(new PixelGameLinkRuntimeRefreshRequestedEvent(false));
        eventBus.Publish(new PixelGameLinkRuntimeUserStoppedGameEvent());

        var presentation = viewModel.ConsumeRuntimeRefreshPresentation(rebuildRequested: true);

        Assert.IsTrue(presentation.Rebuild);
        Assert.AreEqual(PixelGameLinkRuntimeNotificationKind.Message, presentation.Notification?.Kind);
        Assert.AreEqual("大厅已解散", presentation.Notification?.Title);
        Assert.IsNull(viewModel.RuntimeNotification);
        Assert.IsFalse(viewModel.RuntimeSnapshot.HasRefreshRequest);
    }

    [TestMethod]
    public void AccountEventsNotifyAccountSnapshotChanges()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var viewModel = CreateViewModel(eventBus: eventBus);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        eventBus.Publish(new NatayarkLoginCompletedEvent(true));
        eventBus.Publish(new NatayarkLoggedOutEvent());

        Assert.IsTrue(changed.Contains(nameof(PixelGameLinkViewModel.AccountSnapshot)));
        Assert.IsTrue(changed.Contains(nameof(PixelGameLinkViewModel.IsNatayarkLoggedIn)));
        Assert.IsTrue(changed.Contains(nameof(PixelGameLinkViewModel.SidebarSnapshot)));
    }

    [TestMethod]
    public void AccountEventsDriveGameLinkStateMachine()
    {
        using var eventBus = new ReactivePixelEventBus();
        var stateEvents = new List<PixelStateChangedEvent>();
        using var stateSubscription = eventBus.Observe<PixelStateChangedEvent>()
            .Subscribe(new ListObserver<PixelStateChangedEvent>(stateEvents));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var stateMachine = new PixelGameLinkStateMachine(factory);
        using var viewModel = CreateViewModel(stateMachine: stateMachine, eventBus: eventBus);

        eventBus.Publish(new NatayarkLoginCompletedEvent(true));

        Assert.AreEqual(PixelGameLinkState.Ready, stateMachine.State);
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(PixelGameLinkTrigger.StartLogin),
                nameof(PixelGameLinkTrigger.LoginSucceeded)
            },
            stateEvents.Select(static @event => @event.Trigger).ToArray());
    }

    [TestMethod]
    public void AccountSnapshotPromptsLoginWhenTokenIsMissing()
    {
        States.Link.NaidRefreshToken = string.Empty;
        var viewModel = CreateViewModel();

        var snapshot = viewModel.AccountSnapshot;

        Assert.IsFalse(snapshot.IsLoggedIn);
        Assert.AreEqual("点击登录 Natayark 账户", snapshot.Text);
        Assert.AreEqual("NAT 测试", snapshot.NatTestButtonText);
        Assert.AreEqual("正在测试", snapshot.NatTestBusyText);
    }

    [TestMethod]
    public void EulaAndFooterSnapshotsExposeServiceTextAndLinks()
    {
        var viewModel = CreateViewModel();

        var eula = viewModel.EulaSnapshot;
        var footer = viewModel.FooterSnapshot;
        var setup = viewModel.SetupPageSnapshot;

        Assert.AreEqual("PCL CE 联机大厅说明与条款", eula.Title);
        StringAssert.Contains(eula.IntroText, "使用大厅功能");
        Assert.AreEqual("我已阅读并同意", eula.AcceptButtonText);
        Assert.AreEqual(2, eula.Links.Count);
        Assert.AreEqual("PCL CE 大厅相关隐私政策", eula.Links[0].Text);
        Assert.AreEqual("https://www.pclc.cc/privacy/personal-info-brief.html", eula.Links[0].Url);
        StringAssert.Contains(eula.CommitmentText, "我确认我已满十四周岁");

        Assert.AreEqual("联机服务", footer.Title);
        Assert.AreEqual("停用联机功能", footer.DisableButtonText);
        Assert.AreEqual("撤销授权确认", footer.DisableConfirmation.Title);
        Assert.AreEqual("确定", footer.DisableConfirmation.PrimaryButtonText);
        Assert.AreEqual("友情链接：", footer.FriendPrefix);
        Assert.IsTrue(footer.Links.Any(static link => link.Text == "违法违规举报"));
        Assert.IsTrue(footer.FriendLinks.Any(static link => link.Text == "EasyTier 工具官网"));

        Assert.AreEqual("修改此处的设置后，需要重新启动大厅以使设置生效。", setup.RestartRequiredHint);
        Assert.AreEqual("网络测试", setup.NetworkTestTitle);
    }

    [TestMethod]
    public void SelectCardSnapshotsExposeUiTextAndWorlds()
    {
        LobbyService.DiscoveredWorlds.Add(new FoundWorld("World", 25565));
        var viewModel = CreateViewModel();

        var create = viewModel.CreateCardSnapshot;
        var join = viewModel.JoinCardSnapshot;

        Assert.AreEqual("创建大厅", create.Title);
        Assert.AreEqual("创建", create.CreateButtonText);
        Assert.AreEqual("刷新", create.RefreshButtonText);
        Assert.AreEqual("手动输入", create.ManualButtonText);
        Assert.AreEqual("手动输入端口", create.ManualPortDialog.Title);
        Assert.AreEqual("请输入 1024-65535 之间的端口", create.ManualPortDialog.ValidationErrorText);
        Assert.AreEqual(3, create.Steps.Count);
        Assert.IsTrue(create.CanCreate);
        Assert.AreEqual(0, create.DefaultWorldIndex);
        Assert.AreEqual("World", create.Worlds[0].Name);
        Assert.AreEqual(25565, create.Worlds[0].Port);

        Assert.AreEqual("加入大厅", join.Title);
        Assert.AreEqual("编号应该长得像这样：U/NNNN-AAAA-SSSS-EEEE", join.InputHint);
        Assert.AreEqual("清除", join.ClearButtonText);
        Assert.AreEqual("粘贴", join.PasteButtonText);
        Assert.AreEqual("加入", join.JoinButtonText);
        Assert.AreEqual(2, join.Steps.Count);
    }

    [TestMethod]
    public void EasyTierAutoInstallDoesNotStartBeforeEula()
    {
        States.Link.LinkEula = false;
        var viewModel = new PixelGameLinkViewModel(new RecordingCommandBus());

        Assert.IsFalse(viewModel.ShouldStartEasyTierAutoInstall());
        Assert.IsFalse(viewModel.IsEasyTierAutoInstallStarted);
    }

    [TestMethod]
    public void NatayarkLoginMessagesAreProvidedByViewModel()
    {
        Assert.AreEqual("请在浏览器中完成 Natayark 登录。", PixelGameLinkViewModel.GetNatayarkLoginStartMessage());
        Assert.AreEqual(
            "Natayark 登录失败：boom",
            PixelGameLinkViewModel.GetNatayarkLoginExceptionMessage(new InvalidOperationException("boom")));
        Assert.AreEqual("已复制大厅编号。", PixelGameLinkViewModel.GetLobbyCodeCopiedMessage());
        Assert.AreEqual("请输入有效端口。", PixelGameLinkViewModel.GetInvalidPortMessage());

        var ipDialog = PixelGameLinkViewModel.GetVirtualIpDialogSnapshot("127.0.0.1:25565");
        var portDialog = PixelGameLinkViewModel.GetManualPortDialogSnapshot();

        Assert.AreEqual("复制 IP", ipDialog.Title);
        StringAssert.Contains(ipDialog.Body, "127.0.0.1:25565");
        Assert.AreEqual("复制", ipDialog.CopyButtonText);
        Assert.AreEqual("返回", ipDialog.BackButtonText);
        Assert.AreEqual("手动输入端口", portDialog.Title);
        Assert.AreEqual("1024-65535", portDialog.InputHint);
        Assert.AreEqual("创建", portDialog.CreateButtonText);
        Assert.AreEqual("取消", portDialog.CancelButtonText);
        Assert.AreEqual("请输入 1024-65535 之间的端口", portDialog.ValidationErrorText);
    }

    [TestMethod]
    public void NatDialogSnapshotFormatsNatTestResult()
    {
        var dialog = PixelGameLinkViewModel.GetNatDialogSnapshot(
            PixelGameLinkNatTestResult.Success("Full Cone", "Symmetric", supportsIPv6: true));

        Assert.AreEqual("NAT 类型", dialog.Title);
        StringAssert.Contains(dialog.Body, "UDP NAT 类型: Full Cone");
        StringAssert.Contains(dialog.Body, "TCP NAT 类型: Symmetric");
        StringAssert.Contains(dialog.Body, "IPv6: 支持");
    }

    [TestMethod]
    public void NatStatusSnapshotFormatsInlineNetworkTestState()
    {
        var initial = PixelGameLinkViewModel.GetInitialNatStatusSnapshot();
        var success = PixelGameLinkViewModel.GetNatStatusSnapshot(
            PixelGameLinkNatTestResult.Success("Full Cone", "Symmetric", supportsIPv6: false));
        var messages = PixelGameLinkViewModel.GetNetworkTestMessages();

        Assert.AreEqual("UDP NAT 类型: 尚未检测", initial.UdpText);
        Assert.AreEqual("TCP NAT 类型: 尚未检测", initial.TcpText);
        Assert.AreEqual("IPv6: 尚未检测", initial.Ipv6Text);
        Assert.AreEqual("UDP NAT 类型: Full Cone", success.UdpText);
        Assert.AreEqual("TCP NAT 类型: Symmetric", success.TcpText);
        Assert.AreEqual("IPv6: 不支持", success.Ipv6Text);
        Assert.AreEqual("开始测试", messages.StartButtonText);
        Assert.AreEqual("正在测试", messages.TestingButtonText);
        Assert.AreEqual("网络测试完成。", messages.CompletedMessage);
        Assert.AreEqual("网络测试失败：boom", messages.GetFailureMessage(new InvalidOperationException("boom")));
        Assert.AreEqual(
            "网络测试失败：boom",
            PixelGameLinkViewModel.GetNetworkTestExceptionMessage(new InvalidOperationException("boom")));
    }

    [TestMethod]
    public void AnnouncementSnapshotMapsWarningAnnouncement()
    {
        var viewModel = CreateViewModel();
        viewModel.SetAnnouncements([new LinkAnnounceInfo(LinkAnnounceType.Warning, "be careful")]);

        var snapshot = viewModel.AnnouncementSnapshot;

        Assert.AreEqual(PixelGameLinkAnnouncementSeverity.Warning, snapshot.Severity);
        Assert.AreEqual("[注意] be careful", snapshot.Text);
    }

    [TestMethod]
    public void AnnouncementSnapshotShowsUnavailableLobby()
    {
        var viewModel = CreateViewModel();
        viewModel.SetLobbyAvailability(false);

        var snapshot = viewModel.AnnouncementSnapshot;

        Assert.AreEqual(PixelGameLinkAnnouncementSeverity.Important, snapshot.Severity);
        Assert.AreEqual("大厅功能暂不可用，请稍后再试。", snapshot.Text);
    }

    [TestMethod]
    public void AnnouncementLoadedEventUpdatesSnapshot()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var viewModel = CreateViewModel(eventBus: eventBus);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        eventBus.Publish(new GameLinkAnnouncementsLoadedEvent(
            [new PixelGameLinkAnnouncementSnapshot(PixelGameLinkAnnouncementSeverity.Notice, "[提示] loaded")],
            false));

        Assert.IsFalse(viewModel.IsLobbyAvailable);
        Assert.AreEqual("[提示] loaded", viewModel.AnnouncementSnapshot.Text);
        Assert.IsTrue(changed.Contains(nameof(PixelGameLinkViewModel.AnnouncementSnapshot)));
    }

    [TestMethod]
    public void SidebarSnapshotAndSelectNavigationRespectEula()
    {
        States.Link.LinkEula = false;
        var viewModel = new PixelGameLinkViewModel(new RecordingCommandBus());

        var snapshot = viewModel.SidebarSnapshot;

        Assert.AreEqual("联机大厅", snapshot.Title);
        Assert.AreEqual("大厅公告", snapshot.AnnouncementTitle);
        Assert.AreEqual("服务状态与通知", snapshot.AnnouncementInfo);
        Assert.AreEqual("mdi-bullhorn-outline", snapshot.AnnouncementIcon);
        Assert.AreEqual("使用协议", snapshot.EulaTitle);
        Assert.AreEqual("mdi-file-document-outline", snapshot.EulaIcon);
        Assert.AreEqual("加入 / 创建", snapshot.SelectTitle);
        Assert.AreEqual("选择大厅或开放 LAN", snapshot.SelectInfo);
        Assert.AreEqual("mdi-lan-connect", snapshot.SelectIcon);
        Assert.AreEqual("当前大厅", snapshot.CurrentLobbyTitle);
        Assert.AreEqual("mdi-account-multiple-outline", snapshot.CurrentLobbyIcon);
        Assert.AreEqual("EasyTier", snapshot.EasyTierTitle);
        Assert.AreEqual("mdi-download-network-outline", snapshot.EasyTierIcon);
        Assert.AreEqual("控件验收", snapshot.TestEntryTitle);
        Assert.AreEqual("迁移组件预览", snapshot.TestEntryInfo);
        Assert.AreEqual("mdi-tools", snapshot.TestEntryIcon);
        Assert.AreEqual("测试入口", snapshot.TestEntryCardTitle);
        Assert.AreEqual("首次使用需确认", snapshot.EulaInfo);
        viewModel.ShowSelectOrEula();
        Assert.AreEqual(PixelGameLinkSubpage.Eula, viewModel.Subpage);

        viewModel.AcceptEula();
        snapshot = viewModel.SidebarSnapshot;

        Assert.AreEqual("已同意", snapshot.EulaInfo);
        viewModel.ShowSelectOrEula();
        Assert.AreEqual(PixelGameLinkSubpage.Select, viewModel.Subpage);
    }

    [TestMethod]
    public void DiscoveredWorldsAreProjectedToSnapshots()
    {
        LobbyService.DiscoveredWorlds.Clear();
        LobbyService.DiscoveredWorlds.Add(new FoundWorld("World A", 25565));
        LobbyService.DiscoveredWorlds.Add(new FoundWorld("World B", 24454));
        var viewModel = CreateViewModel();

        var worlds = viewModel.DiscoveredWorldsSnapshot;

        Assert.AreEqual(2, worlds.Count);
        Assert.AreEqual(new PixelGameLinkWorldSnapshot("World A", 25565), worlds[0]);
        Assert.AreEqual(new PixelGameLinkWorldSnapshot("World B", 24454), worlds[1]);
    }

    [TestMethod]
    public void FinishSnapshotProjectsPlayersForUi()
    {
        LobbyService.Players.Clear();
        LobbyService.Players.Add(new PlayerProfile
        {
            Name = "Host",
            MachineId = "host-machine",
            Vendor = "client-a",
            Kind = PlayerKind.HOST
        });
        var viewModel = CreateViewModel();

        var snapshot = viewModel.FinishSnapshot;

        Assert.AreEqual(1, snapshot.Players.Count);
        Assert.IsTrue(snapshot.HasPlayers);
        CollectionAssert.Contains(snapshot.InfoLines.ToArray(), "用户类型: " + (snapshot.IsHost ? "创建者" : "加入者"));
        Assert.AreEqual("大厅信息", snapshot.InfoTitle);
        Assert.AreEqual("大厅操作", snapshot.ActionsTitle);
        Assert.AreEqual("大厅成员列表（共 1 人）", snapshot.MembersTitle);
        Assert.AreEqual("正在获取信息", snapshot.EmptyPlayersText);
        Assert.AreEqual("复制大厅编号", snapshot.CopyCodeButtonText);
        Assert.AreEqual("复制虚拟 IP", snapshot.CopyVirtualIpButtonText);
        Assert.AreEqual(snapshot.IsHost ? "关闭大厅" : "退出大厅", snapshot.ExitButtonText);
        Assert.AreEqual(!snapshot.IsHost && !string.IsNullOrWhiteSpace(snapshot.VirtualIp), snapshot.CanCopyVirtualIp);
        Assert.AreEqual(PixelGameLinkViewModel.GetLobbyCodeCopiedMessage(), snapshot.LobbyCodeCopiedMessage);
        Assert.AreEqual(string.IsNullOrWhiteSpace(snapshot.VirtualIp), snapshot.VirtualIpDialog is null);
        var player = snapshot.Players.Single();
        Assert.AreEqual("Host", player.Name);
        Assert.AreEqual("[主机] client-a", player.Info);
        Assert.AreEqual("mdi-crown-outline", player.Icon);
        StringAssert.Contains(player.DetailBody, "client-a");
    }

    [TestMethod]
    public void EasyTierSnapshotExposesCardActions()
    {
        var viewModel = CreateViewModel();

        var snapshot = viewModel.EasyTierSnapshot;

        Assert.AreEqual("EasyTier 依赖", snapshot.Title);
        Assert.AreEqual(
            snapshot.State == EasyTierDependencyState.Failed ? "重试安装" : "立即安装",
            snapshot.InstallText);
        Assert.AreEqual("重新检测", snapshot.CheckButtonText);
    }

    private static PixelGameLinkViewModel CreateViewModel(
        PixelGameLinkStateMachine? stateMachine = null,
        IPixelEventBus? eventBus = null)
    {
        return new PixelGameLinkViewModel(new RecordingCommandBus(), stateMachine, eventBus);
    }

    private sealed class RecordingCommandBus : IPixelCommandBus
    {
        public List<object> SentRequests { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            switch (request)
            {
                case AcceptGameLinkEulaCommand:
                    States.Link.LinkEula = true;
                    break;
                case ResetGameLinkAuthorizationCommand:
                    States.Link.NaidRefreshToken = string.Empty;
                    States.Link.LinkEula = false;
                    break;
                default:
                    throw new NotSupportedException(request.GetType().Name);
            }

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
