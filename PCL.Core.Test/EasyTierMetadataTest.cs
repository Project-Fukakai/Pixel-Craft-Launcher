using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.Test;

[TestClass]
public class EasyTierMetadataTest
{
    [DataTestMethod]
    [DataRow("windows", Architecture.X64, "easytier-windows-x86_64", "easytier-core.exe", "easytier-cli.exe", "Packet.dll")]
    [DataRow("windows", Architecture.Arm64, "easytier-windows-arm64", "easytier-core.exe", "easytier-cli.exe", "Packet.dll")]
    [DataRow("windows", Architecture.X86, "easytier-windows-i686", "easytier-core.exe", "easytier-cli.exe", "Packet.dll")]
    [DataRow("macos", Architecture.X64, "easytier-macos-x86_64", "easytier-core", "easytier-cli", null)]
    [DataRow("macos", Architecture.Arm64, "easytier-macos-aarch64", "easytier-core", "easytier-cli", null)]
    [DataRow("linux", Architecture.X64, "easytier-linux-x86_64", "easytier-core", "easytier-cli", null)]
    [DataRow("linux", Architecture.Arm64, "easytier-linux-aarch64", "easytier-core", "easytier-cli", null)]
    [DataRow("linux", Architecture.Arm, "easytier-linux-armv7", "easytier-core", "easytier-cli", null)]
    public void ResolvePlatformReturnsExpectedAssetNames(
        string os,
        Architecture arch,
        string platformId,
        string coreExecutable,
        string cliExecutable,
        string? windowsOnlyRequiredFile)
    {
        var info = EasyTierMetadata.ResolvePlatform(os, arch);

        Assert.IsTrue(info.IsSupported);
        Assert.AreEqual(platformId, info.PlatformId);
        Assert.AreEqual($"{platformId}-v{EasyTierMetadata.CurrentEasyTierVer}.zip", info.ArchiveName);
        Assert.AreEqual(coreExecutable, info.CoreExecutableName);
        Assert.AreEqual(cliExecutable, info.CliExecutableName);
        Assert.Contains(coreExecutable, info.RequiredFiles.ToArray());
        Assert.Contains(cliExecutable, info.RequiredFiles.ToArray());
        if (windowsOnlyRequiredFile is not null)
            Assert.Contains(windowsOnlyRequiredFile, info.RequiredFiles.ToArray());
        Assert.IsTrue(info.DownloadUrls.Any(url => url.Contains($"github.com/EasyTier/EasyTier/releases/download/v{EasyTierMetadata.CurrentEasyTierVer}/")));
    }

    [TestMethod]
    public void ResolvePlatformRejectsUnsupportedArchitecture()
    {
        var info = EasyTierMetadata.ResolvePlatform("macos", Architecture.X86);

        Assert.IsFalse(info.IsSupported);
        Assert.AreEqual("easytier-core", info.CoreExecutableName);
        Assert.AreEqual("easytier-cli", info.CliExecutableName);
        Assert.IsEmpty(info.RequiredFiles);
        Assert.IsEmpty(info.DownloadUrls);
    }
}
