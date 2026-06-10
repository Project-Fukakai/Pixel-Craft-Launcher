using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.Slices.Java;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Services;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public enum HintType
{
    Info,
    Finish,
    Critical
}

internal enum PageHostUpdateMode
{
    Initial,
    MainTransition,
    NestedTransition,
    SilentRefresh
}

public partial class MainWindow : Window
{
    private static IBrush BodyForeground => ThemeBrushes.Text;
    private static IBrush SubForeground => ThemeBrushes.TextSecondary;
    private static readonly Geometry ProfileNewIcon = Geometry.Parse("M512.277 954.412c-118.89 0-230.659-46.078-314.73-129.73S67.12 629.666 67.12 511.222s46.327-229.744 130.398-313.427 195.82-129.73 314.73-129.73 230.659 46.078 314.72 129.73S957.397 392.81 957.397 511.183 911.078 740.96 826.97 824.642s-195.8 129.77-314.692 129.77z m0-822.784c-101.972 0-197.809 39.494-269.865 111.222s-111.7 166.997-111.7 268.373 39.653 196.695 111.67 268.335S410.246 890.78 512.248 890.78s197.809-39.484 269.865-111.222 111.7-166.998 111.67-268.374c-0.03-101.375-39.654-196.665-111.67-268.303S614.22 131.628 512.277 131.628z m222.585 347.8H544.073V288.64c-0.76-17.561-15.613-31.18-33.173-30.419-16.495 0.714-29.704 13.924-30.419 30.419v190.787H289.703c-17.56 0.761-31.179 15.614-30.419 33.174 0.715 16.494 13.924 29.703 30.42 30.418H480.48v190.788c0.761 17.56 15.614 31.179 33.174 30.419 16.494-0.715 29.703-13.925 30.418-30.42V543.02h190.788c17.56 0.762 32.413-12.857 33.173-30.418 0.762-17.561-12.858-32.414-30.419-33.174a31.683 31.683 0 0 0-2.753 0z");
    private static readonly Geometry ProfilePortIcon = Geometry.Parse("M768.704 703.616c-35.648 0-67.904 14.72-91.136 38.304l-309.152-171.712c9.056-17.568 14.688-37.184 14.688-58.272 0-12.576-2.368-24.48-5.76-35.936l304.608-189.152c22.688 20.416 52.384 33.184 85.216 33.184 70.592 0 128-57.408 128-128s-57.408-128-128-128-128 57.408-128 128c0 14.56 2.976 28.352 7.456 41.408l-301.824 187.392c-23.136-22.784-54.784-36.928-89.728-36.928-70.592 0-128 57.408-128 128 0 70.592 57.408 128 128 128 25.664 0 49.504-7.744 69.568-20.8l321.216 178.4c-3.04 10.944-5.184 22.208-5.184 34.08 0 70.592 57.408 128 128 128s128-57.408 128-128S839.328 703.616 768.704 703.616zM767.2 128.032c35.296 0 64 28.704 64 64s-28.704 64-64 64-64-28.704-64-64S731.904 128.032 767.2 128.032zM191.136 511.936c0-35.296 28.704-64 64-64s64 28.704 64 64c0 35.296-28.704 64-64 64S191.136 547.232 191.136 511.936zM768.704 895.616c-35.296 0-64-28.704-64-64s28.704-64 64-64 64 28.704 64 64S804 895.616 768.704 895.616z");
    private readonly MainWindowViewModel _shellViewModel;
    private readonly PixelJavaService _javaService;
    private readonly PixelGameLinkToolsPageController _gameLinkToolsController;
    private readonly PixelLaunchFolderService _launchFolderService;
    private readonly PixelLaunchInstanceListService _launchInstanceListService;
    private readonly PixelLaunchSidebarService _launchSidebarService;
    private readonly PixelHomepageService _homepageService;
    private readonly PixelMemoryPreviewService _memoryPreviewService;
    private readonly PixelSettingsChangeService _settingsChangeService;
    private readonly PixelSetupNavigationService _setupNavigationService;
    private readonly PixelColorSchemeSettingsService _colorSchemeSettingsService;
    private readonly PixelSettingDisplayService _settingDisplayService;
    private readonly PixelShellSettingsService _shellSettingsService;
    private readonly PixelShellVisibilityService _shellVisibilityService;
    private readonly PixelSettingsObservationService _settingsObservationService;
    private readonly PixelSettingValueService _settingValueService;
    private readonly PixelProfileListService _profileListService;
    private readonly PixelProfilePageService _profilePageService;
    private readonly IExternalProcessService _externalProcessService;
    private readonly PixelPersonalizationService _pixelPersonalizationService;
    private readonly PersonalizationPlatformBridge _personalization;
    private readonly ShellThemePlatformBridge _themeBridge = new();
    private readonly List<IDisposable> _settingsSubscriptions = [];
    private PageHostTransitionPresenter _pageHostTransitions = null!;
    private ShellThemePresenter _shellThemePresenter = null!;
    private PixelLoadingTriggerAdapter? _launchLoadingState;
    private PixelLoadingTriggerAdapter? _downloadVersionLoadingState;
    private bool _isMainPageReady;
    private bool _isCloseRequestedByLifecycle;
    private bool _isTopTitleSecondary;
    private bool _isDownloadRefreshQueued;
    private bool _downloadRefreshNeedsLeft;
    private bool _suppressDownloadRefresh;
    private bool _suppressLaunchRefresh;
    private bool _lastLaunchIsLaunching;

