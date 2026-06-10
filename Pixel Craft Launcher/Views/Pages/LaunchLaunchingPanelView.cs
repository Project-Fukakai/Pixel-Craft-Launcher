using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchLaunchingPanelView : Grid
{
    private readonly PixelLaunchViewModel _viewModel;

    public LaunchLaunchingPanelView(
        PixelLaunchViewModel viewModel,
        PixelLaunchPageMessages messages,
        PixelLoadingTriggerAdapter loadingState,
        Action cancelLaunch)
    {
        _viewModel = viewModel;
        var snapshot = viewModel.GetLaunchingPanelSnapshot();
        IsVisible = snapshot.IsVisible;
        Opacity = 0;
        IsHitTestVisible = false;
        RenderTransform = new ScaleTransform(0.8, 0.8);
        RowDefinitions.AddRange(
        [
            new RowDefinition(GridLength.Star),
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Star),
            new RowDefinition(GridLength.Auto)
        ]);
        AttachedToVisualTree += (_, _) => AnimateIn(this);

        var loading = new MyLoading
        {
            Text = "",
            ShowProgress = false,
            Height = 50,
            Margin = new Thickness(0, 12, 0, 12),
            State = loadingState
        };

        var progress = BuildProgressBar(snapshot.Progress);
        var info = BuildInfoGrid(messages, snapshot);
        var launchTitle = new TextBlock
        {
            Text = snapshot.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 20,
            Foreground = ThemeBrushes.Primary,
            Margin = new Thickness(15, 12, 15, 0),
            RenderTransform = new SkewTransform(-3, 0)
        };
        launchTitle.Bind(TextBlock.TextProperty, new Binding("Launch.LaunchTitleText"));

        var stack = new StackPanel
        {
            Margin = new Thickness(0, -7, 0, 0),
            Spacing = 0,
            Children =
            {
                loading,
                launchTitle,
                new TextBlock
                {
                    Text = snapshot.InstanceName,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13.5,
                    Foreground = ThemeBrushes.Primary,
                    Margin = new Thickness(40, 5, 40, 0),
                    RenderTransform = new SkewTransform(-3, 0)
                },
                progress,
                info
            }
        };
        Grid.SetRow(stack, 1);
        Children.Add(stack);

        var cancelButton = new MyButton
        {
            Text = messages.CancelLaunchButtonText,
            Height = 35,
            Margin = new Thickness(20, 0, 20, 20),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        cancelButton.Click += (_, _) => cancelLaunch();
        Grid.SetRow(cancelButton, 4);
        Children.Add(cancelButton);
    }

    private Grid BuildProgressBar(double initialProgress)
    {
        var progressValue = Math.Clamp(initialProgress, 0, 1);
        var progress = new Grid
        {
            Height = 4,
            Margin = new Thickness(30, 12, 30, 27),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(Math.Max(progressValue, 0.001), GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(1 - progressValue, 0.001), GridUnitType.Star))
            },
            Children =
            {
                new Rectangle { Fill = ThemeBrushes.Primary },
                new Rectangle { Fill = ThemeBrushes.Border, Opacity = 0.6 }
            }
        };
        Grid.SetColumn(progress.Children[1], 1);
        AttachProgressMonitor(progress, progress.ColumnDefinitions[0], progress.ColumnDefinitions[1]);
        return progress;
    }

    private Grid BuildInfoGrid(PixelLaunchPageMessages messages, PixelLaunchLaunchingPanelSnapshot snapshot)
    {
        var info = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(15)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        AddInfoRow(info, 0, messages.LaunchStageLabel, snapshot.Stage, "Launch.Stage");
        AddInfoRow(info, 1, messages.LaunchProfileMethodLabel, snapshot.ProfileMethod, "Launch.Summary.ProfileMethod");
        AddInfoRow(info, 2, messages.LaunchProgressLabel, snapshot.ProgressText, "Launch.LaunchProgressText");
        return info;
    }

    private static void AnimateIn(Control root)
    {
        if (root.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(0.8, 0.8);
            root.RenderTransform = scale;
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(root, 1 - root.Opacity, 150, 100),
            ModAnimation.AaScaleTransform(scale, 1, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
            ModAnimation.AaCode(() =>
            {
                root.Opacity = 1;
                root.IsHitTestVisible = true;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, after: true)
        }, "Launch State Page", true);
    }

    private void AttachProgressMonitor(Control owner, ColumnDefinition finished, ColumnDefinition unfinished)
    {
        var shownProgress = Math.Clamp(_viewModel.GetLaunchingPanelSnapshot().Progress, 0, 1);
        var targetProgress = shownProgress;
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, (_, _) =>
        {
            if (targetProgress >= shownProgress)
                shownProgress += (targetProgress - shownProgress) * 0.2d + 0.005d;
            if (targetProgress <= shownProgress || Math.Abs(targetProgress - shownProgress) < 0.002d)
                shownProgress = targetProgress;
            Apply(shownProgress);
            if (Math.Abs(targetProgress - shownProgress) < 0.0001d)
                timer?.Stop();
        });

        void Apply(double value)
        {
            var progress = Math.Clamp(value, 0, 1);
            finished.Width = new GridLength(Math.Max(progress, 0.001), GridUnitType.Star);
            unfinished.Width = new GridLength(Math.Max(1 - progress, 0.001), GridUnitType.Star);
        }

        void OnProgressChanged(object? _, double progress) =>
            Dispatcher.UIThread.Post(() =>
            {
                targetProgress = Math.Clamp(progress, 0, 1);
                if (timer is { IsEnabled: false })
                    timer.Start();
            }, DispatcherPriority.Render);

        Apply(shownProgress);
        _viewModel.LoadingState.ProgressChanged += OnProgressChanged;
        owner.DetachedFromVisualTree += (_, _) =>
        {
            timer?.Stop();
            _viewModel.LoadingState.ProgressChanged -= OnProgressChanged;
        };
    }

    private static void AddInfoRow(Grid grid, int row, string label, string value, string? bindingPath)
    {
        var left = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            Opacity = 0.5,
            Foreground = ThemeBrushes.Text
        };
        var right = new TextBlock
        {
            Text = value,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = ThemeBrushes.Text
        };
        if (!string.IsNullOrWhiteSpace(bindingPath))
            right.Bind(TextBlock.TextProperty, new Binding(bindingPath));
        Grid.SetRow(left, row);
        Grid.SetColumn(left, 1);
        Grid.SetRow(right, row);
        Grid.SetColumn(right, 3);
        grid.Children.Add(left);
        grid.Children.Add(right);
    }
}
