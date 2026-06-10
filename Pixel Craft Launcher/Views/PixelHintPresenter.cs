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

public sealed class PixelHintPresenter(Panel host)
{
    private int _hintId;

    private sealed class HintTag
    {
        public HintTag(int id, string text)
        {
            Id = id;
            Text = text;
        }

        public int Id { get; }
        public string Text { get; }
        public bool Reusable { get; set; } = true;
    }

    public void Show(string text, MyHint.Themes theme)
    {
        var type = theme switch
        {
            MyHint.Themes.Red => HintType.Critical,
            MyHint.Themes.Yellow => HintType.Finish,
            _ => HintType.Info
        };
        Show(text, type);
    }

    public void Show(string text, HintType type)
    {
        text = (text ?? string.Empty)
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (string.IsNullOrWhiteSpace(text) || host.Children.Count >= 20)
            return;

        var duplicate = host.Children
            .OfType<Border>()
            .FirstOrDefault(child => child.Tag is HintTag { Reusable: true } tag && tag.Text == text);

        if (duplicate is not null && duplicate.Tag is HintTag duplicateTag)
        {
            PlayDuplicateHint(duplicate, duplicateTag, type);
            return;
        }

        var tag = new HintTag(++_hintId, text);
        var hint = CreatePopupHint(text, type, tag);
        host.Children.Add(hint);

        var translate = EnsureHintTransform(hint);
        var animations = new List<ModAnimation.AniData>();
        if (host.Children.Count > 1)
            animations.Add(ModAnimation.AaHeight(hint, 26, 150, ease: new ModAnimation.AniEaseOutFluent()));
        else
            hint.Height = 26;

        animations.AddRange(new[]
        {
            ModAnimation.AaTranslateX(translate, 70, 450, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaOpacity(hint, 1, 100)
        });
        ModAnimation.AniStart(animations, $"Hint Show {tag.Id}", true);
        ScheduleHintHide(hint, tag, GetHintDelay(text));
    }

    private Border CreatePopupHint(string text, HintType type, HintTag tag)
    {
        var (start, end) = GetHintBrushColors(type);
        return new Border
        {
            Tag = tag,
            Margin = new Thickness(0, 0, 20, 0),
            Opacity = 0,
            Height = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            RenderTransform = new TranslateTransform(-70, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(start, 0),
                    new GradientStop(end, 1)
                }
            },
            Child = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 13,
                Foreground = ThemeBrushes.OnPrimary,
                Margin = new Thickness(33, 5, 8, 5)
            }
        };
    }

    private void PlayDuplicateHint(Border hint, HintTag tag, HintType type)
    {
        var translate = EnsureHintTransform(hint);
        var (start, end) = GetHintBrushColors(type);
        if (hint.Background is LinearGradientBrush gradient && gradient.GradientStops.Count >= 2)
        {
            gradient.GradientStops[0].Color = start;
            gradient.GradientStops[1].Color = end;
        }

        ModAnimation.AniStop($"Hint Hide {tag.Id}");
        ModAnimation.AniStop($"Hint Show {tag.Id}");
        hint.Opacity = 1;
        hint.Height = 26;
        tag.Reusable = true;

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaTranslateX(translate, 8, 50, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(translate, -8, 50, 50, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaTranslateX(translate, 8, 50, 100, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(translate, -8, 50, 150, new ModAnimation.AniEaseInFluent())
        }, $"Hint Show {tag.Id}", true);
        ScheduleHintHide(hint, tag, GetHintDelay(tag.Text));
    }

    private void ScheduleHintHide(Border hint, HintTag tag, int delay)
    {
        var translate = EnsureHintTransform(hint);
        ModAnimation.AniStop($"Hint Hide {tag.Id}");
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaTranslateX(translate, -70 - translate.X, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(hint, -1, 150, delay),
            ModAnimation.AaCode(() => tag.Reusable = false, delay),
            ModAnimation.AaHeight(hint, -26, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() => host.Children.Remove(hint), after: true)
        }, $"Hint Hide {tag.Id}", true);
    }

    private static TranslateTransform EnsureHintTransform(Border hint)
    {
        if (hint.RenderTransform is TranslateTransform translate)
            return translate;
        translate = new TranslateTransform();
        hint.RenderTransform = translate;
        return translate;
    }

    private static int GetHintDelay(string text)
    {
        return (int)Math.Round(800 + Math.Clamp(text.Length, 5, 23) * 180d);
    }

    private static (Color start, Color end) GetHintBrushColors(HintType type)
    {
        return type switch
        {
            HintType.Finish => (ThemeBrushes.SuccessColor, ThemeBrushes.SuccessContainerColor),
            HintType.Critical => (ThemeBrushes.ErrorColor, ThemeBrushes.ErrorContainerColor),
            _ => (ThemeBrushes.InfoColor, ThemeBrushes.PrimaryHoverColor)
        };
    }
}
