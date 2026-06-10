using Microsoft.Extensions.Logging;
using PCL.Core.App.Pixel.Events;
using Stateless;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelStateMachine<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly IPixelEventBus _eventBus;
    private readonly ILogger _logger;
    private readonly StateMachine<TState, TTrigger> _inner;

    internal PixelStateMachine(
        string name,
        TState initialState,
        IPixelEventBus eventBus,
        ILogger logger)
    {
        Name = name;
        _eventBus = eventBus;
        _logger = logger;
        _inner = new StateMachine<TState, TTrigger>(initialState);
        _inner.OnTransitioned(OnTransitioned);
    }

    public string Name { get; }

    public TState State => _inner.State;

    public StateMachine<TState, TTrigger>.StateConfiguration Configure(TState state) => _inner.Configure(state);

    public bool CanFire(TTrigger trigger) => _inner.CanFire(trigger);

    public void Fire(TTrigger trigger) => _inner.Fire(trigger);

    public Task FireAsync(TTrigger trigger) => _inner.FireAsync(trigger);

    private void OnTransitioned(StateMachine<TState, TTrigger>.Transition transition)
    {
        _logger.LogDebug(
            "Pixel state machine {MachineName} transitioned {SourceState} -> {DestinationState} by {Trigger}",
            Name,
            transition.Source,
            transition.Destination,
            transition.Trigger);

        _eventBus.Publish(new PixelStateChangedEvent(
            Name,
            transition.Source.ToString() ?? string.Empty,
            transition.Destination.ToString() ?? string.Empty,
            transition.Trigger.ToString() ?? string.Empty));
    }
}
