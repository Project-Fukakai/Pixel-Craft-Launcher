# Pixel 整体重构计划

## 1. 重构目标

Pixel 的目标架构为 **MVVM + Vertical Slice + Command Bus + State Machine + Event Aggregator**。

本次重构的核心目标是把 Pixel 从“窗口代码承载页面、状态、业务流程”的形态，迁移为：

- `Pixel Craft Launcher` 只负责 Avalonia UI、平台能力适配和窗口生命周期。
- `PCL.Core` 承载所有业务规则、状态判断、配置绑定、命令处理、流程编排和可测试 ViewModel。
- 页面按 Page/View 拆分，复杂页面块沉淀为独立 View，业务流程沉淀为 Core slice。
- 用户动作统一通过 Command Bus 进入 Core。
- 跨页面状态变化统一通过 Rx.NET Event Aggregator 分发。
- 启动、下载、联机、Shell 等关键流程由 Stateless 状态机表达。
- DI、日志、状态流、命令流都能被单元测试验证。

必须使用并保留的基础设施：

- **MediatR**：作为 `IPixelCommandBus` 的底层调度器。
- **Stateless**：承载 Shell、Launch、Download、GameLink 等有限状态机。
- **Reactive Extensions (Rx.NET)**：作为 Core 事件聚合、状态流和跨 slice 通知基础。
- **Microsoft.Extensions.DependencyInjection**：统一 Core 和 Avalonia 的组合根。
- **Serilog**：作为 Pixel 启动后的结构化日志入口。

## 2. 目标工程边界

### 2.1 Pixel Craft Launcher

UI 项目允许包含：

- Avalonia `Window`、`UserControl`、Page/View 组件和控件组合。
- 文件选择器、剪贴板、外部链接、弹窗、窗口透明/主题、LibVLC 等平台能力适配。
- 将用户输入转换为 Core command/query 的薄回调。
- 将 Core ViewModel、snapshot、route state 渲染为具体控件。
- UI-only 动画、视觉状态、控件生命周期订阅和释放。

UI 项目不应包含：

- Minecraft 启动、下载、Java 检测、联机大厅、账号认证、配置可用性判断等业务规则。
- 对 `States`、`Config`、`LobbyService`、`EasyTierDependencyService`、`NatayarkProfileManager` 等静态业务状态的直接解释。
- 网络请求、缓存解析、依赖下载、日志分析、实例保存等流程代码。
- `Path.Combine`、`File.Exists`、`Directory.CreateDirectory` 等业务路径/文件规则，除非它是纯平台适配。
- 页面级 fallback `new PixelXxxService()`，最终应由 DI 注入或 Core ViewModel 承载。

### 2.2 PCL.Core

`PCL.Core/App/Pixel` 是 Pixel 应用层入口，负责：

- `Composition`：注册 DI、MediatR handlers、state machines、event bus、ViewModels、slice services。
- `Infrastructure`：Command Bus、logging behavior、state machine factory、host lifecycle、UI adapter interfaces。
- `Events`：Rx.NET event aggregator。
- `Navigation` / `Shell`：页面导航模型、功能可见性、Shell 设置快照。
- `Slices`：按业务纵切组织 command、handler、service、snapshot、state machine、event。
- `ViewModels`：不依赖 Avalonia 的 MVVM 状态、快照和用户流程状态。

`PCL.Core/Minecraft` 继续承载 Minecraft 领域能力，例如启动日志分析、进程监控、服务端脚本生成、核心包保存等。Pixel slice 可以编排这些领域服务，但 UI 不直接编排。

## 3. 目标目录结构

```text
PCL.Core/
  App/
    Pixel/
      Composition/
        PixelServiceCollectionExtensions.cs
      Events/
        IPixelEventBus.cs
        ReactivePixelEventBus.cs
        *Event.cs
      Infrastructure/
        IPixelCommandBus.cs
        MediatRPixelCommandBus.cs
        PixelLoggingBehavior.cs
        IPixelStateMachineFactory.cs
        PixelStateMachineFactory.cs
        UiAdapters.cs
      Navigation/
        PixelRouter.cs
        PixelRoutes.cs
        PixelRouteChangedEvent.cs
      Shell/
        MainPageKind.cs
        Shell states and snapshots
      Slices/
        Launch/
        Download/
        Profiles/
        Java/
        GameLink/
        Personalization/
        Settings/
      ViewModels/
        MainWindowViewModel.cs
        PixelLaunchViewModel.cs
        PixelDownloadViewModel.cs
        PixelInstanceViewModel.cs
        PixelGameLinkViewModel.cs

Pixel Craft Launcher/
  Composition/
    PixelApplication.cs
    PixelAvaloniaServiceCollectionExtensions.cs
  Services/
    Avalonia/platform adapter services only
  Views/
    MainWindow.axaml
    MainWindow.axaml.cs
    Pages/
      Launch page views
      Download page views
      Profile page views
      GameLink page views
      Placeholder/message views
    Setup/
      SettingRow.axaml
      Setting renderers
      Setup-specific Avalonia controls
```

## 4. 架构规则

### 4.1 MVVM

- ViewModel 必须放在 `PCL.Core/App/Pixel/ViewModels`，不依赖 Avalonia 类型。
- View 只绑定 ViewModel 属性、调用 command callback、触发平台适配。
- ViewModel 可以依赖 Core service、`IPixelCommandBus`、`IPixelEventBus`、状态机 factory。
- ViewModel 暴露 UI-ready snapshot，避免 UI 根据领域对象自行拼文案、图标、可见性。

### 4.2 Vertical Slice

每个 slice 包含自己的：

- Commands / Queries。
- Command handlers。
- Service / workflow coordinator。
- Snapshot / DTO。
- Events。
- State machine。
- Tests。

slice 之间通过 command bus、event bus、稳定 service interface 通信，避免跨 slice 直接读取内部状态。

### 4.3 Command Bus

- 所有用户动作对应一个 command，例如 `CreateGameLinkLobbyCommand`、`RefreshJavaInstallationsCommand`、`SelectProfileCommand`。
- UI 默认依赖 `IPixelCommandBus` 或 Core ViewModel 方法；迁移期存在的 service fallback 必须列入清理清单。
- handler 不依赖 Avalonia 类型。
- handler 完成后按需发布 event，例如 `JavaInstallationsRefreshedEvent`、`GameLinkLobbyJoinedEvent`。
- command 失败路径必须记录 Serilog 结构化日志，并向 ViewModel 或 UI adapter 返回可展示结果。

### 4.4 State Machine

使用 Stateless 表达有明确状态和触发器的流程，不把流程状态散落在 bool 字段中。

优先落地：

- Shell State Machine：启动、导航、忙碌、错误。
- Launch State Machine：实例解析、准备、启动、运行、失败。
- Download State Machine：版本解析、loader 选择、下载、保存、完成、失败。
- GameLink State Machine：EULA、依赖安装、登录、创建/加入、已连接、失败。

### 4.5 Event Aggregator

- `IPixelEventBus` 基于 Rx.NET。
- 事件命名使用过去式，例如 `PixelSettingChangedEvent`、`PixelRouteChangedEvent`。
- UI 不直接订阅 Core 领域静态事件；由 Core service 包装成 application event 或 runtime callback。
- 长生命周期订阅必须可释放，窗口关闭时释放。
- ViewModel 订阅 event bus 后负责更新自身 snapshot。

### 4.6 Dependency Injection

- `PCL.Core` 通过 `AddPclCore` 注册 Core 基础设施、slice services、ViewModels、handlers。
- Avalonia 项目通过 `AddPixelAvalonia` 注册平台适配实现。
- 页面和窗口不随意 `new` 业务服务。
- 测试使用同一组 DI 扩展验证组合根完整性。

### 4.7 Serilog

- UI 启动阶段只负责 bootstrap logger。
- Core 提供日志路径策略和 command pipeline 日志。
- MediatR pipeline 记录 command 开始、完成、异常。
- slice 内关键失败路径记录结构化字段，例如 instance id、lobby id、java path、route。

## 5. Vertical Slice 详细计划

### 5.1 Launch Slice

职责：

- 启动命令、启动状态机、实例选择、进程监控、日志导出、启动日志分析。
- 首页启动按钮状态、当前实例摘要、运行状态、错误状态。
- 启动文件夹选择和自定义文件夹管理。
- 内存预览和运行前配置解析。

目标文件：

- `Slices/Launch/StartMinecraftLaunchCommand.cs`
- `Slices/Launch/KillMinecraftProcessCommand.cs`
- `Slices/Launch/MonitorMinecraftProcessCommand.cs`
- `Slices/Launch/ExportLaunchLogCommand.cs`
- `Slices/Launch/PixelLaunchStateMachine.cs`
- `Slices/Launch/PixelLaunchFolderService.cs`
- `Slices/Launch/PixelHomepageService.cs`
- `Slices/Launch/PixelMemoryPreviewService.cs`
- `Slices/Launch/PixelLaunchInstanceListService.cs`

待迁移重点：

- 从 `MainWindow.Pages.LaunchAndLeft.axaml.cs` 迁移实例列表分组、实例类型文案、图标选择、选中状态、隐藏过滤。
- UI 不再直接接收 `MinecraftInstanceInfo` 作为渲染模型，改用 `PixelLaunchInstanceItemSnapshot`。
- 工具入口摘要、联机状态摘要、EasyTier 摘要由 Core 生成。

验收标准：

- Launch 页启动、实例选择、文件夹选择通过 Core ViewModel 或 command。
- UI 不直接组合实例业务文案和图标。
- `PixelLaunchInstanceListService` 有分组、隐藏过滤、类型显示测试。
- 启动状态变化可被 event bus 订阅。

### 5.2 Download Slice

职责：

- Minecraft 版本、Mod Loader、整合包、服务端脚本、下载任务编排。
- Loader 选择、版本保存、下载进度模型。
- 下载页 UI-ready snapshot。

目标文件：

- `Slices/Download/PixelDownloadCommands.cs`
- `Slices/Download/PixelLoaderChoiceService.cs`
- `Slices/Download/PixelLoaderSelectionService.cs`
- `ViewModels/PixelDownloadViewModel.cs`

待迁移重点：

- 从 `MainWindow.Pages.Download.axaml.cs` 拆出下载页视图块。
- 将下载页按钮状态、loader 可选性、版本选择规则收敛到 `PixelDownloadViewModel`。
- 下载命令统一通过 `IPixelCommandBus`。

验收标准：

- 下载页不直接解释配置规则。
- 下载按钮只触发 command 或 ViewModel 方法。
- Loader 选择和保存策略有 Core 测试。

### 5.3 Profiles Slice

职责：

- 实例列表、实例刷新、实例元信息、实例操作入口。
- Profile 类型名、UUID、路径、版本摘要等 UI-ready snapshot。

目标文件：

- `Slices/Profiles/PixelProfileCommands.cs`
- `Slices/Profiles/PixelProfileListService.cs`
- `ViewModels/PixelInstanceViewModel.cs`

待迁移重点：

- 从 `MainWindow.Pages.Profiles.axaml.cs` 迁移 `MinecraftProfileService.GetProfileTypeName(profile)` 等显示逻辑。
- Profiles 页改用 `PixelProfileItemSnapshot`。
- 实例刷新、删除、打开目录、选择默认实例经 command 或 ViewModel。

验收标准：

- Profiles 页不直接维护 Minecraft profile 业务状态。
- `MinecraftProfileService` 由 DI 管理。
- profile 类型显示和列表排序有 Core 测试。

### 5.4 Java Slice

职责：

- Java 列表刷新、手动添加、选中、启用/禁用、兼容性信息。
- Java toolbar、列表项、详情弹窗所需 snapshot。

目标文件：

- `Slices/Java/PixelJavaCommands.cs`
- `Slices/Java/PixelJavaService.cs`
- `Slices/Java/PixelJavaPageService.cs`
- `ViewModels/PixelJavaViewModel.cs`

待迁移重点：

- 从 `MainWindow.Pages.Setup.axaml.cs` 迁移 Java 列表 item 构建所需文案和按钮状态。
- UI 不再持有 `JavaEntry` 作为操作参数，改为 stable id/path command。
- 删除 `GetPixelJavaService() => new PixelJavaService()` fallback。

验收标准：

- Java 页不直接写 `Config.Launch.SelectedJava`。
- Java 操作全部经 command bus 或 Core ViewModel。
- Java 刷新、添加、选择、禁用有 Core 测试。

### 5.5 GameLink Slice

职责：

- 大厅公告、EULA、EasyTier 依赖、Natayark 登录、NAT 测试、创建/加入/离开大厅、本地世界发现、运行时事件订阅。
- 对外提供页面 snapshot，UI 不解释业务状态。

目标文件：

- `Slices/GameLink/PixelGameLinkCommands.cs`
- `Slices/GameLink/PixelGameLinkService.cs`
- `Slices/GameLink/NatayarkOAuthService.cs`
- `Slices/GameLink/PixelGameLinkToolsPageController.cs`
- `ViewModels/PixelGameLinkViewModel.cs`

待迁移重点：

- 从 `MainWindow.Pages.Tools.axaml.cs` 迁移登录、依赖安装、NAT 测试、创建/加入大厅、公告加载的流程编排。
- 消除 `GetPixelGameLinkService() => new PixelGameLinkService()` fallback。
- GameLink 页面只绑定 `PixelGameLinkViewModel` 和调用 command。
- `GameLinkAnnouncementHintView`、`GameLinkCreateCardView`、`GameLinkFinishPanelView` 不再直接引用领域模型，改用 snapshot。

验收标准：

- Tools 页不直接访问 `LobbyService`、`EasyTierDependencyService`、`NatayarkProfileManager`。
- GameLink 操作通过 command bus、Core ViewModel 或 page controller。
- 运行时事件由 Core 包装后再交给 UI 展示。
- EULA、登录、依赖安装、创建/加入/离开大厅有 Core 测试。
- GameLink 关键流程由 `PixelGameLinkStateMachine` 表达，并通过 Rx.NET event bus 发布状态迁移。

### 5.6 Personalization Slice

职责：

- 背景目录、媒体扫描、轮播策略、音乐路径、运行中播放策略、音量计算。
- Core 负责策略，UI 负责 Avalonia/LibVLC 适配。

目标文件：

- `PixelPersonalizationService.cs`
- `Slices/Personalization/PixelBackgroundService.cs`
- `Slices/Personalization/PixelMediaPlaybackPolicyService.cs`

待迁移重点：

- `Pixel Craft Launcher/Services/PersonalizationPlatformBridge.cs` 保留播放器适配和 Avalonia 资源应用。
- `ColorSchemeService.SetBackgroundSeed` 相关策略进一步封装到 Core 或明确标注为主题 UI adapter。

验收标准：

- 背景目录创建、扫描、播放策略有 Core 测试。
- UI 服务不读取配置规则，只执行 Core 返回的播放/主题应用指令。

### 5.7 Settings Slice

职责：

- 设置目录、设置项模型、可用性判断、读写绑定、重置分区、配置联动。

目标文件：

- `PixelSettingsCatalog.cs`
- `PixelSettingsBinder.cs`
- `PixelSettingsChangeService.cs`
- `PixelSettingsObservationService.cs`
- `PixelSettingDisplayService.cs`
- `PixelSettingValueService.cs`
- `PixelColorSchemeSettingsService.cs`
- `PixelShellSettingsService.cs`

待迁移重点：

- `MainWindow.Pages.Setup.axaml.cs` 只渲染 setting model。
- `PixelColorSchemeSettingView` 中颜色解析、格式化、预览色计算如属于业务规则，应由 `PixelColorSchemeSettingsService` 暴露。
- `FeatureVisibilityService` 逐步由 Shell/ViewModel snapshot 消化，减少 View 中散落调用。

验收标准：

- UI 不直接调用 `PixelSettingsBinder`。
- 所有设置显示、读写、联动经 Core service。
- 设置目录、显示规则、变更 effect、观察订阅有 Core 测试。

## 6. Pages 和 Views 拆分计划

### 6.1 拆分原则

- `MainWindow.axaml.cs` 只保留窗口生命周期、顶层导航、主题应用、平台适配桥接。
- `MainWindow.Pages.*.axaml.cs` 只作为过渡层，最终每个页面收敛为独立 `Views/Pages/*View.cs` 或 `.axaml`。
- 可复用区域拆成 View，业务状态来自 Core snapshot。
- View 中允许有控件拼装代码，但不允许有业务判断。
- 控件事件只调用传入 callback 或 ViewModel command。
- 每个 Page/View 只能依赖 Core 暴露的 ViewModel、snapshot、command callback、UI adapter interface；不能依赖 Minecraft、Link、Config、States 等领域对象。
- View 拆分时优先按“用户任务”而不是按视觉 card 切分，避免出现只能服务单个字段的小控件膨胀。
- 对列表、工具栏、弹窗这三类控件统一使用 stable id 或 action id 回调，不把领域对象作为 Tag、CommandParameter 或闭包传递。

### 6.2 页面归属矩阵

