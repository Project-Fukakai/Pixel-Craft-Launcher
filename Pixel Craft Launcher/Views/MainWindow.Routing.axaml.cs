using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private double GetLeftPaneWidth(MainPageKind page)
    {
        if (IsDownloadSecondaryPage())
            return MainWindowViewModel.SecondaryLeftPaneWidth;
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
            return BuildDownloadRightPage();
        if (IsProfileManagerRoute(route))
            return BuildProfileManagerRightPage(route);

        return BuildPlaceholderRightPage(SelectedMainPage);
    }

    private Control BuildRouteLeftPage(RouteNode route, MainPageKind page)
    {
        return IsGlobalSecondaryRoute(route) ? BuildSecondaryLeftPage(route) : BuildMainLeftPage(page);
    }

    private Control BuildRouteRightPage(RouteNode route, MainPageKind page)
    {
        return IsGlobalSecondaryRoute(route) ? BuildSecondaryRightPage(route) : BuildMainRightPage(page);
    }

    private Control BuildMainLeftPage(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Launch => BuildLaunchLeftPage(),
            MainPageKind.Download => BuildDownloadLeftPage(),
            MainPageKind.Setup => BuildSetupLeftPage(),
            _ => BuildLeftPage(page)
        };
    }

    private Control BuildMainRightPage(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Launch => BuildLaunchRightPage(),
            MainPageKind.Download => BuildDownloadRightPage(),
            MainPageKind.Setup => BuildSetupRightPage(),
            MainPageKind.Tools => BuildControlsPreviewPage(),
            _ => BuildPlaceholderRightPage(page)
        };
    }

    private static bool IsGlobalSecondaryRoute(RouteNode route) => MainWindowViewModel.IsGlobalSecondaryRoute(route);

    private bool IsDownloadSecondaryPage()
    {
        return IsDownloadSecondaryRoute();
    }

    private bool IsDownloadInstallRoute()
    {
        return _shellViewModel.IsCurrentDownloadInstallRoute();
    }

    private bool IsDownloadTaskRoute()
    {
        return _shellViewModel.IsCurrentDownloadTaskRoute();
    }

    private static bool IsDownloadTaskRoute(RouteNode route)
    {
        return MainWindowViewModel.IsDownloadTaskRoute(route);
    }

    private bool IsProfileManagerRoute()
    {
        return _shellViewModel.IsCurrentProfileManagerRoute();
    }

    private static bool IsProfileManagerRoute(RouteNode route)
    {
        return MainWindowViewModel.IsProfileManagerRoute(route);
    }

    private void ApplyDownloadTaskRouteSelection(RouteNode route)
    {
        if (!IsDownloadTaskRoute(route))
            return;

        _downloadViewModel.SelectTask(MainWindowViewModel.GetDownloadTaskRouteTaskId(route));
    }

    private bool IsDownloadSecondaryRoute()
    {
        return _shellViewModel.IsCurrentDownloadSecondaryRoute();
    }

    private static bool IsDownloadSecondaryRoute(RouteNode route) =>
        MainWindowViewModel.IsDownloadSecondaryRoute(route);

    private bool IsLaunchInstanceRoute()
    {
        return _shellViewModel.IsCurrentLaunchInstanceRoute();
    }

    private static bool IsLaunchInstanceRoute(RouteNode route)
    {
        return MainWindowViewModel.IsLaunchInstanceRoute(route);
    }

    private void NavigateBackFromDownloadSecondaryPage()
    {
        if (IsDownloadInstallRoute())
            CloseInstallSelectionWithoutRefresh();
        _shellViewModel.NavigateMainPage(MainPageKind.Download);
    }

    private void NavigateToDownloadTasks()
    {
        _shellViewModel.NavigateDownloadTaskDetails(_downloadViewModel.GetTaskDetailsRouteTaskId());
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
        AnimateLeftPaneWidth(MainWindowViewModel.SecondaryLeftPaneWidth);
        if (refreshLeft)
            SetPageHostContent(LeftContentHost, BuildDownloadManagerLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildDownloadRightPage(), PageHostUpdateMode.SilentRefresh);
    }

    private bool TryReturnFromDownloadTasks()
    {
        if (!IsDownloadTaskRoute() || !_downloadViewModel.ShouldReturnFromTaskDetails())
            return false;

        if (!_shellViewModel.Back())
            _shellViewModel.NavigateMainPage(MainPageKind.Download);
        return true;
    }

    private bool HasVisibleDownloadTasks()
    {
        return _downloadViewModel.HasVisibleTasksOrPendingOperation();
    }

    private void ApplyTopTitleMode()
    {
        var snapshot = MainWindowViewModel.GetSecondaryTitleSnapshot(
            _shellViewModel.CurrentRoute,
            _downloadViewModel.GetInstallStateSnapshot().SecondaryTitle);
        var isSecondary = snapshot.IsVisible;
        LabTitleSecondary.Text = snapshot.Title;
        if (_isTopTitleSecondary == isSecondary)
            return;

        _isTopTitleSecondary = isSecondary;
        AnimateTitleElement(PanTitleLeft, !isSecondary, "FrmMain TitleLogo", enterX: -10);
        AnimateTitleElement(PanTitleNav, !isSecondary, "FrmMain TitleNav", enterX: 10);
        AnimateTitleElement(PanTitleSecondary, isSecondary, "FrmMain TitleSecondary", enterX: -10);
    }
}
