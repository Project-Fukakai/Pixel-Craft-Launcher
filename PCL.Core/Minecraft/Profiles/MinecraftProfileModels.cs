using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Profiles;

public enum MinecraftProfileType
{
    Offline,
    Microsoft,
    AuthlibInjector
}

public enum OfflineUuidMode
{
    Standard,
    Legacy,
    Custom
}

public sealed record MinecraftProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public MinecraftProfileType Type { get; init; }
    public string Username { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ClientToken { get; set; } = string.Empty;
    public long Expires { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public string SkinHeadId { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
}

public sealed record AuthServerPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ApiRoot { get; set; } = string.Empty;
    public string RegisterUrl { get; set; } = string.Empty;
}

public sealed record MinecraftProfileStoreSnapshot(
    IReadOnlyList<MinecraftProfile> Profiles,
    IReadOnlyList<AuthServerPreset> AuthServers,
    int LastUsed);

public sealed record DeviceCodePrompt(
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    DateTimeOffset ExpiresAt);

public sealed record MinecraftProfileLoginProgress(string Stage, double Progress, string Message);

public sealed class MinecraftProfileException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IMinecraftProfileUiCallbacks
{
    Task ShowDeviceCodeAsync(DeviceCodePrompt prompt, CancellationToken cancellationToken);
    Task<int?> SelectAuthlibProfileAsync(IReadOnlyList<(string Id, string Name)> profiles, CancellationToken cancellationToken);
}
