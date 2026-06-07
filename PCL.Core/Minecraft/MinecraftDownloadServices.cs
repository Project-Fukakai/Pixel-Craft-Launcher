using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO.Download;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Minecraft;

public enum MinecraftLoaderKind
{
    Vanilla,
    OptiFine,
    Forge,
    NeoForge,
    Cleanroom,
    Fabric,
    LegacyFabric,
    Quilt,
    LabyMod,
    LiteLoader
}

public sealed record MinecraftVersionManifestEntry(
    string Id,
    string Type,
    string Url,
    DateTime Time,
    DateTime ReleaseTime);

public sealed record MinecraftFolderInfo(
    string Path,
    bool IsDefault,
    bool IsCustom,
    int InstanceCount);

public sealed record MinecraftInstanceSummary(
    string Name,
    string MinecraftFolder,
    string VersionDirectory,
    string JsonPath,
    string Type,
    DateTime ReleaseTime,
    bool IsFavorite,
    bool IsHidden);

public sealed record MinecraftLoaderSelection(
    MinecraftLoaderKind Kind,
    string? Version = null,
    bool SaveServerJar = false);

public sealed record MinecraftLoaderOption(
    MinecraftLoaderKind Kind,
    string DisplayName,
    string Description,
    string Icon,
    int SortOrder,
    bool IsAvailable,
    string StatusText,
    bool IsInstallSupported);

public sealed record MinecraftInstallRequest(
    string VersionId,
    string VersionUrl,
    string TargetMinecraftFolder,
    string? InstanceName = null,
    MinecraftLoaderSelection? Loader = null);

public sealed record MinecraftRepairRequest(
    MinecraftInstanceInfo Instance,
    bool IncludeClient = true,
    bool IncludeLibraries = true,
    bool IncludeAssets = true);

public sealed record MinecraftDownloadTaskInfo(
    string Id,
    string Name,
    NDlTaskState State,
    double Progress,
    string? Message,
    long DownloadedBytes = 0,
    long? TotalBytes = null,
    long SpeedBytesPerSecond = 0,
    string? TargetPath = null,
    IReadOnlyList<string>? Urls = null)
{
    public bool IsFinished => State is NDlTaskState.Finished or NDlTaskState.Failed or NDlTaskState.Cancelled;
}

public sealed record MinecraftDownloadFile(
    string Id,
    IReadOnlyList<string> Urls,
    string TargetPath,
    long? Size = null,
    string? Sha1 = null,
    string? Name = null);

public static class MinecraftLoaderCatalog
{
    private static readonly IReadOnlyList<LoaderDefinition> Definitions =
    [
        new(MinecraftLoaderKind.Vanilla, "原版", "不添加 Mod Loader，直接安装 Minecraft 客户端。", "mdi-cube-outline", 0, true),
        new(MinecraftLoaderKind.Forge, "Forge", "适用于大量传统 Forge Mod；通过安装器生成 profile 并补全依赖。", "Assets/Blocks/Anvil.png", 10, true),
        new(MinecraftLoaderKind.Cleanroom, "Cleanroom", "1.12.2 的现代化 Forge 分支；通过安装器生成 profile。", "Assets/Blocks/Cleanroom.png", 20, true),
        new(MinecraftLoaderKind.NeoForge, "NeoForge", "Minecraft 1.20.1+ 的 Forge 分支；通过安装器生成 profile 并补全依赖。", "Assets/Blocks/NeoForge.png", 30, true),
        new(MinecraftLoaderKind.Fabric, "Fabric", "可直接合并 Fabric profile 并补全库文件。", "Assets/Blocks/Fabric.png", 40, true),
        new(MinecraftLoaderKind.LegacyFabric, "Legacy Fabric", "旧版本 Fabric 分支，可直接合并 profile。", "Assets/Blocks/Fabric.png", 50, true),
        new(MinecraftLoaderKind.Quilt, "Quilt", "可直接合并 Quilt profile 并补全库文件。", "Assets/Blocks/Quilt.png", 60, true),
        new(MinecraftLoaderKind.LabyMod, "LabyMod", "客户端定制加载器；合并 LabyMod profile。", "Assets/Blocks/LabyMod.png", 70, true),
        new(MinecraftLoaderKind.OptiFine, "OptiFine", "可独立安装；与其他 Mod Loader 共存时会作为 Mod 下载。", "mdi-package-variant", 80, true),
        new(MinecraftLoaderKind.LiteLoader, "LiteLoader", "旧版本轻量加载器；生成 LiteLoader 启动 profile。", "mdi-package-variant", 90, true)
    ];

