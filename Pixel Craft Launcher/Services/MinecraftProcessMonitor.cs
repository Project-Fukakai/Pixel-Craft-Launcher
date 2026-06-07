using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Services;

public sealed record MinecraftProcessExitInfo(
    int ProcessId,
    string InstanceName,
    int? ExitCode,
    bool WasKilled,
    bool WindowDetected,
    DateTimeOffset StartedAt,
    DateTimeOffset ExitedAt);

public sealed class MinecraftProcessMonitor
{
    public async Task<MinecraftProcessExitInfo> MonitorAsync(
        Process process,
        string instanceName,
        Action<bool>? windowDetected,
        Action? windowProbeTimedOut,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var processId = SafeProcessId(process);
        var windowSeen = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var windowTask = DetectWindowAsync(processId, value =>
        {
            if (!value || windowSeen) return;
            windowSeen = true;
            Dispatcher.UIThread.Post(() => windowDetected?.Invoke(true), DispatcherPriority.Background);
        }, () => Dispatcher.UIThread.Post(() => windowProbeTimedOut?.Invoke(), DispatcherPriority.Background), linked.Token);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            linked.Cancel();
            try
            {
                await windowTask.ConfigureAwait(false);
            }
            catch
            {
                // Window probing is best-effort and must not mask the process result.
            }
        }

        return new MinecraftProcessExitInfo(
            processId,
            instanceName,
            SafeExitCode(process),
            false,
            windowSeen,
            startedAt,
            DateTimeOffset.Now);
    }

    private static async Task DetectWindowAsync(int processId, Action<bool> report, Action timedOut, CancellationToken cancellationToken)
    {
        if (processId <= 0)
            return;

        var deadline = DateTimeOffset.Now.AddMinutes(2);
        if (OperatingSystem.IsMacOS())
        {
            if (await DetectMacWindowAsync(processId, deadline, cancellationToken).ConfigureAwait(false))
            {
                report(true);
                return;
            }

            if (!cancellationToken.IsCancellationRequested)
                timedOut();
            return;
        }

        while (!cancellationToken.IsCancellationRequested && DateTimeOffset.Now < deadline)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    report(true);
                    return;
                }
            }
            catch
            {
                return;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        if (!cancellationToken.IsCancellationRequested)
            timedOut();
    }

    private static async Task<bool> DetectMacWindowAsync(int processId, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var timeout = deadline - DateTimeOffset.Now;
        if (timeout <= TimeSpan.Zero)
            return false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = new List<Task<bool>>
        {
            PollMacWindowAsync(processId, deadline, linked.Token)
        };

        if (MacAccessibilityAccess.IsTrusted)
            tasks.Add(MacAccessibilityWindowObserver.WaitForWindowAsync(processId, timeout, linked.Token));

        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(completed);

            try
            {
                if (await completed.ConfigureAwait(false))
                {
                    linked.Cancel();
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Keep the other macOS detection path alive.
            }
        }

        return false;
    }

    private static async Task<bool> PollMacWindowAsync(int processId, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && DateTimeOffset.Now < deadline)
        {
            if (await MacWindowProbe.HasWindowAsync(processId, cancellationToken).ConfigureAwait(false))
                return true;

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public static bool IsMacAccessibilityTrusted => !OperatingSystem.IsMacOS() || MacAccessibilityAccess.IsTrusted;

    public static void RequestMacAccessibilityAccess()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        MacAccessibilityAccess.RequestAccessPrompt();
        MacAccessibilityAccess.OpenAccessibilitySettings();
    }

    private static int SafeProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }

    private static int? SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static class MacWindowProbe
    {
        public static async Task<bool> HasWindowAsync(int processId, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsMacOS())
                return false;

            var script = "import sys\n" +
                         "try:\n" +
                         " import Quartz\n" +
                         " pid=int(sys.argv[1])\n" +
                         " opts=Quartz.kCGWindowListOptionOnScreenOnly|Quartz.kCGWindowListExcludeDesktopElements\n" +
                         " wins=Quartz.CGWindowListCopyWindowInfo(opts, Quartz.kCGNullWindowID) or []\n" +
                         " print('1' if any(w.get('kCGWindowOwnerPID') == pid for w in wins) else '0')\n" +
                         "except Exception:\n" +
                         " print('0')\n";
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo("/usr/bin/python3")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add(script);
                process.StartInfo.ArgumentList.Add(processId.ToString());
                process.Start();
                var output = await ReadLimitedAsync(process.StandardOutput, cancellationToken).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (output.Trim() == "1")
                    return true;
            }
            catch
            {
                // Try System Events below.
            }

            return await HasWindowViaSystemEventsAsync(processId, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<bool> HasWindowViaSystemEventsAsync(int processId, CancellationToken cancellationToken)
        {
            var script = $"tell application \"System Events\" to count windows of (first application process whose unix id is {processId})";
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo("/usr/bin/osascript")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                process.StartInfo.ArgumentList.Add("-e");
                process.StartInfo.ArgumentList.Add(script);
                process.Start();
                var output = await ReadLimitedAsync(process.StandardOutput, cancellationToken).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return int.TryParse(output.Trim(), out var count) && count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> ReadLimitedAsync(System.IO.TextReader reader, CancellationToken cancellationToken)
        {
            var buffer = new char[16];
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            return new string(buffer, 0, read);
        }
    }
}

