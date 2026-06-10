using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Pixel.Events;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.Minecraft.Profiles;

namespace PCL.Core.Test.App;

[TestClass]
public class PixelProfileListServiceTest
{
    [TestMethod]
    public void GetProfilesBuildsUiReadySnapshots()
    {
        var service = new PixelProfileListService();
        var offline = new MinecraftProfile
        {
            Id = "offline",
            Type = MinecraftProfileType.Offline,
            Username = "Steve",
            Uuid = "offline-uuid"
        };
        var microsoft = new MinecraftProfile
        {
            Id = "microsoft",
            Type = MinecraftProfileType.Microsoft,
            Username = "Alex",
            Uuid = "microsoft-uuid"
        };
        var authlib = new MinecraftProfile
        {
            Id = "authlib",
            Type = MinecraftProfileType.AuthlibInjector,
            Username = "Yggdrasil",
            Uuid = "authlib-uuid",
            ServerName = "LittleSkin"
        };

        var profiles = service.GetProfiles([offline, microsoft, authlib], microsoft);

        Assert.AreEqual(3, profiles.Count);
        Assert.AreEqual("Steve", profiles[0].Username);
        Assert.AreEqual("离线验证 · offline-uuid", profiles[0].Info);
        Assert.AreEqual("mdi-account-outline", profiles[0].Icon);
        Assert.IsTrue(profiles[0].IsOffline);
        Assert.IsFalse(profiles[0].IsSelected);

        Assert.AreEqual("正版验证 · microsoft-uuid", profiles[1].Info);
        Assert.AreEqual("mdi-microsoft", profiles[1].Icon);
        Assert.IsTrue(profiles[1].IsSelected);

        Assert.AreEqual("LittleSkin · authlib-uuid", profiles[2].Info);
        Assert.AreEqual("mdi-server-security", profiles[2].Icon);
    }

    [TestMethod]
    public void AuthlibProfileWithoutServerNameUsesDefaultTypeName()
    {
        var service = new PixelProfileListService();
        var profile = new MinecraftProfile
        {
            Id = "authlib",
            Type = MinecraftProfileType.AuthlibInjector,
            Username = "User",
            Uuid = "uuid"
        };

        var snapshot = service.GetProfiles([profile], null).Single();

        Assert.AreEqual("第三方验证 · uuid", snapshot.Info);
    }

    [TestMethod]
    public void ListPageSnapshotIncludesProfilesAndUiText()
    {
        var service = new PixelProfileListService();
        var offline = new MinecraftProfile
        {
            Id = "offline",
            Type = MinecraftProfileType.Offline,
            Username = "Steve",
            Uuid = "offline-uuid"
        };

        var snapshot = service.GetListPageSnapshot([offline], offline);
        var emptySnapshot = service.GetListPageSnapshot([], null);

        Assert.AreEqual(1, snapshot.Profiles.Count);
        Assert.IsFalse(snapshot.IsEmpty);
        Assert.IsTrue(emptySnapshot.IsEmpty);
        Assert.AreEqual("档案管理", snapshot.Title);
        StringAssert.Contains(snapshot.Description, "微软账号");
        Assert.AreEqual("暂无档案", snapshot.EmptyTitle);
        Assert.AreEqual("请从左侧添加一个账号或离线档案。", snapshot.EmptyText);
        Assert.AreEqual("复制 UUID", snapshot.CopyUuidTip);
        Assert.AreEqual("编辑离线档案", snapshot.EditOfflineTip);
        Assert.AreEqual("删除档案", snapshot.DeleteTip);
        Assert.IsTrue(snapshot.Profiles[0].IsSelected);
    }