    public static IReadOnlyList<MinecraftLoaderOption> GetOptions(string? minecraftVersion)
    {
        return Definitions
            .OrderBy(static definition => definition.SortOrder)
            .Select(definition => CreateOption(definition, minecraftVersion))
            .ToArray();
    }

    public static string GetDisplayName(MinecraftLoaderKind kind) =>
        Definitions.FirstOrDefault(definition => definition.Kind == kind)?.DisplayName ?? kind.ToString();

    public static string BuildDefaultInstanceName(string minecraftVersion, MinecraftLoaderSelection loader)
    {
        if (loader.Kind == MinecraftLoaderKind.Vanilla)
            return minecraftVersion;

        var suffix = loader.Kind.ToString().ToLowerInvariant();
        return $"{minecraftVersion}-{suffix}{(string.IsNullOrWhiteSpace(loader.Version) ? "" : "-" + loader.Version)}";
    }

    private static MinecraftLoaderOption CreateOption(LoaderDefinition definition, string? minecraftVersion)
    {
        var available = IsAvailable(definition.Kind, minecraftVersion);
        var status = available
            ? definition.IsInstallSupported ? "可以添加" : "暂不支持安装"
            : GetUnavailableReason(definition.Kind, minecraftVersion);
        return new MinecraftLoaderOption(
            definition.Kind,
            definition.DisplayName,
            definition.Description,
            definition.Icon,
            definition.SortOrder,
            available,
            status,
            definition.IsInstallSupported);
    }

    private static bool IsAvailable(MinecraftLoaderKind kind, string? minecraftVersion)
    {
        var version = MinecraftVersionNumber.TryParse(minecraftVersion);
        if (version is null) return false;
        return kind switch
        {
            MinecraftLoaderKind.Vanilla => true,
            MinecraftLoaderKind.Fabric => IsAtLeast(version.Value, 1, 14),
            MinecraftLoaderKind.LegacyFabric => IsAtLeast(version.Value, 1, 0) && !IsAtLeast(version.Value, 1, 14),
            MinecraftLoaderKind.Quilt => IsAtLeast(version.Value, 1, 14, 4),
            MinecraftLoaderKind.Cleanroom => string.Equals(minecraftVersion, "1.12.2", StringComparison.OrdinalIgnoreCase),
            MinecraftLoaderKind.NeoForge => IsAtLeast(version.Value, 1, 20, 1),
            MinecraftLoaderKind.LiteLoader => IsAtLeast(version.Value, 1, 0) && !IsAtLeast(version.Value, 1, 13),
            MinecraftLoaderKind.LabyMod => IsAtLeast(version.Value, 1, 8),
            MinecraftLoaderKind.Forge => IsAtLeast(version.Value, 1, 0),
            MinecraftLoaderKind.OptiFine => IsAtLeast(version.Value, 1, 0),
            _ => false
        };
    }

    private static string GetUnavailableReason(MinecraftLoaderKind kind, string? minecraftVersion)
    {
        var version = string.IsNullOrWhiteSpace(minecraftVersion) ? "当前版本" : minecraftVersion;
        return kind switch
        {
            MinecraftLoaderKind.Cleanroom => "仅支持 1.12.2",
            MinecraftLoaderKind.Quilt => "需要 Minecraft 1.14.4 或更高版本",
            MinecraftLoaderKind.Fabric => "需要 Minecraft 1.14 或更高版本",
            MinecraftLoaderKind.LegacyFabric => "仅用于 1.13 及更早版本",
            MinecraftLoaderKind.NeoForge => "需要 Minecraft 1.20.1 或更高版本",
            MinecraftLoaderKind.LiteLoader => "仅用于较早 Minecraft 版本",
            _ => version + " 暂无可用版本"
        };
    }

    private static bool IsAtLeast(MinecraftVersionNumber version, int major, int minor, int patch = 0) =>
        version.CompareTo(new MinecraftVersionNumber(major, minor, patch)) >= 0;

    private sealed record LoaderDefinition(
        MinecraftLoaderKind Kind,
        string DisplayName,
        string Description,
        string Icon,
        int SortOrder,
        bool IsInstallSupported);
}

