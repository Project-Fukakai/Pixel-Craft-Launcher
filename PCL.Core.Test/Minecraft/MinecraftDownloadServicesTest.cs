using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class MinecraftDownloadServicesTest
{
    private string _root = string.Empty;

    private sealed class InstallFixtureServer : HttpServer
    {
        public InstallFixtureServer() : base([IPAddress.Parse("127.0.0.1")])
        {
        }

        protected override void Init()
        {
            Register(HttpMethod.Get, "/vanilla.json", _ => HttpRouteResponse.Json(new JsonObject
            {
                ["id"] = "1.20.1",
                ["type"] = "release",
                ["mainClass"] = "net.minecraft.client.main.Main",
                ["releaseTime"] = "2024-01-01T00:00:00Z",
                ["libraries"] = new JsonArray()
            }).AsTask());
            Register(HttpMethod.Get, "/OptiFine.jar", _ => HttpRouteResponse.Input(CreateInstallerJar(), "application/java-archive").AsTask());
        }
    }

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcl-core-download-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch
        {
            // Best effort.
        }
    }

    [TestMethod]
    public void MavenNameToPathUsesCurrentDirectorySeparators()
    {
        var path = MinecraftResourceResolver.MavenNameToPath("com.example:demo:1.2.3:natives-windows");

        Assert.AreEqual(Path.Combine("com", "example", "demo", "1.2.3", "demo-1.2.3-natives-windows.jar"), path);
    }

    [TestMethod]
    public void RulesRespectCurrentOperatingSystem()
    {
        var current = MinecraftRuntimeRules.CurrentMinecraftOsName;
        var other = current == "windows" ? "linux" : "windows";
        var rules = new JsonArray
        {
            new JsonObject { ["action"] = "allow", ["os"] = new JsonObject { ["name"] = current } }
        };
        var denied = new JsonArray
        {
            new JsonObject { ["action"] = "allow", ["os"] = new JsonObject { ["name"] = other } }
        };

        Assert.IsTrue(MinecraftRuntimeRules.IsRuleAllowed(rules));
        Assert.IsFalse(MinecraftRuntimeRules.IsRuleAllowed(denied));
    }

    [TestMethod]
    public void RequiredFilesIncludesClientLibrariesNativesAndAssetIndex()
    {
        var instance = CreateInstance();

        var files = MinecraftResourceResolver.GetRequiredFiles(instance, includeAssets: true);

        Assert.IsTrue(files.Any(file => file.TargetPath == instance.JarPath));
        Assert.IsTrue(files.Any(file => file.TargetPath.EndsWith(Path.Combine("libraries", "com", "example", "demo", "1.0.0", "demo-1.0.0.jar"), StringComparison.Ordinal)));
        Assert.IsTrue(files.Any(file => file.TargetPath.EndsWith(Path.Combine("assets", "indexes", "1.json"), StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LoaderCatalogReportsAvailabilityForTypicalVersions()
    {
        AssertLoader("1.12.2", MinecraftLoaderKind.Cleanroom, true);
        AssertLoader("1.12.2", MinecraftLoaderKind.LegacyFabric, true);
        AssertLoader("1.13.2", MinecraftLoaderKind.LegacyFabric, true);
        AssertLoader("1.13.2", MinecraftLoaderKind.Fabric, false);
        AssertLoader("1.14.4", MinecraftLoaderKind.Fabric, true);
        AssertLoader("1.14.4", MinecraftLoaderKind.Quilt, true);
        AssertLoader("1.20.1", MinecraftLoaderKind.NeoForge, true);
        AssertLoader("1.21.10", MinecraftLoaderKind.Fabric, true);
        AssertLoader("1.21.10", MinecraftLoaderKind.LegacyFabric, false);
        AssertLoader("1.21.10", MinecraftLoaderKind.Quilt, true);
        AssertLoader("1.21.10", MinecraftLoaderKind.NeoForge, true);
        AssertLoader("26.1.0", MinecraftLoaderKind.Fabric, true);
        AssertLoader("26.1.0", MinecraftLoaderKind.LegacyFabric, false);
        AssertLoader("26.1.0", MinecraftLoaderKind.Quilt, true);
        AssertLoader("26.1.0", MinecraftLoaderKind.NeoForge, true);
        AssertLoader("26.1.0", MinecraftLoaderKind.LiteLoader, false);
    }

    [TestMethod]
    public void LoaderCatalogMarksAvailablePlainLoadersInstallSupported()
    {
        var options = MinecraftLoaderCatalog.GetOptions("1.20.1")
            .Where(option => option.IsAvailable)
            .ToArray();

        Assert.IsTrue(options.Length > 0);
        Assert.IsTrue(options.All(option => option.IsInstallSupported));
        Assert.IsFalse(options.Any(option => option.StatusText.Contains("暂未启用", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LoaderCatalogBuildsDefaultInstanceNames()
    {
        Assert.AreEqual(
            "1.20.1",
            MinecraftLoaderCatalog.BuildDefaultInstanceName("1.20.1", new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla)));
        Assert.AreEqual(
            "1.20.1-fabric",
            MinecraftLoaderCatalog.BuildDefaultInstanceName("1.20.1", new MinecraftLoaderSelection(MinecraftLoaderKind.Fabric)));
        Assert.AreEqual(
            "1.20.1-quilt-0.26.0",
            MinecraftLoaderCatalog.BuildDefaultInstanceName("1.20.1", new MinecraftLoaderSelection(MinecraftLoaderKind.Quilt, "0.26.0")));
    }

    [TestMethod]
    public void MergedInstallBuildsPlainStyleInstanceNames()
    {
        var selection = new MinecraftMergedLoaderSelection(
            Fabric: new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Fabric, "0.16.14", "0.16.14", "1.20.1", true, false, MinecraftRemoteSource.Official),
            OptiFine: new MinecraftLoaderVersionEntry(MinecraftLoaderKind.OptiFine, "HD_U_I6", "1.20.1 I6", "1.20.1", true, false, MinecraftRemoteSource.BmclApi));

        var name = MinecraftMergedInstallService.BuildDefaultInstanceName("1.20.1", selection);

        StringAssert.Contains(name, "Fabric_0.16.14");
        StringAssert.Contains(name, "OptiFine_");
    }

    [TestMethod]
    public void LoaderCompatibilityKeepsLabyModExclusive()
    {
        var fabric = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Fabric, "0.16.14", "0.16.14", "1.20.1", true, false, MinecraftRemoteSource.Official);
        var optiFine = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.OptiFine, "HD_U_I6", "1.20.1 I6", "1.20.1", true, false, MinecraftRemoteSource.BmclApi);
        var labyMod = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.LabyMod, "production", "稳定版", "1.20.1", true, true, MinecraftRemoteSource.Official);

        var selection = MinecraftLoaderCompatibility.SelectLoader(new MinecraftMergedLoaderSelection(Fabric: fabric, OptiFine: optiFine), labyMod);

        Assert.IsNotNull(selection.LabyMod);
        Assert.IsNull(selection.Fabric);
        Assert.IsNull(selection.OptiFine);
        CollectionAssert.Contains(MinecraftLoaderCompatibility.GetConflictingLoaderKinds(selection, MinecraftLoaderKind.Fabric).ToArray(), MinecraftLoaderKind.LabyMod);
    }

    [TestMethod]
    public void LoaderCompatibilityAppliesOptiFineForgeRequirement()
    {
        var optiFine = new MinecraftLoaderVersionEntry(
            MinecraftLoaderKind.OptiFine,
            "HD_U_G5",
            "1.12.2 G5",
            "1.12.2",
            true,
            false,
            MinecraftRemoteSource.BmclApi,
            Metadata: new JsonObject { ["forge"] = "Forge 14.23.5.2859" });
        var matchingForge = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Forge, "14.23.5.2859", "14.23.5.2859", "1.12.2", true, false, MinecraftRemoteSource.BmclApi);
        var otherForge = matchingForge with { Version = "14.23.5.2860", DisplayName = "14.23.5.2860" };

        Assert.IsTrue(MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(optiFine, matchingForge));
        Assert.IsFalse(MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(optiFine, otherForge));

        var normalized = MinecraftLoaderCompatibility.Normalize(new MinecraftMergedLoaderSelection(OptiFine: optiFine, Forge: otherForge), "1.12.2");

        Assert.IsNull(normalized.OptiFine);
        Assert.IsNotNull(normalized.Forge);
    }

    [TestMethod]
    public void LoaderCompatibilityRemovesOptiFineFromUnsupportedCombinations()
    {
        var optiFine = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.OptiFine, "HD_U_I7", "1.20.5 I7", "1.20.5", true, false, MinecraftRemoteSource.BmclApi);
        var fabric = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Fabric, "0.16.14", "0.16.14", "1.20.5", true, false, MinecraftRemoteSource.Official);
        var neoForge = new MinecraftLoaderVersionEntry(MinecraftLoaderKind.NeoForge, "20.5.1", "20.5.1", "1.20.5", true, false, MinecraftRemoteSource.BmclApi);

        var fabricSelection = MinecraftLoaderCompatibility.Normalize(new MinecraftMergedLoaderSelection(OptiFine: optiFine, Fabric: fabric), "1.20.5");
        var neoForgeSelection = MinecraftLoaderCompatibility.Normalize(new MinecraftMergedLoaderSelection(OptiFine: optiFine, NeoForge: neoForge), "1.20.5");

        Assert.IsNull(fabricSelection.OptiFine);
        Assert.IsNotNull(fabricSelection.Fabric);
        Assert.IsNull(neoForgeSelection.OptiFine);
        Assert.IsNotNull(neoForgeSelection.NeoForge);
    }

    [TestMethod]
    public void AddonCompatibilityRequiresVersionAndOptionalLoader()
    {
        var file = new MinecraftAddonFileEntry(
            MinecraftAddonKind.FabricApi,
            "fabric-api",
            "Fabric API",
            "fabric-api.jar",
            ["https://example.invalid/fabric-api.jar"],
            ["1.20.1"],
            [MinecraftLoaderKind.Fabric],
            "abc",
            123,
            true,
            DateTime.UtcNow,
            MinecraftRemoteSource.Modrinth);

        Assert.IsTrue(MinecraftModLoaderCatalogService.IsAddonCompatible(file, "1.20.1", MinecraftLoaderKind.Fabric));
        Assert.IsFalse(MinecraftModLoaderCatalogService.IsAddonCompatible(file, "1.20.2", MinecraftLoaderKind.Fabric));
        Assert.IsFalse(MinecraftModLoaderCatalogService.IsAddonCompatible(file, "1.20.1", MinecraftLoaderKind.Quilt));
    }

    [TestMethod]
    public void CurseForgeAddonParserBuildsOptiFabricFallbackDownloadUrl()
    {
        var files = MinecraftModLoaderCatalogService.ParseCurseForgeAddonFiles(
            MinecraftAddonKind.OptiFabric,
            "322385",
            new JsonObject
            {
                ["data"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = 5699256,
                        ["displayName"] = "OptiFabric 1.14.3",
                        ["fileName"] = "optifabric-1.14.3 mc1.20.1.jar",
                        ["downloadUrl"] = null,
                        ["gameVersions"] = new JsonArray("1.20.1", "Fabric", "Java 17"),
                        ["hashes"] = new JsonArray(new JsonObject { ["algo"] = 1, ["value"] = "abc" }),
                        ["fileLength"] = 1234,
                        ["releaseType"] = 1,
                        ["fileDate"] = "2024-09-01T00:00:00Z"
                    }
                }
            });

        var file = files.Single();

        Assert.AreEqual(MinecraftAddonKind.OptiFabric, file.Kind);
        Assert.AreEqual(MinecraftRemoteSource.CurseForge, file.Source);
        Assert.AreEqual("optifabric-1.14.3 mc1.20.1.jar", file.FileName);
        CollectionAssert.Contains(file.GameVersions.ToArray(), "1.20.1");
        CollectionAssert.Contains(file.Loaders.ToArray(), MinecraftLoaderKind.Fabric);
        StringAssert.Contains(file.DownloadUrls.First(), "https://edge.forgecdn.net/files/5699/256/");
        StringAssert.Contains(file.DownloadUrls.First(), "optifabric-1.14.3%20mc1.20.1.jar");
    }

    [TestMethod]
    public async Task MergedInstallParsesInstallerVersionJsonWithoutRunningJava()
    {
        using var server = new InstallFixtureServer();
        server.Start();
        var service = new MinecraftMergedInstallService();
        var optiFine = new MinecraftLoaderVersionEntry(
            MinecraftLoaderKind.OptiFine,
            "HD_U_TEST",
            "1.20.1 TEST",
            "1.20.1",
            true,
            false,
            MinecraftRemoteSource.Generated,
            $"http://127.0.0.1:{server.Port}/OptiFine.jar",
            FileName: "OptiFine.jar");

        var instance = await service.InstallAsync(new MinecraftMergedInstallRequest(
            "1.20.1",
            $"http://127.0.0.1:{server.Port}/vanilla.json",
            Path.Combine(_root, ".minecraft"),
            "1.20.1-OptiFine_TEST",
            new MinecraftMergedLoaderSelection(OptiFine: optiFine)));

        Assert.AreEqual("1.20.1-OptiFine_TEST", instance.Name);
        Assert.AreEqual("optifine.OptiFineTweaker", instance.Json["mainClass"]?.GetValue<string>());
        Assert.IsTrue(Directory.Exists(Path.Combine(instance.GameDirectory, "mods")));
        Assert.IsFalse(File.Exists(Path.Combine(instance.VersionDirectory, ".pclignore")));
    }

    [TestMethod]
    public void InstanceServiceMovesDeleteIntoLauncherRecycleFolder()
    {
        var instance = CreateInstance();
        var oldLocal = PCL.Core.App.Paths.SharedLocalData;
        PCL.Core.App.Paths.SharedLocalData = Path.Combine(_root, "local");
        Directory.CreateDirectory(PCL.Core.App.Paths.SharedLocalData);
        try
        {
            new MinecraftInstanceService().DeleteToRecycle(instance);

            Assert.IsFalse(Directory.Exists(instance.VersionDirectory));
            Assert.IsTrue(Directory.Exists(Path.Combine(PCL.Core.App.Paths.SharedLocalData, "DeletedInstances")));
        }
        finally
        {
            PCL.Core.App.Paths.SharedLocalData = oldLocal;
        }
    }

    private static void AssertLoader(string version, MinecraftLoaderKind kind, bool available)
    {
        var option = MinecraftLoaderCatalog.GetOptions(version).Single(loader => loader.Kind == kind);

        Assert.AreEqual(available, option.IsAvailable, $"{kind} availability for {version}");
        Assert.IsFalse(string.IsNullOrWhiteSpace(option.StatusText));
    }

    private static MemoryStream CreateInstallerJar()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("version.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(new JsonObject
            {
                ["id"] = "1.20.1-OptiFine_TEST",
                ["mainClass"] = "optifine.OptiFineTweaker",
                ["libraries"] = new JsonArray()
            }.ToJsonString());
        }

        stream.Position = 0;
        return stream;
    }

    private MinecraftInstanceInfo CreateInstance()
    {
        var mc = Path.Combine(_root, ".minecraft");
        var versionDir = Path.Combine(mc, "versions", "1.0");
        Directory.CreateDirectory(versionDir);
        var json = new JsonObject
        {
            ["id"] = "1.0",
            ["type"] = "release",
            ["mainClass"] = "net.minecraft.client.Main",
            ["releaseTime"] = "2024-01-01T00:00:00Z",
            ["downloads"] = new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["url"] = "https://piston-data.mojang.com/v1/objects/client.jar",
                    ["sha1"] = "abc",
                    ["size"] = 12
                }
            },
            ["assetIndex"] = new JsonObject
            {
                ["id"] = "1",
                ["url"] = "https://piston-meta.mojang.com/v1/packages/assets.json",
                ["sha1"] = "def",
                ["size"] = 3
            },
            ["libraries"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "com.example:demo:1.0.0",
                    ["downloads"] = new JsonObject
                    {
                        ["artifact"] = new JsonObject
                        {
                            ["path"] = "com/example/demo/1.0.0/demo-1.0.0.jar",
                            ["url"] = "https://libraries.minecraft.net/com/example/demo/1.0.0/demo-1.0.0.jar",
                            ["size"] = 10,
                            ["sha1"] = "123"
                        }
                    }
                }
            }
        };
        var jsonPath = Path.Combine(versionDir, "1.0.json");
        File.WriteAllText(jsonPath, json.ToJsonString());
        return MinecraftInstanceInfo.Load(jsonPath);
    }
}
