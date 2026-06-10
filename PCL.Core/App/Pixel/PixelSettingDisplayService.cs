using PCL.Core.App;

namespace PCL.Core.App.Pixel;

public sealed class PixelSettingDisplayService
{
    public bool ShouldDisplay(PixelSettingDescriptor setting)
    {
        return setting.ConfigKey switch
        {
            "LaunchArgumentWindowWidth" or "LaunchArgumentWindowHeight" => IsCustomGameWindowSize(),
            "LaunchAdvanceRunWait" => IsRunWaitVisible(),
            _ => true
        };
    }

    public bool IsRunWaitVisible()
    {
        return !string.IsNullOrWhiteSpace(PixelSettingsBinder.LoadValue("LaunchAdvanceRun")?.ToString());
    }

    private static bool IsCustomGameWindowSize()
    {
        return PixelSettingsBinder.LoadValue("LaunchArgumentWindowType") is int windowType &&
               windowType == (int)GameWindowSizeMode.Custom;
    }
}
