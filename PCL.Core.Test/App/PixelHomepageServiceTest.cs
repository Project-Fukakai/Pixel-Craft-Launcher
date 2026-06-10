using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;
using PCL.Core.App.Pixel.Slices.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelHomepageServiceTest
{
    private object? _type;
    private object? _pathOrUrl;
    private object? _preset;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _type = PixelSettingsBinder.LoadValue("UiCustomType");
        _pathOrUrl = PixelSettingsBinder.LoadValue("UiCustomNet");
        _preset = PixelSettingsBinder.LoadValue("UiCustomPreset");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Restore("UiCustomType", _type);
        Restore("UiCustomNet", _pathOrUrl);
        Restore("UiCustomPreset", _preset);
    }

    [TestMethod]
    public void BlankHomepageReturnsNone()
    {
        PixelSettingsBinder.SetValue("UiCustomType", 0);

        var snapshot = new PixelHomepageService().GetSnapshot();

        Assert.AreEqual(PixelHomepageKind.None, snapshot.Kind);
    }

    [TestMethod]
    public void NetworkHomepageRejectsNonHttpUrl()
    {
        PixelSettingsBinder.SetValue("UiCustomType", 2);
        PixelSettingsBinder.SetValue("UiCustomNet", "file:///tmp/index.html");

        var snapshot = new PixelHomepageService().GetSnapshot();

        Assert.AreEqual(PixelHomepageKind.Message, snapshot.Kind);
        Assert.AreEqual("联网主页", snapshot.Title);
    }

    [TestMethod]
    public void LocalMarkdownHomepageReadsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-homepage", Guid.NewGuid().ToString("N") + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# Hello Pixel");
        PixelSettingsBinder.SetValue("UiCustomType", 1);
        PixelSettingsBinder.SetValue("UiCustomNet", path);

        var snapshot = new PixelHomepageService().GetSnapshot();

        Assert.AreEqual(PixelHomepageKind.Markdown, snapshot.Kind);
        Assert.AreEqual("# Hello Pixel", snapshot.Markdown);
    }

    [TestMethod]
    public void PresetHomepageReturnsPresetNumber()
    {
        PixelSettingsBinder.SetValue("UiCustomType", 3);
        PixelSettingsBinder.SetValue("UiCustomPreset", 7);

        var snapshot = new PixelHomepageService().GetSnapshot();

        Assert.AreEqual(PixelHomepageKind.Preset, snapshot.Kind);
        Assert.AreEqual(7, snapshot.Preset);
        Assert.AreEqual("预设主页 7", snapshot.PresetTitle);
        Assert.AreEqual("预设主页框架已接入，后续可将 Plain 的预设内容映射到这里。", snapshot.PresetDescription);
        Assert.AreEqual("图片加载失败：", snapshot.ImageLoadFailedPrefix);
        Assert.AreEqual("图片加载失败：boom", snapshot.GetImageLoadFailedMessage(new InvalidOperationException("boom")));
        Assert.AreEqual("当前平台的 WebView 引擎不可用。", snapshot.WebViewUnavailableText);
        Assert.AreEqual("打开外部浏览器", snapshot.OpenExternalBrowserText);
    }

    private static void Restore(string key, object? value)
    {
        if (value is not null)
            PixelSettingsBinder.SetValue(key, value);
    }
}