    [TestMethod]
    public void OfflineEditorSnapshotUsesDefaultsOrOfflineProfileValues()
    {
        var service = new PixelProfileListService();
        var offline = new MinecraftProfile
        {
            Id = "offline",
            Type = MinecraftProfileType.Offline,
            Username = "Steve",
            Uuid = "offline-uuid"
        };
        var microsoft = new MinecraftProfile
        {
            Id = "microsoft",
            Type = MinecraftProfileType.Microsoft,
            Username = "Alex",
            Uuid = "microsoft-uuid"
        };

        var create = service.GetOfflineEditorSnapshot([offline, microsoft]);
        var edit = service.GetOfflineEditorSnapshot([offline, microsoft], "offline");
        var nonOffline = service.GetOfflineEditorSnapshot([offline, microsoft], "microsoft");

        Assert.IsNull(create.EditingProfileId);
        Assert.AreEqual("Steve", create.Username);
        Assert.AreEqual(string.Empty, create.Uuid);
        Assert.AreEqual(0, create.UuidModeIndex);
        Assert.AreEqual("添加离线档案", create.Title);
        Assert.AreEqual("创建", create.PrimaryButtonText);

        Assert.AreEqual("offline", edit.EditingProfileId);
        Assert.AreEqual("Steve", edit.Username);
        Assert.AreEqual("offline-uuid", edit.Uuid);
        Assert.AreEqual(2, edit.UuidModeIndex);
        Assert.AreEqual("编辑离线档案", edit.Title);
        Assert.AreEqual("保存", edit.PrimaryButtonText);
        Assert.AreEqual("离线档案", edit.PageTitle);
        Assert.AreEqual("玩家 ID", edit.UsernameHint);
        Assert.AreEqual("自定义 UUID（可选）", edit.UuidHint);
        Assert.AreEqual("UUID 类型", edit.UuidModeHint);
        CollectionAssert.AreEqual(
            new[] { "标准 UUID", "旧版 PCL UUID", "自定义 UUID" },
            edit.UuidModeOptions.ToArray());
        Assert.AreEqual("保存离线档案", edit.FormPrimaryButtonText);
        Assert.AreEqual("创建离线档案", create.FormPrimaryButtonText);

        Assert.IsNull(nonOffline.EditingProfileId);
    }

    [TestMethod]
    public void AuthServerSnapshotsExposeListAndFormDefaults()
    {
        var service = new PixelProfileListService();
        var littleSkin = new AuthServerPreset
        {
            Id = "little-skin",
            Name = "LittleSkin",
            ApiRoot = "https://littleskin.cn/api/yggdrasil",
            RegisterUrl = "https://littleskin.cn/auth/register"
        };

        var servers = service.GetAuthServers([littleSkin]);
        var sidebar = service.GetManagerSidebarSnapshot([littleSkin]);
        var selected = service.GetAuthlibProfileFormSnapshot([littleSkin], "little-skin");
        var fallback = service.GetAuthlibProfileFormSnapshot([]);
        var editor = service.GetAuthServerEditorSnapshot();

        Assert.AreEqual(1, servers.Count);
        Assert.AreEqual("little-skin", servers[0].Id);
        Assert.AreEqual("LittleSkin", servers[0].Name);
        Assert.AreEqual("mdi-server-security", servers[0].Icon);
        Assert.AreEqual("添加微软账号", sidebar.AddMicrosoft.Title);
        Assert.AreEqual("设备代码流登录正版账号", sidebar.AddMicrosoft.Info);
        Assert.AreEqual("mdi-microsoft", sidebar.AddMicrosoft.Icon);
        Assert.AreEqual("添加离线档案", sidebar.AddOffline.Title);
        Assert.AreEqual("玩家名、标准/旧版/自定义 UUID", sidebar.AddOffline.Info);
        Assert.AreEqual("添加第三方验证服务器档案", sidebar.AuthServerSectionTitle);
        Assert.AreEqual(1, sidebar.AuthServers.Count);
        Assert.AreEqual("添加第三方验证服务器", sidebar.AddAuthServerButtonText);

        Assert.AreEqual("little-skin", selected.ServerId);
        Assert.AreEqual("LittleSkin", selected.ServerName);
        Assert.AreEqual("https://littleskin.cn/api/yggdrasil", selected.ApiRoot);
        StringAssert.Contains(selected.ServerInfoText, "LittleSkin");
        Assert.AreEqual("第三方验证服务器档案", selected.PageTitle);
        Assert.AreEqual("用户名 / 邮箱", selected.LoginNameHint);
        Assert.AreEqual("密码", selected.PasswordHint);
        Assert.AreEqual("登录并创建档案", selected.SubmitButtonText);

        Assert.IsNull(fallback.ServerId);
        Assert.AreEqual("https://littleskin.cn/api/yggdrasil", fallback.ApiRoot);
        Assert.AreEqual("LittleSkin", editor.Name);
        Assert.AreEqual("https://littleskin.cn/auth/register", editor.RegisterUrl);
        Assert.AreEqual("添加第三方验证服务器", editor.PageTitle);
        Assert.AreEqual("服务器名称", editor.NameHint);
        Assert.AreEqual("Yggdrasil API 地址", editor.ApiRootHint);
        Assert.AreEqual("注册网址", editor.RegisterUrlHint);
    }

