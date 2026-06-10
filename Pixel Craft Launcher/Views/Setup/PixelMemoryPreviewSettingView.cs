using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Pixel.Slices.Launch;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class PixelMemoryPreviewSettingView : StackPanel
{
    private readonly Func<PixelMemoryPreviewSnapshot> _getSnapshot;
    private readonly ColumnDefinition _usedColumn = new(new GridLength(1, GridUnitType.Star));
    private readonly ColumnDefinition _gameColumn = new(new GridLength(1, GridUnitType.Star));
    private readonly ColumnDefinition _freeColumn = new(new GridLength(1, GridUnitType.Star));
    private readonly TextBlock _totalText = CreateSubText("");
    private readonly TextBlock _usedText = CreateSubText("");
    private readonly TextBlock _gameText = CreateSubText("");
    private readonly TextBlock _freeText = CreateSubText("");
    private readonly TextBlock _warningText = CreateSubText("");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public PixelMemoryPreviewSettingView(Func<PixelMemoryPreviewSnapshot> getSnapshot)
    {
        _getSnapshot = getSnapshot;

        Spacing = 7;
        _warningText.Foreground = ThemeBrushes.MemoryUsed;

        Children.Add(_totalText);
        Children.Add(CreateBar());
        Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 2, 0, 0),
            Children =
            {
                CreateMemoryLegend(ThemeBrushes.MemoryUsed, _usedText),
                CreateMemoryLegend(ThemeBrushes.MemoryGame, _gameText),
                CreateMemoryLegend(ThemeBrushes.MemoryFree, _freeText)
            }
        });
        Children.Add(_warningText);

        _timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        Refresh();
    }

    private Grid CreateBar()
    {
        var bar = new Grid
        {
            Height = 10,
            ClipToBounds = true,
            ColumnDefinitions =
            {
                _usedColumn,
                _gameColumn,
                _freeColumn
            },
            Children =
            {
                new Border
                {
                    Background = ThemeBrushes.MemoryUsed,
                    CornerRadius = new CornerRadius(5, 0, 0, 5),
                    Opacity = 0.72
                },
                new Border
                {
                    Background = ThemeBrushes.MemoryGame,
                    Opacity = 0.52
                },
                new Border
                {
                    Background = ThemeBrushes.MemoryFree,
                    CornerRadius = new CornerRadius(0, 5, 5, 0),
                    Opacity = 0.72
                }
            }
        };
        Grid.SetColumn(bar.Children[1], 1);
        Grid.SetColumn(bar.Children[2], 2);
        return bar;
    }

    private void Refresh()
    {
        var snapshot = _getSnapshot();
        var text = PixelMemoryPreviewService.CreateTextSnapshot(snapshot);
        _totalText.Text = text.TotalText;
        _usedText.Text = text.UsedText;
        _gameText.Text = text.GameText;
        _freeText.Text = text.FreeText;
        _warningText.Text = text.WarningText;
        _warningText.IsVisible = text.HasWarning;

        _usedColumn.Width = ToStar(snapshot.UsedGb);
        _gameColumn.Width = ToStar(snapshot.GameActualGb);
        _freeColumn.Width = ToStar(snapshot.FreeAfterLaunchGb);
    }

    private static StackPanel CreateMemoryLegend(IBrush brush, TextBlock label)
    {
        label.Margin = new Thickness(0, 0, 14, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 8, 0),
            Children =
            {
                new Border
                {
                    Width = 9,
                    Height = 9,
                    CornerRadius = new CornerRadius(5),
                    Background = brush,
                    VerticalAlignment = VerticalAlignment.Center
                },
                label
            }
        };
    }

    private static GridLength ToStar(double value) => new(Math.Max(value, 0.01d), GridUnitType.Star);

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
