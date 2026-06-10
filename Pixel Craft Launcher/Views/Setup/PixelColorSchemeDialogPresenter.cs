using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Pixel;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

internal sealed class PixelColorSchemeDialogPresenter
{
    private readonly PixelColorSchemeSettingsService _settings;
    private readonly PixelColorSchemePageMessages _messages;
    private readonly Action<string, HintType> _showHint;
    private readonly Func<string, MyMsgForm> _showFormDialog;

    public PixelColorSchemeDialogPresenter(
        PixelColorSchemeSettingsService settings,
        PixelColorSchemePageMessages messages,
        Action<string, HintType> showHint,
        Func<string, MyMsgForm> showFormDialog)
    {
        _settings = settings;
        _messages = messages;
        _showHint = showHint;
        _showFormDialog = showFormDialog;
    }

    public void Show(uint seed, Action<uint> apply)
    {
        var initial = _settings.ToColor(seed);
        var preview = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(initial),
            BorderBrush = ThemeBrushes.BorderStrong,
            BorderThickness = new Thickness(1)
        };
        var hexBox = new MyTextBox
        {
            Text = _settings.FormatSeed(seed),
            HintText = "#AARRGGBB",
            Width = 142,
            UseFloatingPlaceholder = false
        };
        hexBox.ValidateRules.Add(text => _settings.TryParseSeed(text, out _) ? null : _messages.HexValidationMessage);

        var current = initial;
        var suppress = false;
        MySlider? redSlider = null;
        MySlider? greenSlider = null;
        MySlider? blueSlider = null;

        void SetCurrent(Color color, bool syncControls)
        {
            current = color;
            preview.Background = new SolidColorBrush(color);
            var formatted = _settings.FormatSeed(_settings.FromColor(color));
            if (!string.Equals(hexBox.Text, formatted, StringComparison.OrdinalIgnoreCase))
                hexBox.Text = formatted;
            if (!syncControls)
                return;
            suppress = true;
            if (redSlider is not null) redSlider.Value = color.R;
            if (greenSlider is not null) greenSlider.Value = color.G;
            if (blueSlider is not null) blueSlider.Value = color.B;
            suppress = false;
        }

        hexBox.TextChanged += (_, _) =>
        {
            if (suppress || !_settings.TryParseSeed(hexBox.Text, out var parsed))
                return;
            SetCurrent(_settings.ToColor(parsed), syncControls: true);
        };

        var redRow = CreateColorChannelSlider("R", current.R, out redSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(_settings.WithRedChannel(current, value), syncControls: false);
            });
        var greenRow = CreateColorChannelSlider("G", current.G, out greenSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(_settings.WithGreenChannel(current, value), syncControls: false);
            });
        var blueRow = CreateColorChannelSlider("B", current.B, out blueSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(_settings.WithBlueChannel(current, value), syncControls: false);
            });

        var dialog = _showFormDialog(_messages.ManualDialogTitle);
        dialog.ContentPanel.Children.Add(new StackPanel
        {
            Spacing = 12,
            MinWidth = 360,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        preview,
                        hexBox
                    }
                },
                redRow,
                greenRow,
                blueRow
            }
        });
        dialog.Button1.IsEnabled = hexBox.IsValidated;
        hexBox.ValidateChanged += (_, _) => dialog.Button1.IsEnabled = hexBox.IsValidated;
        dialog.Button1Click += (_, _) =>
        {
            if (!_settings.TryParseSeed(hexBox.Text, out var parsed))
            {
                _showHint(_messages.InvalidColorHint, HintType.Critical);
                return;
            }
            apply(parsed);
        };
    }

    private Control CreateColorChannelSlider(string label, byte initial, out MySlider slider, Action<int> changed)
    {
        var valueText = CreateSubText(_settings.FormatChannel(initial));
        valueText.Width = 34;
        valueText.TextAlignment = TextAlignment.Right;
        var channelSlider = new MySlider
        {
            Minimum = 0,
            Maximum = 255,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Value = initial
        };
        slider = channelSlider;
        channelSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != MySlider.ValueProperty)
                return;
            var value = _settings.NormalizeChannel(channelSlider.Value);
            valueText.Text = _settings.FormatChannel(value);
            changed(value);
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(18)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(42))
            },
            ColumnSpacing = 8
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = ThemeBrushes.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(channelSlider, 1);
        row.Children.Add(channelSlider);
        Grid.SetColumn(valueText, 2);
        row.Children.Add(valueText);
        return row;
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
