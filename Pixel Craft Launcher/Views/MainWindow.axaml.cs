using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Routing;
using Pixel_Craft_Launcher.Services;
using Pixel_Craft_Launcher.Settings;
using Pixel_Craft_Launcher.ViewModels;

namespace Pixel_Craft_Launcher.Views;

public enum MainPageKind
{
    Launch,
    Download,
    Setup,
    Tools
}

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
    private readonly MainWindowViewModel _shellViewModel = new();
    private readonly PersonalizationService _personalization = new();
    private readonly MyLoadingStateSimulator _loadingState = new() { LoadingState = MyLoadingState.Loading };
    private bool _isMainPageReady;
    private bool _isCloseRequestedByLifecycle;
    private bool _isTopTitleSecondary;
    private bool _isDownloadRefreshQueued;
    private bool _downloadRefreshNeedsLeft;
    private bool _suppressDownloadRefresh;
    private bool _suppressLaunchRefresh;
    private bool _lastLaunchIsLaunching;
    private int _hintId;
    private readonly Dictionary<MyFloatingActionButton, FloatingActionState> _floatingActions = new();
    private bool IsAcrylicEnabled => PixelSettingsBinder.LoadValue("UiAcrylic") is bool enabled && enabled;

    private PixelLaunchViewModel _launchViewModel => _shellViewModel.Launch;
    private PixelDownloadViewModel _downloadViewModel => _shellViewModel.Download;
    private PixelInstanceViewModel _instanceViewModel => _shellViewModel.Instance;
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

    private sealed class HintTag
    {
        public HintTag(int id, string text)
        {
            Id = id;
            Text = text;
        }

        public int Id { get; }
        public string Text { get; }
        public bool Reusable { get; set; } = true;
    }

    private sealed class FloatingActionState
    {
        public int Index { get; set; }
        public bool IsShown { get; set; }
        public double Bottom { get; set; }
    }

    public MainWindow()
    {
        InitializeComponent();
        Focusable = true;
        DataContext = _shellViewModel;
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
        _launchViewModel.LaunchFailed += (_, result) =>
            Dispatcher.UIThread.Post(() => ShowLaunchFailureDialog(result), DispatcherPriority.Background);
        _launchViewModel.GameExited += (_, exit) =>
            Dispatcher.UIThread.Post(() =>
            {
                UpdateForceCloseGameButton();
                if (exit.WasKilled)
                    ShowHint("已强制关闭 Minecraft。", HintType.Info);
                else if (exit.ExitCode is 0 or null)
                    ShowHint($"{exit.InstanceName} 已退出。", HintType.Info);
            }, DispatcherPriority.Background);
        _launchViewModel.GameCrashed += (_, exit) =>
            Dispatcher.UIThread.Post(() => ShowCrashDialog(exit), DispatcherPriority.Background);
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
                if (args.PropertyName is nameof(PixelDownloadViewModel.MergedSelection)
                    or nameof(PixelDownloadViewModel.SelectedLoaderKind))
                {
                    ScheduleDownloadRightPageRefresh(refreshLeft: false);
                }
                else if (args.PropertyName is nameof(PixelDownloadViewModel.IsBusy)
                         or nameof(PixelDownloadViewModel.LoaderChoiceGroups)
                         or nameof(PixelDownloadViewModel.IsLoaderChoicesLoading)
                         or nameof(PixelDownloadViewModel.LoaderChoicesError)
                         or nameof(PixelDownloadViewModel.SelectedTask))
                {
                    ScheduleDownloadRightPageRefresh(refreshLeft: false);
                }
            }
        };
        _downloadViewModel.Tasks.CollectionChanged += (_, _) =>
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
            ShowHint("安装完成：" + instance.Name, HintType.Finish);
        };
        _instanceViewModel.Refresh();
        WireShellEvents();
        WireThemeEvents();
        WirePersonalizationEvents();
        WireLauncherMiscEvents();
        ApplyAvaloniaThemeVariant();
        ApplyShellColors();
        ApplyWindowPersonalization();
        ApplyLauncherMiscSettings();
        InitializeFloatingActionButtons();
        ApplyRoute(_shellViewModel.Router.CurrentRoute, isInitial: true);
        _personalization.Start();
        _personalization.SetGameRunning(_launchViewModel.IsGameRunning);
        ShowHint("Pixel 主界面 Shell 已载入，业务页面会逐步迁入。", MyHint.Themes.Blue);
    }

    public MainPageKind SelectedMainPage
    {
        get => _shellViewModel.SelectedMainPage;
        private set => _shellViewModel.SelectedMainPage = value;
    }

    public bool SwitchMainPage(MainPageKind page)
    {
        if (!FeatureVisibilityService.IsMainPageVisible(page))
            return false;

        return _shellViewModel.Navigate(page switch
        {
            MainPageKind.Download => PixelRoutes.DownloadMinecraft(),
            MainPageKind.Setup => PixelRoutes.Setup(_selectedSetupSection),
            MainPageKind.Tools => PixelRoutes.Tools(),
            _ => PixelRoutes.Launch()
        });
    }

    private void OnRouteChanged(object? sender, PixelRouteChangedEventArgs e)
    {
        if (!e.IsInitial && IsNestedRouteChange(e.OldRoute, e.NewRoute))
        {
            ApplyNestedRoute(e.OldRoute, e.NewRoute);
            return;
        }

        ApplyRoute(e.NewRoute, e.IsInitial);
    }

    private static bool IsNestedRouteChange(RouteNode oldRoute, RouteNode newRoute)
    {
        if (!string.Equals(oldRoute.Segment, newRoute.Segment, StringComparison.OrdinalIgnoreCase))
            return false;

        return newRoute.StartsWith("download") ||
               newRoute.StartsWith("setup") ||
               newRoute.StartsWith("launch") ||
               newRoute.StartsWith("secondary");
    }

    private void ApplyNestedRoute(RouteNode oldRoute, RouteNode route)
    {
        ApplyDownloadTaskRouteSelection(route);
        var page = _shellViewModel.SelectedMainPage;
        BtnLaunch.Checked = page == MainPageKind.Launch;
        BtnDownload.Checked = page == MainPageKind.Download;
        BtnSetup.Checked = page == MainPageKind.Setup;
        BtnTools.Checked = page == MainPageKind.Tools;
        ApplyTopTitleMode();
        UpdateDownloadTasksButton();
        AnimateLeftPaneWidth(GetLeftPaneWidth(page));

        var shouldRefreshLeft = IsGlobalSecondaryRoute(route)
            ? IsDownloadSecondaryRoute(oldRoute) != IsDownloadSecondaryRoute(route)
            : page switch
        {
            MainPageKind.Launch => IsLaunchInstanceRoute(oldRoute) != IsLaunchInstanceRoute(route),
            MainPageKind.Download => IsDownloadSecondaryRoute(oldRoute) != IsDownloadSecondaryRoute(route),
            _ => false
        };

        var leftContent = shouldRefreshLeft ? IsGlobalSecondaryRoute(route)
            ? BuildSecondaryLeftPage(route)
            : page switch
        {
            MainPageKind.Download => BuildDownloadLeftPage(),
            MainPageKind.Launch => BuildLaunchLeftPage(),
            _ => LeftContentHost.Content as Control
        } : null;
        if (leftContent is not null)
        {
            SetPageHostContent(
                LeftContentHost,
                leftContent,
                PageHostUpdateMode.NestedTransition,
                page == MainPageKind.Launch ? content => TriggerLeftPageShowAnimation(content, page) : null);
        }

        var rightContent = IsGlobalSecondaryRoute(route) ? BuildSecondaryRightPage(route) : page switch
        {
            MainPageKind.Download => BuildDownloadRightPage(),
            MainPageKind.Setup => BuildSetupRightPage(),
            MainPageKind.Launch => BuildLaunchRightPage(),
            _ => RightContentHost.Content as Control ?? BuildPlaceholderRightPage(page)
        };
        SetPageHostContent(RightContentHost, rightContent, PageHostUpdateMode.NestedTransition);
    }

    private void ApplyRoute(RouteNode route, bool isInitial)
    {
        ApplyDownloadTaskRouteSelection(route);
        var page = _shellViewModel.SelectedMainPage;
        if (!FeatureVisibilityService.IsMainPageVisible(page))
        {
            _shellViewModel.Navigate(PixelRoutes.Launch());
            return;
        }

        BtnLaunch.Checked = page == MainPageKind.Launch;
        BtnDownload.Checked = page == MainPageKind.Download;
        BtnSetup.Checked = page == MainPageKind.Setup;
        BtnTools.Checked = page == MainPageKind.Tools;
        ApplyTopTitleMode();
        UpdateDownloadTasksButton();

        var leftContent = IsGlobalSecondaryRoute(route) ? BuildSecondaryLeftPage(route) : page switch
        {
            MainPageKind.Launch => BuildLaunchLeftPage(),
            MainPageKind.Download => BuildDownloadLeftPage(),
            MainPageKind.Setup => BuildSetupLeftPage(),
            _ => BuildLeftPage(page)
        };
        var rightContent = IsGlobalSecondaryRoute(route) ? BuildSecondaryRightPage(route) : page switch
        {
            MainPageKind.Launch => BuildLaunchRightPage(),
            MainPageKind.Download => BuildDownloadRightPage(),
            MainPageKind.Setup => BuildSetupRightPage(),
            MainPageKind.Tools => BuildControlsPreviewPage(),
            _ => BuildPlaceholderRightPage(page)
        };
        var leftWidth = IsGlobalSecondaryRoute(route) ? 300d : GetLeftPaneWidth(page);

        if (!_isMainPageReady)
        {
            LeftPane.Width = leftWidth;
            SetPageHostContent(LeftContentHost, leftContent, PageHostUpdateMode.Initial);
            SetPageHostContent(RightContentHost, rightContent, PageHostUpdateMode.Initial);
            _isMainPageReady = true;
            return;
        }

        AnimateLeftPaneWidth(leftWidth);
        if (isInitial)
        {
            SetPageHostContent(LeftContentHost, leftContent, PageHostUpdateMode.Initial);
            SetPageHostContent(RightContentHost, rightContent, PageHostUpdateMode.Initial);
            return;
        }

        BeginMainPageTransition(leftContent, rightContent);
    }

    public void ShowMessage(string title, string markdown, bool isWarn = false)
    {
        ShowMessageWithActions(title, markdown, isWarn, button1: "关闭");
    }

    public void ShowMessageWithActions(
        string title,
        string markdown,
        bool isWarn = false,
        string button1 = "关闭",
        string? button2 = null,
        string? button3 = null,
        Action? onButton1 = null,
        Action? onButton2 = null,
        Action? onButton3 = null)
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;
        var message = new MyMsgMarkdown
        {
            Title = title,
            Markdown = markdown,
            IsWarn = isWarn,
            Button1 = button1,
            Button2 = button2,
            Button3 = button3
        };
        message.Button1Click += (_, _) =>
        {
            onButton1?.Invoke();
            CloseMessage();
        };
        message.Button2Click += (_, _) =>
        {
            onButton2?.Invoke();
            CloseMessage();
        };
        message.Button3Click += (_, _) =>
        {
            onButton3?.Invoke();
            CloseMessage();
        };
        PanMsg.Children.Add(message);
    }

    private MyMsgForm ShowFormDialog(string title, string button1 = "确定", string? button2 = "取消")
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;
        var form = new MyMsgForm { Title = title };
        form.Button1.Text = button1;
        form.Button2.Text = button2;
        form.Button2.IsVisible = !string.IsNullOrWhiteSpace(button2);
        form.Button1Click += (_, _) => PanMsgBackground.IsVisible = false;
        form.Button2Click += (_, _) => PanMsgBackground.IsVisible = false;
        PanMsg.Children.Add(form);
        return form;
    }

    public void CloseMessage()
    {
        foreach (var child in PanMsg.Children)
        {
            if (child is MyMsgMarkdown message)
                message.Close();
            else if (child is MyMsgForm form)
                form.Close();
        }

        PanMsgBackground.IsVisible = false;
    }

    private void InitializeFloatingActionButtons()
    {
        RegisterFloatingActionButton(BtnDownloadTasksFloating, 0);
        RegisterFloatingActionButton(BtnForceCloseGame, 1);
        BtnDownloadTasksFloating.Click += (_, _) => NavigateToDownloadTasks();
        BtnForceCloseGame.Click += (_, _) => _launchViewModel.ForceCloseGame();
        UpdateDownloadTasksButton();
        UpdateForceCloseGameButton();
    }

    private void RegisterFloatingActionButton(MyFloatingActionButton button, int index)
    {
        _floatingActions[button] = new FloatingActionState { Index = index };
        Canvas.SetLeft(button, 0);
        Canvas.SetBottom(button, 0);
        button.ScaleTransform.ScaleX = 0.72;
        button.ScaleTransform.ScaleY = 0.72;
        button.TranslateTransform.Y = 12;
    }

    private void UpdateForceCloseGameButton()
    {
        ShowFloatingActionButton(BtnForceCloseGame, 1, _launchViewModel.IsGameRunning);
    }

    private void UpdateDownloadTasksButton()
    {
        ShowFloatingActionButton(BtnDownloadTasksFloating, 0, HasVisibleDownloadTasks() && !IsDownloadTaskRoute());
    }

    private void ShowFloatingActionButton(MyFloatingActionButton button, int index, bool visible)
    {
        if (!_floatingActions.TryGetValue(button, out var state))
            RegisterFloatingActionButton(button, index);

        state = _floatingActions[button];
        state.Index = index;
        ModAnimation.AniStop(GetFloatingActionAnimationName(button));

        if (visible)
        {
            var wasShown = state.IsShown;
            state.IsShown = true;
            button.IsVisible = true;
            ReflowFloatingActionButtons();
            if (!wasShown)
            {
                ModAnimation.AniStop(GetFloatingActionMoveAnimationName(button));
                button.Opacity = 0;
                button.ScaleTransform.ScaleX = 0.72;
                button.ScaleTransform.ScaleY = 0.72;
                button.TranslateTransform.Y = 12;
                ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaOpacity(button, 1 - button.Opacity, 140, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaScaleTransform(button.ScaleTransform, 1, 300, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
                    ModAnimation.AaTranslateY(button.TranslateTransform, -button.TranslateTransform.Y, 260, ease: new ModAnimation.AniEaseOutFluent())
                }, GetFloatingActionAnimationName(button), true);
                if (ReferenceEquals(button, BtnDownloadTasksFloating))
                    PlayDownloadTaskRipple();
            }

            return;
        }

        if (!state.IsShown && !button.IsVisible)
            return;

        state.IsShown = false;
        ModAnimation.AniStop(GetFloatingActionMoveAnimationName(button));
        ReflowFloatingActionButtons();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(button, -button.Opacity, 110, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaScaleTransform(button.ScaleTransform, 0.72, 180, ease: new ModAnimation.AniEaseOutFluent(), absolute: true),
            ModAnimation.AaTranslateY(button.TranslateTransform, 12 - button.TranslateTransform.Y, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() => button.IsVisible = false, 180)
        }, GetFloatingActionAnimationName(button), true);
    }

    private void ReflowFloatingActionButtons()
    {
        const double spacing = 10;
        var shown = _floatingActions
            .Where(static pair => pair.Value.IsShown)
            .OrderBy(static pair => pair.Value.Index)
            .ThenBy(static pair => pair.Key.GetHashCode())
            .ToArray();

        PanFloatingActions.Height = shown.Length == 0
            ? 44
            : shown.Length * 44 + Math.Max(0, shown.Length - 1) * spacing;

        for (var i = 0; i < shown.Length; i++)
        {
            var button = shown[i].Key;
            var state = shown[i].Value;
            var targetBottom = i * (44 + spacing);
            var oldBottom = state.Bottom;
            state.Bottom = targetBottom;
            Canvas.SetBottom(button, targetBottom);
            button.TranslateTransform.Y += oldBottom - targetBottom;
            ModAnimation.AniStart(
                ModAnimation.AaTranslateY(button.TranslateTransform, -button.TranslateTransform.Y, 240,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                GetFloatingActionMoveAnimationName(button),
                true);
        }
    }

    private static string GetFloatingActionAnimationName(MyFloatingActionButton button) => $"FloatingAction Show {button.GetHashCode()}";

    private static string GetFloatingActionMoveAnimationName(MyFloatingActionButton button) => $"FloatingAction Move {button.GetHashCode()}";

    private void PlayDownloadTaskRipple()
    {
        var center = BtnDownloadTasksFloating.TranslatePoint(
            new Point(BtnDownloadTasksFloating.Bounds.Width / 2d, BtnDownloadTasksFloating.Bounds.Height / 2d),
            PanFloatingRipple);
        if (center is null)
            return;

        const double rippleSize = 44d;
        var ripple = new Border
        {
            Width = rippleSize,
            Height = rippleSize,
            CornerRadius = new CornerRadius(1000),
            BorderThickness = new Thickness(0.001),
            Opacity = 0.5,
            Background = ThemeBrushes.PrimaryHover,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ripple, center.Value.X - rippleSize / 2d);
        Canvas.SetTop(ripple, center.Value.Y - rippleSize / 2d);
        PanFloatingRipple.Children.Insert(0, ripple);

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaScaleTransform((ScaleTransform)ripple.RenderTransform, GetWindowRippleScale(center.Value, rippleSize), 1000,
                ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Strong, 0.3),
                absolute: true),
            ModAnimation.AaOpacity(ripple, -ripple.Opacity, 1000),
            ModAnimation.AaCode(() => PanFloatingRipple.Children.Remove(ripple), after: true)
        }, $"DownloadTask Ripple {Guid.NewGuid():N}");
    }

    private double GetWindowRippleScale(Point center, double rippleSize)
    {
        var width = PanFloatingRipple.Bounds.Width > 0 ? PanFloatingRipple.Bounds.Width : Bounds.Width;
        var height = PanFloatingRipple.Bounds.Height > 0 ? PanFloatingRipple.Bounds.Height : Bounds.Height;
        if (width <= 0 || height <= 0)
            return 13d;

        var maxDistance = new[]
        {
            Distance(center, new Point(0, 0)),
            Distance(center, new Point(width, 0)),
            Distance(center, new Point(0, height)),
            Distance(center, new Point(width, height))
        }.Max();
        return Math.Max(13d, maxDistance / (rippleSize / 2d));
    }

    private static double Distance(Point a, Point b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private void ShowLaunchFailureDialog(MinecraftLaunchResult result)
    {
        ShowMessageWithActions(
            "启动失败",
            $"{result.Message}\n\n可能原因：\n{_launchViewModel.LastCrashAnalysis}",
            isWarn: true,
            button1: "关闭",
            button2: "导出日志",
            button3: "打开日志",
            onButton2: ExportLaunchLogWithHint,
            onButton3: ExportAndOpenLaunchLog);
    }

    private void ShowCrashDialog(MinecraftProcessExitInfo exit)
    {
        var exitCode = exit.ExitCode?.ToString() ?? "未知";
        ShowMessageWithActions(
            "Minecraft 已崩溃",
            $"{exit.InstanceName} 异常退出，退出码：{exitCode}\n\n分析结果：\n{_launchViewModel.LastCrashAnalysis}",
            isWarn: true,
            button1: "关闭",
            button2: "导出日志",
            button3: "打开日志",
            onButton2: ExportLaunchLogWithHint,
            onButton3: ExportAndOpenLaunchLog);
    }

    private void ShowAccessibilityPermissionDialog()
    {
        ShowMessageWithActions(
            "需要辅助功能权限",
            "macOS 需要授予 Pixel Craft Launcher “辅助功能”权限，才能用 Accessibility Observer 监听 Minecraft 窗口出现。\n\n请在系统设置打开后，进入“隐私与安全性 → 辅助功能”，启用 Pixel Craft Launcher。授权完成后请重新点击启动。",
            isWarn: true,
            button1: "稍后",
            button2: "打开设置",
            onButton2: MinecraftProcessMonitor.RequestMacAccessibilityAccess);
    }

    private void ExportLaunchLogWithHint()
    {
        var path = _launchViewModel.ExportLaunchLog();
        ShowHint("启动日志已导出：" + path, HintType.Finish);
    }

    private void ExportAndOpenLaunchLog()
    {
        var path = _launchViewModel.ExportLaunchLog();
        OpenPath(path);
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public void ShowHint(string text, MyHint.Themes theme)
    {
        var type = theme switch
        {
            MyHint.Themes.Red => HintType.Critical,
            MyHint.Themes.Yellow => HintType.Finish,
            _ => HintType.Info
        };
        ShowHint(text, type);
    }

    public void ShowHint(string text, HintType type)
    {
        text = (text ?? string.Empty)
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (string.IsNullOrWhiteSpace(text) || PanHint.Children.Count >= 20)
            return;

        var duplicate = PanHint.Children
            .OfType<Border>()
            .FirstOrDefault(child => child.Tag is HintTag { Reusable: true } tag && tag.Text == text);

        if (duplicate is not null && duplicate.Tag is HintTag duplicateTag)
        {
            PlayDuplicateHint(duplicate, duplicateTag, type);
            return;
        }

        var tag = new HintTag(++_hintId, text);
        var hint = CreatePopupHint(text, type, tag);
        PanHint.Children.Add(hint);

        var translate = EnsureHintTransform(hint);
        var animations = new List<ModAnimation.AniData>();
        if (PanHint.Children.Count > 1)
            animations.Add(ModAnimation.AaHeight(hint, 26, 150, ease: new ModAnimation.AniEaseOutFluent()));
        else
            hint.Height = 26;

        animations.AddRange(new[]
        {
            ModAnimation.AaTranslateX(translate, 70, 450, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaOpacity(hint, 1, 100)
        });
        ModAnimation.AniStart(animations, $"Hint Show {tag.Id}", true);
        ScheduleHintHide(hint, tag, GetHintDelay(text));
    }

    private Border CreatePopupHint(string text, HintType type, HintTag tag)
    {
        var (start, end) = GetHintBrushColors(type);
        return new Border
        {
            Tag = tag,
            Margin = new Thickness(0, 0, 20, 0),
            Opacity = 0,
            Height = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            RenderTransform = new TranslateTransform(-70, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(start, 0),
                    new GradientStop(end, 1)
                }
            },
            Child = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 13,
                Foreground = ThemeBrushes.OnPrimary,
                Margin = new Thickness(33, 5, 8, 5)
            }
        };
    }

    private void PlayDuplicateHint(Border hint, HintTag tag, HintType type)
    {
        var translate = EnsureHintTransform(hint);
        var (start, end) = GetHintBrushColors(type);
        if (hint.Background is LinearGradientBrush gradient && gradient.GradientStops.Count >= 2)
        {
            gradient.GradientStops[0].Color = start;
            gradient.GradientStops[1].Color = end;
        }

        ModAnimation.AniStop($"Hint Hide {tag.Id}");
        ModAnimation.AniStop($"Hint Show {tag.Id}");
        hint.Opacity = 1;
        hint.Height = 26;
        tag.Reusable = true;

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaTranslateX(translate, 8, 50, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(translate, -8, 50, 50, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaTranslateX(translate, 8, 50, 100, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(translate, -8, 50, 150, new ModAnimation.AniEaseInFluent())
        }, $"Hint Show {tag.Id}", true);
        ScheduleHintHide(hint, tag, GetHintDelay(tag.Text));
    }

    private void ScheduleHintHide(Border hint, HintTag tag, int delay)
    {
        var translate = EnsureHintTransform(hint);
        ModAnimation.AniStop($"Hint Hide {tag.Id}");
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaTranslateX(translate, -70 - translate.X, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(hint, -1, 150, delay),
            ModAnimation.AaCode(() => tag.Reusable = false, delay),
            ModAnimation.AaHeight(hint, -26, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() => PanHint.Children.Remove(hint), after: true)
        }, $"Hint Hide {tag.Id}", true);
    }

    private static TranslateTransform EnsureHintTransform(Border hint)
    {
        if (hint.RenderTransform is TranslateTransform translate)
            return translate;
        translate = new TranslateTransform();
        hint.RenderTransform = translate;
        return translate;
    }

    private static int GetHintDelay(string text)
    {
        return (int)Math.Round(800 + Math.Clamp(text.Length, 5, 23) * 180d);
    }

    private static (Color start, Color end) GetHintBrushColors(HintType type)
    {
        return type switch
        {
            HintType.Finish => (ThemeBrushes.SuccessColor, ThemeBrushes.SuccessContainerColor),
            HintType.Critical => (ThemeBrushes.ErrorColor, ThemeBrushes.ErrorContainerColor),
            _ => (ThemeBrushes.InfoColor, ThemeBrushes.PrimaryHoverColor)
        };
    }

    private void WireShellEvents()
    {
        BtnLaunch.Check += (_, raiseByMouse) =>
        {
            if (raiseByMouse)
                _shellViewModel.NavigateLaunchCommand.Execute(null);
        };
        BtnDownload.Check += (_, raiseByMouse) =>
        {
            if (raiseByMouse)
                _shellViewModel.NavigateDownloadCommand.Execute(null);
        };
        BtnSetup.Check += (_, raiseByMouse) =>
        {
            if (raiseByMouse)
                _shellViewModel.NavigateSetupCommand.Execute(null);
        };
        BtnTools.Check += (_, raiseByMouse) =>
        {
            if (raiseByMouse)
                _shellViewModel.NavigateToolsCommand.Execute(null);
        };
        BtnTitleBack.Click += (_, _) =>
        {
            if (IsDownloadTaskRoute())
            {
                if (!_shellViewModel.Back())
                    _shellViewModel.Navigate(PixelRoutes.Launch());
            }
            else if (IsDownloadSecondaryPage())
            {
                NavigateBackFromDownloadSecondaryPage();
            }
            else if (IsProfileManagerRoute())
            {
                _shellViewModel.Navigate(PixelRoutes.Launch());
            }
            else if (IsLaunchInstanceRoute())
            {
                _shellViewModel.Navigate(PixelRoutes.Launch());
            }
        };
        PanMsgBackground.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, PanMsgBackground))
                CloseMessage();
        };
        PanPopupOverlay.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, PanPopupOverlay))
                HidePopupOverlay();
        };
    }

    private void WireThemeEvents()
    {
        ThemeService.ColorModeChanged += (_, _) => Dispatcher.UIThread.Post(RefreshShellTheme);
        ThemeService.ColorThemeChanged += _ => Dispatcher.UIThread.Post(RefreshShellTheme);
        PixelSettingsBinder.ObserveChanged("UiAcrylic", _ => Dispatcher.UIThread.Post(RefreshShellTheme));
        foreach (var key in new[]
        {
            "UiColorSchemeMode", "UiColorSchemeSeed", "UiColorSchemeImage", "UiColorSchemeAutoBackground"
        })
        {
            PixelSettingsBinder.ObserveChanged(key, _ => Dispatcher.UIThread.Post(() =>
            {
                RefreshShellTheme();
            }));
        }
    }

    private void WirePersonalizationEvents()
    {
        _personalization.BackgroundChanged += control =>
            Dispatcher.UIThread.Post(() =>
            {
                BackgroundMediaHost.Content = control;
                ApplyBackgroundOverlay();
            });
        _personalization.BackgroundImageChanged += _ =>
            Dispatcher.UIThread.Post(() =>
            {
                if (PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground") is bool enabled && enabled)
                    RefreshShellTheme();
            });

        foreach (var key in new[]
        {
            "UiLauncherTransparent", "UiLockWindowSize", "UiFont", "UiLogoType", "UiLogoText", "UiLogoLeft",
            "UiLauncherLogo", "UiBackgroundFolder", "UiBackgroundSuit", "UiBackgroundOpacity",
            "UiBackgroundCarousel", "UiBackgroundBlur", "UiBackgroundColorful", "UiAutoPauseVideo",
            "UiMusicVolume", "UiMusicRandom", "UiMusicAuto", "UiMusicStart", "UiMusicStop", "UiMusicSMTC",
            "UiCustomType", "UiCustomPreset", "UiCustomNet"
        })
        {
            PixelSettingsBinder.ObserveChanged(key, _ => Dispatcher.UIThread.Post(() =>
            {
                var reloadLibrary = key == "UiBackgroundFolder";
                ApplyWindowPersonalization();
                _personalization.RefreshFromSettings(reloadLibrary);
                if (SelectedMainPage == MainPageKind.Launch)
                    SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
            }));
        }
    }

    private void WireLauncherMiscEvents()
    {
        foreach (var key in new[]
        {
            "SystemDisableHardwareAcceleration", "SystemTelemetry", "SystemMaxLog", "UiAniFPS",
            "SystemNetEnableDoH", "SystemHttpProxyType", "SystemHttpProxy", "SystemHttpProxyCustomUsername",
            "SystemHttpProxyCustomPassword", "SystemDebugMode", "SystemDebugAnim", "SystemDebugDelay",
            "SystemDebugSkipCopy", "SystemDebugAllowRestrictedFeature", "UiHiddenPageDownload",
            "UiHiddenPageSetup", "UiHiddenPageTools", "UiHiddenSetupLaunch", "UiHiddenSetupJava",
            "UiHiddenSetupGameManage", "UiHiddenSetupGameLink", "UiHiddenSetupUi", "UiHiddenSetupLauncherMisc",
            "UiHiddenSetupAbout", "UiHiddenSetupUpdate", "UiHiddenSetupFeedback", "UiHiddenSetupLog",
            "UiHiddenToolsGameLink", "UiHiddenToolsHelp", "UiHiddenToolsTest", "UiHiddenVersionEdit",
            "UiHiddenVersionExport", "UiHiddenVersionSave", "UiHiddenVersionScreenshot", "UiHiddenVersionMod",
            "UiHiddenVersionResourcePack", "UiHiddenVersionShader", "UiHiddenVersionSchematic",
            "UiHiddenVersionServer", "UiHiddenFunctionSelect", "UiHiddenFunctionModUpdate", "UiHiddenFunctionHidden"
        })
        {
            PixelSettingsBinder.ObserveChanged(key, _ => Dispatcher.UIThread.Post(() =>
            {
                ApplyLauncherMiscSettings();
                if (key == "SystemDisableHardwareAcceleration")
                    ShowHint("禁用硬件加速将在下次启动启动器时生效。", HintType.Info);
                if (key.StartsWith("UiHidden", StringComparison.Ordinal))
                    RefreshSetupVisibility();
                if (SelectedMainPage == MainPageKind.Tools && key.StartsWith("UiHiddenTools", StringComparison.Ordinal))
                    SetPageHostContent(RightContentHost, BuildControlsPreviewPage(), PageHostUpdateMode.SilentRefresh);
                if (SelectedMainPage == MainPageKind.Launch && key.StartsWith("UiHiddenVersion", StringComparison.Ordinal))
                    RefreshLaunchPage(null);
                if (key == "SystemHttpProxyType")
                    RefreshSetupRightPage();
            }));
        }
    }

    private void ApplyLauncherMiscSettings()
    {
        ApplyFeatureVisibility();
        ApplyShellAnimationSettings();
    }

    private void ApplyFeatureVisibility()
    {
        BtnDownload.IsVisible = FeatureVisibilityService.IsMainPageVisible(MainPageKind.Download);
        BtnSetup.IsVisible = FeatureVisibilityService.IsMainPageVisible(MainPageKind.Setup);
        BtnTools.IsVisible = FeatureVisibilityService.IsMainPageVisible(MainPageKind.Tools);

        if (!FeatureVisibilityService.IsMainPageVisible(SelectedMainPage))
            _shellViewModel.Navigate(PixelRoutes.Launch());
    }

    private static void ApplyShellAnimationSettings()
    {
        var debugEnabled = PixelSettingsBinder.LoadValue("SystemDebugMode") is bool enabled && enabled;
        var rawSpeed = PixelSettingsBinder.LoadValue("SystemDebugAnim") is IConvertible value ? Convert.ToInt32(value) : 9;
        var scale = DebugSettingsService.ResolveAnimationScale(rawSpeed);
        ModAnimation.IsEnabled = !debugEnabled || rawSpeed > 0;
        ModAnimation.AniSpeed = debugEnabled ? scale : 1d;
    }

    private void RefreshSetupVisibility()
    {
        if (!FeatureVisibilityService.IsSetupSectionVisible(_selectedSetupSection))
            LoadInitialSetupSection();
        if (SelectedMainPage == MainPageKind.Setup)
        {
            SetPageHostContent(LeftContentHost, BuildSetupLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
        }
    }

    private void RefreshShellTheme()
    {
        ApplyAvaloniaThemeVariant();
        ApplyShellColors();
        ApplyWindowPersonalization();
        RefreshCurrentPageTheme();
    }

    private void ApplyAvaloniaThemeVariant()
    {
        var variant = ThemeService.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        RequestedThemeVariant = variant;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = variant;
    }

    private void ApplyShellColors()
    {
        var useAcrylic = IsAcrylicEnabled;
        var opacity = GetWindowOpacity();
        TransparencyLevelHint = useAcrylic
            ? [WindowTransparencyLevel.AcrylicBlur]
            : [WindowTransparencyLevel.None];
        Background = useAcrylic ? Brushes.Transparent : ThemeBrushes.ShellBackground;
        PanTitle.BackgroundBrush = ThemeBrushes.PrimaryContainer;
        PanTitle.Opacity = opacity;
        RightPane.Background = ThemeBrushes.ShellBackground;
        RightPane.Opacity = opacity;
        LeftPane.Background = useAcrylic ? Brushes.Transparent : ThemeBrushes.SidebarBackground;
        LeftPane.Opacity = opacity;
        LeftPane.BorderBrush = ThemeBrushes.SidebarBorder;
        SidebarAcrylic.IsVisible = useAcrylic;
        if (useAcrylic)
            SidebarAcrylic.Material = CreateSidebarAcrylicMaterial();
        SidebarAcrylic.InvalidateVisual();
        RefreshTitleBarControls();
    }

    private void ApplyWindowPersonalization()
    {
        CanResize = !(PixelSettingsBinder.LoadValue("UiLockWindowSize") is bool locked && locked);
        ApplyGlobalFont();
        ApplyTitleBarPersonalization();
        ApplyBackgroundOverlay();
    }

    private void ApplyGlobalFont()
    {
        var font = PixelSettingsBinder.LoadValue("UiFont")?.ToString();
        FontFamily = string.IsNullOrWhiteSpace(font)
            ? FontFamily.Default
            : new FontFamily($"{font}, Inter, Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC, Noto Sans, sans-serif");
    }

    private void ApplyTitleBarPersonalization()
    {
        var type = PixelSettingsBinder.LoadValue("UiLogoType") is int rawType ? (LauncherTitleType)rawType : LauncherTitleType.Default;
        var showDefaultLogo = PixelSettingsBinder.LoadValue("UiLauncherLogo") is not bool showLogo || showLogo;
        var text = PixelSettingsBinder.LoadValue("UiLogoText")?.ToString();
        var leftAlign = PixelSettingsBinder.LoadValue("UiLogoLeft") is bool left && left;

        PathTitleLogo.IsVisible = type == LauncherTitleType.Default && showDefaultLogo;
        ImgTitleLogo.IsVisible = false;
        LabTitleBrand.IsVisible = type == LauncherTitleType.Text || type == LauncherTitleType.Default;
        LabTitleBrand.Text = type == LauncherTitleType.Text
            ? string.IsNullOrWhiteSpace(text) ? "PCL" : text
            : showDefaultLogo ? string.Empty : "PCL";

        if (type == LauncherTitleType.Image)
            ApplyTitleImageLogo();

        Grid.SetColumn(PanTitleNav, leftAlign ? 0 : 1);
        PanTitleNav.HorizontalAlignment = HorizontalAlignment.Left;
        PanTitleNav.Margin = leftAlign ? new Thickness(132, 0, 13, 0) : new Thickness(13, 0);
    }

    private void ApplyTitleImageLogo()
    {
        var image = _personalization.Library.Images.FirstOrDefault()?.Path;
        if (string.IsNullOrWhiteSpace(image))
        {
            PathTitleLogo.IsVisible = true;
            LabTitleBrand.IsVisible = false;
            return;
        }

        try
        {
            ImgTitleLogo.Source = new Avalonia.Media.Imaging.Bitmap(image);
            ImgTitleLogo.IsVisible = true;
            PathTitleLogo.IsVisible = false;
            LabTitleBrand.IsVisible = false;
        }
        catch
        {
            ImgTitleLogo.IsVisible = false;
            PathTitleLogo.IsVisible = true;
            LabTitleBrand.IsVisible = false;
        }
    }

    private void ApplyBackgroundOverlay()
    {
        var hasBackground = BackgroundMediaHost.Content is not null;
        var colorful = PixelSettingsBinder.LoadValue("UiBackgroundColorful") is not bool value || value;
        BackgroundColorOverlay.Background = colorful ? ThemeBrushes.ShellBackground : ThemeBrushes.Background;
        BackgroundColorOverlay.Opacity = hasBackground ? (colorful ? 0.36 : 0.78) : 1;
    }

    private static double GetWindowOpacity()
    {
        var raw = PixelSettingsBinder.LoadValue("UiLauncherTransparent") is IConvertible value ? Convert.ToDouble(value) : 600d;
        return Math.Clamp(raw / 600d, 0.2d, 1d);
    }

    private static ExperimentalAcrylicMaterial CreateSidebarAcrylicMaterial()
    {
        var tint = ThemeBrushes.SidebarBackgroundColor;
        return new ExperimentalAcrylicMaterial
        {
            BackgroundSource = AcrylicBackgroundSource.Digger,
            TintColor = tint,
            TintOpacity = ThemeService.IsDarkMode ? 0.84 : 0.78,
            MaterialOpacity = ThemeService.IsDarkMode ? 0.72 : 0.65,
            FallbackColor = tint
        };
    }

    private void RefreshTitleBarControls()
    {
        BtnLaunch.RefreshTheme();
        BtnDownload.RefreshTheme();
        BtnSetup.RefreshTheme();
        BtnTools.RefreshTheme();
        BtnTitleBack.Foreground = ThemeBrushes.OnPrimaryContainer;
        BtnTitleHelp.Foreground = ThemeBrushes.OnPrimaryContainer;
        BtnTitleMin.Foreground = ThemeBrushes.OnPrimaryContainer;
        BtnTitleClose.Foreground = ThemeBrushes.OnPrimaryContainer;
    }

    private void RefreshCurrentPageTheme()
    {
        if (!_isMainPageReady)
            return;

        var page = SelectedMainPage;
        SetPageHostContent(LeftContentHost, page switch
        {
            MainPageKind.Launch => BuildLaunchLeftPage(),
            MainPageKind.Download => BuildDownloadLeftPage(),
            MainPageKind.Setup => BuildSetupLeftPage(),
            _ => BuildLeftPage(page)
        }, PageHostUpdateMode.SilentRefresh);

        SetPageHostContent(RightContentHost, page switch
        {
            MainPageKind.Launch => BuildLaunchRightPage(),
            MainPageKind.Download => BuildDownloadRightPage(),
            MainPageKind.Setup => BuildSetupRightPage(),
            MainPageKind.Tools => BuildControlsPreviewPage(),
            _ => BuildPlaceholderRightPage(page)
        }, PageHostUpdateMode.SilentRefresh);
    }

    private void ApplyPlatformChromeLayout()
    {
        if (OperatingSystem.IsMacOS())
        {
            PanTitleLeft.Margin = new Thickness(98, 0, 0, 0);
            PanTitleSecondary.Margin = new Thickness(98, 0, 0, 0);
        }
    }

    private void BeginMainPageTransition(Control leftContent, Control rightContent)
    {
        SetPageHostContent(LeftContentHost, leftContent, PageHostUpdateMode.MainTransition,
            content => TriggerLeftPageShowAnimation(content, SelectedMainPage));
        SetPageHostContent(RightContentHost, rightContent, PageHostUpdateMode.MainTransition);
    }

    private void RefreshLaunchPage(string? propertyName)
    {
        if (!_isMainPageReady || SelectedMainPage != MainPageKind.Launch)
            return;

        if (propertyName is nameof(PixelLaunchViewModel.Stage)
            or nameof(PixelLaunchViewModel.StatusText)
            or nameof(PixelLaunchViewModel.LaunchLog)
            or nameof(PixelLaunchViewModel.LaunchProgress)
            or nameof(PixelLaunchViewModel.LaunchProgressText)
            or nameof(PixelLaunchViewModel.LaunchTitleText)
            or nameof(PixelLaunchViewModel.IsGameRunning)
            or nameof(PixelLaunchViewModel.IsGameWindowDetected)
            or nameof(PixelLaunchViewModel.CanLaunch))
            return;

        if (propertyName is nameof(PixelLaunchViewModel.IsLaunching))
        {
            if (_lastLaunchIsLaunching == _launchViewModel.IsLaunching)
                return;
            _lastLaunchIsLaunching = _launchViewModel.IsLaunching;
            SetPageHostContent(
                LeftContentHost,
                BuildLaunchLeftPage(),
                PageHostUpdateMode.NestedTransition,
                content => TriggerLeftPageShowAnimation(content, MainPageKind.Launch));
            SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
            return;
        }

        if (propertyName is null
            or nameof(PixelLaunchViewModel.SelectedInstancePath)
            or nameof(PixelLaunchViewModel.SelectedInstanceName)
            or nameof(PixelLaunchViewModel.SelectedInstance)
            or nameof(PixelLaunchViewModel.SelectedProfile)
            or nameof(PixelLaunchViewModel.SelectedProfileMethod)
            or nameof(PixelLaunchViewModel.IsLaunching)
            or nameof(PixelLaunchViewModel.LaunchButtonText)
            or nameof(PixelLaunchViewModel.InstanceModels))
        {
            SetPageHostContent(LeftContentHost, BuildLaunchLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
        }
    }

    private double GetLeftPaneWidth(MainPageKind page)
    {
        if (IsDownloadSecondaryPage())
            return 300d;
        if (page == MainPageKind.Launch && IsLaunchInstanceRoute())
            return MainWindowViewModel.DefaultLeftPaneWidth;
        return page == MainPageKind.Launch ? MainWindowViewModel.LaunchLeftPaneWidth : MainWindowViewModel.DefaultLeftPaneWidth;
    }

    private Control BuildSecondaryLeftPage(RouteNode route)
    {
        if (IsDownloadTaskRoute(route))
            return BuildDownloadManagerLeftPage();
        if (IsProfileManagerRoute(route))
            return BuildProfileManagerLeftPage();

        return BuildLeftPage(SelectedMainPage);
    }

    private Control BuildSecondaryRightPage(RouteNode route)
    {
        if (IsDownloadTaskRoute(route))
            return BuildDownloadTaskDetailsPage();
        if (IsProfileManagerRoute(route))
            return BuildProfileManagerRightPage(route);

        return BuildPlaceholderRightPage(SelectedMainPage);
    }

    private static bool IsGlobalSecondaryRoute(RouteNode route) => route.StartsWith("secondary");

    private bool IsDownloadSecondaryPage()
    {
        return IsDownloadSecondaryRoute();
    }

    private bool IsDownloadInstallRoute()
    {
        var route = _shellViewModel.CurrentRoute;
        return route.StartsWith("download") &&
               route.Child?.StartsWith("minecraft") == true &&
               route.Child.Child?.StartsWith("install") == true;
    }

    private static bool IsDownloadInstallRoute(RouteNode route)
    {
        return route.StartsWith("download") &&
               route.Child?.StartsWith("minecraft") == true &&
               route.Child.Child?.StartsWith("install") == true;
    }

    private bool IsDownloadTaskRoute()
    {
        var route = _shellViewModel.CurrentRoute;
        return IsDownloadTaskRoute(route);
    }

    private static bool IsDownloadTaskRoute(RouteNode route)
    {
        return route.StartsWith("secondary") &&
               route.Parameters.TryGetValue("kind", out var kind) &&
               string.Equals(kind, "download-tasks", StringComparison.OrdinalIgnoreCase)
            || route.StartsWith("download") &&
               route.Child?.StartsWith("tasks") == true;
    }

    private bool IsProfileManagerRoute()
    {
        return IsProfileManagerRoute(_shellViewModel.CurrentRoute);
    }

    private static bool IsProfileManagerRoute(RouteNode route)
    {
        return route.StartsWith("secondary") &&
               route.Parameters.TryGetValue("kind", out var kind) &&
               string.Equals(kind, "profiles", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyDownloadTaskRouteSelection(RouteNode route)
    {
        if (!IsDownloadTaskRoute(route))
            return;

        var taskId = route.Parameters.TryGetValue("task", out var value) == true
            ? value
            : route.Child?.Parameters.TryGetValue("task", out value) == true
                ? value
                : null;
        _downloadViewModel.SelectTask(taskId);
    }

    private bool IsDownloadSecondaryRoute()
    {
        var route = _shellViewModel.CurrentRoute;
        return IsDownloadSecondaryRoute(route);
    }

    private static bool IsDownloadSecondaryRoute(RouteNode route) =>
        IsDownloadInstallRoute(route) || IsDownloadTaskRoute(route);

    private bool IsLaunchInstanceRoute()
    {
        var route = _shellViewModel.CurrentRoute;
        return IsLaunchInstanceRoute(route);
    }

    private static bool IsLaunchInstanceRoute(RouteNode route)
    {
        return route.StartsWith("launch") &&
               route.Child?.StartsWith("instances") == true;
    }

    private void NavigateBackFromDownloadSecondaryPage()
    {
        if (IsDownloadInstallRoute())
            CloseInstallSelectionWithoutRefresh();
        _shellViewModel.Navigate(PixelRoutes.DownloadMinecraft());
    }

    private void NavigateToDownloadTasks()
    {
        var taskId = _downloadViewModel.SelectedTask?.Id ?? _downloadViewModel.Tasks.FirstOrDefault()?.Id;
        _shellViewModel.NavigateDownloadTaskDetails(taskId);
    }

    private void CloseInstallSelectionWithoutRefresh()
    {
        _suppressDownloadRefresh = true;
        try
        {
            _downloadViewModel.CloseInstallSelection();
        }
        finally
        {
            _suppressDownloadRefresh = false;
        }
    }

    private void RefreshDownloadRightPage(bool refreshLeft)
    {
        ApplyTopTitleMode();
        AnimateLeftPaneWidth(GetLeftPaneWidth(MainPageKind.Download));
        if (refreshLeft)
        {
            SetPageHostContent(LeftContentHost, BuildDownloadLeftPage(), PageHostUpdateMode.SilentRefresh);
        }
        SetPageHostContent(RightContentHost, BuildDownloadRightPage(), PageHostUpdateMode.SilentRefresh);
    }

    private void ScheduleDownloadRightPageRefresh(bool refreshLeft = false)
    {
        if (_suppressDownloadRefresh)
            return;

        _downloadRefreshNeedsLeft |= refreshLeft;
        if (_isDownloadRefreshQueued)
            return;

        _isDownloadRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isDownloadRefreshQueued = false;
            var needsLeft = _downloadRefreshNeedsLeft;
            _downloadRefreshNeedsLeft = false;
            if (IsDownloadTaskRoute())
                RefreshDownloadTaskSecondaryPage(needsLeft);
            else if (SelectedMainPage == MainPageKind.Download)
                RefreshDownloadRightPage(needsLeft);
        }, DispatcherPriority.Background);
    }

    private void RefreshDownloadTaskSecondaryPage(bool refreshLeft)
    {
        if (TryReturnFromDownloadTasks())
            return;

        ApplyTopTitleMode();
        AnimateLeftPaneWidth(300d);
        if (refreshLeft)
            SetPageHostContent(LeftContentHost, BuildDownloadManagerLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildDownloadTaskDetailsPage(), PageHostUpdateMode.SilentRefresh);
    }

    private bool TryReturnFromDownloadTasks()
    {
        if (!IsDownloadTaskRoute() || HasVisibleDownloadTasks() || HasPendingDownloadOperation())
            return false;

        if (!_shellViewModel.Back())
            _shellViewModel.Navigate(PixelRoutes.DownloadMinecraft());
        return true;
    }

    private bool HasVisibleDownloadTasks()
    {
        return _downloadViewModel.Tasks.Any(IsVisibleDownloadTask) || HasPendingDownloadOperation();
    }

    private bool HasPendingDownloadOperation()
    {
        return _downloadViewModel.IsBusy && _downloadViewModel.SelectedVersion is not null;
    }

    private void ApplyTopTitleMode()
    {
        var isSecondary = IsDownloadSecondaryPage() || IsLaunchInstanceRoute() || IsProfileManagerRoute();
        if (IsProfileManagerRoute())
            LabTitleSecondary.Text = "档案管理";
        else if (IsDownloadSecondaryPage())
            LabTitleSecondary.Text = IsDownloadTaskRoute()
                ? "下载管理"
                : (_downloadViewModel.SelectedVersion?.Id ?? "Minecraft") + " 安装";
        else if (IsLaunchInstanceRoute())
            LabTitleSecondary.Text = "实例选择";
        if (_isTopTitleSecondary == isSecondary)
            return;

        _isTopTitleSecondary = isSecondary;
        AnimateTitleElement(PanTitleLeft, !isSecondary, "FrmMain TitleLogo", enterX: -10);
        AnimateTitleElement(PanTitleNav, !isSecondary, "FrmMain TitleNav", enterX: 10);
        AnimateTitleElement(PanTitleSecondary, isSecondary, "FrmMain TitleSecondary", enterX: -10);
    }

    private static void AnimateTitleElement(Control control, bool visible, string animationName, double enterX)
    {
        ModAnimation.AniStop(animationName);
        var translate = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
        control.RenderTransform = translate;

        if (visible)
        {
            control.IsVisible = true;
            control.Opacity = 0;
            translate.X = enterX;
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(control, 1, 140, ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaTranslateX(translate, -enterX, 220, ease: new ModAnimation.AniEaseOutFluent())
            }, animationName, true);
            return;
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(control, -control.Opacity, 100, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaTranslateX(translate, -enterX - translate.X, 140, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaCode(() =>
            {
                control.IsVisible = false;
                control.Opacity = 1;
                translate.X = 0;
            }, 145)
        }, animationName, true);
    }

    private void AnimateLeftPaneWidth(double targetWidth)
    {
        var currentWidth = double.IsNaN(LeftPane.Width) ? LeftPane.Bounds.Width : LeftPane.Width;
        if (Math.Abs(currentWidth - targetWidth) < 0.5)
            return;

        BeginPaneTransitionLayoutLock(currentWidth, targetWidth, 260);
        ModAnimation.AniStop("FrmMain LeftPaneWidth");
        ModAnimation.AniStart(
            ModAnimation.AaWidth(LeftPane, targetWidth - currentWidth, 220, ease: new ModAnimation.AniEaseOutFluent()),
            "FrmMain LeftPaneWidth",
            true);
    }

    private void BeginPaneTransitionLayoutLock(double currentLeftWidth, double targetLeftWidth, int duration)
    {
        var leftContentWidth = Math.Max(currentLeftWidth, targetLeftWidth);

        ModAnimation.AniStop("FrmMain PaneLayoutLock");
        RestoreHostMeasureWidth(RightContentHost);
        LockHostMeasureWidth(LeftContentHost, leftContentWidth);
        LeftPane.ClipToBounds = false;

        ModAnimation.AniStart(
            ModAnimation.AaCode(EndPaneTransitionLayoutLock, duration),
            "FrmMain PaneLayoutLock",
            true);
    }

    private void EndPaneTransitionLayoutLock()
    {
        RestoreHostMeasureWidth(LeftContentHost);
        RestoreHostMeasureWidth(RightContentHost);
        LeftPane.ClipToBounds = true;
    }

    private static void LockHostMeasureWidth(ContentControl host, double width)
    {
        if (width <= 0 || double.IsNaN(width) || double.IsInfinity(width))
            return;

        host.Width = width;
        host.MinWidth = width;
        host.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private static void RestoreHostMeasureWidth(ContentControl host)
    {
        host.Width = double.NaN;
        host.MinWidth = 0;
        host.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private static void PreparePageHost(ContentControl host)
    {
        host.Opacity = 1;
        host.IsHitTestVisible = true;
        if (host.RenderTransform is not TranslateTransform)
            host.RenderTransform = new TranslateTransform();
        if (host.RenderTransform is TranslateTransform translate)
        {
            translate.X = 0;
            translate.Y = 0;
        }
    }

    private void SetPageHostContent(
        ContentControl host,
        Control newContent,
        PageHostUpdateMode mode,
        Action<Control>? afterShown = null)
    {
        if (ReferenceEquals(host, RightContentHost))
            RestoreHostMeasureWidth(host);

        if (mode == PageHostUpdateMode.MainTransition || mode == PageHostUpdateMode.NestedTransition)
        {
            var animationName = GetPageHostAnimationName(host, mode);
            if (ReferenceEquals(host, LeftContentHost))
                AnimateLeftPageHost(host, newContent, animationName, afterShown);
            else
                AnimateRightPageHost(host, newContent, animationName, afterShown);
            return;
        }

        StopPageHostAnimations(host);
        host.Content = newContent;
        PreparePageHost(host);
        afterShown?.Invoke(newContent);
    }

    private string GetPageHostAnimationName(ContentControl host, PageHostUpdateMode mode)
    {
        if (mode == PageHostUpdateMode.NestedTransition)
            return ReferenceEquals(host, RightContentHost) ? "FrmMain NestedPageChangeRight" : "FrmMain NestedPageChangeLeft";
        return ReferenceEquals(host, LeftContentHost) ? "FrmMain PageChangeLeft" : "FrmMain PageChangeRight";
    }

    private void StopPageHostAnimations(ContentControl host)
    {
        if (ReferenceEquals(host, LeftContentHost))
        {
            ModAnimation.AniStop("FrmMain PageChangeLeft");
            ModAnimation.AniStop("FrmMain NestedPageChangeLeft");
            ModAnimation.AniStop("PageLeft LaunchScale");
            ModAnimation.AniStop("PageLeft MenuItems");
            return;
        }

        ModAnimation.AniStop("FrmMain PageChangeRight");
        ModAnimation.AniStop("FrmMain NestedPageChangeRight");
    }

    private void AnimateLeftPageHost(
        ContentControl host,
        Control newContent,
        string animationName,
        Action<Control>? afterShown = null)
    {
        ModAnimation.AniStop(animationName);
        StopPageHostAnimations(host);
        host.IsHitTestVisible = false;
        PreparePageHost(host);

        var oldContent = host.Content as Control;
        var animations = new List<ModAnimation.AniData>();
        var switchDelay = 120;

        if (oldContent is not null)
        {
            if (oldContent.RenderTransform is ScaleTransform scale)
            {
                animations.Add(ModAnimation.AaScaleTransform(scale, 0.96, 140,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak), absolute: true));
                animations.Add(ModAnimation.AaOpacity(oldContent, -oldContent.Opacity, 110,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            }
            else
            {
                var controls = GetSidebarAnimControls(oldContent).Reverse().ToArray();
                var delay = 0;
                foreach (var control in controls)
                {
                    var translate = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translate;
                    animations.Add(ModAnimation.AaOpacity(control, -control.Opacity, 90, delay,
                        new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                    animations.Add(ModAnimation.AaTranslateX(translate, -20 - translate.X, 130, delay,
                        new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                    delay += 12;
                }

                switchDelay = Math.Max(110, delay + 80);
            }
        }

        animations.AddRange(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ModAnimation.AniControlEnabled += 1;
                host.Content = newContent;
                ModAnimation.AniControlEnabled -= 1;
                PreparePageHost(host);
                afterShown?.Invoke(newContent);
            }, switchDelay),
            ModAnimation.AaCode(() =>
            {
                PreparePageHost(host);
                host.IsHitTestVisible = true;
            }, switchDelay + 420)
        });
        ModAnimation.AniStart(animations, animationName, true);
    }

    private void AnimateRightPageHost(
        ContentControl host,
        Control newContent,
        string animationName,
        Action<Control>? afterShown = null)
    {
        ModAnimation.AniStop(animationName);
        StopPageHostAnimations(host);
        host.IsHitTestVisible = false;
        PreparePageHost(host);

        var oldItems = GetRightPagePrimaryControls(host.Content as Control).Reverse().ToArray();
        var animations = new List<ModAnimation.AniData>();
        var delay = 0;
        foreach (var item in oldItems)
        {
            var translate = item.RenderTransform as TranslateTransform ?? new TranslateTransform();
            item.RenderTransform = translate;
            animations.Add(ModAnimation.AaOpacity(item, -item.Opacity, 90, delay,
                new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateY(translate, -18 - translate.Y, 130, delay,
                new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            delay += 18;
        }

        var switchDelay = oldItems.Length == 0 ? 80 : delay + 90;
        PrepareRightPageEnterItems(newContent);
        animations.Add(ModAnimation.AaCode(() =>
        {
            ModAnimation.AniControlEnabled += 1;
            host.Content = newContent;
            ModAnimation.AniControlEnabled -= 1;
            PreparePageHost(host);
            afterShown?.Invoke(newContent);
        }, switchDelay));

        var enterDelay = switchDelay + 35;
        foreach (var item in GetRightPagePrimaryControls(newContent))
        {
            if (item.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform();
                item.RenderTransform = translate;
            }

            animations.Add(ModAnimation.AaOpacity(item, 1 - item.Opacity, 130, enterDelay,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateY(translate, -translate.Y, 260, enterDelay,
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            enterDelay += 28;
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            ResetRightPageEnterItems(newContent);
            PreparePageHost(host);
            host.IsHitTestVisible = true;
        }, enterDelay + 260));
        ModAnimation.AniStart(animations, animationName, true);
    }

    private static void PrepareRightPageEnterItems(Control root)
    {
        foreach (var item in GetRightPagePrimaryControls(root))
        {
            item.Opacity = 0;
            item.RenderTransform = new TranslateTransform(0, 22);
        }
    }

    private static void ResetRightPageEnterItems(Control root)
    {
        foreach (var item in GetRightPagePrimaryControls(root))
        {
            item.Opacity = 1;
            if (item.RenderTransform is TranslateTransform translate)
            {
                translate.X = 0;
                translate.Y = 0;
            }
        }
    }

    private static IEnumerable<Control> GetRightPagePrimaryControls(Control? root)
    {
        if (root is null)
            yield break;

        if (root is MainPaneScrollHost scrollHost)
        {
            foreach (var child in scrollHost.Children.OfType<Control>())
            {
                if (child is Panel panel)
                {
                    foreach (var panelChild in panel.Children.OfType<Control>().Where(static c => c.IsVisible))
                        yield return panelChild;
                }
                else if (child.IsVisible)
                {
                    yield return child;
                }
            }
            yield break;
        }

        if (root is Panel rootPanel)
        {
            foreach (var child in rootPanel.Children.OfType<Control>().Where(static c => c.IsVisible))
                yield return child;
            yield break;
        }

        yield return root;
    }




    private void PanTitle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsTitleInteractiveSource(e.Source as Visual))
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnWindowPointerPressedForFocus(object? sender, PointerPressedEventArgs e)
    {
        if (IsInputFocusSource(e.Source as Visual))
            return;

        var focused = FocusManager?.GetFocusedElement();
        if (focused is TextBox or ComboBox)
            Focus(NavigationMethod.Pointer);
    }

    private static bool IsInputFocusSource(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox or ComboBox)
                return true;
        }

        return false;
    }

    private static bool IsTitleInteractiveSource(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or TextBox or ComboBox)
                return true;
        }

        return false;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isCloseRequestedByLifecycle || Lifecycle.HasShutdownStarted)
            return;

        _personalization.Dispose();
        e.Cancel = true;
        RequestLifecycleShutdown();
    }

    private void RequestLifecycleShutdown()
    {
        if (Lifecycle.HasShutdownStarted)
        {
            _isCloseRequestedByLifecycle = true;
            Close();
            return;
        }

        IsEnabled = false;
        ShowHint("正在关闭启动器……", HintType.Info);
        Lifecycle.Shutdown();
    }

    private void BtnTitleMin_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnTitleClose_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RequestLifecycleShutdown();
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