| UI 文件 | 目标 Page/View | Core 入口 | 允许保留的 UI 职责 | 不允许保留的业务职责 |
| --- | --- | --- | --- | --- |
| `MainWindow.axaml.cs` | `MainWindow` shell | `MainWindowViewModel`、Core slice services、平台 adapter services | 窗口生命周期、主题应用、弹窗展示、平台 adapter 调用 | 直接发送 command、业务流程编排、配置解释、Minecraft/Link 状态解释 |
| `MainWindow.Pages.LaunchAndLeft.axaml.cs` | `LaunchHomePageView`、`LaunchSidebarView`、`LaunchInstanceListView` | `PixelLaunchViewModel`、`PixelLaunchSidebarService`、`PixelLaunchInstanceListService` | 控件组合、导航按钮、启动弹窗展示 | 实例分组、图标/类型文案、启动失败文案、路径规则 |
| `MainWindow.Pages.Download.axaml.cs` | `DownloadPageView` | `PixelDownloadViewModel`、`DownloadPagePlatformBridge` | 下载右页 host 和 bridge/factory 组装 | 版本分组、loader 可用性、安装按钮状态、文件选择和实例管理适配 |
| `MainWindow.Pages.Setup.axaml.cs` | `SetupPageView`、`JavaSetupPageView`、`SettingsSectionView` | `PixelJavaService`、settings services、setup bridge | 设置控件渲染、平台桥接组装 | Java 选择规则、设置读写规则、通知文案 |
| `MainWindow.Pages.Tools.axaml.cs` | `GameLinkToolsPageView` and child views | `PixelGameLinkViewModel`、`PixelGameLinkToolsPageController` | 复制、打开链接、弹窗、输入框取值 | EULA、登录、依赖安装、NAT、创建/加入/离开大厅流程 |
| `MainWindow.Pages.Profiles.axaml.cs` | `ProfilesPageView`、`ProfileListView`、`ProfileEditorDialogView` | `PixelProfileListService`、Profile commands | 弹窗、表单输入、浏览器/剪贴板 | Profile 类型/图标、登录进度文案、设备码弹窗文案、选择/删除规则 |
| `Views/Setup/*` | Setup-specific controls | Settings/Java/GameLink snapshots | Avalonia 控件状态、颜色选择器、布局 | 设置读写、颜色 seed 解析、内存预览业务 |
| `Views/Pages/GameLink*` | GameLink child views | GameLink snapshots | 只读渲染、按钮回调 | Link 领域模型解析、Natayark/EasyTier/Lobby 判断 |

### 6.3 View 拆分交付模板

每拆一个 Page/View 都按以下模板交付：

1. **Core snapshot**：新增或扩展 `Pixel*Snapshot`，包含 View 所需的全部文案、图标 key、可见性、启用状态、action id、stable id。
2. **Core service/ViewModel**：把 snapshot 构造和业务判断放入 `PCL.Core/App/Pixel/Slices/*` 或 `ViewModels/*`。
3. **View 替换**：View 只渲染 snapshot；事件回调只传 id/action id/input value。
4. **Command 接入**：可变更业务状态的操作优先进入 `IPixelCommandBus`，迁移期可由 ViewModel 方法包裹 command。
5. **测试补齐**：对 snapshot 构造、命令处理、状态变化和异常路径补 Core 单元测试。
6. **扫描验证**：运行 UI 业务耦合扫描，确认新 View 未引入领域类型。

### 6.4 Launch 页面

目标 View：

- `LaunchHomePageView`
- `LaunchSidebarView`
- `LaunchInstanceSelectionPageView`
- `LaunchInstanceListView`
- `LaunchInstanceGroupView`
- `LaunchFolderSelectorView`
- `LaunchHomepageView`
- `HomepageCardView`
- `LaunchProfileSummaryView`
- `LaunchRuntimeStatusView`
- `ToolsLeftPageView`

迁移顺序：

1. 抽出实例列表 snapshot service。
2. 新建 `LaunchInstanceListView`，只渲染 group/item snapshot。
3. 将左侧工具入口抽为 `LaunchSidebarView`。
4. 将首页卡片、公告、启动信息抽为 `LaunchHomePageView`。
5. 删除 `MainWindow.Pages.LaunchAndLeft.axaml.cs` 中实例文案/图标/分组方法。
6. 将 `SelectedProfile`、`SelectedProfileMethod`、`SelectedInstancePath` 等绑定迁移为 `PixelLaunchSummarySnapshot`。
7. 将联机状态、测试工具、实例文件夹打开状态统一纳入 `PixelLaunchSidebarSnapshot` 或独立 snapshot。

当前进度：

- 已完成：`ToolsLeftPageView` 已抽出工具页左侧栏，输入为 `PixelGameLinkSidebarSnapshot`、当前 GameLink 子页、测试入口可见性和导航回调；`MainWindow.Pages.LaunchAndLeft.axaml.cs` 不再直接构造该侧栏 item。
- 已完成：`LaunchHomepageView` 已抽出启动页自定义主页渲染，输入为 `PixelHomepageSnapshot` 和打开外部链接回调；图片、Markdown、WebView fallback、预设主页展示不再由 `MainWindow.Pages.LaunchAndLeft.axaml.cs` 直接构造。
- 已完成：`LaunchInstanceSelectionPageView` 已抽出实例选择页、实例分组和 item 操作按钮渲染，输入为 `PixelLaunchInstanceGroupSnapshot`、`PixelLaunchInstanceActionVisibilitySnapshot` 和刷新/选择/打开文件夹回调。
- 已完成：新增 `PixelLaunchSidebarSnapshot` 聚合启动页二级动作和测试工具入口可见性，`MainWindow` 不再分别读取 `GetSecondaryActions` / `IsTestToolVisible` 来拼装左侧状态。

### 6.5 Download 和 Setup 页面

目标 View：

- `DownloadPageView`
- `SetupPageView`
- `SetupSectionNavigationView`
- `JavaSetupPageView`
- `GameLinkSetupPageView`
- `GeneralSettingsPageView`
- `SettingsSectionView`

迁移顺序：

1. 先保留 `PixelSettingControlRenderer`，把 Java page 独立出来。
2. Java 列表改为 `PixelJavaViewModel` snapshot。
3. GameLink setup 中 NAT 测试改为 command。
4. 设置 section 按 catalog 分段渲染。
5. 删除页面中 service fallback。
6. 将安装动作拆成 `StartClientInstallCommand`、`StartServerInstallCommand` 或统一 `StartDownloadInstallCommand`。
7. 将“保存服务端脚本”“打开 Mods/存档”“刷新分类”等操作明确分成业务 command 和 UI adapter 动作。

### 6.6 Tools/GameLink 页面

当前已有 View：

- `ControlsPreviewPageView`
- `GameLinkToolsPageView`
- `GameLinkAccountToolbarView`
- `GameLinkAnnouncementHintView`
- `GameLinkCreateCardView`
- `GameLinkJoinCardView`
- `GameLinkEasyTierCardView`
- `GameLinkEulaCardView`
- `GameLinkFinishPanelView`
- `GameLinkFooterView`

下一步：

1. 增加 `PixelGameLinkToolsPageController` 或 `PixelGameLinkToolsViewModel`。
2. 把 Tools 页中的异步流程编排迁移到 Core。
3. 把 View 构造参数中的领域模型替换为 snapshot。
4. UI 只处理提示展示、复制、打开链接、文件选择等平台动作。
5. 将手动端口、房间码、账号状态等输入/展示状态沉淀为 `PixelGameLinkFormSnapshot`。
6. 将 runtime 订阅统一由 Core controller 管理生命周期，UI 只 dispose controller subscription。

### 6.7 Profiles 页面

目标 View：

- `ProfilesPageView`
- `ProfileManagerLeftPageView`
- `ProfileListPageView`
- `ProfileFormPageView`
- `ProfileItemView`
- `ProfileActionToolbarView`
- `OfflineProfileEditorView`
- `AuthlibLoginDialogView`
- `MicrosoftDeviceCodeDialogView`

迁移顺序：

1. 新建 `PixelProfileListService` 输出 `PixelProfileItemSnapshot`。
2. Profiles 页面使用 snapshot 渲染。
3. 实例操作改为 command。
4. 保留打开目录/外链为 UI adapter。
5. 把 Microsoft/Authlib 登录结果状态进一步包装到 `PixelProfileLoginController`，View 只提供 callback adapter。
6. 已完成：Authlib 多角色选择文案和选项模型移到 Core snapshot，Avalonia 只负责显示选择弹窗并返回索引。

当前进度：

- 已完成：`ProfileListPageView` 已抽出 profile 列表与 item 渲染，输入为 `PixelProfileItemSnapshot` 集合和 select/copy/edit/delete 回调；`MainWindow.Pages.Profiles.axaml.cs` 只保留 Profile 操作编排与弹窗入口。
- 已完成：`ProfileManagerLeftPageView` 已抽出左侧账号/第三方验证服务器导航，输入为 `PixelAuthServerSnapshot` 集合和 add Microsoft/offline/Authlib/server 回调。
- 已完成：`ProfileFormPageView` 已抽出 Profile 表单页面壳，窗口层只负责生成字段控件和绑定保存/登录流程。
- 已完成：`PixelProfilePageService` 的保存离线档案、添加 Microsoft/Authlib/第三方服务器、选择/删除档案入口已通过 command bus 发送对应 command，并补充 PageService command 发送测试与 DI handler 注册测试。
- 已完成：新增 `PixelProfileStateMachine`，覆盖保存离线档案、Microsoft/Authlib 认证、保存第三方服务器、选择/删除档案、完成和失败状态；`PixelProfilePageService` 已在 command bus 操作前后驱动状态迁移，并有 Rx.NET 状态事件测试和 DI 注册测试。

### 6.8 拆分优先级

高优先级：

- 仍然引用 Minecraft/Profile/Launch 领域类型的 View 先拆。
- 已完成：`PixelLaunchViewModel`、`PixelDownloadViewModel`、`PixelGameLinkViewModel`、`PixelJavaService`、`PixelProfilePageService` 的迁移期 command bus fallback 已移除，写操作统一要求构造注入 `IPixelCommandBus`。
- 会改变配置、下载、登录、启动状态的按钮先改为 command-first。

中优先级：

- 单纯页面组合过大的文件，例如 Download/Setup 中的多个 section。
- 只影响显示但逻辑已在 Core 的控件，例如列表项、摘要卡片、工具栏。

低优先级：

- 纯视觉控件、占位页、消息页。
- 只承载 Avalonia 样式或平台能力的 adapter。

## 7. 状态机设计

### 7.1 Shell State Machine

状态：

- `Initializing`
- `Ready`
- `Navigating`
- `Busy`
- `Error`

触发：

- `Boot`
- `Navigate`
- `BeginOperation`
- `CompleteOperation`
- `Fail`
- `Recover`

输出：

- `PixelShellStateChangedEvent`
- 当前 route、busy message、error message。

### 7.2 Launch State Machine

状态：

- `Idle`
- `ResolvingInstance`
- `Preparing`
- `Launching`
- `Running`
- `Stopping`
- `Failed`

触发：

- `Start`
- `InstanceResolved`
- `Prepared`
- `ProcessStarted`
- `Stop`
- `ProcessExited`
- `Fail`

输出：

- `PixelLaunchStateChangedEvent`
- 启动按钮状态、进程状态、日志导出状态。

### 7.3 Download State Machine

状态：

- `Idle`
- `ResolvingVersions`
- `SelectingLoader`
- `Downloading`
- `Saving`
- `Completed`
- `Failed`
- `Cancelled`

触发：

- `StartVersionRefresh`
- `VersionsResolved`
- `OpenLoaderSelection`
- `LoaderChoicesResolved`
- `StartDownload`
- `StartSave`
- `Complete`
- `Fail`
- `Cancel`
- `Reset`

输出：

- `PixelStateChangedEvent`，其中 `MachineName` 为 `PixelDownload`。
- 当前任务、进度、速度、错误信息。

当前进度：

- 已完成：新增 `PixelDownloadStateMachine`，覆盖版本解析、loader 选择、下载、保存、完成、失败、取消和重置基线状态。
- 已完成：通过 `AddPclCore` 注册 `PixelDownloadStateMachine`，并由 DI 组合根测试验证。
- 已完成：状态迁移通过 `PixelStateMachine<TState,TTrigger>` 统一发布 Rx.NET event bus 事件。
- 已完成：`PixelDownloadViewModel` 已在刷新版本、刷新 loader、安装、保存和取消入口驱动 `PixelDownloadStateMachine`。
- 已完成：刷新版本、刷新 loader、安装、保存路径的 ViewModel 状态机事件流已有 Core 测试。
- 已完成：新增 `IPixelOperationDelayService`，生产环境包装 `DebugSettingsService`，测试环境可用 no-op adapter，避免 ViewModel 测试触发静态 `Config` 初始化副作用。
- 已完成：新增 `CancelMinecraftDownloadTaskCommand` 和 `CancelAllMinecraftDownloadsCommand`，下载任务取消已通过 Command Bus 和 Rx event bus 发布。
- 已完成：新增 `PixelDownloadOperationSnapshot`，集中派生 `DownloadState`、忙碌状态、状态文案、版本/loader 错误和 pending operation；安装状态与任务详情 pending 判断改为从 operation snapshot 派生。

### 7.4 GameLink State Machine

状态：

- `Ready`
- `EulaRequired`
- `InstallingDependency`
- `Authenticating`
- `CheckingNetwork`
- `Hosting`
- `Joining`
- `Connected`
- `Leaving`
- `Failed`

触发：

- `RequireEula`
- `AcceptEula`
- `StartDependencyInstall`
- `DependencyReady`
- `StartLogin`
- `LoginSucceeded`
- `LoginFailed`
- `StartNetworkCheck`
- `NetworkCheckSucceeded`
- `NetworkCheckFailed`
- `StartHosting`
- `StartJoining`
- `OperationSucceeded`
- `OperationFailed`
- `Leave`
- `Left`
- `Fail`
- `Reset`

输出：

- `PixelStateChangedEvent`，其中 `MachineName` 为 `PixelGameLink`。
- 当前账号、依赖状态、大厅状态、可执行动作。

当前进度：

- 已完成：新增 `PixelGameLinkStateMachine`，覆盖 EULA、依赖安装、登录、网络检测、创建/加入大厅、已连接、离开、失败状态。
- 已完成：`PixelGameLinkToolsPageController` 已在依赖安装、登录、NAT 测试、创建/加入/离开大厅时驱动状态机。
- 已完成：GameLink 状态迁移通过 Rx.NET event bus 发布，并有 controller 状态事件测试和 DI 组合根测试。
- 已完成：新增 `AcceptGameLinkEulaCommand` 和 `ResetGameLinkAuthorizationCommand`，EULA 接受/授权重置已通过 command bus 和事件发布。
- 已完成：新增 `PixelGameLinkRuntimeEventBridge`，运行时刷新、EasyTier 安装请求、页面切换、客户端 ping、用户关闭游戏、服务端异常已包装为 Core runtime events，同时保留现有 UI callbacks。
- 已完成：`PixelGameLinkViewModel` 已订阅 GameLink runtime events，派生 `PixelGameLinkRuntimeSnapshot`，并更新子页、刷新请求、EasyTier 自动安装标记、ping 和运行时消息。
- 已完成：Tools 页 runtime refresh callback 已改为读取 `PixelGameLinkRuntimeSnapshot`，页面切换由 ViewModel event 订阅处理，UI 不再直接传入 `ShowSelect` / `ShowFinish` runtime callback。
- 已完成：`NatayarkLoginCompletedEvent` / `NatayarkLoggedOutEvent` 已刷新 `PixelGameLinkViewModel.AccountSnapshot`，Tools 页账号工具栏改为读取 ViewModel snapshot 属性。
- 已完成：用户关闭游戏和服务端异常已通过 `PixelGameLinkRuntimeNotification` 暴露为 ViewModel 通知意图，Tools 页不再在 runtime callback 中直接弹出对应提示。
- 已完成：`NatayarkLoginCompletedEvent` 已驱动 `PixelGameLinkStateMachine` 的登录成功/失败迁移，`NatayarkLoggedOutEvent` 会尝试重置状态机并刷新账号/侧栏快照。
- 已完成：`EasyTierSnapshot`、`DiscoveredWorldsSnapshot`、`FinishSnapshot`、`CurrentLobbyCode`、`SidebarSnapshot` 已提升为 ViewModel 属性，Tools/Setup/Left 页改为优先读取属性快照。
- 已完成：`GameLinkAnnouncementsLoadedEvent` 已携带大厅可用性，`PixelGameLinkViewModel` 新增 `IsLobbyAvailable` / `AnnouncementSnapshot` 属性，Tools 页公告提示不再传入 controller 可用性参数。
- 已完成：控件验收页已从 `MainWindow.Pages.Tools.axaml.cs` 抽出为 `ControlsPreviewPageView`，共享页面 helper 已移动到 `MainWindow.PageHelpers.axaml.cs`，Tools 过渡页体量继续下降。
- 待完成：继续收敛 Tools 页中剩余的操作 callback 到 command/event/snapshot，并进一步减少 controller 对 ViewModel 的直接页面切换调用。

## 8. Command Bus 和 Event Aggregator 清单

### 8.1 Command 命名和处理规则

- Command 使用动词开头，表达一个用户意图或系统动作，例如 `StartMinecraftLaunchCommand`、`RefreshDownloadVersionsCommand`。
- Command 参数只能包含 stable id、路径、用户输入值、选项枚举、UI callback interface；不能包含 Avalonia 控件或 View。
- Command handler 放在对应 Vertical Slice 内，依赖 slice service、领域 service、`IPixelEventBus`、Serilog logger。
- Command handler 成功后发布完成事件，失败时记录结构化日志，并返回可被 ViewModel 消费的结果或抛出可识别异常。
- 需要平台能力的流程通过 UI adapter interface 反转依赖，例如选择文件夹、打开 URL、复制文本、显示系统提示。

