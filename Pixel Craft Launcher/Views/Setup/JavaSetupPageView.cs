using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Java;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Views;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class JavaSetupPageView : StackPanel
{
    private readonly Func<Task> _addJava;
    private readonly Func<Task> _refreshJava;
    private readonly Func<PixelJavaEntrySnapshot?, Task> _selectDefaultJava;
    private readonly Func<PixelJavaEntrySnapshot, Task> _toggleJava;
    private readonly Action<string> _openFolder;
    private readonly Action<PixelJavaEntrySnapshot> _showInfo;
    private readonly Action<string, HintType> _showHint;
    private readonly PixelJavaPageMessages _messages;

    public JavaSetupPageView(
        PixelJavaToolbarSnapshot toolbar,
        PixelJavaListSnapshot list,
        Func<Task> addJava,
        Func<Task> refreshJava,
        Func<PixelJavaEntrySnapshot?, Task> selectDefaultJava,
        Func<PixelJavaEntrySnapshot, Task> toggleJava,
        Action<string> openFolder,
        Action<PixelJavaEntrySnapshot> showInfo,
        Action<string, HintType> showHint,
        PixelJavaPageMessages messages)
    {
        _addJava = addJava;
        _refreshJava = refreshJava;
        _selectDefaultJava = selectDefaultJava;
        _toggleJava = toggleJava;
        _openFolder = openFolder;
        _showInfo = showInfo;
        _showHint = showHint;
        _messages = messages;

        Margin = new Thickness(22, 20, 22, 26);
        Spacing = 14;

        Children.Add(BuildCard(_messages.ManagementCardTitle, BuildToolbar(toolbar)));
        Children.Add(BuildCard(_messages.ListCardTitle, BuildList(list)));
    }

    private Control BuildToolbar(PixelJavaToolbarSnapshot toolbar)
    {
        var addButton = CreateActionButton(_messages.AddButtonText, "mdi-plus");
        addButton.Click += async (_, _) => await _addJava();

        var refreshButton = CreateActionButton(_messages.RefreshButtonText, "mdi-refresh");
        refreshButton.Click += async (_, _) => await _refreshJava();

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
                CreateSubText(toolbar.SelectedText)
            }
        };
    }

    private Control BuildList(PixelJavaListSnapshot snapshot)
    {
        if (!snapshot.IsReady)
            return CreateInfoBlock(snapshot.NotReadyTitle ?? _messages.NotReadyFallbackTitle, snapshot.NotReadyDescription);

        var list = new StackPanel { Spacing = 4 };
        var autoItem = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = _messages.AutoSelectTitle,
            Info = _messages.AutoSelectDescription,
            Icon = "mdi-auto-fix",
            Checked = string.IsNullOrWhiteSpace(snapshot.SelectedJavaPath)
        };
        autoItem.Check += async (_, _) =>
        {
            await _selectDefaultJava(null);
        };
        list.Children.Add(autoItem);

        if (snapshot.IsEmpty)
        {
            list.Children.Add(CreateInfoBlock(_messages.EmptyListTitle, _messages.EmptyListDescription));
            return list;
        }

        foreach (var entry in snapshot.Entries)
            list.Children.Add(BuildJavaItem(entry));

        return list;
    }

    private MyListItem BuildJavaItem(PixelJavaEntrySnapshot snapshot)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = snapshot.Title,
            Info = snapshot.Info,
            Icon = "mdi-language-java",
            Checked = snapshot.IsSelected,
            Opacity = snapshot.Opacity
        };
        item.Check += async (_, args) =>
        {
            if (!snapshot.IsAvailable)
            {
                args.Handled = true;
                item.Checked = false;
                _showHint(_messages.UnavailableMessage, HintType.Critical);
                return;
            }

            if (!snapshot.IsEnabled)
            {
                args.Handled = true;
                item.Checked = false;
                _showHint(_messages.DisabledSelectionMessage, HintType.Info);
                return;
            }

            await _selectDefaultJava(snapshot);
        };

        var openButton = CreateIconButton("mdi-folder-open-outline", _messages.OpenFolderTooltip);
        openButton.IsEnabled = snapshot.IsAvailable;
        openButton.Click += (_, _) => _openFolder(snapshot.Folder);
        item.AddButton(openButton);

        var infoButton = CreateIconButton("mdi-information-outline", _messages.InfoTooltip);
        infoButton.IsEnabled = snapshot.IsAvailable;
        infoButton.Click += (_, _) => _showInfo(snapshot);
        item.AddButton(infoButton);

        var enableButton = CreateIconButton(
            snapshot.IsEnabled ? "mdi-eye-off-outline" : "mdi-eye-outline",
            snapshot.IsEnabled ? _messages.DisableTooltip : _messages.EnableTooltip);
        enableButton.IsEnabled = snapshot.IsAvailable;
        enableButton.Click += async (_, _) => await _toggleJava(snapshot);
        item.AddButton(enableButton);

        return item;
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

    private static MyIconButton CreateIconButton(string icon, string tip)
    {
        var button = new MyIconButton
        {
            Icon = icon,
            IconSize = 12,
            Width = 26,
            Height = 26
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static Control CreateInfoBlock(string title, string? description)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Foreground = ThemeBrushes.Text
                },
                CreateSubText(description ?? string.Empty)
            }
        };
    }

    private static TextBlock CreateSubText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextSecondary,
            FontSize = 12,
            LineHeight = 18
        };
    }
}
