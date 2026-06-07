using Avalonia;

namespace Pixel_Craft_Launcher.Controls;

public class MyIconTextButton : MyButton
{
    public delegate void ChangeEventHandler(object sender, bool raiseByMouse);

    public delegate void CheckEventHandler(object sender, bool raiseByMouse);

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyIconTextButton, bool>(nameof(Checked));

    public MyIconTextButton()
    {
        Variant = MyButtonVariant.Tonal;
        Size = MyButtonSize.Medium;
        UseAutoContent();
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    public event CheckEventHandler? Check;

    public event ChangeEventHandler? Change;

    public void SetChecked(bool value, bool raiseByMouse)
    {
        if (Checked == value)
            return;
        SetValue(CheckedProperty, value);
        if (value)
            Check?.Invoke(this, raiseByMouse);
        Change?.Invoke(this, raiseByMouse);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (!IsReadOnly)
            SetChecked(true, true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CheckedProperty)
            Variant = Checked ? MyButtonVariant.Flat : MyButtonVariant.Tonal;
    }
}
