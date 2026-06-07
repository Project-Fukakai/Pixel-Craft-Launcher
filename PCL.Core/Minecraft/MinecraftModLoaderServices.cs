using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Download;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Minecraft;

public enum MinecraftAddonKind
{
    FabricApi,
    LegacyFabricApi,
    Qsl,
    OptiFabric
}

public enum MinecraftRemoteSource
{
    Official,
    BmclApi,
    Modrinth,
    CurseForge,
    Generated
}

public sealed record MinecraftLoaderVersionEntry(
    MinecraftLoaderKind Kind,
    string Version,
    string DisplayName,
    string? MinecraftVersion,
    bool IsStable,
    bool IsRecommended,
    MinecraftRemoteSource Source,
    string? DownloadUrl = null,
    string? Sha1 = null,
    string? FileName = null,
    string? Category = null,
    DateTime? ReleaseTime = null,
    JsonObject? Metadata = null);

public sealed record MinecraftAddonFileEntry(
    MinecraftAddonKind Kind,
    string Id,
    string DisplayName,
    string FileName,
    IReadOnlyList<string> DownloadUrls,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<MinecraftLoaderKind> Loaders,
    string? Sha1,
    long? Size,
    bool IsStable,
    DateTime? ReleaseTime,
    MinecraftRemoteSource Source);

public sealed record MinecraftMergedLoaderSelection(
    MinecraftLoaderVersionEntry? OptiFine = null,
    MinecraftLoaderVersionEntry? Forge = null,
    MinecraftLoaderVersionEntry? NeoForge = null,
    MinecraftLoaderVersionEntry? Cleanroom = null,
    MinecraftLoaderVersionEntry? Fabric = null,
    MinecraftLoaderVersionEntry? LegacyFabric = null,
    MinecraftLoaderVersionEntry? Quilt = null,
    MinecraftLoaderVersionEntry? LiteLoader = null,
    MinecraftLoaderVersionEntry? LabyMod = null,
    MinecraftAddonFileEntry? FabricApi = null,
    MinecraftAddonFileEntry? LegacyFabricApi = null,
    MinecraftAddonFileEntry? Qsl = null,
    MinecraftAddonFileEntry? OptiFabric = null)
{
    public bool HasModLoader =>
        Forge is not null || NeoForge is not null || Cleanroom is not null || Fabric is not null ||
        LegacyFabric is not null || Quilt is not null || LiteLoader is not null || LabyMod is not null;

    public IEnumerable<MinecraftLoaderVersionEntry> Loaders
    {
        get
        {
            if (OptiFine is not null) yield return OptiFine;
            if (Forge is not null) yield return Forge;
            if (NeoForge is not null) yield return NeoForge;
            if (Cleanroom is not null) yield return Cleanroom;
            if (Fabric is not null) yield return Fabric;
            if (LegacyFabric is not null) yield return LegacyFabric;
            if (Quilt is not null) yield return Quilt;
            if (LiteLoader is not null) yield return LiteLoader;
            if (LabyMod is not null) yield return LabyMod;
        }
    }

    public IEnumerable<MinecraftAddonFileEntry> Addons
    {
        get
        {
            if (FabricApi is not null) yield return FabricApi;
            if (LegacyFabricApi is not null) yield return LegacyFabricApi;
            if (Qsl is not null) yield return Qsl;
            if (OptiFabric is not null) yield return OptiFabric;
        }
    }
}

public static class MinecraftLoaderCompatibility
{
    public static bool IsExclusiveLoader(MinecraftLoaderKind kind) => kind == MinecraftLoaderKind.LabyMod;

    public static bool IsOptiFineAllowedWith(MinecraftLoaderKind kind, string? minecraftVersion = null) =>
        kind switch
        {
            MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Cleanroom or MinecraftLoaderKind.Quilt or MinecraftLoaderKind.LabyMod => false,
            MinecraftLoaderKind.Fabric => !IsAtLeast(minecraftVersion, 1, 20, 5),
            _ => true
        };