    [TestMethod]
    public void PageMessagesExposeUiReadyProfileOperationText()
    {
        var service = new PixelProfileListService();

        var messages = service.GetPageMessages();

        Assert.AreEqual("已复制 UUID。", messages.UuidCopied);
        Assert.AreEqual("档案已删除。", messages.ProfileDeleted);
        Assert.AreEqual("离线档案已保存。", messages.OfflineProfileSaved);
        Assert.AreEqual("微软账号已添加。", messages.MicrosoftProfileAdded);
        Assert.AreEqual("第三方验证档案已添加。", messages.AuthlibProfileAdded);
        Assert.AreEqual("第三方验证服务器已添加。", messages.AuthServerAdded);
        Assert.AreEqual("点击登录后会显示微软设备代码，请在浏览器中完成授权。", messages.MicrosoftLoginStatus);
        Assert.AreEqual("使用 Authlib-Injector / Yggdrasil 服务器登录。", messages.AuthlibLoginStatus);
        Assert.AreEqual("输入账号信息后创建第三方验证档案。", messages.AuthlibDialogStatus);
        Assert.AreEqual("创建可直接用于启动的离线档案。", messages.OfflineDialogStatus);
        Assert.AreEqual("保存后会出现在左侧第三方验证服务器列表中。", messages.AuthServerDialogStatus);
        Assert.AreEqual("保存服务器预设", messages.SaveAuthServerButtonText);
        Assert.AreEqual("微软账号", messages.MicrosoftPageTitle);
        Assert.AreEqual("添加微软账号", messages.MicrosoftDialogTitle);
        Assert.AreEqual("登录微软账号", messages.MicrosoftLoginButtonText);
        Assert.AreEqual("取消", messages.CancelButtonText);
        Assert.AreEqual("保存", messages.SaveButtonText);
    }

    [TestMethod]
    public void ProfileOperationFailurePresentationComesFromCore()
    {
        var pageService = new PixelProfilePageService(
            CreateProfileService(),
            new PixelProfileListService(),
            new CapturingCommandBus());

        var failure = pageService.GetOperationFailurePresentation(new InvalidOperationException("boom"));

        Assert.AreEqual("boom", failure.StatusText);
        Assert.AreEqual("boom", failure.HintText);
    }

    [TestMethod]
    public void LoginProgressSnapshotBuildsUiTextAndClampsProgress()
    {
        var service = new PixelProfileListService();
        var progress = new MinecraftProfileLoginProgress("登录", 1.5, "等待授权");

        var snapshot = service.GetLoginProgressSnapshot(progress);

        Assert.AreEqual("登录", snapshot.Stage);
        Assert.AreEqual("等待授权", snapshot.Message);
        Assert.AreEqual(1, snapshot.Progress);
        StringAssert.Contains(snapshot.Text, "登录：等待授权");
        StringAssert.Contains(snapshot.Text, "100");
        Assert.AreEqual(snapshot.Text, service.FormatLoginProgress(progress));
    }

    [TestMethod]
    public void LoginProgressAdapterReportsPixelSnapshot()
    {
        var service = new PixelProfileListService();
        PixelProfileLoginProgressSnapshot? reported = null;
        var adapter = new PixelProfileLoginProgressAdapter(service, snapshot => reported = snapshot);

        adapter.Report(new MinecraftProfileLoginProgress("阶段", 0.25, "消息"));

        Assert.IsNotNull(reported);
        Assert.AreEqual("阶段：消息 (25%)", reported!.Text);
    }

    [TestMethod]
    public void DeviceCodeDialogSnapshotUsesCompleteVerificationUrl()
    {
        var service = new PixelProfileListService();
        var prompt = new DeviceCodePrompt(
            "ABCD-EFGH",
            "https://microsoft.com/devicelogin",
            "https://microsoft.com/devicelogin?code=ABCD-EFGH",
            DateTimeOffset.Parse("2026-01-01T00:10:00Z"));

        var dialog = service.GetDeviceCodeDialogSnapshot(prompt);

        Assert.AreEqual("微软账号登录", dialog.Title);
        StringAssert.Contains(dialog.Markdown, "设备代码：ABCD-EFGH");
        StringAssert.Contains(dialog.Markdown, "https://microsoft.com/devicelogin");
        Assert.AreEqual("继续等待", dialog.WaitButtonText);
        Assert.AreEqual("复制代码", dialog.CopyButtonText);
        Assert.AreEqual("打开网页", dialog.OpenButtonText);
        Assert.AreEqual("ABCD-EFGH", dialog.UserCode);
        Assert.AreEqual("https://microsoft.com/devicelogin?code=ABCD-EFGH", dialog.OpenUrl);
    }

