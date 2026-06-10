using Microsoft.Extensions.Logging;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelStateMachineFactory(
    IPixelEventBus eventBus,
    ILoggerFactory loggerFactory)
    : IPixelStateMachineFactory
{
    public PixelStateMachine<TState, TTrigger> Create<TState, TTrigger>(
        string name,
        TState initialState)
        where TState : notnull
        where TTrigger : notnull =>
        new(name, initialState, eventBus, loggerFactory.CreateLogger<PixelStateMachine<TState, TTrigger>>());
}