public static class MacAccessibilityAccess
{
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Utf8 = 0x08000100;

    public static bool IsTrusted
    {
        get
        {
            if (!OperatingSystem.IsMacOS())
                return true;
            try
            {
                return AXIsProcessTrusted();
            }
            catch
            {
                return false;
            }
        }
    }

    public static void RequestAccessPrompt()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        IntPtr key = IntPtr.Zero;
        IntPtr dictionary = IntPtr.Zero;
        try
        {
            key = CFStringCreateWithCString(IntPtr.Zero, "AXTrustedCheckOptionPrompt", Utf8);
            var value = GetCoreFoundationObject("kCFBooleanTrue");
            var keyCallbacks = GetCoreFoundationSymbol("kCFTypeDictionaryKeyCallBacks");
            var valueCallbacks = GetCoreFoundationSymbol("kCFTypeDictionaryValueCallBacks");
            dictionary = CFDictionaryCreate(IntPtr.Zero, [key], [value], 1, keyCallbacks, valueCallbacks);
            _ = AXIsProcessTrustedWithOptions(dictionary);
        }
        catch
        {
            // Opening System Settings below is the fallback prompt path.
        }
        finally
        {
            if (dictionary != IntPtr.Zero) CFRelease(dictionary);
            if (key != IntPtr.Zero) CFRelease(key);
        }
    }

    public static void OpenAccessibilitySettings()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("/usr/bin/open")
                {
                    UseShellExecute = false,
                    ArgumentList =
                    {
                        "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
                    }
                });
            }
            catch
            {
                // Best-effort only.
            }
        }
    }

    private static IntPtr GetCoreFoundationSymbol(string name)
    {
        var handle = NativeLibrary.Load(CoreFoundation);
        return NativeLibrary.GetExport(handle, name);
    }

    private static IntPtr GetCoreFoundationObject(string name)
    {
        var symbol = GetCoreFoundationSymbol(name);
        return Marshal.ReadIntPtr(symbol);
    }

    [DllImport(ApplicationServices)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(ApplicationServices)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, int encoding);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint count,
        IntPtr keyCallbacks,
        IntPtr valueCallbacks);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}

internal sealed class MacAccessibilityWindowObserver : IDisposable
{
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Utf8 = 0x08000100;
    private const int Success = 0;

