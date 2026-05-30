using System;
using System.IO;
using System.Security;
using Avalonia;
using Avalonia.Platform;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public class SystemTheme {
    private const string ThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeKey = "AppsUseLightTheme";

    /// <summary>
    /// 检查系统是否处于深色模式。
    /// </summary>
    /// <returns>如果系统使用深色模式，则返回 true；否则返回 false（包括注册表不可访问的情况）。</returns>
    public static bool IsSystemInDarkMode() {
        try {
            var platformSettings = Application.Current?.PlatformSettings;
            if (platformSettings is not null)
                return platformSettings.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "无法通过 Avalonia 获取系统主题");
        }

        if (!OperatingSystem.IsWindows()) return false;

        try {
            using var registryKey = Registry.CurrentUser.OpenSubKey(ThemeRegistryPath);
            if (registryKey == null) {
                LogWrapper.Warn($"注册表键 {ThemeRegistryPath} 不存在");
                return false;
            }

            var value = registryKey.GetValue(AppsUseLightThemeKey) as int?;
            return value == 0; // 0 表示深色模式（AppsUseLightTheme = false）
        } catch (Exception ex) when (ex is SecurityException or IOException) {
            LogWrapper.Warn(ex, $"无法访问注册表键 {ThemeRegistryPath}");
            return false;
        }
    }
}
