using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using PCL.Core.App;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.App.Pixel.ViewModels;

public enum PixelGameLinkSubpage
{
    Eula,
    Select,
    Finish
}

public sealed class PixelGameLinkViewModel : PixelViewModelBase, IDisposable
{
    private readonly IPixelCommandBus _commandBus;
    private readonly PixelGameLinkStateMachine? _stateMachine;
    private readonly List<IDisposable> _runtimeSubscriptions = [];
    private bool _isInitialized;
    private bool _isEasyTierAutoInstallStarted;
    private bool _isAnnouncementLoading;
    private bool _isLobbyAvailable = true;
    private int _announcementIndex;
    private PixelGameLinkSubpage _subpage = PixelGameLinkSubpage.Eula;
    private bool _hasRuntimeRefreshRequest;
    private bool _lastRuntimeRefreshRebuild;
    private long? _lastRuntimeClientPing;
    private string? _lastRuntimeMessage;
    private PixelGameLinkRuntimeNotification? _runtimeNotification;

    public PixelGameLinkViewModel(
        IPixelCommandBus commandBus,
        PixelGameLinkStateMachine? stateMachine = null,
        IPixelEventBus? eventBus = null)
    {
        _commandBus = commandBus;
        _stateMachine = stateMachine;
        if (eventBus is not null)
            SubscribeRuntimeEvents(eventBus);
    }

