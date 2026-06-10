using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadInstallSidebarView : UserControl
{
    private readonly PixelDownloadViewModel _viewModel;
    private readonly Action _requestRefresh;
    private readonly bool _isDarkMode;
    private readonly PixelDownloadInstallSidebarMessages _messages;

    public DownloadInstallSidebarView(
        PixelDownloadViewModel viewModel,
        PixelDownloadInstallSidebarMessages messages,
        bool isDarkMode,
        Action requestRefresh)
    {
        _viewModel = viewModel;
        _requestRefresh = requestRefresh;
        _isDarkMode = isDarkMode;
        _messages = messages;
        var snapshot = viewModel.GetInstallSidebarSnapshot();

        var content = new StackPanel
        {
            Margin = new Thickness(14, 14, 14, 10),
            Spacing = 8
        };

        content.Children.Add(CreateSidebarTitle(_messages.InstanceNameSectionTitle));
        content.Children.Add(BuildInstanceSidebar(snapshot));
        content.Children.Add(CreateSidebarTitle(_messages.ChecklistSectionTitle));
        content.Children.Add(BuildChecklist(snapshot));

        var scroller = new MyScrollViewer
        {
            Content = content
        };
        var downloadPanel = BuildDownloadSidebar(snapshot.InstallState);
        Grid.SetRow(downloadPanel, 1);
        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                scroller,
                downloadPanel
            }
        };
    }

    private Control BuildInstanceSidebar(PixelDownloadInstallSidebarSnapshot snapshot)
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 6) };
        stack.Children.Add(CreateInstallInput(_messages.NameInputTitle, snapshot.InstanceName, text => _viewModel.InstanceName = text));
        stack.Children.Add(CreateInstallInput(_messages.DirectoryInputTitle, snapshot.TargetFolder, text => _viewModel.TargetFolder = text));
        return stack;
    }

    private Control BuildChecklist(PixelDownloadInstallSidebarSnapshot snapshot)
    {
        var list = new StackPanel { Spacing = 2 };
        foreach (var checklistItem in snapshot.ChecklistItems)
        {
            var item = new MyListItem
            {
                Title = checklistItem.Title,
                Info = checklistItem.Info,
                Icon = checklistItem.Icon
            };
            if (!checklistItem.CanClear || string.IsNullOrWhiteSpace(checklistItem.ClearActionId))
            {
                list.Children.Add(item);
                continue;
            }

            var delete = new MyIconButton
            {
                Icon = "mdi-close",
                IconSize = 12,
                Padding = new Thickness(4),
                Width = 24,
                Height = 24
            };
            ToolTip.SetTip(delete, _messages.ClearChoiceTooltip);
            delete.Click += (_, _) =>
            {
                _viewModel.ClearInstallChoice(checklistItem.ClearActionId);
                _requestRefresh();
            };
            item.AddButton(delete);
            list.Children.Add(item);
        }

        if (snapshot.ShowVanillaInstanceText)
            list.Children.Add(CreateSubText(_messages.VanillaInstanceText));
        return list;
    }

    private Control BuildDownloadSidebar(PixelDownloadInstallStateSnapshot state)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(14, 8, 14, 16),
            Spacing = 8
        };
        if (state.IsBusy)
            stack.Children.Add(CreateSubText(state.StatusText));
        var start = CreateDownloadButton(state);
        start.PointerEntered += (_, _) => AnimateDownloadButton(start, true);
        start.PointerExited += (_, _) => AnimateDownloadButton(start, false);
        start.PointerPressed += (_, e) =>
        {
            if (!CanInstallNow())
                return;
            SetDownloadButtonPressed(start, true);
            e.Handled = true;
        };
        start.PointerReleased += (_, e) =>
        {
            if (!CanInstallNow())
                return;
            SetDownloadButtonPressed(start, false);
            _ = _viewModel.InstallSelectedAsync();
            e.Handled = true;
        };
        stack.Children.Add(start);
        return stack;
    }

    private bool CanInstallNow() => _viewModel.GetInstallSidebarSnapshot().InstallState.CanInstall;

    private static Border CreateDownloadButton(PixelDownloadInstallStateSnapshot state)
    {
        var canInstall = state.CanInstall;
        var textBrush = canInstall ? ThemeBrushes.OnPrimary : ThemeBrushes.TextDisabled;
        return new Border
        {
            Height = 54,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(18, 0),
            Background = new SolidColorBrush(canInstall ? ThemeBrushes.PrimaryColor : ThemeBrushes.TextDisabledColor),
            BorderBrush = new SolidColorBrush(canInstall ? ThemeBrushes.PrimaryColor : ThemeBrushes.TextDisabledColor),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            Opacity = canInstall ? 1 : 0.55,
            Cursor = canInstall ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            BoxShadow = canInstall
                ? new BoxShadows(new BoxShadow { Blur = 12, Spread = 0, OffsetY = 3, Color = Color.FromArgb(42, ThemeBrushes.PrimaryColor.R, ThemeBrushes.PrimaryColor.G, ThemeBrushes.PrimaryColor.B) })
                : default,
            RenderTransform = new TranslateTransform(),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Avalonia.Controls.Shapes.Path
                    {
                        Width = 21,
                        Height = 21,
                        Stretch = Stretch.Uniform,
                        Fill = textBrush,
                        Data = MaterialIconGeometry.TryGet(state.PrimaryActionIcon)
                    },
                    new TextBlock
                    {
                        Text = state.PrimaryActionText,
                        Foreground = textBrush,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 15,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }

    private static Control CreateInstallInput(string title, string value, Action<string> onChanged)
    {
        var input = new TextBox
        {
            PlaceholderText = title,
            Text = value,
            Height = 40,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0),
            UseFloatingPlaceholder = false
        };
        input.TextChanged += (_, _) => onChanged(input.Text ?? string.Empty);

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 12,
                    Foreground = ThemeBrushes.TextSecondary
                },
                input
            }
        };
    }

    private static TextBlock CreateSidebarTitle(string title)
    {
        return new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = ThemeBrushes.Text,
            Margin = new Thickness(0, 2, 0, 0)
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

    private void AnimateDownloadButton(Border button, bool hover)
    {
        if (button.Opacity < 0.7)
            return;

        var baseColor = ThemeBrushes.PrimaryColor;
        var hoverColor = AdjustFilledHoverColor(baseColor);
        var shadowColor = hover
            ? Color.FromArgb(72, hoverColor.R, hoverColor.G, hoverColor.B)
            : Color.FromArgb(42, baseColor.R, baseColor.G, baseColor.B);
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaColor(button, Border.BackgroundProperty, new SolidColorBrush(hover ? hoverColor : baseColor), 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaColor(button, Border.BorderBrushProperty, new SolidColorBrush(hover ? hoverColor : baseColor), 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() => button.BoxShadow = new BoxShadows(new BoxShadow { Blur = hover ? 18 : 12, Spread = 0, OffsetY = hover ? 5 : 3, Color = shadowColor }))
        }, $"Install Download Button {button.GetHashCode()}");
    }

    private static void SetDownloadButtonPressed(Border button, bool pressed)
    {
        button.Opacity = pressed ? 0.92 : 1;
        if (button.RenderTransform is TranslateTransform translate)
        {
            ModAnimation.AniStart(ModAnimation.AaTranslateY(translate, (pressed ? 1 : 0) - translate.Y, pressed ? 80 : 140,
                ease: new ModAnimation.AniEaseOutFluent()), $"Install Download Button Press {button.GetHashCode()}");
        }
    }

    private Color AdjustFilledHoverColor(Color color)
    {
        var amount = _isDarkMode ? 0.24 : -0.18;
        static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
        byte Adjust(byte channel) => amount >= 0
            ? Clamp(channel + (255 - channel) * amount)
            : Clamp(channel * (1 + amount));
        return Color.FromArgb(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
    }
}
