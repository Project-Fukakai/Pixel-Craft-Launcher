using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Composition;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.Slices.Download;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.Slices.Java;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Link;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelInfrastructureTest
{
    [TestMethod]
    public void AddPixelApplicationRegistersCoreServices()
    {
        using var services = new ServiceCollection()
            .AddPixelApplication()
            .BuildServiceProvider(validateScopes: true);

        Assert.IsNotNull(services.GetRequiredService<IPixelCommandBus>());
        Assert.IsNotNull(services.GetRequiredService<IPixelEventBus>());
        Assert.IsNotNull(services.GetRequiredService<IPixelNavigationService>());
        Assert.IsNotNull(services.GetRequiredService<IPixelStateMachineFactory>());
        Assert.IsNotNull(services.GetRequiredService<PixelLoggingBootstrapService>());
        Assert.IsNotNull(services.GetRequiredService<PixelStartupRenderingService>());
        Assert.IsNotNull(services.GetRequiredService<PixelLoaderChoiceService>());
        Assert.IsNotNull(services.GetRequiredService<PixelLoaderSelectionService>());
        Assert.IsNotNull(services.GetRequiredService<PixelJavaService>());
        Assert.IsNotNull(services.GetRequiredService<PixelGameLinkService>());
        Assert.IsNotNull(services.GetRequiredService<PixelShellSettingsService>());
        Assert.IsNotNull(services.GetRequiredService<PixelSettingsObservationService>());
        Assert.IsNotNull(services.GetRequiredService<PixelSettingValueService>());
        Assert.IsNotNull(services.GetRequiredService<IPixelRendererHintState>());
        Assert.IsNotNull(services.GetRequiredService<PixelGameLinkViewModel>());
        Assert.IsNotNull(services.GetRequiredService<PixelLaunchStateMachine>());
        Assert.IsNotNull(services.GetRequiredService<PixelDownloadStateMachine>());
        Assert.IsNotNull(services.GetRequiredService<PixelGameLinkStateMachine>());
        Assert.IsNotNull(services.GetRequiredService<PixelProfileStateMachine>());
        Assert.IsNotNull(services.GetRequiredService<PixelDownloadCategoryRefreshService>());
        Assert.IsNotNull(services.GetRequiredService<IPixelOperationDelayService>());
        Assert.IsNotNull(services.GetRequiredService<PixelHost>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<ExportLaunchLogCommand, string>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<StartMinecraftLaunchCommand, MinecraftLaunchResult>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<KillMinecraftProcessCommand, bool>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<MonitorMinecraftProcessCommand, MinecraftProcessExitInfo>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RefreshMinecraftVersionsCommand, IReadOnlyList<MinecraftVersionManifestEntry>>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RefreshMinecraftLoaderChoicesCommand, IReadOnlyList<PixelLoaderChoiceGroup>>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<StartDownloadInstallCommand, PixelMinecraftInstanceInstalledSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<SaveMinecraftClientCoreCommand, string>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<SaveMinecraftServerJarCommand, string>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<CancelMinecraftDownloadTaskCommand, bool>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<CancelAllMinecraftDownloadsCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<SaveOfflineMinecraftProfileCommand, PixelProfileItemSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<SaveOfflineMinecraftProfileByIdCommand, PixelProfileItemSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<AddMicrosoftMinecraftProfileCommand, PixelProfileItemSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<AddAuthlibMinecraftProfileCommand, PixelProfileItemSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<AddAuthServerPresetCommand, PixelAuthServerSnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<SelectMinecraftProfileByIdCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RemoveMinecraftProfileByIdCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RefreshJavaEntriesCommand, IReadOnlyList<PixelJavaEntrySnapshot>>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<AddJavaEntryCommand, PixelJavaEntrySnapshot>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<LoadGameLinkAnnouncementsCommand, IReadOnlyList<PixelGameLinkAnnouncementSnapshot>>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<EnsureEasyTierInstalledCommand, bool>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RunGameLinkPrecheckCommand, PixelGameLinkPrecheckResult>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<CreateGameLinkLobbyCommand, PixelGameLinkOperationResult>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<JoinGameLinkLobbyCommand, PixelGameLinkOperationResult>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<LeaveGameLinkLobbyCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<DiscoverGameLinkWorldsCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<RunGameLinkNatTestCommand, PixelGameLinkNatTestResult>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<LoginNatayarkCommand, bool>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<LogoutNatayarkCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<AcceptGameLinkEulaCommand>>());
        Assert.IsNotNull(services.GetRequiredService<IRequestHandler<ResetGameLinkAuthorizationCommand>>());
    }

    [TestMethod]
    public void EventBusPublishesOnlyMatchingEventTypes()
    {
        using var eventBus = new ReactivePixelEventBus();
        var strings = new List<string>();
        var numbers = new List<int>();
        using var stringSubscription = eventBus.Observe<string>().Subscribe(new ListObserver<string>(strings));
        using var numberSubscription = eventBus.Observe<int>().Subscribe(new ListObserver<int>(numbers));

        eventBus.Publish("ready");
        eventBus.Publish(42);

        CollectionAssert.AreEqual(new[] { "ready" }, strings);
        CollectionAssert.AreEqual(new[] { 42 }, numbers);
    }

    [TestMethod]
    public void RouterPublishesRouteChangedEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelRouteChangedEvent>();
        using var subscription = eventBus.Observe<PixelRouteChangedEvent>().Subscribe(new ListObserver<PixelRouteChangedEvent>(events));
        var router = new PixelRouter(PixelRoutes.Launch(), eventBus);

        Assert.IsTrue(router.Navigate(PixelRoutes.DownloadMinecraft()));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("launch", events[0].OldRoute.Segment);
        Assert.AreEqual("download", events[0].NewRoute.Segment);
    }

    [TestMethod]
    public void StartupRenderingServiceDetectsDisabledHardwareAcceleration()
    {
        Assert.IsTrue(PixelStartupRenderingService.IsHardwareAccelerationDisabled([
            "Other: false",
            "SystemDisableHardwareAcceleration: true"
        ]));

        Assert.IsFalse(PixelStartupRenderingService.IsHardwareAccelerationDisabled([
            "SystemDisableHardwareAcceleration: false"
        ]));
    }

    [TestMethod]
    public void LoggingBootstrapServiceCreatesPixelLogDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "PCLTest", "PixelLogs", Guid.NewGuid().ToString("N"));
        try
        {
            var pattern = PixelLoggingBootstrapService.EnsurePixelLogFilePattern(root);

            Assert.AreEqual(Path.Combine(root, "Logs", "pixel-.log"), pattern);
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Logs")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MainWindowViewModelAppliesRoutesToShellState()
    {
        using var provider = new ServiceCollection()
            .AddPclCore()
            .BuildServiceProvider();
        var viewModel = provider.GetRequiredService<MainWindowViewModel>();

        Assert.AreEqual("启动", viewModel.LaunchNavigationText);
        Assert.AreEqual("下载", viewModel.DownloadNavigationText);
        Assert.AreEqual("设置", viewModel.SetupNavigationText);
        Assert.AreEqual("工具", viewModel.ToolsNavigationText);
        Assert.AreEqual("下载管理", viewModel.DownloadTasksFloatingTooltip);
        Assert.AreEqual("强制关闭 Minecraft", viewModel.ForceCloseGameFloatingTooltip);

        viewModel.NavigateMinecraftInstall("1.20.1");

        Assert.AreEqual(MainPageKind.Download, viewModel.SelectedMainPage);
        Assert.AreEqual(MainWindowViewModel.InstallLeftPaneWidth, viewModel.LeftPaneWidth);
        Assert.IsTrue(viewModel.IsSecondaryTitleVisible);
        Assert.AreEqual("Minecraft 安装", viewModel.TitleText);
        Assert.IsTrue(viewModel.IsCurrentDownloadInstallRoute());
        Assert.IsTrue(viewModel.IsCurrentDownloadSecondaryRoute());
        Assert.IsFalse(viewModel.IsCurrentDownloadTaskRoute());

        viewModel.NavigateDownloadTaskDetails("task-1");

        Assert.AreEqual(MainPageKind.Download, viewModel.SelectedMainPage);
        Assert.AreEqual(MainWindowViewModel.InstallLeftPaneWidth, viewModel.LeftPaneWidth);
        Assert.IsTrue(viewModel.IsSecondaryTitleVisible);
        Assert.AreEqual("下载管理", viewModel.TitleText);
        Assert.IsTrue(viewModel.IsCurrentDownloadTaskRoute());
        Assert.IsTrue(MainWindowViewModel.IsDownloadTaskRoute(PixelRoutes.DownloadTaskDetails("task-1")));
        Assert.IsTrue(MainWindowViewModel.IsDownloadSecondaryRoute(PixelRoutes.DownloadTaskDetails("task-1")));
        Assert.AreEqual("task-1", MainWindowViewModel.GetDownloadTaskRouteTaskId(PixelRoutes.DownloadTaskDetails("task-1")));
        Assert.AreEqual(
            "child-task",
            MainWindowViewModel.GetDownloadTaskRouteTaskId(new RouteNode(
                "download",
                Child: new RouteNode("tasks", new Dictionary<string, string> { ["task"] = "child-task" }))));
        var downloadTaskTitle = MainWindowViewModel.GetSecondaryTitleSnapshot(PixelRoutes.DownloadTaskDetails("task-1"), "安装标题");
        Assert.IsTrue(downloadTaskTitle.IsVisible);
        Assert.AreEqual("下载管理", downloadTaskTitle.Title);
        var downloadInstallTitle = MainWindowViewModel.GetSecondaryTitleSnapshot(PixelRoutes.DownloadMinecraftInstall("1.20.1"), "1.20.1 安装");
        Assert.IsTrue(downloadInstallTitle.IsVisible);
        Assert.AreEqual("1.20.1 安装", downloadInstallTitle.Title);

        viewModel.NavigateSetupSection(PixelSettingSectionKind.Java);

        Assert.AreEqual(MainPageKind.Setup, viewModel.SelectedMainPage);
        Assert.AreEqual(PixelSettingSectionKind.Java, viewModel.SelectedSetupSection);
        Assert.AreEqual(MainWindowViewModel.DefaultLeftPaneWidth, viewModel.LeftPaneWidth);
        Assert.IsFalse(viewModel.IsSecondaryTitleVisible);

        Assert.IsTrue(viewModel.Back());
        Assert.AreEqual(MainPageKind.Download, viewModel.SelectedMainPage);
        Assert.IsTrue(viewModel.IsDownloadSelected);
        Assert.IsTrue(viewModel.IsCurrentDownloadTaskRoute());

        viewModel.NavigateProfileManager();

        Assert.AreEqual(MainPageKind.Launch, viewModel.SelectedMainPage);
        Assert.IsTrue(viewModel.IsCurrentGlobalSecondaryRoute());
        Assert.IsTrue(viewModel.IsCurrentProfileManagerRoute());
        Assert.IsFalse(viewModel.IsCurrentDownloadTaskRoute());
        Assert.AreEqual("档案管理", viewModel.TitleText);
        Assert.AreEqual(MainWindowViewModel.InstallLeftPaneWidth, viewModel.LeftPaneWidth);
        Assert.AreEqual(
            PixelProfileManagerPageKind.Authlib,
            MainWindowViewModel.GetProfileManagerRouteSnapshot(PixelRoutes.ProfileManager("authlib", "server-1")).PageKind);
        Assert.AreEqual(
            "server-1",
            MainWindowViewModel.GetProfileManagerRouteSnapshot(PixelRoutes.ProfileManager("authlib", "server-1")).AuthServerId);
        Assert.AreEqual(
            PixelProfileManagerPageKind.List,
            MainWindowViewModel.GetProfileManagerRouteSnapshot(PixelRoutes.Launch()).PageKind);

        viewModel.NavigateLaunchInstances();

        Assert.AreEqual(MainPageKind.Launch, viewModel.SelectedMainPage);
        Assert.IsTrue(viewModel.IsCurrentLaunchInstanceRoute());
        Assert.IsFalse(viewModel.IsCurrentGlobalSecondaryRoute());
        Assert.AreEqual("实例选择", viewModel.TitleText);
        Assert.AreEqual(MainWindowViewModel.DefaultLeftPaneWidth, viewModel.LeftPaneWidth);
        Assert.IsTrue(MainWindowViewModel.IsLaunchInstanceRoute(PixelRoutes.LaunchInstances()));
        Assert.IsTrue(MainWindowViewModel.IsProfileManagerRoute(PixelRoutes.ProfileManager()));
        var launchInstancesTitle = MainWindowViewModel.GetSecondaryTitleSnapshot(PixelRoutes.LaunchInstances(), "安装标题");
        Assert.IsTrue(launchInstancesTitle.IsVisible);
        Assert.AreEqual("实例选择", launchInstancesTitle.Title);

        var placeholder = MainWindowViewModel.GetPlaceholderPageSnapshot(MainPageKind.Setup);

        Assert.AreEqual("设置", placeholder.Title);
        StringAssert.Contains(placeholder.Description, "启动器设置");
        Assert.AreEqual(2, placeholder.Sections.Count);
        Assert.AreEqual("页面承载区", placeholder.Sections[0].Title);
        var controlsPreview = MainWindowViewModel.GetControlsPreviewSnapshot();
        Assert.AreEqual("工具", controlsPreview.HeroTitle);
        Assert.AreEqual("按钮", controlsPreview.ButtonsCardTitle);
        Assert.AreEqual("搜索：", controlsPreview.SearchResultPrefix);
        Assert.AreEqual("打开 MyMsgMarkdown", controlsPreview.OpenMarkdownButtonText);
        var leftItems = MainWindowViewModel.GetPlaceholderLeftItemSnapshots(MainPageKind.Download);
        Assert.AreEqual(3, leftItems.Count);
        Assert.AreEqual("版本列表", leftItems[0].Title);
        Assert.AreEqual("mdi-cube-outline", leftItems[0].Icon);
        Assert.IsTrue(leftItems[0].IsActive);
        Assert.IsTrue(MainWindowViewModel.GetMainPageRoute(MainPageKind.Download, PixelSettingSectionKind.Java).StartsWith("download"));
        Assert.AreEqual("java", MainWindowViewModel.GetMainPageRoute(MainPageKind.Setup, PixelSettingSectionKind.Java).Child?.Segment);
        var mainTitle = MainWindowViewModel.GetSecondaryTitleSnapshot(PixelRoutes.Launch(), "安装标题");
        Assert.IsFalse(mainTitle.IsVisible);
        Assert.AreEqual(string.Empty, mainTitle.Title);

        Assert.IsTrue(viewModel.NavigateMainPage(MainPageKind.Tools));
        Assert.AreEqual(MainPageKind.Tools, viewModel.SelectedMainPage);
    }

    [TestMethod]
    public void StateMachinePublishesTransitionEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var machine = factory.Create<TestState, TestTrigger>("test", TestState.Idle);
        machine.Configure(TestState.Idle).Permit(TestTrigger.Start, TestState.Running);

        machine.Fire(TestTrigger.Start);

        Assert.AreEqual(TestState.Running, machine.State);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("test", events[0].MachineName);
        Assert.AreEqual(nameof(TestState.Idle), events[0].SourceState);
        Assert.AreEqual(nameof(TestState.Running), events[0].DestinationState);
        Assert.AreEqual(nameof(TestTrigger.Start), events[0].Trigger);
    }

    [TestMethod]
    public void InstanceViewModelOpensSelectedChildFolderThroughAdapter()
    {
        var external = new RecordingExternalProcessService();
        var viewModel = new PixelInstanceViewModel(new MinecraftInstanceService(), external);
        var versionDirectory = Path.Combine(Path.GetTempPath(), "PCLTest", "PixelInstance", Guid.NewGuid().ToString("N"));
        viewModel.Instances.Add(new MinecraftInstanceSummary(
            "Demo",
            Path.GetDirectoryName(versionDirectory)!,
            versionDirectory,
            Path.Combine(versionDirectory, "Demo.json"),
            "release",
            DateTime.UnixEpoch,
            false,
            false));
        viewModel.SelectInstanceByPath(versionDirectory);

        viewModel.OpenSelectedFolder("mods");

        var expected = Path.Combine(versionDirectory, "mods");
        Assert.AreEqual(expected, external.OpenedPath);
        Assert.IsTrue(Directory.Exists(expected));
    }

    [TestMethod]
    public void FeatureVisibilityDefaultsToVisibleWhenConfigIsMissing()
    {
        Assert.IsTrue(FeatureVisibilityService.IsMainPageVisible(MainPageKind.Launch));
        Assert.IsTrue(FeatureVisibilityService.IsToolVisible(PixelToolFeature.Help));
        Assert.IsTrue(FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Edit));
        Assert.IsTrue(FeatureVisibilityService.IsFunctionVisible(PixelFunctionFeature.Select));
    }

    [TestMethod]
    public void LaunchStateMachinePublishesTransitionEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var machine = new PixelLaunchStateMachine(factory);

        machine.Fire(PixelLaunchTrigger.Start);
        machine.Fire(PixelLaunchTrigger.LaunchCoreStarted);
        machine.Fire(PixelLaunchTrigger.ProcessStarted);
        machine.Fire(PixelLaunchTrigger.WindowDetected);

        Assert.AreEqual(PixelLaunchState.Running, machine.State);
        Assert.AreEqual(4, events.Count);
        Assert.AreEqual("PixelLaunch", events[0].MachineName);
        Assert.AreEqual(nameof(PixelLaunchState.Idle), events[0].SourceState);
        Assert.AreEqual(nameof(PixelLaunchState.Running), events[^1].DestinationState);
    }

    [TestMethod]
    public void DownloadStateMachinePublishesVersionRefreshTransitions()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var machine = new PixelDownloadStateMachine(factory);

        machine.Fire(PixelDownloadTrigger.StartVersionRefresh);
        machine.Fire(PixelDownloadTrigger.VersionsResolved);

        Assert.AreEqual(PixelDownloadState.Completed, machine.State);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("PixelDownload", events[0].MachineName);
        Assert.AreEqual(nameof(PixelDownloadState.Idle), events[0].SourceState);
        Assert.AreEqual(nameof(PixelDownloadState.ResolvingVersions), events[0].DestinationState);
        Assert.AreEqual(nameof(PixelDownloadTrigger.StartVersionRefresh), events[0].Trigger);
        Assert.AreEqual(nameof(PixelDownloadState.Completed), events[^1].DestinationState);
    }

    [TestMethod]
    public void DownloadStateMachineSupportsLoaderDownloadSaveAndCancelPaths()
    {
        using var eventBus = new ReactivePixelEventBus();
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);

        var loaderMachine = new PixelDownloadStateMachine(factory);
        loaderMachine.Fire(PixelDownloadTrigger.OpenLoaderSelection);
        loaderMachine.Fire(PixelDownloadTrigger.StartDownload);
        loaderMachine.Fire(PixelDownloadTrigger.Complete);
        Assert.AreEqual(PixelDownloadState.Completed, loaderMachine.State);

        var saveMachine = new PixelDownloadStateMachine(factory);
        saveMachine.Fire(PixelDownloadTrigger.StartSave);
        saveMachine.Fire(PixelDownloadTrigger.Complete);
        Assert.AreEqual(PixelDownloadState.Completed, saveMachine.State);

        var cancelledMachine = new PixelDownloadStateMachine(factory);
        cancelledMachine.Fire(PixelDownloadTrigger.StartDownload);
        cancelledMachine.Fire(PixelDownloadTrigger.Cancel);
        Assert.AreEqual(PixelDownloadState.Cancelled, cancelledMachine.State);
        Assert.IsTrue(cancelledMachine.CanFire(PixelDownloadTrigger.Reset));
    }

    [TestMethod]
    public void GameLinkStateMachinePublishesTransitionEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var machine = new PixelGameLinkStateMachine(factory);

        machine.Fire(PixelGameLinkTrigger.StartHosting);
        machine.Fire(PixelGameLinkTrigger.OperationSucceeded);
        machine.Fire(PixelGameLinkTrigger.Leave);
        machine.Fire(PixelGameLinkTrigger.Left);

        Assert.AreEqual(PixelGameLinkState.Ready, machine.State);
        Assert.AreEqual(4, events.Count);
        Assert.AreEqual("PixelGameLink", events[0].MachineName);
        Assert.AreEqual(nameof(PixelGameLinkState.Ready), events[0].SourceState);
        Assert.AreEqual(nameof(PixelGameLinkState.Connected), events[1].DestinationState);
    }

    [TestMethod]
    public async Task ExportLaunchLogCommandWritesFileAndPublishesEvent()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<LaunchLogExportedEvent>();
        using var subscription = eventBus.Observe<LaunchLogExportedEvent>().Subscribe(new ListObserver<LaunchLogExportedEvent>(events));
        var handler = new ExportLaunchLogCommandHandler(eventBus);
        var directory = Path.Combine(Path.GetTempPath(), "PCLTest", "LaunchLogs", Guid.NewGuid().ToString("N"));

        var path = await handler.Handle(
            new ExportLaunchLogCommand("hello launch", directory, new DateTimeOffset(2026, 6, 9, 12, 30, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.AreEqual(Path.Combine(directory, "launch-20260609-123000.log"), path);
        Assert.AreEqual("hello launch", File.ReadAllText(path));
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(path, events[0].Path);
    }

    [TestMethod]
    public async Task KillMinecraftProcessCommandPublishesRequestedAndFailedEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        using var process = new Process();
        var requestedEvents = new List<MinecraftProcessKillRequestedEvent>();
        var failedEvents = new List<MinecraftProcessKillFailedEvent>();
        using var requestedSubscription = eventBus.Observe<MinecraftProcessKillRequestedEvent>()
            .Subscribe(new ListObserver<MinecraftProcessKillRequestedEvent>(requestedEvents));
        using var failedSubscription = eventBus.Observe<MinecraftProcessKillFailedEvent>()
            .Subscribe(new ListObserver<MinecraftProcessKillFailedEvent>(failedEvents));
        var handler = new KillMinecraftProcessCommandHandler(eventBus);

        var killed = await handler.Handle(
            new KillMinecraftProcessCommand(process, "unit-test"),
            CancellationToken.None);

        Assert.IsFalse(killed);
        Assert.AreEqual(1, requestedEvents.Count);
        Assert.AreEqual(-1, requestedEvents[0].ProcessId);
        Assert.AreEqual("unit-test", requestedEvents[0].Reason);
        Assert.AreEqual(1, failedEvents.Count);
        Assert.AreEqual(-1, failedEvents[0].ProcessId);
        Assert.AreEqual("unit-test", failedEvents[0].Reason);
        Assert.IsFalse(string.IsNullOrWhiteSpace(failedEvents[0].Message));
    }

    [TestMethod]
    public async Task CancelDownloadCommandsPublishRequestedEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var taskEvents = new List<MinecraftDownloadTaskCancelRequestedEvent>();
        var allEvents = new List<MinecraftDownloadAllTasksCancelRequestedEvent>();
        using var taskSubscription = eventBus.Observe<MinecraftDownloadTaskCancelRequestedEvent>()
            .Subscribe(new ListObserver<MinecraftDownloadTaskCancelRequestedEvent>(taskEvents));
        using var allSubscription = eventBus.Observe<MinecraftDownloadAllTasksCancelRequestedEvent>()
            .Subscribe(new ListObserver<MinecraftDownloadAllTasksCancelRequestedEvent>(allEvents));
        var downloadService = new MinecraftDownloadService();
        var taskHandler = new CancelMinecraftDownloadTaskCommandHandler(downloadService, eventBus);
        var allHandler = new CancelAllMinecraftDownloadsCommandHandler(downloadService, eventBus);

        var accepted = await taskHandler.Handle(
            new CancelMinecraftDownloadTaskCommand("missing-task"),
            CancellationToken.None);
        await allHandler.Handle(new CancelAllMinecraftDownloadsCommand(), CancellationToken.None);

        Assert.IsFalse(accepted);
        Assert.AreEqual(1, taskEvents.Count);
        Assert.AreEqual("missing-task", taskEvents[0].TaskId);
        Assert.IsFalse(taskEvents[0].Accepted);
        Assert.AreEqual(1, allEvents.Count);
    }

    private enum TestState
    {
        Idle,
        Running
    }

    private enum TestTrigger
    {
        Start
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

    private sealed class RecordingExternalProcessService : IExternalProcessService
    {
        public string? OpenedPath { get; private set; }

        public void OpenPath(string path)
        {
            OpenedPath = path;
        }

        public void OpenUrl(string url)
        {
        }
    }
}
