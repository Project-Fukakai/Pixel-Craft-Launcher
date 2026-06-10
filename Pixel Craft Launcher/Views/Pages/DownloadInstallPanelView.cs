using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadInstallPanelView : UserControl
{
    private readonly PixelDownloadViewModel _viewModel;
    private readonly Func<Task> _refreshLoaderChoices;
    private readonly PixelDownloadInstallPanelMessages _messages;

    public DownloadInstallPanelView(
        PixelDownloadViewModel viewModel,
        PixelDownloadInstallPanelMessages messages,
        Func<Task> refreshLoaderChoices)
    {
        _viewModel = viewModel;
        _messages = messages;
        _refreshLoaderChoices = refreshLoaderChoices;
        var snapshot = viewModel.GetInstallPanelSnapshot();

        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };

        stack.Children.Add(BuildSummaryCard(snapshot.Summary));
        if (snapshot.LoaderChoiceState.Kind == PixelDownloadLoaderChoiceStateKind.Loading)
            stack.Children.Add(BuildLoadingLoaderCard());
        else if (snapshot.LoaderChoiceState.Kind == PixelDownloadLoaderChoiceStateKind.Error)
            stack.Children.Add(BuildLoaderErrorCard(snapshot.LoaderChoiceState.ErrorText));
        else if (snapshot.LoaderChoiceState.Kind == PixelDownloadLoaderChoiceStateKind.Empty)
            stack.Children.Add(BuildCard(_messages.LoaderTitle, CreateSubText(_messages.EmptyLoaderText)));

        foreach (var hint in BuildInstallHints(snapshot.Hints))
            stack.Children.Add(hint);
        foreach (var group in snapshot.LoaderChoiceGroups)
            stack.Children.Add(BuildLoaderSelectionCard(group));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private MyCard BuildLoadingLoaderCard()
    {
        return new MyCard
        {
            Title = _messages.LoadingLoaderTitle,
            CanSwap = false,
            IsSwapped = true,
            IsLoading = true,
            CardContent = CreateSubText(_messages.LoadingLoaderText)
        };
    }

    private MyCard BuildLoaderErrorCard(string? errorText)
    {
        var retry = new MyButton
        {
            Text = _messages.RetryButtonText,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        retry.Click += async (_, _) => await _refreshLoaderChoices();
        return BuildCard("Mod Loader", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateSubText(errorText ?? _messages.LoaderFailedText),
                retry
            }
        });
    }

    private MyCard BuildSummaryCard(PixelInstallSelectionSummarySnapshot summary)
    {
        return BuildCard(_messages.InstallTitle, new StackPanel
        {
            Spacing = 6,
            Children =
            {
                CreateBodyText(summary.VersionTitle),
                CreateSubText(summary.LoaderInfo)
            }
        });
    }

    private static IEnumerable<MyHint> BuildInstallHints(IReadOnlyList<PixelInstallHintSnapshot> hints)
    {
        foreach (var hint in hints)
        {
            yield return new MyHint
            {
                Text = hint.Text,
                Theme = hint.Kind == PixelInstallHintKind.Error ? MyHint.Themes.Red : MyHint.Themes.Yellow,
                CanClose = false
            };
        }
    }

    private MyCard BuildLoaderSelectionCard(PixelLoaderChoiceSnapshot group)
    {
        var content = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(group.Description))
            content.Children.Add(CreateSubText(group.Description));
        var list = new StackPanel { Spacing = 4 };
        if (!group.CanSelect)
        {
            content.Children.Add(CreateDisabledText(group.Status));
        }
        else
        {
            foreach (var choice in group.Items)
            {
                var item = new MyListItem
                {
                    Title = choice.Title,
                    Info = choice.Info,
                    Icon = choice.Icon,
                    Type = MyListItem.CheckType.RadioBox,
                    Checked = choice.IsChecked,
                    IsEnabled = choice.IsEnabled
                };
                item.Check += (_, _) =>
                {
                    if (choice.IsEnabled)
                        _viewModel.SelectLoaderChoiceItem(choice.Id);
                };
                list.Children.Add(item);
            }
        }

        if (group.CanSelect && !group.HasItems)
            list.Children.Add(CreateDisabledText(group.EmptyText));
        if (list.Children.Count > 0)
            content.Children.Add(list);

        content.Children.Add(CreateSubText(group.Status));
        if (group.IsActive && !string.IsNullOrWhiteSpace(group.ClearActionId))
        {
            var clear = new MyButton
            {
                Text = _messages.ClearSelectionButtonText,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            clear.Click += (_, _) => _viewModel.ClearInstallChoice(group.ClearActionId);
            content.Children.Add(clear);
        }

        var card = BuildCard(group.CardTitle, content);
        card.CanSwap = group.CanSelect;
        card.IsSwapped = true;
        card.Opacity = group.CanSelect ? 1 : 0.72;
        return card;
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            CanSwap = false,
            CardContent = content
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            FontSize = 14,
            LineHeight = 22
        };
    }

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            FontSize = 13,
            LineHeight = 20
        };
    }

    private static TextBlock CreateDisabledText(string text)
    {
        var block = CreateSubText(text);
        block.Foreground = ThemeBrushes.TextDisabled;
        return block;
    }
}
