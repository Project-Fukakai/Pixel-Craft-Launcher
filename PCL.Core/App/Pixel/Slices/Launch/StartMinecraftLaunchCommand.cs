using MediatR;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record StartMinecraftLaunchCommand(
    MinecraftInstanceInfo Instance,
    IMinecraftAccountProvider AccountProvider,
    IProgress<MinecraftLaunchProgress>? Progress = null,
    MinecraftLaunchOptions? Options = null)
    : IRequest<MinecraftLaunchResult>;

public sealed record PixelLaunchStartedEvent(string InstanceName);

public sealed record PixelLaunchProgressEvent(MinecraftLaunchProgress Progress);

public sealed record PixelLaunchCompletedEvent(PixelLaunchResultSnapshot Result);

public sealed class StartMinecraftLaunchCommandHandler(
    MinecraftLaunchService launchService,
    MinecraftRepairService repairService,
    IPixelEventBus eventBus)
    : IRequestHandler<StartMinecraftLaunchCommand, MinecraftLaunchResult>
{
    async Task<MinecraftLaunchResult> IRequestHandler<StartMinecraftLaunchCommand, MinecraftLaunchResult>.Handle(
        StartMinecraftLaunchCommand request,
        CancellationToken cancellationToken)
    {
        eventBus.Publish(new PixelLaunchStartedEvent(request.Instance.Name));
        var progress = new Progress<MinecraftLaunchProgress>(value =>
        {
            request.Progress?.Report(value);
            eventBus.Publish(new PixelLaunchProgressEvent(value));
        });
        var launchRequest = new MinecraftLaunchRequest(
            request.Instance,
            request.AccountProvider,
            Options: request.Options ?? new MinecraftLaunchOptions { UseDownloadRepairStep = true },
            FileRepairer: repairService);

        var result = await launchService.LaunchAsync(launchRequest, progress, cancellationToken).ConfigureAwait(false);
        eventBus.Publish(new PixelLaunchCompletedEvent(PixelLaunchResultSnapshot.FromResult(result)));
        return result;
    }
}
