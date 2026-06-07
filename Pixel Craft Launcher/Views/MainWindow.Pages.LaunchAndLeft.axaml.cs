using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Pixel;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using Pixel_Craft_Launcher.Controls;
using Pixel_Craft_Launcher.Controls.Behaviors;
using Pixel_Craft_Launcher.Controls.MyMsg;
using Pixel_Craft_Launcher.Modules.Base;
using Pixel_Craft_Launcher.Routing;
using Pixel_Craft_Launcher.Settings;
using Pixel_Craft_Launcher.ViewModels;
using IOPath = System.IO.Path;

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildLeftPage(MainPageKind page)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(14, 16),
            Spacing = 10
        };

        stack.Children.Add(new TextBlock
        {
            Text = GetPageTitle(page),
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = BodyForeground,
            Margin = new Thickness(2, 0, 2, 8)
        });

        foreach (var item in GetLeftItems(page))
        {
            stack.Children.Add(new MyListItem
            {
                Title = item.title,
                Info = item.info,
                Type = MyListItem.CheckType.RadioBox,
                Checked = item.active,
                Icon = item.icon,
                IsSidebarItem = true
            });
        }

        return stack;
    }

    private Control BuildDownloadLeftPage()
    {
        if (IsDownloadSecondaryPage())
            return IsDownloadTaskRoute() ? BuildDownloadManagerLeftPage() : BuildDownloadInstallLeftPage();

        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        AddDownloadItem(stack, "Minecraft", 1, "mdi-hammer-screwdriver", canRefresh: true);
        AddDownloadCategory(stack, "社区资源");
        AddDownloadItem(stack, "Mod", 2, "mdi-puzzle-outline", canRefresh: true);
        AddDownloadItem(stack, "整合包", 3, "mdi-package-variant-closed", canRefresh: true);
        AddDownloadItem(stack, "数据包", 4, "mdi-view-grid-outline", canRefresh: true);
        AddDownloadItem(stack, "资源包", 5, "mdi-image-outline", canRefresh: true);
        AddDownloadItem(stack, "光影包", 6, "mdi-white-balance-sunny", canRefresh: true);
        AddDownloadItem(stack, "世界", 7, "mdi-earth", canRefresh: true);
        AddDownloadItem(stack, "收藏夹", 8, "mdi-book-heart-outline", canRefresh: true);
        AddDownloadCategory(stack, "安装包");
        AddDownloadItem(stack, "Minecraft", 9, "mdi-cube-outline", canRefresh: true);
        AddDownloadItem(stack, "OptiFine", 10, "mdi-eye-outline", canRefresh: true);
        AddDownloadItem(stack, "Forge", 11, "mdi-anvil", canRefresh: true);
        AddDownloadItem(stack, "NeoForge", 12, "mdi-anvil", canRefresh: true);
        AddDownloadItem(stack, "Cleanroom", 13, "mdi-flask-outline", canRefresh: true);
        AddDownloadItem(stack, "Fabric", 14, "mdi-feather", canRefresh: true);
        AddDownloadItem(stack, "Legacy Fabric", 18, "mdi-feather", canRefresh: true);
        AddDownloadItem(stack, "Quilt", 15, "mdi-grid-large", canRefresh: true);
        AddDownloadItem(stack, "LabyMod", 17, "mdi-test-tube", canRefresh: true);
        AddDownloadItem(stack, "LiteLoader", 16, "mdi-package-variant", canRefresh: true);

        return BuildScrollableLeftPane(stack);
    }

    private Control BuildDownloadManagerLeftPage()
    {
        var tasks = _downloadViewModel.Tasks.Where(IsVisibleDownloadTask).ToArray();
        var activeTasks = tasks.Where(static task => task.State is PCL.Core.IO.Download.NDlTaskState.Waiting or PCL.Core.IO.Download.NDlTaskState.Running).ToArray();
        var rawProgress = tasks.Length == 0 ? 1d : tasks.Average(static task => Math.Clamp(task.Progress, 0d, 1d));
        var speed = activeTasks.Sum(static task => task.SpeedBytesPerSecond);
        var remainingFiles = activeTasks.Length;
        var runningThreads = activeTasks.Count(static task => task.State == PCL.Core.IO.Download.NDlTaskState.Running);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(0.6, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        AddDownloadManagerStat(root, 1, 2, 3, "总进度", FormatPcl2Progress(rawProgress));
        AddDownloadManagerStat(root, 5, 6, 7, "下载速度", FormatBytes(speed) + "/s");
        AddDownloadManagerStat(root, 9, 10, 11, "剩余文件", remainingFiles.ToString());
        AddDownloadManagerStat(root, 13, 14, 15, "剩余线程", $"{runningThreads} / {_downloadViewModel.MaxDownloadThreads}");
        return root;
    }

    private static void AddDownloadManagerStat(Grid root, int titleRow, int splitRow, int valueRow, string title, string value)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14,
            Foreground = ThemeBrushes.PrimaryHover
        };
        Grid.SetRow(titleBlock, titleRow);
        root.Children.Add(titleBlock);

        var split = new Border
        {
            Height = 2,
            Margin = new Thickness(25, 7),
            Background = ThemeBrushes.Border,
            Opacity = 0.8
        };
        Grid.SetRow(split, splitRow);
        root.Children.Add(split);

        var valueBlock = new TextBlock
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 20,
            Foreground = BodyForeground
        };
        Grid.SetRow(valueBlock, valueRow);
        root.Children.Add(valueBlock);
    }

    private Control BuildDownloadInstallLeftPage()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(14, 14, 14, 10),
            Spacing = 8
        };

        content.Children.Add(CreateInstallSidebarTitle("实例名称"));
        content.Children.Add(BuildInstallInstanceSidebar());
        content.Children.Add(CreateInstallSidebarTitle("安装清单"));
        content.Children.Add(BuildInstallChecklist());

        var scroller = new MyScrollViewer
        {
            Content = content
        };
        var downloadPanel = BuildInstallDownloadSidebar();
        Grid.SetRow(downloadPanel, 1);
        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                scroller,
                downloadPanel
            }
        };
    }

    private Control BuildInstallInstanceSidebar()
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 6) };
        stack.Children.Add(CreateInstallInput("名称", _downloadViewModel.InstanceName, text => _downloadViewModel.InstanceName = text));
        stack.Children.Add(CreateInstallInput("目录", _downloadViewModel.TargetFolder, text => _downloadViewModel.TargetFolder = text));
        return stack;
    }

    private static Control CreateInstallInput(string title, string value, Action<string> onChanged)
    {
        var input = new TextBox
        {
            PlaceholderText = title,
            Text = value,
            Height = 40,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0),
            UseFloatingPlaceholder = false
        };
        input.TextChanged += (_, _) => onChanged(input.Text ?? string.Empty);

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 12,
                    Foreground = SubForeground
                },
                input
            }
        };
    }

    private Control BuildInstallChecklist()
    {
        var list = new StackPanel { Spacing = 2 };
        if (_downloadViewModel.SelectedVersion is { } version)
        {
            list.Children.Add(new MyListItem
            {
                Title = version.Id,
                Info = "Minecraft",
                Icon = GetVersionIcon(version)
            });
        }

        foreach (var (title, info, icon, clear) in GetInstallChecklistItems())
        {
            var item = new MyListItem
            {
                Title = title,
                Info = info,
                Icon = icon
            };
            var delete = new MyIconButton
            {
                Icon = "mdi-close",
                IconSize = 12,
                Padding = new Thickness(4),
                Width = 24,
                Height = 24
            };
            ToolTip.SetTip(delete, "删除");
            delete.Click += (_, _) =>
            {
                clear();
                ScheduleDownloadRightPageRefresh(refreshLeft: true);
            };
            item.AddButton(delete);
            list.Children.Add(item);
        }

        if (list.Children.Count <= 1)
            list.Children.Add(CreateSubText("原版实例"));
        return list;
    }

    private Control BuildInstallDownloadSidebar()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(14, 8, 14, 16),
            Spacing = 8
        };
        if (_downloadViewModel.IsBusy)
            stack.Children.Add(CreateSubText(_downloadViewModel.StatusText));
        var start = CreateInstallDownloadButton();
        start.PointerEntered += (_, _) => AnimateInstallDownloadButton(start, true);
        start.PointerExited += (_, _) => AnimateInstallDownloadButton(start, false);
        start.PointerPressed += (_, e) =>
        {
            if (!_downloadViewModel.CanInstall)
                return;
            SetInstallDownloadButtonPressed(start, true);
            e.Handled = true;
        };
        start.PointerReleased += (_, e) =>
        {
            if (!_downloadViewModel.CanInstall)
                return;
            SetInstallDownloadButtonPressed(start, false);
            _ = _downloadViewModel.InstallSelectedAsync();
            e.Handled = true;
        };
        stack.Children.Add(start);
        return stack;
    }

    private Border CreateInstallDownloadButton()
    {
        var canInstall = _downloadViewModel.CanInstall;
        var textBrush = canInstall ? ThemeBrushes.OnPrimary : ThemeBrushes.TextDisabled;
        var button = new Border
        {
            Height = 54,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(18, 0),
            Background = new SolidColorBrush(canInstall ? ThemeBrushes.PrimaryColor : ThemeBrushes.TextDisabledColor),
            BorderBrush = new SolidColorBrush(canInstall ? ThemeBrushes.PrimaryColor : ThemeBrushes.TextDisabledColor),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            Opacity = canInstall ? 1 : 0.55,
            Cursor = canInstall ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            BoxShadow = canInstall
                ? new BoxShadows(new BoxShadow { Blur = 12, Spread = 0, OffsetY = 3, Color = Color.FromArgb(42, ThemeBrushes.PrimaryColor.R, ThemeBrushes.PrimaryColor.G, ThemeBrushes.PrimaryColor.B) })
                : default,
            RenderTransform = new TranslateTransform(),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Avalonia.Controls.Shapes.Path
                    {
                        Width = 21,
                        Height = 21,
                        Stretch = Stretch.Uniform,
                        Fill = textBrush,
                        Data = MaterialIconGeometry.TryGet(_downloadViewModel.IsBusy ? "mdi-progress-download" : "mdi-download")
                    },
                    new TextBlock
                    {
                        Text = _downloadViewModel.IsBusy ? "处理中" : "开始下载",
                        Foreground = textBrush,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 15,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        return button;
    }

    private static void AnimateInstallDownloadButton(Border button, bool hover)
    {
        if (button.Opacity < 0.7)
            return;

        var baseColor = ThemeBrushes.PrimaryColor;
        var hoverColor = AdjustFilledHoverColor(baseColor);
        var shadowColor = hover
            ? Color.FromArgb(72, hoverColor.R, hoverColor.G, hoverColor.B)
            : Color.FromArgb(42, baseColor.R, baseColor.G, baseColor.B);
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaColor(button, Border.BackgroundProperty, new SolidColorBrush(hover ? hoverColor : baseColor), 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaColor(button, Border.BorderBrushProperty, new SolidColorBrush(hover ? hoverColor : baseColor), 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() => button.BoxShadow = new BoxShadows(new BoxShadow { Blur = hover ? 18 : 12, Spread = 0, OffsetY = hover ? 5 : 3, Color = shadowColor }))
        }, $"Install Download Button {button.GetHashCode()}");
    }

    private static void SetInstallDownloadButtonPressed(Border button, bool pressed)
    {
        button.Opacity = pressed ? 0.92 : 1;
        if (button.RenderTransform is TranslateTransform translate)
        {
            ModAnimation.AniStart(ModAnimation.AaTranslateY(translate, (pressed ? 1 : 0) - translate.Y, pressed ? 80 : 140,
                ease: new ModAnimation.AniEaseOutFluent()), $"Install Download Button Press {button.GetHashCode()}");
        }
    }

    private static Color AdjustFilledHoverColor(Color color)
    {
        var amount = ThemeService.IsDarkMode ? 0.24 : -0.18;
        static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
        byte Adjust(byte channel) => amount >= 0
            ? Clamp(channel + (255 - channel) * amount)
            : Clamp(channel * (1 + amount));
        return Color.FromArgb(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
    }

    private static TextBlock CreateInstallSidebarTitle(string title)
    {
        return new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = BodyForeground,
            Margin = new Thickness(0, 2, 0, 0)
        };
    }

    private static void AddDownloadCategory(StackPanel stack, string title)
    {
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(14, 14, 8, 4),
            Opacity = 0.6,
            FontSize = 12,
            Foreground = BodyForeground
        });
    }

    private void AddDownloadItem(StackPanel stack, string title, int tag, string icon, bool canRefresh = false)
    {
        var item = new MyListItem
        {
            Title = title,
            Type = MyListItem.CheckType.RadioBox,
            Checked = _selectedDownloadPage == tag,
            Icon = icon,
            Height = 36,
            LogoSize = 18,
            ContentPadding = new Thickness(8, 0, 8, 0),
            IsSidebarItem = true,
            Tag = tag
        };
        item.Check += (_, _) =>
        {
            if (_downloadViewModel.IsInstallSelectionOpen)
                CloseInstallSelectionWithoutRefresh();
            _shellViewModel.NavigateDownloadCategory(tag);
        };

        if (!canRefresh)
        {
            stack.Children.Add(item);
            return;
        }

        var refreshButton = new MyIconButton
        {
            Icon = "mdi-refresh",
            IconSize = 10.8,
            Padding = new Thickness(4),
            Width = 22,
            Height = 22,
            Tag = tag
        };
        ToolTip.SetTip(refreshButton, "刷新");
        refreshButton.Click += (_, _) =>
        {
            RefreshDownloadCategory(tag);
        };
        item.AddButton(refreshButton);
        stack.Children.Add(item);
    }

    private void RefreshDownloadCategory(int tag)
    {
        if (tag == 1 || tag == 9)
        {
            _ = _downloadViewModel.RefreshVersionsAsync();
        }
        else if (tag is >= 10 and <= 18)
        {
            if (_downloadViewModel.SelectedVersion is not null)
                _ = _downloadViewModel.RefreshLoaderChoicesAsync();
            else
                _ = _downloadViewModel.RefreshVersionsAsync();
        }

        ShowHint("正在刷新……", HintType.Info);
    }

    private Control BuildSetupLeftPage()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        AddSetupCategory(stack, "游戏");
        AddSetupItem(stack, PixelSettingSectionKind.Launch);
        AddSetupItem(stack, PixelSettingSectionKind.Java);
        AddSetupItem(stack, PixelSettingSectionKind.GameManage);
        AddSetupCategory(stack, "工具");
        AddSetupItem(stack, PixelSettingSectionKind.GameLink);
        AddSetupCategory(stack, "启动器");
        AddSetupItem(stack, PixelSettingSectionKind.Ui);
        AddSetupItem(stack, PixelSettingSectionKind.LauncherMisc);
        AddSetupItem(stack, PixelSettingSectionKind.About);
        AddSetupItem(stack, PixelSettingSectionKind.Update);
        AddSetupItem(stack, PixelSettingSectionKind.Feedback);
        AddSetupItem(stack, PixelSettingSectionKind.Log);

        return BuildScrollableLeftPane(stack);
    }

    private static void AddSetupCategory(StackPanel stack, string title)
    {
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(13, 10, 5, 4),
            Opacity = 0.6,
            FontSize = 12,
            Foreground = BodyForeground
        });
    }

    private void AddSetupItem(StackPanel stack, PixelSettingSectionKind kind)
    {
        var section = PixelSettingsCatalog.Get(kind);
        if (!FeatureVisibilityService.IsSetupSectionVisible(kind))
            return;

        var item = new MyListItem
        {
            Title = section.Title,
            Info = section.Info,
            Type = MyListItem.CheckType.RadioBox,
            Checked = _selectedSetupSection == kind,
            Icon = section.Icon,
            Height = 36,
            IsSidebarItem = true,
            Tag = kind
        };
        item.Check += (_, _) =>
        {
            PixelSettingsBinder.SetValue("PixelSetupSelectedSection", kind.ToString());
            _shellViewModel.NavigateSetupSection(kind);
        };

        var resetButton = new MyIconButton
        {
            Icon = "mdi-restore",
            IconSize = 11,
            Padding = new Thickness(4),
            Width = 22,
            Height = 22,
            Tag = kind
        };
        ToolTip.SetTip(resetButton, "初始化本页设置");
        resetButton.Click += (_, _) =>
        {
            PixelSettingsBinder.ResetSection(section);
            ShowHint($"{section.Title} 设置已初始化。", HintType.Finish);
            SetPageHostContent(LeftContentHost, BuildSetupLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildSetupRightPage(), PageHostUpdateMode.SilentRefresh);
        };
        item.AddButton(resetButton);
        stack.Children.Add(item);
    }

    private static Control BuildScrollableLeftPane(Control content)
    {
        return new LeftPaneScrollHost
        {
            Children = { content }
        };
    }

    private static Control BuildScrollableMainPane(Control content)
    {
        return new MainPaneScrollHost
        {
            Children = { content }
        };
    }

    private Control BuildLaunchLeftPage()
    {
        if (IsLaunchInstanceRoute())
            return BuildLaunchInstanceLeftPage();

        var root = new Grid
        {
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(1, 1)
        };
        var launchInputPanel = BuildLaunchInputPanel();
        root.Children.Add(launchInputPanel);
        root.Children.Add(BuildLaunchingPanel());
        return root;
    }

    private Control BuildLaunchInputPanel()
    {
        var grid = new Grid
        {
            IsVisible = !_launchViewModel.IsLaunching,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(20))
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(20)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(10))
            },
            RenderTransform = new ScaleTransform()
        };

        var loginPanel = BuildLaunchAccountPanel();
        Grid.SetRow(loginPanel, 1);
        Grid.SetColumnSpan(loginPanel, 5);
        grid.Children.Add(loginPanel);

        var launchPanel = BuildLaunchMainButtonPanel();
        Grid.SetRow(launchPanel, 2);
        Grid.SetColumnSpan(launchPanel, 5);
        grid.Children.Add(launchPanel);

        var secondaryActions = new Grid
        {
            Margin = new Thickness(20, 10, 20, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        Grid.SetRow(secondaryActions, 3);
        Grid.SetColumnSpan(secondaryActions, 5);
        grid.Children.Add(secondaryActions);

        var instanceButton = new MyButton
        {
            Text = "实例选择",
            Variant = MyButtonVariant.Text,
            Height = 35,
            IsBlock = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !_launchViewModel.IsLaunching,
            IsVisible = FeatureVisibilityService.IsFunctionVisible(PixelFunctionFeature.Select)
        };
        instanceButton.Click += (_, _) => _shellViewModel.Navigate(PixelRoutes.LaunchInstances());
        secondaryActions.Children.Add(instanceButton);

        var moreButton = new MyButton
        {
            Text = "实例设置",
            Variant = MyButtonVariant.Text,
            Height = 35,
            IsBlock = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = _launchViewModel.SelectedInstance is not null,
            IsVisible = FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Edit)
        };
        moreButton.Click += (_, _) => ShowMessage("实例设置", "实例设置页面将在后续迁移到这里。");
        Grid.SetColumn(moreButton, 1);
        secondaryActions.Children.Add(moreButton);

        return grid;
    }

    private Control BuildLaunchMainButtonPanel()
    {
        var canStart = _launchViewModel.Instances.Count == 0 || _launchViewModel.CanLaunch;
        var root = new Grid
        {
            Margin = new Thickness(20, 0),
            Height = 68
        };
        var textTranslate = new TranslateTransform();
        var arrowTranslate = new TranslateTransform();
        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = textTranslate,
            Children =
            {
                new TextBlock
                {
                    Text = _launchViewModel.LaunchButtonText,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = ThemeBrushes.OnPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = _launchViewModel.SelectedInstanceName,
                    FontSize = 12,
                    Foreground = ThemeBrushes.OnPrimary,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };
        ((TextBlock)textStack.Children[0]).Bind(TextBlock.TextProperty, new Binding("Launch.LaunchButtonText"));
        ((TextBlock)textStack.Children[1]).Bind(TextBlock.TextProperty, new Binding("Launch.SelectedInstanceName"));

        var arrow = new Avalonia.Controls.Shapes.Path
        {
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform,
            Fill = ThemeBrushes.OnPrimary,
            Data = MaterialIconGeometry.TryGet("mdi-arrow-right"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = arrowTranslate
        };

        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(2, 0)
        };
        content.Children.Add(textStack);
        Grid.SetColumn(arrow, 1);
        content.Children.Add(arrow);

        var launchButton = new MyButton
        {
            Variant = MyButtonVariant.Flat,
            Height = 66,
            Margin = new Thickness(0),
            Padding = new Thickness(26, 0, 18, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = canStart,
            Content = content
        };
        AttachLaunchButtonMotion(launchButton, textTranslate, arrowTranslate);
        launchButton.Click += (_, _) =>
        {
            if (_launchViewModel.Instances.Count == 0)
            {
                SwitchMainPage(MainPageKind.Download);
                return;
            }

            _launchViewModel.StartLaunch();
        };
        root.Children.Add(launchButton);

        return root;
    }

    private static void AttachLaunchButtonMotion(MyButton button, TranslateTransform text, TranslateTransform arrow)
    {
        const string animationName = "Launch Main Button Motion";

        void Animate(double textX, double arrowX, int time)
        {
            ModAnimation.AniStop(animationName);
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaTranslateX(text, textX - text.X, time, ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaTranslateX(arrow, arrowX - arrow.X, time, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
            }, animationName, true);
        }

        button.PointerEntered += (_, _) =>
        {
            if (button.IsEnabled)
                Animate(2, 5, 180);
        };
        button.PointerExited += (_, _) => Animate(0, 0, 160);
        button.PointerPressed += (_, _) =>
        {
            if (button.IsEnabled)
                Animate(4, 10, 90);
        };
        button.PointerReleased += (_, _) =>
        {
            if (button.IsEnabled && button.IsPointerOver)
                Animate(2, 5, 150);
            else
                Animate(0, 0, 150);
        };
    }

    private static (Border Panel, Grid Content) CreateLaunchGlowPanel(double height = 72, bool enableHover = true)
    {
        var content = new Grid();
        var glowBack = CreateLaunchGlowLayer(height, 0.36, 1);
        var glowCore = CreateLaunchGlowLayer(height, 0.58, 0.78);
        var glowEdge = CreateLaunchGlowLayer(height, 0.95, 0.34);
        content.Children.Add(glowBack);
        content.Children.Add(glowCore);
        content.Children.Add(glowEdge);

        var panel = new Border
        {
            Height = height,
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = content
        };
        SetLaunchGlow(height, glowBack, glowCore, glowEdge, false, animate: false);
        if (enableHover)
        {
            panel.PointerEntered += (_, _) => SetLaunchGlow(height, glowBack, glowCore, glowEdge, true, animate: true);
            panel.PointerExited += (_, _) => SetLaunchGlow(height, glowBack, glowCore, glowEdge, false, animate: true);
        }
        return (panel, content);
    }

    private static Border CreateLaunchGlowLayer(double baseHeight, double opacity, double heightRatio)
    {
        return new Border
        {
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = opacity,
            Height = baseHeight * heightRatio
        };
    }

    private static void SetLaunchGlow(double baseHeight, Border glowBack, Border glowCore, Border glowEdge, bool hover, bool animate)
    {
        var primary = ThemeBrushes.PrimaryColor;
        glowBack.Background = CreateLaunchGlowBrush(primary, hover ? (byte)125 : (byte)92, hover ? 0.04 : 0.1);
        glowCore.Background = CreateLaunchGlowBrush(primary, hover ? (byte)190 : (byte)150, hover ? 0.16 : 0.24);
        glowEdge.Background = CreateLaunchGlowBrush(primary, hover ? (byte)255 : (byte)225, hover ? 0.42 : 0.52);

        var backOpacity = hover ? 0.7 : 0.42;
        var coreOpacity = hover ? 0.88 : 0.58;
        var edgeOpacity = hover ? 1 : 0.92;
        var backHeight = hover ? baseHeight : baseHeight * 0.94;
        var coreHeight = hover ? baseHeight * 0.92 : baseHeight * 0.78;
        var edgeHeight = hover ? baseHeight * 0.44 : baseHeight * 0.33;

        if (!animate)
        {
            glowBack.Opacity = backOpacity;
            glowCore.Opacity = coreOpacity;
            glowEdge.Opacity = edgeOpacity;
            glowBack.Height = backHeight;
            glowCore.Height = coreHeight;
            glowEdge.Height = edgeHeight;
            return;
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(glowBack, backOpacity - glowBack.Opacity, 220, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(glowCore, coreOpacity - glowCore.Opacity, 220, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(glowEdge, edgeOpacity - glowEdge.Opacity, 220, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(glowBack, backHeight - glowBack.Height, 260, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(glowCore, coreHeight - glowCore.Height, 260, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(glowEdge, edgeHeight - glowEdge.Height, 260, ease: new ModAnimation.AniEaseOutFluent())
        }, $"Launch Glow {glowBack.GetHashCode()}", true);
    }

    private static LinearGradientBrush CreateLaunchGlowBrush(Color primary, byte alpha, double clearStop)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Transparent, clearStop),
                new GradientStop(Color.FromArgb((byte)(alpha * 0.28), primary.R, primary.G, primary.B), 0.5),
                new GradientStop(Color.FromArgb((byte)(alpha * 0.62), primary.R, primary.G, primary.B), 0.78),
                new GradientStop(Color.FromArgb(alpha, primary.R, primary.G, primary.B), 1)
            }
        };
    }

    private Control BuildLaunchInstanceLeftPage()
    {
        EnsureLaunchSelectedFolder();
        var stack = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        AddDownloadCategory(stack, "文件夹列表");
        foreach (var folder in _instanceViewModel.Folders)
        {
            var item = new MyListItem
            {
                Title = GetLaunchFolderTitle(folder),
                Info = folder.Path,
                Icon = folder.IsDefault ? "mdi-folder-home-outline" : "mdi-folder-outline",
                Type = MyListItem.CheckType.RadioBox,
                Checked = IsLaunchFolderSelected(folder.Path),
                IsSidebarItem = true
            };
            AddLaunchFolderActionButton(item, folder);
            item.Check += (_, _) =>
            {
                SelectLaunchFolder(folder.Path);
                SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
            };
            stack.Children.Add(item);
        }

        AddDownloadCategory(stack, "管理");
        var add = new MyListItem
        {
            Title = "添加或导入",
            Info = "选择已有 .minecraft 文件夹",
            Icon = "mdi-folder-plus-outline",
            Type = MyListItem.CheckType.Clickable,
            IsSidebarItem = true
        };
        add.Click += (_, _) => _ = AddOrImportLaunchFolderAsync();
        stack.Children.Add(add);
        return BuildScrollableLeftPane(stack);
    }

    private void AddLaunchFolderActionButton(MyListItem item, MinecraftFolderInfo folder)
    {
        var action = new MyIconButton
        {
            Icon = "mdi-cog",
            IconSize = 12,
            Padding = new Thickness(4),
            Width = 24,
            Height = 24
        };
        ToolTip.SetTip(action, "文件夹操作");
        action.Click += (_, e) =>
        {
            e.Handled = true;
            ShowLaunchFolderActionMenu(action, folder);
        };
        item.AddButton(action);
    }

    private void ShowLaunchFolderActionMenu(Control anchor, MinecraftFolderInfo folder)
    {
        HidePopupOverlay();

        var stack = new StackPanel
        {
            MinWidth = 108,
            Spacing = 1
        };
        stack.Children.Add(BuildOverlayMenuButton("打开", "mdi-folder-open-outline", () => OpenFolder(folder.Path)));
        stack.Children.Add(BuildOverlayMenuButton("刷新", "mdi-refresh", () =>
        {
            RefreshLaunchInstancesSuppressed();
            SetPageHostContent(LeftContentHost, BuildLaunchInstanceLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
        }));

        var menu = new Border
        {
            Background = ThemeBrushes.Surface,
            BorderBrush = ThemeBrushes.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Child = stack
        };
        PanPopupOverlay.Children.Add(menu);
        PanPopupOverlay.IsHitTestVisible = true;

        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var point = anchor.TranslatePoint(new Point(anchor.Bounds.Width, anchor.Bounds.Height), PanPopupOverlay) ?? default;
        var overlayWidth = PanPopupOverlay.Bounds.Width > 0 ? PanPopupOverlay.Bounds.Width : Bounds.Width;
        var overlayHeight = PanPopupOverlay.Bounds.Height > 0 ? PanPopupOverlay.Bounds.Height : Bounds.Height;
        var left = Math.Clamp(point.X, 8, Math.Max(8, overlayWidth - menu.DesiredSize.Width - 8));
        var top = Math.Clamp(point.Y, 8, Math.Max(8, overlayHeight - menu.DesiredSize.Height - 8));
        Canvas.SetLeft(menu, left);
        Canvas.SetTop(menu, top);
    }

    private Button BuildOverlayMenuButton(string text, string icon, Action action)
    {
        var iconPath = new Avalonia.Controls.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            Data = MaterialIconGeometry.TryGet(icon),
            Fill = ThemeBrushes.Text
        };
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = ThemeBrushes.Text,
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(iconPath);
        content.Children.Add(label);

        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 3),
            MinWidth = 100,
            Height = 30
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            HidePopupOverlay();
            action();
        };
        return button;
    }

    private void HidePopupOverlay()
    {
        PanPopupOverlay.Children.Clear();
        PanPopupOverlay.IsHitTestVisible = false;
    }

    private static void TriggerLeftPageShowAnimation(Control root, MainPageKind page)
    {
        if (page == MainPageKind.Launch && root.RenderTransform is ScaleTransform)
        {
            AnimateLaunchLeftScale(root);
            return;
        }

        AnimateSidebarItems(root);
    }

    private static void AnimateLaunchLeftScale(Control root)
    {
        if (root.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(0.96, 0.96);
            root.RenderTransform = scale;
        }

        root.Opacity = 0;
        scale.ScaleX = 0.96;
        scale.ScaleY = 0.96;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaScaleTransform(scale, 1, 400, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Strong), absolute: true),
            ModAnimation.AaOpacity(root, 1, 100),
            ModAnimation.AaCode(() =>
            {
                root.Opacity = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, 420)
        }, "PageLeft LaunchScale", true);
    }

    private static void AnimateSidebarItems(Control root)
    {
        var controls = GetSidebarAnimControls(root).ToArray();
        var animations = new List<ModAnimation.AniData>();
        var delay = 0;
        var index = 0;
        foreach (var control in controls)
        {
            var translate = new TranslateTransform(-25, 0);
            control.RenderTransform = translate;
            control.Opacity = 0;
            animations.Add(ModAnimation.AaOpacity(control, control is TextBlock ? 0.6 : 1, 100, delay,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaTranslateX(translate, 5, 200, delay, new ModAnimation.AniEaseOutFluent()));
            animations.Add(ModAnimation.AaTranslateX(translate, 20, 300, delay, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            delay += Math.Max(15 - index, 7) * 2;
            index++;
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (var control in controls)
            {
                control.Opacity = control is TextBlock ? 0.6 : 1;
                if (control.RenderTransform is TranslateTransform translate)
                    translate.X = 0;
            }
        }, delay + 320));

        if (animations.Count > 0)
            ModAnimation.AniStart(animations, "PageLeft MenuItems", true);
    }

    private static IEnumerable<Control> GetSidebarAnimControls(Control root)
    {
        if (root is MyListItem or MyTextBox or TextBlock)
        {
            yield return root;
            yield break;
        }

        if (root is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
            {
                if (!child.IsVisible) continue;
                foreach (var nested in GetSidebarAnimControls(child))
                    yield return nested;
            }
        }
        else if (root is ContentControl { Content: Control content })
        {
            foreach (var nested in GetSidebarAnimControls(content))
                yield return nested;
        }
    }

    private Control BuildLaunchAccountPanel()
    {
        var avatar = new MySkinHead
        {
            Size = 84,
            SkinName = _launchViewModel.OfflineSkinName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        avatar.Bind(MySkinHead.SkinNameProperty, new Binding("Launch.OfflineSkinName"));

        var name = new TextBlock
        {
            Text = _launchViewModel.SelectedProfile,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 250
        };
        name.Bind(TextBlock.TextProperty, new Binding("Launch.SelectedProfile"));

        var method = new TextBlock
        {
            Text = _launchViewModel.SelectedProfileMethod,
            FontSize = 13,
            Foreground = ThemeBrushes.TextSecondary,
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        method.Bind(TextBlock.TextProperty, new Binding("Launch.SelectedProfileMethod"));

        return new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 0),
            Children =
            {
                avatar,
                name,
                method,
                new MyButton
                {
                    Text = "档案管理",
                    PrependIcon = "mdi-account-cog-outline",
                    Variant = MyButtonVariant.Text,
                    Size = MyButtonSize.Small,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                }.WithClick((_, _) => _shellViewModel.NavigateProfileManager())
            }
        };
    }

    private Control BuildLaunchingPanel()
    {
        var root = new Grid
        {
            IsVisible = _launchViewModel.IsLaunching,
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransform = new ScaleTransform(0.8, 0.8),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.AttachedToVisualTree += (_, _) => AnimateLaunchStatePanelIn(root);

        var loading = new MyLoading
        {
            Text = "",
            ShowProgress = false,
            Height = 50,
            Margin = new Thickness(0, 12, 0, 12),
            State = _launchViewModel.LoadingState
        };
        var progressValue = Math.Clamp(_launchViewModel.LaunchProgress, 0, 1);

        var progress = new Grid
        {
            Height = 4,
            Margin = new Thickness(30, 12, 30, 27),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(Math.Max(progressValue, 0.001), GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(1 - progressValue, 0.001), GridUnitType.Star))
            },
            Children =
            {
                new Rectangle { Fill = ThemeBrushes.Primary },
                new Rectangle { Fill = ThemeBrushes.Border, Opacity = 0.6 }
            }
        };
        Grid.SetColumn(progress.Children[1], 1);
        AttachLaunchProgressMonitor(progress, progress.ColumnDefinitions[0], progress.ColumnDefinitions[1]);

        var info = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(15)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        AddLaunchInfoRow(info, 0, "当前步骤", _launchViewModel.Stage, "Launch.Stage");
        AddLaunchInfoRow(info, 1, "验证方式", _launchViewModel.SelectedProfileMethod, "Launch.SelectedProfileMethod");
        AddLaunchInfoRow(info, 2, "启动进度", _launchViewModel.LaunchProgressText, "Launch.LaunchProgressText");

        var launchTitle = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 20,
            Foreground = ThemeBrushes.Primary,
            Margin = new Thickness(15, 12, 15, 0),
            RenderTransform = new SkewTransform(-3, 0)
        };
        launchTitle.Bind(TextBlock.TextProperty, new Binding("Launch.LaunchTitleText"));

        var stack = new StackPanel
        {
            Margin = new Thickness(0, -7, 0, 0),
            Spacing = 0,
            Children =
            {
                loading,
                launchTitle,
                new TextBlock
                {
                    Text = _launchViewModel.SelectedInstanceName,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13.5,
                    Foreground = ThemeBrushes.Primary,
                    Margin = new Thickness(40, 5, 40, 0),
                    RenderTransform = new SkewTransform(-3, 0)
                },
                progress,
                info
            }
        };
        Grid.SetRow(stack, 1);
        root.Children.Add(stack);

        var cancelButton = new MyButton
        {
            Text = "取消",
            Height = 35,
            Margin = new Thickness(20, 0, 20, 20),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        cancelButton.Click += (_, _) => _launchViewModel.CancelLaunch();
        Grid.SetRow(cancelButton, 4);
        root.Children.Add(cancelButton);
        return root;
    }

    private static void AnimateLaunchStatePanelIn(Control root)
    {
        if (root.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(0.8, 0.8);
            root.RenderTransform = scale;
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(root, 1 - root.Opacity, 150, 100),
            ModAnimation.AaScaleTransform(scale, 1, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), absolute: true),
            ModAnimation.AaCode(() =>
            {
                root.Opacity = 1;
                root.IsHitTestVisible = true;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, after: true)
        }, "Launch State Page", true);
    }

    private void AttachLaunchProgressMonitor(Control owner, ColumnDefinition finished, ColumnDefinition unfinished)
    {
        var shownProgress = Math.Clamp(_launchViewModel.LaunchProgress, 0, 1);
        var targetProgress = shownProgress;
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, (_, _) =>
        {
            if (targetProgress >= shownProgress)
                shownProgress += (targetProgress - shownProgress) * 0.2d + 0.005d;
            if (targetProgress <= shownProgress || Math.Abs(targetProgress - shownProgress) < 0.002d)
                shownProgress = targetProgress;
            Apply(shownProgress);
            if (Math.Abs(targetProgress - shownProgress) < 0.0001d)
                timer?.Stop();
        });

        void Apply(double value)
        {
            var progress = Math.Clamp(value, 0, 1);
            finished.Width = new GridLength(Math.Max(progress, 0.001), GridUnitType.Star);
            unfinished.Width = new GridLength(Math.Max(1 - progress, 0.001), GridUnitType.Star);
        }

        void OnProgressChanged(ILoadingTrigger _, double progress) =>
            Dispatcher.UIThread.Post(() =>
            {
                targetProgress = Math.Clamp(progress, 0, 1);
                if (timer is { IsEnabled: false })
                    timer.Start();
            }, DispatcherPriority.Render);

        Apply(shownProgress);
        _launchViewModel.LoadingState.ProgressChanged += OnProgressChanged;
        owner.DetachedFromVisualTree += (_, _) =>
        {
            timer?.Stop();
            _launchViewModel.LoadingState.ProgressChanged -= OnProgressChanged;
        };
    }

    private static void AddLaunchInfoRow(Grid grid, int row, string label, string value, string? bindingPath = null)
    {
        var left = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            Opacity = 0.5,
            Foreground = BodyForeground
        };
        var right = new TextBlock
        {
            Text = value,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = BodyForeground
        };
        if (!string.IsNullOrWhiteSpace(bindingPath))
            right.Bind(TextBlock.TextProperty, new Binding(bindingPath));
        Grid.SetRow(left, row);
        Grid.SetColumn(left, 1);
        Grid.SetRow(right, row);
        Grid.SetColumn(right, 3);
        grid.Children.Add(left);
        grid.Children.Add(right);
    }

    private Control BuildLaunchRightPage()
    {
        if (IsLaunchInstanceRoute())
            return BuildLaunchInstanceSelectionPage();

        var stack = new StackPanel
        {
            Margin = new Thickness(25, 25, 25, 10),
            Spacing = 15
        };

        var homepage = BuildCustomHomepageControl();
        if (homepage is not null)
            stack.Children.Add(homepage);

        var launchLog = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 38, 20, 18),
            Foreground = BodyForeground
        };
        launchLog.Bind(TextBlock.TextProperty, new Binding("Launch.LaunchLog"));
        stack.Children.Add(BuildCard("启动日志", launchLog));

        return BuildScrollableMainPane(stack);
    }

    private Control? BuildCustomHomepageControl()
    {
        var type = PixelSettingsBinder.LoadValue("UiCustomType") is int value ? value : 0;
        return type switch
        {
            0 => null,
            1 => BuildLocalHomepage(),
            2 => BuildNetworkHomepage(),
            3 => BuildPresetHomepage(),
            _ => null
        };
    }

    private Control BuildLocalHomepage()
    {
        var path = PixelSettingsBinder.LoadValue("UiCustomNet")?.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return BuildCard("自定义主页", CreateSubText("未设置本地主页文件路径。"));

        path = Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(path))
            return BuildCard("自定义主页", CreateSubText("找不到本地主页文件：" + path));

        var extension = IOPath.GetExtension(path);
        if (IsHomepageImage(extension))
        {
            try
            {
                return BuildCard("自定义主页", new Image
                {
                    Source = new Avalonia.Media.Imaging.Bitmap(path),
                    Stretch = Stretch.Uniform,
                    MaxHeight = 420
                });
            }
            catch (Exception ex)
            {
                return BuildCard("自定义主页", CreateSubText("图片加载失败：" + ex.Message));
            }
        }

        if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return BuildWebHomepage(new Uri(path).AbsoluteUri, "本地主页");
        }

        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            var text = File.ReadAllText(path);
            return BuildCard("自定义主页", new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BodyForeground,
                Margin = new Thickness(20, 36, 20, 18)
            });
        }

        return BuildCard("自定义主页", CreateSubText("暂不支持该本地主页格式：" + extension));
    }

    private Control BuildNetworkHomepage()
    {
        var url = PixelSettingsBinder.LoadValue("UiCustomNet")?.ToString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return BuildCard("联网主页", CreateSubText("请输入有效的 http 或 https 地址。"));
        }

        return BuildWebHomepage(uri.AbsoluteUri, "联网主页");
    }

    private Control BuildPresetHomepage()
    {
        var preset = PixelSettingsBinder.LoadValue("UiCustomPreset") is IConvertible raw ? Convert.ToInt32(raw) : 14;
        var panel = new StackPanel
        {
            Margin = new Thickness(24, 38, 24, 18),
            Spacing = 6,
            Children =
            {
                CreateBodyText("预设主页 " + preset),
                CreateSubText("预设主页框架已接入，后续可将 Plain 的预设内容映射到这里。")
            }
        };
        return BuildCard("自定义主页", panel);
    }

    private Control BuildWebHomepage(string address, string title)
    {
        var webView = CreateNativeWebView(address);
        if (webView is not null)
        {
            webView.Height = 420;
            return BuildCard(title, webView);
        }

        var open = new MyButton
        {
            Text = "打开外部浏览器",
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        open.Click += (_, _) => Basics.OpenPath(address);
        return BuildCard(title, new StackPanel
        {
            Margin = new Thickness(20, 38, 20, 18),
            Spacing = 10,
            Children =
            {
                CreateSubText("当前平台的 WebView 引擎不可用。"),
                open
            }
        });
    }

    private static Control? CreateNativeWebView(string address)
    {
        var type = Type.GetType("Avalonia.Controls.NativeWebView, Avalonia.Controls.WebView", throwOnError: false)
                   ?? Type.GetType("Avalonia.Controls.WebView, Avalonia.Controls.WebView", throwOnError: false);
        if (type is null || Activator.CreateInstance(type) is not Control control)
            return null;

        foreach (var propertyName in new[] { "Source", "Url", "Address" })
        {
            var property = type.GetProperty(propertyName);
            if (property is null || !property.CanWrite)
                continue;

            if (property.PropertyType == typeof(Uri))
                property.SetValue(control, new Uri(address));
            else
                property.SetValue(control, address);
            return control;
        }

        return control;
    }

    private static bool IsHomepageImage(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

    private Control BuildLaunchInstanceSelectionPage()
    {
        var stack = CreatePageStack();

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refresh = new MyButton { Text = "刷新", Height = 34 };
        refresh.Click += (_, _) =>
        {
            RefreshLaunchInstancesSuppressed();
            SetPageHostContent(LeftContentHost, BuildLaunchInstanceLeftPage(), PageHostUpdateMode.SilentRefresh);
            SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
            ShowHint("实例列表已刷新。", HintType.Finish);
        };
        actions.Children.Add(refresh);

        var openRoot = new MyButton
        {
            Text = "打开文件夹",
            Height = 34,
            IsEnabled = _launchViewModel.SelectedInstance is not null
        };
        openRoot.Click += (_, _) => OpenLaunchSelectedInstanceFolder();
        actions.Children.Add(openRoot);
        stack.Children.Add(BuildCard("实例", actions));

        var instances = GetLaunchVisibleInstances();
        if (instances.Count == 0)
        {
            stack.Children.Add(BuildCard("实例列表", CreateSubText("当前文件夹未找到可用实例。安装 Minecraft 后会显示在这里。")));
            return BuildScrollableMainPane(stack);
        }

        foreach (var group in GetLaunchInstanceGroups(instances))
            stack.Children.Add(BuildLaunchInstanceGroupCard(group.title, group.instances));

        return BuildScrollableMainPane(stack);
    }

    private MyCard BuildLaunchInstanceGroupCard(string title, IReadOnlyList<MinecraftInstanceInfo> instances)
    {
        var list = new StackPanel { Spacing = 4 };
        foreach (var instance in instances)
        {
            var item = new MyListItem
            {
                Title = instance.Name,
                Info = BuildLaunchInstanceInfo(instance),
                Icon = GetLaunchInstanceIcon(instance),
                Type = MyListItem.CheckType.RadioBox,
                Checked = string.Equals(instance.VersionDirectory, _launchViewModel.SelectedInstancePath, StringComparison.OrdinalIgnoreCase)
            };
            item.Check += (_, _) =>
            {
                RunLaunchRefreshSuppressed(() => _launchViewModel.SelectInstance(instance));
                RefreshLaunchPage(nameof(PixelLaunchViewModel.SelectedInstancePath));
            };

            if (FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Edit))
            {
                var open = CreateLaunchInstanceActionButton("mdi-folder-open-outline", "打开实例文件夹");
                open.Click += (_, _) => OpenFolder(instance.VersionDirectory);
                item.AddButton(open);
            }

            if (FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Mod))
            {
                var mods = CreateLaunchInstanceActionButton("mdi-puzzle-outline", "打开 Mods");
                mods.Click += (_, _) => OpenFolder(System.IO.Path.Combine(instance.VersionDirectory, "mods"));
                item.AddButton(mods);
            }

            if (FeatureVisibilityService.IsInstanceFeatureVisible(PixelInstanceFeature.Save))
            {
                var saves = CreateLaunchInstanceActionButton("mdi-content-save-outline", "打开存档");
                saves.Click += (_, _) => OpenFolder(System.IO.Path.Combine(instance.VersionDirectory, "saves"));
                item.AddButton(saves);
            }
            list.Children.Add(item);
        }

        return BuildCard(title, list);
    }

    private IReadOnlyList<MinecraftInstanceInfo> GetLaunchVisibleInstances()
    {
        EnsureLaunchSelectedFolder();
        var selectedFolder = GetSelectedLaunchFolder();
        if (string.IsNullOrWhiteSpace(selectedFolder))
            return _launchViewModel.InstanceModels;
        return _launchViewModel.InstanceModels
            .Where(instance => string.Equals(instance.MinecraftFolder, selectedFolder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private IReadOnlyList<(string title, IReadOnlyList<MinecraftInstanceInfo> instances)> GetLaunchInstanceGroups(IReadOnlyList<MinecraftInstanceInfo> instances)
    {
        var groups = instances
            .GroupBy(GetLaunchInstanceType)
            .OrderByDescending(group => group.Key == "release")
            .ThenBy(group => group.Key)
            .Select(group => (
                title: GetLaunchInstanceGroupTitle(group.Key),
                instances: (IReadOnlyList<MinecraftInstanceInfo>)group
                    .OrderByDescending(instance => instance.ReleaseTime)
                    .ThenBy(instance => instance.Name)
                    .ToArray()))
            .ToArray();
        return groups;
    }

    private static string GetLaunchInstanceGroupTitle(string type) =>
        type switch
        {
            "release" => "正式版",
            "snapshot" => "快照版",
            "old_beta" => "远古 Beta",
            "old_alpha" => "远古 Alpha",
            _ => type
        };

    private static string BuildLaunchInstanceInfo(MinecraftInstanceInfo instance)
    {
        var version = instance.VanillaVersion?.ToString() ?? GetLaunchInstanceType(instance);
        var time = instance.ReleaseTime == DateTime.MinValue ? "" : " · " + instance.ReleaseTime.ToString("yyyy/MM/dd");
        return $"{version}{time} · {instance.VersionDirectory}";
    }

    private static string GetLaunchInstanceIcon(MinecraftInstanceInfo instance) =>
        GetLaunchInstanceType(instance) switch
        {
            "snapshot" => "mdi-flask-outline",
            "old_beta" or "old_alpha" => "mdi-archive-outline",
            _ => "mdi-cube-outline"
        };

    private static string GetLaunchInstanceType(MinecraftInstanceInfo instance) =>
        instance.Json["type"]?.GetValue<string>() ?? "release";

    private static MyIconButton CreateLaunchInstanceActionButton(string icon, string tip)
    {
        var button = new MyIconButton
        {
            Icon = icon,
            IconSize = 12,
            Width = 24,
            Height = 24,
            Padding = new Thickness(4)
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private void OpenLaunchSelectedInstanceFolder()
    {
        if (_launchViewModel.SelectedInstance is not { } instance)
            return;
        OpenFolder(instance.VersionDirectory);
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void RefreshLaunchInstancesSuppressed()
    {
        _instanceViewModel.Refresh();
        RunLaunchRefreshSuppressed(_launchViewModel.RefreshInstances);
        EnsureLaunchSelectedFolder();
    }

    private void RunLaunchRefreshSuppressed(Action action)
    {
        _suppressLaunchRefresh = true;
        try
        {
            action();
        }
        finally
        {
            _suppressLaunchRefresh = false;
        }
    }

    private void EnsureLaunchSelectedFolder()
    {
        var selected = GetSelectedLaunchFolder();
        if (!string.IsNullOrWhiteSpace(selected) &&
            _instanceViewModel.Folders.Any(folder => string.Equals(folder.Path, selected, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var first = _instanceViewModel.Folders.FirstOrDefault()?.Path ?? string.Empty;
        SelectLaunchFolder(first);
    }

    private static string GetSelectedLaunchFolder() => States.Game.SelectedFolder;

    private static void SelectLaunchFolder(string path)
    {
        States.Game.SelectedFolder = path;
    }

    private static bool IsLaunchFolderSelected(string path) =>
        string.Equals(GetSelectedLaunchFolder(), path, StringComparison.OrdinalIgnoreCase);

    private static string GetLaunchFolderTitle(MinecraftFolderInfo folder)
    {
        if (folder.IsDefault && folder.IsCustom)
            return "默认 / 自定义文件夹";
        if (folder.IsDefault)
        {
            if (string.Equals(
                    folder.Path,
                    IOPath.GetFullPath(IOPath.Combine(Basics.ExecutableDirectory, ".minecraft")).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return "当前文件夹";

            return "官方启动器文件夹";
        }

        var name = IOPath.GetFileName(folder.Path.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "自定义文件夹" : name;
    }

    private async Task AddOrImportLaunchFolderAsync()
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 Minecraft 文件夹",
            AllowMultiple = false
        });
        var folder = result.FirstOrDefault();
        if (folder is null)
            return;

        var path = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowHint("无法读取所选文件夹路径。", HintType.Critical);
            return;
        }

        AddLaunchFolder(path);
        SelectLaunchFolder(IOPath.GetFullPath(path));
        RefreshLaunchInstancesSuppressed();
        SetPageHostContent(LeftContentHost, BuildLaunchInstanceLeftPage(), PageHostUpdateMode.SilentRefresh);
        SetPageHostContent(RightContentHost, BuildLaunchInstanceSelectionPage(), PageHostUpdateMode.SilentRefresh);
        ShowHint("已添加实例文件夹。", HintType.Finish);
    }

    private static void AddLaunchFolder(string path)
    {
        var fullPath = IOPath.GetFullPath(path);
        var folders = States.Game.Folders
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(folder => folder.Replace("$", Basics.ExecutableDirectory + IOPath.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToList();
        if (folders.Any(folder => string.Equals(IOPath.GetFullPath(folder), fullPath, StringComparison.OrdinalIgnoreCase)))
            return;
        folders.Add(fullPath);
        States.Game.Folders = string.Join('|', folders);
    }

    private static string GetProfileInfo(string profile)
    {
        return "离线档案 - 行业规范 UUID";
    }

    private static string GetProfileIcon(string profile)
    {
        return "mdi-account-circle-outline";
    }

    private static string GetProfileMethod(string profile)
    {
        return "离线验证";
    }

    private void LoadInitialSetupSection()
    {
        try
        {
            if (PixelSettingsBinder.LoadValue("PixelSetupSelectedSection") is string raw &&
                Enum.TryParse(raw, out PixelSettingSectionKind section))
                _selectedSetupSection = section;
        }
        catch
        {
            _selectedSetupSection = PixelSettingSectionKind.Launch;
        }

        if (FeatureVisibilityService.IsSetupSectionVisible(_selectedSetupSection))
            return;

        foreach (var section in PixelSettingsCatalog.Sections)
        {
            if (!FeatureVisibilityService.IsSetupSectionVisible(section.Kind))
                continue;
            _selectedSetupSection = section.Kind;
            return;
        }
    }

}
