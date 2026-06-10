using System;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingToggleInput(PixelSettingValueService settingValues)
{
    public Control Build(
        PixelSettingDescriptor setting,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var value = settingValues.LoadValue(setting.ConfigKey!) is bool b && b;
        var unavailableReason = settingValues.GetUnavailableReason(setting);
        var box = new MyCheckBox
        {
            Text = setting.Title,
            Checked = value,
            IsEnabled = unavailableReason is null && settingValues.IsInteractionEnabled(setting)
        };
        var description = GetSettingDescription(setting);
        if (!string.IsNullOrWhiteSpace(description))
            ToolTip.SetTip(box, description);
        box.Change += (_, user) =>
        {
            if (user)
                setSettingValue(setting, box.Checked == true);
        };

        if (string.IsNullOrWhiteSpace(description))
            return box;

        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                box,
                CreateSubText(description)
            }
        };
    }

    private string GetSettingDescription(PixelSettingDescriptor setting)
    {
        var unavailableReason = settingValues.GetUnavailableReason(setting);
        if (!string.IsNullOrWhiteSpace(unavailableReason))
            return string.IsNullOrWhiteSpace(setting.Description)
                ? unavailableReason
                : $"{setting.Description}（{unavailableReason}）";

        return setting.Description ?? string.Empty;
    }

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            FontSize = 12,
            LineHeight = 18
        };
    }
}
