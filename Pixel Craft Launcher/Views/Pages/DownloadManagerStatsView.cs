using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadManagerStatsView : UserControl
{
    public DownloadManagerStatsView(
        PixelDownloadViewModel viewModel,
        PixelDownloadManagerStatsMessages messages)
    {
        var stats = viewModel.GetManagerStatsSnapshot();

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        AddStat(root, 1, 2, 3, messages.ProgressTitle, stats.ProgressText);
        AddStat(root, 5, 6, 7, messages.SpeedTitle, stats.SpeedText);
        AddStat(root, 9, 10, 11, messages.RemainingFilesTitle, stats.RemainingFilesText);
        AddStat(root, 13, 14, 15, messages.RemainingThreadsTitle, stats.RemainingThreadsText);
        Content = root;
    }

    private static void AddStat(Grid root, int titleRow, int splitRow, int valueRow, string title, string value)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontSize = 14,
            Foreground = ThemeBrushes.PrimaryHover
        };
        Grid.SetRow(titleBlock, titleRow);
        root.Children.Add(titleBlock);

        var split = new Border
        {
            Height = 2,
            Margin = new Thickness(25, 7),
            Background = ThemeBrushes.Border,
            Opacity = 0.8
        };
        Grid.SetRow(split, splitRow);
        root.Children.Add(split);

        var valueBlock = new TextBlock
        {
            Text = value,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontSize = 13,
            Foreground = ThemeBrushes.Text
        };
        Grid.SetRow(valueBlock, valueRow);
        root.Children.Add(valueBlock);
    }
}