### 8.2 Launch Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `StartMinecraftLaunchCommand` | 首页启动按钮 | 校验实例/profile/Java，进入启动状态机，启动 Minecraft | `PixelLaunchStartedEvent`、`PixelLaunchFailedEvent` |
| `KillMinecraftProcessCommand` | 停止/结束进程按钮 | 停止当前 Minecraft 进程并更新状态机 | `PixelLaunchProcessStoppedEvent` |
| `MonitorMinecraftProcessCommand` | 启动后后台流程 | 订阅进程退出，分析日志，发布运行状态 | `PixelLaunchProcessExitedEvent` |
| `ExportLaunchLogCommand` | 崩溃/失败弹窗 | 导出日志文本或路径，交给 UI adapter 打开/保存 | `PixelLaunchLogExportedEvent` |
| `SelectLaunchInstanceCommand` | 实例列表 item | 按实例 id 设置当前实例，刷新 summary snapshot | `PixelLaunchInstanceSelectedEvent` |
| `SelectLaunchFolderCommand` | 文件夹列表 item | 选择游戏目录，刷新实例列表和 sidebar | `PixelLaunchFolderSelectedEvent` |

### 8.3 Download Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `RefreshDownloadVersionsCommand` | 下载分类切换/刷新 | 拉取版本清单，构建版本 group snapshot | `PixelDownloadVersionsRefreshedEvent` |
| `SelectDownloadVersionCommand` | 版本列表 item | 设置待安装版本，刷新 loader/install 状态 | `PixelDownloadVersionSelectedEvent` |
| `SelectLoaderChoiceCommand` | loader/addon 选择 | 设置 loader 或 addon file 选择 | `PixelDownloadLoaderSelectedEvent` |
| `StartDownloadInstallCommand` | 安装按钮 | 根据安装类型启动下载/保存流程，驱动下载状态机 | `PixelDownloadInstallStartedEvent`、`PixelDownloadInstallFailedEvent` |
| `CancelMinecraftDownloadTaskCommand` | 下载任务 item | 取消指定任务并刷新任务 snapshot | `MinecraftDownloadTaskCancelRequestedEvent` |
| `CancelAllMinecraftDownloadsCommand` | 下载任务组/全局取消 | 请求取消当前下载调度器中的全部任务 | `MinecraftDownloadAllTasksCancelRequestedEvent` |
| `SaveMinecraftServerJarCommand` | 服务端保存流程 | 保存官方服务端 Jar，并由 Core 写出跨平台启动脚本 | `MinecraftCorePackageSavedEvent` |

### 8.4 Profiles Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `RefreshMinecraftProfilesCommand` | Profiles 页面进入/刷新 | 刷新 profile 列表 snapshot | `PixelProfilesRefreshedEvent` |
| `SelectMinecraftProfileByIdCommand` | Profile item | 设置当前 profile，刷新 Launch summary | `PixelProfileSelectedEvent` |
| `RemoveMinecraftProfileByIdCommand` | 删除按钮 | 删除 profile 并刷新列表 | `PixelProfileRemovedEvent` |
| `SaveOfflineMinecraftProfileByIdCommand` | 离线编辑表单 | 新增或更新离线 profile | `PixelProfileSavedEvent` |
| `AddMicrosoftMinecraftProfileCommand` | Microsoft 登录按钮 | 执行设备码登录流程 | `PixelProfileLoginStartedEvent`、`PixelProfileLoginCompletedEvent` |
| `AddAuthlibMinecraftProfileCommand` | Authlib 登录按钮 | 执行第三方验证登录流程 | `PixelProfileLoginStartedEvent`、`PixelProfileLoginCompletedEvent` |
| `AddAuthServerPresetCommand` | 添加验证服务器 | 保存 auth server preset | `PixelAuthServerPresetAddedEvent` |

### 8.5 Java Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `RefreshJavaInstallationsCommand` | Java setup 刷新 | 扫描 Java，生成列表 snapshot | `JavaInstallationsRefreshedEvent` |
| `AddJavaInstallationCommand` | 手动添加 | 校验路径并加入 Java 列表 | `JavaInstallationAddedEvent` |
| `SelectJavaInstallationCommand` | Java item | 设置当前 Java | `JavaInstallationSelectedEvent` |
| `DisableJavaInstallationCommand` | 禁用按钮 | 标记 Java 不可用或隐藏 | `JavaInstallationDisabledEvent` |

### 8.6 GameLink Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `AcceptGameLinkEulaCommand` | EULA 按钮 | 保存 EULA 状态，刷新页面 snapshot | `PixelGameLinkEulaAcceptedEvent` |
| `RefreshGameLinkAnnouncementsCommand` | Tools 页面加载 | 拉取公告，生成公告 snapshot | `PixelGameLinkAnnouncementsRefreshedEvent` |
| `LoginNatayarkCommand` | 登录按钮 | 执行 Natayark OAuth/profile 流程 | `PixelGameLinkLoggedInEvent` |
| `LogoutNatayarkCommand` | 退出登录按钮 | 清理账号状态 | `PixelGameLinkLoggedOutEvent` |
| `InstallEasyTierDependencyCommand` | 依赖安装按钮 | 下载/安装 EasyTier 依赖 | `PixelGameLinkDependencyInstalledEvent` |
| `RunGameLinkNatTestCommand` | NAT 测试按钮 | 执行网络测试并输出 snapshot | `PixelGameLinkNatTestCompletedEvent` |
| `CreateGameLinkLobbyCommand` | 创建房间 | 校验本地世界和账号状态，创建大厅 | `PixelGameLinkLobbyCreatedEvent` |
| `JoinGameLinkLobbyCommand` | 加入房间 | 校验房间码和依赖状态，加入大厅 | `PixelGameLinkLobbyJoinedEvent` |
| `LeaveGameLinkLobbyCommand` | 离开房间 | 释放 runtime 和大厅状态 | `PixelGameLinkLobbyLeftEvent` |

### 8.7 Settings 和 Personalization Commands

| Command | 触发入口 | Handler 职责 | 事件 |
| --- | --- | --- | --- |
| `SetPixelSettingValueCommand` | 设置控件变化 | 写入配置，执行联动规则 | `PixelSettingChangedEvent` |
| `ResetPixelSettingSectionCommand` | 重置分区 | 重置一组配置并发布刷新 | `PixelSettingSectionResetEvent` |
| `RefreshPersonalizationMediaCommand` | 个性化页面/启动 | 扫描背景和媒体资源 | `PixelPersonalizationMediaRefreshedEvent` |
| `ApplyBackgroundSeedCommand` | 背景切换 | 提取背景 seed，刷新主题指令 | `PixelBackgroundSeedAppliedEvent` |

### 8.8 Event Aggregator 订阅规则

- ViewModel 订阅 event bus 后更新 snapshot，不直接更新 Avalonia 控件。
- UI 层如需订阅，只能订阅 UI adapter 事件或 ViewModel property changed，并在窗口关闭时释放。
- 跨 slice 同步通过事件完成，例如 profile 选择后 Launch summary 刷新，设置变化后 Shell visibility 刷新。
- 长任务事件带 correlation id 或 task id，避免多个下载/启动流程互相覆盖 UI 状态。
- 事件对象保持不可变，包含最小必要字段；复杂展示仍由接收方 service 重新构建 snapshot。

## 9. 迁移阶段

### Phase 0：基线与保护

目标：

- 建立可重复构建、测试和扫描命令。
- 记录当前 UI 业务耦合点。
- 保证每轮迁移都有小测试保护。

任务：

- 运行 `dotnet build 'Pixel Craft Launcher.slnx' --no-restore`。
- 运行 `PCL.Core.Test` 中 Pixel 相关测试。
- 使用 `rg` 扫描 UI 对业务服务、配置、文件系统、静态状态的直接引用。
- 将扫描结果维护在本计划的“剩余耦合清单”中。

### Phase 1：基础设施完成

目标：

- Core DI、MediatR、Rx.NET event bus、Stateless factory、Serilog command logging 完整可用。

任务：

- 确认 `AddPclCore` 注册所有基础设施。
- 确认 `IPixelCommandBus` 使用 MediatR。
- 确认 `IPixelEventBus` 使用 Rx.NET。
- 确认状态机 factory 能创建 slice 状态机。
- 确认 Serilog 路径策略和 command pipeline logging 有测试。

验收：

- `PixelInfrastructureTest` 覆盖 DI、command bus、event bus、state machine、logging bootstrap。

### Phase 2：Shell / Navigation / Settings 收敛

目标：

- Shell 设置、导航、功能可见性、设置目录全部由 Core 输出。

任务：

- 完成 `PixelShellSettingsService`、`PixelSettingsObservationService`、`PixelSettingValueService` 使用。
- 将 View 中散落的 `FeatureVisibilityService` 调用逐步收敛到 Shell snapshot。
- MainWindow 只处理导航渲染和窗口主题应用。

验收：

- UI 不直接调用 `PixelSettingsBinder`。
- 设置变更订阅可释放。
- Shell 设置读取有 Core 测试。
- 主导航、Setup 分区和 Tools 入口可见性通过 `PixelShellVisibilityService` 查询，View 不直接调用 `FeatureVisibilityService.Is...Visible`。

### Phase 3：Launch 和 Profiles 收敛

目标：

- 首页启动、实例列表、实例分组、profile 显示规则迁移到 Core。

任务：

- 新增 `PixelLaunchInstanceListService`。
- 新增 `PixelProfileListService`。
- Launch/Profiles 页面改用 snapshot。
- 实例操作改用 command bus。

验收：

- `MainWindow.Pages.LaunchAndLeft.axaml.cs` 不再包含实例类型文案、图标、分组业务。
- `MainWindow.Pages.Profiles.axaml.cs` 不再直接调用 `MinecraftProfileService.GetProfileTypeName`。
- Core 测试覆盖实例列表和 profile snapshot。

### Phase 4：Java 和 Download 收敛

目标：

- Java setup 和下载页的业务状态迁移到 Core ViewModel。

任务：

- 新增 `PixelJavaViewModel` 或 `PixelJavaPageService`。
- Java UI 使用 snapshot 渲染。
- Java 操作按 stable id/path 发送 command。
- 下载页 loader/版本/按钮状态由 `PixelDownloadViewModel` 提供。

验收：

- `MainWindow.Pages.Setup.axaml.cs` 不再持有 `JavaEntry` 作为 UI 操作参数。
- Java fallback service 删除。
- Java 和 Download 关键路径测试通过。

### Phase 5：GameLink 收敛

目标：

- Tools/GameLink 页面只做视图渲染和平台动作。

任务：

- 新增 `PixelGameLinkToolsPageController` 或扩展 `PixelGameLinkViewModel`。
- Tools 页异步流程编排迁移到 Core。
- View 构造参数改为 snapshot。
- Core 包装运行时事件和提示结果。

验收：

- `MainWindow.Pages.Tools.axaml.cs` 不再直接调用 `PixelGameLinkService` fallback。
- Tools 页不直接解释 EasyTier/Natayark/Lobby 领域状态。
- GameLink EULA、登录、安装、NAT、创建、加入、离开测试通过。

### Phase 6：Personalization 和 Theme 边界收敛

目标：

- 个性化业务策略在 Core，Avalonia 项目只做媒体和主题适配。

任务：

- 将背景扫描、播放策略、背景 seed 策略进一步抽到 Core。
- 明确 `ThemeService`、`ColorSchemeService` 属于 UI adapter 还是 Core service；若包含业务规则则迁移。
- UI 服务只执行 Core 返回的 apply instruction。

验收：

- 个性化路径、扫描、播放策略测试通过。
- UI 中不直接读取个性化配置规则。

### Phase 7：清理和冻结架构

目标：

- 删除迁移期 fallback，统一命名，补齐测试和文档。

任务：

- 删除所有 `GetPixelXxxService() => new PixelXxxService()` fallback。
- 删除 UI 项目中旧 `Routing`、`Settings`、`ViewModels` 业务类。
- 扫描 UI 中 `Config`、`States`、`LobbyService`、`Path.Combine`、`File.Exists`、`PixelSettingsBinder`。
- 补充 architecture notes 和 contribution rules。

验收：

- UI 业务耦合扫描无高风险项。
- `dotnet build` 通过。
- Pixel Core 测试通过。

### Phase 8：Page/View 物理拆分完成

目标：

- 所有页面从 `MainWindow.Pages.*.axaml.cs` 迁出为独立 Page/View。
- `MainWindow.Pages.*` 过渡文件删除或只保留极薄的兼容转发。

任务：

- Launch、Download、Setup、Tools、Profiles 分别建立独立 root page view。
- 把页面 root view 的依赖通过构造函数注入，避免在 MainWindow 中直接拼装大块页面。
- 为每个 root page 增加最小 smoke test 或手动验收清单。
- 更新 `ViewLocator` 和导航 route 到新 Page/View。

验收：

- `Pixel Craft Launcher/Views/MainWindow.Pages.*.axaml.cs` 中不再存在大段页面构建代码。
- 每个 root page 可单独构造、单独渲染、单独替换 DataContext。
- MainWindow 的职责退回 shell、route host、dialog host、platform bridge。

## 10. 当前剩余耦合清单

截至当前重构状态，重点关注：

