using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Pixel.Slices.Profiles;
using PCL.Core.UI.Theme;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class ProfileManagerLeftPageView : UserControl
{
    public ProfileManagerLeftPageView(
        PixelProfileManagerSidebarSnapshot snapshot,
        Action addMicrosoftProfile,
        Action addOfflineProfile,
        Action<string> addAuthlibProfile,
        Action addAuthServer)
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

        AddProfileAction(stack, snapshot.AddMicrosoft, addMicrosoftProfile);
        AddProfileAction(stack, snapshot.AddOffline, addOfflineProfile);

        stack.Children.Add(CreateProfileSidebarSubtitle(snapshot.AuthServerSectionTitle));
        foreach (var server in snapshot.AuthServers)
        {
            var item = new MyListItem
            {
                Title = server.Name,
                Info = server.ApiRoot,
                Icon = server.Icon,
                Type = MyListItem.CheckType.Clickable,
                IsSidebarItem = true
            };
            item.Click += (_, _) => addAuthlibProfile(server.Id);
            stack.Children.Add(item);
        }

        root.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        });

        var bottom = new MyButton
        {
            Text = snapshot.AddAuthServerButtonText,
            PrependIcon = "mdi-server-plus",
            Variant = MyButtonVariant.Tonal,
            Margin = new Thickness(14, 10, 14, 16),
            IsBlock = true
        };
        bottom.Click += (_, _) => addAuthServer();
        Grid.SetRow(bottom, 1);
        root.Children.Add(bottom);

        Content = root;
    }

    private static void AddProfileAction(StackPanel stack, PixelProfileManagerSidebarActionSnapshot action, Action onClick)
    {
        var item = new MyListItem
        {
            Title = action.Title,
            Info = action.Info,
            Icon = action.Icon,
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
}
