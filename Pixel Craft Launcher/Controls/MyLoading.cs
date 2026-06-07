using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Pixel_Craft_Launcher.Modules.Base;

namespace Pixel_Craft_Launcher.Controls;

public class MyLoading : StackPanel
{
    public delegate void ClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void IsErrorChangedEventHandler(object sender, bool isError);

    public delegate void StateChangedEventHandler(object sender, MyLoadingState newState, MyLoadingState oldState);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyLoading, string?>(nameof(Text), "Loading");

    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<MyLoading, bool>(nameof(IsError));

    public static readonly StyledProperty<bool> ShowProgressProperty =
        AvaloniaProperty.Register<MyLoading, bool>(nameof(ShowProgress));

    public static readonly StyledProperty<ILoadingTrigger?> StateProperty =
        AvaloniaProperty.Register<MyLoading, ILoadingTrigger?>(nameof(State));

    private readonly MyM3LoadingIndicator _indicator = new()
    {
        Size = 48,
        Color = ThemeBrushes.Primary,
        ErrorColor = ThemeBrushes.Error,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private readonly TextBlock _label = new()
    {
        FontSize = 16,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };

    public MyLoading()
    {
        MinWidth = 50;
        MinHeight = 50;
        Spacing = 10;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_indicator);
        Children.Add(_label);
        PointerReleased += (_, e) => Click?.Invoke(this, e);
        UpdateVisuals();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsError
    {
        get => GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public bool ShowProgress
    {
        get => GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    public ILoadingTrigger? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public event IsErrorChangedEventHandler? IsErrorChanged;

    public event StateChangedEventHandler? StateChanged;

    public event ClickEventHandler? Click;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            if (change.OldValue is ILoadingTrigger oldTrigger)
            {
                oldTrigger.LoadingStateChanged -= OnLoadingStateChanged;
                oldTrigger.ProgressChanged -= OnProgressChanged;
            }

            if (change.NewValue is ILoadingTrigger newTrigger)
            {
                newTrigger.LoadingStateChanged += OnLoadingStateChanged;
                newTrigger.ProgressChanged += OnProgressChanged;
                ApplyState(newTrigger.LoadingState, MyLoadingState.Unloaded);
            }
        }
        else if (change.Property == TextProperty || change.Property == IsErrorProperty || change.Property == ShowProgressProperty)
        {
            if (change.Property == IsErrorProperty)
                IsErrorChanged?.Invoke(this, IsError);

            UpdateVisuals();
        }
    }

    private void OnLoadingStateChanged(ILoadingTrigger sender, MyLoadingState newState, MyLoadingState oldState)
    {
        ApplyState(newState, oldState);
    }

    private void OnProgressChanged(ILoadingTrigger sender, double progress)
    {
        UpdateVisuals();
    }

    private void ApplyState(MyLoadingState newState, MyLoadingState oldState)
    {
        IsError = newState == MyLoadingState.Error;
        IsVisible = newState is MyLoadingState.Loading or MyLoadingState.Run or MyLoadingState.Error;
        _indicator.IsRunning = IsVisible;
        StateChanged?.Invoke(this, newState, oldState);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var brush = IsError ? ThemeBrushes.Error : ThemeBrushes.Primary;
        _indicator.Color = ThemeBrushes.Primary;
        _indicator.ErrorColor = ThemeBrushes.Error;
        _indicator.IsError = IsError;
        _indicator.IsRunning = IsVisible;

        ModAnimation.AniStart(ModAnimation.AaColor(_label, TextBlock.ForegroundProperty, brush, 160),
            $"MyLoading Color {GetHashCode()}");

        if (IsError && State?.Error is { } error)
            _label.Text = error.GetBaseException().Message;
        else if (ShowProgress && State is not null && State.LoadingState is MyLoadingState.Loading or MyLoadingState.Run)
            _label.Text = $"{Text} - {Math.Floor(State.Progress * 100)}%";
        else
            _label.Text = Text;
    }
}
