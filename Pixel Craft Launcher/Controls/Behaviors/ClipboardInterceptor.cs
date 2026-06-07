using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Pixel_Craft_Launcher.Controls.Behaviors;

public static class ClipboardInterceptor
{
    public static readonly AttachedProperty<bool> EnableSafeClipboardProperty =
        AvaloniaProperty.RegisterAttached<ClipboardInterceptorHost, TextBox, bool>("EnableSafeClipboard");

    static ClipboardInterceptor()
    {
        EnableSafeClipboardProperty.Changed.AddClassHandler<TextBox>(OnEnableSafeClipboardChanged);
    }

    public static void SetEnableSafeClipboard(TextBox element, bool value)
    {
        element.SetValue(EnableSafeClipboardProperty, value);
    }

    public static bool GetEnableSafeClipboard(TextBox element)
    {
        return element.GetValue(EnableSafeClipboardProperty);
    }

    private static void OnEnableSafeClipboardChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        textBox.KeyDown -= OnTextBoxKeyDown;
        if (e.NewValue is true)
            textBox.KeyDown += OnTextBoxKeyDown;
    }

    private static async void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || !IsClipboardGesture(e))
            return;

        var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
        if (clipboard is null)
            return;

        try
        {
            switch (e.Key)
            {
                case Key.C when textBox.SelectedText.Length > 0:
                    await clipboard.SetTextAsync(textBox.SelectedText);
                    e.Handled = true;
                    break;
                case Key.X when !textBox.IsReadOnly && textBox.SelectedText.Length > 0:
                    await clipboard.SetTextAsync(textBox.SelectedText);
                    textBox.SelectedText = string.Empty;
                    e.Handled = true;
                    break;
                case Key.V when !textBox.IsReadOnly:
                    var text = await clipboard.TryGetTextAsync();
                    if (text is null)
                        return;
                    if (!textBox.AcceptsReturn)
                        text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                    var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
                    textBox.SelectedText = text;
                    textBox.CaretIndex = start + text.Length;
                    textBox.ClearSelection();
                    e.Handled = true;
                    break;
            }
        }
        catch
        {
            e.Handled = true;
        }
    }

    private static bool IsClipboardGesture(KeyEventArgs e)
    {
        return e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
    }

    private sealed class ClipboardInterceptorHost;
}
