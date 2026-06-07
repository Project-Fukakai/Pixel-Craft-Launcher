using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Pixel_Craft_Launcher.Controls.MyMsg;

public class MyMsgText : StackPanel
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyMsgText, string?>(nameof(Title));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyMsgText, string?>(nameof(Text));

    private readonly TextBlock _title = new() { FontSize = 23, Foreground = Controls.ThemeBrushes.PrimaryHover };
    private readonly TextBlock _text = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Controls.ThemeBrushes.Text };

    public MyMsgText()
    {
        Spacing = 10;
        Children.Add(_title);
        Children.Add(_text);
        UpdateVisuals();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == TextProperty)
            UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _title.Text = Title;
        _text.Text = Text;
        _title.IsVisible = !string.IsNullOrWhiteSpace(Title);
    }
}
