using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.Validate;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Routing;
using Pixel_Craft_Launcher.Services;
using Pixel_Craft_Launcher.Settings;
using Pixel_Craft_Launcher.ViewModels;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private enum GameLinkSubpage
    {
        Eula,
        Select,
        Finish
    }

    private static readonly HttpClient LinkHttpClient = new();
    private readonly List<LinkAnnounceInfo> _linkAnnounces = [];
    private bool _gameLinkToolsInitialized;
    private bool _easyTierAutoInstallStarted;
    private bool _linkAnnouncementLoading;
    private int _linkAnnounceIndex;
    private DispatcherTimer? _linkAnnouncementTimer;
    private GameLinkSubpage _gameLinkSubpage = States.Link.LinkEula ? GameLinkSubpage.Select : GameLinkSubpage.Eula;

    private Control BuildPlaceholderRightPage(MainPageKind page)
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(GetPageTitle(page), GetPageDescription(page)));
        stack.Children.Add(BuildCard("页面承载区", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("这里先保留 Plain 主界面的左右分栏和内容容器，后续可以把真实页面直接放进右侧 ContentControl。"),
                CreateBodyText("Shell 级提示、弹窗遮罩、浮动按钮和窗口按钮已经接入。")
            }
        }));
        stack.Children.Add(BuildCard("迁移备注", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("本轮不接入 PCL.Core，也不迁 MinecraftServer 查询控件。"),
                CreateBodyText("顶部导航当前切换轻量占位页，Tools 页保留控件验收内容。")
            }
        }));

        return BuildScrollableMainPane(stack);
    }

    private Control BuildControlsPreviewPage()
    {
        if (!FeatureVisibilityService.IsToolVisible(PixelToolFeature.GameLink))
        {
            var hiddenStack = CreatePageStack();
            hiddenStack.Children.Add(BuildCard("工具", CreateSubText("联机工具入口已隐藏。")));
            return BuildScrollableMainPane(hiddenStack);
        }

        return BuildGameLinkToolsPage();
    }

    private Control BuildControlsPreviewContentPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard("工具", "临时保留控件验收页，确认基础控件在新 Shell 内仍可显示和交互。"));
        if (!FeatureVisibilityService.IsToolVisible(PixelToolFeature.Test))
        {
            stack.Children.Add(BuildCard("工具", CreateSubText("控件验收入口已隐藏。")));
            return BuildScrollableMainPane(stack);
        }

        stack.Children.Add(BuildButtonsCard());
        stack.Children.Add(BuildInputsCard());
        stack.Children.Add(BuildSelectionCard());
        stack.Children.Add(BuildLoadingAndListCard());
        stack.Children.Add(BuildCard("弹窗与行为", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new MyButton
                {
                    Text = "打开 MyMsgMarkdown",
                    Variant = MyButtonVariant.Flat,
                    HorizontalAlignment = HorizontalAlignment.Left
                }.WithClick((_, _) => ShowMessage("Markdown 消息", "这是 Shell 遮罩层中的消息控件。\n\n正文暂按纯文本换行显示。")),
                CreateLazyLoadExample()
            }
        }));

        return BuildScrollableMainPane(stack);
    }

    private Control BuildGameLinkToolsPage()
    {
        EnsureGameLinkToolsInitialized();

        var stack = CreatePageStack();
        stack.Children.Add(BuildGameLinkAnnouncementHint());

        switch (_gameLinkSubpage)
        {
            case GameLinkSubpage.Eula:
                stack.Children.Add(BuildGameLinkEulaCard());
                break;
            case GameLinkSubpage.Finish:
                stack.Children.Add(BuildGameLinkEasyTierCard());
                stack.Children.Add(BuildGameLinkFinishPanel());
                break;
            default:
                stack.Children.Add(BuildGameLinkEasyTierCard());
                stack.Children.Add(BuildGameLinkAccountToolbar());
                stack.Children.Add(BuildGameLinkJoinCard());
                stack.Children.Add(BuildGameLinkCreateCard());
                break;
        }

        stack.Children.Add(BuildGameLinkFooterCard());

        if (FeatureVisibilityService.IsToolVisible(PixelToolFeature.Test))
        {
            var previewButton = CreateActionButton("控件验收", "mdi-tools");
            previewButton.Click += (_, _) => SetPageHostContent(RightContentHost, BuildControlsPreviewContentPage(), PageHostUpdateMode.SilentRefresh);
            stack.Children.Add(BuildCard("测试入口", new WrapPanel { Children = { previewButton } }));
        }

        return BuildScrollableMainPane(stack);
    }

    private void EnsureGameLinkToolsInitialized()
    {
        if (_gameLinkToolsInitialized)
            return;
        _gameLinkToolsInitialized = true;

        LobbyService.OnNeedDownloadEasyTier += () => _ = EnsureEasyTierInstalledFromToolsAsync(true);
        EasyTierDependencyService.StateChanged += (_, _) => RefreshToolsGameLinkPage(true);
        LobbyService.DiscoveredWorlds.CollectionChanged += (_, _) => RefreshToolsGameLinkPage();
        LobbyService.Players.CollectionChanged += (_, _) => RefreshToolsGameLinkPage();
        LobbyService.OnClientPing += _ => RefreshToolsGameLinkPage();
        LobbyService.OnServerStarted += () =>
        {
            _gameLinkSubpage = GameLinkSubpage.Finish;
            RefreshToolsGameLinkPage(true);
        };
        LobbyService.OnServerShutDown += () =>
        {
            _gameLinkSubpage = GameLinkSubpage.Select;
            RefreshToolsGameLinkPage(true);
        };
        LobbyService.OnUserStopGame += () =>
        {
            _gameLinkSubpage = GameLinkSubpage.Select;
            ShowMessage("大厅已解散", "由于你关闭了联机中的 Minecraft 实例，大厅已自动解散。");
            RefreshToolsGameLinkPage(true);
        };
        LobbyService.OnServerException += ex =>
        {
            _gameLinkSubpage = GameLinkSubpage.Select;
            ShowHint(ex.Message, HintType.Critical);
            RefreshToolsGameLinkPage(true);
        };

        _ = InitializeGameLinkAsync();
    }

    private async Task InitializeGameLinkAsync()
    {
        await LoadLinkAnnouncementAsync();
        await LobbyService.InitializeAsync();
        TryStartEasyTierAutoInstall();
        RefreshToolsGameLinkPage();
    }

    private void TryStartEasyTierAutoInstall()
    {
        if (_easyTierAutoInstallStarted || !States.Link.LinkEula || !EasyTierMetadata.IsPlatformSupported)
            return;

        if (EasyTierDependencyService.State is EasyTierDependencyState.Installed or EasyTierDependencyState.Installing)
            return;

        _easyTierAutoInstallStarted = true;
        _ = EnsureEasyTierInstalledFromToolsAsync(false);
    }

    private Control BuildGameLinkAnnouncementHint()
    {
        var hint = new MyHint
        {
            CanClose = false,
            Theme = MyHint.Themes.Blue,
            Text = _linkAnnouncementLoading ? "正在连接到大厅服务器..." : "大厅服务正在初始化..."
        };

        if (_linkAnnounces.Count > 0)
        {
            var info = _linkAnnounces[Math.Clamp(_linkAnnounceIndex, 0, _linkAnnounces.Count - 1)];
            hint.Theme = info.Type switch
            {
                LinkAnnounceType.Important => MyHint.Themes.Red,
                LinkAnnounceType.Warning => MyHint.Themes.Yellow,
                _ => MyHint.Themes.Blue
            };
            var prefix = info.Type switch
            {
                LinkAnnounceType.Important => "重要",
                LinkAnnounceType.Warning => "注意",
                _ => "提示"
            };
            hint.Text = $"[{prefix}] {info.Content}";
        }
        else if (!LobbyInfoProvider.IsLobbyAvailable && !_linkAnnouncementLoading)
        {
            hint.Theme = MyHint.Themes.Red;
            hint.Text = "大厅功能暂不可用，请稍后再试。";
        }

        return hint;
    }

    private MyCard BuildGameLinkEulaCard()
    {
        var agree = CreateActionButton("我已阅读并同意", "mdi-check");
        agree.Click += (_, _) =>
        {
            States.Link.LinkEula = true;
            _gameLinkSubpage = GameLinkSubpage.Select;
            TryStartEasyTierAutoInstall();
            RefreshToolsGameLinkPage(true);
        };

        return BuildCard("PCL CE 联机大厅说明与条款", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateBodyText("使用大厅功能即代表你同意相关服务文档及下列条款。"),
                BuildExternalLinkItem("PCL CE 大厅相关隐私政策", "了解 PCL CE 如何处理个人信息", "https://www.pclc.cc/privacy/personal-info-brief.html"),
                BuildExternalLinkItem("Natayark Network 用户协议与隐私政策", "查看 Natayark OpenID 服务条款", "https://account.naids.com/policy"),
                CreateBodyText("我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。\n我承诺使用大厅功能带来的一切风险自行承担。\n我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他必要信息。\n为保护未成年人个人信息，使用联机大厅前，我确认我已满十四周岁。"),
                new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { agree } }
            }
        });
    }

    private Control BuildGameLinkAccountToolbar()
    {
        var natText = string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken)
            ? "点击登录 Natayark 账户"
            : string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username)
                ? "Natayark 账户信息待刷新"
                : NatayarkProfileManager.NaidProfile.Status == 0
                    ? NatayarkProfileManager.NaidProfile.Username!
                    : $"{NatayarkProfileManager.NaidProfile.Username}（状态异常）";

        var natButton = CreateActionButton(natText, "mdi-account-circle-outline");
        natButton.Click += async (_, _) => await HandleNatayarkLoginClickAsync(natButton);

        var natTest = CreateActionButton("NAT 测试", "mdi-earth");
        natTest.Click += async (_, _) => await RunToolsNatTestAsync(natTest);

        return new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                natTest,
                natButton
            }
        };
    }

    private MyCard BuildGameLinkEasyTierCard()
    {
        var state = EasyTierMetadata.IsInstalled()
            ? EasyTierDependencyState.Installed
            : EasyTierDependencyService.State;
        var stateText = state switch
        {
            EasyTierDependencyState.Installed => "已安装，联机功能可直接使用。",
            EasyTierDependencyState.Installing => "正在自动安装 EasyTier 依赖，请稍候...",
            EasyTierDependencyState.Failed => "安装失败：" + (EasyTierDependencyService.LastError ?? "未知错误"),
            EasyTierDependencyState.Unsupported => EasyTierMetadata.GetUnsupportedReason(),
            _ => "尚未安装，将在进入联机流程时自动下载并安装。"
        };

        var install = CreateActionButton(state == EasyTierDependencyState.Failed ? "重试安装" : "立即安装", "mdi-download");
        install.IsEnabled = state is EasyTierDependencyState.NotInstalled or EasyTierDependencyState.Failed;
        install.Click += async (_, _) => await EnsureEasyTierInstalledFromToolsAsync(true);

        var check = CreateActionButton("重新检测", "mdi-refresh");
        check.Click += (_, _) =>
        {
            if (EasyTierMetadata.IsInstalled())
                ShowHint("EasyTier 依赖已就绪。", HintType.Finish);
            else
                ShowHint(EasyTierMetadata.IsPlatformSupported ? "尚未检测到完整 EasyTier 依赖。" : EasyTierMetadata.GetUnsupportedReason(), HintType.Info);
            RefreshToolsGameLinkPage(true);
        };

        return BuildCard("EasyTier 依赖", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText(stateText),
                CreateSubText(EasyTierMetadata.IsPlatformSupported
                    ? $"平台包: {EasyTierMetadata.Platform.PlatformId}\n安装目录: {EasyTierMetadata.EasyTierFilePath}"
                    : EasyTierMetadata.GetUnsupportedReason()),
                new WrapPanel { Children = { install, check } }
            }
        });
    }

    private MyCard BuildGameLinkJoinCard()
    {
        var input = new MyTextBox
        {
            HintText = "编号应该长得像这样：U/NNNN-AAAA-SSSS-EEEE",
            MinWidth = 260,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var join = CreateActionButton("加入", "mdi-login");
        join.Click += async (_, _) => await JoinLobbyAsync(input.Text ?? string.Empty, join);

        var paste = CreateActionButton("粘贴", "mdi-clipboard-text-outline");
        paste.Click += async (_, _) =>
        {
            input.Text = await GetClipboardTextCompatAsync();
        };

        var clear = CreateActionButton("清除", "mdi-close");
        clear.Click += (_, _) => input.Text = string.Empty;

        var joinActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { clear, paste, join }
        };
        DockPanel.SetDock(joinActions, Dock.Right);

        return BuildCard("加入大厅", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateBodyText("1、输入朋友发送给你的大厅编号，单击“加入”。\n2、启动游戏，选择“多人游戏”，在局域网游戏中加入大厅。"),
                new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        joinActions,
                        input
                    }
                }
            }
        });
    }

    private MyCard BuildGameLinkCreateCard()
    {
        var combo = new MyComboBox
        {
            MinWidth = 260,
            IsEnabled = LobbyService.DiscoveredWorlds.Count > 0
        };
        foreach (var world in LobbyService.DiscoveredWorlds)
            combo.Items.Add(new MyComboBoxItem { Content = world.Name, Tag = world.Port });
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;

        var create = CreateActionButton("创建", "mdi-plus-circle-outline");
        create.IsEnabled = combo.Items.Count > 0;
        create.Click += async (_, _) =>
        {
            if (combo.SelectedItem is MyComboBoxItem { Tag: int port })
                await CreateLobbyAsync(port, create);
        };

        var refresh = CreateActionButton("刷新", "mdi-refresh");
        refresh.Click += async (_, _) =>
        {
            await LobbyService.DiscoverWorldAsync();
            RefreshToolsGameLinkPage();
        };

        var manual = CreateActionButton("手动输入", "mdi-keyboard-outline");
        manual.Click += (_, _) => ShowManualPortDialog();

        var createActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { manual, refresh, create }
        };
        DockPanel.SetDock(createActions, Dock.Right);

        return BuildCard("创建大厅", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateBodyText("1、进入世界后，在游戏菜单中选择“对局域网开放”。\n2、在下方选择此游戏实例，单击“创建”。\n3、成功创建大厅后，复制大厅编号并发送给朋友。"),
                new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        createActions,
                        combo
                    }
                }
            }
        });
    }

    private Control BuildGameLinkFinishPanel()
    {
        var isHost = LobbyService.IsHost;
        var userType = isHost ? "创建者" : "加入者";
        var lobbyCode = LobbyService.CurrentLobbyCode ?? "正在获取";
        var userName = LobbyService.CurrentUserName ?? LobbyInfoProvider.GetUsername() ?? "未知用户";

        var copyCode = CreateActionButton("复制大厅编号", "mdi-content-copy");
        copyCode.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(LobbyService.CurrentLobbyCode))
                await SetClipboardTextCompatAsync(LobbyService.CurrentLobbyCode);
            ShowHint("已复制大厅编号。", HintType.Finish);
        };

        var copyIp = CreateActionButton("复制虚拟 IP", "mdi-ip-network-outline");
        copyIp.IsEnabled = !isHost && LobbyInfoProvider.McForward is not null;
        copyIp.Click += async (_, _) =>
        {
            var ip = $"127.0.0.1:{LobbyInfoProvider.McForward?.LocalPort}";
            ShowMessageWithActions("复制 IP",
                $"大厅创建者的游戏地址：{ip}\n注意：仅推荐在 MC 多人游戏列表不显示大厅广播时使用 IP 连接。",
                button1: "复制",
                button2: "返回",
                onButton1: async () =>
                {
                    await SetClipboardTextCompatAsync(ip);
                });
        };

        var exit = CreateActionButton(isHost ? "关闭大厅" : "退出大厅", "mdi-exit-run");
        exit.Click += async (_, _) => await LeaveLobbyAsync();

        var left = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                BuildCard("大厅信息", new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        CreateBodyText("连接状况: " + (LobbyService.CurrentState == LobbyState.Connected ? "已连接" : LobbyService.CurrentState.ToString())),
                        CreateBodyText("大厅编号: " + lobbyCode),
                        CreateBodyText("用户名: " + userName),
                        CreateBodyText("用户类型: " + userType)
                    }
                }),
                BuildCard("大厅操作", new WrapPanel { Children = { copyIp, copyCode, exit } })
            }
        };

        var players = new StackPanel { Spacing = 4 };
        if (LobbyService.Players.Count == 0)
        {
            players.Children.Add(CreateSubText("正在获取信息"));
        }
        else
        {
            foreach (var player in LobbyService.Players)
                players.Children.Add(BuildPlayerListItem(player));
        }

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            },
            Children =
            {
                left,
                new MyCard
                {
                    Title = $"大厅成员列表（共 {LobbyService.Players.Count} 人）",
                    Margin = new Thickness(10, 0, 0, 0),
                    CardContent = players,
                    MinWidth = 300,
                    [Grid.ColumnProperty] = 1
                }
            }
        };
    }

    private MyListItem BuildPlayerListItem(PlayerProfile player)
    {
        var item = new MyListItem
        {
            Title = player.Name,
            Info = (player.Kind == PlayerKind.HOST ? "[主机] " : string.Empty) + player.Vendor,
            Type = MyListItem.CheckType.Clickable,
            Icon = player.Kind == PlayerKind.HOST ? "mdi-crown-outline" : "mdi-account-outline"
        };
        item.Click += (_, _) =>
        {
            ShowMessage($"玩家 {player.Name} 的详细信息",
                $"用户名：{player.Name}\n联机协议客户端标识：{player.Vendor}\n此处数据仅供参考，请以实际游玩体验为准。");
        };
        return item;
    }

    private MyCard BuildGameLinkFooterCard()
    {
        var links = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        links.Children.Add(CreateTextLink("违法违规举报", "https://qm.qq.com/q/zfml1KaPWS"));
        links.Children.Add(CreateFooterSeparator());
        links.Children.Add(CreateTextLink("Natayark Network 用户协议与隐私政策", "https://account.naids.com/policy"));
        links.Children.Add(CreateFooterSeparator());
        links.Children.Add(CreateTextLink("大厅隐私协议", "https://www.pclc.cc/privacy/personal-info-brief.html"));
        links.Children.Add(CreateFooterSeparator());

        var stop = new MyTextButton { Text = "停用联机功能" };
        stop.Click += (_, _) =>
        {
            ShowMessageWithActions("撤销授权确认", "你确定要撤销联机协议授权吗？", true,
                button1: "确定",
                button2: "取消",
                onButton1: () =>
                {
                    States.Link.NaidRefreshToken = string.Empty;
                    States.Link.LinkEula = false;
                    _gameLinkSubpage = GameLinkSubpage.Eula;
                    RefreshToolsGameLinkPage();
                });
        };
        links.Children.Add(stop);

        return BuildCard("联机服务", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                links,
                new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        CreateSubText("友情链接："),
                        CreateTextLink("EasyTier 工具官网", "https://easytier.cn/"),
                        CreateFooterSeparator(),
                        CreateTextLink("Pysio's Home", "https://pysio.online/")
                    }
                }
            }
        });
    }

    private MyListItem BuildExternalLinkItem(string title, string info, string url)
    {
        var item = new MyListItem
        {
            Title = title,
            Info = info,
            Type = MyListItem.CheckType.Clickable,
            Icon = "mdi-open-in-new"
        };
        item.Click += (_, _) => OpenExternalUrl(url);
        return item;
    }

    private MyTextButton CreateTextLink(string text, string url)
    {
        var button = new MyTextButton { Text = text };
        button.Click += (_, _) => OpenExternalUrl(url);
        return button;
    }

    private static TextBlock CreateFooterSeparator() => new()
    {
        Text = " | ",
        Foreground = SubForeground,
        VerticalAlignment = VerticalAlignment.Center
    };

    private async Task HandleNatayarkLoginClickAsync(Control anchor)
    {
        anchor.IsEnabled = false;
        try
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                ShowHint("请在浏览器中完成 Natayark 登录。", HintType.Info);
                if (await NatayarkOAuthService.StartLoginAsync())
                    ShowHint("已完成登录操作。", HintType.Finish);
                else
                    ShowHint("Natayark 登录未完成。", HintType.Critical);
            }
            else
            {
                States.Link.NaidRefreshToken = string.Empty;
                ShowHint("已退出 Natayark 登录。", HintType.Finish);
            }
        }
        catch (Exception ex)
        {
            ShowHint("Natayark 登录失败：" + ex.Message, HintType.Critical);
        }
        finally
        {
            anchor.IsEnabled = true;
            RefreshToolsGameLinkPage();
        }
    }

    private async Task RunToolsNatTestAsync(MyButton button)
    {
        button.IsEnabled = false;
        var oldText = button.Text;
        button.Text = "正在测试";
        try
        {
            if (!await EnsureEasyTierInstalledFromToolsAsync(true))
            {
                ShowHint(EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。", HintType.Critical);
                return;
            }

            var status = await CliNetTest.GetNetStatusAsync();
            if (status is null)
            {
                ShowHint("NAT 测试失败。", HintType.Critical);
                return;
            }

            ShowMessage("NAT 类型",
                $"UDP NAT 类型: {CliNetTest.GetNatTypeString(status.UdpNatType)}\nTCP NAT 类型: {CliNetTest.GetNatTypeString(status.TcpNatType)}\nIPv6: {(status.SupportIPv6 ? "支持" : "不支持")}");
        }
        finally
        {
            button.Text = oldText;
            button.IsEnabled = true;
        }
    }

    private async Task<bool> LobbyPrecheckAsync()
    {
        if (!States.Link.LinkEula)
        {
            _gameLinkSubpage = GameLinkSubpage.Eula;
            RefreshToolsGameLinkPage(true);
            return false;
        }

        if (!LobbyInfoProvider.IsLobbyAvailable)
        {
            ShowHint("大厅功能暂不可用，请稍后再试。", HintType.Critical);
            return false;
        }

        if (LobbyInfoProvider.RequiresLogin)
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                ShowHint("请先登录 Natayark Network 再进行联机。", HintType.Critical);
                return false;
            }

            try
            {
                await NatayarkProfileManager.GetNaidDataAsync(States.Link.NaidRefreshToken, true);
            }
            catch
            {
                ShowHint("请重新登录 Natayark Network 账号再试。", HintType.Critical);
                return false;
            }

            if (LobbyInfoProvider.RequiresRealName && !NatayarkProfileManager.NaidProfile.IsRealNamed)
            {
                ShowHint("请先前往 Natayark 账户中心进行实名验证再尝试操作。", HintType.Critical);
                return false;
            }

            if (NatayarkProfileManager.NaidProfile.Status != 0)
            {
                ShowHint("你的 Natayark Network 账号状态异常，可能已被封禁。", HintType.Critical);
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(LobbyInfoProvider.GetUsername()))
        {
            ShowHint("请先在设置中输入大厅用户名，或登录 Natayark Network。", HintType.Critical);
            return false;
        }

        if (!await EnsureEasyTierInstalledFromToolsAsync(true))
        {
            ShowHint(EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。", HintType.Critical);
            return false;
        }

        return true;
    }

    private async Task<bool> EnsureEasyTierInstalledFromToolsAsync(bool showResult)
    {
        RefreshToolsGameLinkPage(true);
        var ok = await EasyTierDependencyService.EnsureInstalledAsync();
        if (showResult)
        {
            ShowHint(ok ? "EasyTier 依赖已就绪。" : EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。",
                ok ? HintType.Finish : HintType.Critical);
        }
        RefreshToolsGameLinkPage(true);
        return ok;
    }

    private async Task CreateLobbyAsync(int port, MyButton button)
    {
        button.IsEnabled = false;
        try
        {
            if (!await LobbyPrecheckAsync())
                return;

            _gameLinkSubpage = GameLinkSubpage.Finish;
            RefreshToolsGameLinkPage(true);

            var username = LobbyInfoProvider.GetUsername() ?? Config.Link.Username;
            var ok = await LobbyService.CreateLobbyAsync(port, username);
            if (!ok)
            {
                _gameLinkSubpage = GameLinkSubpage.Select;
                RefreshToolsGameLinkPage(true);
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task JoinLobbyAsync(string lobbyCode, MyButton button)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            ShowHint("请输入大厅编号。", HintType.Critical);
            return;
        }

        button.IsEnabled = false;
        try
        {
            if (!await LobbyPrecheckAsync())
                return;

            _gameLinkSubpage = GameLinkSubpage.Finish;
            RefreshToolsGameLinkPage(true);

            var username = LobbyInfoProvider.GetUsername() ?? Config.Link.Username;
            var ok = await LobbyService.JoinLobbyAsync(lobbyCode, username);
            if (!ok)
            {
                _gameLinkSubpage = GameLinkSubpage.Select;
                RefreshToolsGameLinkPage(true);
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task LeaveLobbyAsync()
    {
        _gameLinkSubpage = GameLinkSubpage.Select;
        RefreshToolsGameLinkPage(true);
        await LobbyService.LeaveLobbyAsync();
    }

    private void ShowManualPortDialog()
    {
        var input = new MyTextBox
        {
            HintText = "1024-65535",
            Width = 220
        };
        input.ValidateRules.Add(text => int.TryParse(text, out var port) && port is >= 1024 and <= 65535 ? null : "请输入 1024-65535 之间的端口");

        var dialog = ShowFormDialog("手动输入端口", "创建", "取消");
        dialog.ContentPanel.Children.Add(input);
        dialog.Button1Click += async (_, _) =>
        {
            if (!int.TryParse(input.Text, out var port) || port is < 1024 or > 65535)
            {
                ShowHint("请输入有效端口。", HintType.Critical);
                return;
            }

            await CreateLobbyAsync(port, dialog.Button1);
        };
    }

    private async Task LoadLinkAnnouncementAsync()
    {
        if (_linkAnnouncementLoading)
            return;

        _linkAnnouncementLoading = true;
        RefreshToolsGameLinkPage();

        try
        {
            var servers = Secrets.LinkServers.Where(server => !string.IsNullOrWhiteSpace(server)).ToArray();
            if (servers.Length == 0)
                throw new InvalidOperationException("未配置联机服务根地址。");

            JsonNode? root = null;
            foreach (var server in servers)
            {
                try
                {
                    var normalized = server.TrimEnd('/');
                    var cacheText = await LinkHttpClient.GetStringAsync($"{normalized}/api/link/v2/cache.ini");
                    var cacheVer = int.Parse(cacheText.Trim());
                    string json;
                    if (cacheVer == States.Link.AnnounceCacheVer && !string.IsNullOrWhiteSpace(States.Link.AnnounceCache))
                    {
                        json = States.Link.AnnounceCache;
                    }
                    else
                    {
                        json = await LinkHttpClient.GetStringAsync($"{normalized}/api/link/v2/announce.json");
                        States.Link.AnnounceCache = json;
                        States.Link.AnnounceCacheVer = cacheVer;
                    }

                    root = JsonNode.Parse(json);
                    break;
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Link", $"获取大厅公告失败: {server}");
                }
            }

            if (root is null)
                throw new InvalidOperationException("无法连接大厅服务器。");

            LobbyInfoProvider.IsLobbyAvailable = root["available"]?.GetValue<bool>() ?? false;
            LobbyInfoProvider.AllowCustomName = root["allowCustomName"]?.GetValue<bool>() ?? false;
            LobbyInfoProvider.RequiresLogin = root["requireLogin"]?.GetValue<bool>() ?? true;
            LobbyInfoProvider.RequiresRealName = root["requireRealname"]?.GetValue<bool>() ?? true;

            _linkAnnounces.Clear();
            if (root["notices"] is JsonArray notices)
            {
                foreach (var node in notices.OfType<JsonObject>())
                {
                    var content = node["content"]?.ToString();
                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    var minVer = node["minVer"]?.GetValue<double>() ?? double.MinValue;
                    var maxVer = node["maxVer"]?.GetValue<double>() ?? double.MaxValue;
                    if (Basics.VersionCode < minVer || Basics.VersionCode > maxVer)
                        continue;

                    var typeText = node["type"]?.ToString().ToLowerInvariant();
                    var type = typeText is "important" or "red"
                        ? LinkAnnounceType.Important
                        : typeText is "warning" or "yellow"
                            ? LinkAnnounceType.Warning
                            : LinkAnnounceType.Notice;

                    foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        _linkAnnounces.Add(new LinkAnnounceInfo(type, line.Trim()));
                }
            }

            ETRelay.RelayList = [];
            if (root["relays"] is JsonArray relays)
            {
                foreach (var relay in relays.OfType<JsonObject>())
                {
                    var url = relay["url"]?.ToString();
                    if (string.IsNullOrWhiteSpace(url))
                        continue;
                    ETRelay.RelayList.Add(new ETRelay
                    {
                        Name = relay["name"]?.ToString() ?? url,
                        Url = url,
                        Type = relay["type"]?.ToString() == "official" ? ETRelayType.Selfhosted : ETRelayType.Community
                    });
                }
            }

            StartLinkAnnouncementTimer();
        }
        catch (Exception ex)
        {
            LobbyInfoProvider.IsLobbyAvailable = false;
            _linkAnnounces.Clear();
            _linkAnnounces.Add(new LinkAnnounceInfo(LinkAnnounceType.Important, "连接大厅服务器失败。"));
            LogWrapper.Error(ex, "Link", "获取大厅公告失败。");
        }
        finally
        {
            _linkAnnouncementLoading = false;
            RefreshToolsGameLinkPage();
        }
    }

    private void StartLinkAnnouncementTimer()
    {
        if (_linkAnnouncementTimer is not null)
            return;

        _linkAnnouncementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _linkAnnouncementTimer.Tick += (_, _) =>
        {
            if (SelectedMainPage != MainPageKind.Tools || _linkAnnounces.Count == 0)
                return;
            _linkAnnounceIndex = (_linkAnnounceIndex + 1) % _linkAnnounces.Count;
            RefreshToolsGameLinkPage();
        };
        _linkAnnouncementTimer.Start();
    }

    private void RefreshToolsGameLinkPage(bool refreshLeft = false)
    {
        if (SelectedMainPage != MainPageKind.Tools)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedMainPage == MainPageKind.Tools)
            {
                if (refreshLeft)
                    SetPageHostContent(LeftContentHost, BuildLeftPage(MainPageKind.Tools), PageHostUpdateMode.SilentRefresh);
                SetPageHostContent(RightContentHost, BuildGameLinkToolsPage(), PageHostUpdateMode.SilentRefresh);
            }
        }, DispatcherPriority.Background);
    }

    private async Task<string?> GetClipboardTextCompatAsync()
    {
        var clipboard = Clipboard;
        if (clipboard is null)
            return null;

        var asyncMethod = clipboard.GetType().GetMethod("GetTextAsync", BindingFlags.Instance | BindingFlags.Public);
        if (asyncMethod?.Invoke(clipboard, null) is Task<string?> asyncResult)
            return await asyncResult;

        var syncMethod = clipboard.GetType().GetMethod("GetText", BindingFlags.Instance | BindingFlags.Public);
        return syncMethod?.Invoke(clipboard, null) as string;
    }

    private async Task SetClipboardTextCompatAsync(string text)
    {
        var clipboard = Clipboard;
        if (clipboard is null)
            return;

        var asyncMethod = clipboard.GetType().GetMethod("SetTextAsync", BindingFlags.Instance | BindingFlags.Public);
        if (asyncMethod?.Invoke(clipboard, [text]) is Task asyncResult)
        {
            await asyncResult;
            return;
        }

        var syncMethod = clipboard.GetType().GetMethod("SetText", BindingFlags.Instance | BindingFlags.Public);
        syncMethod?.Invoke(clipboard, [text]);
    }

    private Control BuildButtonsCard()
    {
        return BuildCard("按钮", new WrapPanel
        {
            Children =
            {
                new MyButton { Text = "Tonal", Variant = MyButtonVariant.Tonal, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Text", Variant = MyButtonVariant.Text, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Outlined", Variant = MyButtonVariant.Outlined, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Elevated", Variant = MyButtonVariant.Elevated, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Flat", Variant = MyButtonVariant.Flat, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Prepend", PrependIcon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Append", AppendIcon = "mdi-arrow-right", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Stacked", Icon = "mdi-apps", Stacked = true, Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyIconButton { Icon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Small", Size = MyButtonSize.Small, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Large", Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Disabled", IsEnabled = false, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Readonly", IsReadOnly = true, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Block", IsBlock = true, Width = 220, Margin = new Thickness(0, 0, 10, 10) }
            }
        });
    }

    private Control BuildInputsCard()
    {
        var searchBox = new MySearchBox { HintText = "搜索控件", Width = 240 };
        var searchResult = CreateBodyText("等待搜索");
        searchBox.Search += (_, _) => searchResult.Text = $"搜索：{searchBox.Text ?? string.Empty}";

        var textBox = new MyTextBox { HintText = "安全剪贴板 TextBox", Width = 240 };
        ClipboardInterceptor.SetEnableSafeClipboard(textBox, true);

        var comboBox = new MyComboBox
        {
            HintText = "下拉选择",
            Width = 240,
            ItemsSource = new[] { "默认", "轻量", "完整" },
            SelectedIndex = 0
        };

        return BuildCard("输入", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                searchBox,
                searchResult,
                textBox,
                comboBox,
                new FontSelector { Width = 280, Tooltip = "系统字体" },
                new MySlider { Minimum = 0, Maximum = 100, Value = 35, Width = 280 }
            }
        });
    }

    private Control BuildSelectionCard()
    {
        var checkBox = new MyCheckBox { Text = "CheckBox Checked alias", Checked = true };
        var radioBoxA = new MyRadioBox { Text = "RadioBox A", Checked = true };
        var radioBoxB = new MyRadioBox { Text = "RadioBox B" };
        var radioButtonA = new MyRadioButton { Text = "启动", Checked = true, Icon = "mdi-play" };
        var radioButtonB = new MyRadioButton { Text = "下载", Icon = "mdi-download" };

        return BuildCard("选择控件", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                checkBox,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { radioBoxA, radioBoxB } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { radioButtonA, radioButtonB } },
                new MyHint { Text = "提示控件支持 Blue / Yellow / Red 主题。", Theme = MyHint.Themes.Yellow, CanClose = false }
            }
        });
    }

    private Control BuildLoadingAndListCard()
    {
        var loading = new MyLoading
        {
            Text = "Loading",
            State = _loadingState,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var listItem = new MyListItem
        {
            Title = "MyListItem",
            Info = "图标、按钮区、选中条与二级信息",
            Type = MyListItem.CheckType.CheckBox,
            Logo = Geometry.Parse("M3,3 L17,3 L17,17 L3,17 Z")
        };
        listItem.AddButton(new MyIconButton
        {
            Icon = "mdi-check",
            IconSize = 12,
            Width = 28,
            Height = 28
        });

        return BuildCard("加载与列表", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new MyButton { Text = "Loading" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Loading),
                        new MyButton { Text = "Stop" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Stop),
                        new MyButton { Text = "Error", Variant = MyButtonVariant.Outlined, IsDanger = true }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Error)
                    }
                },
                loading,
                BuildM3LoadingIndicatorPreview(),
                listItem
            }
        });
    }

    private static Control BuildM3LoadingIndicatorPreview()
    {
        static Control CreatePreview(string title, MyM3LoadingIndicator indicator)
        {
            return new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 0, 18, 8),
                Children =
                {
                    indicator,
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 12,
                        Foreground = BodyForeground,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
        }

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("Material 3 Loading Indicator"),
                new WrapPanel
                {
                    Children =
                    {
                        CreatePreview("Primary", new MyM3LoadingIndicator
                        {
                            Size = 38,
                            Color = ThemeBrushes.Primary
                        }),
                        CreatePreview("Contained", new MyM3LoadingIndicator
                        {
                            Size = 56,
                            Contained = true,
                            Color = ThemeBrushes.OnPrimaryContainer,
                            ContainerColor = ThemeBrushes.PrimaryContainer
                        }),
                        CreatePreview("Error", new MyM3LoadingIndicator
                        {
                            Size = 46,
                            IsError = true,
                            Color = ThemeBrushes.Primary,
                            ErrorColor = ThemeBrushes.Error
                        })
                    }
                }
            }
        };
    }

    private Control CreateLazyLoadExample()
    {
        var marker = new TextBlock
        {
            Text = "LazyLoad 示例：滚入视口后触发",
            Foreground = BodyForeground,
            Margin = new Thickness(0, 20, 0, 0)
        };
        LazyLoadBehavior.SetAction(marker, () => marker.Text = "LazyLoad 已触发一次");
        return marker;
    }

    private static StackPanel CreatePageStack()
    {
        return new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 26),
            Spacing = 14
        };
    }

    private static MyCard BuildHeroCard(string title, string description)
    {
        return BuildCard(title, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = description,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = BodyForeground
                }
            }
        });
    }

    private static MyCard BuildCard(string title, Control content)
    {
        return new MyCard
        {
            Title = title,
            Margin = new Thickness(0, 0, 0, 2),
            CardContent = content
        };
    }

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BodyForeground,
            LineHeight = 22
        };
    }

    private static string GetPageTitle(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => "下载",
            MainPageKind.Setup => "设置",
            MainPageKind.Tools => "工具",
            _ => "启动"
        };
    }

    private static string GetPageDescription(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => "版本下载、资源补全和任务队列会在这里逐步接入。",
            MainPageKind.Setup => "启动器设置、游戏设置和账户设置先使用占位承载。",
            MainPageKind.Tools => "调试与控件验收入口，后续也可承载日志、导入和维护工具。",
            _ => "启动页 Shell 已就绪，后续迁入账户、版本选择和启动按钮逻辑。"
        };
    }

    private static (string title, string info, string icon, bool active)[] GetLeftItems(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => new[]
            {
                ("版本列表", "Minecraft / Mod Loader", "mdi-cube-outline", true),
                ("下载任务", "队列与进度", "mdi-download", false),
                ("资源补全", "库文件与资产", "mdi-package-variant", false)
            },
            MainPageKind.Setup => new[]
            {
                ("启动器", "主题与窗口", "mdi-cog-outline", true),
                ("游戏", "Java 与内存", "mdi-controller-classic-outline", false),
                ("账户", "登录与档案", "mdi-account-circle-outline", false)
            },
            MainPageKind.Tools => new[]
            {
                ("控件验收", "迁移组件预览", "mdi-tools", true),
                ("日志", "运行与诊断", "mdi-text-box-outline", false),
                ("导入", "整合包与实例", "mdi-import", false)
            },
            _ => new[]
            {
                ("概览", "版本与账户", "mdi-play-circle-outline", true),
                ("实例", "本地游戏列表", "mdi-folder-multiple-outline", false),
                ("动态", "公告与提示", "mdi-bullhorn-outline", false)
            }
        };
    }

}
