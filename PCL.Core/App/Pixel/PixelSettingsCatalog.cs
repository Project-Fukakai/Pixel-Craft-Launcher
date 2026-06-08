using System.Collections.Generic;

namespace PCL.Core.App.Pixel;

public static class PixelSettingsCatalog
{
    public static IReadOnlyList<PixelSettingSection> Sections { get; } =
    [
        new(PixelSettingSectionKind.Launch, "启动", "游戏启动参数与窗口", "mdi-play-circle-outline", "UiHiddenSetupLaunch",
        [
            Group("启动选项",
            [
                Combo("默认版本隔离", "LaunchArgumentIndieV2", ["关闭", "隔离可安装 Mod 的实例", "隔离非正式版", "隔离可安装 Mod 的实例与非正式版", "隔离所有实例"],
                    "当安装新实例时，据此自动设置新实例的隔离选项。"),
                EditableCombo("游戏窗口标题", "LaunchArgumentTitle", ["{name} | 玩家 : {user} | 使用 {login} 登录"],
                    "自定义游戏窗口的标题，留空则不更改。支持 {user}、{login}、{name}、{date}、{time}、{version}。"),
                Text("自定义信息", "LaunchArgumentInfo", "该信息会显示在游戏主界面的左下角，与 F3 调试页面的左上角。",
                    new PixelSettingValidation(Blacklist: ["\"", "“", "”"])),
                Combo("启动器可见性", "LaunchArgumentVisible", ["游戏启动后立即关闭", "保留项", "游戏启动后隐藏，游戏退出后自动关闭", "游戏启动后隐藏，游戏退出后重新打开", "游戏启动后最小化", "游戏启动后仍保持不变"]),
                Combo("进程优先级", "LaunchArgumentPriority", ["高（优先保证游戏运行，游戏性能更佳，但可能造成其他程序卡顿）", "中（平衡）", "低（优先保证其他程序运行，但可能让游戏卡顿，适合挂机时使用）"]),
                Combo("窗口大小", "LaunchArgumentWindowType", ["全屏", "默认", "与启动器尺寸一致", "自定义尺寸", "最大化"]),
                Slider("窗口宽度", "LaunchArgumentWindowWidth", 1, 9999, 1, Unit: "px"),
                Slider("窗口高度", "LaunchArgumentWindowHeight", 1, 9999, 1, Unit: "px"),
                Combo("正版验证方式", "LoginMsAuthType", ["Web 账户管理器（暂时强制设备代码流）", "设备代码流"],
                    "由于部分依赖组件存在较多问题，该选项暂时强制指定为设备代码流。", Available: false, DisabledReason: "暂时强制使用设备代码流。"),
                Combo("IP 协议偏好", "LaunchPreferredIpStack", ["IPv4 优先", "Java 默认", "IPv6 优先"],
                    "通过设置 Java 虚拟机参数来设置 Minecraft 的 IP 协议版本偏好。")
            ]),
            Group("游戏内存",
            [
                Combo("内存分配", "LaunchRamType", ["自动配置", "自定义"]),
                Slider("自定义内存", "LaunchRamCustom", 0, 49, 1, "按 Plain 原始刻度保存，显示时换算为 GB。", Unit: "档", Formatter: "RamScale"),
                Toggle("启动游戏前进行内存优化", "LaunchArgumentRam", "内存优化能降低当前物理内存占用，但在机械硬盘上可能造成短时卡顿。", PixelSettingPlatformAvailability.WindowsOnly),
                MemoryPreview()
            ]),
            Group("高级启动选项",
            [
                Combo("渲染器", "LaunchAdvanceRenderer", ["游戏默认", "软渲染（llvmpipe）", "DirectX12（d3d12）", "Vulkan（zink）"]),
                Text("JVM 参数头部", "LaunchAdvanceJvm", "启动 Minecraft 时使用的额外 JVM 参数，除非有确定把握，否则请不要修改。支持 Minecraft 实例 JSON 中的字符串替换标记，例如 ${library_directory}。"),
                Text("游戏参数尾部", "LaunchAdvanceGame", "文本框中的内容会直接拼合在启动参数末尾，例如 --demo。支持 Minecraft 实例 JSON 中的字符串替换标记。"),
                Text("启动前执行命令", "LaunchAdvanceRun", "在 Minecraft 启动前执行特定命令或程序。支持 {path}、{minecraft}、{verpath}、{verindie}、{java}、{user}、{login}、{uuid}、{name}、{date}、{time}、{version}。"),
                Toggle("等待命令执行完成后再继续启动", "LaunchAdvanceRunWait"),
                Toggle("禁用 Java Launch Wrapper", "LaunchAdvanceDisableJLW"),
                Toggle("禁用 Retro Wrapper", "LaunchAdvanceDisableRW"),
                Toggle("要求 Java 使用高性能显卡", "LaunchAdvanceGraphicCard", "自动在 Windows 图形首选项中将 Java 设为高性能显卡。", PixelSettingPlatformAvailability.WindowsOnly),
                Toggle("使用 java.exe 而不是 javaw.exe", "LaunchAdvanceNoJavaw", "如果游戏窗口长时间不出现等故障可尝试启用。该功能仅影响 Windows 上 java/javaw 的启动选择。", PixelSettingPlatformAvailability.WindowsOnly),
                Toggle("禁用 LWJGL Unsafe Agent", "LaunchAdvanceDisableLwjglUnsafeAgent", "该功能仅会在 LWJGL 版本为 3.4.1 时生效。")
            ])
        ]),
        new(PixelSettingSectionKind.Java, "Java", "Java 选择与扫描", "mdi-language-java", "UiHiddenSetupJava",
        [
            Group("Java 管理", [Text("默认 Java 路径", "LaunchArgumentJavaSelect", "留空时由启动器自动选择。"), Info("Java 列表", "Pixel Java 页会显示 Core JavaManager 的扫描结果，并支持添加、刷新、启用/禁用与详情查看。")])
        ]),
        new(PixelSettingSectionKind.GameManage, "游戏管理", "下载、资源与辅助功能", "mdi-cube-outline", "UiHiddenSetupGameManage",
        [
            Group("下载", [Slider("下载线程数", "ToolDownloadThread", 0, 127, 1), Slider("下载限速", "ToolDownloadSpeed", 0, 42, 1), Combo("文件下载源", "ToolDownloadSource", ["自动", "官方", "BMCLAPI"]), Combo("版本列表源", "ToolDownloadVersion", ["自动", "官方", "BMCLAPI"]), Toggle("自动选择实例", "ToolDownloadAutoSelectVersion"), Toggle("修复 Authlib-Injector", "ToolFixAuthlib")]),
            Group("社区资源", [Combo("资源名称显示", "ToolDownloadTranslate", ["原名", "译名"]), Combo("资源名称显示 V2", "ToolDownloadTranslateV2", ["文件名", "译名", "混合"]), Combo("资源来源", "ToolDownloadMod", ["自动", "Modrinth", "CurseForge"]), Combo("本地 Mod 名称样式", "ToolModLocalNameStyle", ["标题显示译名，详情显示文件名", "标题显示文件名，详情显示译名"]), Toggle("不显示 Quilt 加载器", "ToolDownloadIgnoreQuilt"), Toggle("识别剪贴板资源链接", "ToolDownloadClipboard")]),
            Group("辅助功能", [Toggle("正式版更新提示", "ToolUpdateRelease"), Toggle("测试版更新提示", "ToolUpdateSnapshot"), Toggle("自动设置为中文", "ToolHelpChinese")])
        ]),
        new(PixelSettingSectionKind.GameLink, "联机", "EasyTier 联机设置", "mdi-lan-connect", "UiHiddenSetupGameLink",
        [
            Group("EasyTier", [
                Text("大厅用户名", "LinkUsername", "PCL CE 会尽可能使用此处的用户名用于大厅信息展示。若留空，则使用 Natayark ID 的用户名。"),
                Combo("传输协议优先", "LinkProtocolPreference", ["TCP", "UDP"], "TCP 通常更稳定，UDP 通常延迟更低。"),
                Toggle("选择最低延迟路径而不是最短路径", "LinkLatencyFirstMode", "启用后 EasyTier 会优先选择延迟最低的路径，可能降低延迟，也可能提高丢包率。"),
                Toggle("对对称型 NAT 进行端口猜测", "LinkTryPunchSym", "让 EasyTier 尝试对对称型 NAT 进行端口猜测，一般保持开启即可。"),
                Toggle("允许使用 IPv6 通信", "LinkEnableIPv6", "IPv6 更容易建立 P2P 连接，除有特殊原因外不建议关闭。"),
                Toggle("在日志中输出 CLI 信息以用于调试", "LinkEnableCliOutput", "每 30 秒输出一次 EasyTier CLI 信息，仅建议排查问题时开启。")
            ])
        ]),
        new(PixelSettingSectionKind.Ui, "个性化", "主题、背景、字体与标题栏", "mdi-palette-outline", "UiHiddenSetupUi",
        [
            Group("基础", [Slider("窗口透明度", "UiLauncherTransparent", 0, 600, 10), Combo("深色模式", "UiDarkMode", ["浅色", "深色", "跟随系统"]), ColorScheme(), Toggle("打开启动器时显示 PCL 图标", "UiLauncherLogo"), Toggle("锁定启动器大小", "UiLockWindowSize"), Toggle("启动游戏时显示你知道吗", "UiShowLaunchingHint"), Toggle("亚克力材质", "UiAcrylic", "启用窗口透明与侧边栏亚克力模糊效果。部分平台可能不支持，关闭后使用普通主题背景。")]),
            Group("字体", [Font("全局字体", "UiFont", "选择系统字体，留空时使用启动器默认字体。"), Font("MOTD 字体", "UiMotdFont", "用于服务器状态信息等格式化文本，留空时跟随默认字体。")]),
            Group("背景内容", [Text("背景目录", "UiBackgroundFolder", "留空时使用启动器本地数据目录下的 Backgrounds 文件夹。"), Combo("背景适应方式", "UiBackgroundSuit", ["智能", "居中", "适应", "拉伸", "平铺", "左上", "右上", "左下", "右下"]), Slider("背景透明度", "UiBackgroundOpacity", 0, 1000, 10), Slider("背景轮换", "UiBackgroundCarousel", 0, 1000, 10), Slider("背景模糊", "UiBackgroundBlur", 0, 40, 1), Toggle("游戏启动后暂停视频背景", "UiAutoPauseVideo"), Toggle("叠加彩色背景", "UiBackgroundColorful")]),
            Group("背景音乐", [Slider("音量", "UiMusicVolume", 0, 1000, 10), Toggle("随机播放", "UiMusicRandom"), Toggle("打开启动器自动开始播放", "UiMusicAuto"), Toggle("游戏启动后自动开始播放", "UiMusicStart"), Toggle("游戏启动后自动暂停播放", "UiMusicStop"), Toggle("接入系统媒体传输控制", "UiMusicSMTC")]),
            Group("标题栏与主页", [Combo("标题内容", "UiLogoType", ["无", "默认", "文本", "图片"]), Toggle("标题栏居左", "UiLogoLeft"), Text("标题文本", "UiLogoText"), Combo("主页类型", "UiCustomType", ["空白", "读取本地文件", "联网更新", "预设"]), Slider("主页预设", "UiCustomPreset", 0, 32, 1), Text("自定义主页 URL", "UiCustomNet")])
        ]),
        new(PixelSettingSectionKind.LauncherMisc, "启动器杂项", "系统、网络、调试与隐藏", "mdi-cog-outline", "UiHiddenSetupLauncherMisc",
        [
            Group("系统", [Toggle("禁用硬件加速", "SystemDisableHardwareAcceleration", "该设置会在下次启动启动器时生效。"), Toggle("遥测", "SystemTelemetry"), Slider("实时日志最大行数", "SystemMaxLog", 1, 200, 1, Unit: "行"), Slider("动画帧率上限", "UiAniFPS", 1, 240, 1, Unit: "FPS")]),
            Group("网络代理", [Toggle("启用 DoH", "SystemNetEnableDoH"), Combo("HTTP 代理", "SystemHttpProxyType", ["不使用", "系统代理", "自定义"]), Text("自定义代理地址", "SystemHttpProxy"), Text("代理用户名", "SystemHttpProxyCustomUsername"), Text("代理密码", "SystemHttpProxyCustomPassword")]),
            Group("调试", [Toggle("调试模式", "SystemDebugMode"), Slider("动画速度", "SystemDebugAnim", 0, 20, 1), Toggle("添加随机延迟", "SystemDebugDelay"), Toggle("跳过复制", "SystemDebugSkipCopy"), Toggle("允许受限功能", "SystemDebugAllowRestrictedFeature")]),
            Group("功能隐藏", [Toggle("隐藏下载页", "UiHiddenPageDownload"), Toggle("隐藏设置页", "UiHiddenPageSetup"), Toggle("隐藏工具页", "UiHiddenPageTools"), Toggle("隐藏设置-启动", "UiHiddenSetupLaunch"), Toggle("隐藏设置-Java", "UiHiddenSetupJava"), Toggle("隐藏设置-游戏管理", "UiHiddenSetupGameManage"), Toggle("隐藏设置-联机", "UiHiddenSetupGameLink"), Toggle("隐藏设置-个性化", "UiHiddenSetupUi"), Toggle("隐藏设置-启动器杂项", "UiHiddenSetupLauncherMisc"), Toggle("隐藏设置-关于", "UiHiddenSetupAbout"), Toggle("隐藏设置-更新", "UiHiddenSetupUpdate"), Toggle("隐藏设置-反馈", "UiHiddenSetupFeedback"), Toggle("隐藏设置-日志", "UiHiddenSetupLog"), Toggle("隐藏工具-联机", "UiHiddenToolsGameLink"), Toggle("隐藏工具-帮助", "UiHiddenToolsHelp"), Toggle("隐藏工具-测试", "UiHiddenToolsTest"), Toggle("隐藏实例-编辑", "UiHiddenVersionEdit"), Toggle("隐藏实例-导出", "UiHiddenVersionExport"), Toggle("隐藏实例-另存", "UiHiddenVersionSave"), Toggle("隐藏实例-截图", "UiHiddenVersionScreenshot"), Toggle("隐藏实例-Mod", "UiHiddenVersionMod"), Toggle("隐藏实例-资源包", "UiHiddenVersionResourcePack"), Toggle("隐藏实例-光影", "UiHiddenVersionShader"), Toggle("隐藏实例-投影", "UiHiddenVersionSchematic"), Toggle("隐藏实例-服务器", "UiHiddenVersionServer"), Toggle("隐藏功能-选择实例", "UiHiddenFunctionSelect"), Toggle("隐藏功能-Mod 更新", "UiHiddenFunctionModUpdate"), Toggle("隐藏功能-隐藏实例", "UiHiddenFunctionHidden")])
        ]),
        new(PixelSettingSectionKind.About, "关于", "版本与许可", "mdi-information-outline", "UiHiddenSetupAbout", [Group("关于", [Info("Pixel Craft Launcher", "跨平台 Pixel 迁移入口。版本与许可信息来自启动器 metadata。"), Info("迁移状态", "本页已接入 Core 生命周期、配置、日志、网络、主题和设置适配层。")])]),
        new(PixelSettingSectionKind.Update, "更新", "启动器更新设置", "mdi-update", "UiHiddenSetupUpdate", [Group("更新", [Combo("自动更新行为", "SystemSystemUpdate", ["下载并安装", "下载并提示", "仅提示", "禁用"]), Combo("更新分支", "SystemUpdateChannel", ["Release", "Beta", "Dev"]), Text("Mirror 酱 CDK", "SystemMirrorChyanKey")])]),
        new(PixelSettingSectionKind.Feedback, "反馈", "反馈与诊断入口", "mdi-message-alert-outline", "UiHiddenSetupFeedback", [Group("反馈", [Info("反馈入口", "Plain 的反馈跳转逻辑依赖网页和运行环境检查，本轮保留入口说明。"), Action("打开反馈列表", "外部网页打开能力将在后续工具页统一迁入。", false)])]),
        new(PixelSettingSectionKind.Log, "日志", "运行日志与导出", "mdi-text-box-outline", "UiHiddenSetupLog", [Group("日志", [Info("日志目录", "Core 日志服务已在 Loading 阶段启动，日志写入 PCL/Log。"), Action("导出日志", "日志导出压缩流程将在工具业务迁移时接入。", false)])])
    ];

