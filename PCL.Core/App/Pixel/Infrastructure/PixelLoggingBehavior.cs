using MediatR;
using Microsoft.Extensions.Logging;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelLoggingBehavior<TRequest, TResponse>(
    ILogger<PixelLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        using var scope = logger.BeginScope("PixelCommand:{Command}", requestName);
        logger.LogDebug("Pixel command started");
        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Pixel command completed");
            return response;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Pixel command cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pixel command failed");
            throw;
        }
    }
}

