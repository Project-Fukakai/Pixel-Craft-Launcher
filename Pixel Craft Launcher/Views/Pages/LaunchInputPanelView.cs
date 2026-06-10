using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Launch;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchInputPanelView : Grid
{
    public LaunchInputPanelView(
        bool isLaunching,
        Control accountPanel,
        Control mainButtonPanel,
        PixelLaunchSecondaryActionSnapshot secondaryActions,
        PixelLaunchPageMessages messages,
        Action openInstanceSelection,
        Action openInstanceSettings)
    {
        IsVisible = !isLaunching;
        RowDefinitions.AddRange(
        [
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Star),
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Auto),
            new RowDefinition(new GridLength(20))
        ]);
        ColumnDefinitions.AddRange(
        [
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(new GridLength(20)),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(new GridLength(10))
        ]);
        RenderTransform = new ScaleTransform();

        Grid.SetRow(accountPanel, 1);
        Grid.SetColumnSpan(accountPanel, 5);
        Children.Add(accountPanel);

        Grid.SetRow(mainButtonPanel, 2);
        Grid.SetColumnSpan(mainButtonPanel, 5);
        Children.Add(mainButtonPanel);

        var actions = BuildSecondaryActions(secondaryActions, messages, openInstanceSelection, openInstanceSettings);
        Grid.SetRow(actions, 3);
        Grid.SetColumnSpan(actions, 5);
        Children.Add(actions);
    }

    private static Grid BuildSecondaryActions(
        PixelLaunchSecondaryActionSnapshot secondaryActions,
        PixelLaunchPageMessages messages,
        Action openInstanceSelection,
        Action openInstanceSettings)
    {
        var actions = new Grid
        {
            Margin = new Thickness(20, 10, 20, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var instanceButton = new MyButton
        {
            Text = messages.InstanceSelectButtonText,
            Variant = MyButtonVariant.Text,
            Height = 35,
            IsBlock = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = secondaryActions.IsSelectEnabled,
            IsVisible = secondaryActions.IsSelectVisible
        };
        instanceButton.Click += (_, _) => openInstanceSelection();
        actions.Children.Add(instanceButton);

        var settingsButton = new MyButton
        {
            Text = messages.InstanceSettingsButtonText,
            Variant = MyButtonVariant.Text,
            Height = 35,
            IsBlock = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = secondaryActions.IsEditEnabled,
            IsVisible = secondaryActions.IsEditVisible
        };
        settingsButton.Click += (_, _) => openInstanceSettings();
        Grid.SetColumn(settingsButton, 1);
        actions.Children.Add(settingsButton);
        return actions;
    }
}