    [TestMethod]
    public void AuthlibProfileChoiceDialogSnapshotComesFromCore()
    {
        var service = new PixelProfileListService();
        var choices = new[]
        {
            new PixelAuthlibProfileChoiceSnapshot("id-1", "Alex"),
            new PixelAuthlibProfileChoiceSnapshot("id-2", "Steve")
        };

        var dialog = service.GetAuthlibProfileChoiceDialogSnapshot(choices);

        Assert.AreEqual("选择第三方验证角色", dialog.Title);
        StringAssert.Contains(dialog.Description, "多个可用角色");
        Assert.AreEqual("使用此角色", dialog.ConfirmButtonText);
        Assert.AreEqual("取消", dialog.CancelButtonText);
        Assert.AreSame(choices, dialog.Choices);
    }

    [TestMethod]
    public async Task ProfileUiCallbacksConvertDomainPromptsToPixelSnapshots()
    {
        var service = new PixelProfileListService();
        PixelProfileDeviceCodeDialogSnapshot? shown = null;
        IReadOnlyList<PixelAuthlibProfileChoiceSnapshot>? choices = null;
        var callbacks = new PixelProfileUiCallbacks(
            service,
            (dialog, _) =>
            {
                shown = dialog;
                return Task.CompletedTask;
            },
            (items, _) =>
            {
                choices = items;
                return Task.FromResult<int?>(1);
            });

        await callbacks.ShowDeviceCodeAsync(
            new DeviceCodePrompt(
                "ABCD-EFGH",
                "https://microsoft.com/devicelogin",
                null,
                DateTimeOffset.Parse("2026-01-01T00:10:00Z")),
            CancellationToken.None);
        var selected = await callbacks.SelectAuthlibProfileAsync(
            [("id-1", "Alex"), ("id-2", "Steve")],
            CancellationToken.None);

        Assert.AreEqual("ABCD-EFGH", shown?.UserCode);
        Assert.AreEqual("https://microsoft.com/devicelogin", shown?.OpenUrl);
        Assert.AreEqual(1, selected);
        Assert.AreEqual(2, choices?.Count);
        Assert.AreEqual("id-2", choices?[1].Id);
        Assert.AreEqual("Steve", choices?[1].Name);
    }

    [TestMethod]
    public async Task ProfilePageServiceBuildsSnapshotsFromProfileService()
    {
        var profileService = CreateProfileService();
        var profile = await profileService.AddOfflineProfileAsync("Alex", OfflineUuidMode.Standard);
        await profileService.SelectProfileAsync(profile);
        var server = await profileService.AddAuthServerAsync(
            "TestServer",
            "https://example.invalid/api/yggdrasil",
            "https://example.invalid/register");
        var pageService = new PixelProfilePageService(
            profileService,
            new PixelProfileListService(),
            new CapturingCommandBus());

        var profiles = pageService.GetProfiles();
        var servers = pageService.GetAuthServers();
        var editor = pageService.GetOfflineEditorSnapshot(profile.Id);
        var serverSnapshot = servers.Single(item => item.Id == server.Id);
        var authlib = pageService.GetAuthlibProfileFormSnapshot(serverSnapshot.Id);

        Assert.AreEqual(1, profiles.Count);
        Assert.IsTrue(profiles[0].IsSelected);
        Assert.AreEqual("Alex", editor.Username);
        Assert.IsTrue(servers.Count >= 1);
        Assert.AreEqual("TestServer", authlib.ServerName);
    }

