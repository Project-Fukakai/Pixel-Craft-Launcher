using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Views.Pages;
namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildLaunchLeftPage()
    {
        if (IsLaunchInstanceRoute())
            return BuildLaunchInstanceLeftPage();

        var root = new Grid
        {
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(1, 1)
        };
        var launchInputPanel = BuildLaunchInputPanel();
        root.Children.Add(launchInputPanel);
        root.Children.Add(BuildLaunchingPanel());
        return root;
    }

    private Control BuildLaunchInputPanel()
    {
        var sidebarSnapshot = _launchViewModel.GetSidebarSnapshot(_launchSidebarService);
        var messages = _launchSidebarService.GetPageMessages();
        return new LaunchInputPanelView(
            _launchViewModel.IsLaunching,
            BuildLaunchAccountPanel(),
            BuildLaunchMainButtonPanel(),
            sidebarSnapshot.SecondaryActions,
            messages,
            () => _shellViewModel.NavigateLaunchInstances(),
            () => ShowMessage(messages.InstanceSettingsTitle, messages.InstanceSettingsPlaceholder));
    }

    private Control BuildLaunchMainButtonPanel()
    {
        return new LaunchMainButtonView(
            _launchViewModel,
            () => SwitchMainPage(MainPageKind.Download),
            () => _launchViewModel.StartLaunch());
    }

    private Control BuildLaunchAccountPanel()
    {
        return new LaunchAccountPanelView(
            _launchViewModel,
            _launchSidebarService.GetPageMessages(),
            () => _shellViewModel.NavigateProfileManager());
    }

    private Control BuildLaunchingPanel()
    {
        return new LaunchLaunchingPanelView(
            _launchViewModel,
            _launchSidebarService.GetPageMessages(),
            GetLaunchLoadingState(),
            () => _launchViewModel.CancelLaunch());
    }

    private PixelLoadingTriggerAdapter GetLaunchLoadingState() =>
        _launchLoadingState ??= new PixelLoadingTriggerAdapter(_launchViewModel.LoadingState);

    private Control BuildLaunchRightPage()
    {
        if (IsLaunchInstanceRoute())
            return BuildLaunchInstanceSelectionPage();

        return new LaunchRightPageView(
            BuildCustomHomepageControl(),
            _launchSidebarService.GetPageMessages());
    }

    private Control? BuildCustomHomepageControl()
    {
        var snapshot = _homepageService.GetSnapshot();
        return snapshot.Kind == PixelHomepageKind.None
            ? null
            : new LaunchHomepageView(snapshot, OpenUrl);
    }

}
