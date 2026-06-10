using System.Collections.Generic;
using System.Linq;
using PCL.Core.App.Configuration;
using PCL.Core.App.Pixel.Infrastructure;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;

namespace PCL.Core.App.Pixel.Slices.Java;

public sealed class PixelJavaService
{
    private readonly IPixelCommandBus _commandBus;

    public PixelJavaService(IPixelCommandBus commandBus)
    {
        _commandBus = commandBus;
    }

    public PixelJavaToolbarSnapshot GetToolbarSnapshot()
    {
        var selected = Config.Launch.SelectedJava;
        return new PixelJavaToolbarSnapshot(
            selected,
            string.IsNullOrWhiteSpace(selected) ? "当前默认：自动选择" : $"当前默认：{selected}");
    }

    public PixelJavaListSnapshot GetListSnapshot()
    {
        try
        {
            var selected = Config.Launch.SelectedJava;
            var entries = GetEntries()
                .Select(entry => CreateEntrySnapshot(entry, selected))
                .ToArray();
            return PixelJavaListSnapshot.Ready(selected, entries);
        }
        catch (InvalidOperationException)
        {
            return PixelJavaListSnapshot.NotReady("Java 管理服务尚未就绪", "请稍后重新打开本页。");
        }
    }

    internal IReadOnlyList<JavaEntry> GetEntries()
    {
        return GetRequiredManager().GetSortedJavaList();
    }

