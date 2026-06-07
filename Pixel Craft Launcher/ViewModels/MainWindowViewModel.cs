using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Routing;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public const double LaunchLeftPaneWidth = 300d;
    public const double DefaultLeftPaneWidth = 230d;
    public const double InstallLeftPaneWidth = 300d;

    public MainWindowViewModel()
    {
        Router = new PixelRouter(PixelRoutes.Launch());
        Router.CurrentRouteChanged += (_, args) => ApplyRoute(args.NewRoute);

        Launch = new PixelLaunchViewModel();
        Download = new PixelDownloadViewModel();
        Instance = new PixelInstanceViewModel();

        NavigateLaunchCommand = new RelayCommand(() => Navigate(PixelRoutes.Launch()));
        NavigateDownloadCommand = new RelayCommand(() => Navigate(PixelRoutes.DownloadMinecraft()));
        NavigateSetupCommand = new RelayCommand(() => Navigate(PixelRoutes.Setup(SelectedSetupSection)));
        NavigateToolsCommand = new RelayCommand(() => Navigate(PixelRoutes.Tools()));
        BackCommand = new RelayCommand(() => Back());

        ApplyRoute(Router.CurrentRoute);
    }

    public INavigationService Router { get; }

    public PixelLaunchViewModel Launch { get; }

    public PixelDownloadViewModel Download { get; }

    public PixelInstanceViewModel Instance { get; }

    public IRelayCommand NavigateLaunchCommand { get; }

    public IRelayCommand NavigateDownloadCommand { get; }

    public IRelayCommand NavigateSetupCommand { get; }

    public IRelayCommand NavigateToolsCommand { get; }

    public IRelayCommand BackCommand { get; }

    [ObservableProperty]
    private RouteNode _currentRoute = PixelRoutes.Launch();

    [ObservableProperty]
    private MainPageKind _selectedMainPage = MainPageKind.Launch;

    [ObservableProperty]
    private int _selectedDownloadPage = 1;

    [ObservableProperty]
    private PixelSettingSectionKind _selectedSetupSection = PixelSettingSectionKind.Launch;

    [ObservableProperty]
    private bool _isSecondaryTitleVisible;

    [ObservableProperty]
    private string _titleText = string.Empty;

    [ObservableProperty]
    private double _leftPaneWidth = LaunchLeftPaneWidth;

    public bool Navigate(RouteNode route) => Router.Navigate(route);

    public bool Back() => Router.Back();

    public void NavigateDownloadCategory(int category)
    {
        Navigate(PixelRoutes.DownloadCategory(category));
    }

    public void NavigateSetupSection(PixelSettingSectionKind section)
    {
        SelectedSetupSection = section;
        Navigate(PixelRoutes.Setup(section));
    }

    public void NavigateMinecraftInstall(string versionId)
    {
        Navigate(PixelRoutes.DownloadMinecraftInstall(versionId));
    }

    public void NavigateDownloadTaskDetails(string? taskId = null)
    {
        Navigate(PixelRoutes.DownloadTaskDetails(taskId));
    }

    public void NavigateProfileManager(string? action = null, string? serverId = null)
    {
        Navigate(PixelRoutes.ProfileManager(action, serverId));
    }

    private void ApplyRoute(RouteNode route)
    {
        CurrentRoute = route;

        if (route.StartsWith("download"))
        {
            SelectedMainPage = MainPageKind.Download;
            ApplyDownloadRoute(route);
        }
        else if (route.StartsWith("secondary"))
        {
            ApplySecondaryRoute(route);
        }
        else if (route.StartsWith("setup"))
        {
            SelectedMainPage = MainPageKind.Setup;
            ApplySetupRoute(route);
        }
        else if (route.StartsWith("tools"))
        {
            SelectedMainPage = MainPageKind.Tools;
            SelectedDownloadPage = 1;
            IsSecondaryTitleVisible = false;
            LeftPaneWidth = DefaultLeftPaneWidth;
        }
        else
        {
            SelectedMainPage = MainPageKind.Launch;
            SelectedDownloadPage = 1;
            var isInstances = route.Child?.StartsWith("instances") == true;
            IsSecondaryTitleVisible = isInstances;
            TitleText = isInstances ? "实例选择" : string.Empty;
            LeftPaneWidth = isInstances ? DefaultLeftPaneWidth : LaunchLeftPaneWidth;
        }

        OnPropertyChanged(nameof(IsLaunchSelected));
        OnPropertyChanged(nameof(IsDownloadSelected));
        OnPropertyChanged(nameof(IsSetupSelected));
        OnPropertyChanged(nameof(IsToolsSelected));
    }

    private void ApplyDownloadRoute(RouteNode route)
    {
        var child = route.Child;
        SelectedDownloadPage = child?.Parameters.TryGetValue("category", out var rawCategory) == true &&
                               int.TryParse(rawCategory, out var category)
            ? category
            : child?.Segment switch
            {
                "mod" => 2,
                "modpack" => 3,
                "datapack" => 4,
                "resourcepack" => 5,
                "shaderpack" => 6,
                "world" => 7,
                "favorites" => 8,
                "client" => 9,
                "optifine" => 10,
                "forge" => 11,
                "neoforge" => 12,
                "cleanroom" => 13,
                "fabric" => 14,
                "quilt" => 15,
                "liteloader" => 16,
                "labymod" => 17,
                "legacyfabric" => 18,
                "tasks" => 19,
                _ => 1
            };

        var isInstall = child?.Child?.StartsWith("install") == true;
        var isTasks = child?.StartsWith("tasks") == true;
        IsSecondaryTitleVisible = isInstall || isTasks;
        TitleText = isInstall ? (Download.SelectedVersion?.Id ?? "Minecraft") + " 安装" : isTasks ? "下载管理" : string.Empty;
        LeftPaneWidth = isInstall || isTasks ? InstallLeftPaneWidth : DefaultLeftPaneWidth;
    }

    private void ApplySecondaryRoute(RouteNode route)
    {
        var kind = route.Parameters.TryGetValue("kind", out var rawKind) ? rawKind : string.Empty;
        SelectedMainPage = kind == "download-tasks" ? MainPageKind.Download : MainPageKind.Launch;
        SelectedDownloadPage = kind == "download-tasks" ? 9 : 1;
        IsSecondaryTitleVisible = true;
        LeftPaneWidth = InstallLeftPaneWidth;
        TitleText = kind == "download-tasks"
            ? "下载管理"
            : kind == "profiles"
                ? "档案管理"
                : string.Empty;
    }

    private void ApplySetupRoute(RouteNode route)
    {
        if (route.Child is { } child && Enum.TryParse<PixelSettingSectionKind>(child.Segment, true, out var section))
            SelectedSetupSection = section;

        SelectedDownloadPage = 1;
        IsSecondaryTitleVisible = false;
        LeftPaneWidth = DefaultLeftPaneWidth;
    }

    public bool IsLaunchSelected => SelectedMainPage == MainPageKind.Launch;

    public bool IsDownloadSelected => SelectedMainPage == MainPageKind.Download;

    public bool IsSetupSelected => SelectedMainPage == MainPageKind.Setup;

    public bool IsToolsSelected => SelectedMainPage == MainPageKind.Tools;
}
