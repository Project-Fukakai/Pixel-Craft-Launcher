using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingComboInput(
    PixelSettingValueService settingValues,
    PixelSettingControlMessages messages)
{
    public Control Build(
        PixelSettingDescriptor setting,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var options = setting.Options ?? [];
        var state = settingValues.GetComboState(setting);
        var comboBox = new MyComboBox
        {
            IsEditable = setting.IsEditableCombo,
            HintText = setting.IsEditableCombo ? messages.EditableComboHint : null,
            IsEnabled = settingValues.IsControlEnabled(setting),
            MinWidth = 160
        };

        foreach (var option in options)
        {
            var item = new MyComboBoxItem
            {
                Content = option.Text,
                Tag = option.Value
            };
            if (!string.IsNullOrWhiteSpace(option.ToolTip))
                ToolTip.SetTip(item, option.ToolTip);
            comboBox.Items.Add(item);
        }

        if (setting.IsEditableCombo)
        {
            comboBox.Text = state.Text;
            comboBox.TextChanged += (_, _) => setSettingValue(setting, comboBox.Text ?? string.Empty);
        }
        else if (options.Count > 0)
        {
            comboBox.SelectedIndex = state.SelectedIndex;
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is MyComboBoxItem { Tag: { } value })
                    setSettingValue(setting, value);
            };
        }

        return comboBox;
    }
}
