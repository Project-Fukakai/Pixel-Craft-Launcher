using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.Minecraft.Java.Scanner;

internal static class JavaScannerUtils
{
    public static string[] JavaExecutableNames => OperatingSystem.IsWindows()
        ? ["java.exe"]
        : ["java"];

    public static IEnumerable<string> GetJavaExecutables(string directory)
    {
        foreach (var executable in JavaExecutableNames)
        {
            var path = Path.Combine(directory, executable);
            if (File.Exists(path)) yield return path;
        }
    }
}
