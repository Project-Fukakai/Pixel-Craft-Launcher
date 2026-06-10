using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record PixelLaunchIssueDialogSnapshot(
    string Title,
    string Markdown,
    bool IsWarning,
    string CloseButtonText,
    string ExportLogButtonText,
    string OpenLogButtonText);

public sealed class PixelLaunchDialogService
{
    public PixelLaunchIssueDialogSnapshot GetLaunchFailureDialog(
        MinecraftLaunchResult result,
        string? crashAnalysis)
    {
        return new PixelLaunchIssueDialogSnapshot(
            "启动失败",
            $"{result.Message}\n\n可能原因：\n{NormalizeAnalysis(crashAnalysis)}",
            true,
            "关闭",
            "导出日志",
            "打开日志");
    }

    public PixelLaunchIssueDialogSnapshot GetCrashDialog(
        MinecraftProcessExitInfo exit,
        string? crashAnalysis)
    {
        var exitCode = exit.ExitCode?.ToString() ?? "未知";
        return new PixelLaunchIssueDialogSnapshot(
            "Minecraft 已崩溃",
            $"{exit.InstanceName} 异常退出，退出码：{exitCode}\n\n分析结果：\n{NormalizeAnalysis(crashAnalysis)}",
            true,
            "关闭",
            "导出日志",
            "打开日志");
    }

    private static string NormalizeAnalysis(string? crashAnalysis) =>
        string.IsNullOrWhiteSpace(crashAnalysis) ? "暂无分析结果。" : crashAnalysis;
}
