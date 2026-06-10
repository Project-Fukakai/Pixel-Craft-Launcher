using System.IO;
using PCL.Core.App.Pixel;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed class PixelHomepageService
{
    public PixelHomepageSnapshot GetSnapshot()
    {
        var type = PixelSettingsBinder.LoadValue("UiCustomType") is int value ? value : 0;
        return type switch
        {
            0 => PixelHomepageSnapshot.None(),
            1 => GetLocalSnapshot(),
            2 => GetNetworkSnapshot(),
            3 => GetPresetSnapshot(),
            _ => PixelHomepageSnapshot.None()
        };
    }

    private static PixelHomepageSnapshot GetLocalSnapshot()
    {
        var path = PixelSettingsBinder.LoadValue("UiCustomNet")?.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return PixelHomepageSnapshot.ForMessage("自定义主页", "未设置本地主页文件路径。");

        path = Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(path))
            return PixelHomepageSnapshot.ForMessage("自定义主页", "找不到本地主页文件：" + path);

        var extension = Path.GetExtension(path);
        if (IsImageExtension(extension))
            return PixelHomepageSnapshot.ForImage("自定义主页", path);

        if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return PixelHomepageSnapshot.ForWeb("本地主页", new Uri(path).AbsoluteUri);
        }

        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return PixelHomepageSnapshot.ForMarkdown("自定义主页", File.ReadAllText(path));
        }

        return PixelHomepageSnapshot.ForMessage("自定义主页", "暂不支持该本地主页格式：" + extension);
    }

    private static PixelHomepageSnapshot GetNetworkSnapshot()
    {
        var url = PixelSettingsBinder.LoadValue("UiCustomNet")?.ToString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return PixelHomepageSnapshot.ForMessage("联网主页", "请输入有效的 http 或 https 地址。");
        }

        return PixelHomepageSnapshot.ForWeb("联网主页", uri.AbsoluteUri);
    }

    private static PixelHomepageSnapshot GetPresetSnapshot()
    {
        var preset = PixelSettingsBinder.LoadValue("UiCustomPreset") is IConvertible raw ? Convert.ToInt32(raw) : 14;
        return PixelHomepageSnapshot.ForPreset("自定义主页", preset);
    }

    private static bool IsImageExtension(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);
}

public enum PixelHomepageKind
{
    None,
    Message,
    Image,
    Web,
    Markdown,
    Preset
}

public sealed record PixelHomepageSnapshot(
    PixelHomepageKind Kind,
    string Title,
    string? Message = null,
    string? Path = null,
    string? Address = null,
    string? Markdown = null,
    int Preset = 0,
    string PresetTitle = "",
    string PresetDescription = "",
    string ImageLoadFailedPrefix = "图片加载失败：",
    string WebViewUnavailableText = "当前平台的 WebView 引擎不可用。",
    string OpenExternalBrowserText = "打开外部浏览器")
{
    public static PixelHomepageSnapshot None() => new(PixelHomepageKind.None, string.Empty);

    public static PixelHomepageSnapshot ForMessage(string title, string message) =>
        new(PixelHomepageKind.Message, title, Message: message);

    public static PixelHomepageSnapshot ForImage(string title, string path) =>
        new(PixelHomepageKind.Image, title, Path: path);

    public static PixelHomepageSnapshot ForWeb(string title, string address) =>
        new(PixelHomepageKind.Web, title, Address: address);

    public static PixelHomepageSnapshot ForMarkdown(string title, string markdown) =>
        new(PixelHomepageKind.Markdown, title, Markdown: markdown);

    public static PixelHomepageSnapshot ForPreset(string title, int preset) =>
        new(
            PixelHomepageKind.Preset,
            title,
            Preset: preset,
            PresetTitle: "预设主页 " + preset,
            PresetDescription: "预设主页框架已接入，后续可将 Plain 的预设内容映射到这里。");

    public string GetImageLoadFailedMessage(Exception exception) =>
        ImageLoadFailedPrefix + exception.Message;
}
