using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingTextInput(
    PixelSettingValueService settingValues,
    PixelSettingControlMessages messages,
    PixelSettingBackgroundFolderInput backgroundFolderInput,
    Action<string, HintType> showHint)
{
    public Control Build(
        PixelSettingDescriptor setting,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var textBox = new MyTextBox
        {
            HintText = setting.Title,
            Text = settingValues.GetControlTextValue(setting),
            IsEnabled = settingValues.IsControlEnabled(setting),
            UseFloatingPlaceholder = false
        };
        textBox.TextChanged += (_, _) =>
        {
            if (!setSettingValue(setting, textBox.Text ?? string.Empty))
                showHint(messages.RejectedTextInputHint, HintType.Critical);
        };

        if (settingValues.IsBackgroundFolderSetting(setting))
            return backgroundFolderInput.Build(setting, textBox, setSettingValue);

        return settingValues.IsLaunchAdvanceJvmSetting(setting)
            ? CreateJvmInput(setting, textBox)
            : textBox;
    }

    private Control CreateJvmInput(PixelSettingDescriptor setting, MyTextBox textBox)
    {
        var resetButton = new MyIconButton
        {
            Icon = "mdi-restore",
            IconSize = 13,
            Width = 34,
            Height = 34,
            Margin = new Avalonia.Thickness(8, 0, 0, 0)
        };
        ToolTip.SetTip(resetButton, messages.ResetJvmArgumentsTooltip);
        resetButton.Click += (_, _) =>
        {
            var result = settingValues.Reset(PixelSettingValueService.LaunchAdvanceJvmConfigKey);
            if (result.Success)
            {
                textBox.Text = settingValues.FormatControlValue(result.Value);
                showHint(messages.ResetJvmArgumentsSuccessHint, HintType.Finish);
            }
        };

        var inputWithReset = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        inputWithReset.Children.Add(textBox);
        Grid.SetColumn(resetButton, 1);
        inputWithReset.Children.Add(resetButton);
        return inputWithReset;
    }
}
