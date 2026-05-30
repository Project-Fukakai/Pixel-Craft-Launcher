using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PCL.Core.App.IoC;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.WindowCreating, Priority = int.MaxValue)]
public sealed class MainWindowService : GeneralService
{
    public static Func<Window>? Loading { private get; set; }

    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    private MainWindowService() : base("window", "主窗体", false) { _context = ServiceContext; }
    
    public override void Start()
    {
        Context.Debug("正在初始化 Avalonia 窗体");
        var window = Loading!.Invoke();
        window.Loaded += (_, _) => Lifecycle.OnWindowCreated();
        if (Lifecycle.CurrentApplication.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = window;
        Context.Trace("窗体创建完毕");
    }
}
