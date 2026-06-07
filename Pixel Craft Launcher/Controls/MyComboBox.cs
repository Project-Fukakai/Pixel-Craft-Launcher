using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Pixel_Craft_Launcher.Controls;

public class MyComboBox : ComboBox
{
    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<MyComboBox, string?>(nameof(HintText));

    public MyComboBox()
    {
        SelectionChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    public string? HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public event TextChangedEventHandler? TextChanged;

    protected override Type StyleKeyOverride => typeof(ComboBox);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HintTextProperty)
            PlaceholderText = HintText;
        else if (change.Property == TextProperty)
            TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (IsDropDownOpen)
            return;

        foreach (var ancestor in this.GetVisualAncestors())
        {
            switch (ancestor)
            {
                case MainPaneScrollHost mainHost when mainHost.ScrollByWheel(e.Delta.Y):
                case LeftPaneScrollHost leftHost when leftHost.ScrollByWheel(e.Delta.Y):
                case MyScrollViewer scrollViewer when scrollViewer.ScrollByWheel(e.Delta.Y):
                    e.Handled = true;
                    return;
            }
        }

        e.Handled = true;
    }
}
