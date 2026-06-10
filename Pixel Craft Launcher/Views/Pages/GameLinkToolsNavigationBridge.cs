using System;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkToolsNavigationBridge(
    PixelGameLinkViewModel viewModel,
    Action<bool> refreshGameLinkPage,
    Action openControlsPreview)
{
    public PixelGameLinkSubpage Subpage => viewModel.Subpage;

    public void RefreshGameLinkPage() => refreshGameLinkPage(false);

    public void ShowEula()
    {
        viewModel.ShowEula();
        refreshGameLinkPage(true);
    }

    public void ShowSelectOrEula()
    {
        viewModel.ShowSelectOrEula();
        refreshGameLinkPage(true);
    }

    public void ShowConnectedLobby()
    {
        viewModel.TryShowConnectedLobby();
        refreshGameLinkPage(true);
    }

    public void OpenControlsPreview() => openControlsPreview();
}
