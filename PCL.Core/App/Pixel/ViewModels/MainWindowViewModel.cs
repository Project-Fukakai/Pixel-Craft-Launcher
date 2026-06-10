using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;

namespace PCL.Core.App.Pixel.ViewModels;

public partial class MainWindowViewModel : PixelViewModelBase
{
    public const double LaunchLeftPaneWidth = 300d;
    public const double DefaultLeftPaneWidth = 230d;
    public const double SecondaryLeftPaneWidth = 300d;
    public const double InstallLeftPaneWidth = SecondaryLeftPaneWidth;

    public MainWindowViewModel(
        IPixelNavigationService router,
        PixelLaunchViewModel launch,
        PixelDownloadViewModel download,
        PixelInstanceViewModel instance,
        PixelGameLinkViewModel gameLink)
    {
        Router = router;
        Router.CurrentRouteChanged += (_, args) => ApplyRoute(args.NewRoute);

        Launch = launch;
        Download = download;
        Instance = instance;
        GameLink = gameLink;

        NavigateLaunchCommand = new RelayCommand(() => NavigateMainPage(MainPageKind.Launch));
        NavigateDownloadCommand = new RelayCommand(() => NavigateMainPage(MainPageKind.Download));
        NavigateSetupCommand = new RelayCommand(() => NavigateMainPage(MainPageKind.Setup));
        NavigateToolsCommand = new RelayCommand(() => NavigateMainPage(MainPageKind.Tools));
        BackCommand = new RelayCommand(() => Back());

        ApplyRoute(Router.CurrentRoute);
    }

    public IPixelNavigationService Router { get; }

    public PixelLaunchViewModel Launch { get; }

    public PixelDownloadViewModel Download { get; }

    public PixelInstanceViewModel Instance { get; }

    public PixelGameLinkViewModel GameLink { get; }

    public IRelayCommand NavigateLaunchCommand { get; }

    public IRelayCommand NavigateDownloadCommand { get; }

    public IRelayCommand NavigateSetupCommand { get; }

    public IRelayCommand NavigateToolsCommand { get; }

    public IRelayCommand BackCommand { get; }

    public string LaunchNavigationText => "启动";

    public string DownloadNavigationText => "下载";

    public string SetupNavigationText => "设置";

    public string ToolsNavigationText => "工具";

    public string DownloadTasksFloatingTooltip => "下载管理";

    public string ForceCloseGameFloatingTooltip => "强制关闭 Minecraft";

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

    public bool NavigateMainPage(MainPageKind page)
    {
        return Navigate(GetMainPageRoute(page, SelectedSetupSection));
    }

    public static RouteNode GetMainPageRoute(MainPageKind page, PixelSettingSectionKind selectedSetupSection)
    {
        return page switch
        {
            MainPageKind.Download => PixelRoutes.DownloadMinecraft(),
            MainPageKind.Setup => PixelRoutes.Setup(selectedSetupSection),
            MainPageKind.Tools => PixelRoutes.Tools(),
            _ => PixelRoutes.Launch()
        };
    }

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

    public void NavigateLaunchInstances()
    {
        Navigate(PixelRoutes.LaunchInstances());
    }

    public void NavigateDownloadTaskDetails(string? taskId = null)
    {
        Navigate(PixelRoutes.DownloadTaskDetails(taskId));
    }

    public void NavigateProfileManager(string? action = null, string? serverId = null)
    {
        Navigate(PixelRoutes.ProfileManager(action, serverId));
    }

    public bool IsCurrentDownloadInstallRoute() => IsDownloadInstallRoute(CurrentRoute);

    public bool IsCurrentDownloadTaskRoute() => IsDownloadTaskRoute(CurrentRoute);

    public bool IsCurrentDownloadSecondaryRoute() => IsDownloadSecondaryRoute(CurrentRoute);

    public bool IsCurrentGlobalSecondaryRoute() => IsGlobalSecondaryRoute(CurrentRoute);

    public bool IsCurrentProfileManagerRoute() => IsProfileManagerRoute(CurrentRoute);

    public bool IsCurrentLaunchInstanceRoute() => IsLaunchInstanceRoute(CurrentRoute);

    public static bool IsGlobalSecondaryRoute(RouteNode route)
    {
        return route.StartsWith("secondary");
    }

    public static bool IsDownloadInstallRoute(RouteNode route)
    {
        return route.StartsWith("download") &&
               route.Child?.StartsWith("minecraft") == true &&
               route.Child.Child?.StartsWith("install") == true;
    }

