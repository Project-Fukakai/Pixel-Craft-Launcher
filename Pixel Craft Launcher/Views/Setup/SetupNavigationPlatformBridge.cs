using System;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Services;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class SetupNavigationPlatformBridge(
    PixelSetupNavigationService setupNavigationService,
    PixelPersonalizationService pixelPersonalizationService,
    PersonalizationPlatformBridge personalizationService,
    Action<string, HintType> showHint,
    Action<string> openUrl,
    Action<string> openPath,
    Action<PixelSettingSectionKind> navigateSetupSection)
{
    public void NavigateSetupShortcut(PixelSettingSectionKind section)
    {
        setupNavigationService.SelectSection(section);
        navigateSetupSection(section);
    }

    public void OpenExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            openUrl(url);
        }
        catch (Exception ex)
        {
            var messages = setupNavigationService.GetMessages();
            showHint(messages.GetOpenExternalUrlFailedMessage(ex), HintType.Critical);
        }
    }

    public void OpenPersonalizationBackgroundFolder()
    {
        try
        {
            openPath(personalizationService.EnsureBackgroundFolder());
        }
        catch (Exception ex)
        {
            var messages = pixelPersonalizationService.GetMessages();
            showHint(messages.GetOpenBackgroundFolderFailedMessage(ex), HintType.Critical);
        }
    }
}
