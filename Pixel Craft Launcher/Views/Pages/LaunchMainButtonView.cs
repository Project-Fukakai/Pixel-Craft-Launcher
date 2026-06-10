using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchMainButtonView : Grid
{
    public LaunchMainButtonView(
        PixelLaunchViewModel viewModel,
        Action openDownloadPage,
        Action startLaunch)
    {
        var snapshot = viewModel.GetMainButtonSnapshot();
        Margin = new Thickness(20, 0);
        Height = 68;

        var textTranslate = new TranslateTransform();
        var arrowTranslate = new TranslateTransform();
        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = textTranslate,
            Children =
            {
                new TextBlock
                {
                    Text = snapshot.PrimaryText,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = ThemeBrushes.OnPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = snapshot.SecondaryText,
                    FontSize = 12,
                    Foreground = ThemeBrushes.OnPrimary,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };
        ((TextBlock)textStack.Children[0]).Bind(TextBlock.TextProperty, new Binding("Launch.LaunchButtonText"));
        ((TextBlock)textStack.Children[1]).Bind(TextBlock.TextProperty, new Binding("Launch.SelectedInstanceName"));

        var arrow = new Avalonia.Controls.Shapes.Path
        {
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform,
            Fill = ThemeBrushes.OnPrimary,
            Data = MaterialIconGeometry.TryGet("mdi-arrow-right"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = arrowTranslate
        };

        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(2, 0)
        };
        content.Children.Add(textStack);
        Grid.SetColumn(arrow, 1);
        content.Children.Add(arrow);

        var launchButton = new MyButton
        {
            Variant = MyButtonVariant.Flat,
            Height = 66,
            Margin = new Thickness(0),
            Padding = new Thickness(26, 0, 18, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = snapshot.IsEnabled,
            Content = content
        };
        AttachLaunchButtonMotion(launchButton, textTranslate, arrowTranslate);
        launchButton.Click += (_, _) =>
        {
            if (snapshot.PrimaryAction == PixelLaunchPrimaryAction.DownloadGame)
            {
                openDownloadPage();
                return;
            }

            startLaunch();
        };
        Children.Add(launchButton);
    }

    private static void AttachLaunchButtonMotion(MyButton button, TranslateTransform text, TranslateTransform arrow)
    {
        const string animationName = "Launch Main Button Motion";

        void Animate(double textX, double arrowX, int time)
        {
            ModAnimation.AniStop(animationName);
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaTranslateX(text, textX - text.X, time, ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaTranslateX(arrow, arrowX - arrow.X, time, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
            }, animationName, true);
        }

        button.PointerEntered += (_, _) =>
        {
            if (button.IsEnabled)
                Animate(2, 5, 180);
        };
        button.PointerExited += (_, _) => Animate(0, 0, 160);
        button.PointerPressed += (_, _) =>
        {
            if (button.IsEnabled)
                Animate(4, 10, 90);
        };
        button.PointerReleased += (_, _) =>
        {
            if (button.IsEnabled && button.IsPointerOver)
                Animate(2, 5, 150);
            else
                Animate(0, 0, 150);
        };
    }
}