    public static bool IsOptiFineCompatibleWithForge(MinecraftLoaderVersionEntry optiFine, MinecraftLoaderVersionEntry forge)
    {
        if (!string.Equals(optiFine.MinecraftVersion, forge.MinecraftVersion, StringComparison.OrdinalIgnoreCase))
            return false;

        var required = GetOptiFineRequiredForgeVersion(optiFine);
        if (required is null)
            return false;
        if (required.Length == 0)
            return true;
        if (required.Contains('.', StringComparison.Ordinal))
            return string.Equals(forge.Version, required, StringComparison.OrdinalIgnoreCase);

        var forgeBuild = forge.Version.Split('.').LastOrDefault();
        return string.Equals(forgeBuild, required, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetOptiFineRequiredForgeVersion(MinecraftLoaderVersionEntry optiFine)
    {
        var value = optiFine.Metadata?["forge"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value) || value.Contains("N/A", StringComparison.OrdinalIgnoreCase))
            return null;
        value = value.Replace("Forge", "", StringComparison.OrdinalIgnoreCase).Trim();
        return value;
    }

    public static IReadOnlyList<MinecraftLoaderKind> GetConflictingLoaderKinds(
        MinecraftMergedLoaderSelection selection,
        MinecraftLoaderKind kind)
    {
        var selected = GetSelectedLoaderKinds(selection).Where(selectedKind => selectedKind != kind).ToArray();
        if (kind == MinecraftLoaderKind.LabyMod)
            return selected;
        if (selection.LabyMod is not null)
            return [MinecraftLoaderKind.LabyMod];
        if (IsForgeFamily(kind))
            return selected.Where(IsForgeFamily).ToArray();
        if (IsFabricFamily(kind))
            return selected.Where(IsFabricFamily).ToArray();
        return [];
    }

    public static MinecraftMergedLoaderSelection SelectLoader(
        MinecraftMergedLoaderSelection selection,
        MinecraftLoaderVersionEntry entry)
    {
        if (entry.Kind == MinecraftLoaderKind.LabyMod)
            return new MinecraftMergedLoaderSelection(LabyMod: entry);

        if (selection.LabyMod is not null)
            selection = selection with { LabyMod = null };

        return entry.Kind switch
        {
            MinecraftLoaderKind.OptiFine => selection with { OptiFine = entry },
            MinecraftLoaderKind.Forge => selection with { Forge = entry, NeoForge = null, Cleanroom = null },
            MinecraftLoaderKind.NeoForge => selection with { NeoForge = entry, Forge = null, Cleanroom = null },
            MinecraftLoaderKind.Cleanroom => selection with { Cleanroom = entry, Forge = null, NeoForge = null },
            MinecraftLoaderKind.Fabric => selection with { Fabric = entry, LegacyFabric = null, Quilt = null, LegacyFabricApi = null, Qsl = null },
            MinecraftLoaderKind.LegacyFabric => selection with { LegacyFabric = entry, Fabric = null, Quilt = null, FabricApi = null, Qsl = null, OptiFabric = null },
            MinecraftLoaderKind.Quilt => selection with { Quilt = entry, Fabric = null, LegacyFabric = null, LegacyFabricApi = null, OptiFabric = null },
            MinecraftLoaderKind.LiteLoader => selection with { LiteLoader = entry },
            _ => selection
        };
    }

    public static MinecraftMergedLoaderSelection Normalize(MinecraftMergedLoaderSelection selection)
    {
        if (selection.LabyMod is not null)
            return new MinecraftMergedLoaderSelection(LabyMod: selection.LabyMod);

        if (selection.Forge is not null)
            selection = selection with { NeoForge = null, Cleanroom = null };
        else if (selection.NeoForge is not null)
            selection = selection with { Cleanroom = null };

        if (selection.Fabric is not null)
            selection = selection with { LegacyFabric = null, Quilt = null, LegacyFabricApi = null, Qsl = null };
        else if (selection.LegacyFabric is not null)
            selection = selection with { Quilt = null, FabricApi = null, Qsl = null, OptiFabric = null };
        else if (selection.Quilt is not null)
            selection = selection with { LegacyFabricApi = null, OptiFabric = null };

        if (selection.Fabric is null && selection.Quilt is null)
            selection = selection with { FabricApi = null };
        if (selection.LegacyFabric is null)
            selection = selection with { LegacyFabricApi = null };
        if (selection.Quilt is null)
            selection = selection with { Qsl = null };
        if (selection.Fabric is null || selection.OptiFine is null)
            selection = selection with { OptiFabric = null };
        return selection;
    }

    public static MinecraftMergedLoaderSelection Normalize(MinecraftMergedLoaderSelection selection, string? minecraftVersion)
    {
        selection = Normalize(selection);
        if (selection.OptiFine is null) return selection;

        if (selection.NeoForge is not null || selection.Cleanroom is not null || selection.Quilt is not null || selection.LabyMod is not null)
            selection = selection with { OptiFine = null, OptiFabric = null };
        if (selection.Fabric is not null && !IsOptiFineAllowedWith(MinecraftLoaderKind.Fabric, minecraftVersion))
            selection = selection with { OptiFine = null, OptiFabric = null };
        if (selection.OptiFine is { } optiFine && selection.Forge is not null && !IsOptiFineCompatibleWithForge(optiFine, selection.Forge))
            selection = selection with { OptiFine = null };
        return Normalize(selection);
    }

    private static IEnumerable<MinecraftLoaderKind> GetSelectedLoaderKinds(MinecraftMergedLoaderSelection selection)
    {
        if (selection.OptiFine is not null) yield return MinecraftLoaderKind.OptiFine;
        if (selection.Forge is not null) yield return MinecraftLoaderKind.Forge;
        if (selection.NeoForge is not null) yield return MinecraftLoaderKind.NeoForge;
        if (selection.Cleanroom is not null) yield return MinecraftLoaderKind.Cleanroom;
        if (selection.Fabric is not null) yield return MinecraftLoaderKind.Fabric;
        if (selection.LegacyFabric is not null) yield return MinecraftLoaderKind.LegacyFabric;
        if (selection.Quilt is not null) yield return MinecraftLoaderKind.Quilt;
        if (selection.LiteLoader is not null) yield return MinecraftLoaderKind.LiteLoader;
        if (selection.LabyMod is not null) yield return MinecraftLoaderKind.LabyMod;
    }

    private static bool IsForgeFamily(MinecraftLoaderKind kind) =>
        kind is MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Cleanroom;

    private static bool IsFabricFamily(MinecraftLoaderKind kind) =>
        kind is MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric or MinecraftLoaderKind.Quilt;

    private static bool IsAtLeast(string? minecraftVersion, int major, int minor, int patch = 0)
    {
        var version = MinecraftVersionNumber.TryParse(minecraftVersion);
        return version is not null && version.Value.CompareTo(new MinecraftVersionNumber(major, minor, patch)) >= 0;
    }
}

public sealed record MinecraftMergedInstallRequest(
    string VersionId,
    string VersionUrl,
    string TargetMinecraftFolder,
    string InstanceName,
    MinecraftMergedLoaderSelection Selection,
    bool SaveServerJar = false);

public sealed record MinecraftInstallStageInfo(
    string Id,
    string Name,
    NDlTaskState State,
    double Progress,
    string? Message);

public sealed class MinecraftModLoaderCatalogService
{
    private static readonly HttpClient Http = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<MinecraftLoaderVersionEntry>> _loaderCache = new();
    private readonly ConcurrentDictionary<MinecraftAddonKind, IReadOnlyList<MinecraftAddonFileEntry>> _addonCache = new();

    public async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var key = $"{kind}:{minecraftVersion}";
        if (_loaderCache.TryGetValue(key, out var cached)) return cached;