public static class MinecraftRuntimeRules
{
    public static string CurrentMinecraftOsName
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsMacOS()) return "osx";
            return "linux";
        }
    }

    public static string CurrentMinecraftArchName
    {
        get
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                System.Runtime.InteropServices.Architecture.X64 => "x64",
                _ => "x86"
            };
        }
    }

    public static bool IsRuleAllowed(JsonNode? rulesNode) => IsRuleAllowed(rulesNode, MinecraftArgumentFeatures.None);

    public static bool IsRuleAllowed(JsonNode? rulesNode, MinecraftArgumentFeatures features)
    {
        if (rulesNode is not JsonArray rules) return true;
        var allowed = false;
        foreach (var rule in rules.OfType<JsonObject>())
        {
            if (!RuleMatches(rule, features)) continue;
            var action = rule["action"]?.GetValue<string>();
            allowed = string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static bool RuleMatches(JsonObject rule, MinecraftArgumentFeatures features)
    {
        if (rule["features"] is JsonObject featureRules && !FeaturesMatch(featureRules, features)) return false;
        if (rule["os"] is not JsonObject os) return true;
        var name = os["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name) &&
            !string.Equals(name, CurrentMinecraftOsName, StringComparison.OrdinalIgnoreCase))
            return false;

        var arch = os["arch"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(arch))
        {
            var current = CurrentMinecraftArchName == "arm64" ? "arm64" : (Environment.Is64BitOperatingSystem ? "x64" : "x86");
            if (!string.Equals(arch, current, StringComparison.OrdinalIgnoreCase)) return false;
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
}

public static class MinecraftResourceResolver
{
    public static IReadOnlyList<MinecraftLibraryFile> GetLibraries(MinecraftInstanceInfo instance) =>
        GetLibraryDownloads(instance).Select(static l => l.Library).ToArray();

    public static IReadOnlyList<(MinecraftLibraryFile Library, MinecraftDownloadFile? Download)> GetLibraryDownloads(MinecraftInstanceInfo instance)
    {
        var result = new List<(MinecraftLibraryFile, MinecraftDownloadFile?)>();
        if (instance.Json["libraries"] is not JsonArray libraries) return result;
        var osName = MinecraftRuntimeRules.CurrentMinecraftOsName;
        foreach (var library in libraries.OfType<JsonObject>())
        {
            if (!MinecraftRuntimeRules.IsRuleAllowed(library["rules"])) continue;
            var name = library["name"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var artifact = library["downloads"]?["artifact"] as JsonObject;
            var artifactPath = artifact?["path"]?.GetValue<string>() ?? MavenNameToPath(name);
            if (!string.IsNullOrWhiteSpace(artifactPath))
            {
                var localPath = Path.Combine(instance.MinecraftFolder, "libraries", NormalizePath(artifactPath));
                result.Add((new MinecraftLibraryFile(name, localPath, false), CreateDownload(artifact, localPath, name)));
            }

            if (library["natives"] is not JsonObject natives) continue;
            var classifier = natives[osName]?.GetValue<string>()?
                .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(classifier)) continue;
            var native = library["downloads"]?["classifiers"]?[classifier] as JsonObject;
            var nativePath = native?["path"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(nativePath)) continue;
            var nativeLocalPath = Path.Combine(instance.MinecraftFolder, "libraries", NormalizePath(nativePath));
            result.Add((new MinecraftLibraryFile(name, nativeLocalPath, true), CreateDownload(native, nativeLocalPath, name + ":" + classifier)));
        }

        return result;
    }

    public static IReadOnlyList<MinecraftDownloadFile> GetRequiredFiles(
        MinecraftInstanceInfo instance,
        bool includeClient = true,
        bool includeLibraries = true,
        bool includeAssets = true)
    {
        var files = new List<MinecraftDownloadFile>();
        if (includeClient && instance.Json["downloads"]?["client"] is JsonObject client)
        {
            var clientFile = CreateDownload(client, instance.JarPath, instance.Name + " client");
            if (clientFile is not null) files.Add(clientFile);
        }

        if (includeLibraries)
            files.AddRange(GetLibraryDownloads(instance).Select(static item => item.Download).OfType<MinecraftDownloadFile>());

        if (includeAssets)
            files.AddRange(GetAssetFiles(instance));

        return files
            .GroupBy(static f => f.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(static g => g.First())
            .ToArray();
    }

    public static IReadOnlyList<MinecraftDownloadFile> GetAssetFiles(MinecraftInstanceInfo instance)
    {
        var files = new List<MinecraftDownloadFile>();
        var assetIndex = instance.Json["assetIndex"] as JsonObject;
        if (assetIndex is null) return files;

        var indexId = assetIndex["id"]?.GetValue<string>() ?? instance.AssetIndexName;
        var indexPath = Path.Combine(instance.MinecraftFolder, "assets", "indexes", indexId + ".json");
        var indexDownload = CreateDownload(assetIndex, indexPath, "assets index " + indexId);
        if (indexDownload is not null) files.Add(indexDownload);
        if (!File.Exists(indexPath)) return files;

        JsonObject? index;
        try
        {
            index = JsonNode.Parse(File.ReadAllText(indexPath)) as JsonObject;
        }
        catch
        {
            return files;
        }

        if (index?["objects"] is not JsonObject objects) return files;
        foreach (var (name, value) in objects)
        {
            if (value is not JsonObject obj) continue;
            var hash = obj["hash"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(hash) || hash.Length < 2) continue;
            var size = obj["size"]?.GetValue<long?>();
            var path = Path.Combine(instance.MinecraftFolder, "assets", "objects", hash[..2], hash);
            var urlPath = $"{hash[..2]}/{hash}";
            files.Add(new MinecraftDownloadFile(
                "asset:" + hash,
                MapUrls("https://resources.download.minecraft.net/" + urlPath),
                path,
                size,
                hash,
                name));
        }

        return files;
    }

    public static string? MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return null;
        var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 ? "-" + parts[3] : string.Empty;
        return Path.Combine(group, artifact, version, $"{artifact}-{version}{classifier}.jar");
    }

    internal static string NormalizePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    internal static IReadOnlyList<string> MapUrls(string url)
    {
        var urls = new List<string> { url };
        if (url.Contains("piston-meta.mojang.com", StringComparison.OrdinalIgnoreCase))
            urls.Add(url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase));
        if (url.Contains("piston-data.mojang.com", StringComparison.OrdinalIgnoreCase))
            urls.Add(url.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase));
        if (url.Contains("libraries.minecraft.net", StringComparison.OrdinalIgnoreCase))
            urls.Add(url.Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase));
        if (url.Contains("resources.download.minecraft.net", StringComparison.OrdinalIgnoreCase))
            urls.Add(url.Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets", StringComparison.OrdinalIgnoreCase));
        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static MinecraftDownloadFile? CreateDownload(JsonObject? node, string targetPath, string name)
    {
        var url = node?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url)) return null;
        var size = node?["size"]?.GetValue<long?>();
        var sha1 = node?["sha1"]?.GetValue<string>();
        return new MinecraftDownloadFile("file:" + targetPath, MapUrls(url), targetPath, size, sha1, name);
    }
}

