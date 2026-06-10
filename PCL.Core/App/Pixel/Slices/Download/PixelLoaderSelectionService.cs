using System.Collections.Generic;
using System.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.App.Pixel.Slices.Download;

public sealed record PixelLoaderSelectionState(
    MinecraftMergedLoaderSelection Selection,
    MinecraftLoaderKind SelectedLoaderKind,
    string SelectedLoaderVersion,
    string InstanceName);

public sealed class PixelLoaderSelectionService
{
    public PixelLoaderSelectionState SelectLoaderVersion(
        MinecraftMergedLoaderSelection current,
        MinecraftLoaderVersionEntry entry,
        string minecraftVersion,
        IReadOnlyList<PixelLoaderChoiceGroup> choiceGroups)
    {
        var selection = MinecraftLoaderCompatibility.Normalize(
            MinecraftLoaderCompatibility.SelectLoader(current, entry),
            minecraftVersion);
        var (kind, version) = ResolveSelectedLoader(selection);
        selection = AutoSelectCompatibleAddons(selection, minecraftVersion, choiceGroups);
        return CreateState(selection, minecraftVersion, kind, version);
    }

    public PixelLoaderSelectionState SelectAddon(
        MinecraftMergedLoaderSelection current,
        MinecraftAddonFileEntry entry,
        string minecraftVersion,
        MinecraftLoaderKind selectedLoaderKind,
        string selectedLoaderVersion)
    {
        var selection = MinecraftLoaderCompatibility.Normalize(ApplyAddon(current, entry), minecraftVersion);
        return CreateState(selection, minecraftVersion, selectedLoaderKind, selectedLoaderVersion);
    }

    public PixelLoaderSelectionState ClearLoader(
        MinecraftMergedLoaderSelection current,
        MinecraftLoaderKind kind,
        string minecraftVersion)
    {
        var selection = kind switch
        {
            MinecraftLoaderKind.OptiFine => current with { OptiFine = null, OptiFabric = null },
            MinecraftLoaderKind.Forge => current with { Forge = null },
            MinecraftLoaderKind.NeoForge => current with { NeoForge = null },
            MinecraftLoaderKind.Cleanroom => current with { Cleanroom = null },
            MinecraftLoaderKind.Fabric => current with { Fabric = null, FabricApi = null, OptiFabric = null },
            MinecraftLoaderKind.LegacyFabric => current with { LegacyFabric = null, LegacyFabricApi = null },
            MinecraftLoaderKind.Quilt => current with { Quilt = null, Qsl = null },
            MinecraftLoaderKind.LiteLoader => current with { LiteLoader = null },
            MinecraftLoaderKind.LabyMod => current with { LabyMod = null },
            _ => current
        };
        selection = MinecraftLoaderCompatibility.Normalize(selection, minecraftVersion);
        var (selectedKind, selectedVersion) = ResolveSelectedLoader(selection);
        return CreateState(selection, minecraftVersion, selectedKind, selectedVersion);
    }

    public PixelLoaderSelectionState ClearAddon(
        MinecraftMergedLoaderSelection current,
        MinecraftAddonKind kind,
        string minecraftVersion)
    {
        var selection = kind switch
        {
            MinecraftAddonKind.FabricApi => current with { FabricApi = null },
            MinecraftAddonKind.LegacyFabricApi => current with { LegacyFabricApi = null },
            MinecraftAddonKind.Qsl => current with { Qsl = null },
            MinecraftAddonKind.OptiFabric => current with { OptiFabric = null },
            _ => current
        };
        selection = MinecraftLoaderCompatibility.Normalize(selection, minecraftVersion);
        var (selectedKind, selectedVersion) = ResolveSelectedLoader(selection);
        return CreateState(selection, minecraftVersion, selectedKind, selectedVersion);
    }

    internal MinecraftMergedLoaderSelection Normalize(MinecraftMergedLoaderSelection selection, string? minecraftVersion)
    {
        return string.IsNullOrWhiteSpace(minecraftVersion)
            ? MinecraftLoaderCompatibility.Normalize(selection)
            : MinecraftLoaderCompatibility.Normalize(selection, minecraftVersion);
    }

    private static PixelLoaderSelectionState CreateState(
        MinecraftMergedLoaderSelection selection,
        string minecraftVersion,
        MinecraftLoaderKind selectedLoaderKind,
        string selectedLoaderVersion)
    {
        return new PixelLoaderSelectionState(
            selection,
            selectedLoaderKind,
            selectedLoaderVersion,
            MinecraftMergedInstallService.BuildDefaultInstanceName(minecraftVersion, selection));
    }

    private static MinecraftMergedLoaderSelection AutoSelectCompatibleAddons(
        MinecraftMergedLoaderSelection selection,
        string minecraftVersion,
        IReadOnlyList<PixelLoaderChoiceGroup> choiceGroups)
    {
        if (selection.Fabric is not null && selection.FabricApi is null)
            selection = SelectFirstAddon(selection, choiceGroups, MinecraftAddonKind.FabricApi);
        if (selection.LegacyFabric is not null && selection.LegacyFabricApi is null)
            selection = SelectFirstAddon(selection, choiceGroups, MinecraftAddonKind.LegacyFabricApi);
        if (selection.Quilt is not null && selection.Qsl is null)
            selection = SelectFirstAddon(selection, choiceGroups, MinecraftAddonKind.Qsl);
        if (selection.Fabric is not null && selection.OptiFine is not null && selection.OptiFabric is null)
            selection = SelectFirstAddon(selection, choiceGroups, MinecraftAddonKind.OptiFabric);

        return MinecraftLoaderCompatibility.Normalize(selection, minecraftVersion);
    }

    private static MinecraftMergedLoaderSelection SelectFirstAddon(
        MinecraftMergedLoaderSelection selection,
        IReadOnlyList<PixelLoaderChoiceGroup> choiceGroups,
        MinecraftAddonKind kind)
    {
        var addon = choiceGroups.FirstOrDefault(group => group.AddonKind == kind)?.AddonFiles.FirstOrDefault();
        return addon is null ? selection : ApplyAddon(selection, addon);
    }

    private static MinecraftMergedLoaderSelection ApplyAddon(
        MinecraftMergedLoaderSelection selection,
        MinecraftAddonFileEntry entry)
    {
        return entry.Kind switch
        {
            MinecraftAddonKind.FabricApi => selection with { FabricApi = entry },
            MinecraftAddonKind.LegacyFabricApi => selection with { LegacyFabricApi = entry },
            MinecraftAddonKind.Qsl => selection with { Qsl = entry, FabricApi = null },
            MinecraftAddonKind.OptiFabric => selection with { OptiFabric = entry },
            _ => selection
        };
    }

    private static (MinecraftLoaderKind Kind, string Version) ResolveSelectedLoader(MinecraftMergedLoaderSelection selection)
    {
        var loaders = selection.Loaders.ToArray();
        if (loaders.Length == 0)
            return (MinecraftLoaderKind.Vanilla, string.Empty);

        var loader = loaders[^1];
        return (loader.Kind, loader.Version);
    }
}
