using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel.Shell;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    public bool SwitchMainPage(MainPageKind page)
    {
        if (!_shellVisibilityService.IsMainPageVisible(page))
            return false;

        return _shellViewModel.NavigateMainPage(page);
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
                    _shellViewModel.NavigateMainPage(MainPageKind.Launch);
            }
            else if (IsDownloadSecondaryPage())
            {
                NavigateBackFromDownloadSecondaryPage();
            }
            else if (IsProfileManagerRoute())
            {
                _shellViewModel.NavigateMainPage(MainPageKind.Launch);
            }
            else if (IsLaunchInstanceRoute())
            {
                _shellViewModel.NavigateMainPage(MainPageKind.Launch);
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

    private void ApplyPlatformChromeLayout()
    {
        if (OperatingSystem.IsMacOS())
        {
            PanTitleLeft.Margin = new Thickness(98, 0, 0, 0);
            PanTitleSecondary.Margin = new Thickness(98, 0, 0, 0);
        }
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
        DisposeSettingsSubscriptions();
        DisposeGameLinkSubscriptions();
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
        ShowHint(_shellSettingsService.GetMessages().ClosingMessage, HintType.Info);
        Lifecycle.Shutdown();
    }

    private void BtnTitleMin_OnClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnTitleClose_OnClick(object? sender, RoutedEventArgs e)
    {
        RequestLifecycleShutdown();
    }
}
