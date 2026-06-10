using Avalonia.Controls;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadLoadingPageView : UserControl
{
    public DownloadLoadingPageView(string loadingText, ILoadingTrigger state)
    {
        Content = new Grid
        {
            Children =
            {
                new MyLoading
                {
                    Text = loadingText,
                    State = state,
                    ShowProgress = false
                }
            }
        };
    }
}
