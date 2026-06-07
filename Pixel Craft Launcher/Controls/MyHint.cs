using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyHint : Border
{
    public enum Themes
    {
        Blue,
        Red,
        Yellow
    }

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyHint, string?>(nameof(Text));

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<MyHint, bool>(nameof(CanClose));

    public new static readonly StyledProperty<Themes> ThemeProperty =
        AvaloniaProperty.Register<MyHint, Themes>(nameof(Theme), Themes.Red);

    public static readonly StyledProperty<bool> IsWarnProperty =
        AvaloniaProperty.Register<MyHint, bool>(nameof(IsWarn), true);

    private readonly TextBlock _textBlock = new()
    {
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(12, 9)
    };

    private readonly MyIconButton _closeButton = new()
    {
        Width = 24,
        Height = 24,
        Margin = new Thickness(0, 0, 8, 0),
        Icon = "mdi-close",
        IconSize = 10
    };

    public MyHint()
    {
        BorderThickness = new Thickness(3, 0, 0, 0);
        BorderBrush = ThemeBrushes.Error;
        Background = ThemeBrushes.ErrorContainer;
        CornerRadius = new CornerRadius(2);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Children.Add(_textBlock);
        Grid.SetColumn(_closeButton, 1);
        grid.Children.Add(_closeButton);
        Child = grid;

        _closeButton.Click += (_, _) =>
        {
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(this, -1, 140, ease: new ModAnimation.AniEaseInFluent()),
                ModAnimation.AaCode(() => IsVisible = false, 140)
            }, $"MyHint Close {GetHashCode()}");
        };
        UpdateVisuals();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public new Themes Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public bool IsWarn
    {
        get => GetValue(IsWarnProperty);
        set => SetValue(IsWarnProperty, value);
    }

    public bool HasBorder
    {
        get => BorderThickness.Top > 0;
        set => BorderThickness = value ? new Thickness(3, 1, 1, 1) : new Thickness(3, 0, 0, 0);
    }

    public string RelativeSetup { get; set; } = string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsWarnProperty)
            Theme = IsWarn ? Themes.Red : Themes.Blue;

        if (change.Property == TextProperty ||
            change.Property == CanCloseProperty ||
            change.Property == ThemeProperty ||
            change.Property == IsWarnProperty)
            UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _textBlock.Text = Text;
        _textBlock.Foreground = ThemeBrushes.Text;
        _closeButton.IsVisible = CanClose;
        (BorderBrush, Background) = Theme switch
        {
            Themes.Blue => (ThemeBrushes.Primary, ThemeBrushes.ListItemHover),
            Themes.Yellow => (ThemeBrushes.Warning, ThemeBrushes.WarningContainer),
            _ => (ThemeBrushes.Error, ThemeBrushes.ErrorContainer)
        };
    }
}
