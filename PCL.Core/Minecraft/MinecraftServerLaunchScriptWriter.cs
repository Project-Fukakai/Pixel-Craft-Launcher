using System.IO;
using System.Text;

namespace PCL.Core.Minecraft;

public static class MinecraftServerLaunchScriptWriter
{
    public static async Task WriteAsync(string versionFolder, string versionId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(versionFolder);
        var command = $@"""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar ""{versionId}-server.jar"" nogui";
        var powerShellCommand = $@"& ""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar ""{versionId}-server.jar"" nogui";
        var bat = $"""
@echo off
title {versionId} 原版服务端
echo 如果服务端立即停止，请右键编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。
echo 你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。
echo ------------------------------
echo 如果提示 "You need to agree to the EULA in order to run the server"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。
echo ------------------------------
{powerShellCommand}
echo ----------------------
echo 服务端已停止。
pause
""";
        await File.WriteAllTextAsync(
            Path.Combine(versionFolder, "Launch Server.bat"),
            bat.Replace("\n", "\r\n", StringComparison.Ordinal),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);

        var ps = $"""
$Host.UI.RawUI.WindowTitle = "{versionId} 原版服务端"
Write-Host "如果服务端立即停止，请编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。"
Write-Host "你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。"
Write-Host "------------------------------"
Write-Host "如果提示 `"You need to agree to the EULA in order to run the server`"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。"
Write-Host "------------------------------"
{command}
Write-Host "----------------------"
Write-Host "服务端已停止。"
Read-Host "按 Enter 键退出"
""";
        await File.WriteAllTextAsync(
            Path.Combine(versionFolder, "Launch Server.ps1"),
            ps.Replace("\n", "\r\n", StringComparison.Ordinal),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);

        var sh = $"""
#!/usr/bin/env sh
printf '%s\n' '{versionId} 原版服务端'
printf '%s\n' '如果服务端立即停止，请编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java 路径。'
printf '%s\n' '你可以在 PCL 的 [设置 -> 启动选项] 中查看已安装的 java，所需的 java 一般在其中的 bin 文件夹下。'
printf '%s\n' '------------------------------'
printf '%s\n' '如果提示 "You need to agree to the EULA in order to run the server"，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。'
printf '%s\n' '------------------------------'
{command}
printf '%s\n' '----------------------'
printf '%s\n' '服务端已停止。'
printf '%s' '按 Enter 键退出'
read _
""";
        var shPath = Path.Combine(versionFolder, "Launch Server.sh");
        await File.WriteAllTextAsync(shPath, sh, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        TryMakeExecutable(shPath);
    }

    private static void TryMakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                      UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                      UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort permission update.
        }
    }
}
