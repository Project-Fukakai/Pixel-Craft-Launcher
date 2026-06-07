using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Minecraft.Profiles;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Routing;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildProfileManagerLeftPage()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(14, 16),
            Spacing = 10
        };

        AddProfileAction(stack, "添加微软账号", "设备代码流登录正版账号", "mdi-microsoft", ShowMicrosoftProfileDialog);
        AddProfileAction(stack, "添加离线档案", "玩家名、标准/旧版/自定义 UUID", "mdi-account-outline", () => ShowOfflineProfileDialog());

        stack.Children.Add(CreateProfileSidebarSubtitle("添加第三方验证服务器档案"));
        foreach (var server in _launchViewModel.ProfileService.AuthServers)
        {
            var item = new MyListItem
            {
                Title = server.Name,
                Info = server.ApiRoot,
                Icon = "mdi-server-security",
                Type = MyListItem.CheckType.Clickable,
                IsSidebarItem = true
            };
            item.Click += (_, _) => ShowAuthlibProfileDialog(server);
            stack.Children.Add(item);
        }
        root.Children.Add(BuildScrollableLeftPane(stack));

        var bottom = new MyButton
        {
            Text = "添加第三方验证服务器",
            PrependIcon = "mdi-server-plus",
            Variant = MyButtonVariant.Tonal,
            Margin = new Thickness(14, 10, 14, 16),
            IsBlock = true
        };
        bottom.Click += (_, _) => ShowAuthServerDialog();
        Grid.SetRow(bottom, 1);
        root.Children.Add(bottom);
        return root;
    }

    private void AddProfileAction(StackPanel stack, string title, string info, string icon, string? action)
    {
        AddProfileAction(stack, title, info, icon, () => _shellViewModel.NavigateProfileManager(action));
    }

    private void AddProfileAction(StackPanel stack, string title, string info, string icon, Action onClick)
    {
        var item = new MyListItem
        {
            Title = title,
            Info = info,
            Icon = icon,
            Type = MyListItem.CheckType.Clickable,
            IsSidebarItem = true
        };
        item.Click += (_, _) => onClick();
        stack.Children.Add(item);
    }

    private static Control CreateProfileSidebarSubtitle(string text) => new TextBlock
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = ThemeBrushes.TextSecondary,
        Margin = new Thickness(2, 8, 2, 0)
    };

    private Control BuildProfileManagerRightPage(RouteNode route)
    {
        var action = route.Parameters.TryGetValue("action", out var value) ? value : null;
        return action switch
        {
            "offline" => BuildOfflineProfileForm(),
            "microsoft" => BuildMicrosoftProfilePage(),
            "authlib" => BuildAuthlibProfileForm(route.Parameters.TryGetValue("server", out var serverId) ? serverId : null),
            "server" => BuildAuthServerForm(),
            _ => BuildProfileListPage()
        };
    }

    private Control BuildProfileListPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard("档案管理", "管理用于启动 Minecraft 的微软账号、离线档案与第三方验证档案。"));

        var list = new StackPanel { Spacing = 8 };
        foreach (var profile in _launchViewModel.ProfileService.Profiles)
            list.Children.Add(BuildProfileItem(profile));

        if (list.Children.Count == 0)
            list.Children.Add(BuildCard("暂无档案", new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateBodyText("请从左侧添加一个账号或离线档案。")
                }
            }));

        stack.Children.Add(list);
        return BuildScrollableMainPane(stack);
    }

    private Control BuildProfileItem(MinecraftProfile profile)
    {
        var selected = ReferenceEquals(profile, _launchViewModel.ProfileService.SelectedProfile);
        var item = new MyListItem
        {
            Title = profile.Username,
            Info = $"{MinecraftProfileService.GetProfileTypeName(profile)} · {profile.Uuid}",
            Type = MyListItem.CheckType.RadioBox,
            Checked = selected,
            Icon = profile.Type switch
            {
                MinecraftProfileType.Microsoft => "mdi-microsoft",
                MinecraftProfileType.AuthlibInjector => "mdi-server-security",
                _ => "mdi-account-outline"
            }
        };
        item.Click += async (_, _) =>
        {
            await _launchViewModel.ProfileService.SelectProfileAsync(profile);
            _launchViewModel.RefreshProfileBindings();
            RefreshProfileManagerPage();
        };

        var copy = new MyIconButton { Icon = "mdi-content-copy", Width = 28, Height = 28 };
        ToolTip.SetTip(copy, "复制 UUID");
        copy.Click += async (_, _) =>
        {
            if (Clipboard is not null)
                await Clipboard.SetTextAsync(profile.Uuid);
            ShowHint("已复制 UUID。", HintType.Finish);
        };
        item.AddButton(copy);

        if (profile.Type == MinecraftProfileType.Offline)
        {
            var edit = new MyIconButton { Icon = "mdi-pencil", Width = 28, Height = 28 };
            ToolTip.SetTip(edit, "编辑离线档案");
            edit.Click += (_, _) => ShowOfflineProfileDialog(profile);
            item.AddButton(edit);
        }

        var delete = new MyIconButton { Icon = "mdi-delete-outline", Width = 28, Height = 28 };
        ToolTip.SetTip(delete, "删除档案");
        delete.Click += async (_, _) =>
        {
            await _launchViewModel.ProfileService.RemoveProfileAsync(profile);
            _launchViewModel.RefreshProfileBindings();
            RefreshProfileManagerPage();
            ShowHint("档案已删除。", HintType.Info);
        };
        item.AddButton(delete);
        return item;
    }

    private Control BuildOfflineProfileForm(MinecraftProfile? editing = null)
    {
        var username = new MyTextBox { HintText = "玩家 ID", Text = editing?.Username ?? "Steve" };
        var uuid = new MyTextBox { HintText = "自定义 UUID（可选）", Text = editing?.Uuid ?? string.Empty };
        var mode = new MyComboBox
        {
            HintText = "UUID 类型",
            ItemsSource = new[] { "标准 UUID", "旧版 PCL UUID", "自定义 UUID" },
            SelectedIndex = editing is null ? 0 : 2
        };
        var save = new MyButton
        {
            Text = editing is null ? "创建离线档案" : "保存离线档案",
            PrependIcon = "mdi-check",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        save.Click += async (_, _) =>
        {
            try
            {
                var selectedMode = mode.SelectedIndex switch
                {
                    1 => OfflineUuidMode.Legacy,
                    2 => OfflineUuidMode.Custom,
                    _ => OfflineUuidMode.Standard
                };
                if (editing is null)
                    await _launchViewModel.ProfileService.AddOfflineProfileAsync(username.Text ?? string.Empty, selectedMode, uuid.Text);
                else
                    await _launchViewModel.ProfileService.UpdateOfflineProfileAsync(editing, username.Text ?? string.Empty, selectedMode, uuid.Text);
                _launchViewModel.RefreshProfileBindings();
                RefreshProfileManagerPage();
                ShowHint("离线档案已保存。", HintType.Finish);
            }
            catch (Exception ex)
            {
                ShowHint(ex.Message, HintType.Critical);
            }
        };

        return BuildProfileFormPage("离线档案", new Control[] { username, mode, uuid, save });
    }

    private Control BuildMicrosoftProfilePage()
    {
        var status = CreateBodyText("点击登录后会显示微软设备代码，请在浏览器中完成授权。");
        var button = new MyButton
        {
            Text = "登录微软账号",
            PrependIcon = "mdi-microsoft",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                var progress = new Progress<MinecraftProfileLoginProgress>(p => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    status.Text = $"{p.Stage}：{p.Message} ({p.Progress:P0})"));
                await _launchViewModel.ProfileService.AddMicrosoftProfileAsync(new ProfilePageCallbacks(this), progress);
                _launchViewModel.RefreshProfileBindings();
                RefreshProfileManagerPage();
                ShowHint("微软账号已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                button.IsEnabled = true;
            }
        };

        return BuildProfileFormPage("微软账号", new Control[] { status, button });
    }

    private Control BuildAuthlibProfileForm(string? serverId = null)
    {
        var presets = _launchViewModel.ProfileService.AuthServers.ToArray();
        var selectedServer = presets.FirstOrDefault(item => item.Id == serverId) ?? presets.FirstOrDefault();
        var serverAddress = selectedServer?.ApiRoot ?? "https://littleskin.cn/api/yggdrasil";
        var serverInfo = CreateBodyText($"验证服务器：{selectedServer?.Name ?? serverAddress}\n{serverAddress}");
        var name = new MyTextBox { HintText = "用户名 / 邮箱" };
        var password = new MyTextBox { HintText = "密码", PasswordChar = '●' };
        var status = CreateBodyText("使用 Authlib-Injector / Yggdrasil 服务器登录。");
        var login = new MyButton
        {
            Text = "登录并创建档案",
            PrependIcon = "mdi-login",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        login.Click += async (_, _) =>
        {
            login.IsEnabled = false;
            try
            {
                var progress = new Progress<MinecraftProfileLoginProgress>(p => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    status.Text = $"{p.Stage}：{p.Message} ({p.Progress:P0})"));
                await _launchViewModel.ProfileService.AddAuthlibProfileAsync(
                    serverAddress,
                    name.Text ?? string.Empty,
                    password.Text ?? string.Empty,
                    new ProfilePageCallbacks(this),
                    progress);
                _launchViewModel.RefreshProfileBindings();
                RefreshProfileManagerPage();
                ShowHint("第三方验证档案已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                login.IsEnabled = true;
            }
        };

        return BuildProfileFormPage("第三方验证服务器档案", new Control[] { serverInfo, name, password, status, login });
    }

    private Control BuildAuthServerForm()
    {
        var name = new MyTextBox { HintText = "服务器名称", Text = "LittleSkin" };
        var api = new MyTextBox { HintText = "Yggdrasil API 地址", Text = "https://littleskin.cn/api/yggdrasil" };
        var register = new MyTextBox { HintText = "注册网址", Text = "https://littleskin.cn/auth/register" };
        var save = new MyButton
        {
            Text = "保存服务器预设",
            PrependIcon = "mdi-server-plus",
            Variant = MyButtonVariant.Flat,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        save.Click += async (_, _) =>
        {
            try
            {
                await _launchViewModel.ProfileService.AddAuthServerAsync(name.Text ?? string.Empty, api.Text ?? string.Empty, register.Text ?? string.Empty);
                RefreshProfileManagerPage();
                ShowHint("第三方验证服务器已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                ShowHint(ex.Message, HintType.Critical);
            }
        };

        return BuildProfileFormPage("添加第三方验证服务器", new Control[] { name, api, register, save });
    }

    private void ShowAuthlibProfileDialog(AuthServerPreset server)
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var name = new MyTextBox { HintText = "用户名 / 邮箱" };
        var password = new MyTextBox { HintText = "密码", PasswordChar = '●' };
        var status = CreateBodyText("输入账号信息后创建第三方验证档案。");
        var dialog = BuildProfileDialog(server.Name, new Control[] { CreateBodyText(server.ApiRoot), name, password, status }, "取消", "登录并创建", "mdi-login");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            dialog.Button1.IsEnabled = false;
            dialog.Button2.IsEnabled = false;
            try
            {
                var progress = new Progress<MinecraftProfileLoginProgress>(p => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    status.Text = $"{p.Stage}：{p.Message} ({p.Progress:P0})"));
                await _launchViewModel.ProfileService.AddAuthlibProfileAsync(
                    server.ApiRoot,
                    name.Text ?? string.Empty,
                    password.Text ?? string.Empty,
                    new ProfilePageCallbacks(this),
                    progress);
                _launchViewModel.RefreshProfileBindings();
                CloseMessage();
                RefreshProfileManagerPage();
                ShowHint("第三方验证档案已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                dialog.Button1.IsEnabled = true;
                dialog.Button2.IsEnabled = true;
            }
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowOfflineProfileDialog(MinecraftProfile? editing = null)
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var username = new MyTextBox { HintText = "玩家 ID", Text = editing?.Username ?? "Steve" };
        var uuid = new MyTextBox { HintText = "自定义 UUID（可选）", Text = editing?.Uuid ?? string.Empty };
        var mode = new MyComboBox
        {
            HintText = "UUID 类型",
            ItemsSource = new[] { "标准 UUID", "旧版 PCL UUID", "自定义 UUID" },
            SelectedIndex = editing is null ? 0 : 2
        };
        var status = CreateBodyText("创建可直接用于启动的离线档案。");
        var dialog = BuildProfileDialog(
            editing is null ? "添加离线档案" : "编辑离线档案",
            new Control[] { username, mode, uuid, status },
            "取消",
            editing is null ? "创建" : "保存",
            "mdi-check");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            dialog.Button1.IsEnabled = false;
            dialog.Button2.IsEnabled = false;
            try
            {
                var selectedMode = mode.SelectedIndex switch
                {
                    1 => OfflineUuidMode.Legacy,
                    2 => OfflineUuidMode.Custom,
                    _ => OfflineUuidMode.Standard
                };
                if (editing is null)
                    await _launchViewModel.ProfileService.AddOfflineProfileAsync(username.Text ?? string.Empty, selectedMode, uuid.Text);
                else
                    await _launchViewModel.ProfileService.UpdateOfflineProfileAsync(editing, username.Text ?? string.Empty, selectedMode, uuid.Text);
                _launchViewModel.RefreshProfileBindings();
                CloseMessage();
                RefreshProfileManagerPage();
                ShowHint("离线档案已保存。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                dialog.Button1.IsEnabled = true;
                dialog.Button2.IsEnabled = true;
            }
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowMicrosoftProfileDialog()
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var status = CreateBodyText("点击登录后会显示微软设备代码，请在浏览器中完成授权。");
        var dialog = BuildProfileDialog("添加微软账号", new Control[] { status }, "取消", "登录微软账号", "mdi-microsoft");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            dialog.Button1.IsEnabled = false;
            dialog.Button2.IsEnabled = false;
            try
            {
                var progress = new Progress<MinecraftProfileLoginProgress>(p => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    status.Text = $"{p.Stage}：{p.Message} ({p.Progress:P0})"));
                await _launchViewModel.ProfileService.AddMicrosoftProfileAsync(new ProfilePageCallbacks(this), progress);
                _launchViewModel.RefreshProfileBindings();
                CloseMessage();
                RefreshProfileManagerPage();
                ShowHint("微软账号已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                dialog.Button1.IsEnabled = true;
                dialog.Button2.IsEnabled = true;
            }
        };

        PanMsg.Children.Add(dialog);
    }

    private void ShowAuthServerDialog()
    {
        PanMsg.Children.Clear();
        PanMsgBackground.IsVisible = true;

        var name = new MyTextBox { HintText = "服务器名称", Text = "LittleSkin" };
        var api = new MyTextBox { HintText = "Yggdrasil API 地址", Text = "https://littleskin.cn/api/yggdrasil" };
        var register = new MyTextBox { HintText = "注册网址", Text = "https://littleskin.cn/auth/register" };
        var status = CreateBodyText("保存后会出现在左侧第三方验证服务器列表中。");
        var dialog = BuildProfileDialog("添加第三方验证服务器", new Control[] { name, api, register, status }, "取消", "保存", "mdi-server-plus");

        dialog.Button2.Click += (_, e) =>
        {
            e.Handled = true;
            CloseMessage();
        };
        dialog.Button1.Click += async (_, e) =>
        {
            e.Handled = true;
            dialog.Button1.IsEnabled = false;
            dialog.Button2.IsEnabled = false;
            try
            {
                await _launchViewModel.ProfileService.AddAuthServerAsync(name.Text ?? string.Empty, api.Text ?? string.Empty, register.Text ?? string.Empty);
                CloseMessage();
                RefreshProfileManagerPage();
                ShowHint("第三方验证服务器已添加。", HintType.Finish);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                ShowHint(ex.Message, HintType.Critical);
            }
            finally
            {
                dialog.Button1.IsEnabled = true;
                dialog.Button2.IsEnabled = true;
            }
        };

        PanMsg.Children.Add(dialog);
    }

    private static MyMsgForm BuildProfileDialog(
        string title,
        IReadOnlyList<Control> fields,
        string cancelText,
        string primaryText,
        string? primaryIcon)
    {
        var dialog = new MyMsgForm { Title = title };
        foreach (var field in fields)
            dialog.ContentPanel.Children.Add(field);
        dialog.Button2.Text = cancelText;
        dialog.Button1.Text = primaryText;
        dialog.Button1.PrependIcon = primaryIcon;
        return dialog;
    }

    private Control BuildProfileFormPage(string title, IReadOnlyList<Control> controls)
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(title, " "));
        var form = new StackPanel { Spacing = 10, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var control in controls)
            form.Children.Add(control);
        stack.Children.Add(BuildCard(title, form));
        return BuildScrollableMainPane(stack);
    }

    private void RefreshProfileManagerPage()
    {
        if (!IsProfileManagerRoute())
            return;
        SetPageHostContent(LeftContentHost, BuildProfileManagerLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildProfileListPage(), PageHostUpdateMode.SilentRefresh);
    }

    private sealed class ProfilePageCallbacks(MainWindow window) : IMinecraftProfileUiCallbacks
    {
        public async Task ShowDeviceCodeAsync(DeviceCodePrompt prompt, CancellationToken cancellationToken)
        {
            await window.Dispatcher.InvokeAsync(() =>
            {
                window.ShowMessageWithActions(
                    "微软账号登录",
                    $"设备代码：{prompt.UserCode}\n\n网页登录地址：{prompt.VerificationUri}\n\n请在浏览器完成授权，完成后保持此窗口打开。",
                    button1: "继续等待",
                    button2: "复制代码",
                    button3: "打开网页",
                    onButton2: async () =>
                    {
                        if (window.Clipboard is not null)
                            await window.Clipboard.SetTextAsync(prompt.UserCode);
                    },
                    onButton3: () =>
                    {
                        var url = prompt.VerificationUriComplete ?? prompt.VerificationUri;
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    });
            });
        }

        public Task<int?> SelectAuthlibProfileAsync(IReadOnlyList<(string Id, string Name)> profiles, CancellationToken cancellationToken)
        {
            return Task.FromResult<int?>(0);
        }
    }
}
