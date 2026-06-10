using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.App.Pixel.Slices.Profiles;

public sealed record PixelProfileItemSnapshot(
    string Id,
    string Username,
    string Uuid,
    string Info,
    string Icon,
    bool IsSelected,
    bool IsOffline);

public sealed record PixelProfileListPageSnapshot(
    IReadOnlyList<PixelProfileItemSnapshot> Profiles,
    bool IsEmpty,
    string Title,
    string Description,
    string EmptyTitle,
    string EmptyText,
    string CopyUuidTip,
    string EditOfflineTip,
    string DeleteTip);

public sealed record PixelOfflineProfileEditorSnapshot(
    string? EditingProfileId,
    string Username,
    string Uuid,
    int UuidModeIndex,
    string Title,
    string PrimaryButtonText)
{
    public string PageTitle => "离线档案";

    public string UsernameHint => "玩家 ID";

    public string UuidHint => "自定义 UUID（可选）";

    public string UuidModeHint => "UUID 类型";

    public IReadOnlyList<string> UuidModeOptions => ["标准 UUID", "旧版 PCL UUID", "自定义 UUID"];

    public string FormPrimaryButtonText => EditingProfileId is null ? "创建离线档案" : "保存离线档案";
}

public sealed record PixelAuthServerSnapshot(
    string Id,
    string Name,
    string ApiRoot,
    string RegisterUrl,
    string Icon);

public sealed record PixelProfileManagerSidebarActionSnapshot(
    string Title,
    string Info,
    string Icon);

public sealed record PixelProfileManagerSidebarSnapshot(
    PixelProfileManagerSidebarActionSnapshot AddMicrosoft,
    PixelProfileManagerSidebarActionSnapshot AddOffline,
    string AuthServerSectionTitle,
    IReadOnlyList<PixelAuthServerSnapshot> AuthServers,
    string AddAuthServerButtonText);

public sealed record PixelAuthlibProfileFormSnapshot(
    string? ServerId,
    string ServerName,
    string ApiRoot,
    string ServerInfoText)
{
    public string PageTitle => "第三方验证服务器档案";

    public string LoginNameHint => "用户名 / 邮箱";

    public string PasswordHint => "密码";

    public string SubmitButtonText => "登录并创建档案";
}

public sealed record PixelAuthServerEditorSnapshot(
    string Name,
    string ApiRoot,
    string RegisterUrl)
{
    public string PageTitle => "添加第三方验证服务器";

    public string NameHint => "服务器名称";

    public string ApiRootHint => "Yggdrasil API 地址";

    public string RegisterUrlHint => "注册网址";
}

public sealed record PixelProfileDeviceCodeDialogSnapshot(
    string Title,
    string Markdown,
    string WaitButtonText,
    string CopyButtonText,
    string OpenButtonText,
    string UserCode,
    string OpenUrl);

public sealed record PixelProfileLoginProgressSnapshot(
    string Stage,
    string Message,
    double Progress,
    string Text);

public sealed record PixelAuthlibProfileChoiceSnapshot(
    string Id,
    string Name);

public sealed record PixelAuthlibProfileChoiceDialogSnapshot(
    string Title,
    string Description,
    string ConfirmButtonText,
    string CancelButtonText,
    IReadOnlyList<PixelAuthlibProfileChoiceSnapshot> Choices);

public sealed record PixelProfilePageMessages(
    string UuidCopied,
    string ProfileDeleted,
    string OfflineProfileSaved,
    string MicrosoftProfileAdded,
    string AuthlibProfileAdded,
    string AuthServerAdded,
    string MicrosoftLoginStatus,
    string AuthlibLoginStatus,
    string AuthlibDialogStatus,
    string OfflineDialogStatus,
    string AuthServerDialogStatus,
    string SaveAuthServerButtonText)
{
    public string MicrosoftPageTitle => "微软账号";

    public string MicrosoftDialogTitle => "添加微软账号";

    public string MicrosoftLoginButtonText => "登录微软账号";

    public string CancelButtonText => "取消";

    public string SaveButtonText => "保存";
}

