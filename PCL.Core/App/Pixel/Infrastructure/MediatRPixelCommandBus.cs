using MediatR;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class MediatRPixelCommandBus(IMediator mediator) : IPixelCommandBus
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        mediator.Send(request, cancellationToken);

    public Task Send(IRequest request, CancellationToken cancellationToken = default) =>
        mediator.Send(request, cancellationToken);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        mediator.Publish(notification, cancellationToken);
}

