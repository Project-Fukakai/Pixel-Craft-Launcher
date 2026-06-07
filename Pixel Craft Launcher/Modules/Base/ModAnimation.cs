using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Modules.Base;

public static class ModAnimation
{
    private static readonly ConcurrentDictionary<string, AniGroup> Groups = new();
    private static readonly DispatcherTimer Timer = new(TimeSpan.FromMilliseconds(1000d / 60), DispatcherPriority.Render, OnTick);
    private static long _lastTick = Environment.TickCount64;
    private static int _anonymousId;

    public static double AniSpeed { get; set; } = 1d;
    public static bool IsEnabled { get; set; } = true;
    public static int AniControlEnabled { get; set; }

    public static void AniStart()
    {
        _lastTick = Environment.TickCount64;
        Timer.Start();
    }

    public static void AniStart(AniData data, string name = "", bool refreshTime = false)
    {
        AniStart(new[] { data }, name, refreshTime);
    }

    public static void AniStart(IEnumerable<AniData> data, string name = "", bool refreshTime = false)
    {
        if (!IsEnabled || AniControlEnabled > 0)
        {
            foreach (var animation in data)
                animation.Finish();
            return;
        }

        name = string.IsNullOrWhiteSpace(name) ? $"anonymous-{Interlocked.Increment(ref _anonymousId)}" : name;
        var group = new AniGroup(data.ToList());
        if (refreshTime || !Groups.TryGetValue(name, out _))
            Groups[name] = group;
        else
            Groups[name] = group;

        if (!Timer.IsEnabled)
            AniStart();
    }

    public static void AniStop(string name)
    {
        Groups.TryRemove(name, out _);
    }

    public static AniData AaOpacity(Visual target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return Number(value, time, delay, ease, after, progress =>
        {
            target.Opacity = Math.Clamp(target.Opacity + progress.Delta, 0d, 1d);
        }, () => target.Opacity = Math.Clamp(target.Opacity + value, 0d, 1d));
    }

    public static AniData AaWidth(Layoutable target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return Number(value, time, delay, ease, after, progress =>
        {
            var current = double.IsNaN(target.Width) ? target.Bounds.Width : target.Width;
            target.Width = Math.Max(0, current + progress.Delta);
        });
    }

    public static AniData AaHeight(Layoutable target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return Number(value, time, delay, ease, after, progress =>
        {
            var current = double.IsNaN(target.Height) ? target.Bounds.Height : target.Height;
            target.Height = Math.Max(0, current + progress.Delta);
        });
    }

    public static AniData AaTranslateX(TranslateTransform target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return Number(value, time, delay, ease, after, progress => target.X += progress.Delta);
    }

    public static AniData AaTranslateY(TranslateTransform target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return Number(value, time, delay, ease, after, progress => target.Y += progress.Delta);
    }

    public static AniData AaX(Visual target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return AaTranslateX(EnsureTranslateTransform(target), value, time, delay, ease, after);
    }

    public static AniData AaY(Visual target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        return AaTranslateY(EnsureTranslateTransform(target), value, time, delay, ease, after);
    }

    public static AniData AaScaleTransform(ScaleTransform target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false, bool absolute = false)
    {
        var startX = target.ScaleX;
        var startY = target.ScaleY;
        var distanceX = absolute ? value - startX : value;
        var distanceY = absolute ? value - startY : value;
        return Number(1, time, delay, ease, after, progress =>
        {
            target.ScaleX += distanceX * progress.Delta;
            target.ScaleY += distanceY * progress.Delta;
        }, () =>
        {
            target.ScaleX = absolute ? value : startX + distanceX;
            target.ScaleY = absolute ? value : startY + distanceY;
        });
    }

    public static AniData AaRotateTransform(RotateTransform target, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false, bool absolute = false)
    {
        var start = target.Angle;
        var distance = absolute ? value - start : value;
        if (absolute)
        {
            return Number(1, time, delay, ease, after, progress => target.Angle += distance * progress.Delta,
                () => target.Angle = value);
        }

        var lastProgress = 0d;
        return Number(1, time, delay, ease, after, progress =>
        {
            lastProgress = progress.Value;
            target.Angle += distance * progress.Delta;
        }, () =>
        {
            if (lastProgress < 1d)
                target.Angle += distance * (1d - lastProgress);
        });
    }

