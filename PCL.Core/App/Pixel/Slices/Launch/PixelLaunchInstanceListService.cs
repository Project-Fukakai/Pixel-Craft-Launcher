using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Pixel.Slices.Launch;

public sealed record PixelLaunchInstanceItemSnapshot(
    string Name,
    string Info,
    string Icon,
    string Type,
    string VersionDirectory,
    string MinecraftFolder,
    bool IsSelected);

public sealed record PixelLaunchInstanceGroupSnapshot(
    string Title,
    string Type,
    IReadOnlyList<PixelLaunchInstanceItemSnapshot> Instances);

public sealed record PixelLaunchInstanceSelectionPageSnapshot(
    IReadOnlyList<PixelLaunchInstanceGroupSnapshot> Groups,
    bool IsEmpty);

public sealed class PixelLaunchInstanceListService
{
    internal PixelLaunchInstanceSelectionPageSnapshot GetSelectionPageSnapshot(
        IEnumerable<MinecraftInstanceInfo> instances,
        string? selectedFolder,
        string? selectedInstancePath)
    {
        var groups = GetGroups(instances, selectedFolder, selectedInstancePath);
        return new PixelLaunchInstanceSelectionPageSnapshot(
            groups,
            groups.All(static group => group.Instances.Count == 0));
    }

    internal IReadOnlyList<PixelLaunchInstanceGroupSnapshot> GetGroups(
        IEnumerable<MinecraftInstanceInfo> instances,
        string? selectedFolder,
        string? selectedInstancePath)
    {
        var visibleInstances = GetVisibleInstances(instances, selectedFolder);
        return visibleInstances
            .GroupBy(GetLaunchInstanceType)
            .OrderByDescending(group => group.Key == "release")
            .ThenBy(group => group.Key)
            .Select(group => new PixelLaunchInstanceGroupSnapshot(
                GetLaunchInstanceGroupTitle(group.Key),
                group.Key,
                group
                    .OrderByDescending(instance => instance.ReleaseTime)
                    .ThenBy(instance => instance.Name)
                    .Select(instance => BuildSnapshot(instance, selectedInstancePath))
                    .ToArray()))
            .ToArray();
    }

    internal IReadOnlyList<PixelLaunchInstanceItemSnapshot> GetVisibleInstances(
        IEnumerable<MinecraftInstanceInfo> instances,
        string? selectedFolder,
        string? selectedInstancePath)
    {
        return GetVisibleInstances(instances, selectedFolder)
            .OrderByDescending(instance => instance.ReleaseTime)
            .ThenBy(instance => instance.Name)
            .Select(instance => BuildSnapshot(instance, selectedInstancePath))
            .ToArray();
    }

    internal static string GetLaunchInstanceType(MinecraftInstanceInfo instance)
    {
        return GetString(instance.Json, "type") ?? "release";
    }

    public static string GetLaunchInstanceGroupTitle(string type) =>
        type switch
        {
            "release" => "正式版",
            "snapshot" => "快照版",
            "old_beta" => "远古 Beta",
            "old_alpha" => "远古 Alpha",
            _ => type
        };

    private static IReadOnlyList<MinecraftInstanceInfo> GetVisibleInstances(
        IEnumerable<MinecraftInstanceInfo> instances,
        string? selectedFolder)
    {
        if (string.IsNullOrWhiteSpace(selectedFolder))
            return instances.ToArray();

        return instances
            .Where(instance => string.Equals(instance.MinecraftFolder, selectedFolder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static PixelLaunchInstanceItemSnapshot BuildSnapshot(
        MinecraftInstanceInfo instance,
        string? selectedInstancePath)
    {
        var type = GetLaunchInstanceType(instance);
        var version = instance.VanillaVersion?.ToString() ?? type;
        var time = instance.ReleaseTime == DateTime.MinValue ? "" : " · " + instance.ReleaseTime.ToString("yyyy/MM/dd");
        return new PixelLaunchInstanceItemSnapshot(
            instance.Name,
            $"{version}{time} · {instance.VersionDirectory}",
            GetLaunchInstanceIcon(type),
            type,
            instance.VersionDirectory,
            instance.MinecraftFolder,
            string.Equals(instance.VersionDirectory, selectedInstancePath, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetLaunchInstanceIcon(string type) =>
        type switch
        {
            "snapshot" => "mdi-flask-outline",
            "old_beta" or "old_alpha" => "mdi-archive-outline",
            _ => "mdi-cube-outline"
        };

    private static string? GetString(JsonObject json, string key)
    {
        return json[key]?.GetValue<string>();
    }
}
