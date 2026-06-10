using System.Diagnostics;
using PCL.Core.App.Configuration;

namespace PCL.Core.App.Pixel.Infrastructure;

public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action action);

    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
}

public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action action) => action();

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }
}

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message, bool isWarning = false, CancellationToken cancellationToken = default);
}

public sealed class NullDialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message, bool isWarning = false, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public interface IFilePickerService
{
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default);
}

public sealed class NullFilePickerService : IFilePickerService
{
    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

public interface IExternalProcessService
{
    void OpenPath(string path);

    void OpenUrl(string url);
}

public sealed class ExternalProcessService : IExternalProcessService
{
    public void OpenPath(string path) =>
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

    public void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
}

public interface IPixelOperationDelayService
{
    Task DelayIfNeededAsync(string operation, CancellationToken cancellationToken = default);
}

public sealed class PixelOperationDelayService : IPixelOperationDelayService
{
    public Task DelayIfNeededAsync(string operation, CancellationToken cancellationToken = default) =>
        DebugSettingsService.DelayIfNeededAsync(operation, cancellationToken);
}

public sealed class NoOpPixelOperationDelayService : IPixelOperationDelayService
{
    public Task DelayIfNeededAsync(string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
