using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Parser;
public class PeHeaderParser : IJavaParser
{
    private static readonly Dictionary<string, JavaBrandType> _BrandMap = new()
    {
        ["Adoptium"] = JavaBrandType.EclipseTemurin,
        ["Eclipse"] = JavaBrandType.EclipseTemurin,
        ["Temurin"] = JavaBrandType.EclipseTemurin,
        ["Bellsoft"] = JavaBrandType.Liberica,
        ["Microsoft"] = JavaBrandType.Microsoft,
        ["Amazon"] = JavaBrandType.Corretto,
        ["Azul"] = JavaBrandType.Zulu,
        ["IBM"] = JavaBrandType.IBMSemeru,
        ["Oracle"] = JavaBrandType.Oracle,
        ["Tencent"] = JavaBrandType.TencentKona,
        ["OpenJDK"] = JavaBrandType.OpenJDK,
        ["Alibaba"] = JavaBrandType.Dragonwell,
        ["GraalVM"] = JavaBrandType.GraalVmCommunity,
        ["JetBrains"] = JavaBrandType.JetBrains
    };

    public JavaInstallation? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return null;

            LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息");

            if (!OperatingSystem.IsWindows())
                return ParseUnixLike(javaExePath);

            var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
            var fileVersion = NormalizeVersion(versionInfo.FileVersion);
            var companyName = _NormalizeCompanyName(versionInfo);
            var brand = _DetermineBrand(companyName);

            var javaFolder = Path.GetDirectoryName(javaExePath)!;
            var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

            var peData = PEHeaderReader.ReadPEHeader(javaExePath);
            var arch = peData.Machine;
            var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

            return new JavaInstallation(
                javaFolder,
                fileVersion,
                brand,
                arch,
                is64Bit,
                isJre
            );
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[Java] 解析 {javaExePath} 时出错");
            return null;
        }
    }

    private static JavaInstallation? ParseUnixLike(string javaExePath)
    {
        var output = QueryJavaProperties(javaExePath);
        if (string.IsNullOrWhiteSpace(output)) return null;

        var props = ParsePropertyOutput(output);
        var javaHome = props.GetValueOrDefault("java.home");
        var javaFolder = ResolveJavaBinFolder(javaExePath, javaHome);
        var version = NormalizeVersion(
            props.GetValueOrDefault("java.version") ??
            props.GetValueOrDefault("java.runtime.version") ??
            ExtractQuotedVersion(output));
        var vendor = string.Join(' ',
            props.GetValueOrDefault("java.vendor"),
            props.GetValueOrDefault("java.vm.vendor"),
            props.GetValueOrDefault("java.vm.name"),
            output);
        var osArch = props.GetValueOrDefault("os.arch") ?? string.Empty;
        var arch = DetermineMachineType(osArch);
        var isJre = !File.Exists(Path.Combine(javaFolder, "javac"));

        return new JavaInstallation(
            javaFolder,
            version,
            _DetermineBrand(vendor),
            arch,
            Is64BitArchitecture(osArch, arch),
            isJre
        );
    }

    private static string? QueryJavaProperties(string javaExePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = javaExePath,
                Arguments = "-XshowSettings:properties -version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;
            if (!proc.WaitForExit(5000))
            {
                proc.Kill(true);
                return null;
            }

            return proc.StandardOutput.ReadToEnd() + Environment.NewLine + proc.StandardError.ReadToEnd();
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Java", $"执行 Java 版本查询失败：{javaExePath} | {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, string> ParsePropertyOutput(string output)
    {
        var ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                ret[key] = value;
        }
        return ret;
    }

    private static string ResolveJavaBinFolder(string javaExePath, string? javaHome)
    {
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var bin = Path.Combine(javaHome, "bin");
            var java = Path.Combine(bin, "java");
            if (File.Exists(java)) return bin;
        }

        return Path.GetDirectoryName(javaExePath)!;
    }

    private static string? ExtractQuotedVersion(string output)
    {
        const string marker = "version \"";
        var start = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = output.IndexOf('"', start);
        return end > start ? output[start..end] : null;
    }

    private static Version NormalizeVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion)) return new Version(0, 0, 0, 0);

        var normalized = rawVersion.Trim()
            .Trim('"')
            .Replace('_', '.');
        var parts = new List<int>();
        foreach (var part in normalized.Split('.', '-', '+'))
        {
            var digits = new string(part.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length == 0) break;
            parts.Add(int.Parse(digits));
            if (parts.Count == 4) break;
        }

        while (parts.Count < 2) parts.Add(0);
        return parts.Count switch
        {
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            _ => new Version(parts[0], parts[1], parts[2], parts[3])
        };
    }

    private static MachineType DetermineMachineType(string osArch)
    {
        return osArch.ToLowerInvariant() switch
        {
            "x86_64" or "amd64" => MachineType.AMD64,
            "aarch64" or "arm64" => MachineType.ARM64,
            "x86" or "i386" or "i686" => MachineType.I386,
            "arm" or "arm32" => MachineType.ARM,
            _ => MachineType.Unknown
        };
    }

    private static bool Is64BitArchitecture(string osArch, MachineType machineType)
    {
        if (machineType != MachineType.Unknown) return PEHeaderReader.IsMachine64Bit(machineType);
        return osArch.Contains("64", StringComparison.OrdinalIgnoreCase);
    }

    private static string _NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        // 修复 Oracle/OpenJDK 混淆问题
        if (name.Contains("Oracle", StringComparison.OrdinalIgnoreCase) || name == "N/A")
        {
            if ((info.FileDescription?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (info.ProductName?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false))
                return "Oracle";
            return "OpenJDK";
        }
        return name;
    }

    private static JavaBrandType _DetermineBrand(string output)
    {
        if (output.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
            (output.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase) &&
             !output.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase)))
            return JavaBrandType.OpenJDK;

        var match = _BrandMap.Keys
            .FirstOrDefault(k => output.Contains(k, StringComparison.OrdinalIgnoreCase));
        return match != null ? _BrandMap[match] : JavaBrandType.Unknown;
    }
}
