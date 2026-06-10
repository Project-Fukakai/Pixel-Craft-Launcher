using System.Diagnostics;
using MediatR;
using PCL.Core.App.Pixel.Events;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record KillMinecraftProcessCommand(Process Process, string Reason) : IRequest<bool>;

public sealed record MinecraftProcessKillRequestedEvent(int ProcessId, string Reason);

public sealed record MinecraftProcessKillFailedEvent(int ProcessId, string Reason, string Message);

public sealed class KillMinecraftProcessCommandHandler(IPixelEventBus eventBus)
    : IRequestHandler<KillMinecraftProcessCommand, bool>
{
    public Task<bool> Handle(KillMinecraftProcessCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var processId = SafeProcessId(request.Process);
        eventBus.Publish(new MinecraftProcessKillRequestedEvent(processId, request.Reason));
        try
        {
            if (request.Process.HasExited)
                return Task.FromResult(false);

            request.Process.Kill(entireProcessTree: true);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            eventBus.Publish(new MinecraftProcessKillFailedEvent(processId, request.Reason, ex.Message));
            return Task.FromResult(false);
        }
    }

    private static int SafeProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }
}

