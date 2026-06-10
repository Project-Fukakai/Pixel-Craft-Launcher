using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Pixel.Shell;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Pages;

public static class LaunchPageVisualEffects
{
    public static void TriggerLeftPageShowAnimation(Control root, MainPageKind page)
    {
        if (page == MainPageKind.Launch && root.RenderTransform is ScaleTransform)
        {
            AnimateLaunchLeftScale(root);
            return;
        }

        AnimateSidebarItems(root);
    }

    public static IEnumerable<Control> GetSidebarAnimControls(Control root)
    {
        if (root is MyListItem or MyTextBox or TextBlock)
        {
            yield return root;
            yield break;
        }

        if (root is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
            {
                if (!child.IsVisible)
                    continue;

                foreach (var nested in GetSidebarAnimControls(child))
                    yield return nested;
            }
        }
        else if (root is ContentControl { Content: Control content })
        {
            foreach (var nested in GetSidebarAnimControls(content))
                yield return nested;
        }
    }

    private static void AnimateLaunchLeftScale(Control root)
    {
        if (root.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(0.96, 0.96);
            root.RenderTransform = scale;
        }

        root.Opacity = 0;
        scale.ScaleX = 0.96;
        scale.ScaleY = 0.96;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaScaleTransform(scale, 1, 400, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Strong), absolute: true),
            ModAnimation.AaOpacity(root, 1, 100),
            ModAnimation.AaCode(() =>
            {
                root.Opacity = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, 420)
        }, "PageLeft LaunchScale", true);
    }

    private static void AnimateSidebarItems(Control root)
    {
        var controls = GetSidebarAnimControls(root).ToArray();
        var animations = new List<ModAnimation.AniData>();
        var delay = 0;
        var index = 0;
        foreach (var control in controls)
        {
            var translate = new TranslateTransform(-25, 0);
            control.RenderTransform = translate;
            control.Opacity = 0;
            animations.Add(ModAnimation.AaOpacity(control, control is TextBlock ? 0.6 : 1, 100, delay,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateX(translate, 5, 200, delay, new ModAnimation.AniEaseOutFluent()));
            animations.Add(ModAnimation.AaTranslateX(translate, 20, 300, delay, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            delay += Math.Max(15 - index, 7) * 2;
            index++;
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (var control in controls)
            {
                control.Opacity = control is TextBlock ? 0.6 : 1;
                if (control.RenderTransform is TranslateTransform translate)
                    translate.X = 0;
            }
        }, delay + 320));

        if (animations.Count > 0)
            ModAnimation.AniStart(animations, "PageLeft MenuItems", true);
    }
}
