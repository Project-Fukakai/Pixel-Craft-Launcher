using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Download;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.IO.Download;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelDownloadViewModelTest
{
    [TestMethod]
    public void RefreshStartMessageIsUiReady()
    {
        var pageMessages = PixelDownloadViewModel.GetPageMessages();
        var viewModel = CreateViewModel();
        var messages = pageMessages.Window;

        Assert.AreEqual(pageMessages, viewModel.GetPageMessagesSnapshot());
        Assert.AreEqual("正在刷新……", messages.RefreshStartMessage);
        Assert.AreEqual("安装完成：1.20.1", messages.GetInstallCompletedMessage("1.20.1"));
        Assert.AreEqual(messages, PixelDownloadViewModel.GetWindowMessages());
        Assert.AreEqual(messages.RefreshStartMessage, PixelDownloadViewModel.GetRefreshStartMessage());
        Assert.AreEqual(messages.GetInstallCompletedMessage("1.20.1"), PixelDownloadViewModel.GetInstallCompletedMessage("1.20.1"));
        Assert.AreEqual("正在获取 Minecraft 版本列表", pageMessages.VersionLoadingText);
        Assert.AreEqual(pageMessages.VersionLoadingText, PixelDownloadViewModel.GetVersionLoadingText());
    }

    [TestMethod]
    public void InstallPanelMessagesExposeUiText()
    {
        var pageMessages = PixelDownloadViewModel.GetPageMessages();
        var messages = pageMessages.InstallPanel;

        Assert.AreEqual("Mod Loader", messages.LoaderTitle);
        Assert.AreEqual("当前版本暂无可用 Mod Loader", messages.EmptyLoaderText);
        Assert.AreEqual("Mod Loader - 正在加载可用版本", messages.LoadingLoaderTitle);
        Assert.AreEqual("正在加载可用版本", messages.LoadingLoaderText);
        Assert.AreEqual("重试", messages.RetryButtonText);
        Assert.AreEqual("加载失败", messages.LoaderFailedText);
        Assert.AreEqual("安装", messages.InstallTitle);
        Assert.AreEqual("清除选择", messages.ClearSelectionButtonText);
        Assert.AreEqual(messages, PixelDownloadViewModel.GetInstallPanelMessages());
    }

    [TestMethod]
    public void DownloadPageMessagesExposeSidebarVersionAndTaskText()
    {
        var pageMessages = PixelDownloadViewModel.GetPageMessages();
        var sidebar = pageMessages.InstallSidebar;
        var versions = pageMessages.VersionList;
        var tasks = pageMessages.TaskDetails;
        var stats = pageMessages.ManagerStats;

        Assert.AreEqual("实例名称", sidebar.InstanceNameSectionTitle);
        Assert.AreEqual("安装清单", sidebar.ChecklistSectionTitle);
        Assert.AreEqual("名称", sidebar.NameInputTitle);
        Assert.AreEqual("目录", sidebar.DirectoryInputTitle);
        Assert.AreEqual("删除", sidebar.ClearChoiceTooltip);
        Assert.AreEqual("原版实例", sidebar.VanillaInstanceText);

        Assert.AreEqual("Minecraft 版本", versions.EmptyGroupTitle);
        Assert.AreEqual("最新版本", versions.TopGroupTitle);
        Assert.AreEqual("正在加载版本列表...", versions.LoadingText);
        Assert.AreEqual("没有匹配的版本。", versions.EmptyText);
        Assert.AreEqual("另存为", versions.SaveClientTooltip);
        Assert.AreEqual("选择保存位置", versions.SaveClientPickerTitle);
        Assert.AreEqual("更新日志", versions.VersionInfoTooltip);
        Assert.AreEqual("下载服务端", versions.SaveServerTooltip);
        Assert.AreEqual("选择服务端保存位置", versions.SaveServerPickerTitle);

        Assert.AreEqual("下载任务", tasks.TaskTitle);
        Assert.AreEqual("取消", tasks.CancelTooltip);
        Assert.AreEqual("已请求取消下载任务组。", tasks.CancelGroupRequestedMessage);
        Assert.AreEqual("正在完成安装", tasks.FinishingText);
        Assert.AreEqual("暂无下载任务", tasks.EmptyText);

        Assert.AreEqual("总进度", stats.ProgressTitle);
        Assert.AreEqual("下载速度", stats.SpeedTitle);
        Assert.AreEqual("剩余文件", stats.RemainingFilesTitle);
        Assert.AreEqual("剩余线程", stats.RemainingThreadsTitle);
        Assert.AreEqual(sidebar, PixelDownloadViewModel.GetInstallSidebarMessages());
        Assert.AreEqual(versions, PixelDownloadViewModel.GetVersionListMessages());
        Assert.AreEqual(tasks, PixelDownloadViewModel.GetTaskDetailsMessages());
        Assert.AreEqual(stats, PixelDownloadViewModel.GetManagerStatsMessages());
    }

    [TestMethod]
    public void TaskListChangedEventExposesNarrowTaskChangeSignal()
    {
        var viewModel = CreateViewModel();
        var count = 0;
        viewModel.TaskListChanged += (_, _) => count++;

        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("task", "Task", NDlTaskState.Waiting, 0, null));

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void ManagerStatsSnapshotFormatsDownloadTaskState()
    {
        var viewModel = CreateViewModel();
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo(
            "task-1",
            "Asset 1",
            NDlTaskState.Running,
            0.25,
            null,
            SpeedBytesPerSecond: 2048));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo(
            "task-2",
            "Asset 2",
            NDlTaskState.Waiting,
            0.5,
            null,
            SpeedBytesPerSecond: 1024));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo(
            "task-3",
            "Asset 3",
            NDlTaskState.Finished,
            1,
            null,
            SpeedBytesPerSecond: 1024));

        var stats = viewModel.GetManagerStatsSnapshot();

        Assert.AreEqual("37.50 %", stats.ProgressText);
        Assert.AreEqual("3 KB/s", stats.SpeedText);
        Assert.AreEqual("2", stats.RemainingFilesText);
        Assert.AreEqual("1 / 4", stats.RemainingThreadsText);
    }

    [TestMethod]
    public void InstallPanelSnapshotCombinesSummaryHintsAndLoaderChoiceState()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedVersion = new MinecraftVersionManifestEntry("1.20.1", "release", "url", DateTime.UnixEpoch, DateTime.UnixEpoch);

        var snapshot = viewModel.GetInstallPanelSnapshot();

        Assert.AreEqual("1.20.1", snapshot.Summary.VersionTitle);
        Assert.AreEqual("Mod Loader: 原版", snapshot.Summary.LoaderInfo);
        Assert.AreEqual(PixelDownloadLoaderChoiceStateKind.Empty, snapshot.LoaderChoiceState.Kind);
        Assert.IsNull(snapshot.LoaderChoiceState.ErrorText);
        Assert.AreEqual(0, snapshot.Hints.Count);
        Assert.AreEqual(0, snapshot.LoaderChoiceGroups.Count);
    }

    [TestMethod]
    public void VersionSnapshotsExposeGroupedUiState()
    {
        var viewModel = CreateViewModel();
        viewModel.Versions.Add(new MinecraftVersionManifestEntry("1.21.6", "release", "release-url", new DateTime(2026, 3, 1), new DateTime(2026, 3, 1, 10, 0, 0)));
        viewModel.Versions.Add(new MinecraftVersionManifestEntry("26w14a", "snapshot", "snapshot-url", new DateTime(2026, 4, 1), new DateTime(2026, 4, 1, 10, 0, 0)));
        viewModel.Versions.Add(new MinecraftVersionManifestEntry("b1.7.3", "old_beta", "legacy-url", new DateTime(2011, 7, 8), new DateTime(2011, 7, 8, 10, 0, 0)));
        viewModel.SearchText = "26w";

        var topVersions = viewModel.GetTopVersionSnapshots();
        var groups = viewModel.GetVersionGroupSnapshots();
        var page = viewModel.GetVersionListPageSnapshot();

        Assert.AreEqual(1, topVersions.Count);
        Assert.AreEqual("26w14a", topVersions[0].Id);
        Assert.AreEqual(PixelDownloadVersionKind.AprilFools, topVersions[0].Kind);
        Assert.AreEqual("mdi-party-popper", topVersions[0].Icon);
        StringAssert.Contains(topVersions[0].Info, "愚人节版");
        Assert.AreEqual("26w14a", topVersions[0].WikiUrlSuffix);
        Assert.AreEqual(0, groups.Single(group => group.Title == "正式版").Versions.Count);
        Assert.AreEqual(1, groups.Single(group => group.Title == "愚人节版").Versions.Count);
        Assert.AreEqual(3, page.Groups.Count);
        Assert.AreEqual("最新版本", page.Groups[0].CardTitle);
        Assert.IsTrue(page.Groups[0].HasVersions);
        Assert.IsFalse(page.Groups[0].IsSwapped);
        Assert.AreEqual("预览版 (1)", page.Groups[1].CardTitle);
        Assert.IsTrue(page.Groups[1].IsSwapped);
        Assert.AreEqual("愚人节版 (1)", page.Groups[2].CardTitle);
        Assert.IsTrue(page.Groups[2].IsSwapped);
        Assert.AreEqual("没有匹配的版本。", page.EmptyListText);

        viewModel.Versions.Clear();
        var emptyPage = viewModel.GetVersionListPageSnapshot();
        Assert.IsFalse(emptyPage.Groups[0].HasVersions);
    }

    [TestMethod]
    public void LoaderChoiceSnapshotsExposeSelectableItemsAndChecklist()
    {
        var viewModel = CreateViewModel();
        viewModel.Versions.Add(new MinecraftVersionManifestEntry("1.20.1", "release", "url", new DateTime(2023, 6, 7), new DateTime(2023, 6, 7, 10, 0, 0)));
        viewModel.SelectVersion("1.20.1");
        var fabric = new MinecraftLoaderVersionEntry(
            MinecraftLoaderKind.Fabric,
            "0.15.11",
            "Fabric 0.15.11",
            "1.20.1",
            true,
            true,
            MinecraftRemoteSource.Generated,
            ReleaseTime: new DateTime(2024, 1, 2, 3, 4, 0));
        var fabricApi = new MinecraftAddonFileEntry(
            MinecraftAddonKind.FabricApi,
            "fabric-api-1",
            "Fabric API 0.92.2",
            "fabric-api.jar",
            ["https://example.invalid/fabric-api.jar"],
            ["1.20.1"],
            [MinecraftLoaderKind.Fabric],
            null,
            null,
            true,
            new DateTime(2024, 1, 3, 4, 5, 0),
            MinecraftRemoteSource.Generated);
        viewModel.LoaderChoiceGroups.Add(new PixelLoaderChoiceGroup(
            "Fabric",
            "Fabric loader",
            "mdi-feather",
            MinecraftLoaderKind.Fabric,
            null,
            [fabric],
            [],
            "可以安装",
            true));
        viewModel.LoaderChoiceGroups.Add(new PixelLoaderChoiceGroup(
            "Fabric API",
            "Common Fabric API",
            "mdi-feather",
            null,
            MinecraftAddonKind.FabricApi,
            [],
            [fabricApi],
            "可以添加",
            true));
        viewModel.LoaderChoiceGroups.Add(new PixelLoaderChoiceGroup(
            "Forge",
            "Forge loader",
            "mdi-anvil",
            MinecraftLoaderKind.Forge,
            null,
            [],
            [],
            "可以安装",
            true));

        var before = viewModel.GetLoaderChoiceSnapshots();
        viewModel.SelectLoaderChoiceItem(before.Single(group => group.Title == "Fabric").Items[0].Id);

        var after = viewModel.GetLoaderChoiceSnapshots();
        var checklist = viewModel.GetInstallChecklistSnapshots();

        Assert.AreEqual(MinecraftLoaderKind.Fabric, viewModel.SelectedLoaderKind);
        Assert.AreEqual("0.15.11", viewModel.SelectedLoaderVersion);
        Assert.IsNotNull(viewModel.MergedSelection.FabricApi);
        Assert.IsTrue(after.Single(group => group.Title == "Fabric").IsActive);
        Assert.IsTrue(after.Single(group => group.Title == "Fabric").Items[0].IsChecked);
        Assert.IsTrue(after.Single(group => group.Title == "Fabric").HasItems);
        Assert.IsFalse(after.Single(group => group.Title == "Forge").HasItems);
        Assert.AreEqual(0, viewModel.GetInstallHintSnapshots().Count);
        Assert.IsTrue(checklist.Any(item => item.Title == "Fabric 0.15.11" && item.CanClear));
        Assert.IsTrue(checklist.Any(item => item.Title == "0.92.2" && item.Info == "Fabric API"));

        viewModel.ClearInstallChoice(checklist.Single(item => item.Info == "Fabric").ClearActionId!);

        Assert.IsNull(viewModel.MergedSelection.Fabric);
        Assert.IsNull(viewModel.MergedSelection.FabricApi);
    }

    [TestMethod]
    public void InstallStateSnapshotExposesButtonAndTitleState()
    {
        var viewModel = CreateViewModel();
        viewModel.Versions.Add(new MinecraftVersionManifestEntry("1.20.1", "release", "url", new DateTime(2023, 6, 7), new DateTime(2023, 6, 7, 10, 0, 0)));
        viewModel.SelectVersion("1.20.1");
        viewModel.TargetFolder = "/tmp/minecraft";
        viewModel.InstanceName = "1.20.1";

        var snapshot = viewModel.GetInstallStateSnapshot();
        var sidebar = viewModel.GetInstallSidebarSnapshot();

        Assert.IsTrue(snapshot.CanInstall);
        Assert.IsFalse(snapshot.IsBusy);
        Assert.AreEqual("开始下载", snapshot.PrimaryActionText);
        Assert.AreEqual("mdi-download", snapshot.PrimaryActionIcon);
        Assert.AreEqual("1.20.1 安装", snapshot.SecondaryTitle);
        Assert.AreEqual("原版", snapshot.SelectedLoaderLabel);
        Assert.IsFalse(snapshot.HasPendingOperation);
        Assert.AreEqual("1.20.1", sidebar.InstanceName);
        Assert.AreEqual("/tmp/minecraft", sidebar.TargetFolder);
        Assert.AreEqual(1, sidebar.ChecklistItems.Count);
        Assert.IsTrue(sidebar.ShowVanillaInstanceText);
        Assert.AreEqual(snapshot, sidebar.InstallState);
    }

    [TestMethod]
    public void OperationSnapshotAggregatesDownloadStateAndErrors()
    {
        using var eventBus = new ReactivePixelEventBus();
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var stateMachine = new PixelDownloadStateMachine(factory);
        var viewModel = CreateViewModel(downloadStateMachine: stateMachine);

        viewModel.VersionLoadingState.State = PixelLoadingState.Error;
        viewModel.VersionLoadingState.Error = new InvalidOperationException("Manifest unavailable");
        stateMachine.Fire(PixelDownloadTrigger.StartVersionRefresh);

        var operation = viewModel.GetOperationSnapshot();
        var taskDetails = viewModel.GetTaskDetailsPageSnapshot();

        Assert.AreEqual(PixelDownloadState.ResolvingVersions, operation.State);
        Assert.IsFalse(operation.IsBusy);
        Assert.IsTrue(operation.IsVersionLoading);
        Assert.IsTrue(operation.HasPendingOperation);
        Assert.AreEqual("Manifest unavailable", operation.VersionErrorText);
        Assert.AreEqual(PixelDownloadTaskDetailsPageKind.Finishing, taskDetails.Kind);
        Assert.AreEqual(operation.StatusText, taskDetails.BodyText);
    }

    [TestMethod]
    public void PendingPageSnapshotExposesCategoryText()
    {
        var viewModel = CreateViewModel();

        var mod = viewModel.GetPendingPageSnapshot(2);
        var forge = viewModel.GetPendingPageSnapshot(11);
        var unknown = viewModel.GetPendingPageSnapshot(100);

        Assert.AreEqual("Mod", mod.Title);
        StringAssert.Contains(mod.Description, "社区资源搜索");
        Assert.AreEqual("Forge", forge.Title);
        StringAssert.Contains(forge.Description, "独立安装包版本列表");
        Assert.AreEqual("下载", unknown.Title);
        Assert.AreEqual("Plain 中这里是下载页。", unknown.Description);
        Assert.AreEqual("迁移状态", mod.StatusTitle);
        CollectionAssert.Contains(mod.StatusLines.ToArray(), "Plain 侧这个页面包含搜索、筛选、详情页和文件安装流程。");
        Assert.AreEqual("实例管理", mod.InstanceManagementTitle);
    }

    [TestMethod]
    public void SidebarSnapshotExposesDownloadCategoriesAndSelection()
    {
        var viewModel = CreateViewModel();

        var snapshot = viewModel.GetSidebarSnapshot(18);
        var allItems = snapshot.Groups.SelectMany(group => group.Items).ToArray();

        Assert.AreEqual(3, snapshot.Groups.Count);
        Assert.IsNull(snapshot.Groups[0].Title);
        Assert.AreEqual("社区资源", snapshot.Groups[1].Title);
        Assert.AreEqual("安装包", snapshot.Groups[2].Title);
        Assert.AreEqual(18, allItems.Single(item => item.Title == "Legacy Fabric").Category);
        Assert.AreEqual("mdi-feather", allItems.Single(item => item.Title == "Legacy Fabric").Icon);
        Assert.AreEqual("刷新", allItems.Single(item => item.Title == "Legacy Fabric").RefreshButtonTooltip);
        Assert.IsTrue(allItems.Single(item => item.Category == 18).IsSelected);
        Assert.IsFalse(allItems.Single(item => item.Category == 14).IsSelected);
    }

    [TestMethod]
    public void RightPageKindExposesDownloadRouteTarget()
    {
        var viewModel = CreateViewModel();

        Assert.AreEqual(PixelDownloadRightPageKind.InstallVersionList, viewModel.GetRightPageKind(1, false, false));
        Assert.AreEqual(PixelDownloadRightPageKind.InstallSelection, viewModel.GetRightPageKind(1, true, false));
        Assert.AreEqual(PixelDownloadRightPageKind.ClientVersionList, viewModel.GetRightPageKind(9, false, false));
        Assert.AreEqual(PixelDownloadRightPageKind.Pending, viewModel.GetRightPageKind(2, false, false));
        Assert.AreEqual(PixelDownloadRightPageKind.TaskDetails, viewModel.GetRightPageKind(2, false, true));
        Assert.IsTrue(viewModel.ShouldRefreshVersionsBeforeRightPage(PixelDownloadRightPageKind.InstallVersionList));
        Assert.IsTrue(viewModel.ShouldRefreshVersionsBeforeRightPage(PixelDownloadRightPageKind.Pending));
        Assert.IsFalse(viewModel.ShouldRefreshVersionsBeforeRightPage(PixelDownloadRightPageKind.TaskDetails));

        viewModel.VersionLoadingState.State = PixelLoadingState.Error;

        Assert.AreEqual(PixelDownloadRightPageKind.Loading, viewModel.GetRightPageKind(1, false, false));
        Assert.AreEqual(PixelDownloadRightPageKind.Loading, viewModel.GetRightPageKind(9, false, false));
        Assert.IsTrue(viewModel.ShouldRefreshVersionsBeforeRightPage(PixelDownloadRightPageKind.Loading));
    }

    [TestMethod]
    public void RightPagePresentationExposesRefreshAction()
    {
        var viewModel = CreateViewModel();

        var installVersions = viewModel.GetRightPagePresentation(1, false, false);
        var taskDetails = viewModel.GetRightPagePresentation(1, false, true);

        Assert.AreEqual(PixelDownloadRightPageKind.InstallVersionList, installVersions.Kind);
        Assert.AreEqual(PixelDownloadRightPageRefreshAction.RefreshVersions, installVersions.RefreshAction);
        Assert.AreEqual(PixelDownloadRightPageKind.TaskDetails, taskDetails.Kind);
        Assert.AreEqual(PixelDownloadRightPageRefreshAction.None, taskDetails.RefreshAction);

        viewModel.Versions.Add(new MinecraftVersionManifestEntry(
            "1.20.1",
            "release",
            "url",
            new DateTime(2023, 6, 7),
            new DateTime(2023, 6, 7, 10, 0, 0)));

        var loaded = viewModel.GetRightPagePresentation(1, false, false);

        Assert.AreEqual(PixelDownloadRightPageKind.InstallVersionList, loaded.Kind);
        Assert.AreEqual(PixelDownloadRightPageRefreshAction.None, loaded.RefreshAction);
    }

    [TestMethod]
    public void VisibleTaskSnapshotsHideFinishedAndCancelledTasks()
    {
        var viewModel = CreateViewModel();
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("waiting", "Waiting", NDlTaskState.Waiting, 0, null));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("running", "Running", NDlTaskState.Running, 0.5, null));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("failed", "Failed", NDlTaskState.Failed, 1, "Network failed"));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("finished", "Finished", NDlTaskState.Finished, 1, null));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("cancelled", "Cancelled", NDlTaskState.Cancelled, 0.2, null));

        var snapshots = viewModel.GetVisibleTaskSnapshots();

        Assert.AreEqual(3, snapshots.Count);
        Assert.AreEqual("waiting", snapshots[0].Id);
        Assert.IsTrue(snapshots[0].CanCancel);
        Assert.AreEqual(PixelDownloadTaskStatus.Running, snapshots[1].Status);
        Assert.AreEqual(0.5, snapshots[1].Progress);
        Assert.AreEqual("50%", snapshots[1].ProgressPercentText);
        Assert.AreEqual(PixelDownloadTaskStatus.Failed, snapshots[2].Status);
        Assert.AreEqual("Network failed", snapshots[2].Text);
        StringAssert.Contains(snapshots[2].Info, "失败");
        StringAssert.Contains(snapshots[2].Info, "Network failed");
        Assert.AreEqual("mdi-alert-circle-outline", snapshots[2].Icon);
        Assert.IsFalse(snapshots[2].CanCancel);

        var recent = viewModel.GetRecentTaskSnapshots(10);
        Assert.AreEqual(5, recent.Count);
        Assert.AreEqual("finished", recent[3].Id);
        Assert.AreEqual(PixelDownloadTaskStatus.Cancelled, recent[4].Status);

        var page = viewModel.GetTaskDetailsPageSnapshot();
        Assert.AreEqual(PixelDownloadTaskDetailsPageKind.Tasks, page.Kind);
        Assert.AreEqual("下载任务", page.Title);
        Assert.IsTrue(page.HasCancellableTasks);
        CollectionAssert.AreEqual(new[] { "waiting", "running" }, page.CancellableTaskIds.ToArray());
    }

    [TestMethod]
    public void TaskRouteHelpersExposeSelectedOrFirstVisibleTaskState()
    {
        var viewModel = CreateViewModel();
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("waiting", "Waiting", NDlTaskState.Waiting, 0, null));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("finished", "Finished", NDlTaskState.Finished, 1, null));

        Assert.AreEqual("waiting", viewModel.GetTaskDetailsRouteTaskId());
        Assert.IsTrue(viewModel.HasVisibleTasksOrPendingOperation());
        Assert.IsFalse(viewModel.HasPendingDownloadOperation());
        Assert.IsFalse(viewModel.ShouldReturnFromTaskDetails());

        viewModel.SelectTask("finished");

        Assert.AreEqual("finished", viewModel.GetTaskDetailsRouteTaskId());
    }

    [TestMethod]
    public void TaskRouteHelpersRequestReturnWhenNoVisibleTasksRemain()
    {
        var viewModel = CreateViewModel();
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("finished", "Finished", NDlTaskState.Finished, 1, null));
        viewModel.Tasks.Add(new MinecraftDownloadTaskInfo("cancelled", "Cancelled", NDlTaskState.Cancelled, 0.2, null));

        Assert.IsFalse(viewModel.HasVisibleTasksOrPendingOperation());
        Assert.IsFalse(viewModel.HasPendingDownloadOperation());
        Assert.IsTrue(viewModel.ShouldReturnFromTaskDetails());
    }

    [TestMethod]
    public void TaskGroupTitleUsesSingleTaskName()
    {
        var viewModel = CreateViewModel();
        var task = new PixelDownloadTaskSnapshot(
            "task",
            "Download assets",
            "Download assets",
            "等待中 · 0 %",
            "mdi-clock-outline",
            0,
            "0%",
            0,
            PixelDownloadTaskStatus.Waiting,
            true);

        var title = viewModel.GetTaskGroupTitle([task]);

        Assert.AreEqual("Download assets", title);
    }

    [TestMethod]
    public void ShouldRefreshRightPageForPropertyCoversInstallAndTaskState()
    {
        var viewModel = CreateViewModel();

        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.MergedSelection)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.SelectedLoaderKind)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.IsBusy)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.LoaderChoiceGroups)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.IsLoaderChoicesLoading)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.LoaderChoicesError)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.SelectedTask)));
        Assert.IsTrue(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.DownloadState)));
        Assert.IsFalse(viewModel.ShouldRefreshRightPageForProperty(nameof(PixelDownloadViewModel.SearchText)));
        Assert.IsFalse(viewModel.ShouldRefreshRightPageForProperty(null));
    }

    [TestMethod]
    public async Task DownloadOperationsUseCommandBus()
    {
        var commandBus = new RecordingCommandBus();
        var viewModel = CreateViewModel(
            commandBus: commandBus,
            operationDelayService: new NoOpPixelOperationDelayService());

        await viewModel.RefreshVersionsAsync();
        if (viewModel.Versions.Count == 0)
            viewModel.Versions.Add(commandBus.Version);
        viewModel.TargetFolder = "/tmp/minecraft";
        viewModel.SelectVersion("1.20.1");
        Assert.IsNotNull(viewModel.SelectedVersion);
        await viewModel.InstallSelectedAsync();
        await viewModel.SaveClientCoreAsync("1.20.1", "/tmp/export");
        await viewModel.SaveServerJarAsync("1.20.1", "/tmp/export");
        await viewModel.RefreshLoaderChoicesAsync();
        Assert.IsTrue(await viewModel.CancelTaskAsync("task-1"));
        await viewModel.CancelAsync();

        Assert.IsTrue(commandBus.SentRequests.Any(request => request is RefreshMinecraftVersionsCommand));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is StartDownloadInstallCommand));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is SaveMinecraftClientCoreCommand));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is SaveMinecraftServerJarCommand));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is RefreshMinecraftLoaderChoicesCommand command &&
                                                             command.VersionId == "1.20.1"));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is CancelMinecraftDownloadTaskCommand command &&
                                                             command.TaskId == "task-1"));
        Assert.IsTrue(commandBus.SentRequests.Any(request => request is CancelAllMinecraftDownloadsCommand));
        Assert.IsFalse(viewModel.IsBusy);
        Assert.AreEqual("已请求取消", viewModel.StatusText);
    }

    [TestMethod]
    public async Task DownloadOperationsDriveStateMachineEvents()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>().Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var factory = new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance);
        var stateMachine = new PixelDownloadStateMachine(factory);
        var commandBus = new RecordingCommandBus();
        var viewModel = CreateViewModel(
            commandBus: commandBus,
            downloadStateMachine: stateMachine,
            operationDelayService: new NoOpPixelOperationDelayService());

        await viewModel.RefreshVersionsAsync();
        await viewModel.RefreshLoaderChoicesAsync();
        viewModel.TargetFolder = "/tmp/minecraft";
        viewModel.InstanceName = "1.20.1";
        await viewModel.InstallSelectedAsync();
        await viewModel.SaveServerJarAsync("1.20.1", "/tmp/export");

        Assert.AreEqual(PixelDownloadState.Completed, viewModel.DownloadState);
        var expectedTriggers = string.Join(
            ",",
            [
                nameof(PixelDownloadTrigger.StartVersionRefresh),
                nameof(PixelDownloadTrigger.VersionsResolved),
                nameof(PixelDownloadTrigger.OpenLoaderSelection),
                nameof(PixelDownloadTrigger.LoaderChoicesResolved),
                nameof(PixelDownloadTrigger.StartDownload),
                nameof(PixelDownloadTrigger.Complete),
                nameof(PixelDownloadTrigger.StartSave),
                nameof(PixelDownloadTrigger.Complete)
            ]);
        var actualTriggers = string.Join(
            ",",
            events.Where(@event => @event.MachineName == "PixelDownload").Select(@event => @event.Trigger));
        Assert.AreEqual(expectedTriggers, actualTriggers);
    }

    private static PixelDownloadViewModel CreateViewModel(
        IPixelCommandBus? commandBus = null,
        PixelDownloadStateMachine? downloadStateMachine = null,
        IPixelOperationDelayService? operationDelayService = null,
        IUiDispatcher? uiDispatcher = null)
    {
        var downloadService = new MinecraftDownloadService();
        var loaderCatalogService = new MinecraftModLoaderCatalogService();
        return new PixelDownloadViewModel(
            downloadService,
            new MinecraftInstallService(downloadService),
            loaderCatalogService,
            new MinecraftMergedInstallService(downloadService, loaderCatalogService),
            new MinecraftCorePackageSaver(downloadService),
            new PixelLoaderChoiceService(loaderCatalogService),
            new PixelLoaderSelectionService(),
            new PixelDownloadCategoryRefreshService(),
            operationDelayService ?? new PixelOperationDelayService(),
            uiDispatcher ?? new ImmediateUiDispatcher(),
            commandBus ?? new RecordingCommandBus(),
            downloadStateMachine);
    }

    private sealed class RecordingCommandBus : IPixelCommandBus
    {
        public List<object> SentRequests { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            object response = request switch
            {
                RefreshMinecraftVersionsCommand => new[]
                {
                    Version
                },
                StartDownloadInstallCommand command => new PixelMinecraftInstanceInstalledSnapshot(
                    command.InstanceName ?? command.Version.Id,
                    command.TargetFolder + "/versions/" + (command.InstanceName ?? command.Version.Id)),
                SaveMinecraftClientCoreCommand command => command.BaseFolder,
                SaveMinecraftServerJarCommand command => command.BaseFolder,
                RefreshMinecraftLoaderChoicesCommand => Array.Empty<PixelLoaderChoiceGroup>(),
                CancelMinecraftDownloadTaskCommand => true,
                _ => throw new NotSupportedException(request.GetType().FullName)
            };
            return Task.FromResult((TResponse)response);
        }

        public MinecraftVersionManifestEntry Version { get; } = new(
            "1.20.1",
            "release",
            "url",
            new DateTime(2023, 6, 7),
            new DateTime(2023, 6, 7, 10, 0, 0));

        public Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
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
