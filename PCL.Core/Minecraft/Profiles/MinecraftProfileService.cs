using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Secret;

namespace PCL.Core.Minecraft.Profiles;

public sealed class MinecraftProfileService
{
    private const string ProfileFileName = "profiles.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly string _profilePath;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _loaded;

    public MinecraftProfileService(Func<HttpClient>? httpClientFactory = null, string? profilePath = null)
    {
        _httpClientFactory = httpClientFactory ?? (static () => new HttpClient());
        _profilePath = profilePath ?? Path.Combine(Paths.SharedData, ProfileFileName);
    }

    public ObservableCollection<MinecraftProfile> Profiles { get; } = [];
    public ObservableCollection<AuthServerPreset> AuthServers { get; } = [];
    public MinecraftProfile? SelectedProfile { get; private set; }
    public int LastUsed { get; private set; }

    public event EventHandler? ProfilesChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded) return;
            LoadCore();
            MigrateLegacyProfiles();
            EnsureDefaults();
            SelectLastUsedProfile();
            SaveCore();
            _loaded = true;
        }
        finally
        {
            _syncLock.Release();
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public MinecraftProfileStoreSnapshot Snapshot() =>
        new(Profiles.ToArray(), AuthServers.ToArray(), LastUsed);

    public async Task<MinecraftProfile> AddOfflineProfileAsync(
        string username,
        OfflineUuidMode uuidMode,
        string? customUuid = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalized = MinecraftOfflineUuidHelper.NormalizeUsername(username);
        var profile = new MinecraftProfile
        {
            Type = MinecraftProfileType.Offline,
            Username = normalized,
            Uuid = MinecraftOfflineUuidHelper.Create(normalized, uuidMode, customUuid),
            Description = uuidMode == OfflineUuidMode.Legacy ? "离线档案 - PCL 旧版 UUID" : "离线档案"
        };
        await AddOrUpdateProfileAsync(profile, select: true, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task<AuthServerPreset> AddAuthServerAsync(
        string name,
        string apiRoot,
        string registerUrl,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalizedRoot = NormalizeAuthServerRoot(apiRoot);
        var preset = new AuthServerPreset
        {
            Name = string.IsNullOrWhiteSpace(name) ? normalizedRoot : name.Trim(),
            ApiRoot = normalizedRoot,
            RegisterUrl = registerUrl.Trim()
        };

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AuthServers.Add(preset);
            SaveCore();
        }
        finally
        {
            _syncLock.Release();
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return preset;
    }

    public async Task<MinecraftProfile> AddMicrosoftProfileAsync(
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var login = await MicrosoftMinecraftLoginAsync(null, callbacks, progress, cancellationToken).ConfigureAwait(false);
        var profile = new MinecraftProfile
        {
            Type = MinecraftProfileType.Microsoft,
            Username = login.Username,
            Uuid = login.Uuid,
            AccessToken = login.AccessToken,
            RefreshToken = login.RefreshToken,
            ClientToken = login.Uuid,
            RawJson = login.RawJson,
            Expires = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()
        };
        await AddOrUpdateProfileAsync(profile, select: true, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task<MinecraftProfile> AddAuthlibProfileAsync(
        string apiRoot,
        string loginName,
        string password,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalizedRoot = NormalizeAuthServerRoot(apiRoot);
        var result = await AuthlibAuthenticateAsync(normalizedRoot, loginName, password, null, callbacks, progress, cancellationToken)
            .ConfigureAwait(false);
        var profile = new MinecraftProfile
        {
            Type = MinecraftProfileType.AuthlibInjector,
            Username = result.Username,
            Uuid = result.Uuid,
            Server = normalizedRoot + "/authserver",
            ServerName = result.ServerName,
            LoginName = loginName,
            Password = password,
            AccessToken = result.AccessToken,
            ClientToken = result.ClientToken,
            Expires = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()
        };
        await AddOrUpdateProfileAsync(profile, select: true, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task SelectProfileAsync(MinecraftProfile profile, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var index = Profiles.IndexOf(profile);
        if (index < 0)
            throw new MinecraftProfileException("无法选择不存在的档案。");
        SelectedProfile = profile;
        LastUsed = index;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveProfileAsync(MinecraftProfile profile, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = Profiles.IndexOf(profile);
            if (index < 0) return;
            Profiles.RemoveAt(index);
            if (Profiles.Count == 0)
            {
                LastUsed = 0;
                SelectedProfile = null;
            }
            else
            {
                LastUsed = Math.Clamp(LastUsed >= index ? LastUsed - 1 : LastUsed, 0, Profiles.Count - 1);
                SelectedProfile = Profiles[LastUsed];
            }
            SaveCore();
        }
        finally
        {
            _syncLock.Release();
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateOfflineProfileAsync(
        MinecraftProfile profile,
        string username,
        OfflineUuidMode uuidMode,
        string? customUuid = null,
        CancellationToken cancellationToken = default)
    {
        if (profile.Type != MinecraftProfileType.Offline)
            throw new MinecraftProfileException("只有离线档案支持在启动器内修改玩家名与 UUID。");
        profile.Username = MinecraftOfflineUuidHelper.NormalizeUsername(username);
        profile.Uuid = MinecraftOfflineUuidHelper.Create(profile.Username, uuidMode, customUuid);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public IMinecraftAccountProvider CreateAccountProvider(
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress = null) =>
        new ProfileMinecraftAccountProvider(this, callbacks, progress);

    public async Task<MinecraftAccountSession> GetAccountSessionAsync(
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var profile = SelectedProfile ?? throw new MinecraftProfileException("请先创建并选择一个启动档案。");
        return profile.Type switch
        {
            MinecraftProfileType.Offline => new MinecraftAccountSession(
                MinecraftAccountType.Offline,
                profile.Username,
                profile.Uuid,
                profile.Uuid,
                profile.Uuid),
            MinecraftProfileType.Microsoft => await GetMicrosoftSessionAsync(profile, callbacks, progress, cancellationToken)
                .ConfigureAwait(false),
            MinecraftProfileType.AuthlibInjector => await GetAuthlibSessionAsync(profile, callbacks, progress, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new MinecraftProfileException("未知档案类型。")
        };
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SaveCore();
        }
        finally
        {
            _syncLock.Release();
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string GetProfileTypeName(MinecraftProfile? profile) => profile?.Type switch
    {
        MinecraftProfileType.Microsoft => "正版验证",
        MinecraftProfileType.AuthlibInjector => string.IsNullOrWhiteSpace(profile.ServerName) ? "第三方验证" : profile.ServerName,
        MinecraftProfileType.Offline => "离线验证",
        _ => "未选择档案"
    };

    public static string NormalizeAuthServerRoot(string apiRoot)
    {
        var value = (apiRoot ?? string.Empty).Trim().TrimEnd('/');
        if (value.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
            value = value[..^"/authserver".Length];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new MinecraftProfileException("第三方验证服务器地址必须是 http/https 网址。");
        return value;
    }

    private async Task<MinecraftAccountSession> GetMicrosoftSessionAsync(
        MinecraftProfile profile,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress,
        CancellationToken cancellationToken)
    {
        var login = await MicrosoftMinecraftLoginAsync(profile, callbacks, progress, cancellationToken).ConfigureAwait(false);
        profile.Username = login.Username;
        profile.Uuid = login.Uuid;
        profile.AccessToken = login.AccessToken;
        profile.RefreshToken = login.RefreshToken;
        profile.RawJson = login.RawJson;
        profile.ClientToken = login.Uuid;
        profile.Expires = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return new MinecraftAccountSession(
            MinecraftAccountType.Microsoft,
            profile.Username,
            profile.Uuid,
            profile.AccessToken,
            profile.ClientToken,
            profile.RawJson);
    }

    private async Task<MinecraftAccountSession> GetAuthlibSessionAsync(
        MinecraftProfile profile,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress,
        CancellationToken cancellationToken)
    {
        var root = NormalizeAuthServerRoot(profile.Server);
        AuthlibLoginResult result;
        try
        {
            progress?.Report(new MinecraftProfileLoginProgress("验证第三方会话", 0.15, "正在验证已有登录状态"));
            await AuthlibValidateAsync(root, profile.AccessToken, profile.ClientToken, cancellationToken).ConfigureAwait(false);
            result = new AuthlibLoginResult(profile.Username, profile.Uuid, profile.AccessToken, profile.ClientToken, profile.ServerName);
        }
        catch
        {
            try
            {
                progress?.Report(new MinecraftProfileLoginProgress("刷新第三方会话", 0.35, "正在刷新登录状态"));
                result = await AuthlibRefreshAsync(root, profile, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                progress?.Report(new MinecraftProfileLoginProgress("重新登录第三方档案", 0.55, "正在重新登录第三方验证服务器"));
                result = await AuthlibAuthenticateAsync(root, profile.LoginName, profile.Password, profile, callbacks, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        profile.Username = result.Username;
        profile.Uuid = result.Uuid;
        profile.AccessToken = result.AccessToken;
        profile.ClientToken = result.ClientToken;
        profile.ServerName = result.ServerName;
        profile.Expires = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return new MinecraftAccountSession(
            MinecraftAccountType.AuthlibInjector,
            profile.Username,
            profile.Uuid,
            profile.AccessToken,
            profile.ClientToken,
            AuthServerBaseUrl: root);
    }

    private async Task AddOrUpdateProfileAsync(MinecraftProfile profile, bool select, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = Profiles.FirstOrDefault(item =>
                item.Type == profile.Type &&
                string.Equals(item.Uuid, profile.Uuid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Server, profile.Server, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var index = Profiles.IndexOf(existing);
                Profiles[index] = profile with { Id = existing.Id };
                profile = Profiles[index];
            }
            else
            {
                Profiles.Add(profile);
            }

            if (select)
            {
                LastUsed = Profiles.IndexOf(profile);
                SelectedProfile = profile;
            }
            SaveCore();
        }
        finally
        {
            _syncLock.Release();
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadCore()
    {
        Profiles.Clear();
        AuthServers.Clear();
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath) ?? Paths.SharedData);
        if (!File.Exists(_profilePath))
        {
            LastUsed = 0;
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(_profilePath))?.AsObject();
            if (root is null) return;
            LastUsed = root["lastUsed"]?.GetValue<int>() ?? 0;
            foreach (var profileNode in root["profiles"]?.AsArray() ?? [])
            {
                if (profileNode is JsonObject obj)
                    Profiles.Add(ReadProfile(obj));
            }

            foreach (var serverNode in root["authServers"]?.AsArray() ?? [])
            {
                if (serverNode is JsonObject obj)
                    AuthServers.Add(new AuthServerPreset
                    {
                        Id = obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                        Name = obj["name"]?.GetValue<string>() ?? string.Empty,
                        ApiRoot = obj["apiRoot"]?.GetValue<string>() ?? string.Empty,
                        RegisterUrl = obj["registerUrl"]?.GetValue<string>() ?? string.Empty
                    });
            }
        }
        catch (Exception ex)
        {
            var backup = _profilePath + ".bak" + DateTime.Now.ToBinary();
            try { File.Move(_profilePath, backup, overwrite: true); } catch { }
            throw new MinecraftProfileException("档案数据读取失败，已尝试备份损坏文件。", ex);
        }
    }

    private void SaveCore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath) ?? Paths.SharedData);
        var root = new JsonObject
        {
            ["lastUsed"] = LastUsed,
            ["profiles"] = new JsonArray(Profiles.Select(WriteProfile).ToArray<JsonNode?>()),
            ["authServers"] = new JsonArray(AuthServers.Select(server => new JsonObject
            {
                ["id"] = server.Id,
                ["name"] = server.Name,
                ["apiRoot"] = server.ApiRoot,
                ["registerUrl"] = server.RegisterUrl
            }).ToArray<JsonNode?>())
        };
        var tempFile = _profilePath + ".tmp";
        File.WriteAllText(tempFile, root.ToJsonString(JsonOptions));
        if (File.Exists(_profilePath))
            File.Replace(tempFile, _profilePath, _profilePath + ".bak", ignoreMetadataErrors: true);
        else
            File.Move(tempFile, _profilePath);
    }

    private void EnsureDefaults()
    {
        if (AuthServers.All(server => !string.Equals(server.ApiRoot, "https://littleskin.cn/api/yggdrasil", StringComparison.OrdinalIgnoreCase)))
        {
            AuthServers.Insert(0, new AuthServerPreset
            {
                Name = "LittleSkin",
                ApiRoot = "https://littleskin.cn/api/yggdrasil",
                RegisterUrl = "https://littleskin.cn/auth/register"
            });
        }
    }

    private void SelectLastUsedProfile()
    {
        if (Profiles.Count == 0)
        {
            SelectedProfile = null;
            LastUsed = 0;
            return;
        }
        LastUsed = Math.Clamp(LastUsed, 0, Profiles.Count - 1);
        SelectedProfile = Profiles[LastUsed];
    }

    private void MigrateLegacyProfiles()
    {
        var migrated = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(States.Game.LegacyProfile.LoginMsJson) &&
                States.Game.LegacyProfile.LoginMsJson.Trim() != "{}")
            {
                var oldMs = JsonNode.Parse(States.Game.LegacyProfile.LoginMsJson)?.AsObject();
                if (oldMs is not null)
                {
                    foreach (var pair in oldMs)
                    {
                        var username = pair.Key;
                        var value = pair.Value as JsonObject;
                        Profiles.Add(new MinecraftProfile
                        {
                            Type = MinecraftProfileType.Microsoft,
                            Username = username,
                            Uuid = value?["uuid"]?.GetValue<string>() ?? MinecraftOfflineUuidHelper.Create(username, OfflineUuidMode.Legacy),
                            AccessToken = value?["accessToken"]?.GetValue<string>() ?? string.Empty,
                            RefreshToken = value?["refreshToken"]?.GetValue<string>() ?? string.Empty,
                            RawJson = value?.ToJsonString(JsonOptions) ?? string.Empty,
                            Description = "从旧版配置迁移"
                        });
                        migrated = true;
                    }
                }
            }

            var legacyNames = States.Game.LegacyProfile.LoginLegacyName;
            foreach (var name in (legacyNames ?? string.Empty).Split('\u00a8', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Profiles.Add(new MinecraftProfile
                {
                    Type = MinecraftProfileType.Offline,
                    Username = name,
                    Uuid = MinecraftOfflineUuidHelper.Create(name, OfflineUuidMode.Legacy),
                    Description = "从旧版配置迁移"
                });
                migrated = true;
            }

            if (!string.IsNullOrWhiteSpace(States.Game.LegacyProfile.AuthUserName) &&
                !string.IsNullOrWhiteSpace(States.Game.LegacyProfile.AuthUuid) &&
                !string.IsNullOrWhiteSpace(States.Game.LegacyProfile.AuthServerAddress))
            {
                var root = NormalizeAuthServerRoot(States.Game.LegacyProfile.AuthServerAddress);
                Profiles.Add(new MinecraftProfile
                {
                    Type = MinecraftProfileType.AuthlibInjector,
                    Username = States.Game.LegacyProfile.AuthUserName,
                    Uuid = States.Game.LegacyProfile.AuthUuid,
                    LoginName = States.Game.LegacyProfile.AuthThirdPartyUserName,
                    Password = States.Game.LegacyProfile.AuthPassword,
                    Server = root + "/authserver",
                    ServerName = root,
                    Description = "从旧版配置迁移"
                });
                migrated = true;
            }

            if (migrated)
            {
                States.Game.LegacyProfile.LoginLegacyName = string.Empty;
                States.Game.LegacyProfile.AuthUserName = string.Empty;
                States.Game.LegacyProfile.AuthUuid = string.Empty;
                States.Game.LegacyProfile.AuthServerAddress = string.Empty;
                States.Game.LegacyProfile.AuthThirdPartyUserName = string.Empty;
                States.Game.LegacyProfile.AuthPassword = string.Empty;
                States.Game.LegacyProfile.LoginMsJson = "{}";
            }
        }
        catch
        {
            // Legacy configuration migration is best-effort.
        }
    }

    private static MinecraftProfile ReadProfile(JsonObject obj)
    {
        var type = obj["type"]?.GetValue<string>() switch
        {
            "microsoft" => MinecraftProfileType.Microsoft,
            "authlib" => MinecraftProfileType.AuthlibInjector,
            _ => MinecraftProfileType.Offline
        };
        return new MinecraftProfile
        {
            Id = obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
            Type = type,
            Uuid = obj["uuid"]?.GetValue<string>() ?? string.Empty,
            Username = obj["username"]?.GetValue<string>() ?? string.Empty,
            AccessToken = Decrypt(obj["accessToken"]?.GetValue<string>()),
            RefreshToken = Decrypt(obj["refreshToken"]?.GetValue<string>()),
            ClientToken = Decrypt(obj["clientToken"]?.GetValue<string>()),
            Expires = obj["expires"]?.GetValue<long>() ?? 0,
            Description = obj["desc"]?.GetValue<string>() ?? string.Empty,
            RawJson = Decrypt(obj["rawJson"]?.GetValue<string>()),
            SkinHeadId = obj["skinHeadId"]?.GetValue<string>() ?? string.Empty,
            Server = obj["server"]?.GetValue<string>() ?? string.Empty,
            ServerName = obj["serverName"]?.GetValue<string>() ?? string.Empty,
            LoginName = Decrypt(obj["name"]?.GetValue<string>()),
            Password = Decrypt(obj["password"]?.GetValue<string>())
        };
    }

    private static JsonObject WriteProfile(MinecraftProfile profile)
    {
        var obj = new JsonObject
        {
            ["id"] = profile.Id,
            ["type"] = profile.Type switch
            {
                MinecraftProfileType.Microsoft => "microsoft",
                MinecraftProfileType.AuthlibInjector => "authlib",
                _ => "offline"
            },
            ["uuid"] = profile.Uuid,
            ["username"] = profile.Username,
            ["desc"] = profile.Description,
            ["skinHeadId"] = profile.SkinHeadId
        };

        if (profile.Type == MinecraftProfileType.Microsoft)
        {
            obj["accessToken"] = Encrypt(profile.AccessToken);
            obj["refreshToken"] = Encrypt(profile.RefreshToken);
            obj["expires"] = profile.Expires;
            obj["rawJson"] = Encrypt(profile.RawJson);
        }
        else if (profile.Type == MinecraftProfileType.AuthlibInjector)
        {
            obj["accessToken"] = Encrypt(profile.AccessToken);
            obj["refreshToken"] = Encrypt(profile.RefreshToken);
            obj["clientToken"] = Encrypt(profile.ClientToken);
            obj["expires"] = profile.Expires;
            obj["server"] = profile.Server;
            obj["serverName"] = profile.ServerName;
            obj["name"] = Encrypt(profile.LoginName);
            obj["password"] = Encrypt(profile.Password);
        }

        return obj;
    }

    private static string Encrypt(string? value) => EncryptHelper.SecretEncrypt(value);

    private static string Decrypt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return EncryptHelper.SecretDecrypt(value); }
        catch { return value; }
    }

    private sealed record MicrosoftLoginResult(string Username, string Uuid, string AccessToken, string RefreshToken, string RawJson);

    private async Task<MicrosoftLoginResult> MicrosoftMinecraftLoginAsync(
        MinecraftProfile? existing,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Secrets.MSOAuthClientId))
            throw new MinecraftProfileException("缺少微软 OAuth Client ID，无法登录微软账号。");

        progress?.Report(new MinecraftProfileLoginProgress("微软 OAuth", 0.1, "正在获取微软 OAuth Token"));
        var oauth = string.IsNullOrWhiteSpace(existing?.RefreshToken)
            ? await RequestMicrosoftDeviceTokenAsync(callbacks, cancellationToken).ConfigureAwait(false)
            : await RefreshMicrosoftOAuthAsync(existing.RefreshToken, callbacks, cancellationToken).ConfigureAwait(false);

        progress?.Report(new MinecraftProfileLoginProgress("Xbox Live", 0.3, "正在获取 Xbox Live Token"));
        var xbl = await PostJsonAsync<JsonObject>("https://user.auth.xboxlive.com/user/authenticate", new
        {
            Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + oauth.AccessToken },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        }, cancellationToken).ConfigureAwait(false);
        var xblToken = xbl?["Token"]?.GetValue<string>() ?? throw new MinecraftProfileException("Xbox Live Token 为空。");

        progress?.Report(new MinecraftProfileLoginProgress("XSTS", 0.45, "正在获取 XSTS Token"));
        var xsts = await PostJsonAsync<JsonObject>("https://xsts.auth.xboxlive.com/xsts/authorize", new
        {
            Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xblToken } },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        }, cancellationToken).ConfigureAwait(false);
        var xstsToken = xsts?["Token"]?.GetValue<string>() ?? throw new MinecraftProfileException("XSTS Token 为空。");
        var uhs = xsts?["DisplayClaims"]?["xui"]?[0]?["uhs"]?.GetValue<string>() ?? throw new MinecraftProfileException("XSTS UHS 为空。");

        progress?.Report(new MinecraftProfileLoginProgress("Minecraft", 0.6, "正在获取 Minecraft AccessToken"));
        var mc = await PostJsonAsync<JsonObject>("https://api.minecraftservices.com/authentication/login_with_xbox", new
        {
            identityToken = $"XBL3.0 x={uhs};{xstsToken}"
        }, cancellationToken).ConfigureAwait(false);
        var mcAccessToken = mc?["access_token"]?.GetValue<string>() ?? throw new MinecraftProfileException("Minecraft AccessToken 为空。");

        progress?.Report(new MinecraftProfileLoginProgress("Minecraft", 0.75, "正在验证 Minecraft Java Edition 所有权"));
        using (var client = _httpClientFactory())
        using (var entitlements = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore"))
        {
            entitlements.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcAccessToken);
            using var response = await client.SendAsync(entitlements, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var owns = json?["items"]?.AsArray().Any(item =>
                item?["name"]?.GetValue<string>() is "product_minecraft" or "game_minecraft") == true;
            if (!owns)
                throw new MinecraftProfileException("此微软账号没有可用的 Minecraft Java Edition 权益。");
        }

        progress?.Report(new MinecraftProfileLoginProgress("Minecraft", 0.9, "正在获取玩家档案"));
        using (var client = _httpClientFactory())
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile"))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcAccessToken);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var json = JsonNode.Parse(raw);
            return new MicrosoftLoginResult(
                json?["name"]?.GetValue<string>() ?? throw new MinecraftProfileException("玩家名为空。"),
                json?["id"]?.GetValue<string>() ?? throw new MinecraftProfileException("玩家 UUID 为空。"),
                mcAccessToken,
                oauth.RefreshToken,
                raw);
        }
    }

    private sealed record OAuthTokenResult(string AccessToken, string RefreshToken);

    private async Task<OAuthTokenResult> RequestMicrosoftDeviceTokenAsync(
        IMinecraftProfileUiCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory();
        using var codeResponse = await client.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Secrets.MSOAuthClientId,
                ["scope"] = "XboxLive.signin offline_access"
            }),
            cancellationToken).ConfigureAwait(false);
        codeResponse.EnsureSuccessStatusCode();
        var codeJson = JsonNode.Parse(await codeResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var deviceCode = codeJson?["device_code"]?.GetValue<string>() ?? throw new MinecraftProfileException("设备代码为空。");
        var userCode = codeJson?["user_code"]?.GetValue<string>() ?? string.Empty;
        var verificationUri = codeJson?["verification_uri"]?.GetValue<string>() ?? string.Empty;
        var completeUri = codeJson?["verification_uri_complete"]?.GetValue<string>();
        var interval = Math.Max(1, codeJson?["interval"]?.GetValue<int>() ?? 5);
        var expiresIn = codeJson?["expires_in"]?.GetValue<int>() ?? 900;
        await callbacks.ShowDeviceCodeAsync(
            new DeviceCodePrompt(userCode, verificationUri, completeUri, DateTimeOffset.UtcNow.AddSeconds(expiresIn)),
            cancellationToken).ConfigureAwait(false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
            using var tokenResponse = await client.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = Secrets.MSOAuthClientId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = deviceCode
                }),
                cancellationToken).ConfigureAwait(false);
            var raw = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var json = JsonNode.Parse(raw);
            if (tokenResponse.IsSuccessStatusCode)
                return new OAuthTokenResult(
                    json?["access_token"]?.GetValue<string>() ?? string.Empty,
                    json?["refresh_token"]?.GetValue<string>() ?? string.Empty);

            var error = json?["error"]?.GetValue<string>();
            if (error is "authorization_pending")
                continue;
            if (error is "slow_down")
            {
                interval += 5;
                continue;
            }
            throw new MinecraftProfileException(json?["error_description"]?.GetValue<string>() ?? "微软设备代码登录失败。");
        }
    }

    private async Task<OAuthTokenResult> RefreshMicrosoftOAuthAsync(
        string refreshToken,
        IMinecraftProfileUiCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory();
        using var response = await client.PostAsync(
            "https://login.live.com/oauth20_token.srf",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Secrets.MSOAuthClientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = "XboxLive.signin offline_access"
            }),
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await RequestMicrosoftDeviceTokenAsync(callbacks, cancellationToken).ConfigureAwait(false);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return new OAuthTokenResult(
            json?["access_token"]?.GetValue<string>() ?? string.Empty,
            json?["refresh_token"]?.GetValue<string>() ?? refreshToken);
    }

    private async Task<T?> PostJsonAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory();
        using var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed record AuthlibLoginResult(string Username, string Uuid, string AccessToken, string ClientToken, string ServerName);

    private async Task<AuthlibLoginResult> AuthlibAuthenticateAsync(
        string root,
        string loginName,
        string password,
        MinecraftProfile? existing,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MinecraftProfileLoginProgress("第三方验证", 0.2, "正在登录第三方验证服务器"));
        var request = new JsonObject
        {
            ["agent"] = new JsonObject { ["name"] = "Minecraft", ["version"] = 1 },
            ["username"] = loginName,
            ["password"] = password,
            ["requestUser"] = true
        };
        var json = await PostAuthlibAsync(root + "/authserver/authenticate", request, cancellationToken).ConfigureAwait(false);
        var selected = json["selectedProfile"] as JsonObject;
        var available = json["availableProfiles"] as JsonArray;
        if ((selected is null || existing is not null) && available is { Count: > 0 })
        {
            var candidates = available
                .OfType<JsonObject>()
                .Select(item => (item["id"]?.GetValue<string>() ?? string.Empty, item["name"]?.GetValue<string>() ?? string.Empty))
                .Where(item => !string.IsNullOrWhiteSpace(item.Item1))
                .ToArray();
            if (selected is null && candidates.Length == 1)
                selected = new JsonObject { ["id"] = candidates[0].Item1, ["name"] = candidates[0].Item2 };
            else if (candidates.Length > 1)
            {
                var index = existing is not null
                    ? Array.FindIndex(candidates, item => string.Equals(item.Item1, existing.Uuid, StringComparison.OrdinalIgnoreCase))
                    : -1;
                if (index < 0)
                    index = await callbacks.SelectAuthlibProfileAsync(candidates, cancellationToken).ConfigureAwait(false) ?? 0;
                selected = new JsonObject { ["id"] = candidates[index].Item1, ["name"] = candidates[index].Item2 };
                json = await AuthlibRefreshSelectedAsync(root, json["accessToken"]?.GetValue<string>() ?? string.Empty, selected, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (selected is null)
            throw new MinecraftProfileException("第三方账号没有可用角色。");

        var serverName = await GetAuthServerNameAsync(root, cancellationToken).ConfigureAwait(false);
        return new AuthlibLoginResult(
            selected["name"]?.GetValue<string>() ?? string.Empty,
            selected["id"]?.GetValue<string>() ?? string.Empty,
            json["accessToken"]?.GetValue<string>() ?? string.Empty,
            json["clientToken"]?.GetValue<string>() ?? string.Empty,
            serverName);
    }

    private async Task<AuthlibLoginResult> AuthlibRefreshAsync(string root, MinecraftProfile profile, CancellationToken cancellationToken)
    {
        var selected = new JsonObject { ["id"] = profile.Uuid, ["name"] = profile.Username };
        var json = await AuthlibRefreshSelectedAsync(root, profile.AccessToken, selected, cancellationToken).ConfigureAwait(false);
        var profileJson = json["selectedProfile"] as JsonObject ?? selected;
        return new AuthlibLoginResult(
            profileJson["name"]?.GetValue<string>() ?? profile.Username,
            profileJson["id"]?.GetValue<string>() ?? profile.Uuid,
            json["accessToken"]?.GetValue<string>() ?? profile.AccessToken,
            json["clientToken"]?.GetValue<string>() ?? profile.ClientToken,
            profile.ServerName);
    }

    private async Task<JsonObject> AuthlibRefreshSelectedAsync(string root, string accessToken, JsonObject selected, CancellationToken cancellationToken)
    {
        var request = new JsonObject
        {
            ["accessToken"] = accessToken,
            ["selectedProfile"] = selected.DeepClone(),
            ["requestUser"] = true
        };
        return await PostAuthlibAsync(root + "/authserver/refresh", request, cancellationToken).ConfigureAwait(false);
    }

    private async Task AuthlibValidateAsync(string root, string accessToken, string clientToken, CancellationToken cancellationToken)
    {
        var request = new JsonObject { ["accessToken"] = accessToken, ["clientToken"] = clientToken };
        using var client = _httpClientFactory();
        using var response = await client.PostAsJsonAsync(root + "/authserver/validate", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            throw new MinecraftProfileException("第三方会话验证失败。");
    }

    private async Task<JsonObject> PostAuthlibAsync(string url, JsonObject body, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new MinecraftProfileException(TryGetAuthlibError(raw) ?? "第三方验证请求失败。");
        return JsonNode.Parse(raw)?.AsObject() ?? throw new MinecraftProfileException("第三方验证服务器返回了无效 JSON。");
    }

    private async Task<string> GetAuthServerNameAsync(string root, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory();
            return (JsonNode.Parse(await client.GetStringAsync(root, cancellationToken).ConfigureAwait(false))
                ?["meta"]?["serverName"]?.GetValue<string>()).ReplaceNullOrEmpty(root);
        }
        catch
        {
            return root;
        }
    }

    private static string? TryGetAuthlibError(string raw)
    {
        try
        {
            var json = JsonNode.Parse(raw);
            return json?["errorMessage"]?.GetValue<string>() ?? json?["error"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private sealed class ProfileMinecraftAccountProvider(
        MinecraftProfileService service,
        IMinecraftProfileUiCallbacks callbacks,
        IProgress<MinecraftProfileLoginProgress>? progress) : IMinecraftAccountProvider
    {
        public Task<MinecraftAccountSession> GetAccountAsync(MinecraftInstanceInfo instance, CancellationToken cancellationToken) =>
            service.GetAccountSessionAsync(callbacks, progress, cancellationToken);
    }
}
