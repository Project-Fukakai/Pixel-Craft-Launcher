using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Pixel;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.MyMsg;

namespace Pixel_Craft_Launcher.Views.Setup;

public sealed class AboutPageView : UserControl
{
    private readonly Action<string?> _openExternalUrl;
    private readonly Action<PixelSettingSectionKind> _navigateSetupShortcut;

    public AboutPageView(
        PixelAboutPageSnapshot snapshot,
        Action<string?> openExternalUrl,
        Action<PixelSettingSectionKind> navigateSetupShortcut)
    {
        _openExternalUrl = openExternalUrl;
        _navigateSetupShortcut = navigateSetupShortcut;

        var title = new TextBlock
        {
            FontSize = 30,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        title.Inlines!.Add(new Run(snapshot.BrandPrimaryText) { Foreground = ThemeBrushes.PrimaryHover });
        title.Inlines.Add(new Run(snapshot.BrandRestText) { Foreground = ThemeBrushes.Text });

        var version = new TextBlock
        {
            Text = snapshot.GetVersionLabel(),
            FontSize = 13,
            Foreground = ThemeBrushes.TextSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        Content = new MyScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 22,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(24, 28, 24, 40),
                Children =
                {
                    BuildLogoMark(),
                    title,
                    version,
                    BuildIntroduction(snapshot),
                    BuildRepositoryActions(snapshot),
                    BuildLicenseList(snapshot),
                    BuildPageShortcuts(snapshot)
                }
            }
        };
    }

    private static Grid BuildLogoMark()
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

        return new Grid
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
    }

    private static TextBlock BuildIntroduction(PixelAboutPageSnapshot snapshot)
    {
        return new TextBlock
        {
            Text = snapshot.IntroductionText,
            FontSize = 14,
            Foreground = ThemeBrushes.Text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8, 0),
            LineHeight = 23
        };
    }

    private Control BuildRepositoryActions(PixelAboutPageSnapshot snapshot)
    {
        return BuildSection(snapshot.RepositorySectionTitle, new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                CreateLinkText(snapshot.RepositoryUrl),
                new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        CreateActionButton(snapshot.OpenRepositoryButtonText, "mdi-github", () => _openExternalUrl(snapshot.RepositoryUrl)),
                        CreateActionButton(snapshot.FeedbackButtonText, "mdi-bug-outline", () => _openExternalUrl(snapshot.RepositoryUrl + "/issues"))
                    }
                }
            }
        });
    }

    private Control BuildLicenseList(PixelAboutPageSnapshot snapshot)
    {
        var list = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        if (!snapshot.HasLicenses)
        {
            list.Children.Add(CreateMutedText(snapshot.EmptyLicensesText));
        }
        else
        {
            foreach (var license in snapshot.Licenses)
                list.Children.Add(BuildLicenseItem(snapshot, license));
        }

        return BuildSection(snapshot.LicenseSectionTitle, list);
    }

    private Control BuildLicenseItem(PixelAboutPageSnapshot snapshot, PixelAboutLicenseSnapshot license)
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
            Foreground = ThemeBrushes.Text,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        });

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(license.WebsiteUri))
            actions.Children.Add(CreateActionButton(snapshot.LicenseHomeButtonText, "mdi-web", () => _openExternalUrl(license.WebsiteUri)));
        if (!string.IsNullOrWhiteSpace(license.LicenseUri))
            actions.Children.Add(CreateActionButton(snapshot.LicenseTextButtonText, "mdi-file-document-outline", () => _openExternalUrl(license.LicenseUri)));
        row.Children.Add(actions);
        return row;
    }

    private Control BuildPageShortcuts(PixelAboutPageSnapshot snapshot)
    {
        return BuildSection(snapshot.ShortcutsSectionTitle, new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                CreateActionButton(snapshot.UpdateShortcutButtonText, "mdi-update", () => _navigateSetupShortcut(PixelSettingSectionKind.Update)),
                CreateActionButton(snapshot.FeedbackShortcutButtonText, "mdi-message-alert-outline", () => _navigateSetupShortcut(PixelSettingSectionKind.Feedback)),
                CreateActionButton(snapshot.LogShortcutButtonText, "mdi-text-box-outline", () => _navigateSetupShortcut(PixelSettingSectionKind.Log))
            }
        });
    }

    private static Control BuildSection(string title, Control content)
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
                    Foreground = ThemeBrushes.Text,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                content
            }
        };
    }

    private static TextBlock CreateLinkText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = ThemeBrushes.TextSecondary,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8, 0)
        };
    }

    private static TextBlock CreateMutedText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = ThemeBrushes.TextSecondary,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private static MyButton CreateActionButton(string text, string icon, Action action)
    {
        var button = new MyButton
        {
            Text = text,
            Icon = icon,
            Variant = MyButtonVariant.Tonal,
            Size = MyButtonSize.Medium,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }
}
