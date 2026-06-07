using Avalonia;
using Avalonia.Controls;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Controls.MyMsg;

public class MyMsgSelect : StackPanel
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyMsgSelect, string?>(nameof(Title));

    public TextBlock TitleBlock { get; } = new() { FontSize = 23, Foreground = ThemeBrushes.PrimaryHover };
    public MyComboBox SelectBox { get; } = new();

    public MyMsgSelect()
    {
        Spacing = 10;
        Children.Add(TitleBlock);
        Children.Add(SelectBox);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
            TitleBlock.Text = Title;
    }
}
