using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelSetupNavigationServiceTest
{
    private object? _oldSection;
    private object? _oldHiddenLaunch;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _oldSection = PixelSettingsBinder.LoadValue("PixelSetupSelectedSection");
        _oldHiddenLaunch = PixelSettingsBinder.LoadValue("UiHiddenSetupLaunch");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_oldSection is not null)
            PixelSettingsBinder.SetValue("PixelSetupSelectedSection", _oldSection);
        if (_oldHiddenLaunch is not null)
            PixelSettingsBinder.SetValue("UiHiddenSetupLaunch", _oldHiddenLaunch);
    }

    [TestMethod]
    public void SelectSectionPersistsSetupSelection()
    {
        var service = new PixelSetupNavigationService();

        service.SelectSection(PixelSettingSectionKind.Java);

        Assert.AreEqual(PixelSettingSectionKind.Java, service.GetSelectedSection());
    }

    [TestMethod]
    public void HiddenSelectedSectionFallsBackToVisibleSection()
    {
        var service = new PixelSetupNavigationService();
        service.SelectSection(PixelSettingSectionKind.Launch);
        PixelSettingsBinder.SetValue("UiHiddenSetupLaunch", true);

        var selected = service.GetSelectedSection();

        Assert.AreNotEqual(PixelSettingSectionKind.Launch, selected);
        Assert.IsTrue(FeatureVisibilityService.IsSetupSectionVisible(selected));
    }

    [TestMethod]
    public void ResetSectionReturnsSectionDescriptor()
    {
        var service = new PixelSetupNavigationService();

        var section = service.ResetSection(PixelSettingSectionKind.Java);

        Assert.AreEqual(PixelSettingSectionKind.Java, section.Kind);
        Assert.IsFalse(string.IsNullOrWhiteSpace(section.Title));
    }

    [TestMethod]
    public void AboutPageSnapshotExposesUiTextAndLinks()
    {
        var service = new PixelSetupNavigationService();

        var snapshot = service.GetAboutPageSnapshot();

        Assert.AreEqual("Pixel", snapshot.BrandPrimaryText);
        Assert.AreEqual(" Craft Launcher", snapshot.BrandRestText);
        Assert.AreEqual("Pixel Craft Launcher 是基于 PCL CE 二次开发的跨平台 Minecraft 启动器", snapshot.IntroductionText);
        Assert.AreEqual("仓库地址", snapshot.RepositorySectionTitle);
        Assert.AreEqual("https://github.com/Project-Fukakai/Pixel-Craft-Launcher", snapshot.RepositoryUrl);
        Assert.AreEqual("打开仓库", snapshot.OpenRepositoryButtonText);
        Assert.AreEqual("提交反馈", snapshot.FeedbackButtonText);
        Assert.AreEqual("开源库", snapshot.LicenseSectionTitle);
        Assert.AreEqual("未记录第三方开源库。", snapshot.EmptyLicensesText);
        Assert.AreEqual("主页", snapshot.LicenseHomeButtonText);
        Assert.AreEqual("许可", snapshot.LicenseTextButtonText);
        Assert.AreEqual("页面入口", snapshot.ShortcutsSectionTitle);
        Assert.AreEqual("更新", snapshot.UpdateShortcutButtonText);
        Assert.AreEqual("反馈", snapshot.FeedbackShortcutButtonText);
        Assert.AreEqual("日志", snapshot.LogShortcutButtonText);
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.VersionName));
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.CommitDigest));
        Assert.AreEqual($"版本 {snapshot.VersionName} / {snapshot.CommitDigest}", snapshot.GetVersionLabel());
        Assert.IsNotNull(snapshot.Licenses);
        Assert.AreEqual(snapshot.Licenses.Count > 0, snapshot.HasLicenses);
    }

    [TestMethod]
    public void SidebarSnapshotGroupsVisibleSections()
    {
        var service = new PixelSetupNavigationService();
        PixelSettingsBinder.SetValue("UiHiddenSetupLaunch", true);

        var snapshot = service.GetSidebarSnapshot(PixelSettingSectionKind.Java);

        Assert.AreEqual("初始化本页设置", snapshot.ResetButtonTooltip);
        Assert.AreEqual(3, snapshot.Groups.Count);
        Assert.AreEqual("游戏", snapshot.Groups[0].Title);
        Assert.IsFalse(snapshot.Groups[0].Items.Any(static item => item.Section == PixelSettingSectionKind.Launch));
        Assert.IsTrue(snapshot.Groups[0].HasItems);
        Assert.AreEqual(PixelSettingSectionKind.Java, snapshot.Groups[0].Items[0].Section);
        Assert.IsTrue(snapshot.Groups[0].Items[0].IsSelected);
        Assert.AreEqual("工具", snapshot.Groups[1].Title);
        Assert.AreEqual(PixelSettingSectionKind.GameLink, snapshot.Groups[1].Items[0].Section);
        Assert.AreEqual("启动器", snapshot.Groups[2].Title);
        Assert.IsTrue(snapshot.Groups[2].Items.Any(static item => item.Section == PixelSettingSectionKind.About));
    }

    [TestMethod]
    public void OpenExternalUrlFailedMessageComesFromCore()
    {
        var service = new PixelSetupNavigationService();
        var messages = service.GetMessages();
        var exception = new System.InvalidOperationException("boom");

        Assert.AreEqual("打开链接失败：boom", messages.GetOpenExternalUrlFailedMessage(exception));
        Assert.AreEqual(
            messages.GetOpenExternalUrlFailedMessage(exception),
            PixelSetupNavigationService.GetOpenExternalUrlFailedMessage(exception));
        Assert.AreEqual(
            "Java 设置已初始化。",
            messages.GetSectionResetMessage(PixelSettingsCatalog.Get(PixelSettingSectionKind.Java)));
    }
}
