using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel.Infrastructure;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplicationService.Loading = () => this;
        MainWindowService.Loading = () => PixelApplication.Services.GetRequiredService<MainWindow>();
        PixelApplication.Services.GetRequiredService<PixelHost>().StartAsync().GetAwaiter().GetResult();
        Lifecycle.OnInitialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowService.Loading = () => PixelApplication.Services.GetRequiredService<MainWindow>();
            Lifecycle.OnLoading();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
