using System;

namespace PCL.Core.App.Pixel.Events;

public interface IPixelEventBus
{
    void Publish<TEvent>(TEvent value) where TEvent : notnull;

    IObservable<TEvent> Observe<TEvent>() where TEvent : notnull;
}

