using Avalonia.Controls;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Views.Pages;
using PCL.Core.App.Pixel.Shell;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildPlaceholderRightPage(MainPageKind page)
    {
        var snapshot = MainWindowViewModel.GetPlaceholderPageSnapshot(page);
        return new PixelPlaceholderPageView(snapshot);
    }

    private Control BuildControlsPreviewPage()
    {
        if (!_shellVisibilityService.IsToolVisible(PixelToolFeature.GameLink))
        {
            var hidden = _shellVisibilityService.GetToolHiddenMessage(PixelToolFeature.GameLink);
            return new PixelMessagePageView(hidden.Title, hidden.Message);
        }

        return BuildGameLinkToolsPage();
    }

    private Control BuildControlsPreviewContentPage()
    {
        if (!_shellVisibilityService.IsToolVisible(PixelToolFeature.Test))
        {
            var hidden = _shellVisibilityService.GetToolHiddenMessage(PixelToolFeature.Test);
            return new PixelMessagePageView(hidden.Title, hidden.Message);
        }

        return new ControlsPreviewPageView(
            MainWindowViewModel.GetControlsPreviewSnapshot(),
            (title, message) => ShowMessage(title, message));
    }

}
