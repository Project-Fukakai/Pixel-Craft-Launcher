using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.ViewModels;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkAnnouncementPlatformBridge(
    PixelGameLinkViewModel gameLinkViewModel,
    PixelGameLinkToolsPageController gameLinkToolsController,
    Func<bool> isToolsPageSelected,
    Action refreshGameLinkPage) : IDisposable
{
    private DispatcherTimer? _announcementTimer;

    public async Task LoadAnnouncementsAsync()
    {
        if (gameLinkViewModel.IsAnnouncementLoading)
            return;

        var result = await gameLinkToolsController.LoadAnnouncementsAsync();
        if (result.Started && result.IsSuccess)
            StartTimer();
        refreshGameLinkPage();
    }

    public void Dispose()
    {
        _announcementTimer?.Stop();
        _announcementTimer = null;
    }

    private void StartTimer()
    {
        if (_announcementTimer is not null)
            return;

        _announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _announcementTimer.Tick += (_, _) =>
        {
            if (!isToolsPageSelected() || !gameLinkViewModel.AdvanceAnnouncement())
                return;
            refreshGameLinkPage();
        };
        _announcementTimer.Start();
    }
}