        var result = kind switch
        {
            MinecraftLoaderKind.Fabric => await GetFabricLikeVersionsAsync(kind, minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.LegacyFabric => await GetFabricLikeVersionsAsync(kind, minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.Quilt => await GetFabricLikeVersionsAsync(kind, minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.Forge => await GetForgeVersionsAsync(minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.NeoForge => await GetNeoForgeVersionsAsync(minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.Cleanroom => await GetCleanroomVersionsAsync(cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.OptiFine => await GetOptiFineVersionsAsync(minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.LiteLoader => await GetLiteLoaderVersionsAsync(minecraftVersion, cancellationToken).ConfigureAwait(false),
            MinecraftLoaderKind.LabyMod => await GetLabyModVersionsAsync(minecraftVersion, cancellationToken).ConfigureAwait(false),
            _ => []
        };

        _loaderCache[key] = result;
        return result;
    }

    public async Task<IReadOnlyList<MinecraftAddonFileEntry>> GetAddonFilesAsync(
        MinecraftAddonKind kind,
        CancellationToken cancellationToken = default)
    {
        if (_addonCache.TryGetValue(kind, out var cached)) return cached;

        var slug = kind switch
        {
            MinecraftAddonKind.FabricApi => "fabric-api",
            MinecraftAddonKind.LegacyFabricApi => "legacy-fabric-api",
            MinecraftAddonKind.Qsl => "qsl",
            MinecraftAddonKind.OptiFabric => "optifabric",
            _ => throw new NotSupportedException(kind.ToString())
        };
        var result = await GetModrinthFilesAsync(kind, slug, cancellationToken).ConfigureAwait(false);
        _addonCache[kind] = result;
        return result;
    }

    public static bool IsAddonCompatible(MinecraftAddonFileEntry file, string minecraftVersion, MinecraftLoaderKind? loaderKind = null)
    {
        var versionCompatible = file.GameVersions.Any(v =>
            string.Equals(v, minecraftVersion, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.Replace(" 预览版", "", StringComparison.OrdinalIgnoreCase), minecraftVersion, StringComparison.OrdinalIgnoreCase));
        if (!versionCompatible) return false;
        return loaderKind is null || file.Loaders.Count == 0 || file.Loaders.Contains(loaderKind.Value);
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetFabricLikeVersionsAsync(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var urls = kind switch
        {
            MinecraftLoaderKind.Fabric => new[]
            {
                $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}",
                $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}"
            },
            MinecraftLoaderKind.LegacyFabric => new[]
            {
                $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}"
            },
            MinecraftLoaderKind.Quilt => new[]
            {
                $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}"
            },
            _ => throw new NotSupportedException(kind.ToString())
        };
        var array = await FetchJsonArrayFallbackAsync(urls, cancellationToken).ConfigureAwait(false);
        return array.OfType<JsonObject>()
            .Select(item =>
            {
                var loader = item["loader"] as JsonObject ?? item;
                var version = loader["version"]?.GetValue<string>() ?? item["version"]?.GetValue<string>() ?? string.Empty;
                var stable = loader["stable"]?.GetValue<bool?>() ?? item["stable"]?.GetValue<bool?>() ?? !version.Contains("beta", StringComparison.OrdinalIgnoreCase);
                return new MinecraftLoaderVersionEntry(kind, version, version.Replace("+build", "", StringComparison.OrdinalIgnoreCase),
                    minecraftVersion, stable, false, MinecraftRemoteSource.Official, Metadata: item.DeepClone().AsObject());
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
            .ToArray();
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetForgeVersionsAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}";
        var array = await FetchJsonArrayFallbackAsync([url], cancellationToken).ConfigureAwait(false);
        return array.OfType<JsonObject>()
            .Select(token =>
            {
                var version = token["version"]?.GetValue<string>() ?? string.Empty;
                var branch = token["branch"]?.GetValue<string>();
                var category = "installer";
                string? hash = null;
                if (token["files"] is JsonArray files)
                {
                    var selected = files.OfType<JsonObject>()
                        .FirstOrDefault(file => file["category"]?.GetValue<string>() == "installer" && file["format"]?.GetValue<string>() == "jar") ??
                                   files.OfType<JsonObject>().FirstOrDefault();
                    category = selected?["category"]?.GetValue<string>() ?? category;
                    hash = selected?["hash"]?.GetValue<string>();
                }
                var fileVersion = version + (string.IsNullOrWhiteSpace(branch) ? "" : "-" + branch);
                var fileName = $"{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}/forge-{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}-{category}.jar";
                return new MinecraftLoaderVersionEntry(
                    MinecraftLoaderKind.Forge,
                    version,
                    version,
                    minecraftVersion,
                    true,
                    false,
                    MinecraftRemoteSource.BmclApi,
                    $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                    hash,
                    $"forge-{minecraftVersion}-{fileVersion}-{category}.jar",
                    category,
                    ParseOptionalDate(token["modified"]?.GetValue<string>()),
                    token.DeepClone().AsObject());
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
            .OrderByDescending(entry => ParseVersionForSort(entry.Version))
            .ToArray();
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetNeoForgeVersionsAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var urls = new[]
        {
            "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
            "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"
        };
        var root = await FetchJsonObjectFallbackAsync(urls, cancellationToken).ConfigureAwait(false);
        var versions = ExtractVersionStrings(root)
            .Where(v => IsNeoForgeForMinecraft(v, minecraftVersion))
            .Select(v => new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.NeoForge,
                v,
                v.StartsWith("1.20.1-", StringComparison.Ordinal) ? v["1.20.1-".Length..] : v,
                minecraftVersion,
                !v.Contains("beta", StringComparison.OrdinalIgnoreCase),
                false,
                MinecraftRemoteSource.BmclApi,
                $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{v}/neoforge-{v}-installer.jar",
                FileName: $"neoforge-{v}-installer.jar"))
            .OrderByDescending(entry => ParseVersionForSort(entry.DisplayName))
            .ToArray();
        return versions;
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetCleanroomVersionsAsync(CancellationToken cancellationToken)
    {
        var array = await FetchJsonArrayFallbackAsync(["https://api.github.com/repos/CleanroomMC/Cleanroom/releases"], cancellationToken).ConfigureAwait(false);
        return array.OfType<JsonObject>()
            .Select(item =>
            {
                var tag = item["tag_name"]?.GetValue<string>() ?? string.Empty;
                var fileName = $"cleanroom-{tag}-installer.jar";
                var assetUrl = (item["assets"] as JsonArray)?.OfType<JsonObject>()
                    .FirstOrDefault(asset => string.Equals(asset["name"]?.GetValue<string>(), fileName, StringComparison.OrdinalIgnoreCase))
                    ?["browser_download_url"]?.GetValue<string>();
                return new MinecraftLoaderVersionEntry(
                    MinecraftLoaderKind.Cleanroom,
                    tag,
                    tag,
                    "1.12.2",
                    !tag.Contains("alpha", StringComparison.OrdinalIgnoreCase) && !tag.Contains("beta", StringComparison.OrdinalIgnoreCase),
                    false,
                    MinecraftRemoteSource.Official,
                    assetUrl ?? $"https://github.com/CleanroomMC/Cleanroom/releases/download/{tag}/{fileName}",
                    FileName: fileName,
                    Metadata: item.DeepClone().AsObject());
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
            .ToArray();
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetOptiFineVersionsAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var array = await FetchJsonArrayFallbackAsync(["https://bmclapi2.bangbang93.com/optifine/versionList"], cancellationToken).ConfigureAwait(false);
        return array.OfType<JsonObject>()
            .Where(item => string.Equals(item["mcversion"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var type = item["type"]?.GetValue<string>() ?? "HD_U";
                var patch = item["patch"]?.GetValue<string>() ?? string.Empty;
                var file = item["filename"]?.GetValue<string>() ?? $"OptiFine_{minecraftVersion}_{type}_{patch}.jar";
                var version = $"{type}_{patch}";
                return new MinecraftLoaderVersionEntry(
                    MinecraftLoaderKind.OptiFine,
                    version,
                    $"{minecraftVersion} {patch}".Trim(),
                    minecraftVersion,
                    !patch.Contains("pre", StringComparison.OrdinalIgnoreCase),
                    false,
                    MinecraftRemoteSource.BmclApi,
                    $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersion}/{type}/{patch}",
                    FileName: file,
                    Metadata: item.DeepClone().AsObject());
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLiteLoaderVersionsAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var root = await FetchJsonObjectFallbackAsync([
            "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json",
            "https://dl.liteloader.com/versions/versions.json"
        ], cancellationToken).ConfigureAwait(false);
        if (root["versions"] is not JsonObject versions || versions[minecraftVersion] is not JsonObject versionNode)
            return [];
        var latest = versionNode["artefacts"]?["com.mumfrey:liteloader"]?["latest"] as JsonObject ??
                     versionNode["snapshots"]?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
        if (latest is null) return [];
        var fileName = $"liteloader-installer-{minecraftVersion}{(minecraftVersion is "1.8" or "1.9" ? ".0" : "")}-00-SNAPSHOT.jar";
        return
        [
            new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.LiteLoader,
                latest["version"]?.GetValue<string>() ?? minecraftVersion,
                latest["version"]?.GetValue<string>() ?? minecraftVersion,
                minecraftVersion,
                !string.Equals(latest["stream"]?.GetValue<string>(), "snapshot", StringComparison.OrdinalIgnoreCase),
                false,
                MinecraftRemoteSource.BmclApi,
                $"https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/installer/{minecraftVersion}/{fileName}",
                latest["md5"]?.GetValue<string>(),
                fileName,
                Metadata: latest.DeepClone().AsObject())
        ];
    }

    private static async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLabyModVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var entries = new List<MinecraftLoaderVersionEntry>();
        foreach (var channel in new[] { "production", "snapshot" })
        {
            var manifest = await FetchJsonObjectFallbackAsync([
                $"https://releases.r2.labymod.net/api/v1/manifest/{channel}/latest.json"
            ], cancellationToken).ConfigureAwait(false);
            var minecraftEntry = (manifest["minecraftVersions"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(version => string.Equals(version["version"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase));
            if (minecraftEntry is null) continue;

            var commit = manifest["commitReference"]?.GetValue<string>() ?? string.Empty;
            var labyVersion = manifest["labyModVersion"]?.GetValue<string>() ?? commit;
            var display = channel == "snapshot" ? labyVersion + " 快照版" : labyVersion + " 稳定版";
            var customManifestUrl = minecraftEntry["customManifestUrl"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(customManifestUrl) && !string.IsNullOrWhiteSpace(commit))
                customManifestUrl = $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{channel}/{minecraftVersion}/{commit}.json";

            entries.Add(new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.LabyMod,
                channel,
                display,
                minecraftVersion,
                channel == "production",
                channel == "production",
                MinecraftRemoteSource.Official,
                customManifestUrl,
                ReleaseTime: ParseUnixMilliseconds(manifest["releaseTime"]?.GetValue<long?>()),
                Metadata: new JsonObject
                {
                    ["channel"] = channel,
                    ["commitReference"] = commit,
                    ["labyModVersion"] = labyVersion,
                    ["releaseTime"] = manifest["releaseTime"]?.DeepClone(),
                    ["core"] = manifest.DeepClone()
                }));
        }

        return entries;
    }

    private static async Task<IReadOnlyList<MinecraftAddonFileEntry>> GetModrinthFilesAsync(
        MinecraftAddonKind kind,
        string slug,
        CancellationToken cancellationToken)
    {
        var array = await FetchJsonArrayFallbackAsync([$"https://api.modrinth.com/v2/project/{slug}/version"], cancellationToken).ConfigureAwait(false);
        return array.OfType<JsonObject>()
            .Select(item =>
            {
                var file = (item["files"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(f => f["primary"]?.GetValue<bool?>() == true) ??
                           (item["files"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
                if (file is null) return null;
                var loaders = (item["loaders"] as JsonArray)?.Select(ToLoaderKind).OfType<MinecraftLoaderKind>().ToArray() ?? [];
                var versions = (item["game_versions"] as JsonArray)?.Select(v => v?.GetValue<string>() ?? string.Empty).Where(static v => v.Length > 0).ToArray() ?? [];
                var filename = file["filename"]?.GetValue<string>() ?? slug + ".jar";
                var urls = new[] { file["url"]?.GetValue<string>() }
                    .OfType<string>()
                    .SelectMany(MinecraftResourceResolver.MapUrls)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new MinecraftAddonFileEntry(
                    kind,
                    item["id"]?.GetValue<string>() ?? filename,
                    item["name"]?.GetValue<string>() ?? item["version_number"]?.GetValue<string>() ?? filename,
                    filename,
                    urls,
                    versions,
                    loaders,
                    file["hashes"]?["sha1"]?.GetValue<string>(),
                    file["size"]?.GetValue<long?>(),
                    string.Equals(item["version_type"]?.GetValue<string>(), "release", StringComparison.OrdinalIgnoreCase),
                    ParseOptionalDate(item["date_published"]?.GetValue<string>()),
                    MinecraftRemoteSource.Modrinth);
            })
            .OfType<MinecraftAddonFileEntry>()
            .Where(static file => file.DownloadUrls.Count > 0)
            .ToArray();
    }

    private static MinecraftLoaderKind? ToLoaderKind(JsonNode? node) =>
        node?.GetValue<string>()?.ToLowerInvariant() switch
        {
            "forge" => MinecraftLoaderKind.Forge,
            "neoforge" => MinecraftLoaderKind.NeoForge,
            "fabric" => MinecraftLoaderKind.Fabric,
            "quilt" => MinecraftLoaderKind.Quilt,
            _ => null
        };

    private static async Task<JsonArray> FetchJsonArrayFallbackAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                var text = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(text) as JsonArray ?? throw new InvalidDataException("返回 JSON 不是数组：" + url);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new InvalidOperationException("没有可用下载源。");
    }

    private static async Task<JsonObject> FetchJsonObjectFallbackAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                var text = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("返回 JSON 不是对象：" + url);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new InvalidOperationException("没有可用下载源。");
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("PCL-CE/1.0");
        return await (await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
            .EnsureSuccessStatusCode()
            .Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IEnumerable<string> ExtractVersionStrings(JsonObject root)
    {
        if (root["versions"] is JsonArray versions)
        {
            foreach (var item in versions)
                if (item?.GetValue<string>() is { } value)
                    yield return value;
        }
        foreach (var match in Regex.Matches(root.ToJsonString(), "\"([0-9][^\"]+)\"").Cast<Match>())
            yield return match.Groups[1].Value;
    }

    private static bool IsNeoForgeForMinecraft(string version, string minecraftVersion)
    {
        if (version.StartsWith(minecraftVersion + "-", StringComparison.OrdinalIgnoreCase)) return true;
        if (minecraftVersion == "1.20.1" && version.StartsWith("47.", StringComparison.Ordinal)) return true;
        var parsed = MinecraftVersionNumber.TryParse(minecraftVersion);
        if (parsed is null) return false;

        var minecraft = parsed.Value;
        if (minecraft.Major == 1 && minecraft.Minor >= 20)
            return version.StartsWith($"{minecraft.Minor}.", StringComparison.Ordinal);
        if (minecraft.Major > 1)
            return version.StartsWith($"{minecraft.Major}.", StringComparison.Ordinal);
        return false;
    }

    private static DateTime? ParseOptionalDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date) ? date : null;

    private static DateTime? ParseUnixMilliseconds(long? value) =>
        value is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(value.Value).UtcDateTime;

    private static Version ParseVersionForSort(string value)
    {
        var parts = Regex.Matches(value, "\\d+").Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).Take(4).ToList();
        while (parts.Count < 4) parts.Add(0);
        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }
}

public sealed class MinecraftMergedInstallService(
    MinecraftDownloadService? downloadService = null,
    MinecraftModLoaderCatalogService? catalogService = null)
{
    private readonly MinecraftDownloadService _downloadService = downloadService ?? new MinecraftDownloadService();
    private readonly MinecraftModLoaderCatalogService _catalogService = catalogService ?? new MinecraftModLoaderCatalogService();

    public async Task<MinecraftInstanceInfo> InstallAsync(
        MinecraftMergedInstallRequest request,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        IProgress<MinecraftInstallStageInfo>? stageProgress = null,
        CancellationToken cancellationToken = default)
    {
        request = request with { Selection = MinecraftLoaderCompatibility.Normalize(request.Selection, request.VersionId) };
        var instanceName = request.InstanceName.Trim();
        var instanceDirectory = Path.Combine(request.TargetMinecraftFolder, "versions", instanceName);
        var ignorePath = Path.Combine(instanceDirectory, ".pclignore");
        Directory.CreateDirectory(instanceDirectory);
        await File.WriteAllTextAsync(ignorePath, "用于临时地在 PCL 的实例列表中屏蔽此实例。", cancellationToken).ConfigureAwait(false);

        try
        {
            stageProgress?.Report(new MinecraftInstallStageInfo("prepare", "准备安装", NDlTaskState.Running, 0.02, null));
            var json = await _downloadService.GetVersionJsonAsync(request.VersionUrl, cancellationToken).ConfigureAwait(false);
            json["id"] = instanceName;

            json = await ApplyPrimaryLoadersAsync(json, request, stageProgress, cancellationToken).ConfigureAwait(false);
            json["id"] = instanceName;

            var jsonPath = Path.Combine(instanceDirectory, instanceName + ".json");
            await File.WriteAllTextAsync(jsonPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
            var instance = MinecraftInstanceInfo.Load(jsonPath);

            stageProgress?.Report(new MinecraftInstallStageInfo("files", "下载游戏文件", NDlTaskState.Running, 0.35, null));
            await DownloadInstanceFilesAsync(instance, progress, cancellationToken).ConfigureAwait(false);
            await DownloadAddonsAsync(instance, request.Selection, progress, cancellationToken).ConfigureAwait(false);
            await DownloadOptiFineAsModIfNeededAsync(instance, request, progress, cancellationToken).ConfigureAwait(false);
            CreateInstanceFolders(instance);

            if (request.SaveServerJar && json["downloads"]?["server"] is JsonObject server)
            {
                var serverTarget = Path.Combine(instanceDirectory, instanceName + "-server.jar");
                var serverFile = CreateServerDownload(server, serverTarget, instanceName);
                if (serverFile is not null)
                    await _downloadService.DownloadFilesAsync([serverFile], progress, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(ignorePath)) File.Delete(ignorePath);
            stageProgress?.Report(new MinecraftInstallStageInfo("finish", "安装完成", NDlTaskState.Finished, 1, instanceName));
            return MinecraftInstanceInfo.Load(jsonPath);
        }
        catch
        {
            stageProgress?.Report(new MinecraftInstallStageInfo("failed", "安装失败", NDlTaskState.Failed, 0, null));
            CleanupFailedInstall(instanceDirectory, ignorePath);
            throw;
        }
    }

    public static string BuildDefaultInstanceName(string minecraftVersion, MinecraftMergedLoaderSelection selection)
    {
        var suffixes = selection.Loaders
            .Where(loader => loader.Kind != MinecraftLoaderKind.OptiFine || !selection.HasModLoader)
            .Select(loader => loader.Kind switch
            {
                MinecraftLoaderKind.LegacyFabric => "LegacyFabric_" + loader.DisplayName,
                _ => MinecraftLoaderCatalog.GetDisplayName(loader.Kind) + "_" + loader.DisplayName
            })
            .ToArray();
        if (selection.OptiFine is not null)
            suffixes = suffixes.Append("OptiFine_" + selection.OptiFine.DisplayName.Replace(minecraftVersion, "", StringComparison.OrdinalIgnoreCase).Trim()).ToArray();
        return suffixes.Length == 0 ? minecraftVersion : minecraftVersion + "-" + string.Join("-", suffixes).Replace(' ', '_');
    }

    private async Task<JsonObject> ApplyPrimaryLoadersAsync(
        JsonObject vanillaJson,
        MinecraftMergedInstallRequest request,
        IProgress<MinecraftInstallStageInfo>? stageProgress,
        CancellationToken cancellationToken)
    {
        var selection = request.Selection;
        if (selection.Fabric is not null)
            vanillaJson = await MergeFabricLikeProfileAsync(vanillaJson, request.VersionId, selection.Fabric, cancellationToken).ConfigureAwait(false);
        if (selection.LegacyFabric is not null)
            vanillaJson = await MergeFabricLikeProfileAsync(vanillaJson, request.VersionId, selection.LegacyFabric, cancellationToken).ConfigureAwait(false);
        if (selection.Quilt is not null)
            vanillaJson = await MergeFabricLikeProfileAsync(vanillaJson, request.VersionId, selection.Quilt, cancellationToken).ConfigureAwait(false);

        var forgeLike = selection.Forge ?? selection.NeoForge ?? selection.Cleanroom;
        if (forgeLike is not null)
            vanillaJson = await InstallForgeLikeProfileAsync(vanillaJson, request, forgeLike, stageProgress, cancellationToken).ConfigureAwait(false);

        if (selection.OptiFine is not null && !selection.HasModLoader)
            vanillaJson = await InstallOptiFineProfileAsync(vanillaJson, request, selection.OptiFine, progress: stageProgress, cancellationToken).ConfigureAwait(false);

        if (selection.LiteLoader is not null)
            AddLiteLoaderProfile(vanillaJson, request.VersionId, selection.LiteLoader);
        if (selection.LabyMod is not null)
            await MergeLabyModProfileAsync(vanillaJson, request.VersionId, selection.LabyMod, cancellationToken).ConfigureAwait(false);
        return vanillaJson;
    }

    private static async Task<JsonObject> MergeFabricLikeProfileAsync(
        JsonObject target,
        string minecraftVersion,
        MinecraftLoaderVersionEntry loader,
        CancellationToken cancellationToken)
    {
        var safeVersion = minecraftVersion.Replace("∞", "infinite", StringComparison.Ordinal);
        var urls = loader.Kind switch
        {
            MinecraftLoaderKind.Fabric => new[]
            {
                $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{safeVersion}/{loader.Version}/profile/json",
                $"https://meta.fabricmc.net/v2/versions/loader/{safeVersion}/{loader.Version}/profile/json"
            },
            MinecraftLoaderKind.LegacyFabric => new[] { $"https://meta.legacyfabric.net/v2/versions/loader/{safeVersion}/{loader.Version}/profile/json" },
            MinecraftLoaderKind.Quilt => new[] { $"https://meta.quiltmc.org/v3/versions/loader/{safeVersion}/{loader.Version}/profile/json" },
            _ => throw new NotSupportedException(loader.Kind.ToString())
        };
        var profile = await FetchJsonObjectAsync(urls, cancellationToken).ConfigureAwait(false);
        MergeJson(target, profile);
        return target;
    }

    private async Task<JsonObject> InstallForgeLikeProfileAsync(
        JsonObject vanillaJson,
        MinecraftMergedInstallRequest request,
        MinecraftLoaderVersionEntry loader,
        IProgress<MinecraftInstallStageInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loader.DownloadUrl))
            return vanillaJson;

        progress?.Report(new MinecraftInstallStageInfo("installer", "下载 " + loader.Kind + " 安装器", NDlTaskState.Running, 0.12, loader.DisplayName));
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcl-loader-install", Guid.NewGuid().ToString("N"));
        var installerPath = Path.Combine(tempRoot, loader.FileName ?? loader.Kind + "-installer.jar");
        Directory.CreateDirectory(tempRoot);
        await _downloadService.DownloadFilesAsync([
            new MinecraftDownloadFile("installer:" + loader.Kind + ":" + loader.Version, MinecraftResourceResolver.MapUrls(loader.DownloadUrl), installerPath, Sha1: loader.Sha1, Name: loader.Kind + " installer")
        ], null, cancellationToken).ConfigureAwait(false);

        if (TryReadInstallerVersionJson(installerPath, out var installerJson, out var requiresProcessors))
        {
            if (!requiresProcessors && installerJson is not null)
            {
                MergeJson(vanillaJson, installerJson);
                return vanillaJson;
            }

            progress?.Report(new MinecraftInstallStageInfo("installer", "安装器需要生成依赖，回退运行安装器", NDlTaskState.Running, 0.16, loader.DisplayName));
        }
        else if (loader.Kind == MinecraftLoaderKind.Cleanroom || loader.Kind == MinecraftLoaderKind.Forge && IsLegacyForge(loader.Version))
        {
            return vanillaJson;
        }

        var knownProfiles = SnapshotVersionJsonFiles(request.TargetMinecraftFolder);
        await RunJavaInstallerAsync(installerPath, request.TargetMinecraftFolder, progress, cancellationToken).ConfigureAwait(false);
        var generatedJson = FindGeneratedProfile(request.TargetMinecraftFolder, loader, knownProfiles);
        if (generatedJson is null)
        {
            if (installerJson is not null)
            {
                MergeJson(vanillaJson, installerJson);
                return vanillaJson;
            }

            return vanillaJson;
        }
        var generated = JsonNode.Parse(await File.ReadAllTextAsync(generatedJson, cancellationToken).ConfigureAwait(false))?.AsObject()
                        ?? throw new InvalidDataException("安装器生成的版本 JSON 无效：" + generatedJson);
        MergeJson(vanillaJson, generated);
        return vanillaJson;
    }

    private async Task<JsonObject> InstallOptiFineProfileAsync(
        JsonObject vanillaJson,
        MinecraftMergedInstallRequest request,
        MinecraftLoaderVersionEntry loader,
        IProgress<MinecraftInstallStageInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loader.DownloadUrl)) return vanillaJson;
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcl-optifine-install", Guid.NewGuid().ToString("N"));
        var installerPath = Path.Combine(tempRoot, loader.FileName ?? "OptiFine.jar");
        Directory.CreateDirectory(tempRoot);
        await _downloadService.DownloadFilesAsync([
            new MinecraftDownloadFile("installer:optifine:" + loader.Version, MinecraftResourceResolver.MapUrls(loader.DownloadUrl), installerPath, Sha1: loader.Sha1, Name: "OptiFine installer")
        ], null, cancellationToken).ConfigureAwait(false);
        if (TryReadInstallerVersionJson(installerPath, out var installerJson, out var requiresProcessors) && !requiresProcessors && installerJson is not null)
        {
            MergeJson(vanillaJson, installerJson);
            return vanillaJson;
        }

        var knownProfiles = SnapshotVersionJsonFiles(request.TargetMinecraftFolder);
        await RunJavaInstallerAsync(installerPath, request.TargetMinecraftFolder, progress, cancellationToken).ConfigureAwait(false);
        var generatedJson = FindGeneratedProfile(request.TargetMinecraftFolder, loader, knownProfiles);
        if (generatedJson is null)
        {
            if (installerJson is not null)
                MergeJson(vanillaJson, installerJson);
            return vanillaJson;
        }
        var generated = JsonNode.Parse(await File.ReadAllTextAsync(generatedJson, cancellationToken).ConfigureAwait(false))?.AsObject()
                        ?? throw new InvalidDataException("OptiFine 生成的版本 JSON 无效：" + generatedJson);
        MergeJson(vanillaJson, generated);
        return vanillaJson;
    }

    private static void AddLiteLoaderProfile(JsonObject target, string minecraftVersion, MinecraftLoaderVersionEntry loader)
    {
        target["mainClass"] = "net.minecraft.launchwrapper.Launch";
        var libraries = target["libraries"] as JsonArray ?? [];
        target["libraries"] = libraries;
        if (loader.Metadata?["libraries"] is JsonArray loaderLibraries)
        {
            foreach (var library in loaderLibraries.OfType<JsonObject>())
            {
                var name = library["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;
                AddLibraryIfMissing(libraries, CreateMavenLibraryNode(name, library["url"]?.GetValue<string>()));
            }
        }

        var liteLoaderVersion = loader.Metadata?["version"]?.GetValue<string>() ?? loader.Version;
        var liteLoaderName = $"com.mumfrey:liteloader:{liteLoaderVersion}";
        AddLibraryIfMissing(libraries, CreateMavenLibraryNode(liteLoaderName, "https://bmclapi2.bangbang93.com/maven/"));
        var args = target["minecraftArguments"]?.GetValue<string>() ?? string.Empty;
        var tweakClass = loader.Metadata?["tweakClass"]?.GetValue<string>() ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker";
        var tweakArg = "--tweakClass " + tweakClass;
        if (!args.Contains(tweakArg, StringComparison.Ordinal))
            target["minecraftArguments"] = (args + " " + tweakArg).Trim();
    }

    private static async Task MergeLabyModProfileAsync(
        JsonObject target,
        string minecraftVersion,
        MinecraftLoaderVersionEntry loader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loader.DownloadUrl)) return;
        try
        {
            var profile = await FetchJsonObjectAsync([loader.DownloadUrl], cancellationToken).ConfigureAwait(false);
            profile.Remove("releaseTime");
            profile.Remove("time");
            MergeJson(target, profile);

            var channel = loader.Version;
            var manifest = await FetchJsonObjectAsync([
                $"https://releases.r2.labymod.net/api/v1/manifest/{channel}/latest.json"
            ], cancellationToken).ConfigureAwait(false);
            var commit = manifest["commitReference"]?.GetValue<string>() ??
                         loader.Metadata?["commitReference"]?.GetValue<string>();
            var version = manifest["labyModVersion"]?.GetValue<string>() ??
                          loader.Metadata?["labyModVersion"]?.GetValue<string>() ??
                          loader.DisplayName;
            target["mainClass"] = "net.labymod.core.main.LabyModLauncher";
            target["labymod_data"] = new JsonObject
            {
                ["channelType"] = channel,
                ["commitReference"] = commit,
                ["version"] = version,
                ["versionType"] = channel == "snapshot" ? "snapshot" : "release"
            };

            var libraries = target["libraries"] as JsonArray ?? [];
            target["libraries"] = libraries;
            AddLabyModLibraries(libraries, await TryFetchLabyModLibrariesAsync(channel, cancellationToken).ConfigureAwait(false));
            AddLibraryIfMissing(libraries, new JsonObject
            {
                ["name"] = "net.labymod:LabyMod:4",
                ["downloads"] = new JsonObject
                {
                    ["artifact"] = new JsonObject
                    {
                        ["path"] = "net/labymod/LabyMod/4/LabyMod-4.jar",
                        ["sha1"] = manifest["sha1"]?.DeepClone(),
                        ["size"] = manifest["size"]?.DeepClone(),
                        ["url"] = string.IsNullOrWhiteSpace(commit)
                            ? null
                            : $"https://releases.r2.labymod.net/api/v1/download/labymod4/{channel}/{commit}.jar"
                    }
                }
            });
        }
        catch
        {
            target["mainClass"] = target["mainClass"]?.DeepClone() ?? "net.minecraft.client.main.Main";
            target["labyModChannel"] = loader.Version;
            target["inheritsFrom"] = minecraftVersion;
        }
    }

    private async Task DownloadInstanceFilesAsync(
        MinecraftInstanceInfo instance,
        IProgress<MinecraftDownloadTaskInfo>? progress,
        CancellationToken cancellationToken)
    {
        var files = MinecraftResourceResolver.GetRequiredFiles(instance, includeAssets: false).ToList();
        await _downloadService.DownloadFilesAsync(files, progress, cancellationToken).ConfigureAwait(false);
        var assetIndexFiles = MinecraftResourceResolver.GetAssetFiles(instance).Take(1).ToArray();
        await _downloadService.DownloadFilesAsync(assetIndexFiles, progress, cancellationToken).ConfigureAwait(false);
        var assetFiles = MinecraftResourceResolver.GetAssetFiles(instance).Skip(1).ToArray();
        await _downloadService.DownloadFilesAsync(assetFiles, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadAddonsAsync(
        MinecraftInstanceInfo instance,
        MinecraftMergedLoaderSelection selection,
        IProgress<MinecraftDownloadTaskInfo>? progress,
        CancellationToken cancellationToken)
    {
        var modsFolder = Path.Combine(instance.GameDirectory, "mods");
        Directory.CreateDirectory(modsFolder);
        var files = selection.Addons.Select(addon => new MinecraftDownloadFile(
            "addon:" + addon.Kind + ":" + addon.Id,
            addon.DownloadUrls,
            Path.Combine(modsFolder, addon.FileName),
            addon.Size,
            addon.Sha1,
            addon.DisplayName)).ToArray();
        await _downloadService.DownloadFilesAsync(files, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadOptiFineAsModIfNeededAsync(
        MinecraftInstanceInfo instance,
        MinecraftMergedInstallRequest request,
        IProgress<MinecraftDownloadTaskInfo>? progress,
        CancellationToken cancellationToken)
    {
        var optiFine = request.Selection.OptiFine;
        if (optiFine is null || !request.Selection.HasModLoader || string.IsNullOrWhiteSpace(optiFine.DownloadUrl)) return;
        var modsFolder = Path.Combine(instance.GameDirectory, "mods");
        Directory.CreateDirectory(modsFolder);
        await _downloadService.DownloadFilesAsync([
            new MinecraftDownloadFile(
                "addon:optifine:" + optiFine.Version,
                MinecraftResourceResolver.MapUrls(optiFine.DownloadUrl),
                Path.Combine(modsFolder, optiFine.FileName ?? "OptiFine.jar"),
                Sha1: optiFine.Sha1,
                Name: optiFine.DisplayName)
        ], progress, cancellationToken).ConfigureAwait(false);
    }

    private static void CreateInstanceFolders(MinecraftInstanceInfo instance)
    {
        Directory.CreateDirectory(Path.Combine(instance.GameDirectory, "mods"));
        Directory.CreateDirectory(Path.Combine(instance.GameDirectory, "resourcepacks"));
    }

    private static void CleanupFailedInstall(string instanceDirectory, string ignorePath)
    {
        try
        {
            if (File.Exists(ignorePath)) File.Delete(ignorePath);
            if (!Directory.Exists(instanceDirectory)) return;
            var keepsUserData = Directory.Exists(Path.Combine(instanceDirectory, "saves")) ||
                                Directory.Exists(Path.Combine(instanceDirectory, "mods")) ||
                                File.Exists(Path.Combine(instanceDirectory, "server.dat"));
            if (!keepsUserData) Directory.Delete(instanceDirectory, true);
        }
        catch
        {
            // Best effort cleanup. The original exception is more useful to callers.
        }
    }

    private static async Task RunJavaInstallerAsync(
        string installerPath,
        string minecraftFolder,
        IProgress<MinecraftInstallStageInfo>? progress,
        CancellationToken cancellationToken)
    {
        var javaExe = await ResolveJavaExeAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(new MinecraftInstallStageInfo("installer", "运行安装器", NDlTaskState.Running, 0.18, Path.GetFileName(installerPath)));
        var startInfo = new ProcessStartInfo(javaExe)
        {
            WorkingDirectory = minecraftFolder,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(installerPath);
        startInfo.ArgumentList.Add("--installClient");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("安装器进程未能创建。");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("安装器运行失败：" + error);
        }
    }

    private static async Task<string> ResolveJavaExeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (JavaService.JavaManager is not null)
            {
                await JavaService.JavaManager.ScanJavaAsync().ConfigureAwait(false);
                var java = JavaService.JavaManager.GetSortedJavaList().FirstOrDefault();
                if (java?.Installation.JavaExePath is { } path && File.Exists(path)) return path;
            }
        }
        catch
        {
            // Fall back to PATH java below.
        }
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private static Dictionary<string, DateTime> SnapshotVersionJsonFiles(string minecraftFolder)
    {
        var versions = Path.Combine(minecraftFolder, "versions");
        if (!Directory.Exists(versions)) return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(versions, "*.json", SearchOption.AllDirectories)
            .ToDictionary(Path.GetFullPath, File.GetLastWriteTimeUtc, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindGeneratedProfile(
        string minecraftFolder,
        MinecraftLoaderVersionEntry loader,
        IReadOnlyDictionary<string, DateTime> knownProfiles)
    {
        var versions = Path.Combine(minecraftFolder, "versions");
        if (!Directory.Exists(versions)) return null;
        var needles = loader.Kind switch
        {
            MinecraftLoaderKind.Forge => new[] { "forge-" + loader.Version, "forge" },
            MinecraftLoaderKind.NeoForge => new[] { "neoforge-" + loader.Version, "neoforge" },
            MinecraftLoaderKind.Cleanroom => new[] { "cleanroom-" + loader.Version, "cleanroom" },
            MinecraftLoaderKind.OptiFine => new[] { "optifine", "OptiFine" },
            _ => [loader.Version]
        };
        return Directory.EnumerateFiles(versions, "*.json", SearchOption.AllDirectories)
            .Where(path => needles.Any(needle => path.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            .Where(path =>
            {
                var fullPath = Path.GetFullPath(path);
                return !knownProfiles.TryGetValue(fullPath, out var previousWrite) ||
                       File.GetLastWriteTimeUtc(path) > previousWrite;
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool TryReadInstallerVersionJson(string installerPath, out JsonObject? versionJson, out bool requiresProcessors)
    {
        versionJson = null;
        requiresProcessors = false;
        using var archive = ZipFile.OpenRead(installerPath);
        var entry = archive.GetEntry("version.json") ?? archive.GetEntry("install_profile.json");
        if (entry is null) return false;
        using var stream = entry.Open();
        var source = JsonNode.Parse(stream) as JsonObject;
        if (source is null) return false;
        requiresProcessors = source["processors"] is JsonArray processors && processors.Count > 0;
        versionJson = (source["versionInfo"] as JsonObject ?? source).DeepClone().AsObject();
        return true;
    }

    private static bool IsLegacyForge(string version)
    {
        var major = version.Split('.').FirstOrDefault();
        return int.TryParse(major, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed < 20;
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

    private static async Task<JsonObject> FetchJsonObjectAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        Exception? last = null;
        using var http = new HttpClient();
        foreach (var url in urls)
        {
            try
            {
                var text = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("JSON 无效：" + url);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new InvalidOperationException("没有可用下载源。");
    }

    private static async Task<JsonObject?> TryFetchLabyModLibrariesAsync(string channel, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchJsonObjectAsync([
                $"https://releases.r2.labymod.net/api/v1/libraries/{channel}.json"
            ], cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static void AddLabyModLibraries(JsonArray targetLibraries, JsonObject? source)
    {
        if (source is null) return;
        if (source["libraries"] is not JsonArray libraries) return;
        foreach (var library in libraries.OfType<JsonObject>())
        {
            var name = library["name"]?.GetValue<string>();
            var url = library["url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
            var marker = "https://releases.r2.labymod.net/libraries/";
            var path = url.Contains(marker, StringComparison.OrdinalIgnoreCase)
                ? url[(url.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length)..]
                : MinecraftResourceResolver.MavenNameToPath(name)?.Replace(Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrWhiteSpace(path)) continue;
            AddLibraryIfMissing(targetLibraries, new JsonObject
            {
                ["name"] = name,
                ["downloads"] = new JsonObject
                {
                    ["artifact"] = new JsonObject
                    {
                        ["path"] = path,
                        ["sha1"] = library["sha1"]?.DeepClone(),
                        ["size"] = library["size"]?.DeepClone(),
                        ["url"] = url
                    }
                }
            });
        }
    }

    private static void AddLibraryIfMissing(JsonArray libraries, JsonObject library)
    {
        if (!ContainsLibrary(libraries, library))
            libraries.Add(library);
    }

    private static JsonObject CreateMavenLibraryNode(string name, string? baseUrl)
    {
        var path = MinecraftResourceResolver.MavenNameToPath(name)?.Replace(Path.DirectorySeparatorChar, '/');
        var urlBase = string.IsNullOrWhiteSpace(baseUrl)
            ? (name.StartsWith("net.minecraft:", StringComparison.OrdinalIgnoreCase)
                ? "https://libraries.minecraft.net/"
                : "https://bmclapi2.bangbang93.com/maven/")
            : baseUrl;
        if (!urlBase.EndsWith("/", StringComparison.Ordinal)) urlBase += "/";
        return new JsonObject
        {
            ["name"] = name,
            ["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = path,
                    ["url"] = path is null ? null : urlBase + path
                }
            }
        };
    }

    private static void MergeJson(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToArray())
        {
            if (value is null) continue;
            if (key == "libraries" && target[key] is JsonArray targetLibraries && value is JsonArray sourceLibraries)
            {
                foreach (var library in sourceLibraries)
                    if (!ContainsLibrary(targetLibraries, library))
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

    private static bool ContainsLibrary(JsonArray libraries, JsonNode? candidate)
    {
        var name = candidate?["name"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(name) &&
               libraries.OfType<JsonObject>().Any(lib => string.Equals(lib["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase));
    }
}
