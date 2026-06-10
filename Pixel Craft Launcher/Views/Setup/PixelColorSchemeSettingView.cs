using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PCL.Core.App;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelColorSchemeSettingView : SettingRow
{
    private readonly IStorageProvider _storageProvider;
    private readonly PixelColorSchemeSettingsService _settings;
    private readonly PixelColorSchemeThemeBridge _themeBridge;
    private readonly Action<string, HintType> _showHint;
    private readonly PixelColorSchemePageMessages _messages;
    private readonly PixelColorSchemeDialogPresenter _dialogPresenter;

    public PixelColorSchemeSettingView(
        PixelSettingDescriptor setting,
        IStorageProvider storageProvider,
        PixelColorSchemeSettingsService settings,
        PixelColorSchemeThemeBridge themeBridge,
        Action<string, HintType> showHint,
        Func<string, MyMsgForm> showFormDialog)
    {
        _storageProvider = storageProvider;
        _settings = settings;
        _themeBridge = themeBridge;
        _showHint = showHint;
        _messages = settings.GetPageMessages();
        _dialogPresenter = new PixelColorSchemeDialogPresenter(settings, _messages, showHint, showFormDialog);

        Title = setting.Title;
        Description = setting.Description ?? string.Empty;
        SettingControl = BuildContent();
    }

    private Control BuildContent()
    {
        var manualPicker = CreateColorSchemeIconButton(_messages.ManualPickerTooltip, "mdi-palette-outline");
        manualPicker.Click += (_, _) =>
        {
            var pickerSeed = _settings.GetEffectiveSeed();
            _dialogPresenter.Show(pickerSeed, nextSeed => ApplySeed(nextSeed, ColorSchemeMode.Manual));
        };

        var pickImage = CreateColorSchemeIconButton(_messages.ImagePickerTooltip, "mdi-image-search-outline");
        pickImage.Click += async (_, _) =>
        {
            var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _messages.ImagePickerTitle,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(_messages.ImageFileTypeName)
                    {
                        Patterns = _messages.ImageFilePatterns.ToArray()
                    }
                ]
            });
            var path = result.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            var imageResult = _settings.ApplyImageSeed(path);
            if (!imageResult.Success)
            {
                _showHint(_messages.ImageExtractionFailedHint, HintType.Critical);
                return;
            }

            RefreshThemeIfNeeded(imageResult.RefreshTheme);
            _showHint(_messages.ImageExtractionSuccessHint, HintType.Finish);
        };

        var autoBackground = new MyCheckBox
        {
            Text = _messages.AutoBackgroundText,
            Checked = _settings.IsAutoBackgroundEnabled()
        };
        autoBackground.Change += (_, user) =>
        {
            if (!user)
                return;
            RefreshThemeIfNeeded(_settings.SetAutoBackground(autoBackground.Checked == true).RefreshTheme);
        };

        var presets = new WrapPanel();
        foreach (var preset in _settings.GetPresets())
            presets.Children.Add(CreatePresetColorButton(preset.Name, preset.Seed));
        presets.Children.Add(manualPicker);
        presets.Children.Add(pickImage);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                presets,
                autoBackground
            }
        };
    }

    private void ApplySeed(uint nextSeed, ColorSchemeMode mode)
    {
        RefreshThemeIfNeeded(_settings.ApplySeed(nextSeed, mode).RefreshTheme);
    }

    private void RefreshThemeIfNeeded(bool refreshTheme)
    {
        if (refreshTheme)
            _themeBridge.RefreshColorScheme();
    }

    private static MyButton CreateColorSchemeIconButton(string name, string icon)
    {
        var button = new MyButton
        {
            Icon = icon,
            Width = 34,
            Height = 34,
            MinWidth = 34,
            MinHeight = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Variant = MyButtonVariant.Outlined,
            Size = MyButtonSize.Small,
            Margin = new Thickness(0, 0, 8, 8)
        };
        ToolTip.SetTip(button, name);
        return button;
    }

    private Control CreatePresetColorButton(string name, uint seed)
    {
        var primaryPart = new Border
        {
            [Grid.ColumnProperty] = 0,
            [Grid.RowProperty] = 0,
            [Grid.RowSpanProperty] = 2
        };
        var secondaryPart = new Border
        {
            [Grid.ColumnProperty] = 1,
            [Grid.RowProperty] = 0
        };
        var tertiaryPart = new Border
        {
            [Grid.ColumnProperty] = 1,
            [Grid.RowProperty] = 1
        };

        void ApplyPreviewColors()
        {
            var previewColors = _settings.GetPreviewColors(seed, _themeBridge.IsDarkMode);
            primaryPart.Background = new SolidColorBrush(previewColors.Primary);
            secondaryPart.Background = new SolidColorBrush(previewColors.Secondary);
            tertiaryPart.Background = new SolidColorBrush(previewColors.Tertiary);
        }

        ApplyPreviewColors();

        var button = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = ThemeBrushes.Surface,
            BorderBrush = ThemeBrushes.BorderStrong,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
            Child = new Grid
            {
                Width = 34,
                Height = 34,
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("*,*"),
                Children =
                {
                    primaryPart,
                    secondaryPart,
                    tertiaryPart
                }
            }
        };
        IDisposable? colorModeSubscription = null;
        button.AttachedToVisualTree += (_, _) =>
        {
            colorModeSubscription?.Dispose();
            colorModeSubscription = _themeBridge.SubscribeColorModeChanged(ApplyPreviewColors);
            ApplyPreviewColors();
        };
        button.DetachedFromVisualTree += (_, _) =>
        {
            colorModeSubscription?.Dispose();
            colorModeSubscription = null;
        };
        ToolTip.SetTip(button, name);
        button.PointerPressed += (_, e) =>
        {
            ApplySeed(seed, ColorSchemeMode.Preset);
            e.Handled = true;
        };
        return button;
    }

}
