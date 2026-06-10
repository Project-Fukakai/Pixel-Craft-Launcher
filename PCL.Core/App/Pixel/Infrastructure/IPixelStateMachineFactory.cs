namespace PCL.Core.App.Pixel.Infrastructure;

public interface IPixelStateMachineFactory
{
    PixelStateMachine<TState, TTrigger> Create<TState, TTrigger>(
        string name,
        TState initialState)
        where TState : notnull
        where TTrigger : notnull;
}

