using System.Collections.Generic;
using System.Reactive.Disposables;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel;

public sealed class PixelSettingsObservationService(IPixelEventBus? eventBus = null)
{
    public IDisposable Observe(string key, Action<PixelSettingChangedEvent> handler)
    {
        return Observe([key], handler);
    }

    public IDisposable Observe(IEnumerable<string> keys, Action<PixelSettingChangedEvent> handler)
    {
        var disposables = new CompositeDisposable();
        foreach (var key in keys)
        {
            if (!PixelSettingsBinder.TryGetItem(key, out var item))
                continue;

            ConfigObserver? observer = null;
            observer = PixelSettingsBinder.ObserveChanged(key, value =>
            {
                var change = new PixelSettingChangedEvent(key, value);
                eventBus?.Publish(change);
                handler(change);
            });

            if (observer is not null)
                disposables.Add(Disposable.Create(() => item.Unobserve(observer)));
        }

        return disposables;
    }
}

public sealed record PixelSettingChangedEvent(string Key, object? Value);
