using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Rune.Wpf;

/// <summary>
/// AppShell 接管窗口的部分（外壳是窗口的内容时）：顶栏兼作标题栏、窗口全屏。
/// 标题栏：WindowChrome 去掉系统的标题栏、保留缩放边框（CaptionHeight 为 0），非客户区的命中由这里的钩子判断——
/// 顶栏的空白处是标题栏（拖动、双击最大化、右键系统菜单），里面能聚焦的控件照常收输入，最大化按钮是 HTMAXBUTTON（Windows 11 悬停时弹出贴靠布局），
/// 所以它的悬停、按下也从非客户区的消息来。最大化时窗口超出屏幕一圈边框的宽度，内容按显示器的工作区缩回来。
/// </summary>
public partial class AppShell
{
    const int WmMouseMove = 0x0200;
    const int WmNcHitTest = 0x0084;
    const int WmNcMouseMove = 0x00A0;
    const int WmNcLButtonDown = 0x00A1;
    const int WmNcLButtonUp = 0x00A2;
    const int WmNcMouseLeave = 0x02A2;
    const int HtClient = 1;
    const int HtCaption = 2;
    const int HtMaxButton = 9;

    Window? window;
    Window? keyTarget;
    HwndSource? source;
    WindowChrome? restoreChrome;
    bool chromeApplied;
    bool switching;
    (WindowStyle Style, ResizeMode Resize, WindowState State)? beforeFullscreen;

    /// <summary>顶栏兼作窗口的标题栏（测试用）。</summary>
    internal bool IsTitleBar => chromeApplied;

