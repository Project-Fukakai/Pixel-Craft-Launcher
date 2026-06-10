using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadTaskDetailsView : UserControl
{
    private readonly PixelDownloadViewModel _viewModel;
    private readonly Action<string> _showInfo;
    private readonly PixelDownloadTaskDetailsMessages _messages;

    public DownloadTaskDetailsView(
        PixelDownloadViewModel viewModel,
        PixelDownloadTaskDetailsMessages messages,
        Action<string> showInfo)
    {
        _viewModel = viewModel;
        _showInfo = showInfo;
        _messages = messages;
        var snapshot = viewModel.GetTaskDetailsPageSnapshot();

        var stack = new StackPanel
        {
            Margin = new Thickness(25, 25, 25, 10),
            Spacing = 0
        };

        if (snapshot.Kind == PixelDownloadTaskDetailsPageKind.Tasks)
            stack.Children.Add(BuildTaskGroupCard(snapshot));
        else
            stack.Children.Add(BuildStatusCard(snapshot));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private MyCard BuildTaskGroupCard(PixelDownloadTaskDetailsPageSnapshot snapshot)
    {
        var content = new Grid
        {
            Margin = new Thickness(14, 0, 15, 10)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < snapshot.Tasks.Count; i++)
            AddTaskRow(content, i, snapshot.Tasks[i]);

        var card = new MyCard
        {
            Title = snapshot.Title,
            Margin = new Thickness(0, 0, 0, 15),
            CardContent = content
        };

        if (!snapshot.HasCancellableTasks)
            return card;

        var cancel = new MyIconButton
        {
            Icon = "mdi-close",
            IconSize = 16,
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 10, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(2),
            Foreground = ThemeBrushes.Text
        };
        ToolTip.SetTip(cancel, _messages.CancelTooltip);
        cancel.Click += async (_, _) =>
        {
            var count = 0;
            foreach (var taskId in snapshot.CancellableTaskIds)
            {
                if (await _viewModel.CancelTaskAsync(taskId).ConfigureAwait(true))
                    count++;
            }
            if (count > 0)
                _showInfo(_messages.CancelGroupRequestedMessage);
        };
        if (card.Content is Panel root)
            root.Children.Add(cancel);
        return card;
    }

    private static MyCard BuildStatusCard(PixelDownloadTaskDetailsPageSnapshot snapshot)
    {
        var content = CreateTaskGrid();
        AddTextRow(content, 0, CreateWaitingGlyph(), snapshot.BodyText);

        return new MyCard
        {
            Title = snapshot.Title,
            Margin = new Thickness(0, 0, 0, 15),
            CardContent = content
        };
    }

    private static Grid CreateTaskGrid()
    {
        var grid = new Grid
        {
            Margin = new Thickness(14, 0, 15, 10)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        return grid;
    }

    private static void AddTaskRow(Grid grid, int row, PixelDownloadTaskSnapshot task)
    {
        Control marker = task.Status switch
        {
            PixelDownloadTaskStatus.Finished => CreateFinishedGlyph(),
            PixelDownloadTaskStatus.Failed or PixelDownloadTaskStatus.Cancelled => CreateFailedGlyph(),
            PixelDownloadTaskStatus.Running => CreateProgressGlyph(task.ProgressPercentText),
            _ => CreateWaitingGlyph()
        };
        AddTextRow(grid, row, marker, task.Text);
    }

    private static void AddTextRow(Grid grid, int row, Control marker, string text)
    {
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(26)));
        Grid.SetRow(marker, row);
        Grid.SetColumn(marker, 0);
        grid.Children.Add(marker);

        var label = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text
        };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
    }

    private static TextBlock CreateProgressGlyph(string progressText)
    {
        return new TextBlock
        {
            Text = progressText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = ThemeBrushes.PrimaryHover
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateWaitingGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 18,
            Height = 6,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 7, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z")
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateFinishedGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 15,
            Height = 16,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z")
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateFailedGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z")
        };
    }
}
