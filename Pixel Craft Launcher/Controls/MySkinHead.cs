using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pixel_Craft_Launcher.Controls;

public class MySkinHead : Control
{
    private static readonly string SkinResourceBase =
        $"avares://{Uri.EscapeDataString(typeof(MySkinHead).Assembly.GetName().Name ?? "Pixel Craft Launcher")}/Assets/Skins/";

    public static readonly StyledProperty<string?> SkinNameProperty =
        AvaloniaProperty.Register<MySkinHead, string?>(nameof(SkinName), "Steve");

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<MySkinHead, double>(nameof(Size), 84d);

    private Bitmap? _skin;
    private string? _loadedSkinName;

    static MySkinHead()
    {
        AffectsMeasure<MySkinHead>(SizeProperty);
        AffectsRender<MySkinHead>(SkinNameProperty, SizeProperty);
    }

    public MySkinHead()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public string? SkinName
    {
        get => GetValue(SkinNameProperty);
        set => SetValue(SkinNameProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Max(0d, Size);
        return new Size(size, size);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var skin = GetSkin();
        if (skin is null)
            return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;

        var offsetX = (Bounds.Width - size) / 2d;
        var offsetY = (Bounds.Height - size) / 2d;
        var face = new Rect(8, 8, 8, 8);
        var hat = new Rect(40, 8, 8, 8);
        context.DrawImage(skin, face, new Rect(offsetX + size * 4d / 56d, offsetY + size * 4d / 56d, size * 48d / 56d, size * 48d / 56d));
        context.DrawImage(skin, hat, new Rect(offsetX, offsetY, size, size));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SkinNameProperty)
        {
            _skin?.Dispose();
            _skin = null;
            _loadedSkinName = null;
        }
    }

    private Bitmap? GetSkin()
    {
        var skinName = NormalizeSkinName(SkinName);
        if (_skin is not null && string.Equals(_loadedSkinName, skinName, StringComparison.Ordinal))
            return _skin;

        _skin?.Dispose();
        _skin = null;
        _loadedSkinName = skinName;

        try
        {
            using var stream = OpenSkin(skinName);
            _skin = new Bitmap(stream);
        }
        catch
        {
            if (skinName == "Steve")
                return null;

            _loadedSkinName = "Steve";
            using var stream = OpenSkin("Steve");
            _skin = new Bitmap(stream);
        }

        return _skin;
    }

    private static System.IO.Stream OpenSkin(string skinName)
    {
        return AssetLoader.Open(new Uri(SkinResourceBase + skinName + ".png"));
    }

    private static string NormalizeSkinName(string? skinName)
    {
        if (string.IsNullOrWhiteSpace(skinName))
            return "Steve";

        return skinName.Trim() switch
        {
            "Alex" => "Alex",
            "Ari" => "Ari",
            "Efe" => "Efe",
            "Kai" => "Kai",
            "Makena" => "Makena",
            "Noor" => "Noor",
            "Steve" => "Steve",
            "Sunny" => "Sunny",
            "Zuri" => "Zuri",
            _ => "Steve"
        };
    }
}
