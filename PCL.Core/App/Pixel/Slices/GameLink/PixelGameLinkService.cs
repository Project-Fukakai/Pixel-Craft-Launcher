using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Pixel.Events;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;

namespace PCL.Core.App.Pixel.Slices.GameLink;

public sealed class PixelGameLinkService(HttpClient? httpClient = null, IPixelEventBus? eventBus = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    internal async Task<IReadOnlyList<LinkAnnounceInfo>> LoadAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var servers = Secrets.LinkServers.Where(server => !string.IsNullOrWhiteSpace(server)).ToArray();
            if (servers.Length == 0)
                throw new InvalidOperationException("未配置联机服务根地址。");

            JsonNode? root = null;
            foreach (var server in servers)
            {
                try
                {
                    var normalized = server.TrimEnd('/');
                    var cacheText = await _httpClient.GetStringAsync($"{normalized}/api/link/v2/cache.ini", cancellationToken).ConfigureAwait(false);
                    var cacheVer = int.Parse(cacheText.Trim());
                    string json;
                    if (cacheVer == States.Link.AnnounceCacheVer && !string.IsNullOrWhiteSpace(States.Link.AnnounceCache))
                    {
                        json = States.Link.AnnounceCache;
                    }
                    else
                    {
                        json = await _httpClient.GetStringAsync($"{normalized}/api/link/v2/announce.json", cancellationToken).ConfigureAwait(false);
                        States.Link.AnnounceCache = json;
                        States.Link.AnnounceCacheVer = cacheVer;
                    }

                    root = JsonNode.Parse(json);
                    break;
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Link", $"获取大厅公告失败: {server}");
                }
            }

            if (root is null)
                throw new InvalidOperationException("无法连接大厅服务器。");

            ApplyLobbyMetadata(root);
            ApplyRelays(root);
            return ParseAnnouncements(root);
        }
        catch (Exception ex)
        {
            LobbyInfoProvider.IsLobbyAvailable = false;
            LogWrapper.Error(ex, "Link", "获取大厅公告失败。");
            return [new LinkAnnounceInfo(LinkAnnounceType.Important, "连接大厅服务器失败。")];
        }
    }

    public async Task<bool> EnsureEasyTierInstalledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await EasyTierDependencyService.EnsureInstalledAsync().ConfigureAwait(false);
    }

    public async Task<PixelGameLinkPrecheckResult> RunPrecheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pureResult = await ValidatePrecheckBeforeInstallAsync(cancellationToken).ConfigureAwait(false);
        if (!pureResult.IsSuccess)
            return pureResult;

        if (!await EnsureEasyTierInstalledAsync(cancellationToken).ConfigureAwait(false))
        {
            return PixelGameLinkPrecheckResult.Failed(
                PixelGameLinkPrecheckFailureKind.EasyTierUnavailable,
                EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。");
        }

        return PixelGameLinkPrecheckResult.Success(LobbyInfoProvider.GetUsername() ?? Config.Link.Username);
    }

    public async Task<PixelGameLinkOperationResult> CreateLobbyAsync(int port, CancellationToken cancellationToken = default)
    {
        var precheck = await RunPrecheckAsync(cancellationToken).ConfigureAwait(false);
        if (!precheck.IsSuccess)
            return PixelGameLinkOperationResult.PrecheckFailed(precheck);

        var ok = await LobbyService.CreateLobbyAsync(port, precheck.Username).ConfigureAwait(false);
        return ok
            ? PixelGameLinkOperationResult.Success("大厅创建成功。")
            : PixelGameLinkOperationResult.Failed("创建大厅失败，请检查日志或稍后重试。");
    }

    public async Task<PixelGameLinkOperationResult> JoinLobbyAsync(string lobbyCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            return PixelGameLinkOperationResult.PrecheckFailed(
                PixelGameLinkPrecheckResult.Failed(PixelGameLinkPrecheckFailureKind.MissingLobbyCode, "请输入大厅编号。"));
        }

        var precheck = await RunPrecheckAsync(cancellationToken).ConfigureAwait(false);
        if (!precheck.IsSuccess)
            return PixelGameLinkOperationResult.PrecheckFailed(precheck);

        var ok = await LobbyService.JoinLobbyAsync(lobbyCode, precheck.Username).ConfigureAwait(false);
        return ok
            ? PixelGameLinkOperationResult.Success("已加入大厅。")
            : PixelGameLinkOperationResult.Failed("加入大厅失败，请检查大厅编号或稍后重试。");
    }

    public async Task LeaveLobbyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await LobbyService.LeaveLobbyAsync().ConfigureAwait(false);
    }

    public async Task DiscoverWorldsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await LobbyService.DiscoverWorldAsync().ConfigureAwait(false);
    }

    public bool IsLobbyAvailable => LobbyInfoProvider.IsLobbyAvailable;

    public async Task<bool> LoginNatayarkAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await NatayarkOAuthService.StartLoginAsync(cancellationToken).ConfigureAwait(false);
    }

    public void LogoutNatayark() => NatayarkOAuthService.Logout();

    public async Task InitializeLobbyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await LobbyService.InitializeAsync().ConfigureAwait(false);
    }

    public IDisposable SubscribeRuntimeEvents(PixelGameLinkRuntimeCallbacks callbacks)
    {
        var bridge = new PixelGameLinkRuntimeEventBridge(callbacks, eventBus);
        void NeedDownloadEasyTier() => bridge.RequestEasyTierInstall();
        void EasyTierStateChanged(EasyTierDependencyState state, string? error) => bridge.RefreshForEasyTierState(state, error);
        void WorldsChanged(object? sender, NotifyCollectionChangedEventArgs args) => bridge.RequestRefresh(false);
        void PlayersChanged(object? sender, NotifyCollectionChangedEventArgs args) => bridge.RequestRefresh(false);
        void ClientPing(long latency) => bridge.ReportClientPing(latency);
        void ServerStarted() => bridge.ShowFinish();
        void ServerShutDown() => bridge.ShowSelect();
        void UserStopGame() => bridge.NotifyUserStopGame();
        void ServerException(Exception ex) => bridge.NotifyServerException(ex);

        LobbyService.OnNeedDownloadEasyTier += NeedDownloadEasyTier;
        EasyTierDependencyService.StateChanged += EasyTierStateChanged;
        LobbyService.DiscoveredWorlds.CollectionChanged += WorldsChanged;
        LobbyService.Players.CollectionChanged += PlayersChanged;
        LobbyService.OnClientPing += ClientPing;
        LobbyService.OnServerStarted += ServerStarted;
        LobbyService.OnServerShutDown += ServerShutDown;
        LobbyService.OnUserStopGame += UserStopGame;
        LobbyService.OnServerException += ServerException;

        return Disposable.Create(() =>
        {
            LobbyService.OnNeedDownloadEasyTier -= NeedDownloadEasyTier;
            EasyTierDependencyService.StateChanged -= EasyTierStateChanged;
            LobbyService.DiscoveredWorlds.CollectionChanged -= WorldsChanged;
            LobbyService.Players.CollectionChanged -= PlayersChanged;
            LobbyService.OnClientPing -= ClientPing;
            LobbyService.OnServerStarted -= ServerStarted;
            LobbyService.OnServerShutDown -= ServerShutDown;
            LobbyService.OnUserStopGame -= UserStopGame;
            LobbyService.OnServerException -= ServerException;
        });
    }

    public async Task<PixelGameLinkNatTestResult> RunNatTestAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureEasyTierInstalledAsync(cancellationToken).ConfigureAwait(false))
            return PixelGameLinkNatTestResult.Failed(EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。");

        var status = await CliNetTest.GetNetStatusAsync().ConfigureAwait(false);
        if (status is null)
            return PixelGameLinkNatTestResult.Failed("NAT 测试失败。");

        return PixelGameLinkNatTestResult.Success(
            CliNetTest.GetNatTypeString(status.UdpNatType),
            CliNetTest.GetNatTypeString(status.TcpNatType),
            status.SupportIPv6);
    }

    private static async Task<PixelGameLinkPrecheckResult> ValidatePrecheckBeforeInstallAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!States.Link.LinkEula)
            return PixelGameLinkPrecheckResult.Failed(PixelGameLinkPrecheckFailureKind.EulaRequired);

        if (!LobbyInfoProvider.IsLobbyAvailable)
        {
            return PixelGameLinkPrecheckResult.Failed(
                PixelGameLinkPrecheckFailureKind.LobbyUnavailable,
                "大厅功能暂不可用，请稍后再试。");
        }

        if (LobbyInfoProvider.RequiresLogin)
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                return PixelGameLinkPrecheckResult.Failed(
                    PixelGameLinkPrecheckFailureKind.LoginRequired,
                    "请先登录 Natayark Network 再进行联机。");
            }

            try
            {
                await NatayarkProfileManager.GetNaidDataAsync(States.Link.NaidRefreshToken, true).ConfigureAwait(false);
            }
            catch
            {
                return PixelGameLinkPrecheckResult.Failed(
                    PixelGameLinkPrecheckFailureKind.LoginExpired,
                    "请重新登录 Natayark Network 账号再试。");
            }

            if (LobbyInfoProvider.RequiresRealName && !NatayarkProfileManager.NaidProfile.IsRealNamed)
            {
                return PixelGameLinkPrecheckResult.Failed(
                    PixelGameLinkPrecheckFailureKind.RealNameRequired,
                    "请先前往 Natayark 账户中心进行实名验证再尝试操作。");
            }

            if (NatayarkProfileManager.NaidProfile.Status != 0)
            {
                return PixelGameLinkPrecheckResult.Failed(
                    PixelGameLinkPrecheckFailureKind.AccountBlocked,
                    "你的 Natayark Network 账号状态异常，可能已被封禁。");
            }
        }

        var username = LobbyInfoProvider.GetUsername() ?? Config.Link.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            return PixelGameLinkPrecheckResult.Failed(
                PixelGameLinkPrecheckFailureKind.MissingUsername,
                "请先在设置中输入大厅用户名，或登录 Natayark Network。");
        }

        return PixelGameLinkPrecheckResult.Success(username);
    }

    private static void ApplyLobbyMetadata(JsonNode root)
    {
        LobbyInfoProvider.IsLobbyAvailable = root["available"]?.GetValue<bool>() ?? false;
        LobbyInfoProvider.AllowCustomName = root["allowCustomName"]?.GetValue<bool>() ?? false;
        LobbyInfoProvider.RequiresLogin = root["requireLogin"]?.GetValue<bool>() ?? true;
        LobbyInfoProvider.RequiresRealName = root["requireRealname"]?.GetValue<bool>() ?? true;
    }

    private static IReadOnlyList<LinkAnnounceInfo> ParseAnnouncements(JsonNode root)
    {
        var announces = new List<LinkAnnounceInfo>();
        if (root["notices"] is not JsonArray notices)
            return announces;

        foreach (var node in notices.OfType<JsonObject>())
        {
            var content = node["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var minVer = node["minVer"]?.GetValue<double>() ?? double.MinValue;
            var maxVer = node["maxVer"]?.GetValue<double>() ?? double.MaxValue;
            if (Basics.VersionCode < minVer || Basics.VersionCode > maxVer)
                continue;

            var typeText = node["type"]?.ToString().ToLowerInvariant();
            var type = typeText is "important" or "red"
                ? LinkAnnounceType.Important
                : typeText is "warning" or "yellow"
                    ? LinkAnnounceType.Warning
                    : LinkAnnounceType.Notice;

            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                announces.Add(new LinkAnnounceInfo(type, line.Trim()));
        }

        return announces;
    }

    private static void ApplyRelays(JsonNode root)
    {
        ETRelay.RelayList = [];
        if (root["relays"] is not JsonArray relays)
            return;

        foreach (var relay in relays.OfType<JsonObject>())
        {
            var url = relay["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                continue;
            ETRelay.RelayList.Add(new ETRelay
            {
                Name = relay["name"]?.ToString() ?? url,
                Url = url,
                Type = relay["type"]?.ToString() == "official" ? ETRelayType.Selfhosted : ETRelayType.Community
            });
        }
    }
}

public enum PixelGameLinkPrecheckFailureKind
{
    None,
    EulaRequired,
    LobbyUnavailable,
    LoginRequired,
    LoginExpired,
    RealNameRequired,
    AccountBlocked,
    MissingUsername,
    EasyTierUnavailable,
    MissingLobbyCode
}

public sealed record PixelGameLinkPrecheckResult(
    bool IsSuccess,
    PixelGameLinkPrecheckFailureKind FailureKind,
    string Message,
    string Username)
{
    public static PixelGameLinkPrecheckResult Success(string? username) =>
        new(true, PixelGameLinkPrecheckFailureKind.None, string.Empty, username ?? string.Empty);

    public static PixelGameLinkPrecheckResult Failed(
        PixelGameLinkPrecheckFailureKind failureKind,
        string? message = null) =>
        new(false, failureKind, message ?? string.Empty, string.Empty);
}

public sealed record PixelGameLinkOperationResult(
    bool IsSuccess,
    string Message,
    PixelGameLinkPrecheckResult? Precheck)
{
    public static PixelGameLinkOperationResult Success(string message) => new(true, message, null);

    public static PixelGameLinkOperationResult Failed(string message) => new(false, message, null);

    public static PixelGameLinkOperationResult PrecheckFailed(PixelGameLinkPrecheckResult precheck) =>
        new(false, precheck.Message, precheck);
}

public sealed record PixelGameLinkNatTestResult(
    bool IsSuccess,
    string Message,
    string UdpNatType,
    string TcpNatType,
    bool SupportsIPv6)
{
    public static PixelGameLinkNatTestResult Success(string udpNatType, string tcpNatType, bool supportsIPv6) =>
        new(true, string.Empty, udpNatType, tcpNatType, supportsIPv6);

    public static PixelGameLinkNatTestResult Failed(string message) =>
        new(false, message, string.Empty, string.Empty, false);
}

public sealed record PixelGameLinkRuntimeCallbacks(
    Action<bool> Refresh,
    Action InstallEasyTier,
    Action ShowSelect,
    Action ShowFinish,
    Action NotifyUserStopGame,
    Action<Exception> NotifyServerException);

public enum PixelGameLinkRuntimePage
{
    Select,
    Finish
}

public sealed record PixelGameLinkRuntimeRefreshRequestedEvent(bool Rebuild);

public sealed record PixelGameLinkRuntimeEasyTierInstallRequestedEvent;

public sealed record PixelGameLinkRuntimeEasyTierStateChangedEvent(EasyTierDependencyState State, string? Error);

public sealed record PixelGameLinkRuntimeClientPingEvent(long Latency);

public sealed record PixelGameLinkRuntimePageRequestedEvent(PixelGameLinkRuntimePage Page);

public sealed record PixelGameLinkRuntimeUserStoppedGameEvent;

public sealed record PixelGameLinkRuntimeServerExceptionEvent(string Message);

public sealed class PixelGameLinkRuntimeEventBridge(
    PixelGameLinkRuntimeCallbacks callbacks,
    IPixelEventBus? eventBus = null)
{
    public void RequestRefresh(bool rebuild)
    {
        eventBus?.Publish(new PixelGameLinkRuntimeRefreshRequestedEvent(rebuild));
        callbacks.Refresh(rebuild);
    }

    public void RequestEasyTierInstall()
    {
        eventBus?.Publish(new PixelGameLinkRuntimeEasyTierInstallRequestedEvent());
        callbacks.InstallEasyTier();
    }

    public void RefreshForEasyTierState(EasyTierDependencyState state, string? error)
    {
        eventBus?.Publish(new PixelGameLinkRuntimeEasyTierStateChangedEvent(state, error));
        RequestRefresh(true);
    }

    public void ReportClientPing(long latency)
    {
        eventBus?.Publish(new PixelGameLinkRuntimeClientPingEvent(latency));
        RequestRefresh(false);
    }

    public void ShowFinish()
    {
        eventBus?.Publish(new PixelGameLinkRuntimePageRequestedEvent(PixelGameLinkRuntimePage.Finish));
        callbacks.ShowFinish();
        RequestRefresh(true);
    }

    public void ShowSelect()
    {
        eventBus?.Publish(new PixelGameLinkRuntimePageRequestedEvent(PixelGameLinkRuntimePage.Select));
        callbacks.ShowSelect();
        RequestRefresh(true);
    }

    public void NotifyUserStopGame()
    {
        eventBus?.Publish(new PixelGameLinkRuntimeUserStoppedGameEvent());
        ShowSelect();
        callbacks.NotifyUserStopGame();
    }

    public void NotifyServerException(Exception exception)
    {
        eventBus?.Publish(new PixelGameLinkRuntimeServerExceptionEvent(exception.Message));
        ShowSelect();
        callbacks.NotifyServerException(exception);
    }
}
