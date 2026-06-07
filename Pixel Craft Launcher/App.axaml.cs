using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using Pixel_Craft_Launcher.ViewModels;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplicationService.Loading = () => this;
        MainWindowService.Loading = () => new MainWindow();
        Lifecycle.OnInitialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowService.Loading = () => new MainWindow();
            Lifecycle.OnLoading();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
