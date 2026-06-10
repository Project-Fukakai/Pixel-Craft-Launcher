using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Pixel.Slices.Launch;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class LaunchHomepageView : UserControl
{
    private readonly PixelHomepageSnapshot _snapshot;
    private readonly Action<string> _openUrl;

    public LaunchHomepageView(PixelHomepageSnapshot snapshot, Action<string> openUrl)
    {
        _snapshot = snapshot;
        _openUrl = openUrl;
        Content = BuildContent();
    }

    private Control? BuildContent()
    {
        return _snapshot.Kind switch
        {
            PixelHomepageKind.None => null,
            PixelHomepageKind.Message => BuildCard(_snapshot.Title, CreateSubText(_snapshot.Message ?? string.Empty)),
            PixelHomepageKind.Image => BuildHomepageImage(),
            PixelHomepageKind.Web => BuildWebHomepage(_snapshot.Address ?? string.Empty, _snapshot.Title),
            PixelHomepageKind.Markdown => BuildCard(_snapshot.Title, new TextBlock
            {
                Text = _snapshot.Markdown ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrushes.Text,
                Margin = new Thickness(20, 36, 20, 18)
            }),
            PixelHomepageKind.Preset => BuildPresetHomepage(),
            _ => null
        };
    }

    private Control BuildHomepageImage()
    {
        try
        {
            return BuildCard(_snapshot.Title, new Image
            {
                Source = new Bitmap(_snapshot.Path ?? string.Empty),
                Stretch = Stretch.Uniform,
                MaxHeight = 420
            });
        }
        catch (Exception ex)
        {
            return BuildCard(_snapshot.Title, CreateSubText(_snapshot.GetImageLoadFailedMessage(ex)));
        }
    }

    private Control BuildPresetHomepage()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(24, 38, 24, 18),
            Spacing = 6,
            Children =
            {
                CreateBodyText(_snapshot.PresetTitle),
                CreateSubText(_snapshot.PresetDescription)
            }
        };
        return BuildCard(_snapshot.Title, panel);
    }

    private Control BuildWebHomepage(string address, string title)
    {
        var webView = CreateNativeWebView(address);
        if (webView is not null)
        {
            webView.Height = 420;
            return BuildCard(title, webView);
        }

        var open = new MyButton
        {
            Text = _snapshot.OpenExternalBrowserText,
            Height = 34,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        open.Click += (_, _) => _openUrl(address);
        return BuildCard(title, new StackPanel
        {
            Margin = new Thickness(20, 38, 20, 18),
            Spacing = 10,
            Children =
            {
                CreateSubText(_snapshot.WebViewUnavailableText),
                open
            }
        });
    }

    private static Control? CreateNativeWebView(string address)
    {
        var type = Type.GetType("Avalonia.Controls.NativeWebView, Avalonia.Controls.WebView", throwOnError: false)
                   ?? Type.GetType("Avalonia.Controls.WebView, Avalonia.Controls.WebView", throwOnError: false);
        if (type is null || Activator.CreateInstance(type) is not Control control)
            return null;

        foreach (var propertyName in new[] { "Source", "Url", "Address" })
        {
            var property = type.GetProperty(propertyName);
            if (property is null || !property.CanWrite)
                continue;

            if (property.PropertyType == typeof(Uri))
                property.SetValue(control, new Uri(address));
            else
                property.SetValue(control, address);
            return control;
        }

        return control;
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            Margin = new Thickness(0, 0, 0, 2),
            CardContent = content
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            LineHeight = 22
        };
    }

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            LineHeight = 20
        };
    }
}
