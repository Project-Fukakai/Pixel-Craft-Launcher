using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views;

public sealed class PixelFloatingActionPresenter(
    Canvas actionsHost,
    Canvas rippleHost,
    Func<Size> getWindowSize)
{
    private readonly Dictionary<MyFloatingActionButton, FloatingActionState> _actions = new();

    private sealed class FloatingActionState
    {
        public int Index { get; set; }
        public bool IsShown { get; set; }
        public double Bottom { get; set; }
    }

    public void Register(MyFloatingActionButton button, int index)
    {
        _actions[button] = new FloatingActionState { Index = index };
        Canvas.SetLeft(button, 0);
        Canvas.SetBottom(button, 0);
        button.ScaleTransform.ScaleX = 0.72;
        button.ScaleTransform.ScaleY = 0.72;
        button.TranslateTransform.Y = 12;
    }

    public void Show(MyFloatingActionButton button, int index, bool visible, bool playRippleWhenFirstShown = false)
    {
        if (!_actions.TryGetValue(button, out var state))
            Register(button, index);

        state = _actions[button];
        state.Index = index;
        ModAnimation.AniStop(GetShowAnimationName(button));

        if (visible)
        {
            var wasShown = state.IsShown;
            state.IsShown = true;
            button.IsVisible = true;
            Reflow();
            if (!wasShown)
            {
                ModAnimation.AniStop(GetMoveAnimationName(button));
                button.Opacity = 0;
                button.ScaleTransform.ScaleX = 0.72;
                button.ScaleTransform.ScaleY = 0.72;
                button.TranslateTransform.Y = 12;
                ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaOpacity(button, 1 - button.Opacity, 140, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaScaleTransform(button.ScaleTransform, 1, 300, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
                    ModAnimation.AaTranslateY(button.TranslateTransform, -button.TranslateTransform.Y, 260, ease: new ModAnimation.AniEaseOutFluent())
                }, GetShowAnimationName(button), true);
                if (playRippleWhenFirstShown)
                    PlayRipple(button);
            }

            return;
        }

        if (!state.IsShown && !button.IsVisible)
            return;

        state.IsShown = false;
        ModAnimation.AniStop(GetMoveAnimationName(button));
        Reflow();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(button, -button.Opacity, 110, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaScaleTransform(button.ScaleTransform, 0.72, 180, ease: new ModAnimation.AniEaseOutFluent(), absolute: true),
            ModAnimation.AaTranslateY(button.TranslateTransform, 12 - button.TranslateTransform.Y, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() => button.IsVisible = false, 180)
        }, GetShowAnimationName(button), true);
    }

    private void Reflow()
    {
        const double spacing = 10;
        var shown = _actions
            .Where(static pair => pair.Value.IsShown)
            .OrderBy(static pair => pair.Value.Index)
            .ThenBy(static pair => pair.Key.GetHashCode())
            .ToArray();

        actionsHost.Height = shown.Length == 0
            ? 44
            : shown.Length * 44 + Math.Max(0, shown.Length - 1) * spacing;

        for (var i = 0; i < shown.Length; i++)
        {
            var button = shown[i].Key;
            var state = shown[i].Value;
            var targetBottom = i * (44 + spacing);
            var oldBottom = state.Bottom;
            state.Bottom = targetBottom;
            Canvas.SetBottom(button, targetBottom);
            button.TranslateTransform.Y += oldBottom - targetBottom;
            ModAnimation.AniStart(
                ModAnimation.AaTranslateY(button.TranslateTransform, -button.TranslateTransform.Y, 240,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                GetMoveAnimationName(button),
                true);
        }
    }

    private static string GetShowAnimationName(MyFloatingActionButton button) => $"FloatingAction Show {button.GetHashCode()}";

    private static string GetMoveAnimationName(MyFloatingActionButton button) => $"FloatingAction Move {button.GetHashCode()}";

    private void PlayRipple(MyFloatingActionButton sourceButton)
    {
        var center = sourceButton.TranslatePoint(
            new Point(sourceButton.Bounds.Width / 2d, sourceButton.Bounds.Height / 2d),
            rippleHost);
        if (center is null)
            return;

        const double rippleSize = 44d;
        var ripple = new Border
        {
            Width = rippleSize,
            Height = rippleSize,
            CornerRadius = new CornerRadius(1000),
            BorderThickness = new Thickness(0.001),
            Opacity = 0.5,
            Background = ThemeBrushes.PrimaryHover,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ripple, center.Value.X - rippleSize / 2d);
        Canvas.SetTop(ripple, center.Value.Y - rippleSize / 2d);
        rippleHost.Children.Insert(0, ripple);

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaScaleTransform((ScaleTransform)ripple.RenderTransform, GetRippleScale(center.Value, rippleSize), 1000,
                ease: new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Strong, 0.3),
                absolute: true),
            ModAnimation.AaOpacity(ripple, -ripple.Opacity, 1000),
            ModAnimation.AaCode(() => rippleHost.Children.Remove(ripple), after: true)
        }, $"DownloadTask Ripple {Guid.NewGuid():N}");
    }

    private double GetRippleScale(Point center, double rippleSize)
    {
        var windowSize = getWindowSize();
        var width = rippleHost.Bounds.Width > 0 ? rippleHost.Bounds.Width : windowSize.Width;
        var height = rippleHost.Bounds.Height > 0 ? rippleHost.Bounds.Height : windowSize.Height;
        if (width <= 0 || height <= 0)
            return 13d;

        var maxDistance = new[]
        {
            Distance(center, new Point(0, 0)),
            Distance(center, new Point(width, 0)),
            Distance(center, new Point(0, height)),
            Distance(center, new Point(width, height))
        }.Max();
        return Math.Max(13d, maxDistance / (rippleSize / 2d));
    }

    private static double Distance(Point a, Point b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