    private PixelLaunchViewModel _launchViewModel => _shellViewModel.Launch;
    private PixelDownloadViewModel _downloadViewModel => _shellViewModel.Download;
    private PixelInstanceViewModel _instanceViewModel => _shellViewModel.Instance;
    private PixelGameLinkViewModel _gameLinkViewModel => _shellViewModel.GameLink;
    private int _selectedDownloadPage
    {
        get => _shellViewModel.SelectedDownloadPage;
        set => _shellViewModel.SelectedDownloadPage = value;
    }

    private PCL.Core.App.Pixel.PixelSettingSectionKind _selectedSetupSection
    {
        get => _shellViewModel.SelectedSetupSection;
        set => _shellViewModel.SelectedSetupSection = value;
    }

    public MainWindow()
        : this(
            PixelApplication.Services.GetRequiredService<MainWindowViewModel>(),
            PixelApplication.Services.GetRequiredService<PixelJavaService>(),
            PixelApplication.Services.GetRequiredService<PixelGameLinkToolsPageController>(),
            PixelApplication.Services.GetRequiredService<PixelLaunchFolderService>(),
            PixelApplication.Services.GetRequiredService<PixelLaunchInstanceListService>(),
            PixelApplication.Services.GetRequiredService<PixelLaunchSidebarService>(),
            PixelApplication.Services.GetRequiredService<PixelHomepageService>(),
            PixelApplication.Services.GetRequiredService<PixelMemoryPreviewService>(),
            PixelApplication.Services.GetRequiredService<PixelSettingsChangeService>(),
            PixelApplication.Services.GetRequiredService<PixelSetupNavigationService>(),
            PixelApplication.Services.GetRequiredService<PixelColorSchemeSettingsService>(),
            PixelApplication.Services.GetRequiredService<PixelSettingDisplayService>(),
            PixelApplication.Services.GetRequiredService<PixelShellSettingsService>(),
            PixelApplication.Services.GetRequiredService<PixelShellVisibilityService>(),
            PixelApplication.Services.GetRequiredService<PixelSettingsObservationService>(),
            PixelApplication.Services.GetRequiredService<PixelSettingValueService>(),
            PixelApplication.Services.GetRequiredService<PixelProfileListService>(),
            PixelApplication.Services.GetRequiredService<PixelProfilePageService>(),
            PixelApplication.Services.GetRequiredService<IExternalProcessService>(),
            PixelApplication.Services.GetRequiredService<PixelPersonalizationService>(),
            PixelApplication.Services.GetRequiredService<PersonalizationPlatformBridge>())
    {
    }

