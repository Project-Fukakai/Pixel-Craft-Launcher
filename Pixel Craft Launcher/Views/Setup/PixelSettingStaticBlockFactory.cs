using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingStaticBlockFactory(
    PixelSettingValueService settingValues,
    PixelSettingControlMessages messages,
    Action openBackgroundFolder,
    Action<string, HintType> showHint)
{
    public Control CreateInfoBlock(string title, string? description)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Foreground = ThemeBrushes.Text,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold
                },
                CreateSubText(description ?? string.Empty)
            }
        };
    }

    public Control CreateActionBlock(PixelSettingDescriptor setting)
    {
        var button = new MyButton
        {
            Text = setting.Title,
            IsEnabled = settingValues.IsControlAvailable(setting),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) =>
        {
            if (settingValues.IsOpenBackgroundFolderAction(setting))
            {
                openBackgroundFolder();
                return;
            }

            showHint(
                settingValues.GetUnavailableReason(setting) ??
                setting.UnavailableReason ??
                messages.ReservedActionHint,
                HintType.Info);
        };
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                button,
                CreateSubText(setting.Description ?? setting.UnavailableReason ?? string.Empty)
            }
        };
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
