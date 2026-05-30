using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace PCL.Core.UI.Animation.UIAccessProvider;

public sealed class WpfUIAccessProvider(Dispatcher dispatcher) : IUIAccessProvider
{
    private readonly Dispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Post(action, DispatcherPriority.Send);
    }

    public Task InvokeAsync(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _dispatcher.Post(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Send);
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (_dispatcher.CheckAccess()) return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>();
        _dispatcher.Post(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Send);
        return tcs.Task;
    }

    public Task InvokeAsync(Func<Task> func)
    {
        if (_dispatcher.CheckAccess()) return func();

        var tcs = new TaskCompletionSource();
        _dispatcher.Post(async () =>
        {
            try
            {
                await func().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Send);
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (_dispatcher.CheckAccess()) return func();

        var tcs = new TaskCompletionSource<T>();
        _dispatcher.Post(async () =>
        {
            try
            {
                tcs.SetResult(await func().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Send);
        return tcs.Task;
    }

    public event EventHandler FrameTick
    {
        add
        {
            _frameTimer.Tick += value;
            if (!_frameTimer.IsEnabled) _frameTimer.Start();
        }
        remove => _frameTimer.Tick -= value;
    }
}
