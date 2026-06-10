using System.IO;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft;

public sealed class MinecraftCorePackageSaver(MinecraftDownloadService? downloadService = null)
{
    private readonly MinecraftDownloadService _downloadService = downloadService ?? new MinecraftDownloadService();

    public async Task<string> SaveClientCoreAsync(
        MinecraftVersionManifestEntry version,
        string baseFolder,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versionFolder = Path.Combine(baseFolder, version.Id);
        Directory.CreateDirectory(versionFolder);
        var jsonPath = Path.Combine(versionFolder, version.Id + ".json");
        var jarPath = Path.Combine(versionFolder, version.Id + ".jar");

        await _downloadService.DownloadFilesAsync([
            new MinecraftDownloadFile(
                "save-json:" + version.Id,
                [version.Url],
                jsonPath,
                Name: "下载实例 JSON 文件")
        ], progress, cancellationToken).ConfigureAwait(false);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false))?.AsObject()
                   ?? throw new InvalidDataException("版本 JSON 无效。");
        var clientFile = CreateVersionDownloadFile(json["downloads"]?["client"] as JsonObject, jarPath, "下载核心 JAR 文件", "save-client:" + version.Id)
                         ?? throw new InvalidDataException("版本 JSON 中没有客户端 JAR 下载地址。");

        await _downloadService.DownloadFilesAsync([clientFile], progress, cancellationToken).ConfigureAwait(false);
        return versionFolder;
    }

    public async Task<string> SaveServerJarAsync(
        MinecraftVersionManifestEntry version,
        string baseFolder,
        IProgress<MinecraftDownloadTaskInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versionFolder = Path.Combine(baseFolder, version.Id);
        Directory.CreateDirectory(versionFolder);
        var jsonPath = Path.Combine(versionFolder, version.Id + ".json");
        var serverPath = Path.Combine(versionFolder, version.Id + "-server.jar");

        await _downloadService.DownloadFilesAsync([
            new MinecraftDownloadFile(
                "server-json:" + version.Id,
                [version.Url],
                jsonPath,
                Name: "下载实例 JSON 文件")
        ], progress, cancellationToken).ConfigureAwait(false);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false))?.AsObject()
                   ?? throw new InvalidDataException("版本 JSON 无效。");
        var serverFile = CreateVersionDownloadFile(json["downloads"]?["server"] as JsonObject, serverPath, "下载服务端文件", "save-server:" + version.Id)
                         ?? throw new InvalidDataException($"Mojang 没有给 Minecraft {version.Id} 提供官方服务端下载。");

        await MinecraftServerLaunchScriptWriter.WriteAsync(versionFolder, version.Id, cancellationToken).ConfigureAwait(false);
        await _downloadService.DownloadFilesAsync([serverFile], progress, cancellationToken).ConfigureAwait(false);
        TryDelete(jsonPath);
        return versionFolder;
    }

    private static MinecraftDownloadFile? CreateVersionDownloadFile(JsonObject? node, string targetPath, string name, string id)
    {
        var url = node?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return new MinecraftDownloadFile(
            id,
            [url],
            targetPath,
            node?["size"]?.GetValue<long?>(),
            node?["sha1"]?.GetValue<string>(),
            name);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