public sealed record PixelProfileOperationFailurePresentation(
    string StatusText,
    string HintText);

public enum PixelProfileOperationKind
{
    SaveOfflineProfile,
    AddMicrosoftProfile,
    AddAuthlibProfile,
    AddAuthServer
}

public sealed record PixelProfileOperationResultSnapshot(
    PixelProfileOperationKind Kind,
    bool IsSuccess,
    string StatusText,
    string HintText);

public sealed class PixelProfileLoginProgressAdapter(
    PixelProfileListService profileListService,
    Action<PixelProfileLoginProgressSnapshot> onProgress)
    : IProgress<MinecraftProfileLoginProgress>
{
    public void Report(MinecraftProfileLoginProgress value)
    {
        onProgress(profileListService.GetLoginProgressSnapshot(value));
    }
}

public sealed class PixelProfileUiCallbacks(
    PixelProfileListService profileListService,
    Func<PixelProfileDeviceCodeDialogSnapshot, CancellationToken, Task> showDeviceCodeAsync,
    Func<IReadOnlyList<PixelAuthlibProfileChoiceSnapshot>, CancellationToken, Task<int?>>? selectAuthlibProfileAsync = null)
    : IMinecraftProfileUiCallbacks
{
    public Task ShowDeviceCodeAsync(DeviceCodePrompt prompt, CancellationToken cancellationToken)
    {
        return showDeviceCodeAsync(profileListService.GetDeviceCodeDialogSnapshot(prompt), cancellationToken);
    }

    public Task<int?> SelectAuthlibProfileAsync(
        IReadOnlyList<(string Id, string Name)> profiles,
        CancellationToken cancellationToken)
    {
        if (selectAuthlibProfileAsync is null)
            return Task.FromResult<int?>(0);

        return selectAuthlibProfileAsync(
            profiles.Select(profile => new PixelAuthlibProfileChoiceSnapshot(profile.Id, profile.Name)).ToArray(),
            cancellationToken);
    }
}

public sealed class PixelProfileListService
{
    public IReadOnlyList<PixelProfileItemSnapshot> GetProfiles(
        IEnumerable<MinecraftProfile> profiles,
        MinecraftProfile? selectedProfile)
    {
        var selectedId = selectedProfile?.Id;
        return profiles
            .Select(profile => GetProfileSnapshot(profile, selectedId))
            .ToArray();
    }

    public PixelProfileListPageSnapshot GetListPageSnapshot(
        IEnumerable<MinecraftProfile> profiles,
        MinecraftProfile? selectedProfile)
    {
        var profileSnapshots = GetProfiles(profiles, selectedProfile);
        return new PixelProfileListPageSnapshot(
            profileSnapshots,
            profileSnapshots.Count == 0,
            "档案管理",
            "管理用于启动 Minecraft 的微软账号、离线档案与第三方验证档案。",
            "暂无档案",
            "请从左侧添加一个账号或离线档案。",
            "复制 UUID",
            "编辑离线档案",
            "删除档案");
    }

