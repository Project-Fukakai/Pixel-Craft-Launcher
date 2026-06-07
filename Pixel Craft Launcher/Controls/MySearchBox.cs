using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Pixel_Craft_Launcher.Controls;

public class MySearchBox : MyTextBox
{
    public delegate void SearchEventHandler(object sender, EventArgs e);

    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public static readonly StyledProperty<bool> SearchButtonVisibilityProperty =
        AvaloniaProperty.Register<MySearchBox, bool>(nameof(SearchButtonVisibility), true);

    private readonly MyIconButton _searchButton = new()
    {
        Width = 26,
        Height = 26,
        IconSize = 13,
        Icon = "mdi-magnify"
    };

    private readonly MyIconButton _clearButton = new()
    {
        Width = 26,
        Height = 26,
        IconSize = 10,
        Icon = "mdi-close"
    };

    public MySearchBox()
    {
        HintText = "搜索";
        PlaceholderText = HintText;
        InnerRightContent = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children = { _clearButton, _searchButton }
        };
        _searchButton.Click += (_, _) => Search?.Invoke(this, EventArgs.Empty);
        _clearButton.Click += (_, _) => Text = string.Empty;
        base.TextChanged += (_, _) =>
        {
            UpdateButtons();
            TextChanged?.Invoke(this, EventArgs.Empty);
        };
        KeyDown += OnKeyDown;
        MinHeight = 36;
        UpdateButtons();
    }

    public bool SearchButtonVisibility
    {
        get => GetValue(SearchButtonVisibilityProperty);
        set => SetValue(SearchButtonVisibilityProperty, value);
    }

    public event SearchEventHandler? Search;

    public new event TextChangedEventHandler? TextChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SearchButtonVisibilityProperty)
            UpdateButtons();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Search?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void UpdateButtons()
    {
        _searchButton.IsVisible = SearchButtonVisibility;
        _clearButton.IsVisible = !string.IsNullOrEmpty(Text);
    }
}
