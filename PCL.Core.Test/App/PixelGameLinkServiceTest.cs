using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelGameLinkServiceTest
{
    private bool _linkEula;
    private bool _isLobbyAvailable;
    private bool _allowCustomName;
    private bool _requiresLogin;
    private bool _requiresRealName;
    private string _linkUsername = string.Empty;

    [TestInitialize]
    public void SnapshotState()
    {
        ConfigService.EnsureInitializedForTesting();
        _linkEula = States.Link.LinkEula;
        _isLobbyAvailable = LobbyInfoProvider.IsLobbyAvailable;
        _allowCustomName = LobbyInfoProvider.AllowCustomName;
        _requiresLogin = LobbyInfoProvider.RequiresLogin;
        _requiresRealName = LobbyInfoProvider.RequiresRealName;
        _linkUsername = Config.Link.Username;
    }

    [TestCleanup]
    public void RestoreState()
    {
        States.Link.LinkEula = _linkEula;
        LobbyInfoProvider.IsLobbyAvailable = _isLobbyAvailable;
        LobbyInfoProvider.AllowCustomName = _allowCustomName;
        LobbyInfoProvider.RequiresLogin = _requiresLogin;
        LobbyInfoProvider.RequiresRealName = _requiresRealName;
        Config.Link.Username = _linkUsername;
    }

    [TestMethod]
    public async Task RunPrecheckRequiresEulaBeforeLobbyChecks()
    {
        States.Link.LinkEula = false;
        LobbyInfoProvider.IsLobbyAvailable = false;

        var result = await new PixelGameLinkService().RunPrecheckAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkPrecheckFailureKind.EulaRequired, result.FailureKind);
    }

    [TestMethod]
    public async Task RunPrecheckReportsUnavailableLobby()
    {
        States.Link.LinkEula = true;
        LobbyInfoProvider.IsLobbyAvailable = false;

        var result = await new PixelGameLinkService().RunPrecheckAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkPrecheckFailureKind.LobbyUnavailable, result.FailureKind);
        Assert.AreEqual("大厅功能暂不可用，请稍后再试。", result.Message);
    }

    [TestMethod]
    public async Task RunPrecheckRequiresUsernameBeforeInstallingEasyTier()
    {
        States.Link.LinkEula = true;
        LobbyInfoProvider.IsLobbyAvailable = true;
        LobbyInfoProvider.AllowCustomName = true;
        LobbyInfoProvider.RequiresLogin = false;
        Config.Link.Username = string.Empty;

        var result = await new PixelGameLinkService().RunPrecheckAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelGameLinkPrecheckFailureKind.MissingUsername, result.FailureKind);
        Assert.AreEqual("请先在设置中输入大厅用户名，或登录 Natayark Network。", result.Message);
    }

    [TestMethod]
    public async Task JoinLobbyRequiresCodeBeforePrecheck()
    {
        var result = await new PixelGameLinkService().JoinLobbyAsync(" ");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Precheck);
        Assert.AreEqual(PixelGameLinkPrecheckFailureKind.MissingLobbyCode, result.Precheck.FailureKind);
    }

    [TestMethod]
    public void RuntimeBridgePublishesEventsAndKeepsCallbacks()
    {
        using var eventBus = new ReactivePixelEventBus();
        var refreshEvents = new List<PixelGameLinkRuntimeRefreshRequestedEvent>();
        var installEvents = new List<PixelGameLinkRuntimeEasyTierInstallRequestedEvent>();
        var easyTierEvents = new List<PixelGameLinkRuntimeEasyTierStateChangedEvent>();
        var pingEvents = new List<PixelGameLinkRuntimeClientPingEvent>();
        var pageEvents = new List<PixelGameLinkRuntimePageRequestedEvent>();
        var stopEvents = new List<PixelGameLinkRuntimeUserStoppedGameEvent>();
        var exceptionEvents = new List<PixelGameLinkRuntimeServerExceptionEvent>();
        using var refreshSubscription = eventBus.Observe<PixelGameLinkRuntimeRefreshRequestedEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeRefreshRequestedEvent>(refreshEvents));
        using var installSubscription = eventBus.Observe<PixelGameLinkRuntimeEasyTierInstallRequestedEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeEasyTierInstallRequestedEvent>(installEvents));
        using var easyTierSubscription = eventBus.Observe<PixelGameLinkRuntimeEasyTierStateChangedEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeEasyTierStateChangedEvent>(easyTierEvents));
        using var pingSubscription = eventBus.Observe<PixelGameLinkRuntimeClientPingEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeClientPingEvent>(pingEvents));
        using var pageSubscription = eventBus.Observe<PixelGameLinkRuntimePageRequestedEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimePageRequestedEvent>(pageEvents));
        using var stopSubscription = eventBus.Observe<PixelGameLinkRuntimeUserStoppedGameEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeUserStoppedGameEvent>(stopEvents));
        using var exceptionSubscription = eventBus.Observe<PixelGameLinkRuntimeServerExceptionEvent>().Subscribe(new ListObserver<PixelGameLinkRuntimeServerExceptionEvent>(exceptionEvents));
        var refreshCount = 0;
        var installCount = 0;
        var selectCount = 0;
        var finishCount = 0;
        var stopCount = 0;
        var exceptionCount = 0;
        var bridge = new PixelGameLinkRuntimeEventBridge(
            new PixelGameLinkRuntimeCallbacks(
                _ => refreshCount++,
                () => installCount++,
                () => selectCount++,
                () => finishCount++,
                () => stopCount++,
                _ => exceptionCount++),
            eventBus);

        bridge.RequestEasyTierInstall();
        bridge.RefreshForEasyTierState(EasyTierDependencyState.Failed, "missing");
        bridge.ReportClientPing(42);
        bridge.ShowFinish();
        bridge.NotifyUserStopGame();
        bridge.NotifyServerException(new InvalidOperationException("boom"));

        Assert.AreEqual(5, refreshCount);
        Assert.AreEqual(1, installCount);
        Assert.AreEqual(2, selectCount);
        Assert.AreEqual(1, finishCount);
        Assert.AreEqual(1, stopCount);
        Assert.AreEqual(1, exceptionCount);
        Assert.AreEqual(5, refreshEvents.Count);
        Assert.AreEqual(1, installEvents.Count);
        Assert.AreEqual(EasyTierDependencyState.Failed, easyTierEvents[0].State);
        Assert.AreEqual(42, pingEvents[0].Latency);
        CollectionAssert.AreEqual(
            new[]
            {
                PixelGameLinkRuntimePage.Finish,
                PixelGameLinkRuntimePage.Select,
                PixelGameLinkRuntimePage.Select
            },
            pageEvents.ConvertAll(static @event => @event.Page));
        Assert.AreEqual(1, stopEvents.Count);
        Assert.AreEqual("boom", exceptionEvents[0].Message);
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