    public PixelOfflineProfileEditorSnapshot GetOfflineEditorSnapshot(
        IEnumerable<MinecraftProfile> profiles,
        string? editingProfileId = null)
    {
        var editing = string.IsNullOrWhiteSpace(editingProfileId)
            ? null
            : profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, editingProfileId, StringComparison.Ordinal) &&
                profile.Type == MinecraftProfileType.Offline);
        return editing is null
            ? new PixelOfflineProfileEditorSnapshot(null, "Steve", string.Empty, 0, "添加离线档案", "创建")
            : new PixelOfflineProfileEditorSnapshot(editing.Id, editing.Username, editing.Uuid, 2, "编辑离线档案", "保存");
    }

    internal IReadOnlyList<PixelAuthServerSnapshot> GetAuthServers(IEnumerable<AuthServerPreset> servers)
    {
        return servers
            .Select(GetAuthServerSnapshot)
            .ToArray();
    }

    internal PixelProfileManagerSidebarSnapshot GetManagerSidebarSnapshot(IEnumerable<AuthServerPreset> servers)
    {
        return new PixelProfileManagerSidebarSnapshot(
            new PixelProfileManagerSidebarActionSnapshot("添加微软账号", "设备代码流登录正版账号", "mdi-microsoft"),
            new PixelProfileManagerSidebarActionSnapshot("添加离线档案", "玩家名、标准/旧版/自定义 UUID", "mdi-account-outline"),
            "添加第三方验证服务器档案",
            GetAuthServers(servers),
            "添加第三方验证服务器");
    }

    public PixelAuthlibProfileFormSnapshot GetAuthlibProfileFormSnapshot(
        IEnumerable<AuthServerPreset> servers,
        string? serverId = null)
    {
        var presets = servers.ToArray();
        var selected = presets.FirstOrDefault(item => item.Id == serverId) ?? presets.FirstOrDefault();
        var apiRoot = selected?.ApiRoot ?? "https://littleskin.cn/api/yggdrasil";
        var serverName = selected?.Name ?? apiRoot;
        return new PixelAuthlibProfileFormSnapshot(
            selected?.Id,
            serverName,
            apiRoot,
            $"验证服务器：{serverName}\n{apiRoot}");
    }

    public PixelAuthServerEditorSnapshot GetAuthServerEditorSnapshot() =>
        new("LittleSkin", "https://littleskin.cn/api/yggdrasil", "https://littleskin.cn/auth/register");

    public PixelProfilePageMessages GetPageMessages()
    {
        return new PixelProfilePageMessages(
            "已复制 UUID。",
            "档案已删除。",
            "离线档案已保存。",
            "微软账号已添加。",
            "第三方验证档案已添加。",
            "第三方验证服务器已添加。",
            "点击登录后会显示微软设备代码，请在浏览器中完成授权。",
            "使用 Authlib-Injector / Yggdrasil 服务器登录。",
            "输入账号信息后创建第三方验证档案。",
            "创建可直接用于启动的离线档案。",
            "保存后会出现在左侧第三方验证服务器列表中。",
            "保存服务器预设");
    }

    public PixelProfileLoginProgressSnapshot GetLoginProgressSnapshot(MinecraftProfileLoginProgress progress)
    {
        var normalizedProgress = Math.Clamp(progress.Progress, 0d, 1d);
        return new PixelProfileLoginProgressSnapshot(
            progress.Stage,
            progress.Message,
            normalizedProgress,
            $"{progress.Stage}：{progress.Message} ({normalizedProgress:P0})");
    }

    public string FormatLoginProgress(MinecraftProfileLoginProgress progress) =>
        GetLoginProgressSnapshot(progress).Text;

    public PixelProfileDeviceCodeDialogSnapshot GetDeviceCodeDialogSnapshot(DeviceCodePrompt prompt)
    {
        return new PixelProfileDeviceCodeDialogSnapshot(
            "微软账号登录",
            $"设备代码：{prompt.UserCode}\n\n网页登录地址：{prompt.VerificationUri}\n\n请在浏览器完成授权，完成后保持此窗口打开。",
            "继续等待",
            "复制代码",
            "打开网页",
            prompt.UserCode,
            prompt.VerificationUriComplete ?? prompt.VerificationUri);
    }

    public PixelAuthlibProfileChoiceDialogSnapshot GetAuthlibProfileChoiceDialogSnapshot(
        IReadOnlyList<PixelAuthlibProfileChoiceSnapshot> choices)
    {
        return new PixelAuthlibProfileChoiceDialogSnapshot(
            "选择第三方验证角色",
            "此账号下有多个可用角色，请选择要保存到启动器的角色。",
            "使用此角色",
            "取消",
            choices);
    }

    internal static string GetProfileTypeName(MinecraftProfile? profile) => profile?.Type switch
    {
        MinecraftProfileType.Microsoft => "正版验证",
        MinecraftProfileType.AuthlibInjector => string.IsNullOrWhiteSpace(profile.ServerName) ? "第三方验证" : profile.ServerName,
        MinecraftProfileType.Offline => "离线验证",
        _ => "未选择档案"
    };

    public static string GetProfileIcon(MinecraftProfileType type) =>
        type switch
        {
            MinecraftProfileType.Microsoft => "mdi-microsoft",
            MinecraftProfileType.AuthlibInjector => "mdi-server-security",
            _ => "mdi-account-outline"
        };

    internal static PixelProfileItemSnapshot GetProfileSnapshot(MinecraftProfile profile, string? selectedProfileId = null) =>
        new(
            profile.Id,
            profile.Username,
            profile.Uuid,
            $"{GetProfileTypeName(profile)} · {profile.Uuid}",
            GetProfileIcon(profile.Type),
            string.Equals(profile.Id, selectedProfileId, StringComparison.Ordinal),
            profile.Type == MinecraftProfileType.Offline);

    internal static PixelAuthServerSnapshot GetAuthServerSnapshot(AuthServerPreset server) =>
        new(
            server.Id,
            server.Name,
            server.ApiRoot,
            server.RegisterUrl,
            "mdi-server-security");
}