    public static PixelSettingSection Get(PixelSettingSectionKind kind)
    {
        foreach (var section in Sections)
        {
            if (section.Kind == kind)
                return section;
        }
        return Sections[0];
    }

    private static PixelSettingGroup Group(string title, IReadOnlyList<PixelSettingDescriptor> settings) => new(title, settings);

    private static PixelSettingDescriptor Toggle(string title, string key, string? description = null, PixelSettingPlatformAvailability platform = PixelSettingPlatformAvailability.All) => new(title, PixelSettingControlKind.Toggle, key, description, PlatformAvailability: platform);

    private static PixelSettingDescriptor Text(string title, string key, string? description = null, PixelSettingValidation? validation = null) => new(title, PixelSettingControlKind.Text, key, description, Validation: validation);

    private static PixelSettingDescriptor Font(string title, string key, string? description = null) => new(title, PixelSettingControlKind.Font, key, description);

    private static PixelSettingDescriptor Slider(string title, string key, double min, double max, double tick, string? description = null, string? Unit = null, string? Formatter = null) => new(title, PixelSettingControlKind.Slider, key, description, Minimum: min, Maximum: max, TickFrequency: tick, UnitText: Unit, ValueFormatter: Formatter);

    private static PixelSettingDescriptor Combo(string title, string key, IReadOnlyList<string> options, string? description = null, bool Available = true, string? DisabledReason = null)
    {
        var values = new List<PixelSettingOption>(options.Count);
        for (var i = 0; i < options.Count; i++)
            values.Add(new PixelSettingOption(options[i], i));
        return new PixelSettingDescriptor(title, PixelSettingControlKind.Combo, key, description, values, IsAvailable: Available, DisabledReason: DisabledReason);
    }

    private static PixelSettingDescriptor EditableCombo(string title, string key, IReadOnlyList<string> options, string? description = null)
    {
        var values = new List<PixelSettingOption>(options.Count);
        foreach (var option in options)
            values.Add(new PixelSettingOption(option, option));
        return new PixelSettingDescriptor(title, PixelSettingControlKind.Combo, key, description, values, IsEditableCombo: true);
    }

    private static PixelSettingDescriptor Info(string title, string description) => new(title, PixelSettingControlKind.Info, Description: description);

    private static PixelSettingDescriptor Action(string title, string description, bool available) => new(title, PixelSettingControlKind.Action, Description: description, IsAvailable: available, UnavailableReason: available ? null : "此操作依赖尚未迁入的外围业务。");

    private static PixelSettingDescriptor MemoryPreview() => new("内存占用预览", PixelSettingControlKind.MemoryPreview,
        Description: "展示当前总内存、已用内存、游戏预估占用与启动后空闲内存。");

    private static PixelSettingDescriptor ColorScheme() => new("配色方案", PixelSettingControlKind.ColorScheme,
        Description: "选择预设色、手动输入颜色，或从图片和当前背景中提取 Material 3 配色。");
}
