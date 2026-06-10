using System.Collections.Generic;
using System.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.App.Pixel.Slices.Download;

public sealed class PixelLoaderChoiceService(MinecraftModLoaderCatalogService catalogService)
{
    private readonly MinecraftModLoaderCatalogService _catalogService = catalogService;

    public async Task<IReadOnlyList<PixelLoaderChoiceGroup>> RefreshAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var groups = new List<PixelLoaderChoiceGroup>();
        foreach (var item in GetPcl2InstallChoiceOrder())
        {
            if (item.LoaderKind is { } kind)
            {
                var option = MinecraftLoaderCatalog.GetOptions(minecraftVersion).FirstOrDefault(option => option.Kind == kind);
                if (option is null)
                    continue;

                groups.Add(option.IsAvailable
                    ? await BuildLoaderGroupAsync(kind, minecraftVersion, option, cancellationToken).ConfigureAwait(false)
                    : new PixelLoaderChoiceGroup(
                        option.DisplayName,
                        option.Description,
                        option.Icon,
                        kind,
                        null,
                        [],
                        [],
                        option.StatusText,
                        false));
            }
            else if (item.AddonKind is { } addon)
            {
                groups.Add(await BuildAddonGroupAsync(addon, minecraftVersion, cancellationToken).ConfigureAwait(false));
            }
        }

        return groups;
    }

    private async Task<PixelLoaderChoiceGroup> BuildLoaderGroupAsync(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        MinecraftLoaderOption option,
        CancellationToken cancellationToken)
    {
        try
        {
            var versions = await _catalogService.GetLoaderVersionsAsync(kind, minecraftVersion, cancellationToken).ConfigureAwait(false);
            return new PixelLoaderChoiceGroup(
                option.DisplayName,
                option.Description,
                option.Icon,
                kind,
                null,
                versions.Take(40).ToArray(),
                [],
                versions.Count == 0 ? "无可用版本" : "可以添加",
                versions.Count != 0);
        }
        catch (Exception ex)
        {
            return new PixelLoaderChoiceGroup(option.DisplayName, option.Description, option.Icon, kind, null, [], [], "获取失败：" + ex.Message, false);
        }
    }

    private async Task<PixelLoaderChoiceGroup> BuildAddonGroupAsync(
        MinecraftAddonKind kind,
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var (title, description, icon, parent) = kind switch
        {
            MinecraftAddonKind.FabricApi => ("Fabric API", "Fabric / Quilt 常用 API；Fabric 安装后推荐添加。", "Assets/Blocks/Fabric.png", (MinecraftLoaderKind?)null),
            MinecraftAddonKind.LegacyFabricApi => ("Legacy Fabric API", "Legacy Fabric 常用 API；旧版本 Fabric 安装后推荐添加。", "Assets/Blocks/Fabric.png", MinecraftLoaderKind.LegacyFabric),
            MinecraftAddonKind.Qsl => ("QFAPI / QSL", "Quilt 常用 API；Quilt 安装后推荐添加。", "Assets/Blocks/Quilt.png", MinecraftLoaderKind.Quilt),
            MinecraftAddonKind.OptiFabric => ("OptiFabric", "让 Fabric 与 OptiFine 一起工作。", "Assets/Blocks/OptiFabric.png", MinecraftLoaderKind.Fabric),
            _ => ("附加 Mod", "", "mdi-package-variant", MinecraftLoaderKind.Vanilla)
        };
        try
        {
            var files = await _catalogService.GetAddonFilesAsync(kind, cancellationToken).ConfigureAwait(false);
            var compatible = files.Where(file => MinecraftModLoaderCatalogService.IsAddonCompatible(file, minecraftVersion, parent)).Take(40).ToArray();
            return new PixelLoaderChoiceGroup(title, description, icon, null, kind, [], compatible, compatible.Length == 0 ? "无可用版本" : "可以添加", compatible.Length != 0);
        }
        catch (Exception ex)
        {
            return new PixelLoaderChoiceGroup(title, description, icon, null, kind, [], [], "获取失败：" + ex.Message, false);
        }
    }

    private static IReadOnlyList<(MinecraftLoaderKind? LoaderKind, MinecraftAddonKind? AddonKind)> GetPcl2InstallChoiceOrder() =>
    [
        (MinecraftLoaderKind.Cleanroom, null),
        (MinecraftLoaderKind.NeoForge, null),
        (MinecraftLoaderKind.Forge, null),
        (MinecraftLoaderKind.Fabric, null),
        (null, MinecraftAddonKind.FabricApi),
        (MinecraftLoaderKind.LegacyFabric, null),
        (null, MinecraftAddonKind.LegacyFabricApi),
        (MinecraftLoaderKind.Quilt, null),
        (null, MinecraftAddonKind.Qsl),
        (MinecraftLoaderKind.LabyMod, null),
        (MinecraftLoaderKind.OptiFine, null),
        (null, MinecraftAddonKind.OptiFabric),
        (MinecraftLoaderKind.LiteLoader, null)
    ];
}