public sealed class MinecraftDownloadService
{
    private static readonly HttpClient Http = new();

    public MinecraftDownloadService()
    {
        Scheduler.ProgressChanged += (_, progress) =>
        {
            var task = Scheduler.Snapshot().FirstOrDefault(t => t.Request.Id == progress.Id);
            var info = new MinecraftDownloadTaskInfo(
                progress.Id,
                task?.Request.DisplayName ?? progress.Id,
                progress.State,
                progress.Progress,
                progress.Message,
                progress.DownloadedBytes,
                progress.TotalBytes,
                progress.SpeedBytesPerSecond,
                task?.Request.TargetPath,
                task?.Request.Urls);
            UpdateRecentTask(info);
            ProgressChanged?.Invoke(this, info);
        };
    }

    public NDlScheduler Scheduler { get; } = new(4);
    public ObservableCollection<MinecraftDownloadTaskInfo> RecentTasks { get; } = [];
    public event EventHandler<MinecraftDownloadTaskInfo>? ProgressChanged;

    public bool CancelTask(string id) => Scheduler.Cancel(id);

    private void UpdateRecentTask(MinecraftDownloadTaskInfo info)
    {
        var existing = RecentTasks.FirstOrDefault(task => task.Id == info.Id);
        if (existing is not null)
            RecentTasks.Remove(existing);
        RecentTasks.Insert(0, info);
        while (RecentTasks.Count > 100)
            RecentTasks.RemoveAt(RecentTasks.Count - 1);
    }

