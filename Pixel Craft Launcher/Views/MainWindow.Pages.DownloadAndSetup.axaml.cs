using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Routing;
using Pixel_Craft_Launcher.Settings;
using Pixel_Craft_Launcher.ViewModels;
using Pixel_Craft_Launcher.Views.Setup;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildDownloadRightPage()
    {
        if (_downloadViewModel.Versions.Count == 0 && !_downloadViewModel.IsBusy)
            _ = _downloadViewModel.RefreshVersionsAsync();

        if (IsDownloadTaskRoute())
            return BuildDownloadTaskDetailsPage();

        return _selectedDownloadPage switch
        {
            1 => IsDownloadInstallRoute() ? BuildMinecraftInstallSelectionPage() : BuildMinecraftInstallVersionPage(),
            9 => BuildMinecraftClientVersionPage(),
            _ => BuildDownloadPendingPage()
        };
    }

    private Control BuildMinecraftInstallVersionPage()
    {
        if (_downloadViewModel.Versions.Count == 0 &&
            (_downloadViewModel.IsBusy || _downloadViewModel.VersionLoadingState.LoadingState == MyLoadingState.Error))
            return BuildDownloadLoadingPage();

        var stack = CreatePageStack();
        if (_downloadViewModel.Versions.Count == 0)
        {
            stack.Children.Add(BuildVersionGroupCard("Minecraft 版本", [], installMode: true, isSwapped: false));
            return BuildScrollableMainPane(stack);
        }

        stack.Children.Add(BuildVersionGroupCard("最新版本", GetTopVersions(), installMode: true, isSwapped: false));
        foreach (var group in GetVersionGroups().Where(static group => group.versions.Count > 0))
            stack.Children.Add(BuildVersionGroupCard(group.title, group.versions, installMode: true));
        return BuildScrollableMainPane(stack);
    }

    private Control BuildMinecraftClientVersionPage()
    {
        if (_downloadViewModel.Versions.Count == 0 &&
            (_downloadViewModel.IsBusy || _downloadViewModel.VersionLoadingState.LoadingState == MyLoadingState.Error))
            return BuildDownloadLoadingPage();

        var stack = CreatePageStack();
        if (_downloadViewModel.Versions.Count == 0)
        {
            stack.Children.Add(BuildVersionGroupCard("Minecraft 版本", [], installMode: false, isSwapped: false));
            return BuildScrollableMainPane(stack);
        }

        stack.Children.Add(BuildVersionGroupCard("最新版本", GetTopVersions(), installMode: false, isSwapped: false));
        foreach (var group in GetVersionGroups().Where(static group => group.versions.Count > 0))
            stack.Children.Add(BuildVersionGroupCard(group.title, group.versions, installMode: false));
        return BuildScrollableMainPane(stack);
    }

    private Control BuildDownloadLoadingPage()
    {
        return new Grid
        {
            Children =
            {
                new MyLoading
                {
                    Text = "正在获取 Minecraft 版本列表",
                    State = _downloadViewModel.VersionLoadingState,
                    ShowProgress = false
                }
            }
        };
    }

    private Control BuildDownloadPendingPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(GetDownloadPageTitle(_selectedDownloadPage), GetDownloadPageDescription(_selectedDownloadPage)));
        stack.Children.Add(BuildCard("迁移状态", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("Plain 侧这个页面包含搜索、筛选、详情页和文件安装流程。"),
                CreateBodyText("本轮先保留同名入口、侧边栏刷新按钮和右页结构，占位等待后续迁移具体数据源。")
            }
        }));
        stack.Children.Add(BuildCard("实例管理", BuildInstanceManagementPanel()));
        return BuildScrollableMainPane(stack);
    }

    private Control BuildDownloadToolbar()
    {
        var toolbar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        var searchBox = new MyTextBox
        {
            HintText = "搜索版本",
            Text = _downloadViewModel.SearchText,
            MinWidth = 180
        };
        searchBox.TextChanged += (_, _) => _downloadViewModel.SearchText = searchBox.Text ?? string.Empty;
        toolbar.Children.Add(searchBox);

        var refresh = new MyButton { Text = "刷新", Height = 36 };
        refresh.Click += (_, _) => _ = _downloadViewModel.RefreshVersionsAsync();
        Grid.SetColumn(refresh, 1);
        toolbar.Children.Add(refresh);

        var client = new MyButton
        {
            Text = "下载客户端",
            Variant = MyButtonVariant.Flat,
            Height = 36,
            IsEnabled = _downloadViewModel.CanInstall
        };
        client.Click += (_, _) => _ = _downloadViewModel.InstallSelectedAsync(new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla));
        Grid.SetColumn(client, 2);
        toolbar.Children.Add(client);

        var server = new MyButton
        {
            Text = "保存服务端",
            Height = 36,
            IsEnabled = _downloadViewModel.CanInstall
        };
        server.Click += (_, _) => _ = _downloadViewModel.InstallSelectedAsync(new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla, SaveServerJar: true), saveServerJar: true);
        Grid.SetColumn(server, 3);
        toolbar.Children.Add(server);
        return toolbar;
    }

    private IReadOnlyList<MinecraftVersionManifestEntry> GetTopVersions()
    {
        var topVersions = new List<MinecraftVersionManifestEntry>();
        var release = _downloadViewModel.Versions.FirstOrDefault(static version => version.Type == "release");
        var snapshot = _downloadViewModel.Versions.FirstOrDefault(static version => version.Type is "snapshot" or "pending");
        if (release is not null) topVersions.Add(release);
        if (snapshot is not null && (release is null || snapshot.ReleaseTime > release.ReleaseTime))
            topVersions.Add(snapshot);
        return topVersions;
    }

    private MyCard BuildVersionGroupCard(string title, IReadOnlyList<MinecraftVersionManifestEntry> versions, bool installMode, bool isSwapped = true)
    {
        var content = isSwapped ? new StackPanel { Spacing = 2 } : BuildVersionList(versions, installMode);
        var card = BuildCard(title == "最新版本" ? title : $"{title} ({versions.Count})", content);
        card.CanSwap = true;
        card.IsSwapped = isSwapped;
        if (isSwapped)
            card.InstallMethod = stack => PopulateVersionList(stack, versions, installMode);
        return card;
    }

    private Control BuildVersionList(IEnumerable<MinecraftVersionManifestEntry> versions, bool installMode)
    {
        var list = new StackPanel { Spacing = 2 };
        PopulateVersionList(list, versions, installMode);
        return list;
    }

    private void PopulateVersionList(StackPanel list, IEnumerable<MinecraftVersionManifestEntry> versions, bool installMode)
    {
        if (list.Children.Count > 0)
            return;

        var query = _downloadViewModel.SearchText.Trim();
        foreach (var version in versions.Where(version =>
                     string.IsNullOrWhiteSpace(query) ||
                     version.Id.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            var item = new MyListItem
            {
                Title = FormatMinecraftVersionTitle(version),
                Info = GetVersionInfo(version, installMode),
                Icon = GetVersionIcon(version),
                Type = MyListItem.CheckType.Clickable
            };
            item.Click += (_, _) =>
            {
                if (installMode)
                {
                    _downloadViewModel.OpenInstallSelection(version);
                    _shellViewModel.NavigateMinecraftInstall(version.Id);
                }
                else
                {
                    _downloadViewModel.SelectedVersion = version;
                    _downloadViewModel.InstanceName = version.Id;
                    _ = _downloadViewModel.InstallSelectedAsync(new MinecraftLoaderSelection(MinecraftLoaderKind.Vanilla));
                }
            };
            AddMinecraftVersionActionButtons(item, version);
            list.Children.Add(item);
        }

        if (list.Children.Count == 0)
            list.Children.Add(CreateBodyText(_downloadViewModel.IsBusy ? "正在加载版本列表..." : "没有匹配的版本。"));
    }

    private void AddMinecraftVersionActionButtons(MyListItem item, MinecraftVersionManifestEntry version)
    {
        var save = CreateVersionActionButton("mdi-content-save-outline", "另存为");
        save.Click += async (_, _) =>
        {
            var folder = await PickDownloadSaveFolderAsync("选择保存位置");
            if (string.IsNullOrWhiteSpace(folder))
                return;

            await _downloadViewModel.SaveClientCoreAsync(version, folder);
        };
        item.AddButton(save);

        var info = CreateVersionActionButton("mdi-information-outline", "更新日志");
        info.Click += (_, _) =>
        {
            var wikiName = McFormatter.GetWikiUrlSuffix(version.Id);
            OpenExternalUrl("https://zh.minecraft.wiki/w/Special:Search?search=" + wikiName);
        };
        item.AddButton(info);

        var server = CreateVersionActionButton("mdi-server-outline", "下载服务端");
        server.Click += async (_, _) =>
        {
            var folder = await PickDownloadSaveFolderAsync("选择服务端保存位置");
            if (string.IsNullOrWhiteSpace(folder))
                return;

            await _downloadViewModel.SaveServerJarAsync(version, folder);
        };
        item.AddButton(server);
    }

    private async Task<string?> PickDownloadSaveFolderAsync(string title)
    {
        var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return selected.Count > 0 ? selected[0].TryGetLocalPath() : null;
    }

    private static MyIconButton CreateVersionActionButton(string icon, string tip)
    {
        var button = new MyIconButton
        {
            Icon = icon,
            IconSize = 13,
            Padding = new Thickness(4),
            Width = 24,
            Height = 24
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Control BuildMinecraftInstallSelectionPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildInstallSelectionSummaryCard());
        if (_downloadViewModel.IsLoaderChoicesLoading)
            stack.Children.Add(BuildLoadingLoaderCard());
        else if (!string.IsNullOrWhiteSpace(_downloadViewModel.LoaderChoicesError))
            stack.Children.Add(BuildLoaderErrorCard());
        else if (_downloadViewModel.LoaderChoiceGroups.Count == 0)
            stack.Children.Add(BuildCard("Mod Loader", CreateSubText("当前版本暂无可用 Mod Loader")));
        foreach (var hint in BuildInstallHints())
            stack.Children.Add(hint);
        foreach (var group in _downloadViewModel.LoaderChoiceGroups)
            stack.Children.Add(BuildLoaderSelectionCard(group));
        return BuildScrollableMainPane(stack);
    }

    private static MyCard BuildLoadingLoaderCard()
    {
        return new MyCard
        {
            Title = "Mod Loader - 正在加载可用版本",
            CanSwap = false,
            IsSwapped = true,
            IsLoading = true,
            CardContent = CreateSubText("正在加载可用版本")
        };
    }

    private MyCard BuildLoaderErrorCard()
    {
        var retry = new MyButton
        {
            Text = "重试",
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        retry.Click += (_, _) => _ = _downloadViewModel.RefreshLoaderChoicesAsync();
        return BuildCard("Mod Loader", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateSubText(_downloadViewModel.LoaderChoicesError ?? "加载失败"),
                retry
            }
        });
    }

    private MyCard BuildInstallSelectionSummaryCard()
    {
        var version = _downloadViewModel.SelectedVersion?.Id ?? "Minecraft";
        var loader = _downloadViewModel.MergedSelection.Loaders.Any()
            ? _downloadViewModel.SelectedLoaderLabel
            : "原版";
        return BuildCard("安装", new StackPanel
        {
            Spacing = 6,
            Children =
            {
                CreateBodyText(version),
                CreateSubText("Mod Loader: " + loader)
            }
        });
    }

    private Control BuildInstallSettingsPanel()
    {
        var stack = new StackPanel { Spacing = 8 };
        var nameBox = new TextBox
        {
            PlaceholderText = "实例名称",
            Text = _downloadViewModel.InstanceName,
            Height = 40,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0),
            UseFloatingPlaceholder = false
        };
        nameBox.TextChanged += (_, _) => _downloadViewModel.InstanceName = nameBox.Text ?? string.Empty;
        stack.Children.Add(nameBox);

        var targetBox = new TextBox
        {
            PlaceholderText = "Minecraft 文件夹",
            Text = _downloadViewModel.TargetFolder,
            Height = 40,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0),
            UseFloatingPlaceholder = false
        };
        targetBox.TextChanged += (_, _) => _downloadViewModel.TargetFolder = targetBox.Text ?? string.Empty;
        stack.Children.Add(targetBox);
        stack.Children.Add(CreateBodyText("当前选择：" + _downloadViewModel.SelectedLoaderLabel));
        return stack;
    }

    private Control BuildInstallStartPanel()
    {
        var start = new MyButton
        {
            Text = _downloadViewModel.IsBusy ? "处理中" : "开始下载",
            Variant = MyButtonVariant.Flat,
            Height = 34,
            IsEnabled = _downloadViewModel.CanInstall
        };
        start.Click += (_, _) => _ = _downloadViewModel.InstallSelectedAsync();
        return BuildCard("开始安装", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("确认实例名称、目标文件夹和加载器选择后开始下载。"),
                start
            }
        });
    }

    private IEnumerable<MyHint> BuildInstallHints()
    {
        var selection = _downloadViewModel.MergedSelection;
        if (selection.Fabric is not null && selection.FabricApi is null)
            yield return new MyHint { Text = "如果不安装 Fabric API，大多数 Mod 都会无法使用！", Theme = MyHint.Themes.Red, CanClose = false };
        if (selection.LegacyFabric is not null && selection.LegacyFabricApi is null)
            yield return new MyHint { Text = "如果不安装 Legacy Fabric API，大多数 Mod 都会无法使用！", Theme = MyHint.Themes.Red, CanClose = false };
        if (selection.Quilt is not null && selection.Qsl is null && selection.FabricApi is null)
            yield return new MyHint { Text = "如果不安装 QFAPI / QSL，大多数 Mod 都会无法使用！如果 QFAPI / QSL 无可用版本，你可以选择安装 Fabric API。", Theme = MyHint.Themes.Red, CanClose = false };
        if (selection.Fabric is not null && selection.OptiFine is not null && selection.OptiFabric is null)
            yield return new MyHint { Text = "必须安装 OptiFabric 才能正常使用 OptiFine！", Theme = MyHint.Themes.Red, CanClose = false };
        if (selection.OptiFine is not null && (selection.Forge is not null || selection.Fabric is not null))
            yield return new MyHint { Text = "OptiFine 与一部分 Mod 的兼容性不佳，请谨慎安装。", Theme = MyHint.Themes.Yellow, CanClose = false };
    }

    private MyCard BuildLoaderSelectionCard(PixelLoaderChoiceGroup group)
    {
        var active = GetSelectedText(group) is not null;
        var disabledReason = GetLoaderGroupDisabledReason(group);
        var canSelect = disabledReason is null;
        var status = GetSelectedText(group) ?? disabledReason ?? group.StatusText;
        var content = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(group.Description))
            content.Children.Add(CreateSubText(group.Description));
        var list = new StackPanel { Spacing = 4 };
        if (!canSelect)
        {
            content.Children.Add(CreateDisabledText(status));
        }
        else if (group.IsAddon)
        {
            foreach (var file in group.AddonFiles)
            {
                var item = new MyListItem
                {
                    Title = FormatAddonTitle(group.AddonKind!.Value, file),
                    Info = BuildAddonItemInfo(group.AddonKind!.Value, file),
                    Icon = group.Icon,
                    Type = MyListItem.CheckType.RadioBox,
                    Checked = IsSelectedAddon(group.AddonKind.Value, file),
                    IsEnabled = canSelect
                };
                item.Check += (_, _) =>
                {
                    if (!canSelect)
                        return;
                    _downloadViewModel.SelectAddon(file);
                };
                list.Children.Add(item);
            }
        }
        else
        {
            foreach (var version in group.LoaderVersions)
            {
                var item = new MyListItem
                {
                    Title = version.DisplayName,
                    Info = BuildLoaderItemInfo(version),
                    Icon = group.Icon,
                    Type = MyListItem.CheckType.RadioBox,
                    Checked = IsSelectedLoader(version),
                    IsEnabled = canSelect
                };
                item.Check += (_, _) =>
                {
                    if (!canSelect)
                        return;
                    _downloadViewModel.SelectLoaderVersion(version);
                };
                list.Children.Add(item);
            }
        }

        if (canSelect && list.Children.Count == 0)
            list.Children.Add(CreateDisabledText(group.StatusText));
        if (list.Children.Count > 0)
            content.Children.Add(list);

        content.Children.Add(CreateSubText(status));
        if (active)
        {
            var clear = new MyButton
            {
                Text = "清除选择",
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            clear.Click += (_, _) => _downloadViewModel.ClearChoice(group);
            content.Children.Add(clear);
        }
        var title = active
            ? group.Title + " - " + status.Replace("已选择：", "", StringComparison.Ordinal)
            : canSelect
                ? group.Title
                : group.Title + " - " + status;
        var card = BuildCard(title, content);
        card.CanSwap = canSelect;
        card.IsSwapped = true;
        card.Opacity = canSelect ? 1 : 0.72;
        return card;
    }

    private string? GetLoaderGroupDisabledReason(PixelLoaderChoiceGroup group)
    {
        if (!group.CanSelect)
            return group.StatusText;

        var selection = _downloadViewModel.MergedSelection;
        if (group.LoaderKind is { } loaderKind)
        {
            if (loaderKind == MinecraftLoaderKind.OptiFine)
            {
                var blocker = selection.NeoForge ?? selection.Cleanroom ?? selection.Quilt ?? selection.LabyMod;
                if (blocker is not null)
                    return "与 " + MinecraftLoaderCatalog.GetDisplayName(blocker.Kind) + " 不兼容";
                if (selection.Fabric is not null &&
                    !MinecraftLoaderCompatibility.IsOptiFineAllowedWith(MinecraftLoaderKind.Fabric, _downloadViewModel.SelectedVersion?.Id))
                    return "与 Fabric 不兼容";
                if (selection.Forge is not null &&
                    !group.LoaderVersions.Any(version => MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(version, selection.Forge)))
                    return "仅兼容特定版本的 Forge";
            }

            if (selection.OptiFine is not null)
            {
                if (!MinecraftLoaderCompatibility.IsOptiFineAllowedWith(loaderKind, _downloadViewModel.SelectedVersion?.Id))
                    return "与 OptiFine 不兼容";
                if (loaderKind == MinecraftLoaderKind.Forge &&
                    !group.LoaderVersions.Any(version => MinecraftLoaderCompatibility.IsOptiFineCompatibleWithForge(selection.OptiFine, version)))
                    return "与 OptiFine 不兼容";
            }
        }

        return group.AddonKind switch
        {
            MinecraftAddonKind.FabricApi when selection.Fabric is null && selection.Quilt is null => "需要先选择 Fabric 或 Quilt",
            MinecraftAddonKind.LegacyFabricApi when selection.LegacyFabric is null => "需要先选择 Legacy Fabric",
            MinecraftAddonKind.Qsl when selection.Quilt is null => "需要先选择 Quilt",
            MinecraftAddonKind.OptiFabric when selection.Fabric is null || selection.OptiFine is null => "需要先选择 Fabric 和 OptiFine",
            _ => null
        };
    }

    private static TextBlock CreateDisabledText(string text)
    {
        var block = CreateSubText(text);
        block.Foreground = ThemeBrushes.TextDisabled;
        return block;
    }

    private string BuildLoaderItemInfo(MinecraftLoaderVersionEntry version)
    {
        var parts = new List<string>
        {
            version.IsRecommended ? "推荐版" : version.IsStable ? "稳定版" : "测试版"
        };
        if (version.ReleaseTime is not null)
            parts.Add("发布于 " + version.ReleaseTime.Value.ToString("yyyy/MM/dd HH:mm"));
        var conflict = GetLoaderConflictText(version.Kind);
        if (!string.IsNullOrWhiteSpace(conflict) && !IsSelectedLoader(version))
            parts.Add(conflict);
        return string.Join(" · ", parts);
    }

    private string BuildAddonItemInfo(MinecraftAddonKind kind, MinecraftAddonFileEntry file)
    {
        var parts = new List<string>
        {
            file.IsStable ? "正式版" : "测试版"
        };
        if (file.ReleaseTime is not null)
            parts.Add("发布于 " + file.ReleaseTime.Value.ToString("yyyy/MM/dd HH:mm"));
        if (kind == MinecraftAddonKind.Qsl && _downloadViewModel.MergedSelection.FabricApi is not null && !IsSelectedAddon(kind, file))
            parts.Add("会替换 Fabric API");
        return string.Join(" · ", parts);
    }

    private string? GetLoaderConflictText(MinecraftLoaderKind kind)
    {
        var conflicts = MinecraftLoaderCompatibility.GetConflictingLoaderKinds(_downloadViewModel.MergedSelection, kind);
        if (conflicts.Count == 0)
            return null;

        return "会替换 " + string.Join(" / ", conflicts.Select(MinecraftLoaderCatalog.GetDisplayName));
    }

    private bool IsSelectedLoader(MinecraftLoaderVersionEntry version)
    {
        var selection = _downloadViewModel.MergedSelection;
        return version.Kind switch
        {
            MinecraftLoaderKind.OptiFine => Equals(selection.OptiFine, version),
            MinecraftLoaderKind.Forge => Equals(selection.Forge, version),
            MinecraftLoaderKind.NeoForge => Equals(selection.NeoForge, version),
            MinecraftLoaderKind.Cleanroom => Equals(selection.Cleanroom, version),
            MinecraftLoaderKind.Fabric => Equals(selection.Fabric, version),
            MinecraftLoaderKind.LegacyFabric => Equals(selection.LegacyFabric, version),
            MinecraftLoaderKind.Quilt => Equals(selection.Quilt, version),
            MinecraftLoaderKind.LiteLoader => Equals(selection.LiteLoader, version),
            MinecraftLoaderKind.LabyMod => Equals(selection.LabyMod, version),
            _ => false
        };
    }

    private bool IsSelectedAddon(MinecraftAddonKind kind, MinecraftAddonFileEntry file)
    {
        var selection = _downloadViewModel.MergedSelection;
        return kind switch
        {
            MinecraftAddonKind.FabricApi => Equals(selection.FabricApi, file),
            MinecraftAddonKind.LegacyFabricApi => Equals(selection.LegacyFabricApi, file),
            MinecraftAddonKind.Qsl => Equals(selection.Qsl, file),
            MinecraftAddonKind.OptiFabric => Equals(selection.OptiFabric, file),
            _ => false
        };
    }

    private string? GetSelectedText(PixelLoaderChoiceGroup group)
    {
        var selection = _downloadViewModel.MergedSelection;
        if (group.LoaderKind is { } kind)
        {
            var entry = kind switch
            {
                MinecraftLoaderKind.OptiFine => selection.OptiFine,
                MinecraftLoaderKind.Forge => selection.Forge,
                MinecraftLoaderKind.NeoForge => selection.NeoForge,
                MinecraftLoaderKind.Cleanroom => selection.Cleanroom,
                MinecraftLoaderKind.Fabric => selection.Fabric,
                MinecraftLoaderKind.LegacyFabric => selection.LegacyFabric,
                MinecraftLoaderKind.Quilt => selection.Quilt,
                MinecraftLoaderKind.LiteLoader => selection.LiteLoader,
                MinecraftLoaderKind.LabyMod => selection.LabyMod,
                _ => null
            };
            return entry is null ? null : "已选择：" + entry.DisplayName;
        }

        var addon = group.AddonKind switch
        {
            MinecraftAddonKind.FabricApi => selection.FabricApi,
            MinecraftAddonKind.LegacyFabricApi => selection.LegacyFabricApi,
            MinecraftAddonKind.Qsl => selection.Qsl,
            MinecraftAddonKind.OptiFabric => selection.OptiFabric,
            _ => null
        };
        return addon is null || group.AddonKind is null ? null : "已选择：" + FormatAddonTitle(group.AddonKind.Value, addon);
    }

    private IEnumerable<(string title, string info, string icon, Action clear)> GetInstallChecklistItems()
    {
        var selection = _downloadViewModel.MergedSelection;
        if (selection.Forge is not null) yield return (selection.Forge.DisplayName, "Forge", "mdi-anvil", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.Forge));
        if (selection.Cleanroom is not null) yield return (selection.Cleanroom.DisplayName, "Cleanroom", "mdi-flask-outline", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.Cleanroom));
        if (selection.NeoForge is not null) yield return (selection.NeoForge.DisplayName, "NeoForge", "mdi-anvil", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.NeoForge));
        if (selection.Fabric is not null) yield return (selection.Fabric.DisplayName, "Fabric", "mdi-feather", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.Fabric));
        if (selection.LegacyFabric is not null) yield return (selection.LegacyFabric.DisplayName, "Legacy Fabric", "mdi-feather", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.LegacyFabric));
        if (selection.Quilt is not null) yield return (selection.Quilt.DisplayName, "Quilt", "mdi-grid-large", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.Quilt));
        if (selection.LabyMod is not null) yield return (selection.LabyMod.DisplayName, "LabyMod", "mdi-test-tube", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.LabyMod));
        if (selection.OptiFine is not null) yield return (selection.OptiFine.DisplayName, "OptiFine", "mdi-eye-outline", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.OptiFine));
        if (selection.LiteLoader is not null) yield return (selection.LiteLoader.DisplayName, "LiteLoader", "mdi-package-variant", () => _downloadViewModel.ClearLoader(MinecraftLoaderKind.LiteLoader));

        if (selection.FabricApi is not null) yield return (FormatAddonTitle(MinecraftAddonKind.FabricApi, selection.FabricApi), "Fabric API", "mdi-feather", () => _downloadViewModel.ClearAddon(MinecraftAddonKind.FabricApi));
        if (selection.LegacyFabricApi is not null) yield return (FormatAddonTitle(MinecraftAddonKind.LegacyFabricApi, selection.LegacyFabricApi), "Legacy Fabric API", "mdi-feather", () => _downloadViewModel.ClearAddon(MinecraftAddonKind.LegacyFabricApi));
        if (selection.Qsl is not null) yield return (FormatAddonTitle(MinecraftAddonKind.Qsl, selection.Qsl), "QFAPI / QSL", "mdi-grid-large", () => _downloadViewModel.ClearAddon(MinecraftAddonKind.Qsl));
        if (selection.OptiFabric is not null) yield return (FormatAddonTitle(MinecraftAddonKind.OptiFabric, selection.OptiFabric), "OptiFabric", "mdi-package-variant", () => _downloadViewModel.ClearAddon(MinecraftAddonKind.OptiFabric));
    }

    private static string FormatAddonTitle(MinecraftAddonKind kind, MinecraftAddonFileEntry file) =>
        kind switch
        {
            MinecraftAddonKind.FabricApi => file.DisplayName.Replace("Fabric API ", "", StringComparison.OrdinalIgnoreCase),
            MinecraftAddonKind.LegacyFabricApi => file.DisplayName.Replace("Legacy Fabric API ", "", StringComparison.OrdinalIgnoreCase),
            MinecraftAddonKind.Qsl => file.DisplayName.Replace(" build ", ".", StringComparison.OrdinalIgnoreCase).Split('+')[0],
            MinecraftAddonKind.OptiFabric => file.DisplayName.ToLowerInvariant().Replace("optifabric-", "", StringComparison.Ordinal).Replace(".jar", "", StringComparison.Ordinal).Trim().TrimStart('v'),
            _ => file.DisplayName
        };

    private static string FormatMinecraftVersionTitle(MinecraftVersionManifestEntry version) =>
        version.Id.Replace('_', ' ');

    private IReadOnlyList<(string title, IReadOnlyList<MinecraftVersionManifestEntry> versions)> GetVersionGroups()
    {
        var source = _downloadViewModel.FilteredVersions.Count == 0 && _downloadViewModel.Versions.Count > 0
            ? _downloadViewModel.Versions
            : _downloadViewModel.FilteredVersions;
        return
        [
            ("正式版", source.Where(static version => version.Type == "release").ToArray()),
            ("预览版", source.Where(static version => version.Type is "snapshot" or "pending").ToArray()),
            ("愚人节版", source.Where(IsAprilFoolsVersion).ToArray()),
            ("远古版", source.Where(static version => version.Type is not ("release" or "snapshot" or "pending") && !IsAprilFoolsVersion(version)).ToArray())
        ];
    }

    private static bool IsAprilFoolsVersion(MinecraftVersionManifestEntry version)
    {
        var id = version.Id.ToLowerInvariant();
        return id is "2point0_blue" or "2point0_red" or "2point0_purple" or "2.0_blue" or "2.0_red" or "2.0_purple"
                   or "20w14infinite" or "20w14∞" or "3d shareware v1.34" or "1.rv-pre1" or "15w14a"
                   or "22w13oneblockatatime" or "23w13a_or_b" or "24w14potato" or "25w14craftmine" or "26w14a" ||
               version.ReleaseTime.Month == 4 && version.ReleaseTime.Day == 1;
    }

    private static string GetVersionInfo(MinecraftVersionManifestEntry version, bool installMode)
    {
        var type = version.Type switch
        {
            "release" => "正式版",
            "snapshot" or "pending" => "预览版",
            _ when IsAprilFoolsVersion(version) => "愚人节版",
            _ => "远古版"
        };
        return $"{type} · 发布于 {version.ReleaseTime:yyyy/MM/dd HH:mm}";
    }

    private static string GetVersionIcon(MinecraftVersionManifestEntry version)
    {
        if (IsAprilFoolsVersion(version)) return "mdi-party-popper";
        return version.Type switch
        {
            "release" => "mdi-cube-outline",
            "snapshot" or "pending" => "mdi-flask-outline",
            _ => "mdi-archive-outline"
        };
    }

    private static string GetDownloadPageTitle(int page) =>
        page switch
        {
            2 => "Mod",
            3 => "整合包",
            4 => "数据包",
            5 => "资源包",
            6 => "光影包",
            7 => "世界",
            8 => "收藏夹",
            9 => "Minecraft",
            10 => "OptiFine",
            11 => "Forge",
            12 => "NeoForge",
            13 => "Cleanroom",
            14 => "Fabric",
            15 => "Quilt",
            16 => "LiteLoader",
            17 => "LabyMod",
            18 => "Legacy Fabric",
            _ => "下载"
        };

    private static string GetDownloadPageDescription(int page) =>
        page switch
        {
            >= 2 and <= 8 => "Plain 中这里是社区资源搜索与详情页；本轮先保留同名入口、刷新按钮和页面占位。",
            >= 10 and <= 18 => "Plain 中这里是独立安装包版本列表；本轮先保留同名入口、刷新按钮和页面占位。",
            _ => "Plain 中这里是下载页。"
        };

    private Control BuildDownloadTaskList()
    {
        var stack = new StackPanel { Spacing = 4 };
        foreach (var task in _downloadViewModel.Tasks.Take(10))
        {
            var item = new MyListItem
            {
                Title = task.Name,
                Info = $"{FormatDownloadTaskState(task.State)} · {task.Progress:P0}" + (string.IsNullOrWhiteSpace(task.Message) ? "" : " · " + task.Message),
                Icon = GetDownloadTaskIcon(task),
                Type = MyListItem.CheckType.Clickable
            };
            item.Click += (_, _) =>
            {
                _downloadViewModel.SelectTask(task.Id);
                _shellViewModel.NavigateDownloadTaskDetails(task.Id);
            };
            stack.Children.Add(item);
        }

        if (stack.Children.Count == 0)
            stack.Children.Add(CreateBodyText("暂无运行中的下载任务。"));
        return stack;
    }

    private Control BuildDownloadTaskDetailsPage()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(25, 25, 25, 10),
            Spacing = 0
        };

        var visibleTasks = _downloadViewModel.Tasks
            .Where(IsVisibleDownloadTask)
            .ToArray();
        if (visibleTasks.Length > 0)
            stack.Children.Add(BuildDownloadManagerTaskGroupCard(visibleTasks));
        else if (HasPendingDownloadOperation())
            stack.Children.Add(BuildDownloadManagerFinishingCard());

        if (stack.Children.Count == 0)
            stack.Children.Add(BuildDownloadManagerEmptyCard());
        return BuildScrollableMainPane(stack);
    }

    private MyCard BuildDownloadManagerTaskGroupCard(IReadOnlyList<MinecraftDownloadTaskInfo> tasks)
    {
        var content = new Grid
        {
            Margin = new Thickness(14, 0, 15, 10)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < tasks.Count; i++)
            AddDownloadManagerTaskRow(content, i, tasks[i], BuildDownloadManagerTaskText(tasks[i]));

        var card = new MyCard
        {
            Title = GetDownloadManagerGroupTitle(tasks),
            Margin = new Thickness(0, 0, 0, 15),
            CardContent = content
        };

        if (!tasks.Any(static task => task.State is PCL.Core.IO.Download.NDlTaskState.Waiting or PCL.Core.IO.Download.NDlTaskState.Running))
            return card;

        var cancel = new MyIconButton
        {
            Icon = "mdi-close",
            IconSize = 16,
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 10, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(2),
            Foreground = ThemeBrushes.Text
        };
        ToolTip.SetTip(cancel, "取消");
        cancel.Click += (_, _) =>
        {
            var count = 0;
            foreach (var task in tasks.Where(static task => task.State is PCL.Core.IO.Download.NDlTaskState.Waiting or PCL.Core.IO.Download.NDlTaskState.Running))
            {
                if (_downloadViewModel.CancelTask(task.Id))
                    count++;
            }
            if (count > 0)
                ShowHint("已请求取消下载任务组。", HintType.Info);
        };
        if (card.Content is Panel root)
            root.Children.Add(cancel);
        return card;
    }

    private string GetDownloadManagerGroupTitle(IReadOnlyList<MinecraftDownloadTaskInfo> tasks)
    {
        var version = _downloadViewModel.SelectedVersion?.Id;
        if (!string.IsNullOrWhiteSpace(version))
            return "Minecraft " + version + " 下载";
        return tasks.Count == 1 ? tasks[0].Name : "下载任务";
    }

    private static bool IsVisibleDownloadTask(MinecraftDownloadTaskInfo task) =>
        task.State is not (PCL.Core.IO.Download.NDlTaskState.Finished or PCL.Core.IO.Download.NDlTaskState.Cancelled);

    private static MyCard BuildDownloadManagerFinishingCard()
    {
        var content = new Grid
        {
            Margin = new Thickness(14, 0, 15, 10)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        AddDownloadManagerTextRow(content, 0, CreateWaitingGlyph(), "正在完成安装");

        return new MyCard
        {
            Title = "下载任务",
            Margin = new Thickness(0, 0, 0, 15),
            CardContent = content
        };
    }

    private static MyCard BuildDownloadManagerEmptyCard()
    {
        var content = new Grid
        {
            Margin = new Thickness(14, 0, 15, 10)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        AddDownloadManagerTextRow(content, 0, CreateWaitingGlyph(), "暂无下载任务");

        return new MyCard
        {
            Title = "下载任务",
            Margin = new Thickness(0, 0, 0, 15),
            CardContent = content
        };
    }

    private static void AddDownloadManagerTaskRow(Grid grid, int row, MinecraftDownloadTaskInfo task, string text)
    {
        Control marker = task.State switch
        {
            PCL.Core.IO.Download.NDlTaskState.Finished => CreateFinishedGlyph(),
            PCL.Core.IO.Download.NDlTaskState.Failed or PCL.Core.IO.Download.NDlTaskState.Cancelled => CreateFailedGlyph(),
            PCL.Core.IO.Download.NDlTaskState.Running => CreateProgressGlyph(task.Progress),
            _ => CreateWaitingGlyph()
        };
        AddDownloadManagerTextRow(grid, row, marker, text);
    }

    private static void AddDownloadManagerTextRow(Grid grid, int row, Control marker, string text)
    {
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(26)));
        Grid.SetRow(marker, row);
        Grid.SetColumn(marker, 0);
        grid.Children.Add(marker);

        var label = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BodyForeground
        };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
    }

    private static string BuildDownloadManagerTaskText(MinecraftDownloadTaskInfo task)
    {
        return task.State switch
        {
            PCL.Core.IO.Download.NDlTaskState.Waiting => task.Name,
            PCL.Core.IO.Download.NDlTaskState.Running => task.Name,
            PCL.Core.IO.Download.NDlTaskState.Failed => string.IsNullOrWhiteSpace(task.Message) ? "下载失败" : task.Message,
            _ => task.Name
        };
    }

    private static TextBlock CreateProgressGlyph(double progress)
    {
        return new TextBlock
        {
            Text = Math.Floor(Math.Clamp(progress, 0d, 1d) * 100) + "%",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = ThemeBrushes.PrimaryHover
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateWaitingGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 18,
            Height = 6,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 7, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z")
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateFinishedGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 15,
            Height = 16,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z")
        };
    }

    private static Avalonia.Controls.Shapes.Path CreateFailedGlyph()
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
            Fill = ThemeBrushes.PrimaryHover,
            Data = Geometry.Parse("M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z")
        };
    }

    private static string FormatDownloadTaskState(PCL.Core.IO.Download.NDlTaskState state) =>
        state switch
        {
            PCL.Core.IO.Download.NDlTaskState.Waiting => "等待中",
            PCL.Core.IO.Download.NDlTaskState.Running => "下载中",
            PCL.Core.IO.Download.NDlTaskState.Finished => "已完成",
            PCL.Core.IO.Download.NDlTaskState.Failed => "失败",
            PCL.Core.IO.Download.NDlTaskState.Cancelled => "已取消",
            _ => state.ToString()
        };

    private static string GetDownloadTaskIcon(MinecraftDownloadTaskInfo task) =>
        task.State switch
        {
            PCL.Core.IO.Download.NDlTaskState.Finished => "mdi-check-circle-outline",
            PCL.Core.IO.Download.NDlTaskState.Failed => "mdi-alert-circle-outline",
            PCL.Core.IO.Download.NDlTaskState.Cancelled => "mdi-cancel",
            PCL.Core.IO.Download.NDlTaskState.Running => "mdi-download-outline",
            _ => "mdi-clock-outline"
        };

    private static string FormatDownloadSize(long downloaded, long? total) =>
        total is > 0 ? $"{FormatBytes(downloaded)} / {FormatBytes(total.Value)}" : FormatBytes(downloaded);

    private static string FormatPcl2Progress(double progress)
    {
        var value = Math.Clamp(progress, 0d, 1d);
        if (value > 0.999999)
            return "100 %";
        var percent = value * 100;
        var integer = Math.Floor(percent);
        var decimals = Math.Floor((percent - integer) * 100);
        return $"{integer:0}.{decimals:00} %";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(bytes, 0);
        var size = (double)value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }

    private Control BuildInstanceManagementPanel()
    {
        _instanceViewModel.Refresh();
        var stack = new StackPanel { Spacing = 8 };
        foreach (var instance in _instanceViewModel.Instances.Take(12))
        {
            var item = new MyListItem
            {
                Title = instance.Name,
                Info = instance.VersionDirectory,
                Icon = instance.IsHidden ? "mdi-eye-off-outline" : "mdi-cube-outline",
                Type = MyListItem.CheckType.RadioBox,
                Checked = Equals(instance, _instanceViewModel.SelectedInstance)
            };
            item.Check += (_, _) => _instanceViewModel.SelectedInstance = instance;
            var open = new MyIconButton { Icon = "mdi-folder-open-outline", Width = 24, Height = 24 };
            ToolTip.SetTip(open, "打开实例文件夹");
            open.Click += (_, _) =>
            {
                _instanceViewModel.SelectedInstance = instance;
                _instanceViewModel.OpenSelectedFolder();
            };
            item.AddButton(open);
            stack.Children.Add(item);
        }

        if (_instanceViewModel.Instances.Count == 0)
            stack.Children.Add(CreateBodyText("未找到实例，安装完成后会自动刷新。"));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refresh = new MyButton { Text = "刷新实例", Height = 34 };
        refresh.Click += (_, _) =>
        {
            _launchViewModel.RefreshInstances();
            _instanceViewModel.Refresh();
            ScheduleDownloadRightPageRefresh();
        };
        actions.Children.Add(refresh);
        var openMods = new MyButton { Text = "打开 Mods", Height = 34, IsEnabled = _instanceViewModel.SelectedInstance is not null };
        openMods.Click += (_, _) => _instanceViewModel.OpenSelectedFolder("mods");
        actions.Children.Add(openMods);
        var openSaves = new MyButton { Text = "打开存档", Height = 34, IsEnabled = _instanceViewModel.SelectedInstance is not null };
        openSaves.Click += (_, _) => _instanceViewModel.OpenSelectedFolder("saves");
        actions.Children.Add(openSaves);
        stack.Children.Add(actions);
        return stack;
    }

    private Control BuildSetupRightPage()
    {
        if (_selectedSetupSection == PixelSettingSectionKind.Java)
            return BuildJavaSetupRightPage();
        if (_selectedSetupSection == PixelSettingSectionKind.GameLink)
            return BuildGameLinkSetupRightPage();
        if (_selectedSetupSection == PixelSettingSectionKind.About)
            return BuildAboutRightPage();

        var section = PixelSettingsCatalog.Get(_selectedSetupSection);
        var stack = CreatePageStack();

        foreach (var group in section.Groups)
        {
            var content = new StackPanel { Spacing = 12 };
            foreach (var setting in group.Settings)
            {
                if (!ShouldDisplaySetting(setting))
                    continue;
                content.Children.Add(BuildSettingControl(setting));
            }
            stack.Children.Add(BuildCard(group.Title, content));
        }

        return BuildScrollableMainPane(stack);
    }

    private Control BuildGameLinkSetupRightPage()
    {
        var section = PixelSettingsCatalog.Get(PixelSettingSectionKind.GameLink);
        var stack = CreatePageStack();
        stack.Children.Add(new MyHint
        {
            Text = "修改此处的设置后，需要重新启动大厅以使设置生效。",
            Theme = MyHint.Themes.Yellow,
            CanClose = false
        });

        foreach (var group in section.Groups)
        {
            var content = new StackPanel { Spacing = 12 };
            foreach (var setting in group.Settings)
                content.Children.Add(BuildSettingControl(setting));
            stack.Children.Add(BuildCard(group.Title, content));
        }

        stack.Children.Add(BuildCard("网络测试", BuildGameLinkNetworkTestPanel()));
        return BuildScrollableMainPane(stack);
    }

    private Control BuildGameLinkNetworkTestPanel()
    {
        var udpText = CreateBodyText("UDP NAT 类型: 尚未检测");
        var tcpText = CreateBodyText("TCP NAT 类型: 尚未检测");
        var ipv6Text = CreateBodyText("IPv6: 尚未检测");
        var platformText = CreateSubText(EasyTierMetadata.IsPlatformSupported
            ? $"EasyTier 平台包: {EasyTierMetadata.Platform.PlatformId}"
            : EasyTierMetadata.GetUnsupportedReason());

        var button = CreateActionButton("开始测试", "mdi-earth");
        button.IsEnabled = EasyTierMetadata.IsPlatformSupported;
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            button.Text = "正在测试";
            try
            {
                if (!await EasyTierDependencyService.EnsureInstalledAsync())
                {
                    ShowHint(EasyTierDependencyService.LastError ?? "EasyTier 依赖不可用。", HintType.Critical);
                    return;
                }

                var status = await CliNetTest.GetNetStatusAsync();
                if (status is null)
                {
                    ShowHint("网络测试失败，请检查 EasyTier 依赖和网络连接。", HintType.Critical);
                    return;
                }

                udpText.Text = "UDP NAT 类型: " + CliNetTest.GetNatTypeString(status.UdpNatType);
                tcpText.Text = "TCP NAT 类型: " + CliNetTest.GetNatTypeString(status.TcpNatType);
                ipv6Text.Text = "IPv6: " + (status.SupportIPv6 ? "支持" : "不支持");
                ShowHint("网络测试完成。", HintType.Finish);
            }
            catch (Exception ex)
            {
                ShowHint("网络测试失败：" + ex.Message, HintType.Critical);
            }
            finally
            {
                button.Text = "开始测试";
                button.IsEnabled = EasyTierMetadata.IsPlatformSupported;
            }
        };

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                platformText,
                udpText,
                tcpText,
                ipv6Text,
                new WrapPanel { Children = { button } }
            }
        };
    }

    private Control BuildAboutRightPage()
    {
        var logoRotate = new RotateTransform();
        var logoPath = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(MyM3LoadingIndicator.SecondaryShapePath),
            Width = 160,
            Height = 160,
            Stretch = Stretch.Uniform,
            Fill = ThemeBrushes.PrimaryContainer,
            RenderTransform = logoRotate,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => logoRotate.Angle = (logoRotate.Angle + 0.08d) % 360d;
        logoPath.AttachedToVisualTree += (_, _) => timer.Start();
        logoPath.DetachedFromVisualTree += (_, _) => timer.Stop();

        var mark = new Grid
        {
            Width = 196,
            Height = 196,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                logoPath,
                new TextBlock
                {
                    Text = "P",
                    FontSize = 64,
                    FontWeight = FontWeight.Bold,
                    Foreground = ThemeBrushes.PrimaryHover,
                    Margin = new Thickness(0, 16, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        var title = new TextBlock
        {
            FontSize = 30,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        title.Inlines!.Add(new Run("Pixel") { Foreground = ThemeBrushes.PrimaryHover });
        title.Inlines.Add(new Run(" Craft Launcher") { Foreground = BodyForeground });

        var version = new TextBlock
        {
            Text = $"版本 {Basics.VersionName} / {Basics.Metadata.Version.CommitDigest}",
            FontSize = 13,
            Foreground = SubForeground,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var content = new StackPanel
        {
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, 28, 24, 40),
            Children =
            {
                mark,
                title,
                version,
                BuildAboutIntroduction(),
                BuildAboutRepositoryActions(),
                BuildAboutLicenseList(),
                BuildAboutPageShortcuts()
            }
        };

        return new MyScrollViewer
        {
            Content = content
        };
    }

    private static TextBlock BuildAboutIntroduction()
    {
        return new TextBlock
        {
            Text = "Pixel Craft Launcher 是基于 PCL CE 二次开发的跨平台 Minecraft 启动器",
            FontSize = 14,
            Foreground = BodyForeground, 
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8, 0),
            LineHeight = 23
        };
    }

    private Control BuildAboutRepositoryActions()
    {
        return BuildAboutSection("仓库地址", new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                CreateAboutLinkText("https://github.com/Project-Fukakai/Pixel-Craft-Launcher"),
                new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        CreateAboutActionButton("打开仓库", "mdi-github", () => OpenExternalUrl("https://github.com/Project-Fukakai/Pixel-Craft-Launcher")),
                        CreateAboutActionButton("提交反馈", "mdi-bug-outline", () => OpenExternalUrl("https://github.com/Project-Fukakai/Pixel-Craft-Launcher/issues")),
                    }
                }
            }
        });
    }

    private Control BuildAboutLicenseList()
    {
        var list = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        if (Basics.Metadata.Licenses.Length == 0)
        {
            list.Children.Add(CreateAboutMutedText("未记录第三方开源库。"));
        }
        else
        {
            foreach (var license in Basics.Metadata.Licenses)
                list.Children.Add(BuildAboutLicenseItem(license));
        }

        return BuildAboutSection("开源库", list);
    }

    private Control BuildAboutLicenseItem(ThirdPartyLicenseModel license)
    {
        var row = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(license.Information)
                ? license.Name
                : $"{license.Name}  {license.Information}",
            FontSize = 13,
            Foreground = BodyForeground,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        });

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(license.WebsiteUri))
            actions.Children.Add(CreateAboutActionButton("主页", "mdi-web", () => OpenExternalUrl(license.WebsiteUri)));
        if (!string.IsNullOrWhiteSpace(license.LicenseUri))
            actions.Children.Add(CreateAboutActionButton("许可", "mdi-file-document-outline", () => OpenExternalUrl(license.LicenseUri)));
        row.Children.Add(actions);
        return row;
    }

    private Control BuildAboutPageShortcuts()
    {
        return BuildAboutSection("页面入口", new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                CreateAboutActionButton("更新", "mdi-update", () => NavigateSetupShortcut(PixelSettingSectionKind.Update)),
                CreateAboutActionButton("反馈", "mdi-message-alert-outline", () => NavigateSetupShortcut(PixelSettingSectionKind.Feedback)),
                CreateAboutActionButton("日志", "mdi-text-box-outline", () => NavigateSetupShortcut(PixelSettingSectionKind.Log))
            }
        });
    }

    private static Control BuildAboutSection(string title, Control content)
    {
        return new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = BodyForeground,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                content
            }
        };
    }

    private static TextBlock CreateAboutLinkText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = SubForeground,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8, 0)
        };
    }

    private static TextBlock CreateAboutMutedText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = SubForeground,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private MyButton CreateAboutActionButton(string text, string icon, Action action)
    {
        var button = CreateActionButton(text, icon);
        button.Click += (_, _) => action();
        return button;
    }

    private static MyButton CreateActionButton(string text, string icon)
    {
        return new MyButton
        {
            Text = text,
            Icon = icon,
            Variant = MyButtonVariant.Tonal,
            Size = MyButtonSize.Medium,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 8)
        };
    }

    private void NavigateSetupShortcut(PixelSettingSectionKind section)
    {
        PixelSettingsBinder.SetValue("PixelSetupSelectedSection", section.ToString());
        _shellViewModel.NavigateSetupSection(section);
    }

    private void OpenExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        try
        {
            Basics.OpenPath(url);
        }
        catch (Exception ex)
        {
            ShowHint("打开链接失败：" + ex.Message, HintType.Critical);
        }
    }

    private static bool ShouldDisplaySetting(PixelSettingDescriptor setting)
    {
        return setting.ConfigKey switch
        {
            "LaunchArgumentWindowWidth" or "LaunchArgumentWindowHeight" =>
                PixelSettingsBinder.LoadValue("LaunchArgumentWindowType") is int windowType &&
                windowType == (int)GameWindowSizeMode.Custom,
            "LaunchAdvanceRunWait" =>
                !string.IsNullOrWhiteSpace(PixelSettingsBinder.LoadValue("LaunchAdvanceRun")?.ToString()),
            _ => true
        };
    }

    private Control BuildJavaSetupRightPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildCard("Java 管理", BuildJavaToolbar()));
        stack.Children.Add(BuildCard("Java 列表", BuildJavaList()));
        return BuildScrollableMainPane(stack);
    }

    private Control BuildJavaToolbar()
    {
        var addButton = CreateActionButton("添加", "mdi-plus");
        addButton.Click += async (_, _) => await AddJavaAsync();

        var refreshButton = CreateActionButton("刷新", "mdi-refresh");
        refreshButton.Click += async (_, _) => await RefreshJavaListAsync();

        var selected = PixelSettingsBinder.LoadValue("LaunchArgumentJavaSelect")?.ToString();
        var selectedText = string.IsNullOrWhiteSpace(selected) ? "当前默认：自动选择" : $"当前默认：{selected}";

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new WrapPanel
                {
                    Children =
                    {
                        addButton,
                        refreshButton
                    }
                },
                CreateSubText(selectedText)
            }
        };
    }

    private Control BuildJavaList()
    {
        var manager = TryGetJavaManager();
        if (manager is null)
            return CreateInfoBlock("Java 管理服务尚未就绪", "请稍后重新打开本页。");

        var list = new StackPanel { Spacing = 4 };
        var selectedJava = Config.Launch.SelectedJava;
        var autoItem = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = "自动选择",
            Info = "依据游戏版本需求自动选择合适的 Java",
            Icon = "mdi-auto-fix",
            Checked = string.IsNullOrWhiteSpace(selectedJava)
        };
        autoItem.Check += (_, _) =>
        {
            Config.Launch.SelectedJava = string.Empty;
            ShowHint("默认 Java 已设为自动选择。", HintType.Finish);
        };
        list.Children.Add(autoItem);

        var entries = manager.GetSortedJavaList();
        if (entries.Count == 0)
        {
            list.Children.Add(CreateInfoBlock("未检测到 Java", "点击刷新重新扫描，或点击添加手动选择 Java 程序。"));
            return list;
        }

        foreach (var entry in entries)
            list.Children.Add(BuildJavaItem(entry));

        return list;
    }

    private MyListItem BuildJavaItem(JavaEntry entry)
    {
        var installation = entry.Installation;
        var versionType = installation.IsJre ? "JRE" : "JDK";
        var bitness = installation.Is64Bit ? "64 Bit" : "32 Bit";
        var available = installation.IsStillAvailable;
        var title = $"{versionType} {installation.MajorVersion} · {bitness} · {installation.Brand}";
        var info = available
            ? installation.JavaFolder + (entry.IsEnabled ? string.Empty : "（已禁用）")
            : installation.JavaFolder + "（不可用，请刷新列表）";
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = title,
            Info = info,
            Icon = "mdi-language-java",
            Checked = string.Equals(Config.Launch.SelectedJava, installation.JavaExePath, StringComparison.OrdinalIgnoreCase),
            Opacity = available && entry.IsEnabled ? 1 : 0.58
        };
        item.Check += (_, args) =>
        {
            if (!available)
            {
                args.Handled = true;
                item.Checked = false;
                ShowHint("此 Java 不可用，请刷新列表。", HintType.Critical);
                return;
            }

            if (!entry.IsEnabled)
            {
                args.Handled = true;
                item.Checked = false;
                ShowHint("请先启用此 Java 后再选择其作为默认 Java。", HintType.Info);
                return;
            }

            Config.Launch.SelectedJava = installation.JavaExePath;
            ShowHint($"默认 Java 已设为 {versionType} {installation.MajorVersion}。", HintType.Finish);
        };

        var openButton = new MyIconButton
        {
            Icon = "mdi-folder-open-outline",
            IconSize = 12,
            Width = 26,
            Height = 26,
            IsEnabled = available
        };
        ToolTip.SetTip(openButton, "打开");
        openButton.Click += (_, _) => OpenJavaFolder(installation.JavaFolder);
        item.AddButton(openButton);

        var infoButton = new MyIconButton
        {
            Icon = "mdi-information-outline",
            IconSize = 12,
            Width = 26,
            Height = 26,
            IsEnabled = available
        };
        ToolTip.SetTip(infoButton, "详细信息");
        infoButton.Click += (_, _) => ShowJavaInfo(entry);
        item.AddButton(infoButton);

        var enableButton = new MyIconButton
        {
            Icon = entry.IsEnabled ? "mdi-eye-off-outline" : "mdi-eye-outline",
            IconSize = 12,
            Width = 26,
            Height = 26,
            IsEnabled = available
        };
        ToolTip.SetTip(enableButton, entry.IsEnabled ? "禁用此 Java" : "启用此 Java");
        enableButton.Click += (_, _) => ToggleJavaEntry(entry);
        item.AddButton(enableButton);

        return item;
    }

    private async Task AddJavaAsync()
    {
        var storage = StorageProvider;
        var patterns = OperatingSystem.IsWindows() ? new[] { "java.exe" } : new[] { "java", "java.exe", "*" };
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Java 程序",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Java 程序")
                {
                    Patterns = patterns
                }
            ]
        });
        var path = result.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var manager = TryGetJavaManager();
        if (manager is null)
        {
            ShowHint("Java 管理服务尚未就绪。", HintType.Critical);
            return;
        }

        var entry = await Task.Run(() => manager.AddOrGet(path));
        if (entry is null)
        {
            ShowHint("未能成功将 Java 加入列表。", HintType.Critical);
            return;
        }

        manager.SaveConfig();
        ShowHint("已添加 Java。", HintType.Finish);
        RefreshSetupRightPage();
    }

    private async Task RefreshJavaListAsync()
    {
        var manager = TryGetJavaManager();
        if (manager is null)
        {
            ShowHint("Java 管理服务尚未就绪。", HintType.Critical);
            return;
        }

        ShowHint("正在刷新 Java 列表……", HintType.Info);
        await manager.ScanJavaAsync(true);
        ShowHint("Java 列表已刷新。", HintType.Finish);
        RefreshSetupRightPage();
    }

    private void ToggleJavaEntry(JavaEntry entry)
    {
        var manager = TryGetJavaManager();
        if (manager is null)
            return;

        if (!entry.Installation.IsStillAvailable)
        {
            ShowHint("此 Java 不可用，请刷新列表。", HintType.Critical);
            return;
        }

        if (entry.IsEnabled && string.Equals(Config.Launch.SelectedJava, entry.Installation.JavaExePath, StringComparison.OrdinalIgnoreCase))
        {
            ShowHint("请先取消选择此 Java 作为默认 Java 后再禁用。", HintType.Info);
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        manager.SaveConfig();
        ShowHint(entry.IsEnabled ? "已启用 Java。" : "已禁用 Java。", HintType.Finish);
        RefreshSetupRightPage();
    }

    private void ShowJavaInfo(JavaEntry entry)
    {
        var installation = entry.Installation;
        var versionType = installation.IsJre ? "JRE" : "JDK";
        var bitness = installation.Is64Bit ? "64 Bit" : "32 Bit";
        ShowMessage("Java 信息",
            $"类型: {versionType}\n版本: {installation.Version}\n架构: {installation.Architecture} ({bitness})\n品牌: {installation.Brand}\n位置: {installation.JavaFolder}");
    }

    private void OpenJavaFolder(string folder)
    {
        try
        {
            Basics.OpenPath(folder);
        }
        catch (Exception ex)
        {
            ShowHint("打开 Java 文件夹失败：" + ex.Message, HintType.Critical);
        }
    }

    private static JavaManager? TryGetJavaManager()
    {
        try
        {
            return JavaService.JavaManager;
        }
        catch
        {
            return null;
        }
    }

    private Control BuildSettingControl(PixelSettingDescriptor setting)
    {
        if (setting.Control == PixelSettingControlKind.MemoryPreview)
            return CreateMemoryPreviewSetting();

        if (setting.Control == PixelSettingControlKind.ColorScheme)
            return CreateColorSchemeSetting(setting);

        if (setting.Control == PixelSettingControlKind.Info)
            return CreateInfoBlock(setting.Title, setting.Description);

        if (setting.Control == PixelSettingControlKind.Action)
            return CreateActionBlock(setting);

        if (setting.ConfigKey is null)
            return CreateInfoBlock(setting.Title, setting.Description ?? "该设置项没有绑定配置键。");

        return setting.Control switch
        {
            PixelSettingControlKind.Toggle => CreateToggleSetting(setting),
            PixelSettingControlKind.Combo => CreateComboSetting(setting),
            PixelSettingControlKind.Slider => CreateSliderSetting(setting),
            PixelSettingControlKind.Text => CreateTextSetting(setting),
            PixelSettingControlKind.Font => CreateFontSetting(setting),
            _ => CreateInfoBlock(setting.Title, setting.Description)
        };
    }

    private Control CreateToggleSetting(PixelSettingDescriptor setting)
    {
        var value = PixelSettingsBinder.LoadValue(setting.ConfigKey!) is bool b && b;
        var unavailableReason = PixelSettingsBinder.GetUnavailableReason(setting);
        var box = new MyCheckBox
        {
            Text = setting.Title,
            Checked = value,
            IsEnabled = unavailableReason is null && IsSettingInteractionEnabled(setting)
        };
        var description = GetSettingDescription(setting);
        if (!string.IsNullOrWhiteSpace(description))
            ToolTip.SetTip(box, description);
        box.Change += (_, user) =>
        {
            if (user)
                SetSettingValue(setting, box.Checked == true);
        };

        if (string.IsNullOrWhiteSpace(description))
            return box;

        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                box,
                CreateSubText(description)
            }
        };
    }

    private Control CreateComboSetting(PixelSettingDescriptor setting)
    {
        var options = setting.Options ?? [];
        var current = PixelSettingsBinder.LoadValue(setting.ConfigKey!);
        var selectedIndex = Math.Max(0, options.ToList().FindIndex(option => Equals(option.Value, current)));
        var comboBox = new MyComboBox
        {
            IsEditable = setting.IsEditableCombo,
            HintText = setting.IsEditableCombo ? "默认" : null,
            IsEnabled = PixelSettingsBinder.IsControlAvailable(setting) && IsSettingInteractionEnabled(setting),
            MinWidth = 160
        };

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var item = new MyComboBoxItem
            {
                Content = option.Text,
                Tag = option.Value
            };
            if (!string.IsNullOrWhiteSpace(option.ToolTip))
                ToolTip.SetTip(item, option.ToolTip);
            comboBox.Items.Add(item);
        }

        if (setting.IsEditableCombo)
        {
            comboBox.Text = current?.ToString() ?? string.Empty;
            comboBox.TextChanged += (_, _) => SetSettingValue(setting, comboBox.Text ?? string.Empty);
        }
        else if (options.Count > 0)
        {
            comboBox.SelectedIndex = selectedIndex;
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is MyComboBoxItem { Tag: { } value })
                    SetSettingValue(setting, value);
            };
        }

        return CreateLabeledSetting(setting, comboBox);
    }

    private Control CreateColorSchemeSetting(PixelSettingDescriptor setting)
    {
        void ApplySeed(uint nextSeed, ColorSchemeMode mode)
        {
            var formatted = ColorSchemeService.FormatSeed(nextSeed);
            var oldMode = PixelSettingsBinder.LoadValue("UiColorSchemeMode") is int modeValue ? modeValue : -1;
            var oldSeed = PixelSettingsBinder.LoadValue("UiColorSchemeSeed")?.ToString();
            var changed = oldMode != (int)mode || !string.Equals(oldSeed, formatted, StringComparison.OrdinalIgnoreCase);
            if (oldMode != (int)mode)
                PixelSettingsBinder.SetValue("UiColorSchemeMode", (int)mode);
            if (!string.Equals(oldSeed, formatted, StringComparison.OrdinalIgnoreCase))
                PixelSettingsBinder.SetValue("UiColorSchemeSeed", formatted);
            if (changed)
                ThemeService.RefreshColorScheme();
        }

        var manualPicker = CreateColorSchemeIconButton("手动取色", "mdi-palette-outline");
        manualPicker.Click += (_, _) =>
        {
            var pickerSeed = ColorSchemeService.GetEffectiveSeed();
            ShowColorPickerDialog(pickerSeed, nextSeed => ApplySeed(nextSeed, ColorSchemeMode.Manual));
        };

        var pickImage = CreateColorSchemeIconButton("图片取色", "mdi-image-search-outline");
        pickImage.Click += async (_, _) =>
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择取色图片",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("图片")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]
                    }
                ]
            });
            var path = result.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (!ImageColorExtractor.TryExtractSeed(path, out var imageSeed))
            {
                ShowHint("无法从该图片提取配色。", HintType.Critical);
                return;
            }
            PixelSettingsBinder.SetValue("UiColorSchemeImage", path);
            ApplySeed(imageSeed, ColorSchemeMode.Image);
            ShowHint("已从图片提取配色。", HintType.Finish);
        };

        var autoBackground = new MyCheckBox
        {
            Text = "显示背景图片时自动使用图片颜色",
            Checked = PixelSettingsBinder.LoadValue("UiColorSchemeAutoBackground") is bool enabled && enabled
        };
        autoBackground.Change += (_, user) =>
        {
            if (!user)
                return;
            PixelSettingsBinder.SetValue("UiColorSchemeAutoBackground", autoBackground.Checked == true);
            PixelSettingsBinder.SetValue("UiColorSchemeMode", autoBackground.Checked == true
                ? (int)ColorSchemeMode.AutoBackground
                : (int)ColorSchemeMode.Manual);
            ThemeService.RefreshColorScheme();
        };

        var root = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new WrapPanel
                {
                    Children =
                    {
                        CreatePresetColorButton("天空蓝", 0xFF1370F3, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("猫猫蓝", 0xFF2078DA, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("死亡蓝", 0xFF4263DE, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("HMCL 蓝", 0xFF5D6AC4, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("青绿", 0xFF008C7A, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("草绿", 0xFF3F7E2F, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("琥珀", 0xFFB26A00, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        CreatePresetColorButton("玫红", 0xFFC22F6A, (nextSeed, mode) => ApplySeed(nextSeed, mode)),
                        manualPicker,
                        pickImage
                    }
                },
                autoBackground
            }
        };

        return CreateLabeledSetting(setting, root);
    }

    private static MyButton CreateColorSchemeIconButton(string name, string icon)
    {
        var button = new MyButton
        {
            Icon = icon,
            Width = 34,
            Height = 34,
            MinWidth = 34,
            MinHeight = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Variant = MyButtonVariant.Outlined,
            Size = MyButtonSize.Small,
            Margin = new Thickness(0, 0, 8, 8)
        };
        ToolTip.SetTip(button, name);
        return button;
    }

    private static Control CreatePresetColorButton(string name, uint seed, Action<uint, ColorSchemeMode> applySeed)
    {
        var primaryPart = new Border
        {
            [Grid.ColumnProperty] = 0,
            [Grid.RowProperty] = 0,
            [Grid.RowSpanProperty] = 2
        };
        var secondaryPart = new Border
        {
            [Grid.ColumnProperty] = 1,
            [Grid.RowProperty] = 0
        };
        var tertiaryPart = new Border
        {
            [Grid.ColumnProperty] = 1,
            [Grid.RowProperty] = 1
        };

        void ApplyPreviewColors()
        {
            var previewColors = PixelThemePaletteBuilder.BuildPreviewColors(seed, ThemeService.IsDarkMode);
            primaryPart.Background = new SolidColorBrush(previewColors.Primary);
            secondaryPart.Background = new SolidColorBrush(previewColors.Secondary);
            tertiaryPart.Background = new SolidColorBrush(previewColors.Tertiary);
        }

        ApplyPreviewColors();

        var button = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = ThemeBrushes.Surface,
            BorderBrush = ThemeBrushes.BorderStrong,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
            Child = new Grid
            {
                Width = 34,
                Height = 34,
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("*,*"),
                Children =
                {
                    primaryPart,
                    secondaryPart,
                    tertiaryPart
                }
            }
        };
        ColorModeChangedEvent? onColorModeChanged = null;
        onColorModeChanged = (_, _) => ApplyPreviewColors();
        button.AttachedToVisualTree += (_, _) =>
        {
            ThemeService.ColorModeChanged -= onColorModeChanged;
            ThemeService.ColorModeChanged += onColorModeChanged;
            ApplyPreviewColors();
        };
        button.DetachedFromVisualTree += (_, _) => ThemeService.ColorModeChanged -= onColorModeChanged;
        ToolTip.SetTip(button, name);
        button.PointerPressed += (_, e) =>
        {
            applySeed(seed, ColorSchemeMode.Preset);
            e.Handled = true;
        };
        return button;
    }

    private void ShowColorPickerDialog(uint seed, Action<uint> apply)
    {
        var initial = ColorSchemeService.ToColor(seed);
        var preview = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(initial),
            BorderBrush = ThemeBrushes.BorderStrong,
            BorderThickness = new Thickness(1)
        };
        var hexBox = new MyTextBox
        {
            Text = ColorSchemeService.FormatSeed(seed),
            HintText = "#AARRGGBB",
            Width = 142,
            UseFloatingPlaceholder = false
        };
        hexBox.ValidateRules.Add(text => ColorSchemeService.TryParseSeed(text, out _) ? null : "请输入 #RRGGBB 或 #AARRGGBB");

        var current = initial;
        var suppress = false;
        MySlider? redSlider = null;
        MySlider? greenSlider = null;
        MySlider? blueSlider = null;

        void SetCurrent(Color color, bool syncControls)
        {
            current = color;
            preview.Background = new SolidColorBrush(color);
            var formatted = ColorSchemeService.FormatSeed(ColorSchemeService.FromColor(color));
            if (!string.Equals(hexBox.Text, formatted, StringComparison.OrdinalIgnoreCase))
                hexBox.Text = formatted;
            if (!syncControls)
                return;
            suppress = true;
            if (redSlider is not null) redSlider.Value = color.R;
            if (greenSlider is not null) greenSlider.Value = color.G;
            if (blueSlider is not null) blueSlider.Value = color.B;
            suppress = false;
        }

        hexBox.TextChanged += (_, _) =>
        {
            if (suppress || !ColorSchemeService.TryParseSeed(hexBox.Text, out var parsed))
                return;
            SetCurrent(ColorSchemeService.ToColor(parsed), syncControls: true);
        };

        var redRow = CreateColorChannelSlider("R", current.R, out redSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(Color.FromArgb(255, (byte)value, current.G, current.B), syncControls: false);
            });
        var greenRow = CreateColorChannelSlider("G", current.G, out greenSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(Color.FromArgb(255, current.R, (byte)value, current.B), syncControls: false);
            });
        var blueRow = CreateColorChannelSlider("B", current.B, out blueSlider,
            value =>
            {
                if (!suppress)
                    SetCurrent(Color.FromArgb(255, current.R, current.G, (byte)value), syncControls: false);
            });

        var dialog = ShowFormDialog("手动取色");
        dialog.ContentPanel.Children.Add(new StackPanel
        {
            Spacing = 12,
            MinWidth = 360,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        preview,
                        hexBox
                    }
                },
                redRow,
                greenRow,
                blueRow
            }
        });
        dialog.Button1.IsEnabled = hexBox.IsValidated;
        hexBox.ValidateChanged += (_, _) => dialog.Button1.IsEnabled = hexBox.IsValidated;
        dialog.Button1Click += (_, _) =>
        {
            if (!ColorSchemeService.TryParseSeed(hexBox.Text, out var parsed))
            {
                ShowHint("请输入有效颜色。", HintType.Critical);
                return;
            }
            apply(parsed);
        };
    }

    private static Control CreateColorChannelSlider(string label, byte initial, out MySlider slider, Action<int> changed)
    {
        var valueText = CreateSubText(initial.ToString());
        valueText.Width = 34;
        valueText.TextAlignment = TextAlignment.Right;
        var channelSlider = new MySlider
        {
            Minimum = 0,
            Maximum = 255,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Value = initial
        };
        slider = channelSlider;
        channelSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != MySlider.ValueProperty)
                return;
            var value = (int)Math.Round(channelSlider.Value);
            valueText.Text = value.ToString();
            changed(value);
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(18)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(42))
            },
            ColumnSpacing = 8
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = SubForeground,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(channelSlider, 1);
        row.Children.Add(channelSlider);
        Grid.SetColumn(valueText, 2);
        row.Children.Add(valueText);
        return row;
    }

    private Control CreateSliderSetting(PixelSettingDescriptor setting)
    {
        var raw = PixelSettingsBinder.LoadValue(setting.ConfigKey!);
        var maximum = GetRuntimeMaximum(setting);
        var value = raw is null ? setting.Minimum : Convert.ToDouble(raw);
        value = Math.Clamp(value, setting.Minimum, maximum);
        var valueText = CreateSubText(FormatSettingValue(setting, value));
        valueText.HorizontalAlignment = HorizontalAlignment.Right;

        var slider = new MySlider
        {
            Minimum = setting.Minimum,
            Maximum = maximum,
            TickFrequency = setting.TickFrequency,
            IsSnapToTickEnabled = setting.TickFrequency > 0,
            Value = value,
            IsEnabled = PixelSettingsBinder.IsControlAvailable(setting) && IsSettingInteractionEnabled(setting)
        };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != MySlider.ValueProperty)
                return;
            var next = Math.Round(slider.Value);
            valueText.Text = FormatSettingValue(setting, next);
            SetSettingValue(setting, next);
        };

        var control = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(54))
            }
        };
        control.Children.Add(slider);
        Grid.SetColumn(valueText, 1);
        control.Children.Add(valueText);
        return CreateLabeledSetting(setting, control);
    }

    private Control CreateTextSetting(PixelSettingDescriptor setting)
    {
        var textBox = new MyTextBox
        {
            HintText = setting.Title,
            Text = PixelSettingsBinder.LoadValue(setting.ConfigKey!)?.ToString() ?? string.Empty,
            IsEnabled = PixelSettingsBinder.IsControlAvailable(setting) && IsSettingInteractionEnabled(setting),
            UseFloatingPlaceholder = false
        };
        textBox.TextChanged += (_, _) =>
        {
            if (!SetSettingValue(setting, textBox.Text ?? string.Empty))
                ShowHint("输入包含不允许的字符，已忽略。", HintType.Critical);
        };
        if (setting.ConfigKey == "UiBackgroundFolder")
            return CreateLabeledSetting(setting, CreateBackgroundFolderInput(setting, textBox));

        if (setting.ConfigKey != "LaunchAdvanceJvm")
            return CreateLabeledSetting(setting, textBox);

        var resetButton = new MyIconButton
        {
            Icon = "mdi-restore",
            IconSize = 13,
            Width = 34,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0)
        };
        ToolTip.SetTip(resetButton, "还原默认 JVM 参数");
        resetButton.Click += (_, _) =>
        {
            if (PixelSettingsBinder.TryGetItem("LaunchAdvanceJvm", out var item))
            {
                item.Reset();
                textBox.Text = PixelSettingsBinder.LoadValue("LaunchAdvanceJvm")?.ToString() ?? string.Empty;
                ShowHint("JVM 参数头部已还原。", HintType.Finish);
            }
        };

        var inputWithReset = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        inputWithReset.Children.Add(textBox);
        Grid.SetColumn(resetButton, 1);
        inputWithReset.Children.Add(resetButton);
        return CreateLabeledSetting(setting, inputWithReset);
    }

    private Control CreateFontSetting(PixelSettingDescriptor setting)
    {
        var selector = new FontSelector
        {
            SelectedFontTag = PixelSettingsBinder.LoadValue(setting.ConfigKey!)?.ToString() ?? string.Empty,
            Tooltip = GetSettingDescription(setting),
            IsEnabled = PixelSettingsBinder.IsControlAvailable(setting) && IsSettingInteractionEnabled(setting)
        };
        selector.SelectionChanged += (_, _) => SetSettingValue(setting, selector.SelectedFontTag);
        return CreateLabeledSetting(setting, selector);
    }

    private Control CreateBackgroundFolderInput(PixelSettingDescriptor setting, MyTextBox textBox)
    {
        var selectButton = new MyIconButton
        {
            Icon = "mdi-folder-search-outline",
            IconSize = 14,
            Width = 34,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            IsEnabled = textBox.IsEnabled
        };
        ToolTip.SetTip(selectButton, "选择背景目录");
        selectButton.Click += async (_, _) =>
        {
            var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择背景目录",
                AllowMultiple = false
            });
            var path = selected.Count > 0 ? selected[0].TryGetLocalPath() : null;
            if (string.IsNullOrWhiteSpace(path))
                return;

            textBox.Text = path;
            SetSettingValue(setting, path);
        };

        var openButton = new MyIconButton
        {
            Icon = "mdi-folder-open-outline",
            IconSize = 14,
            Width = 34,
            Height = 34,
            Margin = new Thickness(6, 0, 0, 0)
        };
        ToolTip.SetTip(openButton, "打开背景目录");
        openButton.Click += (_, _) => OpenPersonalizationBackgroundFolder();

        var inputWithButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        inputWithButtons.Children.Add(textBox);
        Grid.SetColumn(selectButton, 1);
        inputWithButtons.Children.Add(selectButton);
        Grid.SetColumn(openButton, 2);
        inputWithButtons.Children.Add(openButton);
        return inputWithButtons;
    }

    private Control CreateLabeledSetting(PixelSettingDescriptor setting, Control control)
    {
        return new SettingRow
        {
            Title = setting.Title,
            Description = string.IsNullOrWhiteSpace(setting.Description) ? string.Empty : GetSettingDescription(setting),
            SettingControl = control
        };
    }

    private static bool IsSettingInteractionEnabled(PixelSettingDescriptor setting)
    {
        return setting.ConfigKey switch
        {
            "LaunchRamCustom" => PixelSettingsBinder.LoadValue("LaunchRamType") is int mode && mode == 1,
            "SystemHttpProxy" or "SystemHttpProxyCustomUsername" or "SystemHttpProxyCustomPassword"
                => PixelSettingsBinder.LoadValue("SystemHttpProxyType") is int proxyMode && proxyMode == 2,
            _ => true
        };
    }

    private static double GetRuntimeMaximum(PixelSettingDescriptor setting)
    {
        if (setting.ConfigKey != "LaunchRamCustom")
            return setting.Maximum;

        var totalGb = KernelInterop.GetPhysicalMemoryBytes().Total / 1024d / 1024d / 1024d;
        return Math.Clamp(GetRamScaleMaximum(totalGb), setting.Minimum, setting.Maximum);
    }

    private bool SetSettingValue(PixelSettingDescriptor setting, object? value)
    {
        DebugSettingsService.DelayIfNeeded("保存设置 " + (setting.ConfigKey ?? setting.Title));
        if (!PixelSettingsBinder.SetValue(setting, value))
            return false;

        OnSettingChanged(setting.ConfigKey, value);
        return true;
    }

    private void OnSettingChanged(string? key, object? value)
    {
        switch (key)
        {
            case "LaunchArgumentWindowType":
            case "LaunchRamType":
                RefreshSetupRightPage();
                break;
            case "SystemHttpProxyType":
                RefreshSetupRightPage();
                break;
            case "SystemDisableHardwareAcceleration":
                ShowHint("禁用硬件加速将在下次启动启动器时生效。", HintType.Info);
                break;
            case "UiAcrylic":
                RefreshShellTheme();
                break;
            case "LaunchAdvanceRun":
                RefreshSetupRightPageOnRunWaitBoundary(value?.ToString());
                break;
            case "LaunchAdvanceRenderer":
                if (value is int renderer && renderer != 0 && !States.Hint.Renderer)
                {
                    States.Hint.Renderer = true;
                    ShowMessage("警告", "修改渲染器会显著影响游戏稳定性与性能。只有在明确知道自己需要软渲染、DirectX12 或 Vulkan 兼容层时才建议继续使用。", true);
                }
                break;
            case "LaunchArgumentVisible":
                if (value is int visibility && visibility == (int)LauncherVisibility.ExitImmediately)
                    ShowMessage("提醒", "若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。若想保留这些功能，可以选择让启动器在游戏启动后隐藏。");
                break;
            case "LaunchArgumentRam":
                if (value is true)
                    ShowMessage("提醒", "内存优化会显著延长启动耗时，建议仅在内存不足时开启。机械硬盘上还可能造成一小段时间的明显卡顿。");
                break;
        }
    }

    private bool _isRunWaitVisible = !string.IsNullOrWhiteSpace(PixelSettingsBinder.LoadValue("LaunchAdvanceRun")?.ToString());

    private void RefreshSetupRightPageOnRunWaitBoundary(string? command)
    {
        var nextVisible = !string.IsNullOrWhiteSpace(command);
        if (nextVisible == _isRunWaitVisible)
            return;
        _isRunWaitVisible = nextVisible;
        RefreshSetupRightPage();
    }

    private void RefreshSetupRightPage()
    {
        if (SelectedMainPage != MainPageKind.Setup)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
        }, DispatcherPriority.Background);
    }

    private static string GetSettingDescription(PixelSettingDescriptor setting)
    {
        var unavailableReason = PixelSettingsBinder.GetUnavailableReason(setting);
        if (!string.IsNullOrWhiteSpace(unavailableReason))
            return string.IsNullOrWhiteSpace(setting.Description)
                ? unavailableReason
                : $"{setting.Description}（{unavailableReason}）";

        return setting.Description ?? string.Empty;
    }

    private static string FormatSettingValue(PixelSettingDescriptor setting, double value)
    {
        var text = setting.ValueFormatter == "RamScale"
            ? FormatRamScale(value)
            : Math.Abs(value - Math.Round(value)) < 0.0001
                ? Math.Round(value).ToString()
                : value.ToString("0.##");
        return string.IsNullOrWhiteSpace(setting.UnitText) ? text : $"{text} {setting.UnitText}";
    }

    private static string FormatRamScale(double value)
    {
        var ram = RamScaleToGb(value);
        return $"{ram:0.#} GB";
    }

    private static double RamScaleToGb(double value)
    {
        return value switch
        {
            <= 12 => Math.Round(value * 0.1 + 0.3, 1),
            <= 25 => Math.Round(1.5 + (value - 12) * 0.5, 1),
            <= 33 => Math.Round(8 + (value - 25), 1),
            _ => Math.Round(16 + (value - 33) * 2, 1)
        };
    }

    private static int GetRamScaleMaximum(double totalGb)
    {
        if (totalGb <= 1.5d)
            return (int)Math.Round(Math.Max(Math.Floor((totalGb - 0.3d) / 0.1d), 1d));
        if (totalGb <= 8d)
            return (int)Math.Round(Math.Floor((totalGb - 1.5d) / 0.5d) + 12d);
        if (totalGb <= 16d)
            return (int)Math.Round(Math.Floor(totalGb - 8d) + 25d);
        return (int)Math.Round(Math.Floor((totalGb - 16d) / 2d) + 33d);
    }

    private Control CreateInfoBlock(string title, string? description)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Foreground = BodyForeground,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold
                },
                CreateSubText(description ?? string.Empty)
            }
        };
    }

    private Control CreateActionBlock(PixelSettingDescriptor setting)
    {
        var button = new MyButton
        {
            Text = setting.Title,
            IsEnabled = PixelSettingsBinder.IsControlAvailable(setting),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) =>
        {
            if (setting.Title == "打开背景目录")
            {
                OpenPersonalizationBackgroundFolder();
                return;
            }

            ShowHint(PixelSettingsBinder.GetUnavailableReason(setting) ?? setting.UnavailableReason ?? "操作入口已预留。", HintType.Info);
        };
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                button,
                CreateSubText(setting.Description ?? setting.UnavailableReason ?? string.Empty)
            }
        };
    }

    private void OpenPersonalizationBackgroundFolder()
    {
        try
        {
            Directory.CreateDirectory(_personalization.BackgroundFolder);
            Basics.OpenPath(_personalization.BackgroundFolder);
        }
        catch (Exception ex)
        {
            ShowHint("打开背景目录失败：" + ex.Message, HintType.Critical);
        }
    }

    private Control CreateMemoryPreviewSetting()
    {
        var usedColumn = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
        var gameColumn = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
        var freeColumn = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
        var totalText = CreateSubText("");
        var usedText = CreateSubText("");
        var gameText = CreateSubText("");
        var freeText = CreateSubText("");
        var warningText = CreateSubText("");
        warningText.Foreground = ThemeBrushes.MemoryUsed;

        var bar = new Grid
        {
            Height = 10,
            ClipToBounds = true,
            ColumnDefinitions =
            {
                usedColumn,
                gameColumn,
                freeColumn
            },
            Children =
            {
                new Border
                {
                    Background = ThemeBrushes.MemoryUsed,
                    CornerRadius = new CornerRadius(5, 0, 0, 5),
                    Opacity = 0.72
                },
                new Border
                {
                    Background = ThemeBrushes.MemoryGame,
                    Opacity = 0.52
                },
                new Border
                {
                    Background = ThemeBrushes.MemoryFree,
                    CornerRadius = new CornerRadius(0, 5, 5, 0),
                    Opacity = 0.72
                }
            }
        };
        Grid.SetColumn(bar.Children[1], 1);
        Grid.SetColumn(bar.Children[2], 2);

        var legend = new WrapPanel
        {
            Margin = new Thickness(0, 2, 0, 0),
            Children =
            {
                CreateMemoryLegend(ThemeBrushes.MemoryUsed, usedText),
                CreateMemoryLegend(ThemeBrushes.MemoryGame, gameText),
                CreateMemoryLegend(ThemeBrushes.MemoryFree, freeText)
            }
        };

        var root = new StackPanel
        {
            Spacing = 7,
            Children =
            {
                totalText,
                bar,
                legend,
                warningText
            }
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        root.AttachedToVisualTree += (_, _) =>
        {
            Refresh();
            timer.Start();
        };
        root.DetachedFromVisualTree += (_, _) => timer.Stop();
        Refresh();

        return root;

        void Refresh()
        {
            var snapshot = GetMemoryPreviewSnapshot();
            totalText.Text = $"总内存 {FormatGb(snapshot.TotalGb)}";
            usedText.Text = $"已用 {FormatGb(snapshot.UsedGb)}";
            gameText.Text = $"游戏预估 {FormatGb(snapshot.GameGb)}";
            freeText.Text = $"启动后空闲 {FormatGb(snapshot.FreeAfterLaunchGb)}";
            warningText.Text = snapshot.GameGb > snapshot.AvailableGb
                ? $"当前可用内存只有 {FormatGb(snapshot.AvailableGb)}，游戏可实际分配约 {FormatGb(snapshot.GameActualGb)}。"
                : string.Empty;
            warningText.IsVisible = !string.IsNullOrWhiteSpace(warningText.Text);

            usedColumn.Width = ToStar(snapshot.UsedGb);
            gameColumn.Width = ToStar(snapshot.GameActualGb);
            freeColumn.Width = ToStar(snapshot.FreeAfterLaunchGb);
        }
    }

    private static StackPanel CreateMemoryLegend(IBrush brush, TextBlock label)
    {
        label.Margin = new Thickness(0, 0, 14, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 8, 0),
            Children =
            {
                new Border
                {
                    Width = 9,
                    Height = 9,
                    CornerRadius = new CornerRadius(5),
                    Background = brush,
                    VerticalAlignment = VerticalAlignment.Center
                },
                label
            }
        };
    }

    private MemoryPreviewSnapshot GetMemoryPreviewSnapshot()
    {
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        var totalGb = memory.Total / 1024d / 1024d / 1024d;
        var availableGb = memory.Available / 1024d / 1024d / 1024d;
        var usedGb = Math.Max(0, totalGb - availableGb);
        var gameGb = GetEstimatedGameMemoryGb(availableGb);
        var gameActualGb = Math.Min(gameGb, Math.Max(availableGb, 0));
        var freeAfterLaunchGb = Math.Max(0, totalGb - usedGb - gameActualGb);
        return new MemoryPreviewSnapshot(totalGb, usedGb, availableGb, gameGb, gameActualGb, freeAfterLaunchGb);
    }

    private double GetEstimatedGameMemoryGb(double availableGb)
    {
        if (_launchViewModel.SelectedInstance is { } instance)
            return MinecraftLaunchService.GetGlobalConfiguredMemoryGb(instance, false);

        if (Config.Launch.MemoryAllocationMode == 1)
            return RamScaleToGb(Config.Launch.CustomMemorySize);

        var give = 0d;
        Add(1.5d, 1d);
        Add(1d, 0.7d);
        Add(1.5d, 0.4d);
        Add(4d, 0.15d);
        return Math.Round(Math.Max(give, 0.5d), 1);

        void Add(double delta, double ratio)
        {
            if (availableGb < 0.1d) return;
            give += Math.Min(availableGb * ratio, delta);
            availableGb -= delta / ratio;
        }
    }

    private static GridLength ToStar(double value) => new(Math.Max(value, 0.01d), GridUnitType.Star);

    private static string FormatGb(double value) => $"{Math.Round(value, 1):0.#} GB";

    private readonly record struct MemoryPreviewSnapshot(
        double TotalGb,
        double UsedGb,
        double AvailableGb,
        double GameGb,
        double GameActualGb,
        double FreeAfterLaunchGb);

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SubForeground,
            FontSize = 12,
            LineHeight = 18
        };
    }

}
