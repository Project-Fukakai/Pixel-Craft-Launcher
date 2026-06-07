using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Pixel_Craft_Launcher.Controls.Platform;

public static class MacOSTitleBar
{
    public static readonly AttachedProperty<bool> IsThickProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>(
            "IsThick", typeof(MacOSTitleBar));

    private static readonly ConditionalWeakTable<Window, ThickTitleBarState> States = new();

    static MacOSTitleBar()
    {
        IsThickProperty.Changed.AddClassHandler<Window>(OnIsThickChanged);
    }

    public static bool GetIsThick(Window window)
    {
        return window.GetValue(IsThickProperty);
    }

    public static void SetIsThick(Window window, bool value)
    {
        window.SetValue(IsThickProperty, value);
    }

    private static void OnIsThickChanged(Window window, AvaloniaPropertyChangedEventArgs args)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        if (args.NewValue is true)
        {
            if (States.TryGetValue(window, out var oldState))
            {
                oldState.Detach();
                States.Remove(window);
            }

            var state = new ThickTitleBarState(window);
            States.Add(window, state);
            state.Attach();
        }
        else if (States.TryGetValue(window, out var state))
        {
            state.Detach();
            States.Remove(window);
        }
    }

    private sealed class ThickTitleBarState
    {
        private readonly Window _window;
        private IntPtr _nsWindow;
        private IntPtr _nsToolbar;
        private IntPtr _nsAccessoryController;
        private IntPtr _nsAccessoryView;
        private IntPtr _selSetToolbar;
        private IntPtr _selAddTitlebarAccessoryViewController;
        private IntPtr _selRemoveTitlebarAccessoryViewController;
        private IntPtr _selStandardWindowButton;
        private IntPtr _selSetFrameOrigin;
        private IntPtr _selSetTitlebarAppearsTransparent;
        private IntPtr _selSetTitleVisibility;
        private bool _attached;

        public ThickTitleBarState(Window window)
        {
            _window = window;
        }

        public void Attach()
        {
            if (_attached)
                return;

            if (_window.IsLoaded)
                Setup();
            else
                _window.Opened += OnOpened;
        }

        public void Detach()
        {
            _window.Opened -= OnOpened;
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window.SizeChanged -= OnWindowSizeChanged;

            if (_nsWindow != IntPtr.Zero && _selSetToolbar != IntPtr.Zero)
                objc_msgSend_arg(_nsWindow, _selSetToolbar, IntPtr.Zero);

            if (_nsWindow != IntPtr.Zero &&
                _nsAccessoryController != IntPtr.Zero &&
                _selRemoveTitlebarAccessoryViewController != IntPtr.Zero &&
                RespondsToSelector(_nsWindow, _selRemoveTitlebarAccessoryViewController))
                objc_msgSend_arg(_nsWindow, _selRemoveTitlebarAccessoryViewController, _nsAccessoryController);

            _attached = false;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            _window.Opened -= OnOpened;
            Setup();
        }

        private void Setup()
        {
            try
            {
                _nsWindow = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (_nsWindow == IntPtr.Zero)
                    return;

                _selSetToolbar = sel_registerName("setToolbar:");
                _selAddTitlebarAccessoryViewController = sel_registerName("addTitlebarAccessoryViewController:");
                _selRemoveTitlebarAccessoryViewController = sel_registerName("removeTitlebarAccessoryViewController:");
                _selStandardWindowButton = sel_registerName("standardWindowButton:");
                _selSetFrameOrigin = sel_registerName("setFrameOrigin:");
                _selSetTitlebarAppearsTransparent = sel_registerName("setTitlebarAppearsTransparent:");
                _selSetTitleVisibility = sel_registerName("setTitleVisibility:");

                _nsToolbar = objc_msgSend_ret(objc_getClass("NSToolbar"), sel_registerName("alloc"));
                _nsToolbar = objc_msgSend_arg_ret(
                    _nsToolbar,
                    sel_registerName("initWithIdentifier:"),
                    CreateNSString("pixelTitleBar"));

                if (_nsToolbar == IntPtr.Zero)
                    return;

                objc_msgSend_bool(_nsToolbar, sel_registerName("setShowsBaselineSeparator:"), false);
                objc_msgSend_arg(_nsWindow, _selSetToolbar, _nsToolbar);
                AddTitlebarAccessory();
                objc_msgSend_bool(_nsWindow, _selSetTitlebarAppearsTransparent, true);
                objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
                PlaceStandardWindowButtons();
                Dispatcher.UIThread.Post(PlaceStandardWindowButtons, DispatcherPriority.Background);

                _window.PropertyChanged += OnWindowPropertyChanged;
                _window.SizeChanged += OnWindowSizeChanged;
                _attached = true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("macOS thick title bar setup failed: {0}", ex.Message);
            }
        }

        private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            PlaceStandardWindowButtons();
            Dispatcher.UIThread.Post(PlaceStandardWindowButtons, DispatcherPriority.Background);
        }

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Window.WindowStateProperty)
                return;

            var newState = e.GetNewValue<WindowState>();
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_nsWindow == IntPtr.Zero || _selSetToolbar == IntPtr.Zero)
                        return;

                    if (newState == WindowState.FullScreen)
                    {
                        objc_msgSend_arg(_nsWindow, _selSetToolbar, IntPtr.Zero);
                        objc_msgSend_bool(_nsWindow, _selSetTitlebarAppearsTransparent, true);
                        objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
                    }
                    else
                    {
                        objc_msgSend_arg(_nsWindow, _selSetToolbar, _nsToolbar);
                        objc_msgSend_bool(_nsWindow, _selSetTitlebarAppearsTransparent, true);
                        objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("macOS thick title bar state update failed: {0}", ex.Message);
                }
            }, DispatcherPriority.Background);
        }

        private void AddTitlebarAccessory()
        {
            if (!RespondsToSelector(_nsWindow, _selAddTitlebarAccessoryViewController))
                return;

            _nsAccessoryController = objc_msgSend_ret(
                objc_msgSend_ret(objc_getClass("NSTitlebarAccessoryViewController"), sel_registerName("alloc")),
                sel_registerName("init"));
            _nsAccessoryView = objc_msgSend_rect_ret(
                objc_msgSend_ret(objc_getClass("NSView"), sel_registerName("alloc")),
                sel_registerName("initWithFrame:"),
                new CGRect(0, 0, 1, 0));

            if (_nsAccessoryController == IntPtr.Zero || _nsAccessoryView == IntPtr.Zero)
                return;

            objc_msgSend_arg(_nsAccessoryController, sel_registerName("setView:"), _nsAccessoryView);
            objc_msgSend_long(_nsAccessoryController, sel_registerName("setLayoutAttribute:"), 1);
            objc_msgSend_arg(_nsWindow, _selAddTitlebarAccessoryViewController, _nsAccessoryController);
        }

        private void PlaceStandardWindowButtons()
        {
            if (!RespondsToSelector(_nsWindow, _selStandardWindowButton))
                return;

            PlaceStandardWindowButton(0, 18, 6);
            PlaceStandardWindowButton(1, 38, 6);
            PlaceStandardWindowButton(2, 58, 6);
        }

        private void PlaceStandardWindowButton(long buttonKind, double x, double y)
        {
            var button = objc_msgSend_long_ret(_nsWindow, _selStandardWindowButton, buttonKind);
            if (button == IntPtr.Zero)
                return;

            if (!RespondsToSelector(button, _selSetFrameOrigin))
                return;

            objc_msgSend_point(button, _selSetFrameOrigin, new CGPoint(x, y));
        }
    }

    private static IntPtr CreateNSString(string value)
    {
        var nsString = objc_msgSend_ret(objc_getClass("NSString"), sel_registerName("alloc"));
        var utf8Ptr = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return objc_msgSend_arg_ret(nsString, sel_registerName("initWithUTF8String:"), utf8Ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Ptr);
        }
    }

    private static bool RespondsToSelector(IntPtr receiver, IntPtr selector)
    {
        return receiver != IntPtr.Zero &&
               selector != IntPtr.Zero &&
               objc_msgSend_bool_ret(receiver, sel_registerName("respondsToSelector:"), selector);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect
    {
        private readonly CGPoint _origin;
        private readonly CGSize _size;

        public CGRect(double x, double y, double width, double height)
        {
            _origin = new CGPoint(x, y);
            _size = new CGSize(width, height);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint
    {
        private readonly double _x;
        private readonly double _y;

        public CGPoint(double x, double y)
        {
            _x = x;
            _y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize
    {
        private readonly double _width;
        private readonly double _height;

        public CGSize(double width, double height)
        {
            _width = width;
            _height = height;
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_arg(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_arg_ret(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_long_ret(IntPtr receiver, IntPtr selector, long arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_rect_ret(IntPtr receiver, IntPtr selector, CGRect rect);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_point(IntPtr receiver, IntPtr selector, CGPoint point);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_ret(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_long(IntPtr receiver, IntPtr selector, long arg);
}