    public async Task<IReadOnlyList<MinecraftVersionManifestEntry>> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        const string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        await using var stream = await Http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (root?["versions"] is not JsonArray versions) return [];

        return versions.OfType<JsonObject>()
            .Select(static version => new MinecraftVersionManifestEntry(
                version["id"]?.GetValue<string>() ?? string.Empty,
                version["type"]?.GetValue<string>() ?? string.Empty,
                version["url"]?.GetValue<string>() ?? string.Empty,
                ParseDate(version["time"]?.GetValue<string>()),
                ParseDate(version["releaseTime"]?.GetValue<string>())))
            .Where(static version => !string.IsNullOrWhiteSpace(version.Id) && !string.IsNullOrWhiteSpace(version.Url))
            .ToArray();
    }

    public async Task<JsonObject> GetVersionJsonAsync(string url, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in MinecraftResourceResolver.MapUrls(url))
        {
            try
            {
                var text = await Http.GetStringAsync(candidate, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("版本 JSON 无效。");
            }
            catch when (candidate != url)
            {
                // Try next mirror.
            }
        }

        throw new InvalidOperationException("无法获取版本 JSON：" + url);
    }

    public async Task DownloadFilesAsync(
        IEnumerable<MinecraftDownloadFile> files,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = files
            .Where(static file => !File.Exists(file.TargetPath) || !IsExistingFileValid(file))
            .Select(file =>
            {
                var task = Scheduler.Enqueue(new NDlRequest(file.Id, file.Urls, file.TargetPath, file.Size, file.Sha1, file.Name));
                return task;
            })
            .ToArray();

        while (tasks.Any(static t => t.State is NDlTaskState.Waiting or NDlTaskState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var task in tasks)
            {
                progress?.Report(new MinecraftDownloadTaskInfo(
                    task.Request.Id,
                    task.Request.DisplayName ?? task.Request.Id,
                    task.State,
                    task.TotalBytes is > 0 ? task.DownloadedBytes / (double)task.TotalBytes.Value : 0,
                    task.ErrorMessage,
                    task.DownloadedBytes,
                    task.TotalBytes,
                    task.SpeedBytesPerSecond,
                    task.Request.TargetPath,
                    task.Request.Urls));
            }

            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }

        var failed = tasks.FirstOrDefault(static t => t.State == NDlTaskState.Failed);
        if (failed is not null)
            throw new IOException($"下载失败：{failed.Request.DisplayName ?? failed.Request.Id}，{failed.ErrorMessage}");
        if (tasks.Any(static t => t.State == NDlTaskState.Cancelled))
            throw new OperationCanceledException();
    }

    private static bool IsExistingFileValid(MinecraftDownloadFile file)
    {
        var info = new FileInfo(file.TargetPath);
        if (file.Size is { } size && info.Length != size) return false;
        return true;
    }

    private static DateTime ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : DateTime.MinValue;
}

public sealed class MinecraftInstallService(MinecraftDownloadService? downloadService = null)
{
    private readonly MinecraftDownloadService _downloadService = downloadService ?? new MinecraftDownloadService();