    public static AniData AaColor(AvaloniaObject target, AvaloniaProperty property, string resourceKey, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        var brush = ResolveBrush(resourceKey) ?? Brushes.Transparent;
        return AaColor(target, property, brush, time, delay, ease, after);
    }

    public static AniData AaColor(AvaloniaObject target, AvaloniaProperty property, IBrush to, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        var from = GetBrush(target, property);
        var fromColor = ToColor(from);
        var toColor = ToColor(to);
        return Number(1, time, delay, ease, after, progress =>
        {
            target.SetValue(property, new SolidColorBrush(Interpolate(fromColor, toColor, progress.Value)));
        }, () => target.SetValue(property, to));
    }

    public static AniData AaCode(Action action, int delay = 0, bool after = false)
    {
        return new AniData(0, delay, new AniEaseLinear(), after, _ => { }, action);
    }

    private static AniData Number(double value, int time, int delay, AniEase? ease, bool after, Action<AniProgress> step, Action? finish = null)
    {
        return new AniData(time, delay, ease ?? new AniEaseLinear(), after, progress =>
        {
            var current = value * progress.Eased;
            var previous = value * progress.PreviousEased;
            step(new AniProgress(progress.Eased, current - previous));
        }, finish);
    }

    private static void OnTick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        var delta = (int)Math.Clamp((now - _lastTick) * AniSpeed, 0, 100);
        _lastTick = now;

        foreach (var item in Groups.ToArray())
        {
            if (item.Value.Tick(delta))
                Groups.TryRemove(item.Key, out _);
        }

