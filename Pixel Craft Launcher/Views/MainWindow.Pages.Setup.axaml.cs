using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Shell;
using Pixel_Craft_Launcher.Views.Pages;
using Pixel_Craft_Launcher.Views.Setup;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private JavaSetupPlatformBridge? _javaSetupBridge;
    private SetupSettingsPlatformBridge? _setupSettingsBridge;
    private SetupNavigationPlatformBridge? _setupNavigationBridge;

    private Control BuildSetupRightPage()
    {
        var javaBridge = GetJavaSetupBridge();
        var navigationBridge = GetSetupNavigationBridge();
        return new SetupRightPageFactory(
            _selectedSetupSection,
            _gameLinkViewModel,
            _gameLinkToolsController,
            _javaService,
            _setupNavigationService,
            _settingDisplayService,
            _settingValueService,
            _colorSchemeSettingsService,
            _memoryPreviewService,
            _launchViewModel,
            StorageProvider,
            javaBridge.AddJavaAsync,
            javaBridge.RefreshJavaListAsync,
            javaBridge.SelectDefaultJavaAsync,
            javaBridge.ToggleJavaEntryAsync,
            javaBridge.OpenJavaFolder,
            javaBridge.ShowJavaInfo,
            ShowHint,
            GetSetupSettingsBridge().OnSettingChanged,
            navigationBridge.OpenPersonalizationBackgroundFolder,
            navigationBridge.OpenExternalUrl,
            navigationBridge.NavigateSetupShortcut,
            title => ShowFormDialog(title))
            .Build();
    }

    private JavaSetupPlatformBridge GetJavaSetupBridge()
    {
        return _javaSetupBridge ??= new JavaSetupPlatformBridge(
            _javaService,
            StorageProvider,
            ShowHint,
            (title, message) => ShowMessage(title, message),
            RefreshSetupRightPage,
            OpenPath);
    }

    private SetupSettingsPlatformBridge GetSetupSettingsBridge()
    {
        return _setupSettingsBridge ??= new SetupSettingsPlatformBridge(
            _settingsChangeService,
            _settingDisplayService,
            RefreshSetupRightPage,
            RefreshShellTheme,
            ShowHint,
            (title, message, isWarning) => ShowMessage(title, message, isWarning));
    }

    private SetupNavigationPlatformBridge GetSetupNavigationBridge()
    {
        return _setupNavigationBridge ??= new SetupNavigationPlatformBridge(
            _setupNavigationService,
            _pixelPersonalizationService,
            _personalization,
            ShowHint,
            OpenUrl,
            OpenPath,
            section => _shellViewModel.NavigateSetupSection(section));
    }

    private void OpenExternalUrl(string? url) => GetSetupNavigationBridge().OpenExternalUrl(url);

    private void RefreshSetupRightPage()
    {
        if (SelectedMainPage != MainPageKind.Setup)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
        }, DispatcherPriority.Background);
    }

    private void InitializeSettingDisplayState()
    {
        GetSetupSettingsBridge().InitializeDisplayState();
    }

    private void LoadInitialSetupSection()
    {
        _selectedSetupSection = _setupNavigationService.GetSelectedSection();
    }
}
