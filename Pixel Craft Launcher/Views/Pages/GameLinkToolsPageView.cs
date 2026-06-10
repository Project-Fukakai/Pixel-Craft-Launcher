using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Core.App.Pixel.ViewModels;
using Pixel_Craft_Launcher.Controls;

namespace Pixel_Craft_Launcher.Views.Pages;

public sealed class GameLinkToolsPageView : UserControl
{
    public GameLinkToolsPageView(
        IEnumerable<Control> sections,
        PixelGameLinkSidebarSnapshot sidebarSnapshot,
        bool showTestEntry,
        Action openTestEntry)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(24)
        };

        foreach (var section in sections)
            stack.Children.Add(section);

        if (showTestEntry)
        {
            var previewButton = new MyButton
            {
                Text = sidebarSnapshot.TestEntryTitle,
                PrependIcon = sidebarSnapshot.TestEntryIcon,
                Variant = MyButtonVariant.Flat
            };
            previewButton.Click += (_, _) => openTestEntry();
            stack.Children.Add(new MyCard
            {
                Title = sidebarSnapshot.TestEntryCardTitle,
                CanSwap = false,
                CardContent = new WrapPanel { Children = { previewButton } }
            });
        }

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }
}
