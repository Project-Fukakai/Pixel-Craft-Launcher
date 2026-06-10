using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace PCL.Core.App.Pixel.Events;

public sealed class ReactivePixelEventBus : IPixelEventBus, IDisposable
{
    private readonly ISubject<object> _events = Subject.Synchronize(new Subject<object>());
    private bool _disposed;

    public void Publish<TEvent>(TEvent value) where TEvent : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _events.OnNext(value);
    }

    public IObservable<TEvent> Observe<TEvent>() where TEvent : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _events.OfType<TEvent>();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _events.OnCompleted();
        if (_events is IDisposable disposable)
            disposable.Dispose();
    }
}

