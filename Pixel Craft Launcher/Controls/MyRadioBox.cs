using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyRadioBox : RadioButton, IMyRadio
{
    public delegate void PreviewChangeEventHandler(object sender, MyRouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, MyRouteEventArgs e);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyRadioBox, string?>(nameof(Text));

    private bool _suppressCheckedEvent;

    private readonly Ellipse _ring = new()
    {
        Width = 18,
        Height = 18,
        StrokeThickness = 1.4,
        Fill = ThemeBrushes.HalfWhite
    };

    private readonly Ellipse _dot = new()
    {
        Width = 8,
        Height = 8,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        RenderTransform = new ScaleTransform()
    };

    private readonly TextBlock _label = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };

    public MyRadioBox()
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

    public bool Checked
    {
        get => IsChecked == true;
        set => SetChecked(value, false);
    }

    public event IMyRadio.CheckEventHandler? Check;

    public event IMyRadio.ChangedEventHandler? Changed;

    public event PreviewCheckEventHandler? PreviewCheck;

    public event PreviewChangeEventHandler? PreviewChange;

    public void SetChecked(bool value, bool user)
    {
        if (value == Checked)
            return;

        if (value)
        {
            var checkPreview = new MyRouteEventArgs(user);
            PreviewCheck?.Invoke(this, checkPreview);
            if (checkPreview.Handled)
                return;
        }

        var changePreview = new MyRouteEventArgs(user);
        PreviewChange?.Invoke(this, changePreview);
        if (changePreview.Handled)
            return;

        _suppressCheckedEvent = true;
        IsChecked = value;
        if (value)
            UncheckSiblingRadios();
        _suppressCheckedEvent = false;
        UpdateVisuals();
        RaiseRadioEvents(user);
    }

    protected override void OnClick()
    {
        SetChecked(true, true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsCheckedProperty || change.Property == IsEnabledProperty)
        {
            UpdateVisuals();
            if (change.Property == IsCheckedProperty && !_suppressCheckedEvent)
            {
                if (Checked)
                    UncheckSiblingRadios();
                RaiseRadioEvents(false);
            }
        }
        else if (change.Property == ForegroundProperty || change.Property == FontSizeProperty)
        {
            UpdateVisuals();
        }
    }

    private void RaiseRadioEvents(bool user)
    {
        var e = new MyRouteEventArgs(user);
        if (Checked)
            Check?.Invoke(this, e);
        Changed?.Invoke(this, e);
    }

    private void UncheckSiblingRadios()
    {
        if (!string.IsNullOrWhiteSpace(GroupName) || Parent is not Panel panel)
            return;

        foreach (var child in panel.Children.OfType<MyRadioBox>())
        {
            if (ReferenceEquals(child, this) || !child.Checked)
                continue;
            child.SetChecked(false, false);
        }
    }

    private Control BuildContent()
    {
        var marker = new Grid { Width = 20, Height = 20 };
        marker.Children.Add(_ring);
        marker.Children.Add(_dot);

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
        _ring.Stroke = accent;
        _dot.Fill = accent;
        _dot.IsVisible = IsChecked == true;
        _label.Text = Text;
        _label.Foreground = Foreground;
        _label.FontSize = FontSize;
        if (_dot.RenderTransform is ScaleTransform scale)
        {
            if (_dot.IsVisible)
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(scale, 1, 180, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
                    $"MyRadioBox Check {GetHashCode()}");
            else
                scale.ScaleX = scale.ScaleY = 0;
        }
    }
}
