using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using PCL.Core.App.Pixel.Infrastructure;
using Serilog;

namespace Pixel_Craft_Launcher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        PixelApplication.Initialize(args);
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            PixelApplication.Dispose();
            Log.CloseAndFlush();
        }
    }

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
        return PixelApplication.Services
            .GetRequiredService<PixelStartupRenderingService>()
            .IsHardwareAccelerationDisabled();
    }
}
