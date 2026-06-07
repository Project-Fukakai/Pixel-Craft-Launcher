using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Java;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Launch;

public enum MinecraftAccountType
{
    Offline,
    Microsoft,
    AuthlibInjector
}

public enum MinecraftLaunchStage
{
    Waiting,
    Precheck,
    SelectJava,
    Account,
    PrepareFiles,
    BuildArguments,
    ExtractNatives,
    PreLaunchCommand,
    StartProcess,
    Finished,
    Failed,
    Cancelled
}

public enum MinecraftLaunchFailureKind
{
    None,
    InvalidInstance,
    MissingJava,
    MissingFiles,
    AccountUnavailable,
    Cancelled,
    ProcessStartFailed,
    Unknown
}

public sealed record MinecraftWindowSize(int Width, int Height);

public sealed record MinecraftAccountSession(
    MinecraftAccountType Type,
    string Name,
    string Uuid,
    string AccessToken,
    string ClientToken,
    string? UserPropertiesJson = null,
    string? AuthServerBaseUrl = null);

public sealed record MinecraftLaunchProgress(
    MinecraftLaunchStage Stage,
    string Message,
    double Progress,
    string? LogLine = null);

public sealed record MinecraftLaunchOptions
{
    public IReadOnlyList<string> ExtraArgs { get; init; } = [];
    public string? SaveBatchPath { get; init; }
    public string? ServerIp { get; init; }
    public string? WorldName { get; init; }
    public bool IsTest { get; init; }
    public bool IsDemo { get; init; }
    public bool UseDownloadRepairStep { get; init; }
    public bool UseJavaWrapper { get; init; } = true;
    public bool UseRetroWrapper { get; init; } = true;
    public bool UseLwjglUnsafeAgent { get; init; } = true;
    public bool UseRendererAgent { get; init; } = true;
    public bool WriteLauncherProfile { get; init; } = true;
    public bool CaptureGameOutput { get; init; } = true;
}

public sealed record MinecraftLaunchRequest(
    MinecraftInstanceInfo Instance,
    IMinecraftAccountProvider AccountProvider,
    IGameWindowContext? WindowContext = null,
    IMinecraftLaunchUiBridge? UiBridge = null,
    MinecraftLaunchOptions? Options = null,
    IMinecraftLaunchFileRepairer? FileRepairer = null);

public sealed record MinecraftLaunchResult(
    bool Success,
    MinecraftLaunchFailureKind FailureKind,
    string Message,
    Process? Process = null,
    string? Arguments = null,
    JavaEntry? Java = null,
    string? BatchPath = null)
{
    public static MinecraftLaunchResult Failed(MinecraftLaunchFailureKind kind, string message) =>
        new(false, kind, message);
}

public interface IMinecraftAccountProvider
{
    Task<MinecraftAccountSession> GetAccountAsync(MinecraftInstanceInfo instance, CancellationToken cancellationToken);
}

public interface IMinecraftLaunchUiBridge
{
    Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken);
    Task NotifyAsync(string message, bool isError, CancellationToken cancellationToken);
    Task OpenPathAsync(string path, CancellationToken cancellationToken);
}

public interface IMinecraftLaunchFileRepairer
{
    Task<bool> RepairAsync(
        MinecraftInstanceInfo instance,
        IProgress<MinecraftLaunchProgress>? progress,
        CancellationToken cancellationToken);
}

public interface IGameWindowContext
{
    Task<MinecraftWindowSize?> GetLauncherWindowSizeAsync(CancellationToken cancellationToken);
    Task ApplyLauncherVisibilityAsync(LauncherVisibility visibility, Process process, CancellationToken cancellationToken);
}

public sealed class OfflineMinecraftAccountProvider(string username) : IMinecraftAccountProvider
{
    public Task<MinecraftAccountSession> GetAccountAsync(MinecraftInstanceInfo instance, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(username) ? "Steve" : username.Trim();
        return Task.FromResult(new MinecraftAccountSession(
            MinecraftAccountType.Offline,
            normalized,
            MinecraftOfflineUuid.Create(normalized),
            "0",
            "0"));
    }
}

public sealed record MinecraftInstanceInfo(
    string Name,
    string MinecraftFolder,
    string VersionDirectory,
    string JsonPath,
    JsonObject Json,
    DateTime ReleaseTime,
    MinecraftVersionNumber? VanillaVersion)
{
    public string GameDirectory => VersionDirectory;
    public string JarPath => Path.Combine(VersionDirectory, $"{Name}.jar");
    public string MainClass => Json["mainClass"]?.GetValue<string>() ?? string.Empty;
    public bool HasArguments => Json["arguments"] is JsonObject args && args["jvm"] is not null;
    public string AssetIndexName => Json["assetIndex"]?["id"]?.GetValue<string>() ?? Json["assets"]?.GetValue<string>() ?? Name;

    public static MinecraftInstanceInfo Load(string jsonPath)
    {
        var versionDirectory = Path.GetDirectoryName(jsonPath) ??
            throw new InvalidOperationException($"无法获取实例目录：{jsonPath}");
        var versionsDirectory = Directory.GetParent(versionDirectory)?.FullName ??
            throw new InvalidOperationException($"无法获取 versions 目录：{jsonPath}");
        var minecraftFolder = Directory.GetParent(versionsDirectory)?.FullName ??
            throw new InvalidOperationException($"无法获取 Minecraft 目录：{jsonPath}");

        var json = JsonNode.Parse(File.ReadAllText(jsonPath))?.AsObject() ??
            throw new InvalidDataException($"实例 JSON 无效：{jsonPath}");
        var name = Path.GetFileNameWithoutExtension(jsonPath);
        var id = json["id"]?.GetValue<string>() ?? name;
        var releaseTime = ParseDate(json["releaseTime"]?.GetValue<string>() ?? json["time"]?.GetValue<string>());
        return new MinecraftInstanceInfo(
            id,
            Path.GetFullPath(minecraftFolder),
            Path.GetFullPath(versionDirectory),
            Path.GetFullPath(jsonPath),
            json,
            releaseTime,
            MinecraftVersionNumber.TryParse(id));
    }

    private static DateTime ParseDate(string? value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result))
            return result;
        return DateTime.MinValue;
    }
}

