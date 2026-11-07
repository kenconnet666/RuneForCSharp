using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rune.WinForms;

/// <summary>
/// 提示的窗口：不抢焦点的置顶分层窗口，整张画好的位图（逐像素透明：圆角、伸出面板的箭头抗锯齿）用 UpdateLayeredWindow 贴上去，
/// 淡入改整体的不透明度。平台的 ToolTip 窗口是系统画的矩形，画不出圆角与箭头。
/// </summary>
sealed class TooltipWindow : Form
{
    const int WsExLayered = 0x80000, WsExToolWindow = 0x80, WsExNoActivate = 0x8000000, WsExTopmost = 0x8;
    const int WsPopup = unchecked((int)0x80000000);

    Bitmap? frame;
    Point location;
    readonly System.Windows.Forms.Timer fade = new() { Interval = 15 };
    System.Diagnostics.Stopwatch? fading;
    TimeSpan fadeDuration;

    public TooltipWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        fade.Tick += (_, _) => StepFade();
    }

    /// <summary>提示的面板在屏幕上的矩形（设备像素；测试与箭头核对用）。</summary>
    public Rectangle Panel { get; private set; }

    /// <summary>箭头所在的边（top：面板在触发器上方，箭头在面板下边）；没有箭头时为 null。</summary>
    public string? ArrowSide { get; private set; }

    /// <summary>现在的整体不透明度（0–255）。</summary>
    public byte Alpha { get; private set; }

    /// <inheritdoc />
    protected override bool ShowWithoutActivation => true;

    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style = WsPopup;
            parameters.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate | WsExTopmost;
            return parameters;
        }
    }

    /// <summary>贴上画好的提示并显示；<paramref name="fadeIn"/> 大于 0 时从透明淡入（enter 缓动）。</summary>
    public void Present(Bitmap bitmap, Point at, Rectangle panel, string? arrowSide, TimeSpan fadeIn)
    {
        frame?.Dispose();
        frame = bitmap;
        location = at;
        Panel = panel;
        ArrowSide = arrowSide;
        fadeDuration = fadeIn;
        fading = fadeIn > TimeSpan.Zero ? System.Diagnostics.Stopwatch.StartNew() : null;
        Alpha = fading is null ? (byte)255 : (byte)0;
        if (!Visible)
        {
            Bounds = new Rectangle(at, bitmap.Size);
            Show();
        }
        Apply();
        if (fading is not null) fade.Start();
    }

    /// <summary>藏起来（不销毁窗口，下一个提示接着用）。</summary>
    public void Dismiss()
    {
        fade.Stop();
        fading = null;
        if (Visible) Hide();
    }

    void StepFade()
    {
        if (fading is null)
        {
            fade.Stop();
            return;
        }
        var progress = Math.Min(1, fading.Elapsed / fadeDuration);
        Alpha = (byte)Math.Round(255 * Easing.Enter.Ease(progress));
        if (progress >= 1)
        {
            fading = null;
            fade.Stop();
        }
        Apply();
    }

    /// <summary>把位图连同整体不透明度贴到窗口上（UpdateLayeredWindow，位图是预乘的 ARGB）。</summary>
    void Apply()
    {
        if (frame is null || !IsHandleCreated) return;
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var hBitmap = frame.GetHbitmap(Color.FromArgb(0));
        var old = SelectObject(memory, hBitmap);
        try
        {
            var size = new NativeSize(frame.Width, frame.Height);
            var source = new NativePoint(0, 0);
            var target = new NativePoint(location.X, location.Y);
            var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = Alpha, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screen, ref target, ref size, memory, ref source, 0, ref blend, 2);
        }
        finally
        {
            SelectObject(memory, old);
            DeleteObject(hBitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>新建一张透明的预乘 ARGB 位图（UpdateLayeredWindow 要预乘的像素）。</summary>
    public static Bitmap Canvas(Size size) => new(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fade.Dispose();
            frame?.Dispose();
        }
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeSize(int width, int height)
    {
        public int Width = width;
        public int Height = height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteObject(IntPtr hObject);
}
