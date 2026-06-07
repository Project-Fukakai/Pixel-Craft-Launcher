using Avalonia;
using System;
using System.IO;
using System.Linq;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using PCL.Core.App;

namespace Pixel_Craft_Launcher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

        return IsHardwareAccelerationDisabled()
            ? ApplySoftwareRendering(builder)
            : builder;
    }

    private static AppBuilder ApplySoftwareRendering(AppBuilder builder)
    {
        return builder
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] })
            .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
    }

    private static bool IsHardwareAccelerationDisabled()
    {
        var localConfigPath = Path.Combine(Paths.Data, "config.v1.yml");
        if (!File.Exists(localConfigPath))
            return false;

        try
        {
            return File.ReadLines(localConfigPath)
                .Select(static line => line.Trim())
                .Any(static line =>
                    line.StartsWith("SystemDisableHardwareAcceleration:", StringComparison.OrdinalIgnoreCase) &&
                    line.EndsWith("true", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