- `Pixel Craft Launcher/Views/MainWindow.Pages.Download.axaml.cs`
  - Java 列表渲染已改为通过 `PixelJavaEntrySnapshot` 的路径字段操作，不再从 UI 列表项持有 `JavaEntry`。
  - Java setup 控件拼装已拆入 `Views/Setup/JavaSetupPageView.cs`。
  - Java setup 对 `PixelJavaService` 的懒加载 fallback 已移除，服务通过 `MainWindow` 构造阶段/DI 提供。
  - Java 刷新、添加、默认选择和启用切换操作已收敛到 `PixelJavaService` 的 page 操作方法，View 不再直接构造 `RefreshJavaEntriesCommand`、`AddJavaEntryCommand`、`SelectDefaultJavaByPathCommand`、`ToggleJavaEntryEnabledByPathCommand`，也不再在 command bus 缺失时回退到直接 service 调用。
  - `PixelSettingsChangeService`、`PixelColorSchemeSettingsService`、`PixelSettingDisplayService`、`PixelMemoryPreviewService`、`PixelSettingValueService` 的迁移期懒加载 fallback 已移除，均通过 `MainWindow` 构造阶段/DI 提供。
  - `PixelMemoryPreviewSettingView` 已改为接收 `PixelMemoryPreviewSnapshot` provider，View 不再引用 `MinecraftInstanceInfo` 或 Minecraft 领域命名空间。
  - 下载任务列表和任务详情卡片已改为消费 `PixelDownloadTaskSnapshot`，任务状态文案、图标、可取消状态和可见性判断由 `PixelDownloadViewModel` 提供，View 不再直接引用 `MinecraftDownloadTaskInfo` / `NDlTaskState`。
  - 下载任务详情页的任务组/收尾中/空态、任务组标题、可取消任务 id 和进度百分比文本已迁入 `PixelDownloadTaskDetailsPageSnapshot` / `PixelDownloadTaskSnapshot.ProgressPercentText`，View 不再自行筛选可取消任务或格式化进度。
  - 下载任务详情路由的默认 task id、可见任务/待处理操作判断和自动返回判断已迁移到 `PixelDownloadViewModel`，`MainWindow` 不再直接组合 `SelectedTask`、`Tasks` 和 pending install 状态。
  - 下载任务详情右页已拆出为 `Views/Pages/DownloadTaskDetailsView.cs`，`MainWindow` 仅负责传入 `PixelDownloadViewModel` 和提示回调。
  - 下载任务统计左页已拆出为 `Views/Pages/DownloadManagerStatsView.cs`，总进度、速度、剩余文件和线程数展示不再由 `MainWindow` 直接拼装。
  - 下载任务统计值已迁入 `PixelDownloadManagerStatsSnapshot` / `PixelDownloadViewModel.GetManagerStatsSnapshot()`，平均进度、速度格式化、剩余文件和线程数计算不再由 View 执行。
  - 下载安装面板的 summary、hint、loader choice loading/error/empty/ready 状态已迁入 `PixelDownloadInstallPanelSnapshot`，`DownloadInstallPanelView` 只按 Core 输出的页面状态渲染。
  - 下载版本列表页的可见分组、卡片标题、展开状态和空列表文案已迁入 `PixelDownloadVersionListPageSnapshot`，`DownloadVersionListView` 不再直接过滤版本组或判断 busy/empty 文案。
  - 下载安装左栏的输入初始值、安装清单、原版提示显隐和安装按钮状态已迁入 `PixelDownloadInstallSidebarSnapshot`，按钮图标由 `PixelDownloadInstallStateSnapshot.PrimaryActionIcon` 输出。
  - 下载右页构建前的版本刷新判断已迁入 `PixelDownloadViewModel.ShouldRefreshVersionsBeforeRightPage`，`DownloadRightPageFactory` 不再直接组合 `Versions.Count` / `IsBusy`。
  - Minecraft 版本列表和安装清单中的版本项已改为消费 `PixelDownloadVersionSnapshot` / `PixelDownloadVersionGroupSnapshot`；版本分组、标题、图标、版本类型文案、搜索过滤和 wiki 搜索后缀由 `PixelDownloadViewModel` 提供，View 不再直接引用 `MinecraftVersionManifestEntry`。
  - Minecraft 安装版本列表和客户端保存版本列表已拆出为 `Views/Pages/DownloadVersionListView.cs`，`MainWindow` 仅保留打开安装 route、文件夹选择、外链打开等平台回调。
  - Loader/addon 安装选择卡片、兼容性禁用原因、安装提示和安装 checklist 已改为消费 `PixelLoaderChoiceSnapshot`、`PixelLoaderChoiceItemSnapshot`、`PixelInstallHintSnapshot`、`PixelInstallChecklistItemSnapshot`；View 通过 action id 回调 `PixelDownloadViewModel`，不再直接引用 `MinecraftLoaderVersionEntry`、`MinecraftAddonFileEntry`、`MinecraftAddonKind`、`MinecraftLoaderKind`。
  - Loader/addon 安装选择右页已拆出为 `Views/Pages/DownloadInstallPanelView.cs`，`MainWindow` 仅负责 route 分发并传入刷新 loader choices 的回调。
  - 安装按钮启用状态、忙碌文案、顶部副标题、pending 下载判断已收敛到 `PixelDownloadInstallStateSnapshot`；下载分类刷新分支已收敛到 `PixelDownloadCategoryRefreshService`，`PixelDownloadViewModel.RefreshDownloadCategoryAsync` 只执行 service 输出的刷新动作。
  - 安装左页的实例名/目录输入、安装清单和开始安装按钮已拆出为 `Views/Pages/DownloadInstallSidebarView.cs`，`MainWindow` 仅传入 `PixelDownloadViewModel` 和刷新回调。
  - 下载主侧栏导航已拆出为 `Views/Pages/DownloadSidebarView.cs`，`MainWindow` 仅传入当前分类、安装选择关闭回调、导航回调和刷新回调。
  - 下载左页 host 分派已抽出为 `Views/Pages/DownloadLeftPageFactory.cs`，普通下载侧栏、安装侧栏和任务统计左页的选择不再散落在 `MainWindow.Pages.LeftNavigation.axaml.cs`。
  - MainWindow 中下载右侧页面刷新所需的属性名判断已迁移到 `PixelDownloadViewModel.ShouldRefreshRightPageForProperty`，窗口不再硬编码 `MergedSelection`、loader choices 和 task 相关属性列表。
  - 下载页实例管理列表项、隐藏图标、选中态、空状态、按钮文案、tooltip 和 Mods/存档按钮启用状态已收敛到 `PixelInstanceManagementSnapshot`。
  - 下载版本加载页已拆出为 `Views/Pages/DownloadLoadingPageView.cs`，`MainWindow` 仅负责把 Core loading state 包装后的 UI adapter 传入视图。
  - 下载占位页已拆出为 `Views/Pages/DownloadPendingPageView.cs`，标题/说明由 route 映射提供，实例管理区域作为独立 view 传入。
  - 下载占位页标题/说明、迁移状态区标题/说明和实例管理区标题已收敛为 `PixelDownloadPendingPageSnapshot`，由 `PixelDownloadViewModel.GetPendingPageSnapshot` 根据分类输出，`MainWindow` / `DownloadPendingPageView` 不再硬编码这些下载占位文案。
  - 下载主侧栏分类分组、标题、图标和选中态已收敛为 `PixelDownloadSidebarSnapshot`，`DownloadSidebarView` 只负责渲染 Core snapshot 并转发导航/刷新回调。
  - 下载主侧栏刷新按钮 tooltip 已收敛到 `PixelDownloadSidebarItemSnapshot.RefreshButtonTooltip`，`DownloadSidebarView` 不再硬编码刷新文案。
  - 下载安装左栏、版本列表和任务详情页的分区标题、按钮 tooltip、空态/加载态和文件选择器标题已收敛到 `PixelDownloadViewModel` messages。
  - 下载任务统计页的统计项标题已收敛为 `PixelDownloadManagerStatsMessages`。
  - 下载右页目标判断已收敛为 `PixelDownloadRightPageKind` / `PixelDownloadViewModel.GetRightPageKind`，`MainWindow` 不再自行判断 Minecraft 安装列表、客户端列表、加载页、任务详情和占位页的切换条件。
  - 下载分类刷新提示文案已迁移到 `PixelDownloadViewModel.GetRefreshStartMessage`。
  - 下载 install/task/secondary route 分类已迁入 `MainWindowViewModel`，Avalonia `MainWindow` 仅保留薄 wrapper，不再维护下载 route 结构判断的权威实现。
  - 下载任务详情 route 的 task id 参数解析已迁入 `MainWindowViewModel.GetDownloadTaskRouteTaskId`，窗口不再直接读取 route 参数字典。
  - 全局 secondary、Profile manager、Launch instance route 分类已迁入 `MainWindowViewModel`，窗口不再直接解释这些 route segment/parameter。
  - Profile manager route 的 action/server 参数已收敛为 `PixelProfileManagerRouteSnapshot` / `PixelProfileManagerPageKind`，窗口不再 switch 原始字符串。
  - Profile 离线档案、微软登录、Authlib 登录和第三方验证服务器表单/弹窗的标题、hint、按钮文案已收敛到 `PixelProfileListService` 输出的 snapshots/messages。
  - Profile manager 左侧栏的账号添加入口、第三方验证服务器分组和底部添加按钮已收敛为 `PixelProfileManagerSidebarSnapshot`。
  - Profile 操作失败的状态行和 hint 展示已收敛为 `PixelProfileOperationFailurePresentation`，Profile View 不再直接展示 `Exception.Message`。
  - 顶栏二级标题可见性和标题文案已收敛为 `PixelSecondaryTitleSnapshot`，窗口不再重复解释 Profile/Download/Launch instance route 的标题规则。
  - 顶栏主导航文字和浮动操作按钮 tooltip 已收敛到 `MainWindowViewModel`，`MainWindow.axaml` 不再硬编码启动/下载/设置/工具与下载管理/强制关闭文案。
  - 通用 placeholder 页面标题和说明已收敛为 `PixelPlaceholderPageSnapshot`，Launch/Download/Setup/Tools 的占位文案由 Core shell 输出。
  - 通用 placeholder 左侧列表项已收敛为 `PixelPlaceholderLeftItemSnapshot`，窗口不再按 `MainPageKind` 拼左侧占位 item 文案、图标和选中态。
  - 主页面切换的 `MainPageKind` 到 `RouteNode` 映射已迁入 `MainWindowViewModel.GetMainPageRoute` / `NavigateMainPage`，窗口不再自行拼主页面 route。
  - Launch instance 导航已通过 `MainWindowViewModel.NavigateLaunchInstances` 暴露；Views 中已无直接 `PixelRoutes.*` 调用。
  - Natayark 登录开始提示和异常提示已收敛为 `PixelGameLinkViewModel` helper，Tools 页不再拼接登录提示文案。
  - Tools/GameLink 左侧栏标题、入口文案、说明和图标已收敛到 `PixelGameLinkSidebarSnapshot`。
  - Tools/GameLink 隐藏入口占位、复制虚拟 IP 弹窗和手动端口弹窗文案已收敛到 `PixelGameLinkViewModel` snapshots。
  - Java 设置页添加/刷新/信息/打开目录失败提示已收敛为 `PixelJavaService` helper，Download/Setup partial 不再拼接这些 Java 页面文案。
  - Java 程序选择器标题和文件类型名已迁移到 `PixelJavaService` helper，Download/Setup partial 仅保留 Avalonia 文件选择器适配。
  - Java 设置页卡片标题、toolbar 按钮、自动选择/空态/不可用提示、item 操作 tooltip 和启用/禁用结果提示已收敛为 `PixelJavaPageMessages`。
  - `JavaSetupPageView` 通过构造参数接收 `PixelJavaPageMessages`，不再直接调用 `PixelJavaService.GetPageMessages()`；消息获取集中到注入的 `PixelJavaService.GetMessages()`。
  - Java 添加、刷新、信息弹窗、选择器、打开文件夹失败和 Java 操作失败提示均改为从 `PixelJavaPageMessages` 读取，Download/Setup partial 与 `JavaSetupPageView` 不再直接展示异常消息。
  - Java 设置页文件选择、刷新提示、信息弹窗、打开目录和页面刷新桥接已抽出为 `Views/Setup/JavaSetupPlatformBridge.cs`，`MainWindow.Pages.Setup.axaml.cs` 不再直接承载 Java 平台操作流程。
  - Java 默认选择和启用/禁用的成功提示、失败提示与刷新页面操作已收敛到 `JavaSetupPlatformBridge`，`JavaSetupPageView` 只保留可用性校验和控件事件转发。
  - Setup 外链打开失败和分区重置完成提示已收敛为 `PixelSetupNavigationMessages`，View 通过注入的 `PixelSetupNavigationService` 读取消息 snapshot。
  - 背景目录打开失败提示已收敛为 `PixelPersonalizationMessages`，窗口通过注入的 Core `PixelPersonalizationService` 读取消息，Avalonia `PersonalizationPlatformBridge` 仅保留背景渲染/目录打开平台适配。
  - 设置变更 effect 的 UI 执行桥接已抽出为 `Views/Setup/SetupSettingsPlatformBridge.cs`，设置通知展示、刷新 Setup 右页和刷新 Shell 主题的分派离开 `MainWindow.Pages.Setup.axaml.cs`。
  - 设置变更后的执行意图已收敛为 `PixelSettingChangePresentation`，RunWait 可见性边界、Setup 右页刷新、Shell 主题刷新和通知意图由 Core 合并输出。
  - Setup 外链打开、背景目录打开和 About 页快捷跳转桥接已抽出为 `Views/Setup/SetupNavigationPlatformBridge.cs`，`MainWindow.Pages.Setup.axaml.cs` 只保留跨页面复用的 `OpenExternalUrl` 薄转发。
  - `MainWindow.Pages.Setup.axaml.cs` 中的 `GetPixelSettingsChangeService` / `GetPixelSettingDisplayService` 字段别名已移除，页面直接使用构造注入服务。
  - 内存预览展示文案和内存不足 warning 已收敛为 `PixelMemoryPreviewTextSnapshot`。
  - 已移除未被调用的旧 `BuildDownloadToolbar` 内联 UI，避免继续保留绕过 snapshot/command-first 路径的下载操作入口。
  - `PixelDownloadViewModel` 中刷新版本、刷新 loader、安装、整合安装、保存客户端核心和保存服务端 Jar 已统一通过 `IPixelCommandBus` 发送 command，不再在 command bus 缺失时回退到直接调用下载领域 service。
  - 下载窗口提示、版本加载、安装面板、安装侧栏、版本列表、任务详情和统计文案已聚合为 `PixelDownloadPageMessages`，Download factory/window 通过 `_downloadViewModel.GetPageMessagesSnapshot()` 分发子消息，旧静态 helper 仅保留为兼容转发。
  - 下载版本列表、任务详情、下载管理统计和安装侧栏 View 已改为构造注入 message snapshot，leaf View 不再直接调用 `PixelDownloadViewModel.Get...Messages()`。
  - 下载/安装页面组合视图已继续拆分到 `DownloadPageView`、左右页 factory、install/sidebar/task 叶子 View；后续重点转为把剩余边缘保存/刷新副作用纳入明确 command/event。
  - 下载占位页中的实例管理面板已拆出为 `Views/Pages/DownloadInstanceManagementView.cs`，实例刷新、选择、打开文件夹和子目录打开适配已收敛到 `Views/Pages/DownloadPagePlatformBridge.cs`。
  - 下载保存目录选择和实例管理面板平台适配已抽出为 `Views/Pages/DownloadPagePlatformBridge.cs`，`MainWindow.Pages.Download.axaml.cs` 仅保留下载右页 host 和 bridge/factory 组装。
  - 下载任务详情右页构造已统一回到 `DownloadRightPageFactory`，`MainWindow` 中的 `BuildDownloadTaskDetailsPage` 兼容入口已删除；factory 会先判断目标页面类型，避免任务详情 route 触发无关版本刷新。
  - 下载右页进入时的数据刷新意图已收敛为 `PixelDownloadRightPagePresentation` / `PixelDownloadRightPageRefreshAction`，`DownloadRightPageFactory` 只消费 Core 输出的页面类型和刷新动作。
  - 通用设置分区已拆出为 `Views/Setup/SettingsSectionView.cs`，`MainWindow` 仅提供 setting 可见性判断和具体控件构建回调。
  - GameLink 设置页已拆出为 `Views/Setup/GameLinkSetupPageView.cs`，`MainWindow` 仅提供设置控件构建和网络测试面板。
  - GameLink 设置页网络测试面板的初始状态、结果状态、按钮文案和完成/失败提示已迁移到 `PixelGameLinkNatStatusSnapshot` / `PixelGameLinkNetworkTestMessages`。
  - GameLink 设置页网络测试兜底异常提示改为通过 `PixelGameLinkNetworkTestMessages.GetFailureMessage` 生成，View 不再拼接异常消息。
  - About 页已拆出为 `Views/Setup/AboutPageView.cs`，`MainWindow` 仅提供版本/许可数据、外链打开回调和设置快捷导航回调。
  - 已完成：下载页 root 外壳已抽出为 `Views/Pages/DownloadPageView.cs`，统一组装 `DownloadLeftPageFactory` / `DownloadRightPageFactory`、平台 bridge 和下载页回调；`MainWindow.Pages.Download` / `MainWindow.Pages.LeftNavigation` 只保留路由入口委托。

- `Pixel Craft Launcher/Views/MainWindow.Pages.Tools.axaml.cs`
  - 已新增 `PixelGameLinkToolsPageController`，公告加载、Natayark 登录/退出、NAT 测试、EasyTier 安装和世界发现开始通过 Core controller/command bus 编排。
  - 创建/加入/离开大厅已接入 `PixelGameLinkToolsPageController` 优先路径。
  - runtime 初始化、runtime 事件订阅和迁移期 `PixelGameLinkService` fallback 已收敛到 `PixelGameLinkToolsPageController`。
  - Tools 顶层入口和控件验收入口可见性已通过 `PixelShellVisibilityService` 查询，View 不再直接调用 `FeatureVisibilityService.IsToolVisible`。
  - `GameLinkAnnouncementHintView`、`GameLinkCreateCardView`、`GameLinkFinishPanelView` 已改为接收 `PixelGameLink*Snapshot`，不再直接接收 `LinkAnnounceInfo`、`FoundWorld`、`LobbyState`、`PlayerProfile` 等 Link 领域模型。
  - 创建大厅卡片的可创建状态和默认世界选择已迁入 `PixelGameLinkCreateCardSnapshot.CanCreate` / `DefaultWorldIndex`，`GameLinkCreateCardView` 不再根据 combo item 数量推断按钮启用和默认选择。
  - `GameLinkEulaCardView` 和 `GameLinkFooterView` 的协议文案、服务链接、友情链接、按钮和撤销授权确认文案已迁移到 `PixelGameLinkEulaSnapshot` / `PixelGameLinkFooterSnapshot`。
  - `GameLinkFinishPanelView` 的大厅信息行、成员标题、空成员提示和操作按钮文案已迁移到 `PixelGameLinkFinishSnapshot`。
  - 大厅编号复制、手动端口校验提示和 NAT 测试结果弹窗已迁移到 `PixelGameLinkViewModel` helper / `PixelGameLinkNatDialogSnapshot`。
  - 创建/加入大厅的预检失败页面切换已迁移到 `PixelGameLinkToolsPageController`；EULA required 会在 Core controller 内切换到 EULA 子页，Tools 页不再判断 `PixelGameLinkPrecheckFailureKind`。
  - Natayark 登录开始提示是否展示已迁移到 `PixelGameLinkToolsPageController.GetNatayarkLoginStartMessage()`，Tools 页不再读取登录状态来决定提示。
  - Natayark 登录/退出异常已由 `PixelGameLinkToolsPageController.ToggleNatayarkLoginAsync()` 转换为失败结果，Tools 页不再 catch 异常并调用登录异常文案 helper。
  - EasyTier 依赖安装结果已收敛为 `PixelGameLinkDependencyInstallResult`，成功/失败文案由 Core controller 返回，Tools 页不再调用 `PixelGameLinkViewModel.GetEasyTierInstallResultMessage`。
  - 已完成：Natayark 登录、EasyTier 安装和大厅操作失败提示收敛为 `PixelGameLinkNotificationSnapshot`，Tools platform bridge 只把 Core notification kind 适配到 Avalonia hint 样式。
  - Tools 页 NAT 测试展示意图已收敛为 `PixelGameLinkNatTestPresentation` / `PixelGameLinkToolsPageController.RunToolsNatTestAsync()`；Tools 页不再调用 `PixelGameLinkViewModel.GetNatDialogSnapshot`，Setup 面板仍使用原始 `PixelGameLinkNatTestResult` 更新状态行。
  - Natayark 登录、Tools NAT 测试、EasyTier 安装、创建/加入/离开大厅操作桥接已抽出为 `Views/Pages/GameLinkToolsPlatformBridge.cs`；GameLink partial 只负责把卡片事件绑定到 bridge 方法。
  - Finish 面板复制大厅编号提示和虚拟 IP 弹窗已迁入 `PixelGameLinkFinishSnapshot`，Tools 页不再调用 `GetLobbyCodeCopiedMessage` / `GetVirtualIpDialogSnapshot`。
  - Finish 面板复制大厅编号、复制虚拟 IP、玩家详情弹窗、Footer 禁用确认和手动端口弹窗已抽出为 `Views/Pages/GameLinkDialogPlatformBridge.cs`；GameLink partial 只传入 snapshot 和 bridge callback。
  - 手动端口弹窗标题、输入 hint、按钮和校验提示已迁入 `PixelGameLinkCreateCardSnapshot.ManualPortDialog`，Tools 页不再调用 `GetManualPortDialogSnapshot` / `GetInvalidPortMessage`。
  - GameLink 设置页网络测试面板的初始状态、按钮文案、完成/失败通知和结果状态已迁入 `PixelGameLinkNetworkTestPanelSnapshot` / `PixelGameLinkSetupNatTestResult`，View 不再直接调用 NAT status/message helper。
  - Tools 隐藏入口文案已迁入 `PixelShellVisibilityService.GetToolHiddenMessage`，Tools 页不再调用 GameLink ViewModel 的隐藏消息 helper。
  - GameLink Tools 页面 host、运行时订阅、公告轮播、联机操作和剪贴板兼容桥接已抽出到 `Views/MainWindow.Pages.GameLink.axaml.cs`；`MainWindow.Pages.Tools.axaml.cs` 收缩为工具入口和控件验收页 host。
  - 剪贴板兼容反射已抽出为 `Views/Pages/ClipboardCompatBridge.cs`，GameLink partial 仅保留薄包装调用，不再直接引用 `System.Reflection`。
  - `MainWindow.Pages.Tools.axaml.cs` 的旧 using 已清理，只保留工具入口 host 实际需要的 Shell/ViewModel/View 依赖。
  - GameLink 公告加载和轮播 timer 已抽出为 `Views/Pages/GameLinkAnnouncementPlatformBridge.cs`，窗口关闭时通过 `DisposeGameLinkSubscriptions()` 释放 announcement bridge 与 runtime 订阅。
  - GameLink runtime 订阅、runtime notification 展示、runtime rebuild 刷新和 EasyTier runtime install callback 已抽出为 `Views/Pages/GameLinkRuntimePlatformBridge.cs`。
  - 已完成：禁用授权确认后的 reset 操作已收敛到 `PixelGameLinkToolsPageController.ResetAuthorizationAsync`，返回 `PixelGameLinkAuthorizationResetResult` / `PixelGameLinkNotificationSnapshot`；`GameLinkDialogPlatformBridge` 只展示确认弹窗、映射 hint 并刷新页面。
  - 已完成：runtime 刷新展示意图已收敛为 `PixelGameLinkRuntimeRefreshPresentation`，`GameLinkRuntimePlatformBridge` 只负责把通知类型映射为 Avalonia hint/message 并调用页面刷新。
  - 后续继续把 GameLink Tools 中剩余 dialog/runtime 回调进一步收敛为更窄的 platform adapter / controller callback，优先处理剪贴板和弹窗边界。

