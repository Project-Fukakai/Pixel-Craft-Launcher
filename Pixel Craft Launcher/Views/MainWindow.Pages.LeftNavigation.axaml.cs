using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Views.Pages;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildLeftPage(MainPageKind page)
    {
        if (page == MainPageKind.Tools)
            return BuildToolsLeftPage();

        var stack = new StackPanel
        {
            Margin = new Thickness(14, 16),
            Spacing = 10
        };

        stack.Children.Add(new TextBlock
        {
            Text = MainWindowViewModel.GetPlaceholderPageSnapshot(page).Title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = BodyForeground,
            Margin = new Thickness(2, 0, 2, 8)
        });

        foreach (var item in MainWindowViewModel.GetPlaceholderLeftItemSnapshots(page))
        {
            stack.Children.Add(new MyListItem
            {
                Title = item.Title,
                Info = item.Info,
                Type = MyListItem.CheckType.RadioBox,
                Checked = item.IsActive,
                Icon = item.Icon,
                IsSidebarItem = true
            });
        }

        return stack;
    }

    private Control BuildToolsLeftPage()
    {
        var gameLink = _gameLinkViewModel.SidebarSnapshot;
        var launchSidebar = _launchViewModel.GetSidebarSnapshot(_launchSidebarService);
        return new ToolsLeftPageView(
            gameLink,
            launchSidebar.IsTestToolVisible,
            GetGameLinkToolsNavigationBridge());
    }

    private Control BuildDownloadLeftPage()
    {
        return GetDownloadPageView().BuildLeftPage(
            _selectedDownloadPage,
            IsDownloadSecondaryPage(),
            IsDownloadTaskRoute());
    }

    private Control BuildDownloadManagerLeftPage()
    {
        return GetDownloadPageView().BuildManagerLeftPage(_selectedDownloadPage);
    }

    private void RefreshDownloadCategory(int tag)
    {
        _ = _downloadViewModel.RefreshDownloadCategoryAsync(tag);

        var messages = _downloadViewModel.GetPageMessagesSnapshot();
        ShowHint(messages.Window.RefreshStartMessage, HintType.Info);
    }

    private Control BuildSetupLeftPage()
    {
        return new SetupSidebarView(
            _setupNavigationService.GetSidebarSnapshot(_selectedSetupSection),
            NavigateSetupSectionFromSidebar,
            ResetSetupSectionFromSidebar);
    }

    private void NavigateSetupSectionFromSidebar(PixelSettingSectionKind kind)
    {
        _setupNavigationService.SelectSection(kind);
        _shellViewModel.NavigateSetupSection(kind);
    }

    private void ResetSetupSectionFromSidebar(PixelSettingSectionKind kind)
    {
        var resetSection = _setupNavigationService.ResetSection(kind);
        var messages = _setupNavigationService.GetMessages();
        ShowHint(messages.GetSectionResetMessage(resetSection), HintType.Finish);
        SetPageHostContent(LeftContentHost, BuildSetupLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
    }
}
