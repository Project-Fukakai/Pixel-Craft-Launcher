using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls.MyMsg;

public class MyMsgForm : Border
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyMsgForm, string?>(nameof(Title));

    private readonly TextBlock _title = new() { FontSize = 23, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _content = new() { Spacing = 10 };
    private readonly MyButton _button1 = new();
    private readonly MyButton _button2 = new();

    public MyMsgForm()
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
            Spacing = 8,
            Children = { _button2, _button1 }
        };

        Child = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                _title,
                new Rectangle { Height = 1, Fill = ThemeBrushes.Border },
                _content,
                buttons
            }
        };

        _button1.Click += (_, e) => OnButtonClicked(e, Button1Click);
        _button2.Click += (_, e) => OnButtonClicked(e, Button2Click);
        AttachedToVisualTree += (_, _) => PlayOpenAnimation();
        UpdateVisuals();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Panel ContentPanel => _content;

    public MyButton Button1 => _button1;

    public MyButton Button2 => _button2;

    public event EventHandler? Button1Click;

    public event EventHandler? Button2Click;

    public void Close()
    {
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(this, -Opacity, 90, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaTranslateY((TranslateTransform)RenderTransform!, 18, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RemoveFromParent, 150)
        }, $"MyMsgForm Close {GetHashCode()}");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
            UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _title.Text = Title;
        _title.Foreground = ThemeBrushes.PrimaryHover;
        _button1.Variant = MyButtonVariant.Flat;
        _button2.Variant = MyButtonVariant.Text;
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
        }, $"MyMsgForm Open {GetHashCode()}");
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
