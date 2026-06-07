using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Minecraft.Profiles;

public static class MinecraftOfflineUuidHelper
{
    public static string Create(string username, OfflineUuidMode mode, string? customUuid = null)
    {
        var normalized = NormalizeUsername(username);
        return mode switch
        {
            OfflineUuidMode.Legacy => CreateLegacy(normalized),
            OfflineUuidMode.Custom => NormalizeUuid(customUuid),
            _ => MinecraftOfflineUuid.Create(normalized)
        };
    }

    public static string NormalizeUsername(string? username)
    {
        var value = string.IsNullOrWhiteSpace(username) ? "Steve" : username.Trim();
        return value.Length <= 16 ? value : value[..16];
    }

    public static string NormalizeUuid(string? uuid)
    {
        var value = (uuid ?? string.Empty).Replace("-", "", StringComparison.Ordinal).Trim();
        if (value.Length != 32 || !value.All(Uri.IsHexDigit))
            throw new MinecraftProfileException("UUID 必须是 32 位十六进制字符。");
        return value.ToLowerInvariant();
    }

    public static string CreateLegacy(string username)
    {
        var fullUuid = Fill(username.Length.ToString("X"), 16) + Fill(GetLegacyHash(username).ToString("X"), 16);
        return (fullUuid[..12] + "3" + fullUuid.Substring(13, 3) + "9" + fullUuid.Substring(17, 15)).ToLowerInvariant();
    }

    private static string Fill(string value, int length)
    {
        if (value.Length >= length) return value[^length..];
        return new string('0', length - value.Length) + value;
    }

    private static ulong GetLegacyHash(string value)
    {
        var hash = 5381UL;
        foreach (var c in value)
            hash = (hash << 5) ^ hash ^ c;
        return hash ^ 0xA98F501BC684032FUL;
    }
}
