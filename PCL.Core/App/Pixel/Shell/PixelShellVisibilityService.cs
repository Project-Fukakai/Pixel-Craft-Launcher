namespace PCL.Core.App.Pixel.Shell;

public sealed record PixelMainPageVisibilitySnapshot(
    bool IsDownloadVisible,
    bool IsSetupVisible,
    bool IsToolsVisible)
{
    public bool IsVisible(MainPageKind page) =>
        page switch
        {
            MainPageKind.Download => IsDownloadVisible,
            MainPageKind.Setup => IsSetupVisible,
            MainPageKind.Tools => IsToolsVisible,
            _ => true
        };
}

public sealed record PixelToolHiddenMessageSnapshot(string Title, string Message);

public sealed class PixelShellVisibilityService
{
    public PixelMainPageVisibilitySnapshot GetMainPageVisibility()
    {
        return new PixelMainPageVisibilitySnapshot(
            FeatureVisibilityService.IsMainPageVisible(MainPageKind.Download),
            FeatureVisibilityService.IsMainPageVisible(MainPageKind.Setup),
            FeatureVisibilityService.IsMainPageVisible(MainPageKind.Tools));
    }

    public bool IsMainPageVisible(MainPageKind page)
    {
        return GetMainPageVisibility().IsVisible(page);
    }

    public bool IsSetupSectionVisible(PixelSettingSectionKind section)
    {
        return FeatureVisibilityService.IsSetupSectionVisible(section);
    }

    public bool IsToolVisible(PixelToolFeature tool)
    {
        return FeatureVisibilityService.IsToolVisible(tool);
    }

    public PixelToolHiddenMessageSnapshot GetToolHiddenMessage(PixelToolFeature tool)
    {
        return tool switch
        {
            PixelToolFeature.GameLink => new PixelToolHiddenMessageSnapshot("工具", "联机工具入口已隐藏。"),
            PixelToolFeature.Test => new PixelToolHiddenMessageSnapshot("工具", "控件验收入口已隐藏。"),
            _ => new PixelToolHiddenMessageSnapshot("工具", "工具入口已隐藏。")
        };
    }
}
