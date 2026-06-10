using System.Collections.Generic;
using System.Linq;
using PCL.Core.App;

namespace PCL.Core.App.Pixel;

public sealed class PixelSetupNavigationService
{
    private const string SelectedSectionKey = "PixelSetupSelectedSection";

    public PixelSettingSectionKind GetSelectedSection()
    {
        var selected = TryLoadSelectedSection() ?? PixelSettingSectionKind.Launch;
        if (FeatureVisibilityService.IsSetupSectionVisible(selected))
            return selected;

        return PixelSettingsCatalog.Sections
            .Select(static section => section.Kind)
            .FirstOrDefault(FeatureVisibilityService.IsSetupSectionVisible);
    }

    public void SelectSection(PixelSettingSectionKind section)
    {
        PixelSettingsBinder.SetValue(SelectedSectionKey, section.ToString());
    }

    public PixelSettingSection ResetSection(PixelSettingSectionKind section)
    {
        var descriptor = PixelSettingsCatalog.Get(section);
        PixelSettingsBinder.ResetSection(descriptor);
        return descriptor;
    }

    public PixelSetupSidebarSnapshot GetSidebarSnapshot(PixelSettingSectionKind selectedSection)
    {
        return new PixelSetupSidebarSnapshot(
            "初始化本页设置",
        [
            BuildSidebarGroup("游戏", selectedSection,
            [
                PixelSettingSectionKind.Launch,
                PixelSettingSectionKind.Java,
                PixelSettingSectionKind.GameManage
            ]),
            BuildSidebarGroup("工具", selectedSection,
            [
                PixelSettingSectionKind.GameLink
            ]),
            BuildSidebarGroup("启动器", selectedSection,
            [
                PixelSettingSectionKind.Ui,
                PixelSettingSectionKind.LauncherMisc,
                PixelSettingSectionKind.About,
                PixelSettingSectionKind.Update,
                PixelSettingSectionKind.Feedback,
                PixelSettingSectionKind.Log
            ])
        ]);
    }

    public PixelAboutPageSnapshot GetAboutPageSnapshot()
    {
        return new PixelAboutPageSnapshot(
            "Pixel",
            " Craft Launcher",
            "Pixel Craft Launcher 是基于 PCL CE 二次开发的跨平台 Minecraft 启动器",
            "仓库地址",
            "https://github.com/Project-Fukakai/Pixel-Craft-Launcher",
            "打开仓库",
            "提交反馈",
            "开源库",
            "未记录第三方开源库。",
            "主页",
            "许可",
            "页面入口",
            "更新",
            "反馈",
            "日志",
            "版本",
            Basics.VersionName,
            Basics.Metadata.Version.CommitDigest,
            Basics.Metadata.Licenses
                .Select(static license => new PixelAboutLicenseSnapshot(
                    license.Name,
                    license.Information,
                    license.WebsiteUri,
                    license.LicenseUri))
                .ToArray());
    }

    public PixelSetupNavigationMessages GetMessages()
    {
        return new PixelSetupNavigationMessages(
            "打开链接失败：{0}",
            "{0} 设置已初始化。");
    }

    public static string GetOpenExternalUrlFailedMessage(Exception exception) =>
        new PixelSetupNavigationService().GetMessages().GetOpenExternalUrlFailedMessage(exception);

    public static string GetSectionResetMessage(PixelSettingSection section) =>
        new PixelSetupNavigationService().GetMessages().GetSectionResetMessage(section);

    private static PixelSetupSidebarGroupSnapshot BuildSidebarGroup(
        string title,
        PixelSettingSectionKind selectedSection,
        IReadOnlyList<PixelSettingSectionKind> sections)
    {
        return new PixelSetupSidebarGroupSnapshot(
            title,
            sections
                .Where(FeatureVisibilityService.IsSetupSectionVisible)
                .Select(section => BuildSidebarItem(section, selectedSection))
                .ToArray());
    }

    private static PixelSetupSidebarItemSnapshot BuildSidebarItem(
        PixelSettingSectionKind section,
        PixelSettingSectionKind selectedSection)
    {
        var descriptor = PixelSettingsCatalog.Get(section);
        return new PixelSetupSidebarItemSnapshot(
            section,
            descriptor.Title,
            descriptor.Info,
            descriptor.Icon,
            selectedSection == section);
    }

    private static PixelSettingSectionKind? TryLoadSelectedSection()
    {
        try
        {
            return PixelSettingsBinder.LoadValue(SelectedSectionKey) is string raw &&
                   Enum.TryParse(raw, out PixelSettingSectionKind section)
                ? section
                : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record PixelAboutPageSnapshot(
    string BrandPrimaryText,
    string BrandRestText,
    string IntroductionText,
    string RepositorySectionTitle,
    string RepositoryUrl,
    string OpenRepositoryButtonText,
    string FeedbackButtonText,
    string LicenseSectionTitle,
    string EmptyLicensesText,
    string LicenseHomeButtonText,
    string LicenseTextButtonText,
    string ShortcutsSectionTitle,
    string UpdateShortcutButtonText,
    string FeedbackShortcutButtonText,
    string LogShortcutButtonText,
    string VersionLabelPrefix,
    string VersionName,
    string CommitDigest,
    IReadOnlyList<PixelAboutLicenseSnapshot> Licenses)
{
    public bool HasLicenses => Licenses.Count > 0;

    public string GetVersionLabel() =>
        $"{VersionLabelPrefix} {VersionName} / {CommitDigest}";
}

public sealed record PixelAboutLicenseSnapshot(
    string Name,
    string Information,
    string? WebsiteUri,
    string? LicenseUri);

public sealed record PixelSetupNavigationMessages(
    string OpenExternalUrlFailedMessageFormat,
    string SectionResetMessageFormat)
{
    public string GetOpenExternalUrlFailedMessage(Exception exception) =>
        string.Format(OpenExternalUrlFailedMessageFormat, exception.Message);

    public string GetSectionResetMessage(PixelSettingSection section) =>
        string.Format(SectionResetMessageFormat, section.Title);
}

public sealed record PixelSetupSidebarSnapshot(
    string ResetButtonTooltip,
    IReadOnlyList<PixelSetupSidebarGroupSnapshot> Groups);

public sealed record PixelSetupSidebarGroupSnapshot(
    string Title,
    IReadOnlyList<PixelSetupSidebarItemSnapshot> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record PixelSetupSidebarItemSnapshot(
    PixelSettingSectionKind Section,
    string Title,
    string Info,
    string Icon,
    bool IsSelected);
