using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.Minecraft.Java.Scanner;

public class PathEnvironmentScanner : IJavaScanner
{
    public void Scan(ICollection<string> results)
    {
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return;

            foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var executable in JavaScannerUtils.JavaExecutableNames)
                {
                    var javaExe = Path.Combine(dir, executable);
                    if (File.Exists(javaExe)) results.Add(javaExe);
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "PATH环境变量扫描失败");
        }
    }
}