    public async Task<MinecraftInstanceInfo> InstallAsync(
        MinecraftInstallRequest request,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var loader = request.Loader ?? new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla);
        if (loader.Kind is not (MinecraftLoaderKind.Vanilla or MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric or MinecraftLoaderKind.Quilt))
        {
            var catalogService = new MinecraftModLoaderCatalogService();
            var selection = await ResolveMergedSelectionAsync(catalogService, loader, request.VersionId, cancellationToken).ConfigureAwait(false);
            var mergedInstanceName = string.IsNullOrWhiteSpace(request.InstanceName)
                ? MinecraftMergedInstallService.BuildDefaultInstanceName(request.VersionId, selection)
                : request.InstanceName.Trim();
            var mergedService = new MinecraftMergedInstallService(_downloadService, catalogService);
            return await mergedService.InstallAsync(
                new MinecraftMergedInstallRequest(
                    request.VersionId,
                    request.VersionUrl,
                    request.TargetMinecraftFolder,
                    mergedInstanceName,
                    selection,
                    loader.SaveServerJar),
                progress,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        var instanceName = string.IsNullOrWhiteSpace(request.InstanceName)
            ? MinecraftLoaderCatalog.BuildDefaultInstanceName(request.VersionId, loader)
            : request.InstanceName.Trim();
        var versionDirectory = Path.Combine(request.TargetMinecraftFolder, "versions", instanceName);
        var jsonPath = Path.Combine(versionDirectory, instanceName + ".json");
        Directory.CreateDirectory(versionDirectory);

        var versionJson = await _downloadService.GetVersionJsonAsync(request.VersionUrl, cancellationToken).ConfigureAwait(false);
        if (loader.Kind is MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric or MinecraftLoaderKind.Quilt)
            versionJson = await MergeLoaderProfileAsync(versionJson, request.VersionId, loader, cancellationToken).ConfigureAwait(false);
        else if (loader.Kind != MinecraftLoaderKind.Vanilla)
            throw new NotSupportedException(loader.Kind.ToString());

        versionJson["id"] = instanceName;
        await File.WriteAllTextAsync(jsonPath, versionJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        var instance = MinecraftInstanceInfo.Load(jsonPath);
        var files = MinecraftResourceResolver.GetRequiredFiles(instance, includeAssets: false).ToList();
        await _downloadService.DownloadFilesAsync(files, progress, cancellationToken).ConfigureAwait(false);

        var assetIndexFiles = MinecraftResourceResolver.GetAssetFiles(instance).Take(1).ToArray();
        await _downloadService.DownloadFilesAsync(assetIndexFiles, progress, cancellationToken).ConfigureAwait(false);
        var assetFiles = MinecraftResourceResolver.GetAssetFiles(instance).Skip(1).ToArray();
        await _downloadService.DownloadFilesAsync(assetFiles, progress, cancellationToken).ConfigureAwait(false);

        if (loader.SaveServerJar && versionJson["downloads"]?["server"] is JsonObject server)
        {
            var serverTarget = Path.Combine(versionDirectory, instanceName + "-server.jar");
            var serverFile = CreateServerDownload(server, serverTarget, instanceName);
            if (serverFile is not null)
                await _downloadService.DownloadFilesAsync([serverFile], progress, cancellationToken).ConfigureAwait(false);
        }

        return MinecraftInstanceInfo.Load(jsonPath);
    }

    private static async Task<MinecraftMergedLoaderSelection> ResolveMergedSelectionAsync(
        MinecraftModLoaderCatalogService catalogService,
        MinecraftLoaderSelection loader,
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var versions = await catalogService.GetLoaderVersionsAsync(loader.Kind, minecraftVersion, cancellationToken).ConfigureAwait(false);
        var entry = string.IsNullOrWhiteSpace(loader.Version)
            ? versions.FirstOrDefault()
            : versions.FirstOrDefault(version =>
                string.Equals(version.Version, loader.Version, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version.DisplayName, loader.Version, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidOperationException($"{MinecraftLoaderCatalog.GetDisplayName(loader.Kind)} 没有可用版本。");

        return loader.Kind switch
        {
            MinecraftLoaderKind.OptiFine => new MinecraftMergedLoaderSelection(OptiFine: entry),
            MinecraftLoaderKind.Forge => new MinecraftMergedLoaderSelection(Forge: entry),
            MinecraftLoaderKind.NeoForge => new MinecraftMergedLoaderSelection(NeoForge: entry),
            MinecraftLoaderKind.Cleanroom => new MinecraftMergedLoaderSelection(Cleanroom: entry),
            MinecraftLoaderKind.LiteLoader => new MinecraftMergedLoaderSelection(LiteLoader: entry),
            MinecraftLoaderKind.LabyMod => new MinecraftMergedLoaderSelection(LabyMod: entry),
            _ => throw new NotSupportedException(loader.Kind.ToString())
        };
    }

    private static MinecraftDownloadFile? CreateServerDownload(JsonObject server, string targetPath, string instanceName)
    {
        var url = server["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url)) return null;
        return new MinecraftDownloadFile(
            "server:" + instanceName,
            MinecraftResourceResolver.MapUrls(url),
            targetPath,
            server["size"]?.GetValue<long?>(),
            server["sha1"]?.GetValue<string>(),
            instanceName + " server");
    }

    private static async Task<JsonObject> MergeLoaderProfileAsync(
        JsonObject vanillaJson,
        string minecraftVersion,
        MinecraftLoaderSelection loader,
        CancellationToken cancellationToken)
    {
        var loaderVersion = loader.Version ?? await ResolveLatestLoaderVersionAsync(loader.Kind, minecraftVersion, cancellationToken).ConfigureAwait(false);
        var profileUrl = loader.Kind switch
        {
            MinecraftLoaderKind.Fabric => $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{loaderVersion}/profile/json",
            MinecraftLoaderKind.LegacyFabric => $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}/{loaderVersion}/profile/json",
            MinecraftLoaderKind.Quilt => $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{loaderVersion}/profile/json",
            _ => throw new NotSupportedException(loader.Kind.ToString())
        };

        using var http = new HttpClient();
        var text = await http.GetStringAsync(profileUrl, cancellationToken).ConfigureAwait(false);
        var loaderJson = JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("加载器 profile JSON 无效。");
        MergeJson(vanillaJson, loaderJson);
        return vanillaJson;
    }

    private static async Task<string> ResolveLatestLoaderVersionAsync(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var listUrl = kind switch
        {
            MinecraftLoaderKind.Fabric => $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}",
            MinecraftLoaderKind.LegacyFabric => $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}",
            MinecraftLoaderKind.Quilt => $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}",
            _ => throw new NotSupportedException(kind.ToString())
        };
        using var http = new HttpClient();
        var text = await http.GetStringAsync(listUrl, cancellationToken).ConfigureAwait(false);
        if (JsonNode.Parse(text) is not JsonArray versions)
            throw new InvalidDataException("加载器版本列表无效。");

        var latest = versions.OfType<JsonObject>()
            .Select(version => version["loader"]?["version"]?.GetValue<string>() ??
                               version["version"]?.GetValue<string>())
            .FirstOrDefault(static version => !string.IsNullOrWhiteSpace(version));
        return latest ?? throw new InvalidDataException("没有可用的加载器版本。");
    }

    private static void MergeJson(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToArray())
        {
            if (value is null) continue;
            if (key == "libraries" && target[key] is JsonArray targetLibraries && value is JsonArray sourceLibraries)
            {
                foreach (var library in sourceLibraries)
                    targetLibraries.Add(library?.DeepClone());
            }
            else if (key == "arguments" && target[key] is JsonObject targetArgs && value is JsonObject sourceArgs)
            {
                foreach (var (argKey, argValue) in sourceArgs)
                {
                    if (argValue is JsonArray sourceArray && targetArgs[argKey] is JsonArray targetArray)
                    {
                        foreach (var item in sourceArray)
                            targetArray.Add(item?.DeepClone());
                    }
                    else
                    {
                        targetArgs[argKey] = argValue?.DeepClone();
                    }
                }
            }
            else if (key is "id" or "inheritsFrom")
            {
                continue;
            }
            else
            {
                target[key] = value.DeepClone();
            }
        }
    }

}