    [ActivatorUtilitiesConstructor]
    public MainWindow(
        MainWindowViewModel shellViewModel,
        PixelJavaService javaService,
        PixelGameLinkToolsPageController gameLinkToolsController,
        PixelLaunchFolderService launchFolderService,
        PixelLaunchInstanceListService launchInstanceListService,
        PixelLaunchSidebarService launchSidebarService,
        PixelHomepageService homepageService,
        PixelMemoryPreviewService memoryPreviewService,
        PixelSettingsChangeService settingsChangeService,
        PixelSetupNavigationService setupNavigationService,
        PixelColorSchemeSettingsService colorSchemeSettingsService,
        PixelSettingDisplayService settingDisplayService,
        PixelShellSettingsService shellSettingsService,
        PixelShellVisibilityService shellVisibilityService,
        PixelSettingsObservationService settingsObservationService,
        PixelSettingValueService settingValueService,
        PixelProfileListService profileListService,
        PixelProfilePageService profilePageService,
        IExternalProcessService externalProcessService,
        PixelPersonalizationService pixelPersonalizationService,
        PersonalizationPlatformBridge personalization)
    {
        _shellViewModel = shellViewModel;
        _javaService = javaService;
        _gameLinkToolsController = gameLinkToolsController;
        _launchFolderService = launchFolderService;
        _launchInstanceListService = launchInstanceListService;
        _launchSidebarService = launchSidebarService;
        _homepageService = homepageService;
        _memoryPreviewService = memoryPreviewService;
        _settingsChangeService = settingsChangeService;
        _setupNavigationService = setupNavigationService;
        _colorSchemeSettingsService = colorSchemeSettingsService;
        _settingDisplayService = settingDisplayService;
        _shellSettingsService = shellSettingsService;
        _shellVisibilityService = shellVisibilityService;
        _settingsObservationService = settingsObservationService;
        _settingValueService = settingValueService;
        _profileListService = profileListService;
        _profilePageService = profilePageService;
        _externalProcessService = externalProcessService;
        _pixelPersonalizationService = pixelPersonalizationService;
        _personalization = personalization;
        InitializeComponent();
        _pageHostTransitions = new PageHostTransitionPresenter(LeftPane, LeftContentHost, RightContentHost);
        _shellThemePresenter = new ShellThemePresenter(
            this,
            _shellSettingsService,
            _personalization,
            BackgroundMediaHost,
            BackgroundColorOverlay,
            PanTitle,
            LeftPane,
            RightPane,
            SidebarAcrylic,
            PathTitleLogo,
            ImgTitleLogo,
            LabTitleBrand,
            PanTitleNav,
            [BtnLaunch, BtnDownload, BtnSetup, BtnTools],
            [BtnTitleBack, BtnTitleHelp, BtnTitleMin, BtnTitleClose],
            _themeBridge);
        Focusable = true;
        DataContext = _shellViewModel;
        InitializeSettingDisplayState();
        Closing += OnWindowClosing;
        AddHandler(PointerPressedEvent, OnWindowPointerPressedForFocus, RoutingStrategies.Tunnel, handledEventsToo: true);
        ApplyPlatformChromeLayout();
        LoadInitialSetupSection();
        _shellViewModel.SelectedSetupSection = _selectedSetupSection;
        _shellViewModel.Router.CurrentRouteChanged += OnRouteChanged;
        _lastLaunchIsLaunching = _launchViewModel.IsLaunching;
        _launchViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PixelLaunchViewModel.IsGameRunning))
            {
                _personalization.SetGameRunning(_launchViewModel.IsGameRunning);
                Dispatcher.UIThread.Post(UpdateForceCloseGameButton, DispatcherPriority.Background);
            }

            if (_suppressLaunchRefresh || SelectedMainPage != MainPageKind.Launch)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                if (_suppressLaunchRefresh)
                    return;
                RefreshLaunchPage(args.PropertyName);
            }, DispatcherPriority.Background);
        };
        _launchViewModel.LaunchSucceeded += (_, result) =>
            Dispatcher.UIThread.Post(() => ShowHint(result.Message, HintType.Finish), DispatcherPriority.Background);
        _launchViewModel.GameExited += (_, exit) =>
            Dispatcher.UIThread.Post(() =>
            {
                var messages = _launchViewModel.GetWindowMessagesSnapshot();
                UpdateForceCloseGameButton();
                if (exit.WasKilled)
                    ShowHint(messages.GameKilledMessage, HintType.Info);
                else if (exit.ExitCode is 0 or null)
                    ShowHint(messages.GetGameExitedMessage(exit), HintType.Info);
            }, DispatcherPriority.Background);
        _launchViewModel.LaunchIssueDialogRequested += (_, dialog) =>
            Dispatcher.UIThread.Post(() => ShowLaunchIssueDialog(dialog), DispatcherPriority.Background);
        _launchViewModel.AccessibilityPermissionRequired += (_, _) =>
            Dispatcher.UIThread.Post(ShowAccessibilityPermissionDialog, DispatcherPriority.Background);
        _downloadViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PixelDownloadViewModel.IsBusy))
            {
                UpdateDownloadTasksButton();
                if (!_downloadViewModel.IsBusy && IsDownloadTaskRoute())
                    TryReturnFromDownloadTasks();
            }

            if (SelectedMainPage == MainPageKind.Download)
            {
                if (_downloadViewModel.ShouldRefreshRightPageForProperty(args.PropertyName))
                    ScheduleDownloadRightPageRefresh(refreshLeft: false);
            }
        };
        _downloadViewModel.TaskListChanged += (_, _) =>
        {
            UpdateDownloadTasksButton();
            if (IsDownloadTaskRoute())
            {
                ScheduleDownloadRightPageRefresh(refreshLeft: true);
                TryReturnFromDownloadTasks();
            }
        };
        _downloadViewModel.InstanceInstalled += (_, instance) =>
        {
            _launchViewModel.RefreshInstances();
            _instanceViewModel.Refresh();
            var messages = _downloadViewModel.GetPageMessagesSnapshot();
            ShowHint(messages.Window.GetInstallCompletedMessage(instance.Name), HintType.Finish);
        };
        _instanceViewModel.Refresh();
        WireShellEvents();
        WireThemeEvents();
        WirePersonalizationEvents();
        WireLauncherMiscEvents();
        _shellThemePresenter.RefreshShellTheme();
        ApplyLauncherMiscSettings();
        InitializeFloatingActionButtons();
        ApplyRoute(_shellViewModel.Router.CurrentRoute, isInitial: true);
        _personalization.Start();
        _personalization.SetGameRunning(_launchViewModel.IsGameRunning);
        ShowHint(_shellSettingsService.GetMessages().LoadedMessage, MyHint.Themes.Blue);
    }

    public MainPageKind SelectedMainPage
    {
        get => _shellViewModel.SelectedMainPage;
        private set => _shellViewModel.SelectedMainPage = value;
    }

}

internal static class MainWindowControlExtensions
{
    public static T WithClick<T>(this T button, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
        where T : Button
    {
        button.Click += handler;
        return button;
    }
}