public sealed class PixelProfilePageService(
    MinecraftProfileService profileService,
    PixelProfileListService profileListService,
    IPixelCommandBus commandBus,
    PixelProfileStateMachine? stateMachine = null)
{
    public IReadOnlyList<PixelProfileItemSnapshot> GetProfiles()
    {
        return profileListService.GetProfiles(profileService.Profiles, profileService.SelectedProfile);
    }

    public PixelProfileListPageSnapshot GetListPageSnapshot()
    {
        return profileListService.GetListPageSnapshot(profileService.Profiles, profileService.SelectedProfile);
    }

    public IReadOnlyList<PixelAuthServerSnapshot> GetAuthServers()
    {
        return profileListService.GetAuthServers(profileService.AuthServers);
    }

    public PixelProfileManagerSidebarSnapshot GetManagerSidebarSnapshot()
    {
        return profileListService.GetManagerSidebarSnapshot(profileService.AuthServers);
    }

    public PixelOfflineProfileEditorSnapshot GetOfflineEditorSnapshot(string? editingProfileId = null)
    {
        return profileListService.GetOfflineEditorSnapshot(profileService.Profiles, editingProfileId);
    }

    public PixelAuthlibProfileFormSnapshot GetAuthlibProfileFormSnapshot(string? serverId = null)
    {
        return profileListService.GetAuthlibProfileFormSnapshot(profileService.AuthServers, serverId);
    }

    public PixelAuthServerEditorSnapshot GetAuthServerEditorSnapshot()
    {
        return profileListService.GetAuthServerEditorSnapshot();
    }

    public PixelProfilePageMessages GetPageMessages()
    {
        return profileListService.GetPageMessages();
    }

    public PixelProfileOperationFailurePresentation GetOperationFailurePresentation(Exception exception)
    {
        return new PixelProfileOperationFailurePresentation(exception.Message, exception.Message);
    }

    public async Task<PixelProfileOperationResultSnapshot> SaveOfflineProfileAsync(
        string? editingProfileId,
        string username,
        PixelOfflineUuidMode uuidMode,
        string? customUuid)
    {
        return await RunProfileOperationAsync(
                PixelProfileOperationKind.SaveOfflineProfile,
                PixelProfileTrigger.StartOfflineSave,
                () => RequireCommandBus().Send(
                    new SaveOfflineMinecraftProfileByIdCommand(editingProfileId, username, uuidMode, customUuid)))
            .ConfigureAwait(false);
    }

    public async Task<PixelProfileOperationResultSnapshot> AddMicrosoftProfileAsync(
        PixelProfileUiCallbacks callbacks,
        PixelProfileLoginProgressAdapter progress)
    {
        return await RunProfileOperationAsync(
                PixelProfileOperationKind.AddMicrosoftProfile,
                PixelProfileTrigger.StartMicrosoftLogin,
                () => RequireCommandBus().Send(new AddMicrosoftMinecraftProfileCommand(callbacks, progress)))
            .ConfigureAwait(false);
    }

    public async Task<PixelProfileOperationResultSnapshot> AddAuthlibProfileAsync(
        string apiRoot,
        string loginName,
        string password,
        PixelProfileUiCallbacks callbacks,
        PixelProfileLoginProgressAdapter progress)
    {
        return await RunProfileOperationAsync(
                PixelProfileOperationKind.AddAuthlibProfile,
                PixelProfileTrigger.StartAuthlibLogin,
                () => RequireCommandBus().Send(new AddAuthlibMinecraftProfileCommand(apiRoot, loginName, password, callbacks, progress)))
            .ConfigureAwait(false);
    }

    public async Task<PixelProfileOperationResultSnapshot> AddAuthServerAsync(string name, string apiRoot, string registerUrl)
    {
        return await RunProfileOperationAsync(
                PixelProfileOperationKind.AddAuthServer,
                PixelProfileTrigger.StartAuthServerSave,
                () => RequireCommandBus().Send(new AddAuthServerPresetCommand(name, apiRoot, registerUrl)))
            .ConfigureAwait(false);
    }

    public async Task SelectProfileAsync(string profileId)
    {
        await SendProfileCommandAsync(
            PixelProfileTrigger.StartSelect,
            () => RequireCommandBus().Send(new SelectMinecraftProfileByIdCommand(profileId)))
            .ConfigureAwait(false);
    }

    public async Task RemoveProfileAsync(string profileId)
    {
        await SendProfileCommandAsync(
            PixelProfileTrigger.StartRemove,
            () => RequireCommandBus().Send(new RemoveMinecraftProfileByIdCommand(profileId)))
            .ConfigureAwait(false);
    }

    private IPixelCommandBus RequireCommandBus()
    {
        return commandBus;
    }

    private async Task<PixelProfileOperationResultSnapshot> RunProfileOperationAsync(
        PixelProfileOperationKind kind,
        PixelProfileTrigger trigger,
        Func<Task> operation)
    {
        try
        {
            await SendProfileCommandAsync(trigger, operation).ConfigureAwait(false);
            return GetOperationSuccessResult(kind);
        }
        catch (Exception ex)
        {
            var failure = GetOperationFailurePresentation(ex);
            return new PixelProfileOperationResultSnapshot(kind, false, failure.StatusText, failure.HintText);
        }
    }

    private PixelProfileOperationResultSnapshot GetOperationSuccessResult(PixelProfileOperationKind kind)
    {
        var messages = GetPageMessages();
        var text = kind switch
        {
            PixelProfileOperationKind.SaveOfflineProfile => messages.OfflineProfileSaved,
            PixelProfileOperationKind.AddMicrosoftProfile => messages.MicrosoftProfileAdded,
            PixelProfileOperationKind.AddAuthlibProfile => messages.AuthlibProfileAdded,
            PixelProfileOperationKind.AddAuthServer => messages.AuthServerAdded,
            _ => string.Empty
        };
        return new PixelProfileOperationResultSnapshot(kind, true, text, text);
    }

    private async Task SendProfileCommandAsync(PixelProfileTrigger startTrigger, Func<Task> action)
    {
        BeginProfileState(startTrigger);
        try
        {
            await action().ConfigureAwait(false);
            TryFireProfileTrigger(PixelProfileTrigger.Succeed);
        }
        catch
        {
            TryFireProfileTrigger(PixelProfileTrigger.Fail);
            throw;
        }
    }

    private void BeginProfileState(PixelProfileTrigger trigger)
    {
        if (stateMachine is null)
            return;

        if (stateMachine.CanFire(trigger))
        {
            TryFireProfileTrigger(trigger);
            return;
        }

        if (stateMachine.CanFire(PixelProfileTrigger.Reset))
            TryFireProfileTrigger(PixelProfileTrigger.Reset);

        TryFireProfileTrigger(trigger);
    }

    private void TryFireProfileTrigger(PixelProfileTrigger trigger)
    {
        if (stateMachine?.CanFire(trigger) == true)
            stateMachine.Fire(trigger);
    }
}
