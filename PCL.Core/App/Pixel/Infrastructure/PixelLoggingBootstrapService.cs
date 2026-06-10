using System.IO;
using PCL.Core.App;

namespace PCL.Core.App.Pixel.Infrastructure;

public sealed class PixelLoggingBootstrapService
{
    public string EnsurePixelLogFilePattern()
    {
        return EnsurePixelLogFilePattern(Paths.SharedLocalData);
    }

    public static string EnsurePixelLogFilePattern(string sharedLocalData)
    {
        Directory.CreateDirectory(sharedLocalData);
        var logDirectory = Path.Combine(sharedLocalData, "Logs");
        Directory.CreateDirectory(logDirectory);
        return Path.Combine(logDirectory, "pixel-.log");
    }
}
