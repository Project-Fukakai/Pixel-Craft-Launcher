using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class MinecraftLaunchServiceTest
{
    private string _oldSharedLocalData = string.Empty;
    private string _oldTemp = string.Empty;
    private string _testRoot = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        ConfigService.EnsureInitializedForTesting();
        _oldSharedLocalData = Paths.SharedLocalData;
        _oldTemp = Paths.Temp;
        _testRoot = Path.Combine(Path.GetTempPath(), "pcl-core-launch-test", Guid.NewGuid().ToString("N"));
        Paths.SharedLocalData = Path.Combine(_testRoot, "local");
        Paths.Temp = Path.Combine(_testRoot, "temp");
        Directory.CreateDirectory(Paths.SharedLocalData);
        Directory.CreateDirectory(Paths.Temp);

        Config.Launch.DisableJlw = true;
        Config.Launch.DisableRw = false;
        Config.Launch.DisableLwjglUnsafeAgent = false;
        Config.Launch.Renderer = 0;
        Config.Launch.GameArgs = "";
        Config.Launch.PreLaunchCommand = "";
        Config.Launch.GameWindowMode = GameWindowSizeMode.Default;
        Config.Launch.MemoryAllocationMode = 1;
        Config.Launch.CustomMemorySize = 15;
        Config.Launch.TypeInfo = "PCLCE";
        Config.Launch.Title = "";
    }

    [TestCleanup]
    public void Cleanup()
    {
        Paths.SharedLocalData = _oldSharedLocalData;
        Paths.Temp = _oldTemp;
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }
        catch
        {
            // Best effort cleanup; locked files should not fail the test run.
        }
    }

    [DataTestMethod]
    [DataRow("1.5.2", 0, 8)]
    [DataRow("1.12.2", 8, 999)]
    [DataRow("1.17", 16, 999)]
    [DataRow("1.18", 17, 999)]
    [DataRow("1.20.5", 21, 999)]
    public void JavaRequirementMatchesVanillaVersion(string versionId, int minMajor, int maxMajor)
    {
        var requirement = MinecraftJavaRequirementResolver.Resolve(CreateInstance(versionId));

        Assert.AreEqual(minMajor, requirement.Minimum.Major);
        Assert.AreEqual(maxMajor, requirement.Maximum.Major);
    }

    [TestMethod]
    public void JavaRequirementUsesMojangJavaVersion()
    {
        var instance = CreateInstance("1.21.6", javaVersion: 22, component: "java-runtime-delta");

        var requirement = MinecraftJavaRequirementResolver.Resolve(instance);

        Assert.AreEqual(22, requirement.Minimum.Major);
        Assert.AreEqual("java-runtime-delta", requirement.RecommendedComponent);
    }

    [TestMethod]
    public void JavaRequirementDetectsCleanroomAndLabyMod()
    {
        var cleanroomLegacy = CreateInstance("1.12.2-cleanroom", libraries: ["com.cleanroommc:cleanroom:0.4.9"]);
        var cleanroomModern = CreateInstance("1.12.2-cleanroom", libraries: ["com.cleanroommc:cleanroom:0.5.0"]);
        var labyMod = CreateInstance("1.20.4-labymod", libraries: ["net.labymod:labymod:4.0.0"]);

        Assert.AreEqual(21, MinecraftJavaRequirementResolver.Resolve(cleanroomLegacy).Minimum.Major);
        Assert.AreEqual(25, MinecraftJavaRequirementResolver.Resolve(cleanroomModern).Minimum.Major);
        Assert.AreEqual(21, MinecraftJavaRequirementResolver.Resolve(labyMod).Minimum.Major);
    }

    [TestMethod]
    public void JavaRequirementDetectsForgeAndOptiFineBranches()
    {
        var forge172 = CreateInstance("1.7.2-forge", libraries: ["net.minecraftforge:forge:1.7.2-10.12.2.1121"]);
        var forge1122 = CreateInstance("1.12.2-forge", libraries: ["net.minecraftforge:forge:1.12.2-14.23.5.2860"]);
        var optifine1122 = CreateInstance("1.12.2-optifine", libraries: ["optifine:OptiFine:1.12.2_HD_U_G5"]);

        var forge172Requirement = MinecraftJavaRequirementResolver.Resolve(forge172);
        Assert.AreEqual(7, forge172Requirement.Minimum.Major);
        Assert.AreEqual(7, forge172Requirement.Maximum.Major);
        Assert.AreEqual(8, MinecraftJavaRequirementResolver.Resolve(forge1122).Maximum.Major);
        Assert.AreEqual(8, MinecraftJavaRequirementResolver.Resolve(optifine1122).Maximum.Major);
    }

    [TestMethod]
    public async Task BuildLaunchPlanSupportsOldArgumentsAndInstanceSettings()
    {
        var instance = CreateInstance(
            "1.12.2",
            minecraftArguments:
            "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} --assetsDir ${assets_root} --assetIndex ${assets_index_name} --uuid ${auth_uuid} --accessToken ${auth_access_token} --userType ${user_type}",
            libraries: ["com.example:demo-lib:1.0.0"],
            releaseTime: "2017-09-18T00:00:00Z");
        CreateLocalFiles(instance, "com.example:demo-lib:1.0.0");
        Config.Launch.MemoryAllocationMode = 1;
        Config.Launch.CustomMemorySize = 15;
        Config.Launch.GameWindowMode = GameWindowSizeMode.Custom;
        Config.Launch.GameWindowWidth = 1280;
        Config.Launch.GameWindowHeight = 720;
        Config.Launch.TypeInfo = "PCLCE-Test";

        var plan = await BuildPlanAsync(instance, new MinecraftLaunchOptions { ServerIp = "play.example.org:25566" });

        CollectionAssert.Contains(plan.JvmArguments.ToArray(), "-Xmx3072m");
        StringAssert.Contains(plan.Classpath, Path.Combine(instance.MinecraftFolder, "libraries", "com", "example", "demo-lib", "1.0.0", "demo-lib-1.0.0.jar"));
        StringAssert.Contains(plan.Classpath, instance.JarPath);
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--height");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "720");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--width");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "1280");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--server");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "play.example.org");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "25566");
    }

    [TestMethod]
    public async Task BuildLaunchPlanSupportsModernArgumentsTitleAndQuickPlay()
    {
        var instance = CreateInstance(
            "1.20.5",
            arguments: new JsonObject
            {
                ["jvm"] = new JsonArray
                {
                    "-Djava.library.path=${natives_directory}",
                    "-cp",
                    "${classpath}"
                },
                ["game"] = new JsonArray
                {
                    "--username",
                    "${auth_player_name}",
                    "--versionType",
                    "${version_type}",
                    new JsonObject
                    {
                        ["rules"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["action"] = "allow",
                                ["features"] = new JsonObject { ["has_custom_resolution"] = true }
                            }
                        },
                        ["value"] = new JsonArray { "--width", "${resolution_width}", "--height", "${resolution_height}" }
                    }
                }
            });
        CreateLocalFiles(instance);
        Config.Launch.GameWindowMode = GameWindowSizeMode.Custom;
        Config.Launch.GameWindowWidth = 1024;
        Config.Launch.GameWindowHeight = 640;
        Config.Launch.TypeInfo = "";
        Config.Launch.Title = "{name} | 玩家 : {user} | 使用 {login} 登录";
        Config.Instance.UseGlobalTitle[instance.VersionDirectory] = false;
        Config.Instance.Title[instance.VersionDirectory] = "";

        var plan = await BuildPlanAsync(instance, new MinecraftLaunchOptions { WorldName = "My World" });

        Assert.AreEqual("1.20.5 | 玩家 : Alex | 使用 离线 登录", plan.LaunchTitle);
        CollectionAssert.DoesNotContain(plan.GameArguments.ToArray(), "--versionType");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--width");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "1024");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--height");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "640");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--quickPlaySingleplayer");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "My World");
    }

    [TestMethod]
    public async Task BuildLaunchPlanReleasesRetroWrapperForLegacyVersions()
    {
        var instance = CreateInstance(
            "1.5.2",
            minecraftArguments:
            "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} --assetsDir ${assets_root} --assetIndex ${assets_index_name} --uuid ${auth_uuid} --accessToken ${auth_access_token}");
        CreateLocalFiles(instance);

        var plan = await BuildPlanAsync(instance, new MinecraftLaunchOptions());

        Assert.IsTrue(plan.Resources.UsesRetroWrapper);
        Assert.IsNotNull(plan.Resources.RetroWrapperPath);
        Assert.IsTrue(File.Exists(plan.Resources.RetroWrapperPath));
        StringAssert.Contains(plan.Classpath, plan.Resources.RetroWrapperPath);
        CollectionAssert.Contains(plan.JvmArguments.ToArray(), "-Dretrowrapper.doUpdateCheck=false");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "--tweakClass");
        CollectionAssert.Contains(plan.GameArguments.ToArray(), "com.zero.retrowrapper.RetroTweaker");
    }

    [TestMethod]
    public async Task BuildLaunchPlanReleasesLwjglUnsafeAgentOnlyWhenLwjgl341IsUsed()
    {
        var affected = CreateInstance("1.21.6", libraries: ["org.lwjgl:lwjgl:3.4.1"]);
        var unaffected = CreateInstance("1.21.6", libraries: ["org.lwjgl:lwjgl:3.3.3"]);
        CreateLocalFiles(affected, "org.lwjgl:lwjgl:3.4.1");
        CreateLocalFiles(unaffected, "org.lwjgl:lwjgl:3.3.3");

        var affectedPlan = await BuildPlanAsync(affected, new MinecraftLaunchOptions());
        var unaffectedPlan = await BuildPlanAsync(unaffected, new MinecraftLaunchOptions());

        Assert.IsTrue(affectedPlan.Resources.UsesLwjglUnsafeAgent);
        Assert.IsNotNull(affectedPlan.Resources.LwjglUnsafeAgentPath);
        Assert.IsTrue(File.Exists(affectedPlan.Resources.LwjglUnsafeAgentPath));
        Assert.IsTrue(affectedPlan.JvmArguments.Any(arg => arg.StartsWith("-javaagent:", StringComparison.Ordinal) && arg.Contains("lwjgl-unsafe-agent.jar", StringComparison.Ordinal)));
        Assert.IsFalse(unaffectedPlan.Resources.UsesLwjglUnsafeAgent);
        Assert.IsFalse(unaffectedPlan.JvmArguments.Any(arg => arg.Contains("lwjgl-unsafe-agent.jar", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task BuildLaunchPlanAddsJavaWrapperOnlyWhenEnabledAndUtf8()
    {
        var instance = CreateInstance("1.20.5", arguments: new JsonObject
        {
            ["jvm"] = new JsonArray
            {
                "-Djava.library.path=${natives_directory}",
                "-cp",
                "${classpath}"
            },
            ["game"] = new JsonArray { "--username", "${auth_player_name}" }
        });
        CreateLocalFiles(instance);
        Config.Launch.DisableJlw = false;

        var plan = await BuildPlanAsync(instance, new MinecraftLaunchOptions { UseJavaWrapper = true });

        if (Encoding.Default.CodePage == 65001)
        {
            Assert.IsTrue(plan.Resources.UsesJavaWrapper);
            Assert.IsNotNull(plan.Resources.JavaWrapperPath);
            Assert.IsTrue(File.Exists(plan.Resources.JavaWrapperPath));
            CollectionAssert.Contains(plan.JvmArguments.ToArray(), "-jar");
            CollectionAssert.Contains(plan.JvmArguments.ToArray(), plan.Resources.JavaWrapperPath);
        }
        else
        {
            Assert.IsFalse(plan.Resources.UsesJavaWrapper);
        }
    }

    [TestMethod]
    public void ExtractNativesSkipsMetaInfAndCleansStaleFiles()
    {
        var nativeJar = Path.Combine(Paths.Temp, "native.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(nativeJar)!);
        using (var archive = ZipFile.Open(nativeJar, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "demo.dll", "native");
            WriteEntry(archive, "META-INF/MANIFEST.MF", "ignored");
        }

        var target = Path.Combine(Paths.Temp, "natives");
        Directory.CreateDirectory(target);
        var stale = Path.Combine(target, "stale.dll");
        File.WriteAllText(stale, "stale");

        MinecraftLaunchService.ExtractNatives([new MinecraftLibraryFile("native:test:1.0", nativeJar, true)], target);

        Assert.IsTrue(File.Exists(Path.Combine(target, "demo.dll")));
        Assert.IsFalse(File.Exists(stale));
        Assert.IsFalse(Directory.Exists(Path.Combine(target, "META-INF")));

        static void WriteEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }
    }

    [TestMethod]
    public void WriteLauncherProfileStoresOfflineAccountAndDoesNotBlockOnMissingFile()
    {
        var instance = CreateInstance("1.20.5");
        var account = new MinecraftAccountSession(
            MinecraftAccountType.Offline,
            "Alex",
            MinecraftOfflineUuid.Create("Alex"),
            "access-token-123456",
            "client-token-123456");

        var written = MinecraftLaunchService.WriteLauncherProfile(instance, account);
        var profilePath = Path.Combine(instance.MinecraftFolder, "launcher_profiles.json");
        var root = JsonNode.Parse(File.ReadAllText(profilePath))!.AsObject();

        Assert.IsTrue(written);
        Assert.AreEqual("client-token-123456", root["clientToken"]!.GetValue<string>());
        Assert.AreEqual("PCL", root["selectedProfile"]!.GetValue<string>());
        Assert.AreEqual("1.20.5", root["profiles"]!["PCL"]!["lastVersionId"]!.GetValue<string>());
        Assert.AreEqual("Alex", root["authenticationDatabase"]!["clienttoken123456"]!["username"]!.GetValue<string>());
    }

    [TestMethod]
    public void FilterSensitiveMasksTokensAndUserHome()
    {
        var account = new MinecraftAccountSession(
            MinecraftAccountType.Microsoft,
            "Alex",
            "uuid",
            "access-token-123456",
            "client-token-123456");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var raw = $"{home}/.minecraft access-token-123456 client-token-123456";

        var filtered = MinecraftLaunchService.FilterSensitive(raw, account);

        Assert.IsFalse(filtered.Contains("access-token-123456", StringComparison.Ordinal));
        Assert.IsFalse(filtered.Contains("client-token-123456", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(home))
            Assert.IsFalse(filtered.Contains(home, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void OfflineUuidIsStableMinecraftNameUuid()
    {
        var first = MinecraftOfflineUuid.Create("Steve");
        var second = MinecraftOfflineUuid.Create("Steve");

        Assert.AreEqual(first, second);
        Assert.AreEqual(32, first.Length);
        Assert.AreEqual('3', first[12]);
    }

    private static MinecraftInstanceInfo CreateInstance(
        string id,
        int? javaVersion = null,
        string? component = null,
        string[]? libraries = null,
        string? minecraftArguments = null,
        JsonObject? arguments = null,
        string releaseTime = "2024-01-01T00:00:00Z")
    {
        var root = new JsonObject
        {
            ["id"] = id,
            ["mainClass"] = "net.minecraft.client.main.Main",
            ["releaseTime"] = releaseTime,
            ["libraries"] = new JsonArray()
        };

        if (javaVersion is not null)
        {
            root["javaVersion"] = new JsonObject
            {
                ["majorVersion"] = javaVersion.Value,
                ["component"] = component
            };
        }

        if (!string.IsNullOrWhiteSpace(minecraftArguments))
            root["minecraftArguments"] = minecraftArguments;

        if (arguments is not null)
            root["arguments"] = arguments;

        foreach (var library in libraries ?? [])
        {
            root["libraries"]!.AsArray().Add(new JsonObject
            {
                ["name"] = library
            });
        }

        var versionDirectory = Path.Combine(Path.GetTempPath(), "pcl-test", Guid.NewGuid().ToString("N"), "versions", id);
        return new MinecraftInstanceInfo(
            id,
            Directory.GetParent(Directory.GetParent(versionDirectory)!.FullName)!.FullName,
            versionDirectory,
            Path.Combine(versionDirectory, id + ".json"),
            root,
            DateTime.Parse(releaseTime),
            MinecraftVersionNumber.TryParse(id));
    }

    private static async Task<MinecraftLaunchPlan> BuildPlanAsync(MinecraftInstanceInfo instance, MinecraftLaunchOptions options)
    {
        var account = new MinecraftAccountSession(MinecraftAccountType.Offline, "Alex", MinecraftOfflineUuid.Create("Alex"), "secret-token", "client-token");
        return await new MinecraftLaunchService().BuildLaunchPlanAsync(
            new MinecraftLaunchRequest(instance, new OfflineMinecraftAccountProvider("Alex"), WindowContext: new FixedWindowContext()),
            options,
            CreateJava(),
            account,
            CancellationToken.None);
    }

    private static JavaEntry CreateJava(int major = 17)
    {
        var folder = Path.Combine(Path.GetTempPath(), "pcl-test-java", major.ToString());
        Directory.CreateDirectory(folder);
        return new JavaEntry
        {
            Installation = new JavaInstallation(folder, new Version(major, 0, 0), JavaBrandType.OpenJDK, MachineType.AMD64, true, false)
        };
    }

    private static void CreateLocalFiles(MinecraftInstanceInfo instance, params string[] libraries)
    {
        Directory.CreateDirectory(instance.VersionDirectory);
        File.WriteAllText(instance.JsonPath, instance.Json.ToJsonString());
        File.WriteAllText(instance.JarPath, string.Empty);

        foreach (var library in libraries)
        {
            var path = Path.Combine(instance.MinecraftFolder, "libraries", MavenNameToPath(library));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
    }

    private static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length >= 4 ? "-" + parts[3] : string.Empty;
        return Path.Combine(group, artifact, version, $"{artifact}-{version}{classifier}.jar");
    }

    private sealed class FixedWindowContext : IGameWindowContext
    {
        public Task<MinecraftWindowSize?> GetLauncherWindowSizeAsync(CancellationToken cancellationToken) =>
            Task.FromResult<MinecraftWindowSize?>(new MinecraftWindowSize(900, 600));

        public Task ApplyLauncherVisibilityAsync(LauncherVisibility visibility, System.Diagnostics.Process process, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
