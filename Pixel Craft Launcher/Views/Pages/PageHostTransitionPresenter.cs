using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Views.Pages;

internal sealed class PageHostTransitionPresenter
{
    private readonly Border _leftPane;
    private readonly ContentControl _leftHost;
    private readonly ContentControl _rightHost;

    public PageHostTransitionPresenter(Border leftPane, ContentControl leftHost, ContentControl rightHost)
    {
        _leftPane = leftPane;
        _leftHost = leftHost;
        _rightHost = rightHost;
    }

    public void AnimateLeftPaneWidth(double targetWidth)
    {
        var currentWidth = double.IsNaN(_leftPane.Width) ? _leftPane.Bounds.Width : _leftPane.Width;
        if (Math.Abs(currentWidth - targetWidth) < 0.5)
            return;

        BeginPaneTransitionLayoutLock(currentWidth, targetWidth, 260);
        ModAnimation.AniStop("FrmMain LeftPaneWidth");
        ModAnimation.AniStart(
            ModAnimation.AaWidth(_leftPane, targetWidth - currentWidth, 220, ease: new ModAnimation.AniEaseOutFluent()),
            "FrmMain LeftPaneWidth",
            true);
    }

    public void SetContent(
        ContentControl host,
        Control newContent,
        PageHostUpdateMode mode,
        Action<Control>? afterShown = null)
    {
        if (ReferenceEquals(host, _rightHost))
            RestoreHostMeasureWidth(host);

        if (mode == PageHostUpdateMode.MainTransition || mode == PageHostUpdateMode.NestedTransition)
        {
            var animationName = GetPageHostAnimationName(host, mode);
            if (ReferenceEquals(host, _leftHost))
                AnimateLeftPageHost(host, newContent, animationName, afterShown);
            else
                AnimateRightPageHost(host, newContent, animationName, afterShown);
            return;
        }

        StopPageHostAnimations(host);
        host.Content = newContent;
        PreparePageHost(host);
        afterShown?.Invoke(newContent);
    }

    private void BeginPaneTransitionLayoutLock(double currentLeftWidth, double targetLeftWidth, int duration)
    {
        var leftContentWidth = Math.Max(currentLeftWidth, targetLeftWidth);

        ModAnimation.AniStop("FrmMain PaneLayoutLock");
        RestoreHostMeasureWidth(_rightHost);
        LockHostMeasureWidth(_leftHost, leftContentWidth);
        _leftPane.ClipToBounds = false;

        ModAnimation.AniStart(
            ModAnimation.AaCode(EndPaneTransitionLayoutLock, duration),
            "FrmMain PaneLayoutLock",
            true);
    }

    private void EndPaneTransitionLayoutLock()
    {
        RestoreHostMeasureWidth(_leftHost);
        RestoreHostMeasureWidth(_rightHost);
        _leftPane.ClipToBounds = true;
    }

