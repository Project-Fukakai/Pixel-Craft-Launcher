using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class MinecraftServerLaunchScriptWriterTest
{
    [TestMethod]
    public async Task WriteAsyncCreatesCrossPlatformLaunchScripts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PCLTest", "ServerScripts", Guid.NewGuid().ToString("N"));

        await MinecraftServerLaunchScriptWriter.WriteAsync(directory, "1.21.8", CancellationToken.None);

        var bat = Path.Combine(directory, "Launch Server.bat");
        var ps1 = Path.Combine(directory, "Launch Server.ps1");
        var sh = Path.Combine(directory, "Launch Server.sh");
        Assert.IsTrue(File.Exists(bat));
        Assert.IsTrue(File.Exists(ps1));
        Assert.IsTrue(File.Exists(sh));
        StringAssert.Contains(await File.ReadAllTextAsync(bat), "1.21.8-server.jar");
        StringAssert.Contains(await File.ReadAllTextAsync(ps1), "1.21.8-server.jar");
        StringAssert.Contains(await File.ReadAllTextAsync(sh), "1.21.8-server.jar");
    }
}