    [TestMethod]
    public async Task ProfilePageServiceSendsProfileCommandsThroughCommandBus()
    {
        var commandBus = new CapturingCommandBus();
        var pageService = new PixelProfilePageService(CreateProfileService(), new PixelProfileListService(), commandBus);
        var callbacks = new PixelProfileUiCallbacks(
            new PixelProfileListService(),
            (_, _) => Task.CompletedTask);
        var progress = new PixelProfileLoginProgressAdapter(new PixelProfileListService(), _ => { });

        await pageService.SaveOfflineProfileAsync("profile-id", "Alex", PixelOfflineUuidMode.Custom, "uuid");
        await pageService.AddMicrosoftProfileAsync(callbacks, progress);
        await pageService.AddAuthlibProfileAsync("https://example.invalid/api/yggdrasil", "alex", "secret", callbacks, progress);
        await pageService.AddAuthServerAsync("Example", "https://example.invalid/api/yggdrasil", "https://example.invalid/register");
        await pageService.SelectProfileAsync("profile-id");
        await pageService.RemoveProfileAsync("profile-id");

        var saveResult = await pageService.SaveOfflineProfileAsync("profile-id", "Alex", PixelOfflineUuidMode.Standard, null);

        var command = commandBus.SentRequests[0] as SaveOfflineMinecraftProfileByIdCommand;
        Assert.IsNotNull(command);
        Assert.AreEqual("profile-id", command!.ExistingProfileId);
        Assert.AreEqual("Alex", command.Username);
        Assert.AreEqual(PixelOfflineUuidMode.Custom, command.UuidMode);
        Assert.AreEqual("uuid", command.CustomUuid);
        Assert.IsInstanceOfType(commandBus.SentRequests[1], typeof(AddMicrosoftMinecraftProfileCommand));
        Assert.IsInstanceOfType(commandBus.SentRequests[2], typeof(AddAuthlibMinecraftProfileCommand));
        Assert.IsInstanceOfType(commandBus.SentRequests[3], typeof(AddAuthServerPresetCommand));
        Assert.IsInstanceOfType(commandBus.SentRequests[4], typeof(SelectMinecraftProfileByIdCommand));
        Assert.IsInstanceOfType(commandBus.SentRequests[5], typeof(RemoveMinecraftProfileByIdCommand));
        Assert.IsTrue(saveResult.IsSuccess);
        Assert.AreEqual(PixelProfileOperationKind.SaveOfflineProfile, saveResult.Kind);
        Assert.AreEqual("离线档案已保存。", saveResult.HintText);
        Assert.IsInstanceOfType(commandBus.SentRequests[6], typeof(SaveOfflineMinecraftProfileByIdCommand));
    }

    [TestMethod]
    public async Task ProfilePageServiceReturnsOperationFailureSnapshot()
    {
        var commandBus = new CapturingCommandBus
        {
            SendException = new InvalidOperationException("profile save failed")
        };
        var pageService = new PixelProfilePageService(CreateProfileService(), new PixelProfileListService(), commandBus);

        var result = await pageService.SaveOfflineProfileAsync("profile-id", "Alex", PixelOfflineUuidMode.Standard, null);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PixelProfileOperationKind.SaveOfflineProfile, result.Kind);
        Assert.AreEqual("profile save failed", result.StatusText);
        Assert.AreEqual("profile save failed", result.HintText);
    }

    [TestMethod]
    public async Task ProfilePageServiceDrivesProfileStateMachine()
    {
        using var eventBus = new ReactivePixelEventBus();
        var events = new List<PixelStateChangedEvent>();
        using var subscription = eventBus.Observe<PixelStateChangedEvent>()
            .Subscribe(new ListObserver<PixelStateChangedEvent>(events));
        var stateMachine = new PixelProfileStateMachine(
            new PixelStateMachineFactory(eventBus, NullLoggerFactory.Instance));
        var commandBus = new CapturingCommandBus();
        var pageService = new PixelProfilePageService(
            CreateProfileService(),
            new PixelProfileListService(),
            commandBus,
            stateMachine);

        await pageService.SelectProfileAsync("profile-id");
        await pageService.RemoveProfileAsync("profile-id");

        Assert.AreEqual(PixelProfileState.Completed, stateMachine.State);
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(PixelProfileTrigger.StartSelect),
                nameof(PixelProfileTrigger.Succeed),
                nameof(PixelProfileTrigger.StartRemove),
                nameof(PixelProfileTrigger.Succeed)
            },
            events
                .Where(@event => @event.MachineName == "PixelProfile")
                .Select(@event => @event.Trigger)
                .ToArray());
    }

    private static MinecraftProfileService CreateProfileService()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profile-page-test-" + Guid.NewGuid().ToString("N"), "profiles.json");
        return new MinecraftProfileService(profilePath: path);
    }

    private sealed class CapturingCommandBus : IPixelCommandBus
    {
        public object? LastRequest { get; private set; }
        public List<object> SentRequests { get; } = [];
        public Exception? SendException { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            SentRequests.Add(request);
            if (SendException is not null)
                throw SendException;
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            SentRequests.Add(request);
            if (SendException is not null)
                throw SendException;
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ListObserver<T>(ICollection<T> values) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Assert.Fail(error.ToString());
        }

        public void OnNext(T value)
        {
            values.Add(value);
        }
    }
}
