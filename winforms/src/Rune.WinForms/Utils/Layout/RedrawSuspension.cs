using System.Runtime.InteropServices;

namespace Rune.WinForms;

/// <summary>
/// 一段改动期间不重画，改完一次画出来：WinForms 的子控件各是一个窗口，换一整页内容时几百个子窗口建出来、挪到位都会各画一次，
/// 屏幕上一块块地闪。停的是宿主窗口的 WM_SETREDRAW；嵌套时只有最外层恢复。
/// <code>
/// using (shell.MainScroll.SuspendRedraw())
/// {
///     shell.Content?.Dispose();
///     shell.Content = nextPage;
/// }
/// </code>
/// </summary>
public static class RedrawSuspension
{
    const int WmSetRedraw = 0x000B;
    const uint RdwInvalidate = 0x0001, RdwErase = 0x0004, RdwAllChildren = 0x0080, RdwFrame = 0x0400;

    static readonly HashSet<Control> suspended = [];

    /// <summary>停画到返回值释放为止；还没有窗口句柄（没显示过）时什么也不做。</summary>
    public static IDisposable SuspendRedraw(this Control control)
    {
        if (!control.IsHandleCreated || !suspended.Add(control)) return Resume.None;
        SendMessage(control.Handle, WmSetRedraw, 0, 0);
        return new Resume(control);
    }

    sealed class Resume(Control? control) : IDisposable
    {
        public static readonly Resume None = new(null);

        public void Dispose()
        {
            if (control is null || !suspended.Remove(control) || !control.IsHandleCreated) return;
            SendMessage(control.Handle, WmSetRedraw, 1, 0);
            RedrawWindow(control.Handle, 0, 0, RdwInvalidate | RdwErase | RdwFrame | RdwAllChildren);
        }
    }

    [DllImport("user32.dll")]
    static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    static extern bool RedrawWindow(nint window, nint updateRect, nint updateRegion, uint flags);
}
