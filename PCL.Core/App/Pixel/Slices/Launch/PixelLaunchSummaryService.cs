using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record PixelLaunchSummarySnapshot(
    string ProfileName,
    string ProfileMethod,
    string InstanceName,
    string? InstancePath,
    bool HasProfile,
    bool HasInstance,
    bool CanLaunch);

public sealed class PixelLaunchSummaryService
{
    public PixelLaunchSummarySnapshot GetSummary(
        MinecraftInstanceInfo? selectedInstance,
        string selectedInstanceName,
        MinecraftProfile? selectedProfile,
        bool canLaunch)
    {
        return new PixelLaunchSummarySnapshot(
            selectedProfile?.Username ?? "未选择档案",
            PixelProfileListService.GetProfileTypeName(selectedProfile),
            selectedInstanceName,
            selectedInstance?.VersionDirectory,
            selectedProfile is not null,
            selectedInstance is not null,
            canLaunch);
    }
}