    public static bool IsDownloadTaskRoute(RouteNode route)
    {
        return route.StartsWith("secondary") &&
               route.Parameters.TryGetValue("kind", out var kind) &&
               string.Equals(kind, "download-tasks", StringComparison.OrdinalIgnoreCase)
            || route.StartsWith("download") &&
               route.Child?.StartsWith("tasks") == true;
    }

    public static string? GetDownloadTaskRouteTaskId(RouteNode route)
    {
        if (!IsDownloadTaskRoute(route))
            return null;

        if (route.Parameters.TryGetValue("task", out var taskId))
            return taskId;

        return route.Child?.Parameters.TryGetValue("task", out taskId) == true
            ? taskId
            : null;
    }

    public static bool IsDownloadSecondaryRoute(RouteNode route)
    {
        return IsDownloadInstallRoute(route) || IsDownloadTaskRoute(route);
    }

    public static bool IsProfileManagerRoute(RouteNode route)
    {
        return route.StartsWith("secondary") &&
               route.Parameters.TryGetValue("kind", out var kind) &&
               string.Equals(kind, "profiles", StringComparison.OrdinalIgnoreCase);
    }

    public static PixelProfileManagerRouteSnapshot GetProfileManagerRouteSnapshot(RouteNode route)
    {
        if (!IsProfileManagerRoute(route))
            return new PixelProfileManagerRouteSnapshot(PixelProfileManagerPageKind.List, null);

        var action = route.Parameters.TryGetValue("action", out var rawAction) ? rawAction : null;
        var pageKind = action switch
        {
            "offline" => PixelProfileManagerPageKind.Offline,
            "microsoft" => PixelProfileManagerPageKind.Microsoft,
            "authlib" => PixelProfileManagerPageKind.Authlib,
            "server" => PixelProfileManagerPageKind.AuthServer,
            _ => PixelProfileManagerPageKind.List
        };
        var serverId = route.Parameters.TryGetValue("server", out var rawServerId) ? rawServerId : null;
        return new PixelProfileManagerRouteSnapshot(pageKind, serverId);
    }

