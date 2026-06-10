using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingSliderInput(PixelSettingValueService settingValues)
{
    public Control Build(
        PixelSettingDescriptor setting,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var state = settingValues.GetSliderState(setting);
        var valueText = CreateValueText(state.ValueText);

        var slider = new MySlider
        {
            Minimum = setting.Minimum,
            Maximum = state.Maximum,
            TickFrequency = setting.TickFrequency,
            IsSnapToTickEnabled = setting.TickFrequency > 0,
            Value = state.Value,
            IsEnabled = settingValues.IsControlEnabled(setting)
        };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != MySlider.ValueProperty)
                return;

            var next = settingValues.NormalizeSliderInput(setting, slider.Value);
            valueText.Text = settingValues.FormatSettingValue(setting, next);
            setSettingValue(setting, next);
        };

        var control = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(54))
            }
        };
        control.Children.Add(slider);
        Grid.SetColumn(valueText, 1);
        control.Children.Add(valueText);
        return control;
    }

    private static TextBlock CreateValueText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            FontSize = 12,
            LineHeight = 18,
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }
}
