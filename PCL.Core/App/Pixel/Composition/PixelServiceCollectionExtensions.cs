using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.Slices.Download;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.Slices.Java;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.App.Pixel.Composition;

public static class PixelServiceCollectionExtensions
{
    public static IServiceCollection AddPclCore(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            configuration.AddOpenBehavior(typeof(PixelLoggingBehavior<,>));
        });

        services.AddSingleton<IPixelCommandBus, MediatRPixelCommandBus>();
        services.AddSingleton<IPixelEventBus, ReactivePixelEventBus>();
        services.AddSingleton<IPixelNavigationService>(provider => new PixelRouter(
            PixelRoutes.Launch(),
            provider.GetRequiredService<IPixelEventBus>(),
            provider.GetRequiredService<ILogger<PixelRouter>>()));
        services.AddSingleton<IPixelStateMachineFactory, PixelStateMachineFactory>();
        services.AddSingleton<PixelLoggingBootstrapService>();
        services.AddSingleton<PixelStartupRenderingService>();
        services.AddSingleton<IUiDispatcher, ImmediateUiDispatcher>();
        services.AddSingleton<IDialogService, NullDialogService>();
        services.AddSingleton<IFilePickerService, NullFilePickerService>();
        services.AddSingleton<IExternalProcessService, ExternalProcessService>();
        services.AddSingleton<IPixelOperationDelayService, PixelOperationDelayService>();
        services.AddSingleton<MinecraftInstanceService>();
        services.AddSingleton<MinecraftDownloadService>();
        services.AddSingleton<MinecraftModLoaderCatalogService>();
        services.AddTransient<MinecraftInstallService>();
        services.AddTransient<MinecraftMergedInstallService>();
        services.AddTransient<MinecraftCorePackageSaver>();
        services.AddSingleton<PixelDownloadCategoryRefreshService>();
        services.AddSingleton<PixelLoaderChoiceService>();
        services.AddSingleton<PixelLoaderSelectionService>();
        services.AddSingleton<PixelJavaService>();
        services.AddSingleton<PixelGameLinkService>();
        services.AddTransient<PixelGameLinkToolsPageController>();
        services.AddSingleton<IPixelRendererHintState, PixelRendererHintState>();
        services.AddSingleton<PixelSettingsChangeService>();
        services.AddSingleton<PixelSetupNavigationService>();
        services.AddSingleton<PixelColorSchemeSettingsService>();
        services.AddSingleton<PixelSettingDisplayService>();
        services.AddSingleton<PixelShellSettingsService>();
        services.AddSingleton<PixelSettingsObservationService>();
        services.AddSingleton<PixelSettingValueService>();
        services.AddSingleton<PixelShellVisibilityService>();
        services.AddSingleton<PixelPersonalizationService>();
        services.AddTransient<MinecraftLaunchService>();
        services.AddTransient<MinecraftProcessMonitor>();
        services.AddTransient<MinecraftRepairService>();
        services.AddSingleton<PixelLaunchFolderService>();
        services.AddSingleton<PixelLaunchInstanceListService>();
        services.AddSingleton<PixelLaunchSidebarService>();
        services.AddSingleton<PixelLaunchDialogService>();
        services.AddSingleton<PixelLaunchSummaryService>();
        services.AddSingleton<PixelHomepageService>();
        services.AddSingleton<PixelMemoryPreviewService>();
        services.AddSingleton<PixelProfileListService>();
        services.AddSingleton<PixelProfilePageService>();
        services.AddSingleton<MinecraftProfileService>();
        services.AddSingleton<PixelProfileStateMachine>();
        services.AddTransient<PixelLaunchStateMachine>();
        services.AddTransient<PixelDownloadStateMachine>();
        services.AddTransient<PixelGameLinkStateMachine>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<PixelLaunchViewModel>();
        services.AddTransient<PixelInstanceViewModel>();
        services.AddTransient<PixelDownloadViewModel>();
        services.AddSingleton<PixelGameLinkViewModel>();
        services.AddSingleton<PixelHost>();
        return services;
    }

    public static IServiceCollection AddPixelApplication(this IServiceCollection services)
    {
        return services.AddPclCore();
    }
}