    /// <summary>加载后：Esc 在整个窗口里都算（TS 监听 document）；外壳就是窗口的内容时接管窗口（嵌在页面里的外壳不动窗口）。</summary>
    void Attach()
    {
        keyTarget = Window.GetWindow(this);
        keyTarget?.AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));
        if (keyTarget is not { } host || host.Content != this) return;
        window = host;
        host.StateChanged += OnWindowStateChanged;
        ApplyChrome();
        ApplyFullscreen();
    }

    void Detach()
    {
        keyTarget?.RemoveHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));
        keyTarget = null;
        if (window is null) return;
        window.StateChanged -= OnWindowStateChanged;
        RemoveChrome();
        window = null;
    }

    /// <summary>顶栏兼作标题栏：换上 WindowChrome、挂上命中判断的钩子，窗口按钮自绘。</summary>
    void ApplyChrome()
    {
        if (window is not { } host) return;
        if (ExtendsContentIntoTitleBar && !chromeApplied)
        {
            restoreChrome = WindowChrome.GetWindowChrome(host);
            WindowChrome.SetWindowChrome(host, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                // 留 1 像素的玻璃框：DWM 照常画窗口的阴影
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                UseAeroCaptionButtons = false,
                CornerRadius = default,
            });
            // 在 WindowChrome 之后挂：后挂的钩子先收到消息，标题栏与最大化按钮由这里判断，缩放边框交给 WindowChrome
            source = PresentationSource.FromVisual(host) as HwndSource;
            source?.AddHook(Hook);
            chromeApplied = true;
        }
        else if (!ExtendsContentIntoTitleBar && chromeApplied) RemoveChrome();
        captionButtons.Visibility = Show(chromeApplied);
        UpdateCaptionButtons();
        UpdateFrameMargin();
    }

    void RemoveChrome()
    {
        if (!chromeApplied || window is null) return;
        source?.RemoveHook(Hook);
        source = null;
        WindowChrome.SetWindowChrome(window, restoreChrome);
        chromeApplied = false;
        captionButtons.Visibility = Visibility.Collapsed;
        root.Margin = default;
    }

    /// <summary>窗口不能缩放时没有最大化，不能最小化时没有最小化（同系统标题栏）。</summary>
    void UpdateCaptionButtons()
    {
        if (window is not { } host) return;
        minimizeButton.Visibility = Show(host.ResizeMode != ResizeMode.NoResize || beforeFullscreen is not null);
        maximizeButton.Visibility = Show(host.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip || beforeFullscreen is not null);
        UpdateTexts();
    }

    void ToggleMaximized() => window?.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>全屏：不显示顶栏和侧栏；外壳是窗口的内容时窗口也全屏（无边框、盖住任务栏），退出时回到原来的样子。</summary>
    void ApplyFullscreen()
    {
        if (IsFullscreen) SetCurrentValue(IsSidebarOpenProperty, false);
        if (window is { } host)
        {
            switching = true;
            if (IsFullscreen && beforeFullscreen is null)
            {
                beforeFullscreen = (host.WindowStyle, host.ResizeMode, host.WindowState);
                // 先还原再最大化：已经最大化的窗口换了样式不会重新铺满
                host.WindowState = WindowState.Normal;
                host.ResizeMode = ResizeMode.NoResize;
                host.WindowStyle = WindowStyle.None;
                host.WindowState = WindowState.Maximized;
            }
            else if (!IsFullscreen && beforeFullscreen is { } before)
            {
                beforeFullscreen = null;
                host.WindowState = WindowState.Normal;
                host.WindowStyle = before.Style;
                host.ResizeMode = before.Resize;
                host.WindowState = before.State;
            }
            switching = false;
            UpdateFrameMargin();
        }
        Sync();
        Dispatcher.BeginInvoke(FollowFullscreenFocus, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 进入、退出后焦点跟到对应的按钮上：原来聚焦的按钮随顶栏隐藏（或被移除），焦点会掉到窗口开头。
    /// 焦点还在看得见的地方（正文里的控件、用 Esc 退出时）不动。
    /// </summary>
    void FollowFullscreenFocus()
    {
        if (System.Windows.Input.Keyboard.FocusedElement is UIElement { IsVisible: true } focused && focused is not Window) return;
        (IsFullscreen ? fullscreenExit : fullscreenButton).Focus();
    }

    void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // 从别处退出了最大化（Win+↓）：外壳也退出全屏
        if (!switching && IsFullscreen && window?.WindowState != WindowState.Maximized) SetCurrentValue(IsFullscreenProperty, false);
        UpdateFrameMargin();
        UpdateTexts();
    }

    /// <summary>最大化的窗口超出显示器一圈边框的宽度：内容按工作区（全屏时按整个显示器）缩回来。</summary>
    void UpdateFrameMargin()
    {
        if (window is null || !chromeApplied || window.WindowState != WindowState.Maximized || source is null)
        {
            root.Margin = default;
            return;
        }
        var handle = source.Handle;
        if (!Native.GetWindowRect(handle, out var bounds)) return;
        var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(handle, Native.MonitorDefaultToNearest), ref monitor)) return;
        var area = IsFullscreen ? monitor.Monitor : monitor.Work;
        var dpi = VisualTreeHelper.GetDpi(this);
        root.Margin = new Thickness(
            Math.Max(0, area.Left - bounds.Left) / dpi.DpiScaleX,
            Math.Max(0, area.Top - bounds.Top) / dpi.DpiScaleY,
            Math.Max(0, bounds.Right - area.Right) / dpi.DpiScaleX,
            Math.Max(0, bounds.Bottom - area.Bottom) / dpi.DpiScaleY);
    }

    IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WmNcHitTest when HitTest(lParam) is { } hit:
                handled = true;
                return hit;
            case WmNcMouseMove:
                var overMaximize = (int)wParam == HtMaxButton;
                maximizeButton.SetNonClientHover(overMaximize);
                if (!overMaximize) maximizeButton.SetNonClientPressed(false);
                else Native.TrackNonClientLeave(hwnd);
                break;
            case WmNcMouseLeave or WmMouseMove:
                maximizeButton.SetNonClientHover(false);
                maximizeButton.SetNonClientPressed(false);
                break;
            case WmNcLButtonDown when (int)wParam == HtMaxButton:
                maximizeButton.SetNonClientPressed(true);
                handled = true;
                break;
            case WmNcLButtonUp when (int)wParam == HtMaxButton:
                if (maximizeButton.IsNonClientPressed) ToggleMaximized();
                maximizeButton.SetNonClientPressed(false);
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 顶栏里：最大化按钮是 HTMAXBUTTON；从命中的元素往上，遇到别的窗口按钮或能聚焦的控件（按钮、输入框、导航项）是客户区；都不是就是标题栏。
    /// 顶栏以外、没最大化时窗口边上的缩放边框不管（交给 WindowChrome）。
    /// </summary>
    IntPtr? HitTest(IntPtr lParam)
    {
        if (window is null || header.Visibility != Visibility.Visible) return null;
        var packed = (int)(long)lParam;
        var screen = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
        var point = header.PointFromScreen(screen);
        if (point.X < 0 || point.Y < 0 || point.X >= header.ActualWidth || point.Y >= header.ActualHeight) return null;
        if (window.WindowState == WindowState.Normal)
        {
            var border = SystemParameters.WindowResizeBorderThickness;
            var inWindow = window.PointFromScreen(screen);
            if (inWindow.Y < border.Top || inWindow.X < border.Left || inWindow.X >= window.ActualWidth - border.Right) return null;
        }
        for (var node = header.InputHitTest(point) as DependencyObject; node is not null && node != header; node = TreeParent(node))
        {
            if (node == maximizeButton) return HtMaxButton;
            if (node is WindowCaptionButton or UIElement { Focusable: true }) return HtClient;
        }
        return HtCaption;
    }

    static DependencyObject? TreeParent(DependencyObject node) => node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    static class Native
    {
        public const int MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TrackMouseEventInfo
        {
            public int Size;
            public int Flags;
            public IntPtr Window;
            public int HoverTime;
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr window, int flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        static extern bool TrackMouseEvent(ref TrackMouseEventInfo info);

        /// <summary>离开非客户区时收到 WM_NCMOUSELEAVE（TME_LEAVE | TME_NONCLIENT）。</summary>
        public static void TrackNonClientLeave(IntPtr window)
        {
            var info = new TrackMouseEventInfo { Size = Marshal.SizeOf<TrackMouseEventInfo>(), Flags = 0x02 | 0x10, Window = window };
            TrackMouseEvent(ref info);
        }
    }
}
