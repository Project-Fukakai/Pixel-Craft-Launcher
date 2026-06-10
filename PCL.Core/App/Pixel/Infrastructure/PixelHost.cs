using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelHost(IServiceProvider services, ILogger<PixelHost> logger) : IDisposable
{
    private bool _started;
    private bool _disposed;

    public IServiceProvider Services { get; } = services;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_started)
            return Task.CompletedTask;

        _started = true;
        logger.LogInformation("Pixel host started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_started)
            return Task.CompletedTask;

        _started = false;
        logger.LogInformation("Pixel host stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (Services is IDisposable disposable)
            disposable.Dispose();
        else if (Services is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
