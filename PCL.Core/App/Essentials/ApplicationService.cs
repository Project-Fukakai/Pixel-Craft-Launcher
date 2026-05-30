using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.App.IoC;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.BeforeLoading, Priority = int.MinValue)]
[LifecycleScope("application", "应用程序", false)]
public sealed partial class ApplicationService
{
    public static Func<Application>? Loading { private get; set; }

    [LifecycleStart]
    private static void _Start()
    {
        Context.Debug("正在初始化 Avalonia 应用程序容器");
        var app = Loading!.Invoke();
        Dispatcher.UIThread.UnhandledException += (_, e) => Lifecycle.OnException(e.Exception);
        Lifecycle.CurrentApplication = app;
        Loading = null;
        Context.Trace("应用程序容器初始化完毕");
    }

    [LifecycleStop]
    private static void _Stop()
    {
        var app = Lifecycle.CurrentApplication;
        var dispatcher = Dispatcher.UIThread;
        if (Lifecycle.IsForceShutdown)
        {
            Context.Warn("已指定强制关闭，跳过 Avalonia 标准关闭流程");
            return;
        }
        using var exited = new ManualResetEventSlim();
        dispatcher.Post(() =>
        {
            Context.Debug("发起 Avalonia 退出流程");
            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            exited.Set();
        }, DispatcherPriority.Send);
        try
        {
            Context.Debug("正在等待应用程序容器退出");
            var result = exited.Wait(5000);
            if (result) Context.Trace("应用程序容器已退出");
            else Context.Warn("应用程序容器退出超时，停止等待");
        }
        finally
        {
            dispatcher.Post(() => { }, DispatcherPriority.Send);
        }
    }
}
