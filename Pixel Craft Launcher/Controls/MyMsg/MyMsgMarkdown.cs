using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls.MyMsg;

public class MyMsgMarkdown : Border
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyMsgMarkdown, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MyMsgMarkdown, string?>(nameof(Markdown));

    public static readonly StyledProperty<string?> Button1Property =
        AvaloniaProperty.Register<MyMsgMarkdown, string?>(nameof(Button1), "确定");

    public static readonly StyledProperty<string?> Button2Property =
        AvaloniaProperty.Register<MyMsgMarkdown, string?>(nameof(Button2));

    public static readonly StyledProperty<string?> Button3Property =
        AvaloniaProperty.Register<MyMsgMarkdown, string?>(nameof(Button3));

    public static readonly StyledProperty<bool> IsWarnProperty =
        AvaloniaProperty.Register<MyMsgMarkdown, bool>(nameof(IsWarn));

    private readonly TextBlock _title = new() { FontSize = 23, TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _markdown = new() { TextWrapping = TextWrapping.Wrap, Foreground = ThemeBrushes.Text };
    private readonly MyButton _button1 = new();
    private readonly MyButton _button2 = new();
    private readonly MyButton _button3 = new();

    public MyMsgMarkdown()
    {
        CornerRadius = new CornerRadius(5);
        Background = ThemeBrushes.TransparentBackground;
        BorderBrush = ThemeBrushes.Border;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(20);
        BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, OffsetY = 4, Color = ThemeBrushes.ShadowColor });
        RenderTransform = new TranslateTransform(0, 18);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(_button3);
        buttons.Children.Add(_button2);
        buttons.Children.Add(_button1);

        Child = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                _title,
                new Rectangle { Height = 1, Fill = ThemeBrushes.Border },
                _markdown,
                buttons
            }
        };

        _button1.Click += (_, e) => OnButtonClicked(e, Button1Click);
        _button2.Click += (_, e) => OnButtonClicked(e, Button2Click);
        _button3.Click += (_, e) => OnButtonClicked(e, Button3Click);
        AttachedToVisualTree += (_, _) => PlayOpenAnimation();
        UpdateVisuals();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public string? Button1
    {
        get => GetValue(Button1Property);
        set => SetValue(Button1Property, value);
    }

    public string? Button2
    {
        get => GetValue(Button2Property);
        set => SetValue(Button2Property, value);
    }

    public string? Button3
    {
        get => GetValue(Button3Property);
        set => SetValue(Button3Property, value);
    }

    public bool IsWarn
    {
        get => GetValue(IsWarnProperty);
        set => SetValue(IsWarnProperty, value);
    }

    public event EventHandler? Button1Click;
    public event EventHandler? Button2Click;
    public event EventHandler? Button3Click;

    public void Close()
    {
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(this, -Opacity, 90, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaTranslateY((TranslateTransform)RenderTransform!, 18, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RemoveFromParent, 150)
        }, $"MyMsgMarkdown Close {GetHashCode()}");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty ||
            change.Property == MarkdownProperty ||
            change.Property == Button1Property ||
            change.Property == Button2Property ||
            change.Property == Button3Property ||
            change.Property == IsWarnProperty)
            UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _title.Text = Title;
        _title.Foreground = IsWarn ? ThemeBrushes.Error : ThemeBrushes.PrimaryHover;
        _markdown.Text = Markdown;
        _button1.Text = string.IsNullOrWhiteSpace(Button1) ? "确定" : Button1;
        _button1.Variant = MyButtonVariant.Flat;
        _button1.IsDanger = IsWarn;
        _button2.Text = Button2;
        _button3.Text = Button3;
        _button2.IsVisible = !string.IsNullOrWhiteSpace(Button2);
        _button3.IsVisible = !string.IsNullOrWhiteSpace(Button3);
    }

    private void PlayOpenAnimation()
    {
        Opacity = 0;
        if (RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform(0, 18);
            RenderTransform = translate;
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(this, 1, 120, 40, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateY(translate, -translate.Y, 260, 40, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
        }, $"MyMsgMarkdown Open {GetHashCode()}");
    }

    private void OnButtonClicked(Avalonia.Interactivity.RoutedEventArgs e, EventHandler? handler)
    {
        handler?.Invoke(this, EventArgs.Empty);
        if (!e.Handled)
            Close();
    }

    private void RemoveFromParent()
    {
        if (Parent is Panel panel)
            panel.Children.Remove(this);
        else
            IsVisible = false;
    }
}
