using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingControlRenderer
{
    private readonly Action<string?, object?> _settingChanged;
    private readonly PixelSettingValueService _settingValues;
    private readonly PixelSettingControlMessages _messages;
    private readonly PixelSettingToggleInput _toggleInput;
    private readonly PixelSettingBackgroundFolderInput _backgroundFolderInput;
    private readonly PixelSettingComboInput _comboInput;
    private readonly PixelSettingSliderInput _sliderInput;
    private readonly PixelSettingTextInput _textInput;
    private readonly PixelSettingFontInput _fontInput;
    private readonly PixelSettingStaticBlockFactory _staticBlocks;

    public PixelSettingControlRenderer(
        IStorageProvider storageProvider,
        Action<string, HintType> showHint,
        Action<string?, object?> settingChanged,
        Action openBackgroundFolder,
        PixelSettingValueService settingValues)
    {
        _settingChanged = settingChanged;
        _settingValues = settingValues;
        _messages = settingValues.GetControlMessages();
        _toggleInput = new PixelSettingToggleInput(settingValues);
        _backgroundFolderInput = new PixelSettingBackgroundFolderInput(storageProvider, openBackgroundFolder, _messages);
        _comboInput = new PixelSettingComboInput(settingValues, _messages);
        _sliderInput = new PixelSettingSliderInput(settingValues);
        _textInput = new PixelSettingTextInput(settingValues, _messages, _backgroundFolderInput, showHint);
        _fontInput = new PixelSettingFontInput(settingValues);
        _staticBlocks = new PixelSettingStaticBlockFactory(settingValues, _messages, openBackgroundFolder, showHint);
    }

    public Control Build(PixelSettingDescriptor setting)
    {
        if (setting.Control == PixelSettingControlKind.Info)
            return _staticBlocks.CreateInfoBlock(setting.Title, setting.Description);

        if (setting.Control == PixelSettingControlKind.Action)
            return _staticBlocks.CreateActionBlock(setting);

        if (setting.ConfigKey is null)
            return _staticBlocks.CreateInfoBlock(setting.Title, setting.Description ?? _messages.MissingConfigDescription);

        return setting.Control switch
        {
            PixelSettingControlKind.Toggle => CreateToggleSetting(setting),
            PixelSettingControlKind.Combo => CreateComboSetting(setting),
            PixelSettingControlKind.Slider => CreateSliderSetting(setting),
            PixelSettingControlKind.Text => CreateTextSetting(setting),
            PixelSettingControlKind.Font => CreateFontSetting(setting),
            _ => _staticBlocks.CreateInfoBlock(setting.Title, setting.Description)
        };
    }

    private Control CreateToggleSetting(PixelSettingDescriptor setting)
    {
        return _toggleInput.Build(setting, SetSettingValue);
    }

    private Control CreateComboSetting(PixelSettingDescriptor setting)
    {
        return CreateLabeledSetting(setting, _comboInput.Build(setting, SetSettingValue));
    }

    private Control CreateSliderSetting(PixelSettingDescriptor setting)
    {
        return CreateLabeledSetting(setting, _sliderInput.Build(setting, SetSettingValue));
    }

    private Control CreateTextSetting(PixelSettingDescriptor setting)
    {
        return CreateLabeledSetting(setting, _textInput.Build(setting, SetSettingValue));
    }

    private Control CreateFontSetting(PixelSettingDescriptor setting)
    {
        return CreateLabeledSetting(setting, _fontInput.Build(setting, SetSettingValue));
    }

    private Control CreateLabeledSetting(PixelSettingDescriptor setting, Control control)
    {
        return new SettingRow
        {
            Title = setting.Title,
            Description = string.IsNullOrWhiteSpace(setting.Description) ? string.Empty : GetSettingDescription(setting),
            SettingControl = control
        };
    }

    private bool SetSettingValue(PixelSettingDescriptor setting, object? value)
    {
        if (!_settingValues.SetValue(setting, value))
            return false;

        _settingChanged(setting.ConfigKey, value);
        return true;
    }

    private string GetSettingDescription(PixelSettingDescriptor setting)
    {
        var unavailableReason = _settingValues.GetUnavailableReason(setting);
        if (!string.IsNullOrWhiteSpace(unavailableReason))
            return string.IsNullOrWhiteSpace(setting.Description)
                ? unavailableReason
                : $"{setting.Description}（{unavailableReason}）";

        return setting.Description ?? string.Empty;
    }

}
