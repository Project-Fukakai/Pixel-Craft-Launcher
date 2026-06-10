using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Services;

namespace Pixel_Craft_Launcher.Views;

internal sealed class ShellThemePresenter
{
    private readonly Window _window;
    private readonly PixelShellSettingsService _settings;
    private readonly PersonalizationPlatformBridge _personalization;
    private readonly ContentControl _backgroundMediaHost;
    private readonly Border _backgroundColorOverlay;
    private readonly AnimatedBackgroundGrid _titleBar;
    private readonly Border _leftPane;
    private readonly Border _rightPane;
    private readonly ExperimentalAcrylicBorder _sidebarAcrylic;
    private readonly Path _titlePathLogo;
    private readonly Image _titleImageLogo;
    private readonly TextBlock _titleBrand;
    private readonly StackPanel _titleNav;
    private readonly MyRadioButton[] _navigationButtons;
    private readonly MyIconButton[] _titleButtons;
    private readonly ShellThemePlatformBridge _themeBridge;

    public ShellThemePresenter(
        Window window,
        PixelShellSettingsService settings,
        PersonalizationPlatformBridge personalization,
        ContentControl backgroundMediaHost,
        Border backgroundColorOverlay,
        AnimatedBackgroundGrid titleBar,
        Border leftPane,
        Border rightPane,
        ExperimentalAcrylicBorder sidebarAcrylic,
        Path titlePathLogo,
        Image titleImageLogo,
        TextBlock titleBrand,
        StackPanel titleNav,
        MyRadioButton[] navigationButtons,
        MyIconButton[] titleButtons,
        ShellThemePlatformBridge themeBridge)
    {
        _window = window;
        _settings = settings;
        _personalization = personalization;
        _backgroundMediaHost = backgroundMediaHost;
        _backgroundColorOverlay = backgroundColorOverlay;
        _titleBar = titleBar;
        _leftPane = leftPane;
        _rightPane = rightPane;
        _sidebarAcrylic = sidebarAcrylic;
        _titlePathLogo = titlePathLogo;
        _titleImageLogo = titleImageLogo;
        _titleBrand = titleBrand;
        _titleNav = titleNav;
        _navigationButtons = navigationButtons;
        _titleButtons = titleButtons;
        _themeBridge = themeBridge;
    }

    public void RefreshShellTheme()
    {
        ApplyAvaloniaThemeVariant();
        ApplyShellColors();
        ApplyWindowPersonalization();
    }

    public void ApplyWindowPersonalization()
    {
        var personalization = _settings.GetPersonalizationSettings();
        _window.CanResize = personalization.CanResize;
        ApplyGlobalFont(personalization);
        ApplyTitleBarPersonalization(personalization);
        ApplyBackgroundOverlay();
    }

    public void ApplyBackgroundOverlay()
    {
        var hasBackground = _backgroundMediaHost.Content is not null;
        var colorful = _settings.GetAppearanceSettings().BackgroundColorful;
        _backgroundColorOverlay.Background = colorful ? ThemeBrushes.ShellBackground : ThemeBrushes.Background;
        _backgroundColorOverlay.Opacity = hasBackground ? (colorful ? 0.36 : 0.78) : 1;
    }

    private void ApplyAvaloniaThemeVariant()
    {
        var variant = _themeBridge.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        _window.RequestedThemeVariant = variant;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = variant;
    }

    private void ApplyShellColors()
    {
        var appearance = _settings.GetAppearanceSettings();
        var useAcrylic = appearance.AcrylicEnabled;
        var opacity = appearance.WindowOpacity;
        _window.TransparencyLevelHint = useAcrylic
            ? [WindowTransparencyLevel.AcrylicBlur]
            : [WindowTransparencyLevel.None];
        _window.Background = useAcrylic ? Brushes.Transparent : ThemeBrushes.ShellBackground;
        _titleBar.BackgroundBrush = ThemeBrushes.PrimaryContainer;
        _titleBar.Opacity = opacity;
        _rightPane.Background = ThemeBrushes.ShellBackground;
        _rightPane.Opacity = opacity;
        _leftPane.Background = useAcrylic ? Brushes.Transparent : ThemeBrushes.SidebarBackground;
        _leftPane.Opacity = opacity;
        _leftPane.BorderBrush = ThemeBrushes.SidebarBorder;
        _sidebarAcrylic.IsVisible = useAcrylic;
        if (useAcrylic)
            _sidebarAcrylic.Material = CreateSidebarAcrylicMaterial();
        _sidebarAcrylic.InvalidateVisual();
        RefreshTitleBarControls();
    }

    private void ApplyGlobalFont(PixelShellPersonalizationSettings settings)
    {
        var font = settings.FontName;
        _window.FontFamily = string.IsNullOrWhiteSpace(font)
            ? FontFamily.Default
            : new FontFamily($"{font}, Inter, Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC, Noto Sans, sans-serif");
    }

    private void ApplyTitleBarPersonalization(PixelShellPersonalizationSettings settings)
    {
        var leftAlign = settings.IsTitleLeftAligned;

        _titlePathLogo.IsVisible = settings.ShowTitlePathLogo;
        _titleImageLogo.IsVisible = false;
        _titleBrand.IsVisible = settings.ShowTitleText;
        _titleBrand.Text = settings.TitleText;

        if (settings.UseTitleImageLogo)
            ApplyTitleImageLogo();

        Grid.SetColumn(_titleNav, leftAlign ? 0 : 1);
        _titleNav.HorizontalAlignment = HorizontalAlignment.Left;
        _titleNav.Margin = leftAlign ? new Thickness(132, 0, 13, 0) : new Thickness(13, 0);
    }

    private void ApplyTitleImageLogo()
    {
        var image = _personalization.Library.Images.FirstOrDefault()?.Path;
        if (string.IsNullOrWhiteSpace(image))
        {
            _titlePathLogo.IsVisible = true;
            _titleBrand.IsVisible = false;
            return;
        }

        try
        {
            _titleImageLogo.Source = new Bitmap(image);
            _titleImageLogo.IsVisible = true;
            _titlePathLogo.IsVisible = false;
            _titleBrand.IsVisible = false;
        }
        catch
        {
            _titleImageLogo.IsVisible = false;
            _titlePathLogo.IsVisible = true;
            _titleBrand.IsVisible = false;
        }
    }

    private ExperimentalAcrylicMaterial CreateSidebarAcrylicMaterial()
    {
        var tint = ThemeBrushes.SidebarBackgroundColor;
        return new ExperimentalAcrylicMaterial
        {
            BackgroundSource = AcrylicBackgroundSource.Digger,
            TintColor = tint,
            TintOpacity = _themeBridge.IsDarkMode ? 0.84 : 0.78,
            MaterialOpacity = _themeBridge.IsDarkMode ? 0.72 : 0.65,
            FallbackColor = tint
        };
    }

    private void RefreshTitleBarControls()
    {
        foreach (var button in _navigationButtons)
            button.RefreshTheme();
        foreach (var button in _titleButtons)
            button.Foreground = ThemeBrushes.OnPrimaryContainer;
    }
}
