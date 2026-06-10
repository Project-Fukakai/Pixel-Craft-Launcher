using System.Diagnostics;
using MediatR;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record MonitorMinecraftProcessCommand(
    Process Process,
    string InstanceName,
    Action<bool>? WindowDetected = null,
    Action? WindowProbeTimedOut = null)
    : IRequest<MinecraftProcessExitInfo>;

public sealed record MinecraftProcessWindowDetectedEvent(int ProcessId, string InstanceName);

public sealed record MinecraftProcessWindowProbeTimedOutEvent(int ProcessId, string InstanceName);

public sealed record MinecraftProcessExitedEvent(PixelGameExitedSnapshot ExitInfo);

public sealed class MonitorMinecraftProcessCommandHandler(
    MinecraftProcessMonitor processMonitor,
    IPixelEventBus eventBus)
    : IRequestHandler<MonitorMinecraftProcessCommand, MinecraftProcessExitInfo>
{
    async Task<MinecraftProcessExitInfo> IRequestHandler<MonitorMinecraftProcessCommand, MinecraftProcessExitInfo>.Handle(
        MonitorMinecraftProcessCommand request,
        CancellationToken cancellationToken)
    {
        var processId = SafeProcessId(request.Process);
        var exitInfo = await processMonitor.MonitorAsync(
            request.Process,
            request.InstanceName,
            detected =>
            {
                if (detected)
                    eventBus.Publish(new MinecraftProcessWindowDetectedEvent(processId, request.InstanceName));
                request.WindowDetected?.Invoke(detected);
            },
            () =>
            {
                eventBus.Publish(new MinecraftProcessWindowProbeTimedOutEvent(processId, request.InstanceName));
                request.WindowProbeTimedOut?.Invoke();
            },
            cancellationToken).ConfigureAwait(false);

        eventBus.Publish(new MinecraftProcessExitedEvent(PixelGameExitedSnapshot.FromExitInfo(exitInfo)));
        return exitInfo;
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
