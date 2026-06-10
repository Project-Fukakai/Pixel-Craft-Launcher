using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class MinecraftLaunchLogAnalyzerTest
{
    [TestMethod]
    public void AnalyzeCrashLogDetectsOutOfMemory()
    {
        var result = MinecraftLaunchLogAnalyzer.AnalyzeCrashLog("java.lang.OutOfMemoryError: Java heap space", 1);

        StringAssert.Contains(result, "内存不足");
    }

    [TestMethod]
    public void AnalyzeLaunchFailureDetectsJavaVersionMismatch()
    {
        var result = MinecraftLaunchLogAnalyzer.AnalyzeLaunchFailure("UnsupportedClassVersionError");

        StringAssert.Contains(result, "Java 版本不匹配");
    }

    [TestMethod]
    public void AnalyzeCrashLogFallsBackToExitCode()
    {
        var result = MinecraftLaunchLogAnalyzer.AnalyzeCrashLog("unknown crash", 255);

        StringAssert.Contains(result, "非零退出码 255");
    }
}
