using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ProfileListPageView : UserControl
{
    public ProfileListPageView(
        PixelProfileListPageSnapshot snapshot,
        Func<string, Task> selectProfile,
        Func<PixelProfileItemSnapshot, Task> copyUuid,
        Action<string> editOfflineProfile,
        Func<string, Task> deleteProfile)
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(snapshot.Title, snapshot.Description));

        var list = new StackPanel { Spacing = 8 };
        foreach (var profile in snapshot.Profiles)
        {
            list.Children.Add(BuildProfileItem(
                profile,
                snapshot,
                selectProfile,
                copyUuid,
                editOfflineProfile,
                deleteProfile));
        }

        if (snapshot.IsEmpty)
        {
            list.Children.Add(BuildCard(snapshot.EmptyTitle, new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateBodyText(snapshot.EmptyText)
                }
            }));
        }

        stack.Children.Add(list);
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static Control BuildProfileItem(
        PixelProfileItemSnapshot snapshot,
        PixelProfileListPageSnapshot page,
        Func<string, Task> selectProfile,
        Func<PixelProfileItemSnapshot, Task> copyUuid,
        Action<string> editOfflineProfile,
        Func<string, Task> deleteProfile)
    {
        var item = new MyListItem
        {
            Title = snapshot.Username,
            Info = snapshot.Info,
            Type = MyListItem.CheckType.RadioBox,
            Checked = snapshot.IsSelected,
            Icon = snapshot.Icon
        };
        item.Click += async (_, _) => await selectProfile(snapshot.Id);

        var copy = new MyIconButton { Icon = "mdi-content-copy", Width = 28, Height = 28 };
        ToolTip.SetTip(copy, page.CopyUuidTip);
        copy.Click += async (_, _) => await copyUuid(snapshot);
        item.AddButton(copy);

        if (snapshot.IsOffline)
        {
            var edit = new MyIconButton { Icon = "mdi-pencil", Width = 28, Height = 28 };
            ToolTip.SetTip(edit, page.EditOfflineTip);
            edit.Click += (_, _) => editOfflineProfile(snapshot.Id);
            item.AddButton(edit);
        }

        var delete = new MyIconButton { Icon = "mdi-delete-outline", Width = 28, Height = 28 };
        ToolTip.SetTip(delete, page.DeleteTip);
        delete.Click += async (_, _) => await deleteProfile(snapshot.Id);
        item.AddButton(delete);
        return item;
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
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = ThemeBrushes.Text
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
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = ThemeBrushes.Text,
            LineHeight = 22
        };
    }
}