    private readonly AXObserverCallback _callback;
    private readonly TaskCompletionSource<bool> _windowDetected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IntPtr _appElement;
    private readonly IntPtr _observer;
    private readonly IntPtr _windowCreated;
    private readonly IntPtr _focusedWindowChanged;
    private readonly IntPtr _runLoopMode;

    private MacAccessibilityWindowObserver(int processId)
    {
        _callback = OnAccessibilityNotification;
        _appElement = AXUIElementCreateApplication(processId);
        _windowCreated = CFStringCreateWithCString(IntPtr.Zero, "AXWindowCreated", Utf8);
        _focusedWindowChanged = CFStringCreateWithCString(IntPtr.Zero, "AXFocusedWindowChanged", Utf8);
        _runLoopMode = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", Utf8);

        var error = AXObserverCreate(processId, _callback, out _observer);
        if (error != Success || _observer == IntPtr.Zero)
            throw new InvalidOperationException("无法创建 macOS Accessibility Observer。");

        var source = AXObserverGetRunLoopSource(_observer);
        CFRunLoopAddSource(CFRunLoopGetCurrent(), source, _runLoopMode);
        _ = AXObserverAddNotification(_observer, _appElement, _windowCreated, IntPtr.Zero);
        _ = AXObserverAddNotification(_observer, _appElement, _focusedWindowChanged, IntPtr.Zero);
    }

    public static async Task<bool> WaitForWindowAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS() || !MacAccessibilityAccess.IsTrusted)
            return false;

        if (await MinecraftProcessMonitorMacFallback.HasWindowAsync(processId, cancellationToken).ConfigureAwait(false))
            return true;

        using var observer = new MacAccessibilityWindowObserver(processId);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : timeout);
        using var _ = timeoutCts.Token.Register(() => observer._windowDetected.TrySetResult(false));

        while (!observer._windowDetected.Task.IsCompleted && !timeoutCts.IsCancellationRequested)
            CFRunLoopRunInMode(observer._runLoopMode, 0.4, true);

        return await observer._windowDetected.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_observer != IntPtr.Zero) CFRelease(_observer);
        if (_appElement != IntPtr.Zero) CFRelease(_appElement);
        if (_windowCreated != IntPtr.Zero) CFRelease(_windowCreated);
        if (_focusedWindowChanged != IntPtr.Zero) CFRelease(_focusedWindowChanged);
        if (_runLoopMode != IntPtr.Zero) CFRelease(_runLoopMode);
    }

    private void OnAccessibilityNotification(IntPtr observer, IntPtr element, IntPtr notification, IntPtr refcon)
    {
        _windowDetected.TrySetResult(true);
    }

    private delegate void AXObserverCallback(IntPtr observer, IntPtr element, IntPtr notification, IntPtr refcon);

    [DllImport(ApplicationServices)]
    private static extern IntPtr AXUIElementCreateApplication(int pid);

    [DllImport(ApplicationServices)]
    private static extern int AXObserverCreate(int pid, AXObserverCallback callback, out IntPtr observer);

    [DllImport(ApplicationServices)]
    private static extern int AXObserverAddNotification(IntPtr observer, IntPtr element, IntPtr notification, IntPtr refcon);

    [DllImport(ApplicationServices)]
    private static extern IntPtr AXObserverGetRunLoopSource(IntPtr observer);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, int encoding);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    private static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, bool returnAfterSourceHandled);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}

internal static class MinecraftProcessMonitorMacFallback
{
    public static async Task<bool> HasWindowAsync(int processId, CancellationToken cancellationToken)
    {
        var script = $"tell application \"System Events\" to count windows of (first application process whose unix id is {processId})";
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("/usr/bin/osascript")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("-e");
            process.StartInfo.ArgumentList.Add(script);
            process.Start();
            var buffer = new char[16];
            var read = await process.StandardOutput.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return int.TryParse(new string(buffer, 0, read).Trim(), out var count) && count > 0;
        }
        catch
        {
            return false;
        }
    }
}
