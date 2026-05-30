using System;
using Avalonia;
using Avalonia.Metadata;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Core;

public class RunAnimation : AvaloniaObject
{
    public static readonly StyledProperty<IAnimation?> AnimationProperty =
        AvaloniaProperty.Register<RunAnimation, IAnimation?>(nameof(Animation));

    public IAnimation? Animation
    {
        get => GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    public static readonly StyledProperty<DependencyProperty?> TargetPropertyProperty =
        AvaloniaProperty.Register<RunAnimation, DependencyProperty?>(nameof(TargetProperty));

    public DependencyProperty? TargetProperty
    {
        get => GetValue(TargetPropertyProperty);
        set => SetValue(TargetPropertyProperty, value);
    }

    public void Invoke(DependencyObject associatedObject)
    {
        if (Animation is null) throw new InvalidOperationException("未指定动画。");

        var aniDependencyObject = (DependencyObject)Animation;
        var targetObject = WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetProperty)
            ? (DependencyObject)aniDependencyObject.GetValue(AnimationExtensions.TargetProperty)!
            : associatedObject;

        DependencyProperty? targetProperty;
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetPropertyProperty))
        {
            targetProperty = (DependencyProperty?)aniDependencyObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        }
        else if (WpfUtils.IsDependencyPropertySet(this, TargetPropertyProperty))
        {
            targetProperty = TargetProperty;
        }
        else
        {
            if (Animation is not AnimationGroup)
                throw new InvalidOperationException("未指定动画的目标属性。");
            targetProperty = null;
        }

        Animation.RunFireAndForget(new WpfAnimatable(targetObject, targetProperty));
    }
}
