using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
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

        var leftContent = shouldRefreshLeft ? BuildRouteLeftPage(route, page) : null;
        if (leftContent is not null)
        {
            SetPageHostContent(
                LeftContentHost,
                leftContent,
                PageHostUpdateMode.NestedTransition,
                page == MainPageKind.Launch
                    ? content => LaunchPageVisualEffects.TriggerLeftPageShowAnimation(content, page)
                    : (Action<Control>?)null);
        }

        var rightContent = BuildRouteRightPage(route, page);
        SetPageHostContent(RightContentHost, rightContent, PageHostUpdateMode.NestedTransition);
    }

    private void ApplyRoute(RouteNode route, bool isInitial)
    {
        ApplyDownloadTaskRouteSelection(route);
        var page = _shellViewModel.SelectedMainPage;
        if (!_shellVisibilityService.IsMainPageVisible(page))
        {
            _shellViewModel.NavigateMainPage(MainPageKind.Launch);
            return;
        }

        BtnLaunch.Checked = page == MainPageKind.Launch;
        BtnDownload.Checked = page == MainPageKind.Download;
        BtnSetup.Checked = page == MainPageKind.Setup;
        BtnTools.Checked = page == MainPageKind.Tools;
        ApplyTopTitleMode();
        UpdateDownloadTasksButton();

        var leftContent = BuildRouteLeftPage(route, page);
        var rightContent = BuildRouteRightPage(route, page);
        var leftWidth = IsGlobalSecondaryRoute(route) ? MainWindowViewModel.SecondaryLeftPaneWidth : GetLeftPaneWidth(page);

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
}
