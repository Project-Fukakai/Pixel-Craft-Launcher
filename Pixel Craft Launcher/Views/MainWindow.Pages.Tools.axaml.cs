using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

namespace Pixel_Craft_Launcher.Views;

public partial class MainWindow
{
    private Control BuildPlaceholderRightPage(MainPageKind page)
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard(GetPageTitle(page), GetPageDescription(page)));
        stack.Children.Add(BuildCard("页面承载区", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("这里先保留 Plain 主界面的左右分栏和内容容器，后续可以把真实页面直接放进右侧 ContentControl。"),
                CreateBodyText("Shell 级提示、弹窗遮罩、浮动按钮和窗口按钮已经接入。")
            }
        }));
        stack.Children.Add(BuildCard("迁移备注", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("本轮不接入 PCL.Core，也不迁 MinecraftServer 查询控件。"),
                CreateBodyText("顶部导航当前切换轻量占位页，Tools 页保留控件验收内容。")
            }
        }));

        return BuildScrollableMainPane(stack);
    }

    private Control BuildControlsPreviewPage()
    {
        var stack = CreatePageStack();
        stack.Children.Add(BuildHeroCard("工具", "临时保留控件验收页，确认基础控件在新 Shell 内仍可显示和交互。"));
        if (!FeatureVisibilityService.IsToolVisible(PixelToolFeature.Test))
        {
            stack.Children.Add(BuildCard("工具", CreateSubText("控件验收入口已隐藏。")));
            return BuildScrollableMainPane(stack);
        }

        stack.Children.Add(BuildButtonsCard());
        stack.Children.Add(BuildInputsCard());
        stack.Children.Add(BuildSelectionCard());
        stack.Children.Add(BuildLoadingAndListCard());
        stack.Children.Add(BuildCard("弹窗与行为", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new MyButton
                {
                    Text = "打开 MyMsgMarkdown",
                    Variant = MyButtonVariant.Flat,
                    HorizontalAlignment = HorizontalAlignment.Left
                }.WithClick((_, _) => ShowMessage("Markdown 消息", "这是 Shell 遮罩层中的消息控件。\n\n正文暂按纯文本换行显示。")),
                CreateLazyLoadExample()
            }
        }));

        return BuildScrollableMainPane(stack);
    }

    private Control BuildButtonsCard()
    {
        return BuildCard("按钮", new WrapPanel
        {
            Children =
            {
                new MyButton { Text = "Tonal", Variant = MyButtonVariant.Tonal, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Text", Variant = MyButtonVariant.Text, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Outlined", Variant = MyButtonVariant.Outlined, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Elevated", Variant = MyButtonVariant.Elevated, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Flat", Variant = MyButtonVariant.Flat, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Prepend", PrependIcon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Append", AppendIcon = "mdi-arrow-right", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Stacked", Icon = "mdi-apps", Stacked = true, Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyIconButton { Icon = "mdi-check", Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Small", Size = MyButtonSize.Small, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Large", Size = MyButtonSize.Large, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Disabled", IsEnabled = false, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Readonly", IsReadOnly = true, Margin = new Thickness(0, 0, 10, 10) },
                new MyButton { Text = "Block", IsBlock = true, Width = 220, Margin = new Thickness(0, 0, 10, 10) }
            }
        });
    }

    private Control BuildInputsCard()
    {
        var searchBox = new MySearchBox { HintText = "搜索控件", Width = 240 };
        var searchResult = CreateBodyText("等待搜索");
        searchBox.Search += (_, _) => searchResult.Text = $"搜索：{searchBox.Text ?? string.Empty}";

        var textBox = new MyTextBox { HintText = "安全剪贴板 TextBox", Width = 240 };
        ClipboardInterceptor.SetEnableSafeClipboard(textBox, true);

        var comboBox = new MyComboBox
        {
            HintText = "下拉选择",
            Width = 240,
            ItemsSource = new[] { "默认", "轻量", "完整" },
            SelectedIndex = 0
        };

        return BuildCard("输入", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                searchBox,
                searchResult,
                textBox,
                comboBox,
                new FontSelector { Width = 280, Tooltip = "系统字体" },
                new MySlider { Minimum = 0, Maximum = 100, Value = 35, Width = 280 }
            }
        });
    }

    private Control BuildSelectionCard()
    {
        var checkBox = new MyCheckBox { Text = "CheckBox Checked alias", Checked = true };
        var radioBoxA = new MyRadioBox { Text = "RadioBox A", Checked = true };
        var radioBoxB = new MyRadioBox { Text = "RadioBox B" };
        var radioButtonA = new MyRadioButton { Text = "启动", Checked = true, Icon = "mdi-play" };
        var radioButtonB = new MyRadioButton { Text = "下载", Icon = "mdi-download" };

        return BuildCard("选择控件", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                checkBox,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { radioBoxA, radioBoxB } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { radioButtonA, radioButtonB } },
                new MyHint { Text = "提示控件支持 Blue / Yellow / Red 主题。", Theme = MyHint.Themes.Yellow, CanClose = false }
            }
        });
    }

    private Control BuildLoadingAndListCard()
    {
        var loading = new MyLoading
        {
            Text = "Loading",
            State = _loadingState,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var listItem = new MyListItem
        {
            Title = "MyListItem",
            Info = "图标、按钮区、选中条与二级信息",
            Type = MyListItem.CheckType.CheckBox,
            Logo = Geometry.Parse("M3,3 L17,3 L17,17 L3,17 Z")
        };
        listItem.AddButton(new MyIconButton
        {
            Icon = "mdi-check",
            IconSize = 12,
            Width = 28,
            Height = 28
        });

        return BuildCard("加载与列表", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new MyButton { Text = "Loading" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Loading),
                        new MyButton { Text = "Stop" }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Stop),
                        new MyButton { Text = "Error", Variant = MyButtonVariant.Outlined, IsDanger = true }.WithClick((_, _) => _loadingState.LoadingState = MyLoadingState.Error)
                    }
                },
                loading,
                BuildM3LoadingIndicatorPreview(),
                listItem
            }
        });
    }

    private static Control BuildM3LoadingIndicatorPreview()
    {
        static Control CreatePreview(string title, MyM3LoadingIndicator indicator)
        {
            return new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 0, 18, 8),
                Children =
                {
                    indicator,
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 12,
                        Foreground = BodyForeground,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
        }

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateBodyText("Material 3 Loading Indicator"),
                new WrapPanel
                {
                    Children =
                    {
                        CreatePreview("Primary", new MyM3LoadingIndicator
                        {
                            Size = 38,
                            Color = ThemeBrushes.Primary
                        }),
                        CreatePreview("Contained", new MyM3LoadingIndicator
                        {
                            Size = 56,
                            Contained = true,
                            Color = ThemeBrushes.OnPrimaryContainer,
                            ContainerColor = ThemeBrushes.PrimaryContainer
                        }),
                        CreatePreview("Error", new MyM3LoadingIndicator
                        {
                            Size = 46,
                            IsError = true,
                            Color = ThemeBrushes.Primary,
                            ErrorColor = ThemeBrushes.Error
                        })
                    }
                }
            }
        };
    }

    private Control CreateLazyLoadExample()
    {
        var marker = new TextBlock
        {
            Text = "LazyLoad 示例：滚入视口后触发",
            Foreground = BodyForeground,
            Margin = new Thickness(0, 20, 0, 0)
        };
        LazyLoadBehavior.SetAction(marker, () => marker.Text = "LazyLoad 已触发一次");
        return marker;
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
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = BodyForeground
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
            TextWrapping = TextWrapping.Wrap,
            Foreground = BodyForeground,
            LineHeight = 22
        };
    }

    private static string GetPageTitle(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => "下载",
            MainPageKind.Setup => "设置",
            MainPageKind.Tools => "工具",
            _ => "启动"
        };
    }

    private static string GetPageDescription(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => "版本下载、资源补全和任务队列会在这里逐步接入。",
            MainPageKind.Setup => "启动器设置、游戏设置和账户设置先使用占位承载。",
            MainPageKind.Tools => "调试与控件验收入口，后续也可承载日志、导入和维护工具。",
            _ => "启动页 Shell 已就绪，后续迁入账户、版本选择和启动按钮逻辑。"
        };
    }

    private static (string title, string info, string icon, bool active)[] GetLeftItems(MainPageKind page)
    {
        return page switch
        {
            MainPageKind.Download => new[]
            {
                ("版本列表", "Minecraft / Mod Loader", "mdi-cube-outline", true),
                ("下载任务", "队列与进度", "mdi-download", false),
                ("资源补全", "库文件与资产", "mdi-package-variant", false)
            },
            MainPageKind.Setup => new[]
            {
                ("启动器", "主题与窗口", "mdi-cog-outline", true),
                ("游戏", "Java 与内存", "mdi-controller-classic-outline", false),
                ("账户", "登录与档案", "mdi-account-circle-outline", false)
            },
            MainPageKind.Tools => new[]
            {
                ("控件验收", "迁移组件预览", "mdi-tools", true),
                ("日志", "运行与诊断", "mdi-text-box-outline", false),
                ("导入", "整合包与实例", "mdi-import", false)
            },
            _ => new[]
            {
                ("概览", "版本与账户", "mdi-play-circle-outline", true),
                ("实例", "本地游戏列表", "mdi-folder-multiple-outline", false),
                ("动态", "公告与提示", "mdi-bullhorn-outline", false)
            }
        };
    }

}
