using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkAnnouncementHintView : MyHint
{
    public GameLinkAnnouncementHintView(PixelGameLinkAnnouncementSnapshot snapshot)
    {
        CanClose = false;
        Theme = snapshot.Severity switch
        {
            PixelGameLinkAnnouncementSeverity.Important => Themes.Red,
            PixelGameLinkAnnouncementSeverity.Warning => Themes.Yellow,
            _ => Themes.Blue
        };
        Text = snapshot.Text;
    }
}
