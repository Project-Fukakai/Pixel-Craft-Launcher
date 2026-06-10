using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchViewModelTest
{
    [TestMethod]
    public void ExportLaunchLogUsesCommandBus()
    {
        var commandBus = new RecordingCommandBus();
        var viewModel = new PixelLaunchViewModel(
            new MinecraftLaunchService(),
            new MinecraftProcessMonitor(),
            new MinecraftRepairService(),
            new MinecraftProfileService(),
            new PixelLaunchSummaryService(),
            new PixelLaunchDialogService(),
            new PixelOperationDelayService(),
            new ImmediateUiDispatcher(),
            commandBus: commandBus);

        var path = viewModel.ExportLaunchLog();

        Assert.AreEqual("/tmp/pixel-launch.log", path);
        Assert.AreEqual(path, viewModel.LastExportedLogPath);
        Assert.AreEqual(PixelLaunchViewModel.GetWindowMessages(), viewModel.GetWindowMessagesSnapshot());
        Assert.IsTrue(commandBus.SentRequests.Exists(static request => request is ExportLaunchLogCommand));
    }

    [TestMethod]
    public void LaunchWindowMessagesAreProvidedByViewModel()
    {
        var messages = PixelLaunchViewModel.GetWindowMessages();
        var exit = new PixelGameExitedSnapshot(
            "Minecraft 1.20.1",
            0,
            false);

        Assert.AreEqual("已强制关闭 Minecraft。", messages.GameKilledMessage);
        Assert.AreEqual("Minecraft 1.20.1 已退出。", messages.GetGameExitedMessage(exit));
        Assert.AreEqual("启动日志已导出：/tmp/pixel-launch.log", messages.GetLaunchLogExportedMessage("/tmp/pixel-launch.log"));
        Assert.AreEqual(messages.GameKilledMessage, PixelLaunchViewModel.GetGameKilledMessage());
        Assert.AreEqual(messages.GetGameExitedMessage(exit), PixelLaunchViewModel.GetGameExitedMessage(exit));
        Assert.AreEqual(messages.GetLaunchLogExportedMessage("/tmp/pixel-launch.log"), PixelLaunchViewModel.GetLaunchLogExportedMessage("/tmp/pixel-launch.log"));
    }

    [TestMethod]
    public void MainButtonSnapshotSelectsDownloadOrLaunchAction()
    {
        var viewModel = CreateViewModel();

        var empty = viewModel.GetMainButtonSnapshot();
        Assert.AreEqual("下载游戏", empty.PrimaryText);
        Assert.AreEqual("未找到可用的游戏实例", empty.SecondaryText);
        Assert.AreEqual(PixelLaunchPrimaryAction.DownloadGame, empty.PrimaryAction);
        Assert.IsTrue(empty.IsEnabled);

        viewModel.Instances.Add("Minecraft 1.20.1");

        var launch = viewModel.GetMainButtonSnapshot();
        Assert.AreEqual("启动游戏", launch.PrimaryText);
        Assert.AreEqual(PixelLaunchPrimaryAction.LaunchGame, launch.PrimaryAction);
        Assert.IsFalse(launch.IsEnabled);
    }

    [TestMethod]
    public void LaunchingPanelSnapshotExposesInitialUiState()
    {
        var viewModel = CreateViewModel();

        var snapshot = viewModel.GetLaunchingPanelSnapshot();

        Assert.IsFalse(snapshot.IsVisible);
        Assert.AreEqual("正在启动游戏", snapshot.Title);
        Assert.AreEqual("未找到可用的游戏实例", snapshot.InstanceName);
        Assert.AreEqual("初始化", snapshot.Stage);
        Assert.AreEqual("未选择档案", snapshot.ProfileMethod);
        Assert.AreEqual(0, snapshot.Progress);
        Assert.AreEqual("0.00 %", snapshot.ProgressText);
    }

    private sealed class RecordingCommandBus : IPixelCommandBus
    {
        public List<object> SentRequests { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            object response = request switch
            {
                ExportLaunchLogCommand => "/tmp/pixel-launch.log",
                _ => throw new System.NotSupportedException(request.GetType().FullName)
            };
            return Task.FromResult((TResponse)response);
        }

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

    private static PixelLaunchViewModel CreateViewModel(IPixelCommandBus? commandBus = null)
    {
        return new PixelLaunchViewModel(
            new MinecraftLaunchService(),
            new MinecraftProcessMonitor(),
            new MinecraftRepairService(),
            new MinecraftProfileService(),
            new PixelLaunchSummaryService(),
            new PixelLaunchDialogService(),
            new PixelOperationDelayService(),
            new ImmediateUiDispatcher(),
            commandBus: commandBus ?? new RecordingCommandBus());
    }
}
