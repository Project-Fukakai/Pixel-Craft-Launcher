using PCL.Core.App.Configuration;

namespace PCL.Core.App;

/// <summary>
/// Pixel-specific launcher state. Plain-compatible settings remain in <see cref="Config"/> and <see cref="States"/>.
/// </summary>
public static partial class PixelConfig
{
    [ConfigGroup("Pixel", ConfigSource.Local)]
    public sealed partial class PixelConfigGroup
    {
        [ConfigItem<string>("PixelSetupSelectedSection", "Launch")] public partial string SelectedSetupSection { get; set; }

        [ConfigItem<bool>("PixelSetupShowUnavailable", true)] public partial bool ShowUnavailableSetupItems { get; set; }
    }
}