        if (Groups.IsEmpty)
            Timer.Stop();
    }

    private static IBrush? GetBrush(AvaloniaObject target, AvaloniaProperty property)
    {
        return target.GetValue(property) as IBrush;
    }

    private static TranslateTransform EnsureTranslateTransform(Visual target)
    {
        if (target.RenderTransform is TranslateTransform translate)
            return translate;

        translate = new TranslateTransform();
        target.RenderTransform = translate;
        return translate;
    }

    private static IBrush? ResolveBrush(string key)
    {
        return Application.Current?.TryGetResource(key, null, out var value) == true ? value as IBrush : null;
    }

    private static Color ToColor(IBrush? brush)
    {
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
    }

    private static Color Interpolate(Color from, Color to, double progress)
    {
        static byte Mix(byte a, byte b, double p) => (byte)Math.Clamp(a + (b - a) * p, 0, 255);
        return Color.FromArgb(Mix(from.A, to.A, progress), Mix(from.R, to.R, progress), Mix(from.G, to.G, progress), Mix(from.B, to.B, progress));
    }

    private sealed class AniGroup
    {
        private readonly List<AniData> _data;

        public AniGroup(List<AniData> data)
        {
            _data = data;
        }

        public bool Tick(int delta)
        {
            var canRunAfter = true;
            for (var i = 0; i < _data.Count;)
            {
                var animation = _data[i];
                if (animation.IsAfter && !canRunAfter)
                    break;

                animation.IsAfter = false;
                canRunAfter = false;
                if (animation.Tick(delta))
                {
                    _data.RemoveAt(i);
                    canRunAfter = true;
                    continue;
                }

                i++;
            }

            return _data.Count == 0;
        }
    }

    public sealed class AniData
    {
        private readonly int _time;
        private readonly int _delay;
        private readonly AniEase _ease;
        private readonly Action<(double Eased, double PreviousEased)> _step;
        private readonly Action? _finish;
        private int _elapsed;
        private double _previousEased;
        private bool _finished;

        internal AniData(int time, int delay, AniEase ease, bool isAfter, Action<(double Eased, double PreviousEased)> step, Action? finish)
        {
            _time = Math.Max(0, time);
            _delay = Math.Max(0, delay);
            _ease = ease;
            IsAfter = isAfter;
            _step = step;
            _finish = finish;
            _elapsed = -_delay;
        }

        public bool IsAfter { get; set; }

        public bool Tick(int delta)
        {
            if (_finished)
                return true;

            _elapsed += delta;
            if (_elapsed < 0)
                return false;

            if (_time == 0)
            {
                Finish();
                return true;
            }

            var raw = Math.Clamp(_elapsed / (double)_time, 0d, 1d);
            var eased = _ease.GetDelta(raw);
            _step((eased, _previousEased));
            _previousEased = eased;

            if (raw < 1)
                return false;

            Finish();
            return true;
        }

        public void Finish()
        {
            if (_finished)
                return;
            _finished = true;
            _finish?.Invoke();
        }
    }

    public readonly record struct AniProgress(double Value, double Delta);

    public abstract class AniEase
    {
        public abstract double GetDelta(double t);
    }

    public sealed class AniEaseLinear : AniEase
    {
        public override double GetDelta(double t) => t;
    }

    public sealed class AniEaseInFluent : AniEase
    {
        private readonly double _power;
        public AniEaseInFluent(AniEasePower power = AniEasePower.Middle) => _power = ToPower(power);
        public override double GetDelta(double t) => Math.Pow(t, _power);
    }

    public sealed class AniEaseOutFluent : AniEase
    {
        private readonly double _power;
        public AniEaseOutFluent(AniEasePower power = AniEasePower.Middle) => _power = ToPower(power);
        public override double GetDelta(double t) => 1 - Math.Pow(1 - t, _power);
    }

    public sealed class AniEaseInoutFluent : AniEase
    {
        private readonly AniEaseInFluent _easeIn;
        private readonly AniEaseOutFluent _easeOut;
        private readonly double _middle;

        public AniEaseInoutFluent(AniEasePower power = AniEasePower.Middle, double middle = 0.5d)
        {
            _easeIn = new AniEaseInFluent(power);
            _easeOut = new AniEaseOutFluent(power);
            _middle = Math.Clamp(middle, 0.05, 0.95);
        }

        public override double GetDelta(double t)
        {
            return t < _middle
                ? _easeIn.GetDelta(t / _middle) * _middle
                : _middle + _easeOut.GetDelta((t - _middle) / (1 - _middle)) * (1 - _middle);
        }
    }

    public sealed class AniEaseOutBack : AniEase
    {
        private readonly double _power;
        public AniEaseOutBack(AniEasePower power = AniEasePower.Middle) => _power = 3d - ToPlainPower(power) * 0.5d;
        public override double GetDelta(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return 1d - Math.Pow(1d - t, _power) * Math.Cos(1.5d * Math.PI * t);
        }
    }

    public sealed class AniEaseInBack : AniEase
    {
        private readonly double _power;
        public AniEaseInBack(AniEasePower power = AniEasePower.Middle) => _power = 3d - ToPlainPower(power) * 0.5d;
        public override double GetDelta(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return Math.Pow(t, _power) * Math.Cos(1.5d * Math.PI * (1d - t));
        }
    }

    public sealed class AniEaseOutElastic : AniEase
    {
        private readonly int _power;
        public AniEaseOutElastic(AniEasePower power = AniEasePower.Middle) => _power = (int)ToPlainPower(power) + 4;
        public override double GetDelta(double t)
        {
            t = 1d - Math.Clamp(t, 0d, 1d);
            return 1d - Math.Pow(t, (_power - 1) * 0.25d) * Math.Cos((_power - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    public enum AniEasePower
    {
        Weak,
        Middle,
        Strong,
        ExtraStrong
    }

    private static double ToPower(AniEasePower power)
    {
        return power switch
        {
            AniEasePower.Weak => 2,
            AniEasePower.Strong => 4,
            AniEasePower.ExtraStrong => 5,
            _ => 3
        };
    }

    private static double ToPlainPower(AniEasePower power)
    {
        return power switch
        {
            AniEasePower.Weak => 2,
            AniEasePower.Strong => 4,
            AniEasePower.ExtraStrong => 5,
            _ => 3
        };
    }
}
