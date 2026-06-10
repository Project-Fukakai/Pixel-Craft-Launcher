using System.Collections.Generic;
using System.IO;
using PCL.Core.App;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelStartupRenderingService
{
    public bool IsHardwareAccelerationDisabled()
    {
        var localConfigPath = Path.Combine(Paths.Data, "config.v1.yml");
        if (!File.Exists(localConfigPath))
            return false;

        try
        {
            return IsHardwareAccelerationDisabled(File.ReadLines(localConfigPath));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsHardwareAccelerationDisabled(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("SystemDisableHardwareAcceleration:", StringComparison.OrdinalIgnoreCase) &&
                trimmed.EndsWith("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
