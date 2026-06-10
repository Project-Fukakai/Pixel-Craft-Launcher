using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelSettingBackgroundFolderInput(
    IStorageProvider storageProvider,
    Action openBackgroundFolder,
    PixelSettingControlMessages messages)
{
    public Control Build(
        PixelSettingDescriptor setting,
        MyTextBox textBox,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var selectButton = new MyIconButton
        {
            Icon = "mdi-folder-search-outline",
            IconSize = 14,
            Width = 34,
            Height = 34,
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            IsEnabled = textBox.IsEnabled
        };
        ToolTip.SetTip(selectButton, messages.SelectBackgroundFolderTooltip);
        selectButton.Click += async (_, _) => await SelectBackgroundFolderAsync(setting, textBox, setSettingValue);

        var openButton = new MyIconButton
        {
            Icon = "mdi-folder-open-outline",
            IconSize = 14,
            Width = 34,
            Height = 34,
            Margin = new Avalonia.Thickness(6, 0, 0, 0)
        };
        ToolTip.SetTip(openButton, messages.OpenBackgroundFolderTooltip);
        openButton.Click += (_, _) => openBackgroundFolder();

        var inputWithButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        inputWithButtons.Children.Add(textBox);
        Grid.SetColumn(selectButton, 1);
        inputWithButtons.Children.Add(selectButton);
        Grid.SetColumn(openButton, 2);
        inputWithButtons.Children.Add(openButton);
        return inputWithButtons;
    }

    private async Task SelectBackgroundFolderAsync(
        PixelSettingDescriptor setting,
        MyTextBox textBox,
        Func<PixelSettingDescriptor, object?, bool> setSettingValue)
    {
        var selected = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = messages.SelectBackgroundFolderDialogTitle,
            AllowMultiple = false
        });
        var path = selected.Count > 0 ? selected[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path))
            return;

        textBox.Text = path;
        setSettingValue(setting, path);
    }
}
