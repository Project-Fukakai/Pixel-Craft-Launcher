namespace PCL.Core.App.Pixel.Slices.Download;

public enum PixelDownloadCategoryRefreshAction
{
    None,
    RefreshVersions,
    RefreshLoaderChoices
}

public sealed class PixelDownloadCategoryRefreshService
{
    public PixelDownloadCategoryRefreshAction GetRefreshAction(int tag, bool hasSelectedVersion)
    {
        if (tag is 1 or 9)
            return PixelDownloadCategoryRefreshAction.RefreshVersions;

        if (tag is >= 10 and <= 18)
            return hasSelectedVersion
                ? PixelDownloadCategoryRefreshAction.RefreshLoaderChoices
                : PixelDownloadCategoryRefreshAction.RefreshVersions;

        return PixelDownloadCategoryRefreshAction.None;
    }
}
