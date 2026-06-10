using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PCL.Core.App.Pixel.Composition;
using PCL.Core.App.Pixel.Infrastructure;
using Serilog;

namespace Pixel_Craft_Launcher;

internal static class PixelApplication
{
    private static ServiceProvider? _services;

    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Pixel services have not been initialized.");

    public static void Initialize(string[] args)
    {
        if (_services is not null)
            return;

        var logFilePattern = new PixelLoggingBootstrapService().EnsurePixelLogFilePattern();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Trace()
            .WriteTo.File(
                logFilePattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });
        services.AddPixelApplication();
        services.AddPixelAvalonia(args);
        _services = services.BuildServiceProvider(validateScopes: true);
    }

    public static void Dispose()
    {
        if (_services is null)
            return;

        _services.GetRequiredService<PCL.Core.App.Pixel.Infrastructure.PixelHost>()
            .StopAsync()
            .GetAwaiter()
            .GetResult();
        _services.Dispose();
        _services = null;
    }
}
