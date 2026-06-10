using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Navigation;
using PCL.Core.App.Pixel.Slices.GameLink;
using PCL.Core.App.Pixel.Slices.Java;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Views.Setup;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class SetupRightPageFactory(
    PixelSettingSectionKind selectedSection,
    PixelGameLinkViewModel gameLinkViewModel,
    PixelGameLinkToolsPageController gameLinkToolsController,
    PixelJavaService javaService,
    PixelSetupNavigationService setupNavigationService,
    PixelSettingDisplayService settingDisplayService,
    PixelSettingValueService settingValueService,
    PixelColorSchemeSettingsService colorSchemeSettingsService,
    PixelMemoryPreviewService memoryPreviewService,
    PixelLaunchViewModel launchViewModel,
    IStorageProvider storageProvider,
    Func<Task> addJava,
    Func<Task> refreshJavaList,
    Func<PixelJavaEntrySnapshot?, Task> selectDefaultJava,
    Func<PixelJavaEntrySnapshot, Task> toggleJava,
    Action<string> openJavaFolder,
    Action<PixelJavaEntrySnapshot> showJavaInfo,
    Action<string, HintType> showHint,
    Action<string?, object?> settingChanged,
    Action openPersonalizationBackgroundFolder,
    Action<string?> openExternalUrl,
    Action<PixelSettingSectionKind> navigateSetupShortcut,
    Func<string, MyMsgForm> showFormDialog)
{
    private readonly PixelColorSchemeThemeBridge _colorSchemeThemeBridge = new();

    public Control Build()
    {
        if (selectedSection == PixelSettingSectionKind.Java)
            return BuildJavaSetupRightPage();
        if (selectedSection == PixelSettingSectionKind.GameLink)
            return BuildGameLinkSetupRightPage();
        if (selectedSection == PixelSettingSectionKind.About)
            return BuildAboutRightPage();

        var section = PixelSettingsCatalog.Get(selectedSection);
        return new SettingsSectionView(
            section,
            setting => settingDisplayService.ShouldDisplay(setting),
            BuildSettingControl);
    }

    private Control BuildGameLinkSetupRightPage()
    {
        var section = PixelSettingsCatalog.Get(PixelSettingSectionKind.GameLink);
        return new GameLinkSetupPageView(
            section,
            gameLinkViewModel.SetupPageSnapshot,
            BuildSettingControl,
            BuildGameLinkNetworkTestPanel());
    }

    private Control BuildGameLinkNetworkTestPanel()
    {
        return new GameLinkNetworkTestPanelView(
            gameLinkToolsController.GetNetworkTestPanelSnapshot(),
            async () => await gameLinkToolsController.RunSetupNatTestAsync(),
            (message, ok) => showHint(message, ok ? HintType.Finish : HintType.Critical));
    }

    private Control BuildAboutRightPage()
    {
        return new AboutPageView(
            setupNavigationService.GetAboutPageSnapshot(),
            openExternalUrl,
            navigateSetupShortcut);
    }

    private Control BuildJavaSetupRightPage()
    {
        return new MainPaneScrollHost
        {
            Children =
            {
                new JavaSetupPageView(
                    javaService.GetToolbarSnapshot(),
                    javaService.GetListSnapshot(),
                    addJava,
                    refreshJavaList,
                    selectDefaultJava,
                    toggleJava,
                    openJavaFolder,
                    showJavaInfo,
                    showHint,
                    javaService.GetMessages())
            }
        };
    }

    private Control BuildSettingControl(PixelSettingDescriptor setting)
    {
        if (setting.Control == PixelSettingControlKind.MemoryPreview)
            return new PixelMemoryPreviewSettingView(
                () => launchViewModel.GetMemoryPreviewSnapshot(memoryPreviewService));

        if (setting.Control == PixelSettingControlKind.ColorScheme)
            return new PixelColorSchemeSettingView(
                setting,
                storageProvider,
                colorSchemeSettingsService,
                _colorSchemeThemeBridge,
                showHint,
                showFormDialog);

        return new PixelSettingControlRenderer(
            storageProvider,
            showHint,
            settingChanged,
            openPersonalizationBackgroundFolder,
            settingValueService).Build(setting);
    }
}