    internal async Task<IReadOnlyList<JavaEntry>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = GetRequiredManager();
        await manager.ScanJavaAsync(true).ConfigureAwait(false);
        return manager.GetSortedJavaList();
    }

    internal async Task<JavaEntry> AddAsync(string javaExecutablePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = GetRequiredManager();
        var entry = await Task.Run(() => manager.AddOrGet(javaExecutablePath), cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("未能成功将 Java 加入列表。");
        manager.SaveConfig();
        return entry;
    }

    internal void SelectDefault(JavaEntry? entry)
    {
        if (entry is null)
        {
            Config.Launch.SelectedJava = string.Empty;
            return;
        }

        if (!entry.Installation.IsStillAvailable)
            throw new InvalidOperationException("此 Java 不可用，请刷新列表。");
        if (!entry.IsEnabled)
            throw new InvalidOperationException("请先启用此 Java 后再选择其作为默认 Java。");

        Config.Launch.SelectedJava = entry.Installation.JavaExePath;
    }

    internal void SelectDefault(string? javaExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
        {
            SelectDefault((JavaEntry?)null);
            return;
        }

        SelectDefault(FindEntry(javaExecutablePath));
    }

    internal bool ToggleEnabled(JavaEntry entry)
    {
        if (!entry.Installation.IsStillAvailable)
            throw new InvalidOperationException("此 Java 不可用，请刷新列表。");
        if (entry.IsEnabled && string.Equals(Config.Launch.SelectedJava, entry.Installation.JavaExePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请先取消选择此 Java 作为默认 Java 后再禁用。");

        entry.IsEnabled = !entry.IsEnabled;
        GetRequiredManager().SaveConfig();
        return entry.IsEnabled;
    }

    internal bool ToggleEnabled(string javaExecutablePath)
    {
        return ToggleEnabled(FindEntry(javaExecutablePath));
    }

    public Task RefreshForPageAsync()
    {
        return RequireCommandBus().Send(new RefreshJavaEntriesCommand());
    }

    public Task AddForPageAsync(string javaExecutablePath)
    {
        return RequireCommandBus().Send(new AddJavaEntryCommand(javaExecutablePath));
    }

    public Task SelectDefaultForPageAsync(string? javaExecutablePath)
    {
        return RequireCommandBus().Send(new SelectDefaultJavaByPathCommand(javaExecutablePath));
    }

    public Task<bool> ToggleEnabledForPageAsync(string javaExecutablePath)
    {
        return RequireCommandBus().Send(new ToggleJavaEntryEnabledByPathCommand(javaExecutablePath));
    }

    public PixelJavaPageMessages GetMessages() => GetPageMessages();

    public static PixelJavaPageMessages GetPageMessages()
    {
        return new PixelJavaPageMessages(
            "Java 管理",
            "Java 列表",
            "添加",
            "刷新",
            "Java 管理服务尚未就绪",
            "自动选择",
            "依据游戏版本需求自动选择合适的 Java",
            "默认 Java 已设为自动选择。",
            "未检测到 Java",
            "点击刷新重新扫描，或点击添加手动选择 Java 程序。",
            "此 Java 不可用，请刷新列表。",
            "请先启用此 Java 后再选择其作为默认 Java。",
            "默认 Java 已设为 {0}。",
            "打开",
            "详细信息",
            "禁用此 Java",
            "启用此 Java",
            "已启用 Java。",
            "已禁用 Java。",
            "已添加 Java。",
            "正在刷新 Java 列表……",
            "Java 列表已刷新。",
            "Java 信息",
            "选择 Java 程序",
            "Java 程序",
            "打开 Java 文件夹失败：{0}");
    }

    public static string GetAddSuccessMessage() => GetPageMessages().AddSuccessMessage;

    public static string GetRefreshStartMessage() => GetPageMessages().RefreshStartMessage;

    public static string GetRefreshSuccessMessage() => GetPageMessages().RefreshSuccessMessage;

    public static string GetInfoDialogTitle() => GetPageMessages().InfoDialogTitle;

    public static string GetAddPickerTitle() => GetPageMessages().AddPickerTitle;

    public static string GetAddPickerFileTypeName() => GetPageMessages().AddPickerFileTypeName;

    public static string GetOpenFolderFailedMessage(Exception exception) =>
        GetPageMessages().GetOpenFolderFailedMessage(exception);

    internal static PixelJavaEntrySnapshot CreateEntrySnapshot(JavaEntry entry, string? selectedJava)
    {
        var installation = entry.Installation;
        var versionType = installation.IsJre ? "JRE" : "JDK";
        var bitness = installation.Is64Bit ? "64 Bit" : "32 Bit";
        var available = installation.IsStillAvailable;
        var title = $"{versionType} {installation.MajorVersion} · {bitness} · {installation.Brand}";
        var info = available
            ? installation.JavaFolder + (entry.IsEnabled ? string.Empty : "（已禁用）")
            : installation.JavaFolder + "（不可用，请刷新列表）";
        var details = $"类型: {versionType}\n版本: {installation.Version}\n架构: {installation.Architecture} ({bitness})\n品牌: {installation.Brand}\n位置: {installation.JavaFolder}";

        return new PixelJavaEntrySnapshot(
            title,
            info,
            details,
            $"{versionType} {installation.MajorVersion}",
            installation.JavaExePath,
            installation.JavaFolder,
            available,
            entry.IsEnabled,
            string.Equals(selectedJava, installation.JavaExePath, StringComparison.OrdinalIgnoreCase));
    }

    internal PixelJavaEntrySnapshot CreateEntrySnapshot(JavaEntry entry)
    {
        return CreateEntrySnapshot(entry, Config.Launch.SelectedJava);
    }

    internal IReadOnlyList<PixelJavaEntrySnapshot> CreateEntrySnapshots(IEnumerable<JavaEntry> entries)
    {
        var selected = Config.Launch.SelectedJava;
        return entries
            .Select(entry => CreateEntrySnapshot(entry, selected))
            .ToArray();
    }

    private JavaEntry FindEntry(string javaExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
            throw new InvalidOperationException("Java 路径不能为空。");

        var entry = GetEntries().FirstOrDefault(item =>
            string.Equals(item.Installation.JavaExePath, javaExecutablePath, StringComparison.OrdinalIgnoreCase));
        return entry ?? throw new InvalidOperationException("未找到指定 Java，请刷新列表。");
    }

    private static JavaManager GetRequiredManager()
    {
        try
        {
            return JavaService.JavaManager;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Java 管理服务尚未就绪。", ex);
        }
    }

    private IPixelCommandBus RequireCommandBus()
    {
        return _commandBus;
    }
}

public sealed record PixelJavaToolbarSnapshot(string SelectedJavaPath, string SelectedText);

public sealed record PixelJavaPageMessages(
    string ManagementCardTitle,
    string ListCardTitle,
    string AddButtonText,
    string RefreshButtonText,
    string NotReadyFallbackTitle,
    string AutoSelectTitle,
    string AutoSelectDescription,
    string AutoSelectSuccessMessage,
    string EmptyListTitle,
    string EmptyListDescription,
    string UnavailableMessage,
    string DisabledSelectionMessage,
    string DefaultSelectedMessageFormat,
    string OpenFolderTooltip,
    string InfoTooltip,
    string DisableTooltip,
    string EnableTooltip,
    string EnabledMessage,
    string DisabledMessage,
    string AddSuccessMessage,
    string RefreshStartMessage,
    string RefreshSuccessMessage,
    string InfoDialogTitle,
    string AddPickerTitle,
    string AddPickerFileTypeName,
    string OpenFolderFailedMessageFormat)
{
    public string GetDefaultSelectedMessage(string versionLabel) =>
        string.Format(DefaultSelectedMessageFormat, versionLabel);

    public string GetToggleEnabledMessage(bool enabled) =>
        enabled ? EnabledMessage : DisabledMessage;

    public string GetOpenFolderFailedMessage(Exception exception) =>
        string.Format(OpenFolderFailedMessageFormat, exception.Message);

    public string GetOperationFailureMessage(Exception exception) =>
        exception.Message;
}

public sealed record PixelJavaListSnapshot(
    bool IsReady,
    string? NotReadyTitle,
    string? NotReadyDescription,
    string SelectedJavaPath,
    bool IsEmpty,
    IReadOnlyList<PixelJavaEntrySnapshot> Entries)
{
    public static PixelJavaListSnapshot Ready(string selectedJavaPath, IReadOnlyList<PixelJavaEntrySnapshot> entries) =>
        new(true, null, null, selectedJavaPath, entries.Count == 0, entries);

    public static PixelJavaListSnapshot NotReady(string title, string description) =>
        new(false, title, description, string.Empty, true, []);
}

public sealed record PixelJavaEntrySnapshot(
    string Title,
    string Info,
    string Details,
    string VersionLabel,
    string JavaExecutablePath,
    string Folder,
    bool IsAvailable,
    bool IsEnabled,
    bool IsSelected)
{
    public double Opacity => IsAvailable && IsEnabled ? 1 : 0.58;
}
