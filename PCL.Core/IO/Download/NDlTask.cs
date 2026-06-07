using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download;

public enum NDlTaskState
{
    Waiting,
    Running,
    Finished,
    Failed,
    Cancelled
}

public sealed record NDlRequest(
    string Id,
    IReadOnlyList<string> Urls,
    string TargetPath,
    long? Size = null,
    string? Sha1 = null,
    string? DisplayName = null);

public sealed record NDlProgress(
    string Id,
    NDlTaskState State,
    long DownloadedBytes,
    long? TotalBytes,
    double Progress,
    long SpeedBytesPerSecond,
    string? Message = null);

public class NDlTask
{
    private static readonly HttpClient Http = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _syncRoot = new();

    public NDlTask(NDlRequest request)
    {
        Request = request;
        State = NDlTaskState.Waiting;
    }

    public NDlRequest Request { get; }
    public NDlTaskState State { get; private set; }
    public long DownloadedBytes { get; private set; }
    public long? TotalBytes { get; private set; }
    public long SpeedBytesPerSecond { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event EventHandler<NDlProgress>? ProgressChanged;

    public void Cancel() => _cancellation.Cancel();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, cancellationToken);
        var token = linked.Token;
        SetState(NDlTaskState.Running, "开始下载");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Request.TargetPath) ?? ".");
            var tempPath = Request.TargetPath + ".pcldownload";
            Exception? lastException = null;

            foreach (var url in Request.Urls)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await DownloadFromUrlAsync(url, tempPath, token).ConfigureAwait(false);
                    await VerifyAsync(tempPath, token).ConfigureAwait(false);
                    if (File.Exists(Request.TargetPath))
                        File.Delete(Request.TargetPath);
                    File.Move(tempPath, Request.TargetPath);
                    SetState(NDlTaskState.Finished, "下载完成");
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TryDelete(tempPath);
                    Publish("下载源失败，尝试下一个源：" + ex.Message);
                }
            }

            throw lastException ?? new IOException("没有可用下载源。");
        }
        catch (OperationCanceledException)
        {
            TryDelete(Request.TargetPath + ".pcldownload");
            SetState(NDlTaskState.Cancelled, "已取消");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            TryDelete(Request.TargetPath + ".pcldownload");
            SetState(NDlTaskState.Failed, ex.Message);
        }
    }

    private async Task DownloadFromUrlAsync(string url, string tempPath, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        TotalBytes = Request.Size ?? response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 128 * 1024, true);
        var buffer = new byte[128 * 1024];
        var stopwatch = Stopwatch.StartNew();
        var lastTicks = 0L;
        var lastBytes = 0L;
        DownloadedBytes = 0;
        SpeedBytesPerSecond = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            DownloadedBytes += read;
            if (stopwatch.ElapsedMilliseconds - lastTicks >= 200)
            {
                var elapsed = Math.Max(stopwatch.ElapsedMilliseconds - lastTicks, 1);
                SpeedBytesPerSecond = (long)((DownloadedBytes - lastBytes) * 1000d / elapsed);
                lastTicks = stopwatch.ElapsedMilliseconds;
                lastBytes = DownloadedBytes;
                Publish();
            }
        }

        SpeedBytesPerSecond = 0;
        Publish();
    }

    private async Task VerifyAsync(string path, CancellationToken cancellationToken)
    {
        if (Request.Size is { } expectedSize && new FileInfo(path).Length != expectedSize)
            throw new InvalidDataException($"文件大小校验失败：期望 {expectedSize}，实际 {new FileInfo(path).Length}");

        if (string.IsNullOrWhiteSpace(Request.Sha1))
            return;

        await using var stream = File.OpenRead(path);
        var hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, Request.Sha1, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA1 校验失败：{actual}");
    }

    private void SetState(NDlTaskState state, string? message)
    {
        lock (_syncRoot)
        {
            State = state;
        }

        Publish(message);
    }

    private void Publish(string? message = null)
    {
        var total = TotalBytes;
        var progress = total is > 0 ? Math.Clamp(DownloadedBytes / (double)total.Value, 0d, 1d) : 0d;
        ProgressChanged?.Invoke(this, new NDlProgress(
            Request.Id,
            State,
            DownloadedBytes,
            total,
            progress,
            SpeedBytesPerSecond,
            message));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
