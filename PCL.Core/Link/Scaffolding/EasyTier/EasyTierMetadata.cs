using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using PCL.Core.App;

namespace PCL.Core.Link.Scaffolding.EasyTier;

public static class EasyTierMetadata
{
    public const string CurrentEasyTierVer = "2.5.0";
    private const string GitHubReleaseRoot = "https://github.com/EasyTier/EasyTier/releases/download";
    private const string StaticAssetsRoot = "https://staticassets.naids.com/resources/pclce/static/easytier";
    private const string PysioAssetsRoot = "https://s3.pysio.online/pcl2-ce/static/easytier";

    public static EasyTierPlatformInfo Platform { get; } = ResolveCurrentPlatform();

    public static bool IsPlatformSupported => Platform.IsSupported;

    public static string EasyTierFilePath => Platform.InstallDirectory;

    public static string CoreExecutablePath => Path.Combine(EasyTierFilePath, Platform.CoreExecutableName);

    public static string CliExecutablePath => Path.Combine(EasyTierFilePath, Platform.CliExecutableName);

    public static bool IsInstalled()
    {
        if (!Platform.IsSupported)
            return false;

        foreach (var file in Platform.RequiredFiles)
        {
            if (!File.Exists(Path.Combine(EasyTierFilePath, file)))
                return false;
        }

        return true;
    }

    public static string GetUnsupportedReason()
    {
        if (Platform.IsSupported)
            return string.Empty;

        return $"当前系统或架构暂不支持 EasyTier：{RuntimeInformation.OSDescription} / {RuntimeInformation.OSArchitecture}";
    }

    private static EasyTierPlatformInfo ResolveCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ResolvePlatform("windows", RuntimeInformation.OSArchitecture);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ResolvePlatform("macos", RuntimeInformation.OSArchitecture);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ResolvePlatform("linux", RuntimeInformation.OSArchitecture);

        return EasyTierPlatformInfo.Unsupported(false);
    }

    public static EasyTierPlatformInfo ResolvePlatform(string os, Architecture arch)
    {
        os = os.ToLowerInvariant();
        var packageArch = os switch
        {
            "windows" => arch switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "i686",
                _ => null
            },
            "macos" => arch switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => "aarch64",
                _ => null
            },
            "linux" => arch switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => "aarch64",
                Architecture.Arm => "armv7",
                _ => null
            },
            _ => null
        };

        if (packageArch is null)
            return EasyTierPlatformInfo.Unsupported(os == "windows");

        var platformId = $"easytier-{os}-{packageArch}";
        var archiveName = $"{platformId}-v{CurrentEasyTierVer}.zip";
        var isWindows = os == "windows";
        var executableSuffix = isWindows ? ".exe" : string.Empty;
        var requiredFiles = isWindows
            ? new[] { "easytier-core.exe", "easytier-cli.exe", "Packet.dll" }
            : new[] { "easytier-core", "easytier-cli" };

        return new EasyTierPlatformInfo(
            true,
            platformId,
            archiveName,
            Path.Combine(Paths.SharedLocalData, "EasyTier", CurrentEasyTierVer, platformId),
            "easytier-core" + executableSuffix,
            "easytier-cli" + executableSuffix,
            requiredFiles,
            [
                $"{StaticAssetsRoot}/{archiveName}",
                $"{PysioAssetsRoot}/{archiveName}",
                $"{GitHubReleaseRoot}/v{CurrentEasyTierVer}/{archiveName}"
            ]);
    }
}

public sealed record EasyTierPlatformInfo(
    bool IsSupported,
    string PlatformId,
    string ArchiveName,
    string InstallDirectory,
    string CoreExecutableName,
    string CliExecutableName,
    IReadOnlyList<string> RequiredFiles,
    IReadOnlyList<string> DownloadUrls)
{
    public static EasyTierPlatformInfo Unsupported(bool isWindows) => new(
        false,
        string.Empty,
        string.Empty,
        Path.Combine(Paths.SharedLocalData, "EasyTier", EasyTierMetadata.CurrentEasyTierVer, "unsupported"),
        isWindows ? "easytier-core.exe" : "easytier-core",
        isWindows ? "easytier-cli.exe" : "easytier-cli",
        [],
        []);
}
