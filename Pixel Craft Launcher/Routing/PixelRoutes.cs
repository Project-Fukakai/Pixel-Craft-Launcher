using System.Collections.Generic;
using PCL.Core.App.Pixel;

namespace Pixel_Craft_Launcher.Routing;

public static class PixelRoutes
{
    public static RouteNode Launch() => new("launch");

    public static RouteNode LaunchInstances() => new("launch", Child: new RouteNode("instances"));

    public static RouteNode Tools() => new("tools");

    public static RouteNode DownloadMinecraft() => new("download", Child: new RouteNode("minecraft"));

    public static RouteNode DownloadMinecraftInstall(string versionId) => new(
        "download",
        Child: new RouteNode(
            "minecraft",
            Child: new RouteNode(
                "install",
                new Dictionary<string, string> { ["version"] = versionId })));

    public static RouteNode DownloadTaskDetails(string? taskId = null) => new(
        "secondary",
        string.IsNullOrWhiteSpace(taskId)
            ? new Dictionary<string, string> { ["kind"] = "download-tasks" }
            : new Dictionary<string, string> { ["kind"] = "download-tasks", ["task"] = taskId });

    public static RouteNode ProfileManager(string? action = null, string? serverId = null)
    {
        var parameters = new Dictionary<string, string> { ["kind"] = "profiles" };
        if (!string.IsNullOrWhiteSpace(action))
            parameters["action"] = action;
        if (!string.IsNullOrWhiteSpace(serverId))
            parameters["server"] = serverId;
        return new RouteNode("secondary", parameters);
    }

    public static RouteNode DownloadCategory(int category) => new(
        "download",
        Child: new RouteNode(
            category switch
            {
                2 => "mod",
                3 => "modpack",
                4 => "datapack",
                5 => "resourcepack",
                6 => "shaderpack",
                7 => "world",
                8 => "favorites",
                9 => "client",
                10 => "optifine",
                11 => "forge",
                12 => "neoforge",
                13 => "cleanroom",
                14 => "fabric",
                15 => "quilt",
                16 => "liteloader",
                17 => "labymod",
                18 => "legacyfabric",
                _ => "minecraft"
            },
            new Dictionary<string, string> { ["category"] = category.ToString() }));

    public static RouteNode Setup(PixelSettingSectionKind section) => new(
        "setup",
        Child: new RouteNode(section.ToString().ToLowerInvariant()));
}
