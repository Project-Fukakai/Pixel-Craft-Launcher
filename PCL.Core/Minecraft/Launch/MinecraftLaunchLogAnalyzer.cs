using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchLogAnalyzer
{
    public static string AnalyzeLaunchFailure(string message) => AnalyzeCrashLog(message, null);

    public static string AnalyzeCrashLog(string log, int? exitCode)
    {
        var lower = log.ToLowerInvariant();
        var reasons = new List<string>();
        if (lower.Contains("outofmemoryerror") || lower.Contains("java heap space"))
            reasons.Add("内存不足：尝试提高游戏内存分配，或减少资源包、光影、Mod 数量。");
        if (lower.Contains("unsupportedclassversionerror"))
            reasons.Add("Java 版本不匹配：当前实例可能需要更高或更低版本的 Java。");
        if (lower.Contains("classnotfoundexception") || lower.Contains("noclassdeffounderror"))
            reasons.Add("缺少类或依赖：实例库文件、Mod 前置或加载器可能缺失。");
        if (lower.Contains("mod loading error") || lower.Contains("failed to load mod") || lower.Contains("fabricloader") || lower.Contains("forge mod loader"))
            reasons.Add("Mod 加载失败：检查最近新增或更新的 Mod，并确认前置依赖与游戏版本一致。");
        if (lower.Contains("glfw") || lower.Contains("opengl") || lower.Contains("lwjgl"))
            reasons.Add("图形环境异常：尝试更新显卡驱动，或切换渲染器/关闭光影。");
        if (lower.Contains("access denied") || lower.Contains("另一个程序正在使用此文件") || lower.Contains("being used by another process"))
            reasons.Add("文件被占用或权限不足：关闭占用文件的程序，并检查启动器目录权限。");
        if (lower.Contains("invalid session") || lower.Contains("authentication") || lower.Contains("authlib"))
            reasons.Add("登录或认证异常：重新登录账号，或检查第三方认证服务器配置。");
        if (exitCode is not null && reasons.Count == 0)
            reasons.Add($"游戏进程以非零退出码 {exitCode} 结束，但日志中没有命中常见特征。建议导出日志进一步排查。");
        if (reasons.Count == 0)
            reasons.Add("暂未识别出明确原因。建议导出完整启动日志并检查最新的游戏日志或崩溃报告。");

        return string.Join("\n", reasons.Select(static reason => "- " + reason));
    }
}