- `Pixel Craft Launcher/Views/MainWindow.Pages.LaunchAndLeft.axaml.cs`
  - `PixelLaunchViewModel` 中选择档案、启动、强制关闭、导出日志和进程监控的迁移期 command bus fallback 已移除，业务动作统一要求 `IPixelCommandBus`。
  - 实例列表分组、图标、类型文案已迁移到 `PixelLaunchInstanceListService`。
  - Launch 左侧二级动作、实例列表动作和测试工具入口可见性已迁移到 `PixelLaunchSidebarService`。
  - Setup 左侧分区入口可见性已通过 `PixelShellVisibilityService` 查询，View 不再直接调用 `FeatureVisibilityService.IsSetupSectionVisible`。
  - `PixelHomepageService`、`PixelLaunchFolderService`、`PixelLaunchInstanceListService`、`PixelSetupNavigationService` 的迁移期懒加载 fallback 已移除，均通过 `MainWindow` 构造阶段/DI 提供。
  - `MainWindow.Pages.LaunchAndLeft.axaml.cs` 中的 `GetPixelHomepageService` / `GetPixelLaunchFolderService` / `GetPixelLaunchInstanceListService` / `GetPixelSetupNavigationService` 字段别名已移除，页面直接使用构造注入服务。
  - 自定义主页的预设展示、图片加载失败、WebView 不可用和外部浏览器按钮文案已收敛到 `PixelHomepageSnapshot`。
  - Launch 文件夹列表已改为使用 `PixelLaunchFolderSnapshot`，View 不再直接使用 `MinecraftFolderInfo` 作为文件夹 UI 模型。
  - Launch 当前档案名、验证方式、实例名、实例路径和可启动状态已收敛为 `PixelLaunchSummarySnapshot` / `PixelLaunchSummaryService`；账号面板和启动中验证方式不再直接绑定 `SelectedProfile` / `SelectedProfileMethod` / `SelectedInstancePath`。
  - 选中实例文件夹打开状态和目录路径已迁移到 `PixelLaunchSelectedInstanceFolderSnapshot`，打开按钮不再直接解释 `SelectedInstance` 是否为空。
  - 实例设置占位、实例列表刷新、文件夹路径不可用、添加实例文件夹和打开文件夹失败提示已迁移到 `PixelLaunchPageMessages` / `PixelLaunchSidebarService` helper。
  - 自定义主页图片加载失败提示改为通过 `PixelHomepageSnapshot.GetImageLoadFailedMessage` 生成，`LaunchHomepageView` 不再拼接异常消息。
  - 强制关闭成功提示、正常退出提示和启动日志导出提示已收敛为 `PixelLaunchWindowMessages`，窗口生命周期事件通过 `_launchViewModel.GetWindowMessagesSnapshot()` 读取 Core snapshot 并展示 hint。
  - 启动失败和崩溃弹窗标题、正文、退出码格式化和空分析兜底已迁移到 `PixelLaunchDialogService`；`PixelLaunchViewModel` 通过 `LaunchIssueDialogRequested` 发布 `PixelLaunchIssueDialogSnapshot`，窗口只负责展示弹窗按钮和日志导出/打开回调。
  - 通用左侧导航、Tools 侧栏、Download 侧栏和 Setup 侧栏已抽出到 `Views/MainWindow.Pages.LeftNavigation.axaml.cs`，Launch partial 不再混合其它主页面侧栏。
  - Launch 实例文件夹左栏、实例选择右页的页面组装已抽出到 `Views/MainWindow.Pages.LaunchInstances.axaml.cs`，实例刷新、选择、文件夹打开和添加实例文件夹平台流程已收敛到 `Views/Pages/LaunchInstancesPlatformBridge.cs`。
  - Setup 初始 section 读取已移入 `Views/MainWindow.Pages.Setup.axaml.cs`。
  - 已完成：Launch 左侧二级动作与测试工具入口可见性收敛为 `PixelLaunchSidebarSnapshot`；当前 UI 已无直接 Link/Lobby/EasyTier/Natayark 状态解释。

- `Pixel Craft Launcher/Views/MainWindow.Pages.Profiles.axaml.cs`
  - Profile 类型文案、图标和离线可编辑判断已迁移到 `PixelProfileListService`。
  - Profile 选择/删除已新增 by-id command 并改为通过 snapshot id 操作。
  - 离线编辑保存已新增 by-id command，command bus 路径不再传 `MinecraftProfile` 本体。
  - 选择、删除、离线保存、微软登录、Authlib 登录和验证服务器保存的 UI helper 已移除 `MinecraftProfileService` fallback，写操作强制通过 `IPixelCommandBus`。
  - Profiles 页 helper 不再返回 `MinecraftProfile` / `AuthServerPreset` 领域对象；页面只等待 command 完成后刷新 snapshot。
  - Profile 写操作已收敛到 `PixelProfilePageService`，View 不再直接构造 `SaveOfflineMinecraftProfileByIdCommand`、`AddMicrosoftMinecraftProfileCommand`、`AddAuthlibMinecraftProfileCommand`、`AddAuthServerPresetCommand`、`SelectMinecraftProfileByIdCommand`、`RemoveMinecraftProfileByIdCommand`。
  - 离线编辑表单初始值、标题、主按钮文案和 UUID 模式选择已迁移到 `PixelOfflineProfileEditorSnapshot`，View 不再把 `MinecraftProfile` 本体传入离线编辑表单。
  - 离线 UUID 模式输入已新增 `PixelOfflineUuidMode`，by-id command handler 在 Core 内映射到 Minecraft 领域枚举，View 不再引用 `OfflineUuidMode` 或 `PCL.Core.Minecraft.Profiles`。
  - 第三方验证服务器列表项、Authlib 登录表单服务器信息和添加服务器表单默认值已迁移到 `PixelAuthServerSnapshot`、`PixelAuthlibProfileFormSnapshot`、`PixelAuthServerEditorSnapshot`。
  - Profile 列表、第三方验证服务器列表、离线编辑表单和 Authlib 表单的读取入口已收敛到 `PixelProfilePageService`，View 不再直接读取 `ProfileService.Profiles`、`ProfileService.AuthServers` 或 `ProfileService.SelectedProfile`。
  - Profile 列表页 hero、空态和 item 操作 tooltip 已收敛到 `PixelProfileListPageSnapshot`，`ProfileListPageView` 只渲染 snapshot。
  - 微软/Authlib 登录进度文案格式化已迁移到 `PixelProfileListService.FormatLoginProgress`，View 不再重复拼接登录阶段、消息和百分比。
  - 微软/Authlib 登录进度和设备码 callback 已新增 `PixelProfileLoginProgressAdapter` / `PixelProfileUiCallbacks`，View 不再直接引用 `MinecraftProfileLoginProgress`、`IMinecraftProfileUiCallbacks`、`DeviceCodePrompt`。
  - 微软设备代码弹窗标题、正文、按钮文案、复制代码和打开 URL 已迁移到 `PixelProfileDeviceCodeDialogSnapshot`。
  - Profile UUID 和微软设备代码复制已改为复用 `Views/Pages/ClipboardCompatBridge.cs`，Profiles partial 不再直接调用 Avalonia Clipboard API。
  - Profile 列表操作结果、Microsoft/Authlib/Offline/AuthServer 表单初始提示文案已迁移到 `PixelProfilePageMessages`，View 不再硬编码这些页面级业务提示。
  - `PixelProfileListService` 的迁移期懒加载 fallback 已移除，通过 `MainWindow` 构造阶段/DI 提供。
  - `MainWindow.Pages.Profiles.axaml.cs` 中的 `GetPixelProfileListService` 字段别名已移除，登录进度和 UI callback adapter 直接使用构造注入服务。
  - Profile 右侧页面表单已抽出到 `Views/MainWindow.Pages.ProfileForms.axaml.cs`，主 Profiles partial 只负责 route/list host 和登录 callback adapter。
  - Profile 弹窗构建已抽出到 `Views/MainWindow.Pages.ProfileDialogs.axaml.cs`，离线、Microsoft、Authlib 和第三方服务器弹窗与列表/路由 host 分离。
  - Profile 表单和弹窗中的保存/登录/添加服务器重复操作流已抽出到 `Views/Pages/ProfileOperationPlatformBridge.cs`，异常展示、刷新 Profile 绑定、刷新管理页和完成提示不再散落在 `MainWindow.Pages.ProfileForms.axaml.cs` / `MainWindow.Pages.ProfileDialogs.axaml.cs`。
  - Profile 列表中的选择、复制 UUID 和删除操作也已收敛到 `ProfileOperationPlatformBridge`，`MainWindow.Pages.Profiles.axaml.cs` 只负责列表 host、路由和登录 callback adapter。
  - Profile 表单和弹窗按钮 busy 状态包装已收敛为 `ProfileOperationPlatformBridge.RunWithBusyStateAsync`，form/dialog partial 不再重复 `IsEnabled` try/finally。
  - Authlib 多角色选择已新增 `PixelAuthlibProfileChoiceDialogSnapshot`，`MainWindow.Pages.Profiles` 只显示 Core snapshot 并把选中索引返回给 `PixelProfileUiCallbacks`。
  - 已完成：Profile 保存/登录/添加服务器结果状态收敛为 `PixelProfileOperationResultSnapshot`，`PixelProfilePageService` 负责把成功/失败映射为 UI-ready status/hint；`ProfileOperationPlatformBridge` 只消费结果并执行刷新、关闭和提示。

- `Pixel Craft Launcher/Views/Setup/PixelColorSchemeSettingView.cs`
  - 颜色 seed 解析、格式化、Avalonia `Color` 转换和预览色计算已迁移到 `PixelColorSchemeSettingsService`。
  - View 仍负责 Avalonia 颜色选择器和控件刷新，主题刷新、深色模式读取与颜色模式变更订阅已抽出为 `PixelColorSchemeThemeBridge`。
  - 后续可继续确认背景 seed 与 `PersonalizationPlatformBridge` 的主题联动边界。

- `Pixel Craft Launcher/Services/PersonalizationPlatformBridge.cs`
  - 已明确为 UI platform bridge，背景目录、扫描、轮播、音频策略由 `PixelPersonalizationService` 承载。
  - 背景图片 seed 提取和 `ColorSchemeService.SetBackgroundSeed` 联动已迁移到 `PixelPersonalizationService.ApplyBackgroundSeedAsync`。
  - UI 服务不再直接调用 `ColorSchemeService` 或 `ImageColorExtractor`，仅负责 Avalonia/LibVLC 控件创建、计时器和播放适配。

- `Pixel Craft Launcher/Views/MainWindow.axaml.cs`
  - `GetPixelShellSettingsService` / `GetPixelSettingsObservationService` / `GetPixelSettingValueService` 字段别名已移除，Shell 设置、消息和设置观察均直接使用构造注入服务。
  - 主题、个性化、设置观察和 shell 外观刷新逻辑已抽出到 `Views/MainWindow.SettingsAndTheme.axaml.cs`，Shell 主题事件订阅和深色模式读取已由 `Views/ShellThemePlatformBridge.cs` 统一封装。
  - 全局 secondary route、下载任务 secondary 页刷新、下载二级页返回和顶栏二级标题切换逻辑已抽出到 `Views/MainWindow.Routing.axaml.cs`，`MainWindow.axaml.cs` 进一步收缩为顶层窗口生命周期与 host 编排。
  - 消息弹窗、hint、浮动操作按钮、启动问题弹窗和打开路径/链接 platform bridge 已抽出到 `Views/MainWindow.Overlays.axaml.cs`，主窗口文件继续收缩。
  - 通用 message/form 弹窗 host、按钮事件和关闭逻辑已抽出为 `Views/PixelDialogPresenter.cs`，`MainWindow.Overlays` 只保留默认文案补齐和业务型弹窗入口。
  - hint 去重、弹出/隐藏动画、颜色映射和生命周期已抽出为 `Views/PixelHintPresenter.cs`，`MainWindow.Overlays` 只保留 `ShowHint` API 委托。
  - 浮动操作按钮注册、排序、显隐动画和下载任务 ripple 已抽出为 `Views/PixelFloatingActionPresenter.cs`，`MainWindow.Overlays` 只保留按钮点击 wiring 和可见性条件。
  - 主/嵌套 page host 转场、Launch 页面刷新、标题切换动画、左栏宽度动画和右页 enter 动画已抽出到 `Views/MainWindow.PageHost.axaml.cs`。
  - route 变更分派、嵌套路由刷新和主 route 应用已抽出到 `Views/MainWindow.RouteHost.axaml.cs`。
  - route/page 到左右 host 内容的构建分派已集中到 `Views/MainWindow.Routing.axaml.cs`，主 route 应用和主题刷新复用同一套 `BuildRouteLeftPage` / `BuildRouteRightPage` helper。
  - secondary 左栏宽度已收敛为 `MainWindowViewModel.SecondaryLeftPaneWidth`，Views 中不再散落下载/全局 secondary 的裸 `300d` 宽度值。
  - `MainWindow.Routing.axaml.cs` 中未使用的 `IsDownloadInstallRoute(RouteNode)` 兼容 wrapper 已删除，route 判断继续委托 `MainWindowViewModel`。
  - Shell 导航按钮 wiring、标题栏拖动、焦点处理和关闭生命周期已抽出到 `Views/MainWindow.ShellLifecycle.axaml.cs`。
  - `MainWindow` 已移除直接 `IPixelCommandBus` 注入；窗口用户动作通过 Core ViewModel/service 暴露的方法进入 command bus。
  - Launch 主按钮的主/副文本、主动作和启用状态已迁入 `PixelLaunchMainButtonSnapshot`，`LaunchMainButtonView` 不再直接根据 `Instances.Count` / `CanLaunch` 分派下载或启动。
  - Launch 启动中面板的可见性、标题、实例名、阶段、登录方式、初始进度和进度文本已迁入 `PixelLaunchLaunchingPanelSnapshot`，`LaunchLaunchingPanelView` 只保留进度条动画和绑定。
  - Launch 实例选择页的分组列表和空态已迁入 `PixelLaunchInstanceSelectionPageSnapshot`，`LaunchInstanceSelectionPageView` 不再自行汇总分组实例数量判断空列表。
  - Shell 仍保留 Avalonia 主题应用、窗口生命周期、弹窗宿主、背景 overlay 和 route host 等 UI/platform 职责。

## 11. 测试策略

### 11.1 Core 基础设施测试

覆盖：

- DI 注册完整性。
- MediatR command bus。
- Rx.NET event bus 发布和订阅。
- Stateless transition。
- Serilog bootstrap 路径。
- UI adapter 默认实现。

### 11.2 Slice 测试

Launch：

- 启动状态机 transition。
- 实例列表分组、隐藏、选中、类型文案。
- 文件夹选择和自定义目录。
- 内存预览。

Download：

- 安装状态 snapshot、顶部副标题、pending 下载判断和下载分类刷新分支。
- loader/addon 选择 snapshot、禁用原因、安装提示和安装 checklist。
- 版本选择。
- 版本列表 snapshot 分组、搜索过滤、版本类型文案、图标和 wiki 后缀。
- 下载 command handler。
- 下载任务 snapshot 过滤、状态、文案、图标和分组标题。

Profiles：

- profile snapshot。
- profile 类型文案。
- 刷新和选择 command。