    private static void LockHostMeasureWidth(ContentControl host, double width)
    {
        if (width <= 0 || double.IsNaN(width) || double.IsInfinity(width))
            return;

        host.Width = width;
        host.MinWidth = width;
        host.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private static void RestoreHostMeasureWidth(ContentControl host)
    {
        host.Width = double.NaN;
        host.MinWidth = 0;
        host.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private static void PreparePageHost(ContentControl host)
    {
        host.Opacity = 1;
        host.IsHitTestVisible = true;
        if (host.RenderTransform is not TranslateTransform)
            host.RenderTransform = new TranslateTransform();
        if (host.RenderTransform is TranslateTransform translate)
        {
            translate.X = 0;
            translate.Y = 0;
        }
    }

    private string GetPageHostAnimationName(ContentControl host, PageHostUpdateMode mode)
    {
        if (mode == PageHostUpdateMode.NestedTransition)
            return ReferenceEquals(host, _rightHost) ? "FrmMain NestedPageChangeRight" : "FrmMain NestedPageChangeLeft";
        return ReferenceEquals(host, _leftHost) ? "FrmMain PageChangeLeft" : "FrmMain PageChangeRight";
    }

    private void StopPageHostAnimations(ContentControl host)
    {
        if (ReferenceEquals(host, _leftHost))
        {
            ModAnimation.AniStop("FrmMain PageChangeLeft");
            ModAnimation.AniStop("FrmMain NestedPageChangeLeft");
            ModAnimation.AniStop("PageLeft LaunchScale");
            ModAnimation.AniStop("PageLeft MenuItems");
            return;
        }

        ModAnimation.AniStop("FrmMain PageChangeRight");
        ModAnimation.AniStop("FrmMain NestedPageChangeRight");
    }

    private void AnimateLeftPageHost(
        ContentControl host,
        Control newContent,
        string animationName,
        Action<Control>? afterShown = null)
    {
        ModAnimation.AniStop(animationName);
        StopPageHostAnimations(host);
        host.IsHitTestVisible = false;
        PreparePageHost(host);

        var oldContent = host.Content as Control;
        var animations = new List<ModAnimation.AniData>();
        var switchDelay = 120;

        if (oldContent is not null)
        {
            if (oldContent.RenderTransform is ScaleTransform scale)
            {
                animations.Add(ModAnimation.AaScaleTransform(scale, 0.96, 140,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak), absolute: true));
                animations.Add(ModAnimation.AaOpacity(oldContent, -oldContent.Opacity, 110,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            }
            else
            {
                var controls = LaunchPageVisualEffects.GetSidebarAnimControls(oldContent).Reverse().ToArray();
                var delay = 0;
                foreach (var control in controls)
                {
                    var translate = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translate;
                    animations.Add(ModAnimation.AaOpacity(control, -control.Opacity, 90, delay,
                        new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                    animations.Add(ModAnimation.AaTranslateX(translate, -20 - translate.X, 130, delay,
                        new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
                    delay += 12;
                }

                switchDelay = Math.Max(110, delay + 80);
            }
        }

        animations.AddRange(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ModAnimation.AniControlEnabled += 1;
                host.Content = newContent;
                ModAnimation.AniControlEnabled -= 1;
                PreparePageHost(host);
                afterShown?.Invoke(newContent);
            }, switchDelay),
            ModAnimation.AaCode(() =>
            {
                PreparePageHost(host);
                host.IsHitTestVisible = true;
            }, switchDelay + 420)
        });
        ModAnimation.AniStart(animations, animationName, true);
    }

    private void AnimateRightPageHost(
        ContentControl host,
        Control newContent,
        string animationName,
        Action<Control>? afterShown = null)
    {
        ModAnimation.AniStop(animationName);
        StopPageHostAnimations(host);
        host.IsHitTestVisible = false;
        PreparePageHost(host);

        var oldItems = GetRightPagePrimaryControls(host.Content as Control).Reverse().ToArray();
        var animations = new List<ModAnimation.AniData>();
        var delay = 0;
        foreach (var item in oldItems)
        {
            var translate = item.RenderTransform as TranslateTransform ?? new TranslateTransform();
            item.RenderTransform = translate;
            animations.Add(ModAnimation.AaOpacity(item, -item.Opacity, 90, delay,
                new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateY(translate, -18 - translate.Y, 130, delay,
                new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            delay += 18;
        }

        var switchDelay = oldItems.Length == 0 ? 80 : delay + 90;
        PrepareRightPageEnterItems(newContent);
        animations.Add(ModAnimation.AaCode(() =>
        {
            ModAnimation.AniControlEnabled += 1;
            host.Content = newContent;
            ModAnimation.AniControlEnabled -= 1;
            PreparePageHost(host);
            afterShown?.Invoke(newContent);
        }, switchDelay));

        var enterDelay = switchDelay + 35;
        foreach (var item in GetRightPagePrimaryControls(newContent))
        {
            if (item.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform();
                item.RenderTransform = translate;
            }

            animations.Add(ModAnimation.AaOpacity(item, 1 - item.Opacity, 130, enterDelay,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateY(translate, -translate.Y, 260, enterDelay,
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            enterDelay += 28;
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            ResetRightPageEnterItems(newContent);
            PreparePageHost(host);
            host.IsHitTestVisible = true;
        }, enterDelay + 260));
        ModAnimation.AniStart(animations, animationName, true);
    }

    private static void PrepareRightPageEnterItems(Control root)
    {
        foreach (var item in GetRightPagePrimaryControls(root))
        {
            item.Opacity = 0;
            item.RenderTransform = new TranslateTransform(0, 22);
        }
    }

    private static void ResetRightPageEnterItems(Control root)
    {
        foreach (var item in GetRightPagePrimaryControls(root))
        {
            item.Opacity = 1;
            if (item.RenderTransform is TranslateTransform translate)
            {
                translate.X = 0;
                translate.Y = 0;
            }
        }
    }

    private static IEnumerable<Control> GetRightPagePrimaryControls(Control? root)
    {
        if (root is null)
            yield break;

        if (root is MainPaneScrollHost scrollHost)
        {
            foreach (var child in scrollHost.Children.OfType<Control>())
            {
                if (child is Panel panel)
                {
                    foreach (var panelChild in panel.Children.OfType<Control>().Where(static c => c.IsVisible))
                        yield return panelChild;
                }
                else if (child.IsVisible)
                {
                    yield return child;
                }
            }
            yield break;
        }

        if (root is Panel rootPanel)
        {
            foreach (var child in rootPanel.Children.OfType<Control>().Where(static c => c.IsVisible))
                yield return child;
            yield break;
        }

        yield return root;
    }
}
