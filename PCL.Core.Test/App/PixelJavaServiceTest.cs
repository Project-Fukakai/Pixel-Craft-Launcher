using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Java;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Utils;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelJavaServiceTest
{
    [TestMethod]
    public void EntrySnapshotFormatsJavaStateForUi()
    {
        var entry = CreateJavaEntry(17, enabled: true);

        var snapshot = PixelJavaService.CreateEntrySnapshot(entry, entry.Installation.JavaExePath);

        StringAssert.Contains(snapshot.Title, "JDK 17");
        StringAssert.Contains(snapshot.Title, "64 Bit");
        StringAssert.Contains(snapshot.Info, entry.Installation.JavaFolder);
        StringAssert.Contains(snapshot.Details, "版本: 17.0.0");
        Assert.AreEqual(entry.Installation.JavaExePath, snapshot.JavaExecutablePath);
        Assert.IsTrue(snapshot.IsAvailable);
        Assert.IsTrue(snapshot.IsEnabled);
        Assert.IsTrue(snapshot.IsSelected);
        Assert.AreEqual(1d, snapshot.Opacity);
    }

    [TestMethod]
    public void EntrySnapshotDimsDisabledJava()
    {
        var entry = CreateJavaEntry(8, enabled: false);

        var snapshot = PixelJavaService.CreateEntrySnapshot(entry, selectedJava: string.Empty);

        StringAssert.Contains(snapshot.Info, "（已禁用）");
        Assert.IsFalse(snapshot.IsEnabled);
        Assert.IsFalse(snapshot.IsSelected);
        Assert.AreEqual(0.58d, snapshot.Opacity);
    }

    [TestMethod]
    public async Task AddJavaEntryCommandRejectsMissingExecutableBeforeServiceAccess()
    {
        using var eventBus = new ReactivePixelEventBus();
        var handler = new AddJavaEntryCommandHandler(new PixelJavaService(new CapturingCommandBus()), eventBus);
        var missing = Path.Combine(Path.GetTempPath(), "pcl-pixel-java-test", Guid.NewGuid().ToString("N"), "java");

        try
        {
            await handler.Handle(new AddJavaEntryCommand(missing), CancellationToken.None);
            Assert.Fail("Expected missing Java executable to be rejected.");
        }
        catch (FileNotFoundException ex)
        {
            Assert.AreEqual(missing, ex.FileName);
        }
    }

    [TestMethod]
    public async Task PageOperationsSendJavaCommandsThroughCommandBus()
    {
        var commandBus = new CapturingCommandBus();
        var service = new PixelJavaService(commandBus);

        await service.AddForPageAsync("/path/to/java");
        Assert.IsInstanceOfType(commandBus.LastRequest, typeof(AddJavaEntryCommand));

        await service.SelectDefaultForPageAsync("/path/to/java");
        Assert.IsInstanceOfType(commandBus.LastRequest, typeof(SelectDefaultJavaByPathCommand));

        await service.ToggleEnabledForPageAsync("/path/to/java");
        Assert.IsInstanceOfType(commandBus.LastRequest, typeof(ToggleJavaEntryEnabledByPathCommand));

        await service.RefreshForPageAsync();
        Assert.IsInstanceOfType(commandBus.LastRequest, typeof(RefreshJavaEntriesCommand));
    }

    [TestMethod]
    public void JavaListSnapshotExposesEmptyState()
    {
        var entry = CreateJavaEntry(17, enabled: true);
        var entrySnapshot = PixelJavaService.CreateEntrySnapshot(entry, string.Empty);

        var ready = PixelJavaListSnapshot.Ready(string.Empty, [entrySnapshot]);
        var empty = PixelJavaListSnapshot.Ready(string.Empty, []);
        var notReady = PixelJavaListSnapshot.NotReady("未就绪", "稍后重试");

        Assert.IsFalse(ready.IsEmpty);
        Assert.IsTrue(empty.IsEmpty);
        Assert.IsTrue(notReady.IsEmpty);
    }

    [TestMethod]
    public void JavaPageMessagesAreProvidedByService()
    {
        var messages = PixelJavaService.GetPageMessages();
        var instanceMessages = new PixelJavaService(new CapturingCommandBus()).GetMessages();

        Assert.AreEqual("Java 管理", messages.ManagementCardTitle);
        Assert.AreEqual("Java 列表", messages.ListCardTitle);
        Assert.AreEqual("添加", messages.AddButtonText);
        Assert.AreEqual("刷新", messages.RefreshButtonText);
        Assert.AreEqual("Java 管理服务尚未就绪", messages.NotReadyFallbackTitle);
        Assert.AreEqual("自动选择", messages.AutoSelectTitle);
        Assert.AreEqual("依据游戏版本需求自动选择合适的 Java", messages.AutoSelectDescription);
        Assert.AreEqual("默认 Java 已设为自动选择。", messages.AutoSelectSuccessMessage);
        Assert.AreEqual("未检测到 Java", messages.EmptyListTitle);
        Assert.AreEqual("点击刷新重新扫描，或点击添加手动选择 Java 程序。", messages.EmptyListDescription);
        Assert.AreEqual("此 Java 不可用，请刷新列表。", messages.UnavailableMessage);
        Assert.AreEqual("请先启用此 Java 后再选择其作为默认 Java。", messages.DisabledSelectionMessage);
        Assert.AreEqual("默认 Java 已设为 JDK 17。", messages.GetDefaultSelectedMessage("JDK 17"));
        Assert.AreEqual("打开", messages.OpenFolderTooltip);
        Assert.AreEqual("详细信息", messages.InfoTooltip);
        Assert.AreEqual("禁用此 Java", messages.DisableTooltip);
        Assert.AreEqual("启用此 Java", messages.EnableTooltip);
        Assert.AreEqual("已启用 Java。", messages.GetToggleEnabledMessage(true));
        Assert.AreEqual("已禁用 Java。", messages.GetToggleEnabledMessage(false));
        Assert.AreEqual("已添加 Java。", messages.AddSuccessMessage);
        Assert.AreEqual("正在刷新 Java 列表……", messages.RefreshStartMessage);
        Assert.AreEqual("Java 列表已刷新。", messages.RefreshSuccessMessage);
        Assert.AreEqual("Java 信息", messages.InfoDialogTitle);
        Assert.AreEqual("选择 Java 程序", messages.AddPickerTitle);
        Assert.AreEqual("Java 程序", messages.AddPickerFileTypeName);
        Assert.AreEqual("打开 Java 文件夹失败：boom", messages.GetOpenFolderFailedMessage(new InvalidOperationException("boom")));
        Assert.AreEqual("boom", messages.GetOperationFailureMessage(new InvalidOperationException("boom")));

        Assert.AreEqual(messages.AddSuccessMessage, PixelJavaService.GetAddSuccessMessage());
        Assert.AreEqual(messages.RefreshStartMessage, PixelJavaService.GetRefreshStartMessage());
        Assert.AreEqual(messages.RefreshSuccessMessage, PixelJavaService.GetRefreshSuccessMessage());
        Assert.AreEqual(messages.InfoDialogTitle, PixelJavaService.GetInfoDialogTitle());
        Assert.AreEqual(messages.AddPickerTitle, PixelJavaService.GetAddPickerTitle());
        Assert.AreEqual(messages.AddPickerFileTypeName, PixelJavaService.GetAddPickerFileTypeName());
        Assert.AreEqual(messages, instanceMessages);
        Assert.AreEqual(
            "打开 Java 文件夹失败：boom",
            PixelJavaService.GetOpenFolderFailedMessage(new InvalidOperationException("boom")));
    }

    private static JavaEntry CreateJavaEntry(int major, bool enabled)
    {
        var folder = Path.Combine(Path.GetTempPath(), "pcl-pixel-java-test", major.ToString());
        Directory.CreateDirectory(folder);
        var executable = Path.Combine(folder, OperatingSystem.IsWindows() ? "java.exe" : "java");
        File.WriteAllText(executable, string.Empty);

        return new JavaEntry
        {
            Installation = new JavaInstallation(folder, new Version(major, 0, 0), JavaBrandType.OpenJDK, MachineType.AMD64, true, false),
            IsEnabled = enabled
        };
    }

    private sealed class CapturingCommandBus : IPixelCommandBus
    {
        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }
}