Java：

- 刷新。
- 手动添加路径校验。
- 默认 Java 选择。
- 启用/禁用。

GameLink：

- EULA。
- Natayark 登录/退出。
- EasyTier 依赖检查/安装。
- NAT 测试。
- 创建/加入/离开大厅。
- runtime event 包装。

Settings：

- catalog。
- display rule。
- value get/set。
- change effect。
- observation disposal。

Personalization：

- 背景目录。
- 媒体扫描。
- 播放策略。
- 颜色 seed 策略。

### 11.3 UI 验收

UI 层不追求大量业务单元测试，主要做：

- 页面构建 smoke test。
- 手动或截图验收关键页面不崩溃。
- 主题、透明、背景、弹窗、文件选择等平台能力验收。

## 12. 扫描与验收命令

构建：

```bash
XDG_DATA_HOME=/tmp/pcl-ce-xdg NUGET_PACKAGES=/tmp/pcl-ce-nuget dotnet build 'Pixel Craft Launcher.slnx' --no-restore
```

Pixel Core 测试：

```bash
XDG_DATA_HOME=/tmp/pcl-ce-xdg NUGET_PACKAGES=/tmp/pcl-ce-nuget dotnet test 'PCL.Core.Test/PCL.Core.Test.csproj' --no-restore --filter "FullyQualifiedName~Pixel"
```

UI 业务耦合扫描：

```bash
rg "PCL\\.Core\\.(Minecraft|Link|App\\.Configuration)|PCL\\.Core\\.App\\.Pixel\\.Slices|Pixel[A-Za-z]+Service|Minecraft[A-Za-z]+Service|JavaEntry|MinecraftInstanceInfo|LobbyService|Natayark|EasyTier|ThemeService|ColorSchemeService|FeatureVisibilityService|States\\.|Config\\." 'Pixel Craft Launcher/Views' 'Pixel Craft Launcher/Services' 'Pixel Craft Launcher/Composition' -g '*.cs' -n
```

文件系统/平台规则扫描：

```bash
rg "File\\.Exists|File\\.Read|Directory\\.CreateDirectory|Path\\.Combine|IOPath|using System\\.IO|Basics\\.OpenPath|Process\\.Start|PixelSettingsBinder|DebugSettingsService" 'Pixel Craft Launcher' -g '*.cs' -n
```

迁移期 fallback 扫描：

```bash
rg "new Pixel[A-Za-z]+Service\\(|GetPixel[A-Za-z]+Service\\(\\).*=>|\\?\\?= new Pixel" 'Pixel Craft Launcher' -g '*.cs' -n
```

当前最新验收记录：

- `dotnet build 'Pixel Craft Launcher.slnx' --no-restore` 已通过；仅保留 `NU1510` 等既有警告。
- `dotnet test 'PCL.Core.Test/PCL.Core.Test.csproj' --no-restore --filter "FullyQualifiedName~PixelSettingsCatalogTest"` 已通过。
- `rg -n "\p{Han}" 'Pixel Craft Launcher/Views' -g '*.cs' -g '*.axaml'` 无输出，View 层业务/中文文案已迁入 Core snapshot/messages。
- `rg -n "GetPixel[A-Za-z]+Service\(|new Pixel[A-Za-z]+Service\(" 'Pixel Craft Launcher' -g '*.cs'` 仅剩 `PixelLoggingBootstrapService` 启动日志 bootstrap 创建点。

## 13. 完成定义

重构完成需要同时满足：

- `PCL.Core/App/Pixel` 中存在完整的 Composition、Infrastructure、Events、Navigation、Shell、Slices、ViewModels 分层。
- UI 项目无业务 ViewModel、业务 Router、业务 Settings binder。
- UI 页面不直接解释 Minecraft、Java、GameLink、Settings 领域状态。
- 用户动作进入 Core command bus 或 Core ViewModel。
- 关键流程有 Stateless 状态机。
- 跨 slice 通知通过 Rx.NET event bus。
- Core DI 和 Avalonia DI 边界清晰。
- Serilog command pipeline 和启动日志路径策略可测试。
- `dotnet build` 通过。
- Pixel Core 测试通过。
- UI 业务耦合扫描只剩明确允许的平台适配项。

## 14. 风险与应对

- 风险：一次迁移过多导致 UI 行为回归。
  - 应对：按 slice 小步迁移，每步补 Core 测试和构建验证。

- 风险：旧静态服务仍隐藏业务状态。
  - 应对：先由 Pixel slice 包装，再逐步把状态读写改为 DI service。

- 风险：ViewModel 过度膨胀。
  - 应对：复杂流程放入 slice service 或 page controller，ViewModel 只持有状态和命令入口。

- 风险：UI adapter 与 Core service 边界模糊。
  - 应对：凡依赖 Avalonia、窗口、剪贴板、文件选择器、媒体控件的代码留 UI；凡依赖配置、业务状态、规则判断的代码进 Core。

- 风险：迁移期 fallback 长期遗留。
  - 应对：每个 fallback 必须在扫描清单中出现，并在 Phase 7 删除。

## 15. 下一步推荐执行顺序

1. 继续收敛 Download 剩余边缘副作用：安装与保存 command 入参已 DTO 化，服务端启动脚本保存已进入 event 流，分类刷新规则已进入 slice service，右页刷新 intent 已进入 presentation；后续继续处理剩余状态事件和保存/刷新结果展示。
2. 继续冻结 Setup 设置控件边界；toggle、背景目录输入、combo 输入、slider 输入、text/JVM reset 输入、font 输入以及 action/info block 已分别抽出，设置变更 effect presentation 和颜色设置主题 adapter 已收敛，后续审计剩余平台桥接。
3. 审计 GameLink dialog/runtime platform adapter，继续压缩 runtime 通知、剪贴板和弹窗回调；禁用授权 reset 已完成 controller/result 化。
4. 继续明确 theme/color/personalization 边界；背景 seed、背景渲染 presentation 和配色弹窗主体已收敛，后续重点确认主题刷新和 Avalonia presenter 之间的责任划分。
5. 运行完整 UI 业务耦合扫描、构建和 Pixel Core 测试，冻结架构规则。

## 16. 优先迁移 Backlog

### P0：架构门禁

目标：

- 确保新增代码不会继续把业务逻辑写回 UI。

任务：

- 在文档中固化扫描命令和 allowed exceptions。
- 为 `PixelServiceCollectionExtensions` 增加 DI 完整性测试，覆盖新增 slice service、command handler、ViewModel。
- 为 `IPixelEventBus` 增加订阅释放和多订阅测试。
- 为 MediatR logging pipeline 增加异常路径测试。

完成条件：

- `PixelInfrastructureTest` 覆盖 command bus、event bus、state machine factory、Serilog bootstrap。
- 业务耦合扫描结果只剩当前剩余耦合清单中列出的迁移项。

### P1：Launch Summary 和 Page 拆分（已完成主体）

目标：

- 让 Launch 页不再直接绑定 `SelectedProfile`、`SelectedProfileMethod`、`SelectedInstancePath` 等细碎业务字段。

任务：

- 已完成：新增 `PixelLaunchSummarySnapshot`，包含 profile 名称、验证方式、实例路径、实例状态和按钮启用状态。
- 已完成：`PixelLaunchViewModel.Summary` 通过 `PixelLaunchSummaryService` 生成 summary，Launch 页面读取 ViewModel snapshot/helper。
- 已完成：新增 `PixelLaunchSidebarSnapshot`，统一二级动作和测试工具入口可见性。
- 已完成：Launch 左页主输入区、启动面板、实例列表、文件夹/内存等入口改为 snapshot/helper 消费 Core 输出。
- 已完成：`PixelLaunchSummaryServiceTest`、`PixelLaunchSidebarServiceTest` 和 `PixelLaunchViewModelTest` 覆盖关键 summary/sidebar 行为。

完成条件：

- `MainWindow.Pages.LaunchAndLeft.axaml.cs` 不再直接使用 `SelectedProfile`、`SelectedProfileMethod`、`SelectedInstancePath` 做显示判断。
- Launch summary/sidebar 在 profile/instance/launch 状态切换后通过 ViewModel 更新。

### P1：Profiles Login Controller

目标：

- 把 Microsoft/Authlib 登录结果状态从 View helper 进一步迁入 Core controller。

任务：

- 新增 `PixelProfileLoginController`。
- 定义 `PixelProfileLoginRequest`、`PixelProfileLoginProgressSnapshot`、`PixelProfileLoginResultSnapshot`。
- 已完成：设备码、登录进度、Authlib 服务器表单、角色选择和 Profile 操作 result 状态已统一为 Core snapshot。
- UI 保留 `IMinecraftProfileUiCallbacks` 的平台表现，或将其包成更窄的 `IPixelProfileLoginUiAdapter`。
- 移除 `MainWindow.Pages.Profiles.axaml.cs` 中返回 `Task<MinecraftProfile>` / `Task<AuthServerPreset>` 的 helper 暴露。

完成条件：

- Profiles 页不再把 `MinecraftProfile`、`AuthServerPreset` 作为 UI helper 的返回类型。
- 登录进度、设备码弹窗、Authlib 角色选择有 Core 测试。

### P1：Download Install Command-first

目标：

- 下载页所有会改变安装/下载状态的操作经 command bus。

任务：

- 已完成：普通安装与整合安装入口已统一为内部 `StartDownloadInstallCommand`，`PixelDownloadViewModel` 不再分别发送 `InstallMinecraftInstanceCommand` / `InstallMergedMinecraftInstanceCommand`。
- 已完成：`StartDownloadInstallCommand` 入参已收窄为 `PixelDownloadInstallVersion`、`PixelDownloadInstallLoader`、`PixelDownloadInstallMergedSelection` 等 Pixel DTO，handler 内再还原 Minecraft 领域 request。
- 已完成：保存客户端核心/服务端 Jar command 入参已收窄为 `PixelDownloadInstallVersion` DTO，handler 内再还原 Minecraft manifest entry。
- 已完成：服务端启动脚本保存副作用已通过 `MinecraftServerLaunchScriptSavedEvent` 进入 event bus。
- 已完成：下载分类刷新规则已抽出为 `PixelDownloadCategoryRefreshService` 并由 `PixelDownloadCategoryRefreshServiceTest` 覆盖。
- 已完成：下载右页刷新副作用已收敛为 `PixelDownloadRightPagePresentation` / `PixelDownloadRightPageRefreshAction`，由 Core 输出进入页面前的数据刷新动作。
- 后续继续把剩余保存/刷新结果展示和状态回写纳入明确 command/state/event。
- 已完成：刷新版本、刷新 loader、安装、整合安装、保存客户端核心和保存服务端 Jar 已收敛到 command 或 ViewModel command wrapper。
- 已完成：新增 `PixelDownloadStateMachine` 并注册到 Core DI，状态迁移已通过 Rx event bus 发布和测试覆盖。
- 已完成：`PixelDownloadViewModel` 的刷新版本、刷新 loader、安装、保存和取消路径已触发下载状态机；刷新/loader/安装/保存事件链已由 `PixelDownloadViewModelTest` 覆盖。
- 已完成：下载任务取消和取消全部下载已收敛为 command，并由 handler 发布取消事件。
- 继续把服务端脚本保存、分类刷新副作用纳入明确 command 和状态事件。
- 已完成：安装结果、错误、忙碌状态已通过 `PixelDownloadOperationSnapshot` 聚合供 UI 消费。
- 已完成：已拆出 `DownloadInstallPanelView`、`DownloadInstallSidebarView`、`DownloadPageView` 与左右页 factory；继续将命名和边缘事件流统一到 install state/event。

完成条件：

- 安装按钮和下载任务操作不直接调用下载领域服务。
- `PixelDownloadViewModelTest` 覆盖安装状态、pending operation、失败文案、事件刷新。

### P2：Tools/GameLink Platform Adapter 清理

目标：

- Tools 页只负责平台动作和渲染，不再承载异步业务编排。

任务：

- 审计 `MainWindow.Pages.Tools.axaml.cs` 中剩余 async 方法，按业务/平台分流。
- 将创建/加入/离开大厅输入状态抽成 `PixelGameLinkLobbyFormSnapshot`。
- 将 NAT 测试结果、EasyTier 安装结果、Natayark 登录结果用事件刷新 ViewModel。
- 已完成：创建/加入大厅失败后的 EULA 预检分支由 `PixelGameLinkToolsPageController` 处理，并由 controller 测试覆盖；View 只显示通用失败提示。
- 已完成：Natayark 登录开始提示意图由 `PixelGameLinkToolsPageController` 输出，并由 controller 测试覆盖；View 不再读取 `IsNatayarkLoggedIn`。
- 已完成：Natayark 登录/退出异常由 `PixelGameLinkToolsPageController` 返回失败 result，并由 controller 测试覆盖；View 不再调用 `GetNatayarkLoginExceptionMessage`。
- 已完成：EasyTier 依赖安装结果由 `PixelGameLinkToolsPageController` 返回 `PixelGameLinkDependencyInstallResult`，并由 controller 测试覆盖；View 只按成功/失败选择 UI hint 样式。
- 已完成：Tools 页 NAT 测试成功弹窗/失败提示意图由 `PixelGameLinkToolsPageController.RunToolsNatTestAsync` 输出，并由 controller 测试覆盖；设置页保留原始 NAT result 用于局部状态渲染。
- 已完成：Finish 面板复制提示、虚拟 IP 弹窗和手动端口弹窗/校验提示由 `PixelGameLinkFinishSnapshot` / `PixelGameLinkCreateCardSnapshot` 输出，并由 ViewModel 测试覆盖。
- 已完成：GameLink 设置页网络测试面板由 `PixelGameLinkToolsPageController.GetNetworkTestPanelSnapshot` / `RunSetupNatTestAsync` 提供初始状态和运行结果，并由 controller 测试覆盖。
- 已完成：Tools 隐藏入口文案由 `PixelShellVisibilityService.GetToolHiddenMessage` 输出，并由 Shell visibility 测试覆盖。
- 已完成：runtime 刷新、通知消费和 rebuild 合并由 `PixelGameLinkViewModel.ConsumeRuntimeRefreshPresentation` 输出，并由 ViewModel 测试覆盖。
- 把复制、打开链接、弹窗统一通过 UI adapter 调用。

完成条件：

- Tools 页不直接访问 Link、Natayark、EasyTier 领域服务。
- GameLink View 构造参数中无领域模型。

### P2：Setup Section 物理拆分

目标：

- 旧 `MainWindow.Pages.DownloadAndSetup.axaml.cs` 已拆分为 `MainWindow.Pages.Download.axaml.cs` 和 `MainWindow.Pages.Setup.axaml.cs`。

任务：

