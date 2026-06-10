using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchAccountPanelView : StackPanel
{
    public LaunchAccountPanelView(
        PixelLaunchViewModel viewModel,
        PixelLaunchPageMessages messages,
        Action openProfileManager)
    {
        Spacing = 8;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(20, 0);

        var avatar = new MySkinHead
        {
            Size = 84,
            SkinName = viewModel.OfflineSkinName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        avatar.Bind(MySkinHead.SkinNameProperty, new Binding("Launch.OfflineSkinName"));

        var name = new TextBlock
        {
            Text = viewModel.Summary.ProfileName,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 250
        };
        name.Bind(TextBlock.TextProperty, new Binding("Launch.Summary.ProfileName"));

        var method = new TextBlock
        {
            Text = viewModel.Summary.ProfileMethod,
            FontSize = 13,
            Foreground = ThemeBrushes.TextSecondary,
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        method.Bind(TextBlock.TextProperty, new Binding("Launch.Summary.ProfileMethod"));

        var manageButton = new MyButton
        {
            Text = messages.ProfileManageButtonText,
            PrependIcon = "mdi-account-cog-outline",
            Variant = MyButtonVariant.Text,
            Size = MyButtonSize.Small,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        manageButton.Click += (_, _) => openProfileManager();

        Children.Add(avatar);
        Children.Add(name);
        Children.Add(method);
        Children.Add(manageButton);
    }
}
