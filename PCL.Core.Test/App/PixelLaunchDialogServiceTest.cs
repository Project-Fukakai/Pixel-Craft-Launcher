using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelLaunchDialogServiceTest
{
    [TestMethod]
    public void LaunchFailureDialogIncludesMessageAndAnalysis()
    {
        var service = new PixelLaunchDialogService();
        var result = MinecraftLaunchResult.Failed(MinecraftLaunchFailureKind.Unknown, "Java not found");

        var dialog = service.GetLaunchFailureDialog(result, "检查 Java 路径");

        Assert.AreEqual("启动失败", dialog.Title);
        Assert.IsTrue(dialog.IsWarning);
        Assert.AreEqual("关闭", dialog.CloseButtonText);
        Assert.AreEqual("导出日志", dialog.ExportLogButtonText);
        Assert.AreEqual("打开日志", dialog.OpenLogButtonText);
        StringAssert.Contains(dialog.Markdown, "Java not found");
        StringAssert.Contains(dialog.Markdown, "检查 Java 路径");
    }

    [TestMethod]
    public void CrashDialogFormatsUnknownExitCode()
    {
        var service = new PixelLaunchDialogService();
        var exit = new MinecraftProcessExitInfo(
            100,
            "Minecraft 1.20.1",
            null,
            false,
            true,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:01:00Z"));

        var dialog = service.GetCrashDialog(exit, null);

        Assert.AreEqual("Minecraft 已崩溃", dialog.Title);
        StringAssert.Contains(dialog.Markdown, "Minecraft 1.20.1");
        StringAssert.Contains(dialog.Markdown, "退出码：未知");
        StringAssert.Contains(dialog.Markdown, "暂无分析结果。");
    }
}
