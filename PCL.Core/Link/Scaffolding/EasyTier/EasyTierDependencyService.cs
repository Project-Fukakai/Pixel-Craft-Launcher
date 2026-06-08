using PCL.Core.App;
using PCL.Core.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Scaffolding.EasyTier;

public enum EasyTierDependencyState
{
    NotInstalled,
    Installing,
    Installed,
    Failed,
    Unsupported
}

public static class EasyTierDependencyService
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static readonly HttpClient HttpClient = new();

    public static EasyTierDependencyState State { get; private set; } =
        EasyTierMetadata.IsPlatformSupported
            ? EasyTierMetadata.IsInstalled() ? EasyTierDependencyState.Installed : EasyTierDependencyState.NotInstalled
            : EasyTierDependencyState.Unsupported;

    public static string? LastError { get; private set; }

    public static event Action<EasyTierDependencyState, string?>? StateChanged;

    public static bool IsReady => EasyTierMetadata.IsPlatformSupported && EasyTierMetadata.IsInstalled();

    public static async Task<bool> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (!EasyTierMetadata.IsPlatformSupported)
        {
            SetState(EasyTierDependencyState.Unsupported, EasyTierMetadata.GetUnsupportedReason());
            return false;
        }

        if (EasyTierMetadata.IsInstalled())
        {
            SetState(EasyTierDependencyState.Installed);
            return true;
        }

        await InstallLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (EasyTierMetadata.IsInstalled())
            {
                SetState(EasyTierDependencyState.Installed);
                return true;
            }

            SetState(EasyTierDependencyState.Installing);
            Directory.CreateDirectory(EasyTierMetadata.EasyTierFilePath);

            var tempRoot = Path.Combine(Paths.Temp, "EasyTier");
            Directory.CreateDirectory(tempRoot);
            var archivePath = Path.Combine(tempRoot, EasyTierMetadata.Platform.ArchiveName);

            await DownloadArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
            ExtractArchive(archivePath);
            await FixExecutablePermissionsAsync(cancellationToken).ConfigureAwait(false);
            CleanupOldVersions();

            if (!EasyTierMetadata.IsInstalled())
                throw new FileNotFoundException("EasyTier 文件不完整。");

            SetState(EasyTierDependencyState.Installed);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "EasyTier", "安装 EasyTier 依赖失败。");
            SetState(EasyTierDependencyState.Failed, ex.Message);
            return false;
        }
        finally
        {
            InstallLock.Release();
        }
    }

    private static async Task DownloadArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        foreach (var url in EasyTierMetadata.Platform.DownloadUrls)
        {
            try
            {
                LogWrapper.Info("EasyTier", $"下载 EasyTier: {url}");
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(archivePath);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                LogWrapper.Warn(ex, "EasyTier", $"下载源不可用: {url}");
            }
        }

        throw new InvalidOperationException("所有 EasyTier 下载源均不可用。", lastException);
    }

    private static void ExtractArchive(string archivePath)
    {
        if (Directory.Exists(EasyTierMetadata.EasyTierFilePath))
            Directory.Delete(EasyTierMetadata.EasyTierFilePath, true);

        Directory.CreateDirectory(EasyTierMetadata.EasyTierFilePath);
        ZipFile.ExtractToDirectory(archivePath, EasyTierMetadata.EasyTierFilePath, true);
        TryFlattenSingleNestedDirectory();

        try
        {
            File.Delete(archivePath);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "EasyTier", "清理 EasyTier 下载缓存失败。");
        }
    }

    private static void TryFlattenSingleNestedDirectory()
    {
        if (EasyTierMetadata.IsInstalled())
            return;

        var dirs = Directory.GetDirectories(EasyTierMetadata.EasyTierFilePath);
        if (dirs.Length != 1)
            return;

        foreach (var file in Directory.GetFiles(dirs[0], "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dirs[0], file);
            var target = Path.Combine(EasyTierMetadata.EasyTierFilePath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target, true);
        }

        Directory.Delete(dirs[0], true);
    }

    private static async Task FixExecutablePermissionsAsync(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        foreach (var executable in new[] { EasyTierMetadata.CoreExecutablePath, EasyTierMetadata.CliExecutablePath })
        {
            if (!File.Exists(executable))
                continue;

            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{executable}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (chmod is not null)
                await chmod.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void CleanupOldVersions()
    {
        var easyTierRoot = Path.Combine(Paths.SharedLocalData, "EasyTier");
        if (!Directory.Exists(easyTierRoot))
            return;

        foreach (var folder in Directory.GetDirectories(easyTierRoot))
        {
            if (string.Equals(Path.GetFileName(folder), EasyTierMetadata.CurrentEasyTierVer, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "EasyTier", $"清理旧版 EasyTier 失败: {folder}");
            }
        }
    }

    private static void SetState(EasyTierDependencyState state, string? error = null)
    {
        State = state;
        LastError = error;
        StateChanged?.Invoke(state, error);
    }
}
