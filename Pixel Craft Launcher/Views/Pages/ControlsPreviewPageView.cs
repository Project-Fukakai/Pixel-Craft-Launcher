using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ControlsPreviewPageView : UserControl
{
    private readonly Action<string, string> _showMessage;
    private readonly PixelControlsPreviewSnapshot _snapshot;
    private readonly MyLoadingStateSimulator _loadingState = new() { LoadingState = MyLoadingState.Loading };

    public ControlsPreviewPageView(PixelControlsPreviewSnapshot snapshot, Action<string, string> showMessage)
    {
        _snapshot = snapshot;
        _showMessage = showMessage;

        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(snapshot.HeroTitle, snapshot.HeroDescription));
        stack.Children.Add(BuildButtonsCard());
        stack.Children.Add(BuildInputsCard());
        stack.Children.Add(BuildSelectionCard());
        stack.Children.Add(BuildLoadingAndListCard());
        stack.Children.Add(BuildCard(snapshot.DialogAndBehaviorCardTitle, new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new MyButton
                {
                    Text = snapshot.OpenMarkdownButtonText,
                    Variant = MyButtonVariant.Flat,
                    HorizontalAlignment = HorizontalAlignment.Left
                }.WithClick((_, _) => _showMessage(snapshot.MarkdownMessageTitle, snapshot.MarkdownMessageBody)),
                CreateLazyLoadExample()
            }
        }));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private Control BuildButtonsCard()
    {
        return BuildCard(_snapshot.ButtonsCardTitle, new WrapPanel
        {
            Children =
            {
                new MyButton { Text = "Tonal", Variant = MyButtonVariant.Tonal, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Text", Variant = MyButtonVariant.Text, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Outlined", Variant = MyButtonVariant.Outlined, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Elevated", Variant = MyButtonVariant.Elevated, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Flat", Variant = MyButtonVariant.Flat, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Prepend", PrependIcon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Append", AppendIcon = "mdi-arrow-right", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Stacked", Icon = "mdi-apps", Stacked = true, Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyIconButton { Icon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Small", Size = MyButtonSize.Small, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Large", Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Disabled", IsEnabled = false, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Readonly", IsReadOnly = true, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Block", IsBlock = true, Width = 220, Margin = new Thickness(0, 0, 10, 10) }
            }
        });
    }

    private Control BuildInputsCard()
    {
        var searchBox = new MySearchBox { HintText = _snapshot.SearchHint, Width = 240 };
        var searchResult = CreateBodyText(_snapshot.SearchWaitingText);
        searchBox.Search += (_, _) => searchResult.Text = _snapshot.SearchResultPrefix + (searchBox.Text ?? string.Empty);

        var textBox = new MyTextBox { HintText = _snapshot.SafeTextBoxHint, Width = 240 };
        ClipboardInterceptor.SetEnableSafeClipboard(textBox, true);

        var comboBox = new MyComboBox
        {
            HintText = _snapshot.ComboHint,
            Width = 240,
            ItemsSource = _snapshot.ComboItems,
            SelectedIndex = 0
        };

        return BuildCard(_snapshot.InputsCardTitle, new StackPanel
        {
            Spacing = 10,
            Children =
            {
                searchBox,
                searchResult,
                textBox,
                comboBox,
                new FontSelector { Width = 280, Tooltip = _snapshot.FontTooltip },
                new MySlider { Minimum = 0, Maximum = 100, Value = 35, Width = 280 }
            }
        });
    }

    private Control BuildSelectionCard()
    {
        var checkBox = new MyCheckBox { Text = "CheckBox Checked alias", Checked = true };
        var radioBoxA = new MyRadioBox { Text = "RadioBox A", Checked = true };
        var radioBoxB = new MyRadioBox { Text = "RadioBox B" };
        var radioButtonA = new MyRadioButton { Text = _snapshot.LaunchRadioText, Checked = true, Icon = "mdi-play" };
        var radioButtonB = new MyRadioButton { Text = _snapshot.DownloadRadioText, Icon = "mdi-download" };

        return BuildCard(_snapshot.SelectionCardTitle, new StackPanel
        {
            Spacing = 10,
            Children =
            {
                checkBox,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { radioBoxA, radioBoxB } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { radioButtonA, radioButtonB } },
                new MyHint { Text = _snapshot.HintText, Theme = MyHint.Themes.Yellow, CanClose = false }
            }
        });
    }

    private Control BuildLoadingAndListCard()
    {
        var loading = new MyLoading
        {
            Text = "Loading",
            State = _loadingState,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var listItem = new MyListItem
        {
            Title = "MyListItem",
            Info = _snapshot.ListItemInfo,
            Type = MyListItem.CheckType.CheckBox,
            Logo = Geometry.Parse("M3,3 L17,3 L17,17 L3,17 Z")
        };
        listItem.AddButton(new MyIconButton
        {
            Icon = "mdi-check",
            IconSize = 12,
            Width = 28,
            Height = 28
        });

        return BuildCard(_snapshot.LoadingAndListCardTitle, new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new MyButton { Text = "Loading" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Loading),
                        new MyButton { Text = "Stop" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Stop),
                        new MyButton { Text = "Error", Variant = MyButtonVariant.Outlined, IsDanger = true }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Error)
                    }
                },
                loading,
                BuildM3LoadingIndicatorPreview(),
                listItem
            }
        });
    }

    private static Control BuildM3LoadingIndicatorPreview()
    {
        static Control CreatePreview(string title, MyM3LoadingIndicator indicator)
        {
            return new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 0, 18, 8),
                Children =
                {
                    indicator,
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 12,
                        Foreground = ThemeBrushes.Text,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
        }

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("Material 3 Loading Indicator"),
                new WrapPanel
                {
                    Children =
                    {
                        CreatePreview("Primary", new MyM3LoadingIndicator
                        {
                            Size = 38,
                            Color = ThemeBrushes.Primary
                        }),
                        CreatePreview("Contained", new MyM3LoadingIndicator
                        {
                            Size = 56,
                            Contained = true,
                            Color = ThemeBrushes.OnPrimaryContainer,
                            ContainerColor = ThemeBrushes.PrimaryContainer
                        }),
                        CreatePreview("Error", new MyM3LoadingIndicator
                        {
                            Size = 46,
                            IsError = true,
                            Color = ThemeBrushes.Primary,
                            ErrorColor = ThemeBrushes.Error
                        })
                    }
                }
            }
        };
    }

    private Control CreateLazyLoadExample()
    {
        var marker = new TextBlock
        {
            Text = _snapshot.LazyLoadText,
            Foreground = ThemeBrushes.Text,
            Margin = new Thickness(0, 20, 0, 0)
        };
        LazyLoadBehavior.SetAction(marker, () => marker.Text = _snapshot.LazyLoadTriggeredText);
        return marker;
    }

    private static StackPanel CreatePageStack()
    {
        return new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 26),
            Spacing = 14
        };
    }

    private static MyCard BuildHeroCard(string title, string description)
    {
        return BuildCard(title, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = description,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrushes.Text
                }
            }
        });
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            Margin = new Thickness(0, 0, 0, 2),
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
            LineHeight = 22
        };
    }
}
