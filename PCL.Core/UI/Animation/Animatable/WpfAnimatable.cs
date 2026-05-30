using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Animatable;

public sealed class WpfAnimatable(DependencyObject owner, DependencyProperty? property) : IAnimatable
{
    public DependencyObject Owner { get; set; } = owner;
    public DependencyProperty? Property { get; set; } = property;

    public object? GetValue()
    {
        DependencyProperty? actualProperty;

        if (Owner is Control control && Property == Layoutable.WidthProperty)
        {
            return control.Bounds.Width;
        }
        else if (Owner is Control control2 && Property == Layoutable.HeightProperty)
        {
            return control2.Bounds.Height;
        }
        else
        {
            actualProperty = Property;
        }

        ArgumentNullException.ThrowIfNull(actualProperty);
        
        return Owner.GetValue(actualProperty);
    }

    public void SetValue(object value)
    {
        value = ValueProcessorManager.Filter(value);
        ArgumentNullException.ThrowIfNull(Property);
        Owner.SetValue(Property, value);
    }
}
