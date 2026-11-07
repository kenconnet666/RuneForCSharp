using System.Runtime.InteropServices;

namespace Rune.WinForms;

/// <summary>
/// AppShellControl 接管窗体的部分（外壳铺满窗体时）：顶栏兼作标题栏、窗体全屏。
/// 标题栏：子类化窗体（NativeWindow），WM_NCCALCSIZE 时把客户区的上边挪回窗体顶端（去掉系统的标题栏，左右下的缩放边框照旧），
/// WM_NCHITTEST 时顶上一条是缩放边框，顶栏的空白处是标题栏（拖动、双击最大化、右键系统菜单），里面能聚焦的控件照常收输入，
/// 最大化按钮是 HTMAXBUTTON（Windows 11 悬停时弹出贴靠布局），所以它的悬停、按下也从非客户区的消息来。最大化时上边缩回一圈边框的厚度。
/// </summary>
public partial class AppShellControl
{
    Form? attached;
    TitleBarHook? hook;
    (FormBorderStyle Border, FormWindowState State)? beforeFullscreen;
    FormWindowState lastState;
    bool switching;

    /// <summary>外壳铺满的窗体；嵌在别的容器里时为 null（不动窗体）。</summary>
    Form? Form => Parent is Form form && Dock == DockStyle.Fill ? form : null;

    /// <summary>顶栏兼作窗体的标题栏（测试用）。</summary>
    internal bool IsTitleBar => hook is not null;

    /// <inheritdoc />
    protected override void OnDockChanged(EventArgs e)
    {
        base.OnDockChanged(e);
        AttachForm();
    }

    /// <summary>换了窗体：从旧的上撤下，接到新的上（句柄建好后才子类化）。</summary>
    void AttachForm()
    {
        var next = Form;
        if (next == attached)
        {
            ApplyChrome();
            return;
        }
        if (attached is { } old)
        {
            old.HandleCreated -= OnFormHandleCreated;
            old.HandleDestroyed -= OnFormHandleDestroyed;
            old.Resize -= OnFormResize;
            RemoveChrome();
        }
        attached = next;
        if (next is null) return;
        next.HandleCreated += OnFormHandleCreated;
        next.HandleDestroyed += OnFormHandleDestroyed;
        next.Resize += OnFormResize;
        lastState = next.WindowState;
        ApplyChrome();
    }

    void OnFormHandleCreated(object? sender, EventArgs e) => ApplyChrome();

    void OnFormHandleDestroyed(object? sender, EventArgs e) => RemoveChrome();

    /// <summary>顶栏兼作标题栏：子类化窗体；关掉时撤下，系统的标题栏回来。</summary>
    void ApplyChrome()
    {
        var want = attached is { IsHandleCreated: true } && ExtendsContentIntoTitleBar;
        if (want && hook is null) hook = new TitleBarHook(this, attached!);
        else if (!want) RemoveChrome();
        UpdateCaptionButtons();
    }

    void RemoveChrome()
    {
        hook?.Dispose();
        hook = null;
        UpdateCaptionButtons();
    }

    /// <summary>窗口按钮只在顶栏兼作标题栏时有；窗体没有最小化、最大化框时也没有对应的按钮（同系统标题栏）。</summary>
    void UpdateCaptionButtons()
    {
        var shown = hook is not null && attached is { } form;
        minimizeButton.Visible = shown && attached!.MinimizeBox;
        maximizeButton.Visible = shown && attached!.MaximizeBox && attached.FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;
        closeButton.Visible = shown;
        UpdateTexts();
        PerformLayout();
    }

    void ToggleMaximized() => Form?.WindowState = Form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    /// <summary>窗体的状态变了：最大化按钮换图标与名称；从别处退出了全屏（Win+↓）时外壳也退出。</summary>
    void OnFormResize(object? sender, EventArgs e)
    {
        if (attached is not { } form || form.WindowState == lastState) return;
        lastState = form.WindowState;
        if (!switching && IsFullscreen && form.WindowState != FormWindowState.Maximized) IsFullscreen = false;
        UpdateTexts();
    }

    /// <summary>全屏：外壳铺满窗体时窗体也全屏（无边框、盖住任务栏），退出时回到原来的样子。</summary>
    void ApplyFullscreen()
    {
        if (attached is not { } form) return;
        switching = true;
        if (IsFullscreen && beforeFullscreen is null)
        {
            beforeFullscreen = (form.FormBorderStyle, form.WindowState);
            // 先还原再最大化：已经最大化的窗体换了边框不会重新铺满
            form.WindowState = FormWindowState.Normal;
            form.FormBorderStyle = FormBorderStyle.None;
            form.WindowState = FormWindowState.Maximized;
        }
        else if (!IsFullscreen && beforeFullscreen is { } before)
        {
            beforeFullscreen = null;
            form.WindowState = FormWindowState.Normal;
            form.FormBorderStyle = before.Border;
            form.WindowState = before.State;
        }
        lastState = form.WindowState;
        switching = false;
    }

    /// <summary>
    /// 窗体客户区里的命中（屏幕坐标）：没最大化时顶上一条是缩放边框；顶栏里最大化按钮是 HTMAXBUTTON，
    /// 从命中的控件往上遇到别的窗口按钮或能聚焦的控件是客户区，都不是就是标题栏。
    /// </summary>
    internal int HitTest(Point screen, bool zoomed, int frame)
    {
        var point = PointToClient(screen);
        if (!zoomed && point.Y >= 0 && point.Y < frame)
            return point.X < frame ? Native.HtTopLeft : point.X >= Width - frame ? Native.HtTopRight : Native.HtTop;
        if (!header.Visible || !header.Bounds.Contains(point)) return Native.HtClient;
        for (Control? node = Deepest(header, header.PointToClient(screen)); node is not null && node != header; node = node.Parent)
        {
            if (node == maximizeButton) return Native.HtMaxButton;
            if (node is WindowCaptionButton || node.CanSelect) return Native.HtClient;
        }
        return Native.HtCaption;
    }