    private ObservableCollection<PixelGameLinkAnnouncementSnapshot> Announcements { get; } = [];

    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetField(ref _isInitialized, value);
    }

    public bool IsEasyTierAutoInstallStarted
    {
        get => _isEasyTierAutoInstallStarted;
        set => SetField(ref _isEasyTierAutoInstallStarted, value);
    }

    public bool IsAnnouncementLoading
    {
        get => _isAnnouncementLoading;
        private set
        {
            if (SetField(ref _isAnnouncementLoading, value))
                OnPropertyChanged(nameof(AnnouncementSnapshot));
        }
    }

    public bool IsLobbyAvailable
    {
        get => _isLobbyAvailable;
        private set
        {
            if (SetField(ref _isLobbyAvailable, value))
                OnPropertyChanged(nameof(AnnouncementSnapshot));
        }
    }

    public int AnnouncementIndex
    {
        get => _announcementIndex;
        private set
        {
            if (SetField(ref _announcementIndex, value))
                OnPropertyChanged(nameof(AnnouncementSnapshot));
        }
    }

    private PixelGameLinkAnnouncementSnapshot? CurrentAnnouncement =>
        Announcements.Count == 0
            ? null
            : Announcements[Math.Clamp(AnnouncementIndex, 0, Announcements.Count - 1)];

    public PixelGameLinkAnnouncementSnapshot AnnouncementSnapshot => GetAnnouncementSnapshot(IsLobbyAvailable);

    public PixelGameLinkSubpage Subpage
    {
        get => _subpage;
        private set => SetField(ref _subpage, value);
    }

    public PixelGameLinkRuntimeSnapshot RuntimeSnapshot => new(
        _hasRuntimeRefreshRequest,
        _lastRuntimeRefreshRebuild,
        _lastRuntimeClientPing,
        _lastRuntimeMessage);

    public PixelGameLinkRuntimeNotification? RuntimeNotification => _runtimeNotification;

    public PixelGameLinkEulaSnapshot EulaSnapshot => GetEulaSnapshot();

    public PixelGameLinkFooterSnapshot FooterSnapshot => GetFooterSnapshot();

    public PixelGameLinkSetupPageSnapshot SetupPageSnapshot => GetSetupPageSnapshot();

    public PixelGameLinkAccountSnapshot AccountSnapshot => GetAccountSnapshot();

    public PixelGameLinkEasyTierSnapshot EasyTierSnapshot => GetEasyTierSnapshot();

    public IReadOnlyList<PixelGameLinkWorldSnapshot> DiscoveredWorldsSnapshot => GetDiscoveredWorlds();

    public PixelGameLinkCreateCardSnapshot CreateCardSnapshot => GetCreateCardSnapshot();

    public PixelGameLinkJoinCardSnapshot JoinCardSnapshot => GetJoinCardSnapshot();

    public PixelGameLinkFinishSnapshot FinishSnapshot => GetFinishSnapshot();

    public string? CurrentLobbyCode => LobbyService.CurrentLobbyCode;

    public PixelGameLinkSidebarSnapshot SidebarSnapshot => GetSidebarSnapshot();

    public void AcceptEula()
    {
        AcceptEulaAsync().GetAwaiter().GetResult();
    }

    public async Task AcceptEulaAsync(CancellationToken cancellationToken = default)
    {
        await RequireCommandBus()
            .Send(new AcceptGameLinkEulaCommand(), cancellationToken)
            .ConfigureAwait(false);
        TryFireGameLinkTrigger(PixelGameLinkTrigger.AcceptEula);
        Subpage = PixelGameLinkSubpage.Select;
    }

    internal async Task ResetAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        await RequireCommandBus()
            .Send(new ResetGameLinkAuthorizationCommand(), cancellationToken)
            .ConfigureAwait(false);
        ApplyAuthorizationReset();
    }

    internal void ApplyAuthorizationReset()
    {
        BeginGameLinkState(PixelGameLinkTrigger.RequireEula);
        IsEasyTierAutoInstallStarted = false;
        Subpage = PixelGameLinkSubpage.Eula;
    }

    public void ShowEula() => Subpage = PixelGameLinkSubpage.Eula;

    public void ShowSelect() => Subpage = PixelGameLinkSubpage.Select;

    public void ShowFinish() => Subpage = PixelGameLinkSubpage.Finish;

    public void ShowSelectOrEula()
    {
        if (States.Link.LinkEula)
            ShowSelect();
        else
            ShowEula();
    }

    public bool TryShowConnectedLobby()
    {
        if (LobbyService.CurrentState != LobbyState.Connected)
            return false;

        ShowFinish();
        return true;
    }

    public bool TryBeginInitialization()
    {
        if (IsInitialized)
            return false;

        IsInitialized = true;
        return true;
    }

    public bool TryMarkEasyTierAutoInstallStarted()
    {
        if (IsEasyTierAutoInstallStarted)
            return false;

        IsEasyTierAutoInstallStarted = true;
        return true;
    }

    public void BeginAnnouncementLoading()
    {
        IsAnnouncementLoading = true;
    }

    internal void SetAnnouncements(IEnumerable<LinkAnnounceInfo> announcements)
    {
        SetAnnouncementSnapshots(GetAnnouncementSnapshots(announcements));
    }

    internal void SetAnnouncementSnapshots(IEnumerable<PixelGameLinkAnnouncementSnapshot> announcements)
    {
        Announcements.Clear();
        foreach (var announcement in announcements)
            Announcements.Add(announcement);
        AnnouncementIndex = 0;
        OnPropertyChanged(nameof(AnnouncementSnapshot));
    }

    public void SetLobbyAvailability(bool isAvailable)
    {
        IsLobbyAvailable = isAvailable;
    }

    public void SetAnnouncementError(string message)
    {
        SetAnnouncementSnapshots([
            new PixelGameLinkAnnouncementSnapshot(PixelGameLinkAnnouncementSeverity.Important, message)
        ]);
    }

    public void EndAnnouncementLoading()
    {
        IsAnnouncementLoading = false;
    }

    public bool AdvanceAnnouncement()
    {
        if (Announcements.Count == 0)
            return false;

        AnnouncementIndex = (AnnouncementIndex + 1) % Announcements.Count;
        OnPropertyChanged(nameof(AnnouncementSnapshot));
        return true;
    }

    public bool ShouldStartEasyTierAutoInstall()
    {
        if (!States.Link.LinkEula || !EasyTierMetadata.IsPlatformSupported)
            return false;

        if (EasyTierDependencyService.State is EasyTierDependencyState.Installed or EasyTierDependencyState.Installing)
            return false;

        return TryMarkEasyTierAutoInstallStarted();
    }

    public PixelGameLinkAnnouncementSnapshot GetAnnouncementSnapshot(bool isLobbyAvailable)
    {
        if (CurrentAnnouncement is { } announcement)
            return announcement;

        if (!isLobbyAvailable && !IsAnnouncementLoading)
            return new PixelGameLinkAnnouncementSnapshot(
                PixelGameLinkAnnouncementSeverity.Important,
                "大厅功能暂不可用，请稍后再试。");

        return new PixelGameLinkAnnouncementSnapshot(
            PixelGameLinkAnnouncementSeverity.Notice,
            IsAnnouncementLoading ? "正在连接到大厅服务器..." : "大厅服务正在初始化...");
    }

    internal static IReadOnlyList<PixelGameLinkAnnouncementSnapshot> GetAnnouncementSnapshots(IEnumerable<LinkAnnounceInfo> announcements)
    {
        return announcements.Select(GetAnnouncementSnapshot).ToArray();
    }

    private static PixelGameLinkAnnouncementSnapshot GetAnnouncementSnapshot(LinkAnnounceInfo announcement)
    {
        return announcement.Type switch
        {
            LinkAnnounceType.Important => new PixelGameLinkAnnouncementSnapshot(
                PixelGameLinkAnnouncementSeverity.Important,
                $"[重要] {announcement.Content}"),
            LinkAnnounceType.Warning => new PixelGameLinkAnnouncementSnapshot(
                PixelGameLinkAnnouncementSeverity.Warning,
                $"[注意] {announcement.Content}"),
            _ => new PixelGameLinkAnnouncementSnapshot(
                PixelGameLinkAnnouncementSeverity.Notice,
                $"[提示] {announcement.Content}")
        };
    }

    public PixelGameLinkEulaSnapshot GetEulaSnapshot()
    {
        return new PixelGameLinkEulaSnapshot(
            "PCL CE 联机大厅说明与条款",
            "使用大厅功能即代表你同意相关服务文档及下列条款。",
            [
                new PixelGameLinkLinkSnapshot(
                    "PCL CE 大厅相关隐私政策",
                    "https://www.pclc.cc/privacy/personal-info-brief.html",
                    "了解 PCL CE 如何处理个人信息"),
                new PixelGameLinkLinkSnapshot(
                    "Natayark Network 用户协议与隐私政策",
                    "https://account.naids.com/policy",
                    "查看 Natayark OpenID 服务条款")
            ],
            "我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。\n我承诺使用大厅功能带来的一切风险自行承担。\n我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他必要信息。\n为保护未成年人个人信息，使用联机大厅前，我确认我已满十四周岁。",
            "我已阅读并同意");
    }

    public PixelGameLinkFooterSnapshot GetFooterSnapshot()
    {
        return new PixelGameLinkFooterSnapshot(
            "联机服务",
            [
                new PixelGameLinkLinkSnapshot("违法违规举报", "https://qm.qq.com/q/zfml1KaPWS"),
                new PixelGameLinkLinkSnapshot("Natayark Network 用户协议与隐私政策", "https://account.naids.com/policy"),
                new PixelGameLinkLinkSnapshot("大厅隐私协议", "https://www.pclc.cc/privacy/personal-info-brief.html")
            ],
            "友情链接：",
            [
                new PixelGameLinkLinkSnapshot("EasyTier 工具官网", "https://easytier.cn/"),
                new PixelGameLinkLinkSnapshot("Pysio's Home", "https://pysio.online/")
            ],
            "停用联机功能",
            new PixelGameLinkConfirmationSnapshot(
                "撤销授权确认",
                "你确定要撤销联机协议授权吗？",
                "确定",
                "取消"));
    }

    public static PixelGameLinkSetupPageSnapshot GetSetupPageSnapshot()
    {
        return new PixelGameLinkSetupPageSnapshot(
            "修改此处的设置后，需要重新启动大厅以使设置生效。",
            "网络测试");
    }

    public PixelGameLinkAccountSnapshot GetAccountSnapshot()
    {
        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            return new PixelGameLinkAccountSnapshot("点击登录 Natayark 账户", false, "NAT 测试", "正在测试");

        if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
            return new PixelGameLinkAccountSnapshot("Natayark 账户信息待刷新", true, "NAT 测试", "正在测试");

        return NatayarkProfileManager.NaidProfile.Status == 0
            ? new PixelGameLinkAccountSnapshot(NatayarkProfileManager.NaidProfile.Username!, true, "NAT 测试", "正在测试")
            : new PixelGameLinkAccountSnapshot($"{NatayarkProfileManager.NaidProfile.Username}（状态异常）", true, "NAT 测试", "正在测试");
    }

    public PixelGameLinkEasyTierSnapshot GetEasyTierSnapshot()
    {
        var state = EasyTierMetadata.IsInstalled()
            ? EasyTierDependencyState.Installed
            : EasyTierDependencyService.State;
        var stateText = state switch
        {
            EasyTierDependencyState.Installed => "已安装，联机功能可直接使用。",
            EasyTierDependencyState.Installing => "正在自动安装 EasyTier 依赖，请稍候...",
            EasyTierDependencyState.Failed => "安装失败：" + (EasyTierDependencyService.LastError ?? "未知错误"),
            EasyTierDependencyState.Unsupported => EasyTierMetadata.GetUnsupportedReason(),
            _ => "尚未安装，将在进入联机流程时自动下载并安装。"
        };

        return new PixelGameLinkEasyTierSnapshot(
            "EasyTier 依赖",
            state,
            stateText,
            EasyTierMetadata.IsPlatformSupported
                ? $"平台包: {EasyTierMetadata.Platform.PlatformId}\n安装目录: {EasyTierMetadata.EasyTierFilePath}"
                : EasyTierMetadata.GetUnsupportedReason(),
            EasyTierMetadata.IsPlatformSupported,
            state is EasyTierDependencyState.NotInstalled or EasyTierDependencyState.Failed,
            state == EasyTierDependencyState.Failed ? "重试安装" : "立即安装",
            "重新检测",
            EasyTierMetadata.IsInstalled()
                ? "EasyTier 依赖已就绪。"
                : EasyTierMetadata.IsPlatformSupported
                    ? "尚未检测到完整 EasyTier 依赖。"
                    : EasyTierMetadata.GetUnsupportedReason(),
            EasyTierMetadata.IsInstalled());
    }

    public PixelGameLinkFinishSnapshot GetFinishSnapshot()
    {
        var isHost = LobbyService.IsHost;
        var lobbyCode = LobbyService.CurrentLobbyCode ?? "正在获取";
        var userName = LobbyService.CurrentUserName ?? LobbyInfoProvider.GetUsername() ?? "未知用户";
        var lobbyStateText = LobbyService.CurrentState == LobbyState.Connected ? "已连接" : LobbyService.CurrentState.ToString();
        var virtualIp = LobbyInfoProvider.McForward is null ? null : $"127.0.0.1:{LobbyInfoProvider.McForward.LocalPort}";
        var players = LobbyService.Players.Select(ToPlayerSnapshot).ToArray();
        return new PixelGameLinkFinishSnapshot(
            isHost,
            lobbyCode,
            userName,
            lobbyStateText,
            virtualIp,
            players,
            players.Length > 0,
            [
                "连接状况: " + lobbyStateText,
                "大厅编号: " + lobbyCode,
                "用户名: " + userName,
                "用户类型: " + (isHost ? "创建者" : "加入者")
            ],
            "大厅信息",
            "大厅操作",
            "正在获取信息",
            $"大厅成员列表（共 {players.Length} 人）",
            "复制大厅编号",
            "复制虚拟 IP",
            isHost ? "关闭大厅" : "退出大厅",
            !isHost && !string.IsNullOrWhiteSpace(virtualIp),
            GetLobbyCodeCopiedMessage(),
            string.IsNullOrWhiteSpace(virtualIp) ? null : GetVirtualIpDialogSnapshot(virtualIp));
    }

    public IReadOnlyList<PixelGameLinkWorldSnapshot> GetDiscoveredWorlds()
    {
        return LobbyService.DiscoveredWorlds
            .Select(static world => new PixelGameLinkWorldSnapshot(world.Name, world.Port))
            .ToArray();
    }

    public PixelGameLinkCreateCardSnapshot GetCreateCardSnapshot()
    {
        var worlds = GetDiscoveredWorlds();
        return new PixelGameLinkCreateCardSnapshot(
            "创建大厅",
            [
                "1、进入世界后，在游戏菜单中选择“对局域网开放”。",
                "2、在下方选择此游戏实例，单击“创建”。",
                "3、成功创建大厅后，复制大厅编号并发送给朋友。"
            ],
            worlds,
            worlds.Count > 0,
            worlds.Count > 0 ? 0 : -1,
            "手动输入",
            "刷新",
            "创建",
            GetManualPortDialogSnapshot());
    }

    public static PixelGameLinkJoinCardSnapshot GetJoinCardSnapshot()
    {
        return new PixelGameLinkJoinCardSnapshot(
            "加入大厅",
            "编号应该长得像这样：U/NNNN-AAAA-SSSS-EEEE",
            [
                "1、输入朋友发送给你的大厅编号，单击“加入”。",
                "2、启动游戏，选择“多人游戏”，在局域网游戏中加入大厅。"
            ],
            "清除",
            "粘贴",
            "加入");
    }

    public string? GetCurrentLobbyCode() => CurrentLobbyCode;

    public PixelGameLinkSidebarSnapshot GetSidebarSnapshot()
    {
        return new PixelGameLinkSidebarSnapshot(
            States.Link.LinkEula ? "已同意" : "首次使用需确认",
            LobbyService.CurrentState == LobbyState.Connected
                ? LobbyService.CurrentLobbyCode ?? "已连接"
                : "等待连接",
            LobbyService.CurrentState == LobbyState.Connected,
            GetEasyTierSidebarText());
    }

    public bool IsNatayarkLoggedIn => !string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken);

    public static string GetEasyTierInstallResultMessage(bool isInstalled) =>
        isInstalled ? "EasyTier 依赖已就绪。" : EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。";

    public static string GetNatayarkLoginStartMessage() => "请在浏览器中完成 Natayark 登录。";

    public static string GetNatayarkLoginExceptionMessage(Exception exception) =>
        "Natayark 登录失败：" + exception.Message;

    public static string GetLobbyCodeCopiedMessage() => "已复制大厅编号。";

    public static string GetInvalidPortMessage() => "请输入有效端口。";

    public static PixelGameLinkVirtualIpDialogSnapshot GetVirtualIpDialogSnapshot(string ip) =>
        new(
            "复制 IP",
            $"大厅创建者的游戏地址：{ip}\n注意：仅推荐在 MC 多人游戏列表不显示大厅广播时使用 IP 连接。",
            "复制",
            "返回");

    public static PixelGameLinkManualPortDialogSnapshot GetManualPortDialogSnapshot() =>
        new(
            "手动输入端口",
            "1024-65535",
            "创建",
            "取消",
            "请输入 1024-65535 之间的端口");

    public static PixelGameLinkNatDialogSnapshot GetNatDialogSnapshot(PixelGameLinkNatTestResult result)
    {
        return new PixelGameLinkNatDialogSnapshot(
            "NAT 类型",
            $"UDP NAT 类型: {result.UdpNatType}\nTCP NAT 类型: {result.TcpNatType}\nIPv6: {(result.SupportsIPv6 ? "支持" : "不支持")}");
    }

    public static PixelGameLinkNatStatusSnapshot GetInitialNatStatusSnapshot()
    {
        return new PixelGameLinkNatStatusSnapshot(
            "UDP NAT 类型: 尚未检测",
            "TCP NAT 类型: 尚未检测",
            "IPv6: 尚未检测");
    }

    public static PixelGameLinkNatStatusSnapshot GetNatStatusSnapshot(PixelGameLinkNatTestResult result)
    {
        return new PixelGameLinkNatStatusSnapshot(
            "UDP NAT 类型: " + result.UdpNatType,
            "TCP NAT 类型: " + result.TcpNatType,
            "IPv6: " + (result.SupportsIPv6 ? "支持" : "不支持"));
    }

    public static PixelGameLinkNetworkTestMessages GetNetworkTestMessages()
    {
        return new PixelGameLinkNetworkTestMessages(
            "开始测试",
            "正在测试",
            "网络测试完成。",
            "网络测试失败：");
    }

    public static string GetNetworkTestExceptionMessage(Exception exception) =>
        GetNetworkTestMessages().GetFailureMessage(exception);

    private void BeginGameLinkState(PixelGameLinkTrigger trigger)
    {
        if (_stateMachine is null)
            return;

        if (_stateMachine.CanFire(trigger))
        {
            TryFireGameLinkTrigger(trigger);
            return;
        }

        if (_stateMachine.CanFire(PixelGameLinkTrigger.Reset))
            TryFireGameLinkTrigger(PixelGameLinkTrigger.Reset);

        TryFireGameLinkTrigger(trigger);
    }

    private void TryFireGameLinkTrigger(PixelGameLinkTrigger trigger)
    {
        if (_stateMachine?.CanFire(trigger) != true)
            return;

        _stateMachine.Fire(trigger);
    }

    private IPixelCommandBus RequireCommandBus()
    {
        return _commandBus;
    }

    private void SubscribeRuntimeEvents(IPixelEventBus eventBus)
    {
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeRefreshRequestedEvent>()
            .Subscribe(@event => MarkRuntimeRefresh(@event.Rebuild)));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimePageRequestedEvent>()
            .Subscribe(@event =>
            {
                Subpage = @event.Page == PixelGameLinkRuntimePage.Finish
                    ? PixelGameLinkSubpage.Finish
                    : PixelGameLinkSubpage.Select;
                NotifyGameLinkSnapshotsChanged();
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeClientPingEvent>()
            .Subscribe(@event =>
            {
                _lastRuntimeClientPing = @event.Latency;
                MarkRuntimeRefresh(false);
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeEasyTierStateChangedEvent>()
            .Subscribe(_ =>
            {
                OnPropertyChanged(nameof(EasyTierSnapshot));
                OnPropertyChanged(nameof(SidebarSnapshot));
                MarkRuntimeRefresh(true);
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeEasyTierInstallRequestedEvent>()
            .Subscribe(_ =>
            {
                IsEasyTierAutoInstallStarted = true;
                MarkRuntimeRefresh(true);
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<GameLinkAnnouncementsLoadedEvent>()
                .Subscribe(@event =>
                {
                    SetLobbyAvailability(@event.IsLobbyAvailable);
                    SetAnnouncementSnapshots(@event.Announcements);
                }));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeUserStoppedGameEvent>()
            .Subscribe(_ =>
            {
                _lastRuntimeMessage = "大厅已解散";
                SetRuntimeNotification(new PixelGameLinkRuntimeNotification(
                    PixelGameLinkRuntimeNotificationKind.Message,
                    "大厅已解散",
                    "由于你关闭了联机中的 Minecraft 实例，大厅已自动解散。"));
                OnPropertyChanged(nameof(RuntimeSnapshot));
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<PixelGameLinkRuntimeServerExceptionEvent>()
            .Subscribe(@event =>
            {
                _lastRuntimeMessage = @event.Message;
                SetRuntimeNotification(new PixelGameLinkRuntimeNotification(
                    PixelGameLinkRuntimeNotificationKind.CriticalHint,
                    string.Empty,
                    @event.Message));
                OnPropertyChanged(nameof(RuntimeSnapshot));
            }));
        _runtimeSubscriptions.Add(eventBus.Observe<NatayarkLoginCompletedEvent>()
            .Subscribe(OnNatayarkLoginCompleted));
        _runtimeSubscriptions.Add(eventBus.Observe<NatayarkLoggedOutEvent>()
            .Subscribe(_ =>
            {
                TryFireGameLinkTrigger(PixelGameLinkTrigger.Reset);
                NotifyAccountSnapshotChanged();
            }));
    }

    private void MarkRuntimeRefresh(bool rebuild)
    {
        _hasRuntimeRefreshRequest = true;
        _lastRuntimeRefreshRebuild = rebuild;
        NotifyGameLinkSnapshotsChanged();
        OnPropertyChanged(nameof(RuntimeSnapshot));
    }

    public void ClearRuntimeRefreshRequest()
    {
        _hasRuntimeRefreshRequest = false;
        OnPropertyChanged(nameof(RuntimeSnapshot));
    }

    public PixelGameLinkRuntimeNotification? ConsumeRuntimeNotification()
    {
        var notification = _runtimeNotification;
        if (notification is null)
            return null;

        _runtimeNotification = null;
        OnPropertyChanged(nameof(RuntimeNotification));
        return notification;
    }

    public PixelGameLinkRuntimeRefreshPresentation ConsumeRuntimeRefreshPresentation(bool rebuildRequested)
    {
        var runtime = RuntimeSnapshot;
        var notification = ConsumeRuntimeNotification();
        ClearRuntimeRefreshRequest();
        return new PixelGameLinkRuntimeRefreshPresentation(
            rebuildRequested || runtime.Rebuild,
            notification);
    }

    private void SetRuntimeNotification(PixelGameLinkRuntimeNotification notification)
    {
        _runtimeNotification = notification;
        OnPropertyChanged(nameof(RuntimeNotification));
    }

    private void NotifyAccountSnapshotChanged()
    {
        OnPropertyChanged(nameof(AccountSnapshot));
        OnPropertyChanged(nameof(IsNatayarkLoggedIn));
        OnPropertyChanged(nameof(SidebarSnapshot));
        OnPropertyChanged(nameof(RuntimeSnapshot));
    }

    private void OnNatayarkLoginCompleted(NatayarkLoginCompletedEvent @event)
    {
        BeginGameLinkState(PixelGameLinkTrigger.StartLogin);
        TryFireGameLinkTrigger(@event.IsSuccess
            ? PixelGameLinkTrigger.LoginSucceeded
            : PixelGameLinkTrigger.LoginFailed);
        NotifyAccountSnapshotChanged();
    }

    private void NotifyGameLinkSnapshotsChanged()
    {
        OnPropertyChanged(nameof(EasyTierSnapshot));
        OnPropertyChanged(nameof(DiscoveredWorldsSnapshot));
        OnPropertyChanged(nameof(FinishSnapshot));
        OnPropertyChanged(nameof(CurrentLobbyCode));
        OnPropertyChanged(nameof(SidebarSnapshot));
    }

    public void Dispose()
    {
        foreach (var subscription in _runtimeSubscriptions)
            subscription.Dispose();
        _runtimeSubscriptions.Clear();
    }

    private static string GetEasyTierSidebarText()
    {
        return EasyTierDependencyService.State switch
        {
            EasyTierDependencyState.Installed => "依赖已就绪",
            EasyTierDependencyState.Installing => "正在自动安装",
            EasyTierDependencyState.Failed => "安装失败，可重试",
            EasyTierDependencyState.Unsupported => "当前平台不支持",
            _ => "等待自动安装"
        };
    }

    private static PixelGameLinkPlayerSnapshot ToPlayerSnapshot(PlayerProfile player)
    {
        var isHost = player.Kind == PlayerKind.HOST;
        return new PixelGameLinkPlayerSnapshot(
            player.Name,
            player.Vendor,
            isHost ? "[主机] " + player.Vendor : player.Vendor,
            isHost ? "mdi-crown-outline" : "mdi-account-outline",
            $"玩家 {player.Name} 的详细信息",
            $"用户名：{player.Name}\n联机协议客户端标识：{player.Vendor}\n此处数据仅供参考，请以实际游玩体验为准。");
    }
}

public sealed record PixelGameLinkAccountSnapshot(
    string Text,
    bool IsLoggedIn,
    string NatTestButtonText,
    string NatTestBusyText);

public sealed record PixelGameLinkLinkSnapshot(string Text, string Url, string? Info = null);

public sealed record PixelGameLinkEulaSnapshot(
    string Title,
    string IntroText,
    IReadOnlyList<PixelGameLinkLinkSnapshot> Links,
    string CommitmentText,
    string AcceptButtonText);

public sealed record PixelGameLinkConfirmationSnapshot(
    string Title,
    string Body,
    string PrimaryButtonText,
    string SecondaryButtonText);

public sealed record PixelGameLinkFooterSnapshot(
    string Title,
    IReadOnlyList<PixelGameLinkLinkSnapshot> Links,
    string FriendPrefix,
    IReadOnlyList<PixelGameLinkLinkSnapshot> FriendLinks,
    string DisableButtonText,
    PixelGameLinkConfirmationSnapshot DisableConfirmation);

public sealed record PixelGameLinkSetupPageSnapshot(
    string RestartRequiredHint,
    string NetworkTestTitle);

public enum PixelGameLinkAnnouncementSeverity
{
    Notice,
    Warning,
    Important
}

public sealed record PixelGameLinkAnnouncementSnapshot(PixelGameLinkAnnouncementSeverity Severity, string Text);

public sealed record PixelGameLinkNatDialogSnapshot(string Title, string Body);

public sealed record PixelGameLinkVirtualIpDialogSnapshot(
    string Title,
    string Body,
    string CopyButtonText,
    string BackButtonText);

public sealed record PixelGameLinkManualPortDialogSnapshot(
    string Title,
    string InputHint,
    string CreateButtonText,
    string CancelButtonText,
    string ValidationErrorText);

public sealed record PixelGameLinkNatStatusSnapshot(string UdpText, string TcpText, string Ipv6Text);

public sealed record PixelGameLinkNetworkTestMessages(
    string StartButtonText,
    string TestingButtonText,
    string CompletedMessage,
    string FailurePrefix)
{
    public string GetFailureMessage(Exception exception) =>
        FailurePrefix + exception.Message;
}

public sealed record PixelGameLinkEasyTierSnapshot(
    string Title,
    EasyTierDependencyState State,
    string StateText,
    string Details,
    bool IsPlatformSupported,
    bool CanInstall,
    string InstallText,
    string CheckButtonText,
    string CheckMessage,
    bool IsReady);

public sealed record PixelGameLinkWorldSnapshot(string Name, int Port);

public sealed record PixelGameLinkCreateCardSnapshot(
    string Title,
    IReadOnlyList<string> Steps,
    IReadOnlyList<PixelGameLinkWorldSnapshot> Worlds,
    bool CanCreate,
    int DefaultWorldIndex,
    string ManualButtonText,
    string RefreshButtonText,
    string CreateButtonText,
    PixelGameLinkManualPortDialogSnapshot ManualPortDialog);

public sealed record PixelGameLinkJoinCardSnapshot(
    string Title,
    string InputHint,
    IReadOnlyList<string> Steps,
    string ClearButtonText,
    string PasteButtonText,
    string JoinButtonText);

public sealed record PixelGameLinkFinishSnapshot(
    bool IsHost,
    string LobbyCode,
    string UserName,
    string LobbyStateText,
    string? VirtualIp,
    IReadOnlyCollection<PixelGameLinkPlayerSnapshot> Players,
    bool HasPlayers,
    IReadOnlyList<string> InfoLines,
    string InfoTitle,
    string ActionsTitle,
    string EmptyPlayersText,
    string MembersTitle,
    string CopyCodeButtonText,
    string CopyVirtualIpButtonText,
    string ExitButtonText,
    bool CanCopyVirtualIp,
    string LobbyCodeCopiedMessage,
    PixelGameLinkVirtualIpDialogSnapshot? VirtualIpDialog);

public sealed record PixelGameLinkPlayerSnapshot(
    string Name,
    string Vendor,
    string Info,
    string Icon,
    string DetailTitle,
    string DetailBody);

public sealed record PixelGameLinkSidebarSnapshot(
    string EulaInfo,
    string CurrentLobbyInfo,
    bool IsLobbyConnected,
    string EasyTierInfo)
{
    public string Title => "联机大厅";

    public string AnnouncementTitle => "大厅公告";

    public string AnnouncementInfo => "服务状态与通知";

    public string AnnouncementIcon => "mdi-bullhorn-outline";

    public string EulaTitle => "使用协议";

    public string EulaIcon => "mdi-file-document-outline";

    public string SelectTitle => "加入 / 创建";

    public string SelectInfo => "选择大厅或开放 LAN";

    public string SelectIcon => "mdi-lan-connect";

    public string CurrentLobbyTitle => "当前大厅";

    public string CurrentLobbyIcon => "mdi-account-multiple-outline";

    public string EasyTierTitle => "EasyTier";

    public string EasyTierIcon => "mdi-download-network-outline";

    public string TestEntryTitle => "控件验收";

    public string TestEntryInfo => "迁移组件预览";

    public string TestEntryIcon => "mdi-tools";

    public string TestEntryCardTitle => "测试入口";
}

public sealed record PixelGameLinkRuntimeSnapshot(
    bool HasRefreshRequest,
    bool Rebuild,
    long? LastClientPing,
    string? Message);

public sealed record PixelGameLinkRuntimeRefreshPresentation(
    bool Rebuild,
    PixelGameLinkRuntimeNotification? Notification);

public enum PixelGameLinkRuntimeNotificationKind
{
    Message,
    CriticalHint
}

public sealed record PixelGameLinkRuntimeNotification(
    PixelGameLinkRuntimeNotificationKind Kind,
    string Title,
    string Message);
