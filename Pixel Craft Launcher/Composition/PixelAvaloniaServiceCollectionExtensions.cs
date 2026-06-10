using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PCL.Core.App.Pixel.Infrastructure;
using Pixel_Craft_Launcher.Services;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher;

internal static class PixelAvaloniaServiceCollectionExtensions
{
    public static IServiceCollection AddPixelAvalonia(this IServiceCollection services, string[] args)
    {
        services.AddSingleton(args);
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddTransient<PersonalizationPlatformBridge>();
        services.AddTransient<MainWindow>();
        return services;
    }
}

internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }
}