public readonly record struct MinecraftVersionNumber(int Major, int Minor, int Patch)
    : IComparable<MinecraftVersionNumber>
{
    public static MinecraftVersionNumber? TryParse(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var span = id.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            if (!char.IsDigit(span[i])) continue;
            var start = i;
            while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.')) i++;
            var raw = span[start..i].Trim('.');
            var parts = raw.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) continue;
            var patch = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p : 0;
            return new MinecraftVersionNumber(major, minor, patch);
        }
        return null;
    }

    public bool IsAtLeast(int major, int minor, int patch = 0)
    {
        if (Major != major) return Major > major;
        if (Minor != minor) return Minor > minor;
        return Patch >= patch;
    }

    public int CompareTo(MinecraftVersionNumber other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public bool IsBetween(
        int minMajor,
        int minMinor,
        int minPatch,
        int maxMajor,
        int maxMinor,
        int maxPatch)
    {
        var min = new MinecraftVersionNumber(minMajor, minMinor, minPatch);
        var max = new MinecraftVersionNumber(maxMajor, maxMinor, maxPatch);
        return CompareTo(min) >= 0 && CompareTo(max) <= 0;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record MinecraftJavaRequirement(
    Version Minimum,
    Version Maximum,
    string? RecommendedComponent = null)
{
    public bool Allows(JavaInstallation installation)
    {
        var normalized = NormalizeJavaVersion(installation.Version);
        return normalized >= NormalizeJavaVersion(Minimum) && normalized <= NormalizeJavaVersion(Maximum);
    }

    private static Version NormalizeJavaVersion(Version version) =>
        version.Major == 1 ? new Version(version.Minor, version.Build < 0 ? 0 : version.Build, version.Revision < 0 ? 0 : version.Revision) : version;
}

public static class MinecraftInstanceScanner
{
    public static IReadOnlyList<MinecraftInstanceInfo> ScanDefaultFolders()
    {
        return ScanFolders(GetDefaultMinecraftFolders());
    }

    public static IReadOnlyList<MinecraftInstanceInfo> ScanFolders(IEnumerable<string> minecraftFolders)
    {
        var result = new List<MinecraftInstanceInfo>();
        foreach (var folder in minecraftFolders
                     .Select(NormalizeMinecraftFolder)
                     .Where(static f => !string.IsNullOrWhiteSpace(f))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var versions = Path.Combine(folder, "versions");
            if (!Directory.Exists(versions)) continue;
            foreach (var versionDirectory in Directory.EnumerateDirectories(versions).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(versionDirectory);
                var jsonPath = Path.Combine(versionDirectory, $"{name}.json");
                if (!File.Exists(jsonPath)) continue;
                try
                {
                    result.Add(MinecraftInstanceInfo.Load(jsonPath));
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Launch", $"读取实例失败：{jsonPath}");
                }
            }
        }

        return result
            .GroupBy(static i => i.JsonPath, StringComparer.OrdinalIgnoreCase)
            .Select(static g => g.First())
            .OrderBy(static i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetDefaultMinecraftFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new List<string>
        {
            Path.Combine(Basics.ExecutableDirectory, ".minecraft")
        };

        if (!string.IsNullOrWhiteSpace(appData))
            candidates.Add(Path.Combine(appData, ".minecraft"));

        if (OperatingSystem.IsMacOS())
            candidates.Add(Path.Combine(home, "Library", "Application Support", "minecraft"));
        else if (!string.IsNullOrWhiteSpace(home))
            candidates.Add(Path.Combine(home, ".minecraft"));

        try
        {
            var configured = States.Game.Folders;
            foreach (var folder in configured.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(folder.Replace("$", Basics.ExecutableDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            }
        }
        catch
        {
            // Config may not be initialized in tests or early tooling.
        }

        return candidates
            .Select(NormalizeMinecraftFolder)
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeMinecraftFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return string.Empty;

        return Path.GetFullPath(folder.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

public static class MinecraftJavaRequirementResolver
{
    public static MinecraftJavaRequirement Resolve(MinecraftInstanceInfo instance)
    {
        var min = new Version(0, 0, 0, 0);
        var max = new Version(999, 999, 999, 999);
        string? recommendedComponent = null;

        var javaVersion = GetInt(instance.Json["javaVersion"]?["majorVersion"]) ??
                          GetInt(instance.Json["java_version"]);
        if (javaVersion is >= 22)
        {
            min = Max(min, new Version(javaVersion.Value, 0, 0, 0));
            recommendedComponent = instance.Json["javaVersion"]?["component"]?.GetValue<string>() ??
                                   instance.Json["java_component"]?.GetValue<string>();
        }
        else if (instance.VanillaVersion is { } version)
        {
            if (version.IsAtLeast(1, 20, 5)) min = Max(min, new Version(21, 0, 0, 0));
            else if (version.IsAtLeast(1, 18)) min = Max(min, new Version(17, 0, 0, 0));
            else if (version.IsAtLeast(1, 17)) min = Max(min, new Version(16, 0, 0, 0));
            else if (version.IsAtLeast(1, 12)) min = Max(min, new Version(8, 0, 0, 0));
            else if (version.Major == 1 && version.Minor <= 5) max = Min(max, new Version(8, 999, 999, 999));
        }
        else if (instance.ReleaseTime >= new DateTime(2024, 4, 2)) min = Max(min, new Version(21, 0, 0, 0));
        else if (instance.ReleaseTime >= new DateTime(2021, 11, 16)) min = Max(min, new Version(17, 0, 0, 0));
        else if (instance.ReleaseTime >= new DateTime(2021, 5, 11)) min = Max(min, new Version(16, 0, 0, 0));
        else if (instance.ReleaseTime.Year >= 2017) min = Max(min, new Version(8, 0, 0, 0));

        var libraryNames = GetLibraryNames(instance).ToArray();
        var hasForge = libraryNames.Any(static n => n.Contains("forge", StringComparison.OrdinalIgnoreCase));
        var hasOptiFine = libraryNames.Any(static n => n.Contains("optifine", StringComparison.OrdinalIgnoreCase));
        var hasFabric = libraryNames.Any(static n => n.Contains("fabric-loader", StringComparison.OrdinalIgnoreCase));
        var hasLiteLoader = libraryNames.Any(static n => n.Contains("liteloader", StringComparison.OrdinalIgnoreCase));
        var hasCleanroom = libraryNames.Any(static n => n.Contains("cleanroom", StringComparison.OrdinalIgnoreCase));
        var hasLabyMod = libraryNames.Any(static n => n.Contains("labymod", StringComparison.OrdinalIgnoreCase));
        var cleanroomVersion = TryGetLibraryVersion(libraryNames, "cleanroom");

        if (hasOptiFine && instance.VanillaVersion is { } optiFineVersion)
        {
            if (optiFineVersion.Major == 1 && optiFineVersion.Minor < 7)
                max = Min(max, new Version(8, 999, 999, 999));
            else if (optiFineVersion.Major == 1 && optiFineVersion.Minor is >= 8 and <= 12)
            {
                min = Max(min, new Version(8, 0, 0, 0));
                max = Min(max, new Version(8, 999, 999, 999));
            }
        }

        if (hasForge && instance.VanillaVersion is { } forgeVersion)
        {
            if (forgeVersion.IsBetween(1, 6, 1, 1, 7, 2))
            {
                min = Max(min, new Version(7, 0, 0, 0));
                max = Min(max, new Version(7, 999, 999, 999));
            }
            else if (forgeVersion.Major == 1 && forgeVersion.Minor <= 12)
                max = Min(max, new Version(8, 999, 999, 999));
            else if (forgeVersion.Major == 1 && forgeVersion.Minor is >= 13 and <= 14)
            {
                min = Max(min, new Version(8, 0, 0, 0));
                max = Min(max, new Version(10, 999, 999, 999));
            }
            else if (forgeVersion.Major == 1 && forgeVersion.Minor == 15)
            {
                min = Max(min, new Version(8, 0, 0, 0));
                max = Min(max, new Version(15, 999, 999, 999));
            }
            else if (forgeVersion.Major == 1 && forgeVersion.Minor == 18 && hasOptiFine)
            {
                max = Min(max, new Version(18, 999, 999, 999));
            }
        }

        if (hasFabric && instance.VanillaVersion is { } fabricVersion)
        {
            if (fabricVersion.Major == 1 && fabricVersion.Minor is >= 15 and <= 16)
                min = Max(min, new Version(8, 0, 0, 0));
            else if (fabricVersion.IsAtLeast(1, 18))
                min = Max(min, new Version(17, 0, 0, 0));
        }

        if (hasLiteLoader) max = Min(max, new Version(8, 999, 999, 999));
        if (hasCleanroom)
            min = Max(min, cleanroomVersion is not null && cleanroomVersion >= new Version(0, 5, 0)
                ? new Version(25, 0, 0, 0)
                : new Version(21, 0, 0, 0));
        if (hasLabyMod)
        {
            min = Max(min, new Version(21, 0, 0, 0));
            max = new Version(999, 999, 999, 999);
        }

        if (javaVersion is > 0 and <= 21)
            min = Max(min, new Version(javaVersion.Value, 0, 0, 0));

        if (max < min) max = new Version(999, 999, 999, 999);
        return new MinecraftJavaRequirement(min, max, string.IsNullOrWhiteSpace(recommendedComponent) ? null : recommendedComponent);
    }

    private static IEnumerable<string> GetLibraryNames(MinecraftInstanceInfo instance)
    {
        if (instance.Json["libraries"] is not JsonArray libraries) yield break;
        foreach (var library in libraries.OfType<JsonObject>())
        {
            var name = library["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name)) yield return name;
        }
    }

    private static int? GetInt(JsonNode? node)
    {
        if (node is null) return null;
        if (node.GetValueKind() == System.Text.Json.JsonValueKind.Number && node.GetValue<int>() is { } number) return number;
        return int.TryParse(node.ToString(), out var parsed) ? parsed : null;
    }

    private static Version? TryGetLibraryVersion(IEnumerable<string> libraryNames, string artifactNeedle)
    {
        foreach (var name in libraryNames)
        {
            if (!name.Contains(artifactNeedle, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = name.Split(':');
            if (parts.Length < 3) continue;
            var raw = parts[2].Split('-', 2)[0];
            if (Version.TryParse(raw, out var version)) return version;
        }

        return null;
    }

    private static Version Max(Version a, Version b) => a >= b ? a : b;
    private static Version Min(Version a, Version b) => a <= b ? a : b;
}

public sealed class MinecraftLaunchService
{
    private const string ModuleName = "Launch";

    public async Task<MinecraftLaunchResult> LaunchAsync(
        MinecraftLaunchRequest request,
        IProgress<MinecraftLaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var options = request.Options ?? new MinecraftLaunchOptions();
        MinecraftAccountSession? currentAccount = null;
        try
        {
            Report(MinecraftLaunchStage.Precheck, "检查实例", 0.05);
            var precheck = Precheck(request.Instance);
            if (precheck is not null) return Fail(MinecraftLaunchFailureKind.InvalidInstance, precheck);
            cancellationToken.ThrowIfCancellationRequested();

            Report(MinecraftLaunchStage.SelectJava, "选择 Java", 0.16);
            var java = await SelectJavaAsync(request.Instance, cancellationToken).ConfigureAwait(false);
            if (java is null)
            {
                var requirement = MinecraftJavaRequirementResolver.Resolve(request.Instance);
                return Fail(MinecraftLaunchFailureKind.MissingJava,
                    $"未找到满足要求的 Java：最低 {requirement.Minimum}，最高 {requirement.Maximum}");
            }
            Log($"选择的 Java：{java}");
            cancellationToken.ThrowIfCancellationRequested();

            Report(MinecraftLaunchStage.Account, "获取登录档案", 0.28);
            var account = await request.AccountProvider.GetAccountAsync(request.Instance, cancellationToken).ConfigureAwait(false);
            currentAccount = account;
            if (account.Type != MinecraftAccountType.Offline && string.IsNullOrWhiteSpace(account.AccessToken))
                return Fail(MinecraftLaunchFailureKind.AccountUnavailable, "登录档案缺少 AccessToken。");
            cancellationToken.ThrowIfCancellationRequested();

            Report(MinecraftLaunchStage.PrepareFiles, "检查启动文件", 0.4);
            var fileCheck = ValidateLaunchFiles(request.Instance);
            if (fileCheck is not null && options.UseDownloadRepairStep && request.FileRepairer is not null)
            {
                Report(MinecraftLaunchStage.PrepareFiles, "补全启动文件", 0.42, "检测到缺失文件，正在调用文件补全步骤。");
                var repaired = await request.FileRepairer.RepairAsync(request.Instance, progress, cancellationToken).ConfigureAwait(false);
                if (repaired) fileCheck = ValidateLaunchFiles(request.Instance);
            }
            if (fileCheck is not null) return Fail(MinecraftLaunchFailureKind.MissingFiles, fileCheck);

            Report(MinecraftLaunchStage.BuildArguments, "生成启动参数", 0.55);
            var plan = await BuildLaunchPlanAsync(request, options, java, account, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            Report(MinecraftLaunchStage.ExtractNatives, "解压 natives", 0.7);
            ExtractNatives(plan.NativeLibraries, plan.NativesDirectory);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.WriteLauncherProfile)
                WriteLauncherProfile(request.Instance, account, message => Report(MinecraftLaunchStage.PrepareFiles, message, 0.78, message));
            cancellationToken.ThrowIfCancellationRequested();

            Report(MinecraftLaunchStage.PreLaunchCommand, "执行预启动命令", 0.82);
            await RunPreLaunchCommandsAsync(request.Instance, options, plan.ArgumentString, java, account, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(options.SaveBatchPath))
            {
                WriteBatch(options.SaveBatchPath!, java, request.Instance, plan.ArgumentString, account);
                await NotifyAsync(request, $"启动脚本已导出：{options.SaveBatchPath}", false, cancellationToken).ConfigureAwait(false);
                return new MinecraftLaunchResult(true, MinecraftLaunchFailureKind.None, "启动脚本已导出。", Arguments: plan.ArgumentString, Java: java, BatchPath: options.SaveBatchPath);
            }

            Report(MinecraftLaunchStage.StartProcess, "启动游戏进程", 0.9);
            var process = StartProcess(request.Instance, java, plan.ArgumentString, options.CaptureGameOutput);
            if (options.CaptureGameOutput)
                StartGameOutputCapture(process, progress, account);
            ApplyPriority(process);
            if (request.WindowContext is not null)
                await request.WindowContext.ApplyLauncherVisibilityAsync(Config.Launch.LauncherVisibility, process, cancellationToken).ConfigureAwait(false);

            States.System.LaunchCount += 1;
            States.Instance.LaunchCount[request.Instance.VersionDirectory] += 1;
            Report(MinecraftLaunchStage.Finished, "游戏进程已启动", 1);
            await NotifyAsync(request, $"{request.Instance.Name} 启动成功！", false, cancellationToken).ConfigureAwait(false);
            return new MinecraftLaunchResult(true, MinecraftLaunchFailureKind.None, "游戏进程已启动。", process, plan.ArgumentString, java);
        }
        catch (OperationCanceledException)
        {
            Report(MinecraftLaunchStage.Cancelled, "启动已取消", 1);
            return MinecraftLaunchResult.Failed(MinecraftLaunchFailureKind.Cancelled, "启动已取消。");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, "启动失败");
            Report(MinecraftLaunchStage.Failed, "启动失败", 1, ex.Message);
            return MinecraftLaunchResult.Failed(MinecraftLaunchFailureKind.Unknown, ex.Message);
        }

        MinecraftLaunchResult Fail(MinecraftLaunchFailureKind kind, string message)
        {
            Report(MinecraftLaunchStage.Failed, message, 1, message);
            return MinecraftLaunchResult.Failed(kind, message);
        }

        void Report(MinecraftLaunchStage stage, string message, double value, string? log = null)
        {
            var safeMessage = FilterSensitive(message, currentAccount);
            var safeLog = FilterSensitive(string.IsNullOrWhiteSpace(log) ? message : log, currentAccount);
            progress?.Report(new MinecraftLaunchProgress(stage, safeMessage, Math.Clamp(value, 0, 1), safeLog));
            Log(safeLog, currentAccount);
        }
    }

    public async Task<MinecraftLaunchPlan> BuildLaunchPlanAsync(
        MinecraftLaunchRequest request,
        MinecraftLaunchOptions options,
        JavaEntry java,
        MinecraftAccountSession account,
        CancellationToken cancellationToken = default)
    {
        var instance = request.Instance;
        var nativesDirectory = GetNativesDirectory(instance);
        var libraries = GetLibraries(instance).ToArray();
        var nativeLibraries = libraries.Where(static l => l.IsNative).ToArray();
        var resources = PrepareLaunchResources(instance, options, java, libraries);
        var classpath = BuildClasspath(instance, libraries.Where(static l => !l.IsNative), resources);
        var replacements = await BuildReplacementsAsync(request, options, account, classpath, nativesDirectory, cancellationToken).ConfigureAwait(false);
        var launchTitle = ResolveLaunchTitle(instance, account);

        var jvmArgs = BuildJvmArguments(instance, options, java, resources, replacements, account);
        var gameArgs = BuildGameArguments(instance, options, resources, replacements);
        var allArgs = jvmArgs.Concat(gameArgs).Concat(options.ExtraArgs.Select(static a => a.Trim()).Where(static a => a.Length > 0)).ToArray();
        var argumentString = string.Join(" ", allArgs.Select(QuoteArgument));
        return new MinecraftLaunchPlan(argumentString, nativesDirectory, nativeLibraries, classpath, launchTitle, jvmArgs, gameArgs, resources);
    }

    public static double GetConfiguredMemoryGb(MinecraftInstanceInfo instance, bool is32BitJava)
    {
        return CalculateConfiguredMemoryGb(instance, is32BitJava, forceGlobal: false);
    }

    public static double GetGlobalConfiguredMemoryGb(MinecraftInstanceInfo? instance, bool is32BitJava)
    {
        return CalculateConfiguredMemoryGb(instance, is32BitJava, forceGlobal: true);
    }

    private static double CalculateConfiguredMemoryGb(MinecraftInstanceInfo? instance, bool is32BitJava, bool forceGlobal)
    {
        double ramGive;
        var useGlobal = forceGlobal || instance is null;
        var mode = useGlobal ? Config.Launch.MemoryAllocationMode : Config.Instance.MemorySolution[instance!.VersionDirectory];
        useGlobal = useGlobal || mode == 2;
        if (useGlobal)
            mode = Config.Launch.MemoryAllocationMode;

        if (mode == 0)
        {
            var available = KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024d / 1024d / 1024d;
            var modCount = instance is not null && Directory.Exists(Path.Combine(instance.GameDirectory, "mods"))
                ? Directory.EnumerateFiles(Path.Combine(instance.GameDirectory, "mods")).Count()
                : 0;
            MinecraftLibraryFile[] libraries = instance is null ? [] : GetLibraries(instance).ToArray();
            var modable = modCount > 0 || libraries.Any(static l =>
                l.OriginalName.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
                l.OriginalName.Contains("fabric-loader", StringComparison.OrdinalIgnoreCase));
            var optiFine = libraries.Any(static l => l.OriginalName.Contains("optifine", StringComparison.OrdinalIgnoreCase));

            double min;
            double target1;
            double target2;
            double target3;
            if (modable)
            {
                min = 0.5d + modCount / 150d;
                target1 = 1.5d + modCount / 90d;
                target2 = 2.7d + modCount / 50d;
                target3 = 4.5d + modCount / 25d;
            }
            else if (optiFine)
            {
                min = 0.5d;
                target1 = 1.5d;
                target2 = 3d;
                target3 = 5d;
            }
            else
            {
                min = 0.5d;
                target1 = 1.5d;
                target2 = 2.5d;
                target3 = 4d;
            }

            ramGive = AllocateMemory(available, target1, target2, target3);
            ramGive = Math.Round(Math.Max(ramGive, min), 1);
        }
        else
        {
            var value = useGlobal ? Config.Launch.CustomMemorySize : Config.Instance.CustomMemorySize[instance!.VersionDirectory];
            ramGive = value switch
            {
                <= 12 => value * 0.1d + 0.3d,
                <= 25 => (value - 12) * 0.5d + 1.5d,
                <= 33 => value - 25 + 8,
                _ => (value - 33) * 2 + 16
            };
        }

        return is32BitJava ? Math.Min(1d, ramGive) : ramGive;
    }

    private static double AllocateMemory(double available, double target1, double target2, double target3)
    {
        var give = 0d;
        Add(target1, 1d);
        Add(target2 - target1, 0.7d);
        Add(target3 - target2, 0.4d);
        Add(target3, 0.15d);
        return give;

        void Add(double delta, double ratio)
        {
            if (available < 0.1d) return;
            give += Math.Min(available * ratio, delta);
            available -= delta / ratio;
        }
    }

    private static string? Precheck(MinecraftInstanceInfo instance)
    {
        if (string.IsNullOrWhiteSpace(instance.MainClass)) return "实例 JSON 中没有 mainClass 项。";
        if (instance.GameDirectory.Contains('!') || instance.GameDirectory.Contains(';'))
            return $"游戏路径中不可包含 ! 或 ;：{instance.GameDirectory}";
        if (!File.Exists(instance.JsonPath)) return $"实例 JSON 不存在：{instance.JsonPath}";
        return null;
    }

    private static string? ValidateLaunchFiles(MinecraftInstanceInfo instance)
    {
        if (!File.Exists(instance.JarPath)) return $"缺少游戏核心文件：{instance.JarPath}";
        var missing = GetLibraries(instance)
            .Where(static lib => !File.Exists(lib.LocalPath))
            .Select(static lib => lib.LocalPath)
            .Take(5)
            .ToArray();
        if (missing.Length == 0) return null;
        return "实例缺少启动所需文件，请先补全或下载：\n" + string.Join('\n', missing);
    }

    private static async Task<JavaEntry?> SelectJavaAsync(MinecraftInstanceInfo instance, CancellationToken cancellationToken)
    {
        var requirement = MinecraftJavaRequirementResolver.Resolve(instance);
        await JavaService.JavaManager.ScanJavaAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(Config.Launch.SelectedJava))
        {
            var selected = JavaService.JavaManager.Get(Config.Launch.SelectedJava);
            if (selected is { IsEnabled: true } && selected.Installation.IsStillAvailable && requirement.Allows(selected.Installation))
                return selected;
        }

        return JavaService.JavaManager.GetSortedJavaList()
            .Where(static java => java.IsEnabled && java.Installation.IsStillAvailable)
            .Where(java => requirement.Allows(java.Installation))
            .OrderByDescending(static java => java.Installation.Is64Bit)
            .ThenBy(static java => java.Installation.IsJre)
            .ThenBy(java => Math.Abs(java.Installation.MajorVersion - requirement.Minimum.Major))
            .ThenByDescending(static java => java.Installation.Version)
            .FirstOrDefault();
    }

    private static async Task<Dictionary<string, string>> BuildReplacementsAsync(
        MinecraftLaunchRequest request,
        MinecraftLaunchOptions options,
        MinecraftAccountSession account,
        string classpath,
        string nativesDirectory,
        CancellationToken cancellationToken)
    {
        var instance = request.Instance;
        var windowSize = await GetWindowSizeAsync(request, cancellationToken).ConfigureAwait(false);
        var versionType = Config.Instance.TypeInfo[instance.VersionDirectory];
        if (string.IsNullOrWhiteSpace(versionType)) versionType = Config.Launch.TypeInfo;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["${classpath_separator}"] = Path.PathSeparator.ToString(),
            ["${natives_directory}"] = nativesDirectory,
            ["${library_directory}"] = Path.Combine(instance.MinecraftFolder, "libraries"),
            ["${libraries_directory}"] = Path.Combine(instance.MinecraftFolder, "libraries"),
            ["${launcher_name}"] = "PCLCE",
            ["${launcher_version}"] = Basics.VersionCode.ToString(CultureInfo.InvariantCulture),
            ["${version_name}"] = instance.Name,
            ["${version_type}"] = versionType ?? string.Empty,
            ["${game_directory}"] = instance.GameDirectory,
            ["${assets_root}"] = Path.Combine(instance.MinecraftFolder, "assets"),
            ["${game_assets}"] = Path.Combine(instance.MinecraftFolder, "assets", "virtual", "legacy"),
            ["${assets_index_name}"] = instance.AssetIndexName,
            ["${user_properties}"] = account.UserPropertiesJson ?? "{}",
            ["${auth_player_name}"] = account.Name,
            ["${auth_uuid}"] = account.Uuid,
            ["${auth_access_token}"] = account.AccessToken,
            ["${access_token}"] = account.AccessToken,
            ["${auth_session}"] = account.AccessToken,
            ["${clientid}"] = account.ClientToken,
            ["${auth_xuid}"] = "0",
            ["${user_type}"] = account.Type == MinecraftAccountType.Microsoft ? "msa" : "legacy",
            ["${resolution_width}"] = windowSize.Width.ToString(CultureInfo.InvariantCulture),
            ["${resolution_height}"] = windowSize.Height.ToString(CultureInfo.InvariantCulture),
            ["${classpath}"] = classpath
        };

        static async Task<MinecraftWindowSize> GetWindowSizeAsync(MinecraftLaunchRequest request, CancellationToken ct)
        {
            if (Config.Launch.GameWindowMode == GameWindowSizeMode.Launcher && request.WindowContext is not null)
            {
                var size = await request.WindowContext.GetLauncherWindowSizeAsync(ct).ConfigureAwait(false);
                if (size is not null) return size;
            }

            if (Config.Launch.GameWindowMode == GameWindowSizeMode.Custom)
                return new MinecraftWindowSize(Math.Max(100, Config.Launch.GameWindowWidth), Math.Max(100, Config.Launch.GameWindowHeight));

            return new MinecraftWindowSize(854, 480);
        }
    }

    private static MinecraftLaunchResources PrepareLaunchResources(
        MinecraftInstanceInfo instance,
        MinecraftLaunchOptions options,
        JavaEntry java,
        IReadOnlyList<MinecraftLibraryFile> libraries)
    {
        var resourceDirectory = MinecraftLaunchResourceService.ResourceDirectory;
        var usesRetroWrapper = NeedsRetroWrapper(instance, options);
        var usesLwjglUnsafeAgent = UsesLwjglUnsafeAgent(instance, options, libraries);
        var usesJavaWrapper = UsesJavaWrapper(instance, options, java);
        var rendererMode = ResolveRendererMode(instance, options);

        string? javaWrapperPath = null;
        string? retroWrapperPath = null;
        string? lwjglUnsafeAgentPath = null;
        string? rendererAgentPath = null;

        if (usesJavaWrapper)
            javaWrapperPath = MinecraftLaunchResourceService.ExtractEmbeddedJar("java-wrapper.jar");

        if (usesRetroWrapper)
            retroWrapperPath = MinecraftLaunchResourceService.ExtractEmbeddedJar("retro-wrapper.jar");

        if (usesLwjglUnsafeAgent)
            lwjglUnsafeAgentPath = MinecraftLaunchResourceService.ExtractEmbeddedJar("lwjgl-unsafe-agent.jar");

        if (rendererMode != 0)
        {
            rendererAgentPath = MinecraftLaunchResourceService.ResolveRendererAgentPath();
            if (rendererAgentPath is null)
            {
                Log("已启用渲染器代理，但未找到 mesa-loader-windows/25.3.5/Loader.jar，已跳过。");
                rendererMode = 0;
            }
        }

        return new MinecraftLaunchResources(
            resourceDirectory,
            javaWrapperPath,
            retroWrapperPath,
            lwjglUnsafeAgentPath,
            rendererAgentPath,
            usesJavaWrapper,
            usesRetroWrapper,
            usesLwjglUnsafeAgent,
            rendererMode);
    }

    private static bool NeedsRetroWrapper(MinecraftInstanceInfo instance, MinecraftLaunchOptions options)
    {
        if (!options.UseRetroWrapper || Config.Launch.DisableRw || Config.Instance.DisableRw[instance.VersionDirectory])
            return false;

        if (instance.VanillaVersion is { } version)
            return version.Major == 1 && version.Minor < 6;

        return instance.ReleaseTime != DateTime.MinValue && instance.ReleaseTime < new DateTime(2013, 6, 25);
    }

    private static bool UsesLwjglUnsafeAgent(
        MinecraftInstanceInfo instance,
        MinecraftLaunchOptions options,
        IEnumerable<MinecraftLibraryFile> libraries)
    {
        if (!options.UseLwjglUnsafeAgent ||
            Config.Launch.DisableLwjglUnsafeAgent ||
            Config.Instance.DisableLwjglUnsafeAgent[instance.VersionDirectory])
        {
            return false;
        }

        return libraries.Any(static library =>
        {
            var parts = library.OriginalName.Split(':');
            return parts.Length >= 3 &&
                   parts[0].Equals("org.lwjgl", StringComparison.OrdinalIgnoreCase) &&
                   parts[1].Equals("lwjgl", StringComparison.OrdinalIgnoreCase) &&
                   parts[2].Equals("3.4.1", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool UsesJavaWrapper(MinecraftInstanceInfo instance, MinecraftLaunchOptions options, JavaEntry java)
    {
        return options.UseJavaWrapper &&
               IsUtf8CodePage() &&
               !Config.Launch.DisableJlw &&
               !Config.Instance.DisableJlw[instance.VersionDirectory] &&
               java.Installation.MajorVersion >= 8;
    }

    private static int ResolveRendererMode(MinecraftInstanceInfo instance, MinecraftLaunchOptions options)
    {
        if (!options.UseRendererAgent || !OperatingSystem.IsWindows()) return 0;
        var instanceRenderer = Config.Instance.Renderer[instance.VersionDirectory];
        var renderer = instanceRenderer != 0 ? instanceRenderer - 1 : Config.Launch.Renderer;
        return renderer <= 0 ? 0 : renderer;
    }

    private static bool IsUtf8CodePage() => Encoding.Default.CodePage == 65001;

    private static IReadOnlyList<string> BuildJvmArguments(
        MinecraftInstanceInfo instance,
        MinecraftLaunchOptions options,
        JavaEntry java,
        MinecraftLaunchResources resources,
        Dictionary<string, string> replacements,
        MinecraftAccountSession account)
    {
        var args = new List<string>();
        var customJvm = Config.Instance.JvmArgs[instance.VersionDirectory];
        if (string.IsNullOrWhiteSpace(customJvm)) customJvm = Config.Launch.JvmArgs;
        args.AddRange(SplitCommandLine(customJvm));
        args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");

        var memoryGb = GetConfiguredMemoryGb(instance, !java.Installation.Is64Bit);
        args.Add($"-Xmn{Math.Floor(memoryGb * 1024d * 0.15d)}m");
        args.Add($"-Xmx{Math.Floor(memoryGb * 1024d)}m");
        if (!args.Any(static arg => arg.Contains("-Dlog4j2.formatMsgNoLookups=true", StringComparison.Ordinal)))
            args.Add("-Dlog4j2.formatMsgNoLookups=true");
        if (java.Installation.MajorVersion > 8)
        {
            if (!args.Any(static arg => arg.StartsWith("-Dstdout.encoding=", StringComparison.Ordinal)))
                args.Insert(0, "-Dstdout.encoding=UTF-8");
            if (!args.Any(static arg => arg.StartsWith("-Dstderr.encoding=", StringComparison.Ordinal)))
                args.Insert(0, "-Dstderr.encoding=UTF-8");
        }

        if (java.Installation.MajorVersion >= 18 &&
            !args.Any(static arg => arg.StartsWith("-Dfile.encoding=", StringComparison.Ordinal)))
        {
            args.Insert(0, "-Dfile.encoding=COMPAT");
        }

        switch (Config.Launch.PreferredIpStack)
        {
            case JvmPreferredIpStack.PreferV4:
                args.Add("-Djava.net.preferIPv4Stack=true");
                args.Add("-Djava.net.preferIPv4Addresses=true");
                break;
            case JvmPreferredIpStack.PreferV6:
                args.Add("-Djava.net.preferIPv6Stack=true");
                args.Add("-Djava.net.preferIPv6Addresses=true");
                break;
        }

        AddProxyArguments(instance, args);

        if (resources.UsesLwjglUnsafeAgent && resources.LwjglUnsafeAgentPath is not null)
            args.Insert(0, "-javaagent:" + resources.LwjglUnsafeAgentPath);

        if (resources.RendererMode != 0 && resources.RendererAgentPath is not null)
            args.Insert(0, "-javaagent:" + resources.RendererAgentPath + "=" + ResolveRendererName(resources.RendererMode));

        if (account.Type == MinecraftAccountType.AuthlibInjector && !string.IsNullOrWhiteSpace(account.AuthServerBaseUrl))
        {
            var authlibPath = EnsureAuthlibInjector();
            args.Insert(0, $"-javaagent:\"{authlibPath}\"={account.AuthServerBaseUrl}");
            args.Insert(1, "-Dauthlibinjector.side=client");
        }

        if (instance.HasArguments)
        {
            args.AddRange(ReadArgumentArray(instance, "jvm", MinecraftArgumentFeatures.None).Select(arg => ApplyReplacements(arg, replacements)));
        }
        else
        {
            args.Add("-Djava.library.path=${natives_directory}");
            args.Add("-cp");
            args.Add("${classpath}");
        }

        if (Config.Instance.UseDebugLof4j2Config[instance.VersionDirectory])
        {
            var log4j = instance.ReleaseTime.Year >= 2017
                ? Utils.LaunchEnvUtils.ExtractDebugLog4j2Config()
                : Utils.LaunchEnvUtils.ExtractLegacyDebugLog4j2Config();
            args.Insert(0, $"-Dlog4j.configurationFile={log4j}");
        }

        if (resources.UsesRetroWrapper)
            args.Add("-Dretrowrapper.doUpdateCheck=false");

        if (resources.UsesJavaWrapper && resources.JavaWrapperPath is not null)
        {
            if (java.Installation.MajorVersion >= 9)
            {
                args.Add("--add-exports");
                args.Add("cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            }

            args.Add("-Doolloo.jlw.tmpdir=" + resources.ResourceDirectory);
            args.Add("-jar");
            args.Add(resources.JavaWrapperPath);
        }

        if (instance.MainClass.Length == 0) throw new InvalidDataException("实例 JSON 中没有 mainClass 项。");
        args.Add(instance.MainClass);
        return args.Select(arg => ApplyReplacements(arg, replacements)).Where(static arg => !string.IsNullOrWhiteSpace(arg)).ToArray();
    }

    private static string ResolveRendererName(int rendererMode) =>
        rendererMode switch
        {
            1 => "llvmpipe",
            2 => "d3d12",
            _ => "zink"
        };

    private static string EnsureAuthlibInjector()
    {
        var path = Path.Combine(Paths.Temp, "authlib-injector.jar");
        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        Directory.CreateDirectory(Paths.Temp);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var metaText = client.GetStringAsync("https://authlib-injector.yushi.moe/artifact/latest.json")
                .GetAwaiter()
                .GetResult();
            var meta = JsonNode.Parse(metaText);
            var url = meta?["download_url"]?.GetValue<string>() ??
                      meta?["url"]?.GetValue<string>() ??
                      string.Empty;
            if (url.IsNullOrEmpty())
                throw new InvalidDataException("Authlib-Injector 最新版本信息中没有下载地址。");
            var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("无法准备 Authlib-Injector，第三方验证档案不能启动。", ex);
        }
    }

    private static IReadOnlyList<string> BuildGameArguments(
        MinecraftInstanceInfo instance,
        MinecraftLaunchOptions options,
        MinecraftLaunchResources resources,
        Dictionary<string, string> replacements)
    {
        var args = new List<string>();
        if (instance.Json["minecraftArguments"] is JsonNode oldArgs && !string.IsNullOrWhiteSpace(oldArgs.GetValue<string>()))
        {
            if (resources.UsesRetroWrapper)
            {
                args.Add("--tweakClass");
                args.Add("com.zero.retrowrapper.RetroTweaker");
            }

            args.AddRange(SplitCommandLine(oldArgs.GetValue<string>()));
            if (!args.Contains("--height", StringComparer.Ordinal) && !args.Contains("--width", StringComparer.Ordinal))
            {
                args.Add("--height");
                args.Add("${resolution_height}");
                args.Add("--width");
                args.Add("${resolution_width}");
            }
        }

        if (instance.HasArguments)
            args.AddRange(ReadArgumentArray(instance, "game", MinecraftArgumentFeatures.FromOptions(options)));

        if (Config.Launch.GameWindowMode == GameWindowSizeMode.Fullscreen && !args.Contains("--fullscreen"))
            args.Add("--fullscreen");
        if (options.IsDemo && !args.Contains("--demo"))
            args.Add("--demo");

        var customGame = Config.Instance.GameArgs[instance.VersionDirectory];
        if (string.IsNullOrWhiteSpace(customGame)) customGame = Config.Launch.GameArgs;
        args.AddRange(SplitCommandLine(customGame));

        if (!string.IsNullOrWhiteSpace(options.WorldName))
        {
            args.Add("--quickPlaySingleplayer");
            args.Add(options.WorldName!);
        }
        else if (!string.IsNullOrWhiteSpace(options.ServerIp))
        {
            if (instance.ReleaseTime > new DateTime(2023, 4, 4))
            {
                args.Add("--quickPlayMultiplayer");
                args.Add(options.ServerIp!);
            }
            else
            {
                var parts = options.ServerIp!.Split(':', 2);
                args.Add("--server");
                args.Add(parts[0]);
                args.Add("--port");
                args.Add(parts.Length == 2 ? parts[1] : "25565");
            }
        }
        else
        {
            var configuredServer = Config.Instance.ServerToEnter[instance.VersionDirectory];
            if (!string.IsNullOrWhiteSpace(configuredServer))
            {
                if (instance.ReleaseTime > new DateTime(2023, 4, 4))
                {
                    args.Add("--quickPlayMultiplayer");
                    args.Add(configuredServer);
                }
                else
                {
                    var parts = configuredServer.Split(':', 2);
                    args.Add("--server");
                    args.Add(parts[0]);
                    args.Add("--port");
                    args.Add(parts.Length == 2 ? parts[1] : "25565");
                }
            }
        }

        return ApplyAndFilterGameArguments(args, replacements);
    }

    private static IEnumerable<string> ReadArgumentArray(
        MinecraftInstanceInfo instance,
        string key,
        MinecraftArgumentFeatures features)
    {
        if (instance.Json["arguments"]?[key] is not JsonArray arguments) yield break;
        foreach (var item in arguments)
        {
            if (item is null) continue;
            if (item.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                yield return item.GetValue<string>();
                continue;
            }

            if (item is not JsonObject obj || !IsRuleAllowed(obj["rules"], features)) continue;
            if (obj["value"] is JsonArray values)
            {
                foreach (var value in values)
                    if (value is not null) yield return value.GetValue<string>();
            }
            else if (obj["value"] is JsonNode value)
            {
                yield return value.GetValue<string>();
            }
        }
    }

    private static string BuildClasspath(
        MinecraftInstanceInfo instance,
        IEnumerable<MinecraftLibraryFile> libraries,
        MinecraftLaunchResources resources)
    {
        var classpath = new List<string>();
        if (resources.UsesRetroWrapper && resources.RetroWrapperPath is not null && File.Exists(resources.RetroWrapperPath))
            classpath.Add(resources.RetroWrapperPath);

        var optiFinePath = string.Empty;
        foreach (var library in libraries)
        {
            if (ShouldSkipClasspathLibraryForCleanroom(instance, library)) continue;
            if (!File.Exists(library.LocalPath)) continue;

            if (library.OriginalName.Contains(":OptiFine:", StringComparison.OrdinalIgnoreCase) ||
                library.OriginalName.Contains(":optifine:", StringComparison.OrdinalIgnoreCase))
            {
                optiFinePath = library.LocalPath;
            }
            else
            {
                classpath.Add(library.LocalPath);
            }
        }

        foreach (var customHead in Config.Instance.ClasspathHead[instance.VersionDirectory].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            classpath.Insert(0, customHead);
        if (!string.IsNullOrWhiteSpace(optiFinePath))
            classpath.Add(optiFinePath);
        classpath.Add(instance.JarPath);
        return string.Join(Path.PathSeparator, classpath);
    }

    private static bool ShouldSkipClasspathLibraryForCleanroom(MinecraftInstanceInfo instance, MinecraftLibraryFile library)
    {
        if (!IsCleanroomInstance(instance)) return false;
        return library.OriginalName.Contains("org.lwjgl.lwjgl:lwjgl:2.9.4", StringComparison.OrdinalIgnoreCase) ||
               library.OriginalName.Contains("net.java.dev.jna:platform:3.4.0", StringComparison.OrdinalIgnoreCase) ||
               library.OriginalName.Contains("com.ibm.icu:icu4j-core-mojang:51.2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCleanroomInstance(MinecraftInstanceInfo instance)
    {
        if (instance.Json["libraries"] is not JsonArray libraries) return false;
        return libraries
            .OfType<JsonObject>()
            .Select(static library => library["name"]?.GetValue<string>() ?? string.Empty)
            .Any(static name => name.Contains("cleanroom", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<MinecraftLibraryFile> GetLibraries(MinecraftInstanceInfo instance)
        => MinecraftResourceResolver.GetLibraries(instance);

    private static string[] ApplyAndFilterGameArguments(
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string> replacements)
    {
        var raw = args.ToArray();
        var result = new List<string>();
        for (var i = 0; i < raw.Length; i++)
        {
            var current = ApplyReplacements(raw[i], replacements);
            var next = i + 1 < raw.Length ? ApplyReplacements(raw[i + 1], replacements) : null;
            if (string.Equals(current, "--versionType", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(next))
            {
                i++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(current))
                result.Add(current);
        }

        return result.ToArray();
    }

    private static void AddProxyArguments(MinecraftInstanceInfo instance, ICollection<string> args)
    {
        if (!Config.Instance.UseProxy[instance.VersionDirectory] ||
            Config.Network.HttpProxy.Type != (int)HttpProxyManager.ProxyMode.CustomProxy ||
            string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
        {
            return;
        }

        try
        {
            var address = Config.Network.HttpProxy.CustomAddress;
            var proxyUri = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : new Uri("http://" + address);
            var scheme = proxyUri.Scheme.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
            args.Add($"-D{scheme}.proxyHost={proxyUri.Host}");
            args.Add($"-D{scheme}.proxyPort={proxyUri.Port}");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, ModuleName, "添加游戏代理参数失败");
        }
    }

    private static bool IsRuleAllowed(JsonNode? rulesNode) =>
        MinecraftRuntimeRules.IsRuleAllowed(rulesNode, MinecraftArgumentFeatures.None);

    private static bool IsRuleAllowed(JsonNode? rulesNode, MinecraftArgumentFeatures features)
        => MinecraftRuntimeRules.IsRuleAllowed(rulesNode, features);

    private static bool RuleMatches(JsonObject rule, MinecraftArgumentFeatures features)
    {
        if (rule["features"] is JsonObject featureRules && !FeaturesMatch(featureRules, features)) return false;
        if (rule["os"] is not JsonObject os) return true;
        var name = os["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, GetMinecraftOsName(), StringComparison.OrdinalIgnoreCase))
            return false;
        var arch = os["arch"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(arch))
        {
            var currentArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            if (!string.Equals(arch, currentArch, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static bool FeaturesMatch(JsonObject featureRules, MinecraftArgumentFeatures features)
    {
        foreach (var (key, valueNode) in featureRules)
        {
            if (valueNode is null) continue;
            var expected = valueNode.GetValue<bool>();
            var actual = key switch
            {
                "has_custom_resolution" => features.HasCustomResolution,
                "is_demo_user" => features.IsDemoUser,
                "has_quick_plays_support" => features.HasQuickPlaySupport,
                "is_quick_play_singleplayer" => features.IsQuickPlaySingleplayer,
                "is_quick_play_multiplayer" => features.IsQuickPlayMultiplayer,
                "is_quick_play_realms" => false,
                "has_user_properties" => features.HasUserProperties,
                _ => false
            };

            if (actual != expected) return false;
        }

        return true;
    }

    private static string GetMinecraftOsName()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "osx";
        return "linux";
    }

    private static string? MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return null;
        var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 ? "-" + parts[3] : string.Empty;
        return Path.Combine(group, artifact, version, $"{artifact}-{version}{classifier}.jar");
    }

    private static string NormalizePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string GetNativesDirectory(MinecraftInstanceInfo instance)
    {
        var dir = Path.Combine(Paths.Temp, "Launch", instance.Name, "natives");
        Directory.CreateDirectory(dir);
        return dir;
    }

    internal static void ExtractNatives(IEnumerable<MinecraftLibraryFile> nativeLibraries, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var targetRoot = Path.GetFullPath(targetDirectory);
        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var native in nativeLibraries)
        {
            if (!File.Exists(native.LocalPath)) continue;
            using var archive = ZipFile.OpenRead(native.LocalPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;

                var outputPath = Path.GetFullPath(Path.Combine(targetDirectory, NormalizePath(entry.FullName)));
                if (!IsPathInside(outputPath, targetRoot))
                {
                    Log($"已跳过可疑 native 条目：{entry.FullName}");
                    continue;
                }

                expectedFiles.Add(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                entry.ExtractToFile(outputPath, true);
            }
        }

        foreach (var file in Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(file);
            if (expectedFiles.Contains(fullPath)) continue;
            try
            {
                File.Delete(fullPath);
                Log($"已清理过期 native 文件：{fullPath}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogWrapper.Warn(ex, ModuleName, $"清理过期 native 文件失败：{fullPath}");
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(targetDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogWrapper.Warn(ex, ModuleName, $"清理空 native 目录失败：{directory}");
            }
        }
    }

    private static bool IsPathInside(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLaunchTitle(MinecraftInstanceInfo instance, MinecraftAccountSession account)
    {
        var title = Config.Instance.Title[instance.VersionDirectory];
        if (string.IsNullOrEmpty(title) && !Config.Instance.UseGlobalTitle[instance.VersionDirectory])
            title = Config.Launch.Title;
        return ApplyLauncherPlaceholders(title ?? string.Empty, instance, account, replaceTime: false);
    }

    private static string ApplyLauncherPlaceholders(
        string text,
        MinecraftInstanceInfo instance,
        MinecraftAccountSession account,
        bool replaceTime)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace("{pcl_version}", Basics.VersionName, StringComparison.Ordinal)
            .Replace("{pcl_version_code}", Basics.VersionCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{pcl_version_branch}", Basics.VersionBranch, StringComparison.Ordinal)
            .Replace("{path}", Basics.CurrentDirectory, StringComparison.Ordinal)
            .Replace("{path_with_name}", Basics.ExecutablePath, StringComparison.Ordinal)
            .Replace("{path_temp}", Paths.Temp, StringComparison.Ordinal)
            .Replace("{minecraft}", instance.MinecraftFolder, StringComparison.Ordinal)
            .Replace("{version_path}", instance.VersionDirectory, StringComparison.Ordinal)
            .Replace("{verpath}", instance.VersionDirectory, StringComparison.Ordinal)
            .Replace("{version_indie}", instance.GameDirectory, StringComparison.Ordinal)
            .Replace("{verindie}", instance.GameDirectory, StringComparison.Ordinal)
            .Replace("{name}", instance.Name, StringComparison.Ordinal)
            .Replace("{version}", instance.VanillaVersion?.ToString() ?? instance.Name, StringComparison.Ordinal)
            .Replace("{user}", account.Name, StringComparison.Ordinal)
            .Replace("{uuid}", account.Uuid.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("{login}", account.Type switch
            {
                MinecraftAccountType.Microsoft => "正版",
                MinecraftAccountType.AuthlibInjector => "Authlib-Injector",
                _ => "离线"
            }, StringComparison.Ordinal);

        if (replaceTime)
        {
            text = text.Replace("{date}", DateTime.Now.ToString("yyyy/M/d", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{time}", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return text;
    }

    internal static bool WriteLauncherProfile(
        MinecraftInstanceInfo instance,
        MinecraftAccountSession account,
        Action<string>? log = null)
    {
        try
        {
            Directory.CreateDirectory(instance.MinecraftFolder);
            var path = Path.Combine(instance.MinecraftFolder, "launcher_profiles.json");
            JsonObject root;
            if (File.Exists(path))
            {
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? CreateLauncherProfilesRoot();
                }
                catch
                {
                    root = CreateLauncherProfilesRoot();
                }
            }
            else
            {
                root = CreateLauncherProfilesRoot();
            }

            var profiles = GetOrCreateObject(root, "profiles");
            var profileId = "PCL";
            profiles[profileId] = new JsonObject
            {
                ["name"] = "PCL",
                ["type"] = "custom",
                ["lastVersionId"] = instance.Name,
                ["gameDir"] = instance.GameDirectory,
                ["created"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["lastUsed"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            var accountId = NormalizeProfileKey(string.IsNullOrWhiteSpace(account.ClientToken) || account.ClientToken == "0"
                ? account.Uuid
                : account.ClientToken);
            var profileUuid = NormalizeProfileKey(account.Uuid);
            var authDb = GetOrCreateObject(root, "authenticationDatabase");
            authDb[accountId] = new JsonObject
            {
                ["username"] = account.Name,
                ["accessToken"] = account.AccessToken,
                ["profiles"] = new JsonObject
                {
                    [profileUuid] = new JsonObject
                    {
                        ["displayName"] = account.Name
                    }
                }
            };

            root["clientToken"] = account.ClientToken;
            root["selectedUser"] = new JsonObject
            {
                ["account"] = accountId,
                ["profile"] = profileUuid
            };

            File.WriteAllText(path, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            log?.Invoke("已更新 launcher_profiles.json");
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, ModuleName, "更新 launcher_profiles.json 失败，启动将继续");
            log?.Invoke("更新 launcher_profiles.json 失败，启动将继续。");
            return false;
        }

        static JsonObject CreateLauncherProfilesRoot() => new()
        {
            ["profiles"] = new JsonObject(),
            ["authenticationDatabase"] = new JsonObject(),
            ["selectedProfile"] = "PCL"
        };

        static JsonObject GetOrCreateObject(JsonObject root, string key)
        {
            if (root[key] is JsonObject obj) return obj;
            obj = new JsonObject();
            root[key] = obj;
            return obj;
        }

        static string NormalizeProfileKey(string raw)
        {
            var filtered = new string((raw ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            return string.IsNullOrWhiteSpace(filtered) ? "00000000000000000000000000000000" : filtered;
        }
    }

    private static async Task RunPreLaunchCommandsAsync(
        MinecraftInstanceInfo instance,
        MinecraftLaunchOptions options,
        string arguments,
        JavaEntry java,
        MinecraftAccountSession account,
        CancellationToken cancellationToken)
    {
        var commands = new[]
        {
            (Command: Config.Launch.PreLaunchCommand, Wait: Config.Launch.PreLaunchCommandWait),
            (Command: Config.Instance.PreLaunchCommand[instance.VersionDirectory], Wait: Config.Instance.PreLaunchCommandWait[instance.VersionDirectory])
        };

        foreach (var (command, wait) in commands)
        {
            if (string.IsNullOrWhiteSpace(command)) continue;
            var expanded = ApplyLauncherPlaceholders(command, instance, account, replaceTime: true)
                .Replace("{java}", java.Installation.JavaFolder, StringComparison.Ordinal)
                .Replace("{launch_args}", arguments, StringComparison.Ordinal);
            using var process = StartShell(expanded, instance.MinecraftFolder);
            if (!wait) continue;
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static Process StartShell(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("预启动命令未能创建进程。");
    }

    private static Process StartProcess(MinecraftInstanceInfo instance, JavaEntry java, string arguments, bool captureOutput)
    {
        var useJavaExe = Config.Launch.NoJavaw || java.Installation.JavawExePath is null;
        var fileName = useJavaExe ? java.Installation.JavaExePath : java.Installation.JavawExePath!;
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = instance.GameDirectory,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = Config.Launch.NoJavaw,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };
        startInfo.Environment["APPDATA"] = instance.MinecraftFolder;
        var path = startInfo.Environment.TryGetValue("PATH", out var existingPath) ? existingPath : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        startInfo.Environment["PATH"] = java.Installation.JavaFolder + Path.PathSeparator + path;
        return Process.Start(startInfo) ?? throw new InvalidOperationException("游戏进程未能创建。");
    }

    private static void StartGameOutputCapture(
        Process process,
        IProgress<MinecraftLaunchProgress>? progress,
        MinecraftAccountSession account)
    {
        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var safe = FilterSensitive(line, account);
            progress?.Report(new MinecraftLaunchProgress(MinecraftLaunchStage.StartProcess, "游戏输出", 1, safe));
            Log(safe, account);
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);
        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogWrapper.Warn(ex, ModuleName, "捕获游戏输出失败");
        }
    }

    private static void ApplyPriority(Process process)
    {
        try
        {
            process.PriorityBoostEnabled = true;
            process.PriorityClass = Config.Launch.ProcessPriority switch
            {
                GameProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                GameProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                _ => ProcessPriorityClass.Normal
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, ModuleName, "设置游戏进程优先级失败");
        }
    }

    private static void WriteBatch(string path, JavaEntry java, MinecraftInstanceInfo instance, string arguments, MinecraftAccountSession account)
    {
        var lines = OperatingSystem.IsWindows()
            ? $"@echo off\r\ncd /D \"{instance.GameDirectory}\"\r\n\"{java.Installation.JavaExePath}\" {arguments}\r\npause\r\n"
            : $"#!/bin/sh\ncd \"{instance.GameDirectory}\"\n\"{java.Installation.JavaExePath}\" {arguments}\n";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, FilterSensitive(lines, account), Encoding.UTF8);
    }

    private static string ApplyReplacements(string input, IReadOnlyDictionary<string, string> replacements)
    {
        foreach (var (key, value) in replacements)
            input = input.Replace(key, value, StringComparison.Ordinal);
        return input;
    }

    private static IReadOnlyList<string> SplitCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return [];
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];
            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (char.IsWhiteSpace(c) && !inQuote)
            {
                Flush();
                continue;
            }
            current.Append(c);
        }
        Flush();
        return args;

        void Flush()
        {
            if (current.Length == 0) return;
            args.Add(current.ToString());
            current.Clear();
        }
    }

    private static string QuoteArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (!arg.Any(static c => char.IsWhiteSpace(c) || c == '"')) return arg;
        return "\"" + arg.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static async Task NotifyAsync(MinecraftLaunchRequest request, string message, bool isError, CancellationToken cancellationToken)
    {
        if (request.UiBridge is null) return;
        await request.UiBridge.NotifyAsync(message, isError, cancellationToken).ConfigureAwait(false);
    }

    private static void Log(string text, MinecraftAccountSession? account = null)
    {
        LogWrapper.Info(ModuleName, FilterSensitive(text, account));
    }

    internal static string FilterSensitive(string text, MinecraftAccountSession? account = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        try
        {
            if (!string.IsNullOrWhiteSpace(States.Game.LegacyProfile.LoginMsJson))
                text = text.Replace(States.Game.LegacyProfile.LoginMsJson, "***", StringComparison.Ordinal);
        }
        catch
        {
            // Config may be unavailable in tests.
        }

        if (account is not null)
        {
            text = MaskSecret(text, account.AccessToken);
            text = MaskSecret(text, account.ClientToken);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            text = text.Replace(home, "{user-home}", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(home.Replace('\\', '/'), "{user-home}", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(home.Replace('/', '\\'), "{user-home}", StringComparison.OrdinalIgnoreCase);
        }

        return text;

        static string MaskSecret(string value, string? secret)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 10) return value;
            return value.Replace(secret, "***", StringComparison.Ordinal);
        }
    }
}

public sealed record MinecraftLaunchPlan(
    string ArgumentString,
    string NativesDirectory,
    IReadOnlyList<MinecraftLibraryFile> NativeLibraries,
    string Classpath,
    string LaunchTitle,
    IReadOnlyList<string> JvmArguments,
    IReadOnlyList<string> GameArguments,
    MinecraftLaunchResources Resources);

public sealed record MinecraftLaunchResources(
    string ResourceDirectory,
    string? JavaWrapperPath,
    string? RetroWrapperPath,
    string? LwjglUnsafeAgentPath,
    string? RendererAgentPath,
    bool UsesJavaWrapper,
    bool UsesRetroWrapper,
    bool UsesLwjglUnsafeAgent,
    int RendererMode);

public sealed record MinecraftLibraryFile(string OriginalName, string LocalPath, bool IsNative);

internal static class MinecraftLaunchResourceService
{
    private const string RendererAgentVersion = "25.3.5";
    private static readonly object ExtractLock = new();

    public static string ResourceDirectory => Path.Combine(Paths.SharedLocalData, "Launch", "Resources");

    public static string ExtractEmbeddedJar(string fileName)
    {
        lock (ExtractLock)
        {
            Directory.CreateDirectory(ResourceDirectory);
            var targetPath = Path.Combine(ResourceDirectory, fileName);
            try
            {
                WriteResource(fileName, targetPath);
                return targetPath;
            }
            catch (Exception ex) when (File.Exists(targetPath))
            {
                LogWrapper.Warn(ex, "Launch", $"释放启动资源失败，将尝试删除后重试：{targetPath}");
                try
                {
                    File.Delete(targetPath);
                    WriteResource(fileName, targetPath);
                    return targetPath;
                }
                catch (Exception retryEx)
                {
                    LogWrapper.Warn(retryEx, "Launch", $"启动资源重试释放失败，将使用现有文件：{targetPath}");
                    return targetPath;
                }
            }
        }
    }

    public static string? ResolveRendererAgentPath()
    {
        var candidates = new[]
        {
            Path.Combine(ResourceDirectory, "mesa-loader-windows", RendererAgentVersion, "Loader.jar"),
            Path.Combine(Basics.ExecutableDirectory, "mesa-loader-windows", RendererAgentVersion, "Loader.jar")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void WriteResource(string fileName, string targetPath)
    {
        var assembly = typeof(MinecraftLaunchResourceService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new FileNotFoundException($"启动资源不存在：{fileName}");

        using var resource = assembly.GetManifestResourceStream(resourceName) ??
                             throw new FileNotFoundException($"启动资源无法读取：{fileName}");
        if (File.Exists(targetPath) && resource.CanSeek && new FileInfo(targetPath).Length == resource.Length)
            return;

        resource.Position = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var output = File.Create(targetPath);
        resource.CopyTo(output);
    }
}

public sealed record MinecraftArgumentFeatures(
    bool HasCustomResolution,
    bool IsDemoUser,
    bool HasQuickPlaySupport,
    bool IsQuickPlaySingleplayer,
    bool IsQuickPlayMultiplayer,
    bool HasUserProperties)
{
    public static MinecraftArgumentFeatures None { get; } = new(false, false, false, false, false, false);

    public static MinecraftArgumentFeatures FromOptions(MinecraftLaunchOptions options) =>
        new(
            Config.Launch.GameWindowMode != GameWindowSizeMode.Fullscreen,
            options.IsDemo,
            !string.IsNullOrWhiteSpace(options.WorldName) || !string.IsNullOrWhiteSpace(options.ServerIp),
            !string.IsNullOrWhiteSpace(options.WorldName),
            !string.IsNullOrWhiteSpace(options.ServerIp),
            false);
}

public static class MinecraftOfflineUuid
{
    public static string Create(string username)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
