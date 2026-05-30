using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Core.Logging;

namespace PCL.Core.UI;

// 图标管理器（处理集合和选择逻辑）
public class IconManager : INotifyPropertyChanged {
    private readonly Dictionary<string, IconModel> _iconIndex = new();

    public IconModel? SelectedIcon
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(SelectedIcon));
        }
    }

    public bool SetSelectedIconByName(string name) {
        if (_iconIndex.TryGetValue(name, out var icon)) {
            SelectedIcon = icon;
            return true;
        }
        return false;
    }

    public bool AddIconFromXaml(string name, string xamlString) {
        if (string.IsNullOrWhiteSpace(name) || _iconIndex.ContainsKey(name)) return false; // 避免重复

        if (TryLoadIconFromXaml(xamlString, out var content)) {
            var model = new IconModel(name, content);
            _iconIndex[name] = model;
            return true;
        }
        return false;
    }

    // 可选：添加移除方法
    public void RemoveIconByName(string name) {
        _iconIndex.Remove(name);
    }
    
    // 从 XAML 字符串加载图标
    public static bool TryLoadIconFromXaml(string xamlString, out UIElement? icon) {
        icon = null;
        if (string.IsNullOrWhiteSpace(xamlString)) return false;

        try {
            return LoadIconFromXaml(xamlString, out icon);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "加载 Avalonia XAML 图标失败");
            icon = null;
            return false;
        }
    }
    
    // 从 XAML 字符串加载图标
    public static bool LoadIconFromXaml(string xamlString, out UIElement? icon) {
        icon = null;
        if (string.IsNullOrWhiteSpace(xamlString)) {
            throw new ArgumentNullException(nameof(xamlString), "XAML 字符串不能为空或空白。");
        }

        var obj = LoadRuntimeXaml(xamlString);
        icon = obj switch {
            UIElement element => element,
            Visual visual => visual as UIElement,
            _ => null
        };

        if (icon is null)
            throw new InvalidOperationException($"XAML 根节点必须是 Avalonia 控件，实际为 {obj.GetType().FullName}。");
        return true;
    }

    private static object LoadRuntimeXaml(string xamlString) {
        var loaderType = typeof(AvaloniaXamlLoader);
        var runtimeLoaderType = loaderType.GetNestedType("IRuntimeXamlLoader", BindingFlags.NonPublic);
        if (runtimeLoaderType is null)
            throw new NotSupportedException("当前 Avalonia 版本未公开运行时 XAML 加载服务。");

        var locatorProperty = typeof(AvaloniaLocator).GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.Static)
                              ?? typeof(AvaloniaLocator).GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
        var locator = locatorProperty?.GetValue(null)
                      ?? throw new InvalidOperationException("Avalonia 服务定位器尚未初始化。");
        var getServiceMethod = locator.GetType().GetMethod("GetService", BindingFlags.Public | BindingFlags.Instance)
                               ?? throw new MissingMethodException(locator.GetType().FullName, "GetService");
        var loader = getServiceMethod.Invoke(locator, [runtimeLoaderType]);
        if (loader is null)
            throw new InvalidOperationException("Avalonia 运行时 XAML 加载服务尚未初始化。");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xamlString));
        var document = new RuntimeXamlLoaderDocument(new Uri("avares://PCL.Core/RuntimeIcon.axaml"), stream);
        var config = new RuntimeXamlLoaderConfiguration {
            LocalAssembly = typeof(IconManager).Assembly
        };

        var loadMethod = runtimeLoaderType.GetMethod("Load", BindingFlags.Public | BindingFlags.Instance)
                         ?? throw new MissingMethodException(runtimeLoaderType.FullName, "Load");
        return loadMethod.Invoke(loader, [document, config])
               ?? throw new InvalidOperationException("运行时 XAML 加载结果为空。");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