    public static PixelPlaceholderPageSnapshot GetPlaceholderPageSnapshot(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => new PixelPlaceholderPageSnapshot(
                "下载",
                "版本下载、资源补全和任务队列会在这里逐步接入。",
                GetDefaultPlaceholderSections()),
            MainPageKind.Setup => new PixelPlaceholderPageSnapshot(
                "设置",
                "启动器设置、游戏设置和账户设置先使用占位承载。",
                GetDefaultPlaceholderSections()),
            MainPageKind.Tools => new PixelPlaceholderPageSnapshot(
                "工具",
                "调试与控件验收入口，后续也可承载日志、导入和维护工具。",
                GetDefaultPlaceholderSections()),
            _ => new PixelPlaceholderPageSnapshot(
                "启动",
                "启动页 Shell 已就绪，后续迁入账户、版本选择和启动按钮逻辑。",
                GetDefaultPlaceholderSections())
        };
    }

    private static IReadOnlyList<PixelPlaceholderSectionSnapshot> GetDefaultPlaceholderSections()
    {
        return
        [
            new PixelPlaceholderSectionSnapshot(
                "页面承载区",
                "这里先保留 Plain 主界面的左右分栏和内容容器，后续可以把真实页面直接放进右侧 ContentControl。\nShell 级提示、弹窗遮罩、浮动按钮和窗口按钮已经接入。"),
            new PixelPlaceholderSectionSnapshot(
                "迁移备注",
                "后续页面会继续拆分为独立 Page/View，并通过 PCL.Core 的 ViewModel、Command Bus 与事件流接入业务。")
        ];
    }

    public static PixelSecondaryTitleSnapshot GetSecondaryTitleSnapshot(RouteNode route, string downloadInstallTitle)
    {
        if (IsProfileManagerRoute(route))
            return new PixelSecondaryTitleSnapshot(true, "档案管理");

        if (IsDownloadTaskRoute(route))
            return new PixelSecondaryTitleSnapshot(true, "下载管理");

        if (IsDownloadInstallRoute(route))
            return new PixelSecondaryTitleSnapshot(true, downloadInstallTitle);

        if (IsLaunchInstanceRoute(route))
            return new PixelSecondaryTitleSnapshot(true, "实例选择");

        return new PixelSecondaryTitleSnapshot(false, string.Empty);
    }

    public static PixelControlsPreviewSnapshot GetControlsPreviewSnapshot()
    {
        return new PixelControlsPreviewSnapshot(
            "工具",
            "临时保留控件验收页，确认基础控件在新 Shell 内仍可显示和交互。",
            "按钮",
            "输入",
            "搜索控件",
            "等待搜索",
            "搜索：",
            "安全剪贴板 TextBox",
            "下拉选择",
            ["默认", "轻量", "完整"],
            "系统字体",
            "选择控件",
            "启动",
            "下载",
            "提示控件支持 Blue / Yellow / Red 主题。",
            "加载与列表",
            "图标、按钮区、选中条与二级信息",
            "弹窗与行为",
            "打开 MyMsgMarkdown",
            "Markdown 消息",
            "这是 Shell 遮罩层中的消息控件。\n\n正文暂按纯文本换行显示。",
            "LazyLoad 示例：滚入视口后触发",
            "LazyLoad 已触发一次");
    }

    public static IReadOnlyList<PixelPlaceholderLeftItemSnapshot> GetPlaceholderLeftItemSnapshots(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download =>
            [
                new PixelPlaceholderLeftItemSnapshot("版本列表", "Minecraft / Mod Loader", "mdi-cube-outline", true),
                new PixelPlaceholderLeftItemSnapshot("下载任务", "队列与进度", "mdi-download", false),
                new PixelPlaceholderLeftItemSnapshot("资源补全", "库文件与资产", "mdi-package-variant", false)
            ],
            MainPageKind.Setup =>
            [
                new PixelPlaceholderLeftItemSnapshot("启动器", "主题与窗口", "mdi-cog-outline", true),
                new PixelPlaceholderLeftItemSnapshot("游戏", "Java 与内存", "mdi-controller-classic-outline", false),
                new PixelPlaceholderLeftItemSnapshot("账户", "登录与档案", "mdi-account-circle-outline", false)
            ],
            MainPageKind.Tools =>
            [
                new PixelPlaceholderLeftItemSnapshot("控件验收", "迁移组件预览", "mdi-tools", true),
                new PixelPlaceholderLeftItemSnapshot("日志", "运行与诊断", "mdi-text-box-outline", false),
                new PixelPlaceholderLeftItemSnapshot("导入", "整合包与实例", "mdi-import", false)
            ],
            _ =>
            [
                new PixelPlaceholderLeftItemSnapshot("概览", "版本与账户", "mdi-play-circle-outline", true),
                new PixelPlaceholderLeftItemSnapshot("实例", "本地游戏列表", "mdi-folder-multiple-outline", false),
                new PixelPlaceholderLeftItemSnapshot("动态", "公告与提示", "mdi-bullhorn-outline", false)
            ]
        };
    }

    public static bool IsLaunchInstanceRoute(RouteNode route)
    {
        return route.StartsWith("launch") &&
               route.Child?.StartsWith("instances") == true;
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
            var isInstances = IsLaunchInstanceRoute(route);
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

        var isInstall = IsDownloadInstallRoute(route);
        var isTasks = IsDownloadTaskRoute(route);
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

public enum PixelProfileManagerPageKind
{
    List,
    Offline,
    Microsoft,
    Authlib,
    AuthServer
}

public sealed record PixelProfileManagerRouteSnapshot(
    PixelProfileManagerPageKind PageKind,
    string? AuthServerId);

public sealed record PixelPlaceholderPageSnapshot(
    string Title,
    string Description,
    IReadOnlyList<PixelPlaceholderSectionSnapshot> Sections);

public sealed record PixelPlaceholderSectionSnapshot(
    string Title,
    string Text);

public sealed record PixelControlsPreviewSnapshot(
    string HeroTitle,
    string HeroDescription,
    string ButtonsCardTitle,
    string InputsCardTitle,
    string SearchHint,
    string SearchWaitingText,
    string SearchResultPrefix,
    string SafeTextBoxHint,
    string ComboHint,
    IReadOnlyList<string> ComboItems,
    string FontTooltip,
    string SelectionCardTitle,
    string LaunchRadioText,
    string DownloadRadioText,
    string HintText,
    string LoadingAndListCardTitle,
    string ListItemInfo,
    string DialogAndBehaviorCardTitle,
    string OpenMarkdownButtonText,
    string MarkdownMessageTitle,
    string MarkdownMessageBody,
    string LazyLoadText,
    string LazyLoadTriggeredText);

public sealed record PixelSecondaryTitleSnapshot(
    bool IsVisible,
    string Title);

public sealed record PixelPlaceholderLeftItemSnapshot(
    string Title,
    string Info,
    string Icon,
    bool IsActive);
