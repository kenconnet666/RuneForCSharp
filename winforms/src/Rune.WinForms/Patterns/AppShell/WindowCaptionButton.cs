using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>窗口按钮的种类。</summary>
public enum WindowCaptionKind
{
    /// <summary>最小化。</summary>
    Minimize,

    /// <summary>最大化（最大化后是还原）。</summary>
    Maximize,

    /// <summary>关闭。</summary>
    Close,
}

/// <summary>
/// 自绘的窗口按钮（桌面特有，<see cref="AppShellControl"/> 的顶栏兼作标题栏时用）：46 像素宽、与标题栏等高，悬停 fillHover 底、按下 fillActive，
/// 关闭悬停 danger 底、dangerContrast 图标。同系统标题栏的按钮：不进 Tab 顺序（键盘用 Alt+空格、Win+方向键），名称与提示是 <see cref="Label"/>。
/// 最大化按钮在非客户区（HTMAXBUTTON，Windows 11 悬停时弹出贴靠布局），收不到客户区的鼠标：悬停、按下由外壳从非客户区的消息转过来。
/// </summary>
[ToolboxItem(false)]
public sealed class WindowCaptionButton : Control
{
    readonly Icon icon = new() { StrokeWidth = 1.5f };
    readonly ToolTip tip = new();
    readonly Effect paint;
    bool mouseOver;
    bool mouseDown;
    bool nonClientHover;

    /// <summary>新建某种窗口按钮。</summary>
    /// <param name="kind">种类。</param>
    public WindowCaptionButton(WindowCaptionKind kind)
    {
        Kind = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AccessibleRole = AccessibleRole.PushButton;
        paint = new Effect(Invalidate);
        UpdateIcon();
    }

    /// <summary>种类。</summary>
    public WindowCaptionKind Kind { get; }

    /// <summary>名称与提示（"最小化"……）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Label
    {
        get => AccessibleName;
        set
        {
            AccessibleName = value;
            tip.SetToolTip(this, value);
        }
    }

    /// <summary>窗口最大化了：最大化按钮换成还原的图标。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsMaximized { get; set { field = value; UpdateIcon(); } }

    /// <summary>指针在按钮上（客户区或非客户区）。</summary>
    internal bool IsHovered => mouseOver || nonClientHover;

    /// <summary>按下着（客户区或非客户区）。</summary>
    internal bool IsPushed => mouseDown || IsNonClientPressed;

    /// <summary>非客户区里按下了（外壳在松开时判断要不要切换最大化）。</summary>
    internal bool IsNonClientPressed { get; private set; }

    /// <summary>非客户区的悬停（最大化按钮）。</summary>
    internal void SetNonClientHover(bool hover)
    {
        if (nonClientHover == hover) return;
        nonClientHover = hover;
        Invalidate();
    }

    /// <summary>非客户区的按下（最大化按钮）。</summary>
    internal void SetNonClientPressed(bool pressed)
    {
        if (IsNonClientPressed == pressed) return;
        IsNonClientPressed = pressed;
        Invalidate();
    }

    void UpdateIcon()
    {
        icon.Data = Kind switch
        {
            WindowCaptionKind.Minimize => Lucide.Minus,
            WindowCaptionKind.Maximize => IsMaximized ? Lucide.Copy : Lucide.Square,
            _ => Lucide.X,
        };
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        mouseOver = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        mouseOver = false;
        mouseDown = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        mouseDown = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        mouseDown = false;
        Invalidate();
    }

    /// <summary>底色（悬停、按下）与居中的 16 像素图标（线宽 1.5，比界面图标细，同系统标题栏）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        using var _ = paint.Track();
        var colors = ProviderControl.ThemeOf(this).Colors;
        var close = Kind == WindowCaptionKind.Close;
        Color? fill = IsPushed ? close ? colors.DangerHover : colors.FillActive : IsHovered ? close ? colors.Danger : colors.FillHover : null;
        var background = AppShellControl.Blend(colors.SurfaceTranslucent, colors.Background);
        using (var brush = new SolidBrush(fill is { } over ? AppShellControl.Blend(over, background) : background)) e.Graphics.FillRectangle(brush, ClientRectangle);
        var foreground = close && (IsHovered || IsPushed) ? colors.DangerContrast : colors.Text;
        var context = new ElementContext(DeviceDpi, new Font(Font.FontFamily, 12f), foreground, RightToLeft == RightToLeft.Yes, ProviderControl.Of(this));
        var size = icon.Measure(context, ClientSize);
        icon.Bounds = new Rectangle((Width - size.Width) / 2, (Height - size.Height) / 2, size.Width, size.Height);
        icon.Paint(e.Graphics, context);
        context.Font.Dispose();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CaptionAccessibleObject(this);

    /// <summary>读屏的"按下"就是点击（同系统标题栏的按钮）。</summary>
    sealed class CaptionAccessibleObject(WindowCaptionButton owner) : ControlAccessibleObject(owner)
    {
        public override string DefaultAction => "按下";

        public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tip.Dispose();
        base.Dispose(disposing);
    }
}