- 已完成：保留 `PixelSettingControlRenderer`，通用设置分区拆成 `SettingsSectionView`。
- 已完成：toggle 控件、tooltip、说明文本和用户触发写回已抽出为 `PixelSettingToggleInput`。
- 已完成：背景目录文本框、文件夹选择和打开目录按钮已抽出为 `PixelSettingBackgroundFolderInput`，`PixelSettingControlRenderer` 不再直接承载文件夹 picker 流程。
- 已完成：combo 控件、editable 文本和选项选择写回已抽出为 `PixelSettingComboInput`，`PixelSettingControlRenderer` 只负责按 setting control kind 分派。
- 已完成：slider 控件、数值文本同步和 slider 输入归一化写回已抽出为 `PixelSettingSliderInput`，`PixelSettingControlRenderer` 只负责按 setting control kind 分派。
- 已完成：text 控件、背景目录 text 委托和 JVM 参数重置按钮已抽出为 `PixelSettingTextInput`，`PixelSettingControlRenderer` 继续收缩为控件类型分派器。
- 已完成：font 控件、字体选择初始值、tooltip 和字体选择写回已抽出为 `PixelSettingFontInput`。
- 已完成：info/action block、action fallback hint 和背景目录 action 回调已抽出为 `PixelSettingStaticBlockFactory`。
- 已完成：GameLink setup 拆成 `GameLinkSetupPageView`。
- 已完成：下载任务详情右页拆成 `DownloadTaskDetailsView`。
- 已完成：下载任务统计左页拆成 `DownloadManagerStatsView`。
- 已完成：Minecraft 安装/客户端保存版本列表拆成 `DownloadVersionListView`。
- 已完成：Loader/addon 安装选择右页拆成 `DownloadInstallPanelView`。
- 已完成：安装左页的输入/清单/开始按钮区域拆成 `DownloadInstallSidebarView`。
- 已完成：下载主侧栏导航拆成 `DownloadSidebarView`。
- 已完成：下载占位页实例管理面板拆成 `DownloadInstanceManagementView`。
- 已完成：下载版本加载页拆成 `DownloadLoadingPageView`。
- 已完成：下载占位页 shell 拆成 `DownloadPendingPageView`。
- 已完成：下载占位页分类标题和说明收敛为 `PixelDownloadPendingPageSnapshot`。
- 已完成：下载主侧栏分类结构收敛为 `PixelDownloadSidebarSnapshot`，View 不再硬编码分类列表。
- 已完成：下载右页目标判断收敛为 `PixelDownloadRightPageKind`，窗口只按 Core 输出选择具体 View。
- 已完成：下载 install/task/secondary route 分类迁入 `MainWindowViewModel`，并由 Core 测试覆盖。
- 已完成：下载任务详情 task id 参数解析迁入 `MainWindowViewModel.GetDownloadTaskRouteTaskId`。
- 已完成：全局 secondary、Profile manager、Launch instance route 分类迁入 `MainWindowViewModel`，并由 Core 测试覆盖。
- 已完成：Profile manager action/server route 参数收敛为 `PixelProfileManagerRouteSnapshot` / `PixelProfileManagerPageKind`。
- 已完成：通用 placeholder 页面标题和说明收敛为 `PixelPlaceholderPageSnapshot`。
- 已完成：通用 placeholder 左侧 item 收敛为 `PixelPlaceholderLeftItemSnapshot`。
- 已完成：主页面切换 route 映射迁入 `MainWindowViewModel.GetMainPageRoute` / `NavigateMainPage`。
- 已完成：Launch instance 导航迁入 `MainWindowViewModel.NavigateLaunchInstances`，Views 中已无直接 `PixelRoutes.*` 调用。
- 已完成：顶栏二级标题规则迁入 `MainWindowViewModel.GetSecondaryTitleSnapshot`。
- 已完成：Natayark 登录开始/异常提示文案迁入 `PixelGameLinkViewModel` helper。
- 已完成：GameLink 大厅编号复制、NAT 测试结果和端口校验提示迁入 `PixelGameLinkViewModel`。
- 已完成：GameLink 设置页 inline 网络测试状态和按钮/完成/失败提示迁入 `PixelGameLinkViewModel` snapshot。
- 已完成：GameLink EULA/Footer 协议文案、链接和撤销授权确认文案迁入 `PixelGameLinkViewModel` snapshot。
- 已完成：GameLink Finish 面板大厅信息、成员标题和操作按钮文案迁入 `PixelGameLinkFinishSnapshot`。
- 已完成：Tools/GameLink 左侧栏入口标题、说明和图标迁入 `PixelGameLinkSidebarSnapshot`。
- 已完成：Tools/GameLink 隐藏入口、复制虚拟 IP 和手动端口弹窗文案迁入 `PixelGameLinkViewModel` snapshots。
- 已完成：Java 设置页提示文案迁入 `PixelJavaService` helper。
- 已完成：Java 设置页主体文案迁入 `PixelJavaPageMessages`，`JavaSetupPageView` 不再硬编码 Java 管理/列表/自动选择/空态/启用禁用相关文案。
- 已完成：`JavaSetupPageView` 改为构造注入 `PixelJavaPageMessages`，Views/factory 通过注入的 `PixelJavaService.GetMessages()` 获取消息，不再直接调用静态 `PixelJavaService.GetPageMessages()`。
- 已完成：Java 添加/刷新/信息/选择器/打开文件夹失败/操作失败提示改为消费 `PixelJavaPageMessages`，Download/Setup partial 和 Java setup leaf View 不再直接展示异常消息。
- 已完成：Java 默认选择和启用/禁用操作的成功/失败提示、刷新页面流程移入 `JavaSetupPlatformBridge`，Java setup leaf View 不再直接 catch Java 操作异常。
- 已完成：Setup 外链打开失败和分区重置完成提示迁入 `PixelSetupNavigationMessages`，Download/Setup 与 Launch/Left partial 不再调用 setup navigation 静态文案 helper。
- 已完成：Personalization 背景目录打开失败提示迁入 `PixelPersonalizationMessages`，窗口显式注入 Core personalization service 获取消息。
- 已完成：内存预览总内存/已用/游戏预估/空闲和内存不足 warning 文案迁入 `PixelMemoryPreviewTextSnapshot`。
- 已完成：Profile 页面操作结果和表单初始提示文案迁入 `PixelProfilePageMessages`，并由 `PixelProfileListServiceTest` 覆盖。
- 已完成：Profile 操作失败展示迁入 `PixelProfileOperationFailurePresentation`，`MainWindow.Pages.Profiles` 不再直接使用 `ex.Message` 作为状态行/hint。
- 已完成：Profile 列表页 hero、空态和 item tooltip 迁入 `PixelProfileListPageSnapshot`。
- 已完成：Profile 表单和弹窗标题、输入 hint、UUID 模式选项、登录/保存/取消按钮文案迁入 `PixelOfflineProfileEditorSnapshot`、`PixelAuthlibProfileFormSnapshot`、`PixelAuthServerEditorSnapshot` 和 `PixelProfilePageMessages`。
- 已完成：Profile manager 左侧栏动作文案和第三方验证服务器分组迁入 `PixelProfileManagerSidebarSnapshot`，`ProfileManagerLeftPageView` 只负责渲染 snapshot。
- 已完成：`MainWindow.Pages.ProfileForms.axaml.cs` / `MainWindow.Pages.ProfileDialogs.axaml.cs` 拆出 Profile 右侧表单和弹窗构建，`MainWindow.Pages.Profiles.axaml.cs` 收缩为 manager/list host 与登录 callback adapter。
- 已完成：Launch 实例选择/文件夹操作提示迁入 `PixelLaunchPageMessages`，下载分类刷新提示迁入 `PixelDownloadViewModel`。
- 已完成：Launch 强制关闭/正常退出/日志导出提示迁入 `PixelLaunchWindowMessages`，下载刷新开始/实例安装完成提示迁入 `PixelDownloadWindowMessages`。
- 已完成：Launch 自定义主页预设和 fallback 文案迁入 `PixelHomepageSnapshot`。
- 已完成：Download pending 页迁移状态文案、实例管理标题和实例管理空态/按钮文案迁入 `PixelDownloadPendingPageSnapshot` / `PixelInstanceManagementSnapshot`。
- 已完成：Setup/About 外链打开失败和 Personalization 背景目录打开失败提示迁入 Core helper。
- 已完成：GameLink 创建/加入卡片文案和 Download 安装/加载面板文案迁入 Core snapshot/messages。
- 已完成：Launch 实例选择页按钮、空态和实例操作 tooltip 文案迁入 `PixelLaunchPageMessages`。
- 已完成：About 页面业务文案、GameLink 设置页提示/网络测试标题、Profile 保存服务器按钮文案迁入 Core snapshot/messages。
- 已完成：主窗口硬件加速设置提示改由 `PixelSettingsChangeService` effect 驱动，Shell 载入/关闭提示迁入 `PixelShellSettingsService`。
- 已完成：Shell 主导航文字和浮动按钮 tooltip 迁入 `MainWindowViewModel`，AXAML 只通过绑定显示。
- 已完成：`PixelDownloadViewModel` / `PixelLaunchViewModel` 的 Pixel service 默认 fallback 已移除，依赖改为显式构造注入；`MainWindowViewModel` 默认构造也改由 DI 测试覆盖。
- 已完成：下载右页 route host 抽出为 `DownloadRightPageFactory`，`MainWindow.Pages.Download` 不再直接承载安装列表、加载页、占位页和安装选择页的 switch 组装。
- 已完成：下载左页 route host 抽出为 `DownloadLeftPageFactory`，普通侧栏、安装侧栏和下载任务统计左页的选择离开 `MainWindow.Pages.LeftNavigation`。
- 已完成：Setup 右页 route host 抽出为 `SetupRightPageFactory`，Java/GameLink/About/通用设置右页分派和控件拼装已离开 `MainWindow.Pages.Setup`。
- 已完成：Setup 左侧导航结构收敛为 `PixelSetupSidebarSnapshot`，并抽出 `SetupSidebarView`；`MainWindow.Pages.LaunchAndLeft` 不再硬编码 Setup 分组、section 列表、重置按钮 tooltip 和选中状态。
- 已完成：下载侧栏 refresh tooltip 已迁入 `PixelDownloadSidebarItemSnapshot`，旧的下载左页 wrapper/scroll helper 已移除或内联。
- 已完成：下载安装左栏、版本列表和任务详情页剩余业务文案迁入 `PixelDownloadInstallSidebarMessages`、`PixelDownloadVersionListMessages`、`PixelDownloadTaskDetailsMessages`。
- 已完成：下载任务统计项标题迁入 `PixelDownloadManagerStatsMessages`。
- 已完成：下载管理统计值迁入 `PixelDownloadManagerStatsSnapshot`，`DownloadManagerStatsView` 只渲染 Core 输出的四项文本。
- 已完成：下载安装面板页面状态迁入 `PixelDownloadInstallPanelSnapshot`，loader choice 分支判断不再散落在 `DownloadInstallPanelView`。
- 已完成：下载版本列表页面结构迁入 `PixelDownloadVersionListPageSnapshot`，空组过滤、卡片标题和空列表文案由 Core 输出。
- 已完成：下载任务详情页面状态迁入 `PixelDownloadTaskDetailsPageSnapshot`，任务组标题、可取消任务 id 和进度文本由 Core 输出。
- 已完成：下载安装左栏展示状态迁入 `PixelDownloadInstallSidebarSnapshot`，`DownloadInstallSidebarView` 不再直接读取清单或分散构造安装状态。
- 已完成：下载右页版本刷新前置判断迁入 `PixelDownloadRightPagePresentation`，`DownloadRightPageFactory` 只负责消费 Core 输出的 refresh action。
- 已完成：启动页主按钮抽出为 `LaunchMainButtonView`，按钮文字绑定、箭头图标和 hover/press 动画离开 `MainWindow.Pages.LaunchAndLeft`；窗口只保留跳转下载页和启动游戏两个回调。
- 已完成：启动中状态面板抽出为 `LaunchLaunchingPanelView`，进度条平滑动画和状态面板进入动画离开 `MainWindow.Pages.LaunchAndLeft`；“当前步骤 / 验证方式 / 启动进度”标签迁入 `PixelLaunchPageMessages`。
- 已完成：启动页账号面板抽出为 `LaunchAccountPanelView`，“档案管理”按钮文案迁入 `PixelLaunchPageMessages`，并删除 `MainWindow.Pages.LaunchAndLeft` 中已无调用的 profile 文案/图标 helper。
- 已完成：启动页输入容器抽出为 `LaunchInputPanelView`，账号面板、主按钮和二级实例操作按钮的布局/可见性离开 `MainWindow.Pages.LaunchAndLeft`；“实例设置”按钮文案迁入 `PixelLaunchPageMessages`。
- 已完成：启动实例文件夹左栏抽出为 `LaunchInstanceSidebarView`，文件夹列表、管理入口和文件夹 action 菜单离开 `MainWindow.Pages.LaunchAndLeft`；相关标题、按钮、tooltip 和菜单文案迁入 `PixelLaunchPageMessages`。
- 已完成：`MainWindow.Pages.LeftNavigation.axaml.cs` 抽出通用左侧导航、Tools/Download/Setup 侧栏 host，`MainWindow.Pages.LaunchAndLeft.axaml.cs` 只保留 Launch 主页面 host。
- 已完成：`MainWindow.Pages.LaunchInstances.axaml.cs` 抽出 Launch 实例文件夹左栏和实例选择右页组装，刷新/选择/打开/添加文件夹平台流程收敛到 `LaunchInstancesPlatformBridge`。
- 已完成：启动默认右页抽出为 `LaunchRightPageView`，启动日志卡片标题和添加实例文件夹的 picker 标题迁入 `PixelLaunchPageMessages`。
- 已完成：启动页自定义主页图片加载失败文案通过 `PixelHomepageSnapshot.GetImageLoadFailedMessage` 输出，View 不再拼接异常消息。
- 已完成：Launch 左侧进入动画与 sidebar item 动画迁入 `LaunchPageVisualEffects`，`MainWindow.Pages.LaunchAndLeft` 中已无 Launch glow 死代码和页面动画 helper。
- 已完成：`PixelGameLinkViewModel` 与 `PixelProfilePageService` 的 command bus fallback 已移除，GameLink EULA/授权重置和 Profile 页面写操作必须通过注入的 `IPixelCommandBus`。
- 已完成：背景渲染策略（适应方式、对齐、透明度、模糊、视频自动暂停）收敛为 `PixelPersonalizationBackgroundRenderSettings`，UI `PersonalizationPlatformBridge` 仅做 Avalonia 控件/播放器适配。
- 已完成：`PixelPersonalizationService` 与 UI `PersonalizationPlatformBridge` 纳入 DI，`MainWindow` 不再直接 new 个性化服务。
- 已完成：Avalonia 个性化适配器从 `PersonalizationService` 重命名为 `PersonalizationPlatformBridge`，UI 项目不再保留容易误判为业务 service 的同名类型。
- 已完成：`PixelPersonalizationServiceTest` 覆盖背景 seed fallback、渲染设置、音量 clamp、启动自动播放计划和游戏运行/停止时的播放策略。
- 已完成：背景切换 presentation 收敛为 `PixelPersonalizationBackgroundPresentation`，Core 统一输出内容类型、媒体路径、shell 背景图路径、渲染设置和 seed 更新结果；`PersonalizationPlatformBridge` 只根据 presentation 创建 Avalonia Image/Video/null 控件。
- 已完成：下载页最终 root 外壳整理为 `DownloadPageView`，左右 route host 与平台 bridge/callback wiring 由该外壳统一承担。
- 已完成：通用设置分区拆成 `SettingsSectionView`。
- 已完成：About 页拆成 `AboutPageView`。
- 已完成：`PixelSettingControlRenderer` 的缺省说明、可编辑组合框 hint、输入校验提示、JVM 参数重置提示、背景目录 picker 文案和 action fallback 文案迁入 `PixelSettingControlMessages`；`LaunchAdvanceJvm` / `UiBackgroundFolder` / 背景目录动作判断改由 `PixelSettingValueService` 提供。
- 已完成：`PixelColorSchemeSettingView` 的手动/图片取色、文件类型、成功/失败提示、自动背景文案、十六进制校验、手动取色弹窗标题和预设色列表迁入 `PixelColorSchemePageMessages` / `PixelColorSchemePreset`。
- 已完成：颜色设置页的主题刷新、深色模式读取和颜色模式变更订阅抽出为 `PixelColorSchemeThemeBridge`，`PixelColorSchemeSettingView` 不再直接调用 `ThemeService`。
- 已完成：`GameLinkAccountToolbarView`、`GameLinkEasyTierCardView`、`GameLinkFinishPanelView` 的 NAT 测试、忙碌态、EasyTier 卡片标题、重新检测按钮和大厅信息/操作卡片标题迁入 `PixelGameLink*Snapshot`。
- 已完成：`GameLinkCreateCardView` 的可创建状态和默认世界选择迁入 `PixelGameLinkCreateCardSnapshot`。
- 已完成：GameLink 创建/加入大厅预检失败的 EULA 子页切换迁入 `PixelGameLinkToolsPageController`，View 不再理解预检失败枚举。
- 已完成：`MainWindow` 通用消息/表单默认按钮和 macOS 辅助功能权限弹窗文案迁入 `PixelShellMessages` / `PixelAccessibilityPermissionDialogSnapshot`。
- 已完成：启动失败/崩溃弹窗的关闭、导出日志和打开日志按钮文案迁入 `PixelLaunchIssueDialogSnapshot`；设置变更消息 fallback 标题迁入 `PixelSettingNotificationMessages`。
- 已完成：`MainWindow.SettingsAndTheme.axaml.cs` 抽出主题、个性化、设置观察和 shell 外观刷新逻辑，`MainWindow.axaml.cs` 进一步收缩。
- 已完成：Shell 主题事件订阅、暗色模式读取和 Acrylic 材质暗色参数读取收敛到 `ShellThemePlatformBridge`，窗口、下载页和 `ShellThemePresenter` 不再直接访问 `ThemeService`。
- 已完成：`PixelPlaceholderPageView` 的页面承载区和迁移备注 sections 迁入 `PixelPlaceholderPageSnapshot`。
- 已完成：渲染器 warning 的 `States.Hint.Renderer` 访问封装为 `IPixelRendererHintState`，`PixelSettingsChangeService` 通过注入 adapter 读写，测试可使用内存实现。
- 已完成：设置变更 effect presentation 收敛为 `PixelSettingChangePresentation`，RunWait 可见性边界刷新、Setup 右页刷新、Shell 主题刷新和通知意图由 Core 合并输出。
- 已完成：`AboutPageView` 的版本标签格式迁入 `PixelAboutPageSnapshot.GetVersionLabel`。
- 已完成：`SetupRightPageFactory` 不再读取 `Basics.VersionName`、commit 和 licenses；About 版本 metadata 与许可证列表改由 `PixelAboutPageSnapshot` / `PixelAboutLicenseSnapshot` 携带，View 不再引用 metadata 模型。
- 已完成：`ControlsPreviewPageView` 和 `GameLinkToolsPageView` 的控件验收/测试入口文案迁入 `PixelControlsPreviewSnapshot` 和 `PixelGameLinkSidebarSnapshot`。
- 已完成：`DownloadInstallSidebarView` 不再直接读取 `ThemeService.IsDarkMode`，由 route host 注入当前主题模式用于 hover 色计算。
- 已完成：`MainWindow` 标题栏不再解释 `LauncherTitleType`；标题 logo/text/image 可见性和显示文本由 `PixelShellPersonalizationSettings` 输出。
- 已完成：`MainWindow.Routing.axaml.cs` 抽出全局 secondary route 和下载任务 route host 刷新逻辑，主窗口文件继续收缩。
- 已完成：`MainWindow.Overlays.axaml.cs` 抽出消息弹窗、hint、浮动操作按钮、启动问题弹窗和打开路径/链接 platform bridge，主窗口文件收缩到 shell/route/page-host 为主。
- 已完成：`PixelDialogPresenter` 抽出通用 message/form 弹窗 host、按钮事件和关闭逻辑，`MainWindow.Overlays` 只保留默认文案补齐与业务弹窗入口。
- 已完成：`PixelHintPresenter` 抽出 hint host 的去重、动画和颜色计算，`MainWindow.Overlays` 中 hint 逻辑缩减为 presenter 委托。
- 已完成：`PixelFloatingActionPresenter` 抽出浮动操作按钮的注册、排序、显隐动画和 ripple，`MainWindow.Overlays` 只保留下载任务/强制关闭点击 wiring。
- 已完成：`MainWindow.PageHost.axaml.cs` 抽出主/嵌套 page host 转场、Launch 页面刷新、标题和左右 pane 动画，主窗口文件进一步收缩到 route/lifecycle 入口。
- 已完成：`MainWindow.RouteHost.axaml.cs` 抽出 route 变更分派、嵌套路由刷新和主 route 应用。
- 已完成：主页面和 secondary route 的左右 host 内容构建集中为 `BuildRouteLeftPage` / `BuildRouteRightPage`，`MainWindow.RouteHost` 与 Shell 主题刷新共用该分派，避免刷新主题时丢失下载任务/Profile 管理等 secondary 页面。
- 已完成：`MainWindow.ShellLifecycle.axaml.cs` 抽出 shell 导航按钮 wiring、标题栏拖动、焦点处理和关闭生命周期，`MainWindow.axaml.cs` 进一步收缩为构造和依赖组合入口。
- 已完成：`MainWindow` 移除直接 `IPixelCommandBus` 注入，窗口不再作为 command dispatch 边界；command 发送由 Core ViewModel/service 负责。
- 已完成：Launch 主按钮状态迁入 `PixelLaunchMainButtonSnapshot`，下载/启动主动作和启用状态由 Core 输出。
- 已完成：Launch 启动中面板初始展示状态迁入 `PixelLaunchLaunchingPanelSnapshot`，标题、实例名、阶段、登录方式和进度文本由 Core 输出。
- 已完成：Launch 实例选择页结构迁入 `PixelLaunchInstanceSelectionPageSnapshot`，分组空态由 Core 输出。
- 已完成：Launch 左侧输入区与 Tools 测试入口共用 `PixelLaunchSidebarSnapshot`，二级动作和测试工具可见性由 Core 输出。
- 已完成：`MainWindow.Pages.Setup.axaml.cs` 抽出 Setup/Java/settings 平台桥接，旧 `MainWindow.Pages.DownloadAndSetup.axaml.cs` 已拆为下载与设置两个 partial。
- 已完成：Java 设置页平台桥接抽出为 `JavaSetupPlatformBridge`，文件选择、刷新提示、信息弹窗、打开目录、默认选择、启用/禁用和刷新页面回调离开 `MainWindow.Pages.Setup` / `JavaSetupPageView`。
- 已完成：设置变更 effect 的 UI 执行桥接抽出为 `SetupSettingsPlatformBridge`，通知展示、Setup 右页刷新和 Shell 主题刷新分派离开 `MainWindow.Pages.Setup`；RunWait 边界刷新决策已迁入 `PixelSettingChangePresentation`。
- 已完成：Setup 外链、背景目录和 About 快捷跳转平台桥接抽出为 `SetupNavigationPlatformBridge`，`MainWindow.Pages.Setup` 只保留跨页面使用的外链薄转发。
- 已完成：下载保存目录选择和实例管理面板适配抽出为 `DownloadPagePlatformBridge`，`MainWindow.Pages.Download` 收缩为下载右页 factory/bridge 组装。
- 已完成：下载任务详情 route 统一走 `DownloadRightPageFactory`，删除 `MainWindow.Pages.Download` 中的任务详情兼容构造入口。
- 已完成：secondary 左栏宽度命名为 `MainWindowViewModel.SecondaryLeftPaneWidth`，并清理 `MainWindow.Routing` 中未使用的下载 install route wrapper。
- 已完成：`MainWindow.Pages.GameLink.axaml.cs` 抽出 GameLink Tools host、运行时订阅、公告 timer、联机操作和剪贴板桥接，`MainWindow.Pages.Tools.axaml.cs` 收缩为工具入口 host。
- 已完成：剪贴板兼容反射抽出为 `ClipboardCompatBridge`，GameLink partial 不再直接持有反射实现；Tools partial 旧 using 同步清理。
- 已完成：GameLink Tools 操作桥接抽出为 `GameLinkToolsPlatformBridge`，Natayark、NAT 测试、EasyTier 和大厅创建/加入/离开操作离开 `MainWindow.Pages.GameLink`。
- 已完成：GameLink Tools 左侧导航动作抽出为 `GameLinkToolsNavigationBridge`，`MainWindow.Pages.LeftNavigation` 不再直接调用 GameLink ViewModel 子页切换方法。
- 已完成：GameLink Tools 操作提示结果抽出为 `PixelGameLinkNotificationSnapshot`，`GameLinkToolsPlatformBridge` 不再用业务成功/失败布尔值自行选择提示等级。
- 已完成：GameLink Finish/Footer/手动端口对话和剪贴板动作桥接抽出为 `GameLinkDialogPlatformBridge`，大厅编号/虚拟 IP 复制、玩家详情、禁用确认和手动端口弹窗离开 `MainWindow.Pages.GameLink`。
- 已完成：GameLink 公告加载与轮播 timer 抽出为 `GameLinkAnnouncementPlatformBridge`，`MainWindow.Pages.GameLink` 不再直接持有 `DispatcherTimer`。
- 已完成：GameLink runtime 订阅和 notification/rebuild UI 刷新抽出为 `GameLinkRuntimePlatformBridge`，`MainWindow.Pages.GameLink` 不再直接持有 runtime subscription。
- 已完成：Profile UUID 和设备码复制改为复用 `ClipboardCompatBridge`，Profiles partial 不再直接依赖 Avalonia Clipboard API。
- 已完成：Profile 保存/登录/添加服务器的表单和弹窗操作流抽出为 `ProfileOperationPlatformBridge`，按钮 busy 包装也统一为 `RunWithBusyStateAsync`，Profile form/dialog partial 只保留控件构造、字段读取和平台关闭回调。
- 已完成：Profile 列表选择、UUID 复制和删除后的绑定刷新、列表刷新与提示也迁入 `ProfileOperationPlatformBridge`。
- 已完成：Profile 保存/登录/添加服务器成功/失败状态收敛为 `PixelProfileOperationResultSnapshot`，UI bridge 不再 catch 异常并自行拼接结果状态。
- 已完成：Authlib 多角色选择新增 `PixelAuthlibProfileChoiceDialogSnapshot` 与 Avalonia 选择弹窗，UI 只返回选中索引，标题/说明/按钮文案和角色选项模型由 Core 输出。
- 已完成：Profile 列表空态迁入 `PixelProfileListPageSnapshot.IsEmpty`，`ProfileListPageView` 不再通过控件集合数量推导业务空态。
- 已完成：GameLink Finish 成员空态迁入 `PixelGameLinkFinishSnapshot.HasPlayers`，`GameLinkFinishPanelView` 不再解释玩家集合数量。
- 已完成：Java 列表空态迁入 `PixelJavaListSnapshot.IsEmpty`，`JavaSetupPageView` 不再直接用条目数量决定空列表展示。
- 已完成：Setup 侧栏分组可见性和下载任务组取消按钮状态分别迁入 `PixelSetupSidebarGroupSnapshot.HasItems` / `PixelDownloadTaskDetailsPageSnapshot.HasCancellableTasks`。
- 已完成：About 开源库空态迁入 `PixelAboutPageSnapshot.HasLicenses`，About View 不再解释许可证集合数量。
- 已完成：设置控件的 combo 初始选中、editable 文本、slider clamp/显示文本和通用值转字符串迁入 `PixelSettingValueService`，`PixelSettingControlRenderer` 保留 Avalonia 控件 wiring。
- 已完成：手动配色弹窗的 RGB 通道归一化、通道显示文本和通道合成迁入 `PixelColorSchemeSettingsService`，颜色 View 只负责滑块和输入框同步。
- 已完成：下载安装面板 loader choice 分组空态迁入 `PixelLoaderChoiceSnapshot.HasItems`，View 不再用已构造控件数量判断业务空态。
- 已完成：下载版本列表分组空态迁入 `PixelDownloadVersionListGroupSnapshot.HasVersions`，`DownloadVersionListView` 不再通过控件数量推导版本空态。
- 已完成：`PixelJavaEntrySnapshot` 移除 `JavaEntry` 领域对象引用，只保留 UI-ready 字段和 stable path；Java 设置 View/bridge 不再接触 Java 领域实体。
- 已完成：`PixelDownloadViewModel` 面向 UI 的安装/保存入口收窄为 version id，`MinecraftVersionManifestEntry` overload 和 `MinecraftLoaderVersionEntry` 选择入口改为内部私有实现，减少 ViewModel 公共表面对领域对象的暴露。
- 已完成：下载任务集合变化新增 `PixelDownloadViewModel.TaskListChanged` 窄事件，`MainWindow` 不再直接订阅领域任务集合的 `CollectionChanged`。
- 已完成：`PixelGameLinkViewModel` 的公告集合和当前公告领域对象改为私有，公开面只保留 `PixelGameLinkAnnouncementSnapshot`。
- 已完成：`PixelInstanceViewModel.SelectedInstance` 领域对象状态改为私有，外部通过 stable path 选择并通过 `PixelInstanceManagementSnapshot` 消费 UI-ready 状态。
- 已完成：`PixelInstanceViewModel.Folders` / `Instances` 领域集合改为 Core 内部可见，Launch 实例文件夹左栏通过 `GetLaunchFolderSnapshots` / `EnsureLaunchSelectedFolder` 消费快照和窄方法，Avalonia 不再传递 `MinecraftFolderInfo` 集合。
- 已完成：`PixelLaunchViewModel.InstanceModels` 领域集合改为 Core 内部可见，`SelectedInstance` 和 `SelectInstance(MinecraftInstanceInfo)` 收为私有；Launch 输入面板、实例选择页、选中实例文件夹和内存预览改为通过 ViewModel snapshot/helper 消费 Core 结果。
- 已完成：`PixelDownloadViewModel.InstanceInstalled` 事件参数由 `MinecraftInstanceInfo` 收窄为 `PixelMinecraftInstanceInstalledSnapshot`，安装完成通知不再向 Avalonia 暴露实例领域对象。
- 已完成：`PixelLaunchViewModel.ProfileService` 改为私有依赖，`LastExitInfo` 改为私有状态；Launch 成功/失败和游戏退出/崩溃事件参数收窄为 `PixelLaunchResultSnapshot` / `PixelGameExitedSnapshot`，窗口层不再消费 `MinecraftLaunchResult` / `MinecraftProcessExitInfo`。
- 已完成：`PixelDownloadViewModel` 的版本集合、过滤集合、任务集合、选中版本、选中任务、loader 选择状态和 loader choice groups 改为 Core 内部可见；公开操作入口保留 stable id 形式的 `SelectLoaderChoiceItem` / `ClearInstallChoice`，领域 enum/object 入口收为私有实现。
- 已完成：`PixelLaunchSidebarService`、`PixelLaunchInstanceListService`、`PixelLaunchFolderService`、`PixelMemoryPreviewService` 中带 `MinecraftInstanceInfo` / `MinecraftFolderInfo` 参数的 helper 收窄为 Core 内部可见，Avalonia 只能通过 ViewModel/snapshot/helper 消费结果。
- 已完成：`PixelJavaService` 中返回或接收 `JavaEntry` 的服务方法和 `CreateEntrySnapshot(JavaEntry, ...)` 收窄为 Core 内部可见，Java 设置页继续只通过 snapshot、stable path 和 page command wrapper 操作。
- 已完成：`PixelGameLinkService.LoadAnnouncementsAsync` 和 `PixelGameLinkViewModel.SetAnnouncements(IEnumerable<LinkAnnounceInfo>)` 收窄为 Core 内部可见，GameLink UI 继续通过 controller result 与 `PixelGameLinkAnnouncementSnapshot` 消费公告状态。
- 已完成：页面 host 切换动画、左右 pane 宽度锁定和右页入场项处理抽出为 `PageHostTransitionPresenter`，`MainWindow.PageHost` 收缩为路由刷新决策和 presenter 调用。
- 已完成：Shell 主题、Acrylic、全局字体、标题栏 logo 和背景遮罩应用抽出为 `ShellThemePresenter`，`MainWindow.SettingsAndTheme` 收缩为设置订阅、刷新决策和 presenter 调用。
- 已完成：手动配色弹窗构造抽出为 `PixelColorSchemeDialogPresenter`，`PixelColorSchemeSettingView` 收缩为配色入口、预设色和图片 seed 操作。
- 已完成：`LoadGameLinkAnnouncementsCommand` 与 `GameLinkAnnouncementsLoadedEvent` 的公告结果由 `LinkAnnounceInfo` 收窄为 `PixelGameLinkAnnouncementSnapshot`，Link 领域模型只保留在 Core 内部服务/映射方法中。
- 已完成：Java 刷新/添加命令和事件结果由 `JavaEntry` 收窄为 `PixelJavaEntrySnapshot`，并删除迁移期 `JavaEntry` 参数命令；Java command/event 公共面只保留 snapshot 与 stable path。
- 已完成：下载安装进度/完成事件载荷由 `MinecraftDownloadTaskInfo` / `MinecraftInstanceInfo` 收窄为 `PixelDownloadTaskSnapshot` / `PixelMinecraftInstanceInstalledSnapshot`，下载事件总线不再公开安装任务与实例领域对象。
- 已完成：Launch 完成/进程退出事件载荷由 `MinecraftLaunchResult` / `MinecraftProcessExitInfo` 收窄为 `PixelLaunchResultSnapshot` / `PixelGameExitedSnapshot`，启动事件总线不再公开启动结果与进程退出领域对象。
- 已完成：Profile 保存/选择/删除与 auth server 保存事件载荷由 `MinecraftProfile` / `AuthServerPreset` 收窄为 `PixelProfileItemSnapshot` / `PixelAuthServerSnapshot`，Profile 事件总线不再公开档案领域对象。
- 已完成：Profile 保存/登录/添加服务器命令返回值由领域对象收窄为 `PixelProfileItemSnapshot` / `PixelAuthServerSnapshot`，选择/删除命令统一使用 stable profile id，删除迁移期 `MinecraftProfile` 参数命令。
- 已完成：Download 版本刷新事件和安装命令返回值收窄为 `PixelDownloadVersionSnapshot` / `PixelMinecraftInstanceInstalledSnapshot`；Launch/Download 内部仍需领域结果的 MediatR handler 改为显式接口实现，避免 handler 类公开领域返回方法。
- 已完成：`PixelProfileListService` 中接收 `AuthServerPreset` / `MinecraftProfile` 的转换 helper 收窄为 Core 内部可见，公开领域对象扫描已无命中。
- 继续将 `MainWindow.Pages.Setup` 和设置控件 renderer 中剩余平台桥接拆成更窄的 adapter/factory；toggle 输入已抽出为 `PixelSettingToggleInput`，背景目录输入已抽出为 `PixelSettingBackgroundFolderInput`，combo 输入已抽出为 `PixelSettingComboInput`，slider 输入已抽出为 `PixelSettingSliderInput`，text/JVM reset 输入已抽出为 `PixelSettingTextInput`，font 输入已抽出为 `PixelSettingFontInput`，action/info block 已抽出为 `PixelSettingStaticBlockFactory`，Download 主页面已收缩为 `DownloadPageView` 委托入口。
- 每个 View 的构造参数只接受 ViewModel/snapshot/callback。