public sealed class MinecraftRepairService(MinecraftDownloadService? downloadService = null) : IMinecraftLaunchFileRepairer
{
    private readonly MinecraftDownloadService _downloadService = downloadService ?? new MinecraftDownloadService();

    public async Task RepairAsync(
        MinecraftRepairRequest request,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = MinecraftResourceResolver.GetRequiredFiles(
            request.Instance,
            request.IncludeClient,
            request.IncludeLibraries,
            request.IncludeAssets);
        await _downloadService.DownloadFilesAsync(files, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RepairAsync(
        MinecraftInstanceInfo instance,
        IProgress<MinecraftLaunchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var adapter = progress is null
            ? null
            : new Progress<MinecraftDownloadTaskInfo>(info => progress.Report(new MinecraftLaunchProgress(
                MinecraftLaunchStage.PrepareFiles,
                "补全启动文件：" + info.Name,
                Math.Clamp(info.Progress, 0, 1),
                info.Message)));
        await RepairAsync(new MinecraftRepairRequest(instance), adapter, cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed class MinecraftInstanceService
{
    public IReadOnlyList<MinecraftFolderInfo> GetFolders(IEnumerable<string>? customFolders = null)
    {
        var defaults = MinecraftInstanceScanner.GetDefaultMinecraftFolders()
            .Select(NormalizeMinecraftFolder)
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Where(IsVisibleDefaultMinecraftFolder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var custom = (customFolders ?? ReadConfiguredMinecraftFolders())
            .Select(NormalizeMinecraftFolder)
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return custom.Concat(defaults)
            .GroupBy(static folder => folder, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var folder = group.First();
                return new MinecraftFolderInfo(
                    folder,
                    defaults.Contains(folder, StringComparer.OrdinalIgnoreCase),
                    custom.Contains(folder, StringComparer.OrdinalIgnoreCase),
                    CountInstances(folder));
            })
            .ToArray();
    }

    private static IEnumerable<string> ReadConfiguredMinecraftFolders()
    {
        try
        {
            return States.Game.Folders
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static folder => folder.Replace("$", Basics.ExecutableDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeMinecraftFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return string.Empty;

        return Path.GetFullPath(folder.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsVisibleDefaultMinecraftFolder(string folder)
    {
        var current = NormalizeMinecraftFolder(Path.Combine(Basics.ExecutableDirectory, ".minecraft"));
        if (string.Equals(folder, current, StringComparison.OrdinalIgnoreCase))
            return true;

        return Directory.Exists(Path.Combine(folder, "versions"));
    }

    public IReadOnlyList<MinecraftInstanceSummary> GetInstances(IEnumerable<string>? folders = null)
    {
        var instances = folders is null
            ? MinecraftInstanceScanner.ScanDefaultFolders()
            : MinecraftInstanceScanner.ScanFolders(folders);
        return instances.Select(ToSummary).ToArray();
    }

    public MinecraftInstanceInfo? Load(string versionDirectory)
    {
        var name = Path.GetFileName(versionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var jsonPath = Path.Combine(versionDirectory, name + ".json");
        return File.Exists(jsonPath) ? MinecraftInstanceInfo.Load(jsonPath) : null;
    }

    public MinecraftInstanceSummary Rename(MinecraftInstanceInfo instance, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("实例名不能为空。", nameof(newName));
        var targetDirectory = Path.Combine(Path.GetDirectoryName(instance.VersionDirectory)!, newName);
        if (Directory.Exists(targetDirectory)) throw new IOException("目标实例已存在：" + targetDirectory);
        Directory.Move(instance.VersionDirectory, targetDirectory);
        var oldJson = Path.Combine(targetDirectory, Path.GetFileName(instance.JsonPath));
        var newJson = Path.Combine(targetDirectory, newName + ".json");
        if (File.Exists(oldJson)) File.Move(oldJson, newJson);
        var oldJar = Path.Combine(targetDirectory, Path.GetFileName(instance.JarPath));
        var newJar = Path.Combine(targetDirectory, newName + ".jar");
        if (File.Exists(oldJar)) File.Move(oldJar, newJar);
        var loaded = MinecraftInstanceInfo.Load(newJson);
        return ToSummary(loaded);
    }

    public void SetHidden(MinecraftInstanceInfo instance, bool hidden)
    {
        var marker = Path.Combine(instance.VersionDirectory, ".pclignore");
        if (hidden) File.WriteAllText(marker, "Hidden by Pixel Craft Launcher.");
        else if (File.Exists(marker)) File.Delete(marker);
    }

    public void DeleteToRecycle(MinecraftInstanceInfo instance)
    {
        var recycleRoot = Path.Combine(Paths.SharedLocalData, "DeletedInstances");
        Directory.CreateDirectory(recycleRoot);
        var target = Path.Combine(recycleRoot, instance.Name + "-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        Directory.Move(instance.VersionDirectory, target);
        LogWrapper.Info("Instance", "实例已移动到启动器回收目录：" + target);
    }

    private static MinecraftInstanceSummary ToSummary(MinecraftInstanceInfo instance) =>
        new(
            instance.Name,
            instance.MinecraftFolder,
            instance.VersionDirectory,
            instance.JsonPath,
            instance.Json["type"]?.GetValue<string>() ?? string.Empty,
            instance.ReleaseTime,
            File.Exists(Path.Combine(instance.VersionDirectory, ".pclfavorite")),
            File.Exists(Path.Combine(instance.VersionDirectory, ".pclignore")));

    private static int CountInstances(string folder)
    {
        var versions = Path.Combine(folder, "versions");
        if (!Directory.Exists(versions)) return 0;
        return Directory.EnumerateDirectories(versions)
            .Count(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".json")));
    }
}
