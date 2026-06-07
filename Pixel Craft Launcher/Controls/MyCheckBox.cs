using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyCheckBox : CheckBox
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, MyRouteEventArgs e);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyCheckBox, string?>(nameof(Text));

    private bool _suppressCheckedEvent;

    private readonly Border _box = new()
    {
        Width = 18,
        Height = 18,
        CornerRadius = new CornerRadius(3),
        BorderThickness = new Thickness(1.1),
        Background = ThemeBrushes.HalfWhite
    };

    private readonly Path _check = new()
    {
        Width = 12,
        Height = 12,
        Stretch = Stretch.Uniform,
        Data = Geometry.Parse("M0,6L1.5,4.5 4.5,7.5 10.5,1.5 12,3 4.5,10.5 0,6z"),
        RenderTransform = new ScaleTransform()
    };

    private readonly Border _indeterminate = new()
    {
        Width = 10,
        Height = 10,
        CornerRadius = new CornerRadius(2)
    };

    private readonly TextBlock _label = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };

    public MyCheckBox()
    {
        MinHeight = 20;
        FontSize = 13;
        Background = Brushes.Transparent;
        Foreground = ThemeBrushes.Text;
        Content = BuildContent();
        UpdateVisuals();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool? Checked
    {
        get => IsChecked;
        set => SetChecked(value, false);
    }

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    public void SetChecked(bool? value, bool user)
    {
        if (value == IsChecked)
            return;

        var preview = new MyRouteEventArgs(user);
        PreviewChange?.Invoke(this, preview);
        if (preview.Handled)
            return;

        _suppressCheckedEvent = true;
        IsChecked = value;
        _suppressCheckedEvent = false;
        UpdateVisuals();
        Change?.Invoke(this, user);
    }

    protected override void OnClick()
    {
        SetChecked(GetNextCheckedValue(), true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsCheckedProperty || change.Property == IsEnabledProperty)
        {
            UpdateVisuals();
            if (change.Property == IsCheckedProperty && !_suppressCheckedEvent)
                Change?.Invoke(this, false);
        }
        else if (change.Property == ForegroundProperty || change.Property == FontSizeProperty)
        {
            UpdateVisuals();
        }
    }

    private bool? GetNextCheckedValue()
    {
        if (!IsThreeState)
            return IsChecked != true;

        return IsChecked switch
        {
            false => true,
            true => null,
            _ => false
        };
    }

    private Control BuildContent()
    {
        var marker = new Grid { Width = 20, Height = 20 };
        marker.Children.Add(_box);
        marker.Children.Add(_check);
        marker.Children.Add(_indeterminate);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { marker, _label }
        };
    }

    private void UpdateVisuals()
    {
        var accent = IsEnabled ? IsChecked == true ? ThemeBrushes.PrimaryHover : ThemeBrushes.Text : ThemeBrushes.TextDisabled;
        _box.BorderBrush = accent;
        _check.Fill = accent;
        _indeterminate.Background = accent;
        _label.Text = Text;
        _label.Foreground = Foreground;
        _label.FontSize = FontSize;
        _check.IsVisible = IsChecked == true;
        _indeterminate.IsVisible = IsChecked is null;
        if (_check.RenderTransform is ScaleTransform scale)
        {
            if (_check.IsVisible)
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(scale, 1, 180, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
                    $"MyCheckBox Check {GetHashCode()}");
            else
                scale.ScaleX = scale.ScaleY = 0;
        }
    }
}
