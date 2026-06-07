using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Pixel_Craft_Launcher.Controls;

[PseudoClasses(":invalid")]
public class MyTextBox : TextBox
{
    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<MyTextBox, string?>(nameof(HintText));

    public static readonly DirectProperty<MyTextBox, string?> ValidateResultProperty =
        AvaloniaProperty.RegisterDirect<MyTextBox, string?>(nameof(ValidateResult), o => o.ValidateResult);

    public static readonly DirectProperty<MyTextBox, bool> IsValidatedProperty =
        AvaloniaProperty.RegisterDirect<MyTextBox, bool>(nameof(IsValidated), o => o.IsValidated);

    private string? _validateResult;
    private bool _isValidated = true;

    public MyTextBox()
    {
        TextChanged += (_, _) => Validate();
    }

    public event EventHandler? ValidateChanged;

    public IList<Func<string?, string?>> ValidateRules { get; } = new List<Func<string?, string?>>();

    public string? HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public string? ValidateResult
    {
        get => _validateResult;
        private set => SetAndRaise(ValidateResultProperty, ref _validateResult, value);
    }

    public bool IsValidated
    {
        get => _isValidated;
        private set => SetAndRaise(IsValidatedProperty, ref _isValidated, value);
    }

    public void Validate()
    {
        var result = ValidateRules.Select(rule => rule(Text)).FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        ValidateResult = result;
        IsValidated = string.IsNullOrWhiteSpace(result);
        PseudoClasses.Set(":invalid", !IsValidated);
        ValidateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HintTextProperty)
            PlaceholderText = HintText;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
}
