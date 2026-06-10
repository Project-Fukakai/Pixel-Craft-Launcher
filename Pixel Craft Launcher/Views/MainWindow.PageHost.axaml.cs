using System;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private void BeginMainPageTransition(Control leftContent, Control rightContent)
    {
        SetPageHostContent(LeftContentHost, leftContent, PageHostUpdateMode.MainTransition,
            content => LaunchPageVisualEffects.TriggerLeftPageShowAnimation(content, SelectedMainPage));
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
                content => LaunchPageVisualEffects.TriggerLeftPageShowAnimation(content, MainPageKind.Launch));
            SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
            return;
        }

        if (propertyName is null
            or nameof(PixelLaunchViewModel.SelectedInstanceName)
            or nameof(PixelLaunchViewModel.SelectedInstancePath)
            or nameof(PixelLaunchViewModel.Summary)
            or nameof(PixelLaunchViewModel.IsLaunching)
            or nameof(PixelLaunchViewModel.LaunchButtonText)
            or nameof(PixelLaunchViewModel.Instances))
        {
            SetPageHostContent(LeftContentHost, BuildLaunchLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildLaunchRightPage(), PageHostUpdateMode.SilentRefresh);
        }
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
        _pageHostTransitions.AnimateLeftPaneWidth(targetWidth);
    }

    private void SetPageHostContent(
        ContentControl host,
        Control newContent,
        PageHostUpdateMode mode,
        Action<Control>? afterShown = null)
    {
        _pageHostTransitions.SetContent(host, newContent, mode, afterShown);
    }
}