完成条件：

- Download/Setup 文件只负责 route/section 选择和容器组装。
- 每个 section 可单独构造并渲染空状态。

### P2：Personalization/Theme 边界冻结

目标：

- 明确 Core 策略和 Avalonia 主题应用的边界。

任务：

- 将背景目录、扫描、播放顺序、背景 seed 策略全部归 `PixelPersonalizationService`。
- `PersonalizationPlatformBridge` 只持有播放器、timer、Avalonia brush/image/control 适配。
- 已完成：颜色 seed 解析、格式化、预览色计算、预设色和配色设置页文案由 `PixelColorSchemeSettingsService` 输出；`ThemeService.RefreshColorScheme()` 由 `PixelColorSchemeThemeBridge` 作为 Avalonia 主题刷新 adapter 包装。
- 已完成：背景 seed fallback、媒体扫描、轮播、音量 clamp、启动自动播放计划和运行中播放策略已有 Core 测试覆盖。
- 已完成：背景 presentation 的空库、图片、视频路径和 shell 背景图路径已有 Core 测试覆盖。

完成条件：

- UI personalization service 不直接解析配置规则或图片颜色业务。
- Core 测试覆盖背景 seed fallback、媒体列表为空、音量 clamp、启动自动播放和游戏运行/停止播放策略。

### P3：最终清理

目标：

- 冻结目标架构，删除迁移期兼容层。

任务：

- 删除所有 `GetPixelXxxService()` fallback 和 `??= new PixelXxxService()`。
- 已完成：UI 项目中的 `GetPixelXxxService()` helper / fallback 扫描已清空；剩余 `new PixelLoggingBootstrapService()` 属于启动日志 bootstrap 边界。
- 删除 UI 项目中旧 Routing、Settings、ViewModels 业务类残留。
- 将 `MainWindow.Pages.*` 过渡文件拆空或删除。
- 全量运行构建、Pixel 测试、扫描命令。
- 更新贡献规则：新增 Pixel 业务必须先进入 Core slice。

完成条件：

- UI 业务耦合扫描无高风险项。
- `dotnet build` 和 `PCL.Core.Test` Pixel filter 通过。
- 文档中的完成定义全部勾选。