    static Control Deepest(Control parent, Point point) =>
        parent.GetChildAtPoint(point, GetChildAtPointSkip.Invisible) is { } child ? Deepest(child, child.PointToClient(parent.PointToScreen(point))) : parent;

    /// <summary>子类化窗体的窗口过程：去掉系统的标题栏、判断非客户区的命中、最大化按钮的悬停与按下、窗体自己收到的 Esc。</summary>
    sealed class TitleBarHook : NativeWindow, IDisposable
    {
        readonly AppShellControl shell;
        readonly Form form;

        public TitleBarHook(AppShellControl shell, Form form)
        {
            this.shell = shell;
            this.form = form;
            AssignHandle(form.Handle);
            Native.RefreshFrame(form.Handle);
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            var handle = Handle;
            ReleaseHandle();
            Native.RefreshFrame(handle);
        }

        /// <summary>最大化时窗体超出屏幕一圈边框的厚度；没最大化时顶上这么宽是缩放边框。</summary>
        int Frame => Native.FrameThickness(form.DeviceDpi);

        protected override void WndProc(ref Message m)
        {
            var maximize = shell.maximizeButton;
            switch (m.Msg)
            {
                case Native.WmNcCalcSize when m.WParam != IntPtr.Zero && form.FormBorderStyle != FormBorderStyle.None:
                    var top = Marshal.PtrToStructure<Native.NcCalcSizeParams>(m.LParam).Rect0.Top;
                    base.WndProc(ref m);
                    var parameters = Marshal.PtrToStructure<Native.NcCalcSizeParams>(m.LParam);
                    parameters.Rect0.Top = top + (Native.IsZoomed(Handle) ? Frame : 0);
                    Marshal.StructureToPtr(parameters, m.LParam, false);
                    m.Result = IntPtr.Zero;
                    return;
                case Native.WmNcHitTest when form.FormBorderStyle != FormBorderStyle.None:
                    base.WndProc(ref m);
                    if ((int)m.Result == Native.HtClient)
                    {
                        var packed = (int)(long)m.LParam;
                        var screen = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
                        m.Result = shell.HitTest(screen, Native.IsZoomed(Handle), Frame);
                    }
                    return;
                case Native.WmNcMouseMove:
                    var over = (int)m.WParam == Native.HtMaxButton;
                    maximize.SetNonClientHover(over);
                    if (!over) maximize.SetNonClientPressed(false);
                    else Native.TrackNonClientLeave(Handle);
                    break;
                case Native.WmNcMouseLeave:
                    maximize.SetNonClientHover(false);
                    maximize.SetNonClientPressed(false);
                    break;
                case Native.WmNcLButtonDown when (int)m.WParam == Native.HtMaxButton:
                    maximize.SetNonClientPressed(true);
                    m.Result = IntPtr.Zero;
                    return;
                case Native.WmNcLButtonUp when (int)m.WParam == Native.HtMaxButton:
                    if (maximize.IsNonClientPressed) shell.ToggleMaximized();
                    maximize.SetNonClientPressed(false);
                    m.Result = IntPtr.Zero;
                    return;
                case Native.WmKeyDown when (Keys)(int)m.WParam == Keys.Escape && shell.HandleEscape():
                    return;
            }
            base.WndProc(ref m);
        }
    }

    static class Native
    {
        public const int WmKeyDown = 0x0100;
        public const int WmNcCalcSize = 0x0083;
        public const int WmNcHitTest = 0x0084;
        public const int WmNcMouseMove = 0x00A0;
        public const int WmNcLButtonDown = 0x00A1;
        public const int WmNcLButtonUp = 0x00A2;
        public const int WmNcMouseLeave = 0x02A2;
        public const int HtClient = 1;
        public const int HtCaption = 2;
        public const int HtMaxButton = 9;
        public const int HtTop = 12;
        public const int HtTopLeft = 13;
        public const int HtTopRight = 14;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NcCalcSizeParams
        {
            public Rect Rect0, Rect1, Rect2;
            public IntPtr Position;
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
        public static extern bool IsZoomed(IntPtr window);

        [DllImport("user32.dll")]
        static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        static extern bool TrackMouseEvent(ref TrackMouseEventInfo info);

        /// <summary>缩放边框的厚度：SM_CYFRAME + SM_CXPADDEDBORDER（按窗体的 DPI）。</summary>
        public static int FrameThickness(int dpi) => GetSystemMetricsForDpi(33, (uint)dpi) + GetSystemMetricsForDpi(92, (uint)dpi);

        /// <summary>让窗体重新计算非客户区（SWP_FRAMECHANGED | NOMOVE | NOSIZE | NOZORDER | NOACTIVATE）。</summary>
        public static void RefreshFrame(IntPtr window) => SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x0020 | 0x0002 | 0x0001 | 0x0004 | 0x0010);

        /// <summary>离开非客户区时收到 WM_NCMOUSELEAVE（TME_LEAVE | TME_NONCLIENT）。</summary>
        public static void TrackNonClientLeave(IntPtr window)
        {
            var info = new TrackMouseEventInfo { Size = Marshal.SizeOf<TrackMouseEventInfo>(), Flags = 0x02 | 0x10, Window = window };
            TrackMouseEvent(ref info);
        }
    }
}
