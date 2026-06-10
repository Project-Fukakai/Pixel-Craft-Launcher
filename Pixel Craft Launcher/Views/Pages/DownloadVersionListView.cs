using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.ViewModels;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class DownloadVersionListView : UserControl
{
    private readonly PixelDownloadViewModel _viewModel;
    private readonly bool _installMode;
    private readonly Action<string> _openInstallSelection;
    private readonly Func<string, Task> _installVanillaVersion;
    private readonly Func<string, string, Task> _saveClientCore;
    private readonly Func<string, string, Task> _saveServerJar;
    private readonly Func<string, Task<string?>> _pickSaveFolder;
    private readonly Action<string> _openVersionInfo;
    private readonly PixelDownloadVersionListMessages _messages;

    public DownloadVersionListView(
        PixelDownloadViewModel viewModel,
        bool installMode,
        Action<string> openInstallSelection,
        Func<string, Task> installVanillaVersion,
        Func<string, string, Task> saveClientCore,
        Func<string, string, Task> saveServerJar,
        Func<string, Task<string?>> pickSaveFolder,
        Action<string> openVersionInfo,
        PixelDownloadVersionListMessages messages)
    {
        _viewModel = viewModel;
        _installMode = installMode;
        _openInstallSelection = openInstallSelection;
        _installVanillaVersion = installVanillaVersion;
        _saveClientCore = saveClientCore;
        _saveServerJar = saveServerJar;
        _pickSaveFolder = pickSaveFolder;
        _openVersionInfo = openVersionInfo;
        _messages = messages;
        var snapshot = viewModel.GetVersionListPageSnapshot();

        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };

        foreach (var group in snapshot.Groups)
            stack.Children.Add(BuildVersionGroupCard(group, snapshot.EmptyListText));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private MyCard BuildVersionGroupCard(
        PixelDownloadVersionListGroupSnapshot group,
        string emptyListText)
    {
        var content = group.IsSwapped ? new StackPanel { Spacing = 2 } : BuildVersionList(group, emptyListText);
        var card = new MyCard
        {
            Title = group.CardTitle,
            CanSwap = true,
            IsSwapped = group.IsSwapped,
            CardContent = content
        };
        if (group.IsSwapped)
            card.InstallMethod = stack => PopulateVersionList(stack, group, emptyListText);
        return card;
    }

    private Control BuildVersionList(PixelDownloadVersionListGroupSnapshot group, string emptyListText)
    {
        var list = new StackPanel { Spacing = 2 };
        PopulateVersionList(list, group, emptyListText);
        return list;
    }

    private void PopulateVersionList(
        StackPanel list,
        PixelDownloadVersionListGroupSnapshot group,
        string emptyListText)
    {
        if (list.Children.Count > 0)
            return;

        if (!group.HasVersions)
        {
            list.Children.Add(CreateBodyText(emptyListText));
            return;
        }

        foreach (var version in group.Versions)
        {
            var item = new MyListItem
            {
                Title = version.Title,
                Info = version.Info,
                Icon = version.Icon,
                Type = MyListItem.CheckType.Clickable
            };
            item.Click += async (_, _) =>
            {
                if (_installMode)
                    _openInstallSelection(version.Id);
                else
                    await _installVanillaVersion(version.Id);
            };
            AddVersionActionButtons(item, version);
            list.Children.Add(item);
        }
    }

    private void AddVersionActionButtons(MyListItem item, PixelDownloadVersionSnapshot version)
    {
        var save = CreateVersionActionButton("mdi-content-save-outline", _messages.SaveClientTooltip);
        save.Click += async (_, _) =>
        {
            var folder = await _pickSaveFolder(_messages.SaveClientPickerTitle);
            if (!string.IsNullOrWhiteSpace(folder))
                await _saveClientCore(version.Id, folder);
        };
        item.AddButton(save);

        var info = CreateVersionActionButton("mdi-information-outline", _messages.VersionInfoTooltip);
        info.Click += (_, _) => _openVersionInfo(version.WikiUrlSuffix);
        item.AddButton(info);

        var server = CreateVersionActionButton("mdi-server-outline", _messages.SaveServerTooltip);
        server.Click += async (_, _) =>
        {
            var folder = await _pickSaveFolder(_messages.SaveServerPickerTitle);
            if (!string.IsNullOrWhiteSpace(folder))
                await _saveServerJar(version.Id, folder);
        };
        item.AddButton(server);
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

    private static TextBlock CreateBodyText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            FontSize = 14,
            LineHeight = 22
        };
    }
}
