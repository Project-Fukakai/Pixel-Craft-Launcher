using System;
using Avalonia.Controls;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingFontInput(PixelSettingValueService settingValues)
{
    public Control Build(
        PixelSettingDescriptor setting,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var selector = new FontSelector
        {
            SelectedFontTag = settingValues.GetControlTextValue(setting),
            Tooltip = GetSettingDescription(setting),
            IsEnabled = settingValues.IsControlEnabled(setting)
        };
        selector.SelectionChanged += (_, _) => setSettingValue(setting, selector.SelectedFontTag);
        return selector;
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
}
