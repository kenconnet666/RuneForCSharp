using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 整块可点的一行或一列（Steps 可点回的步骤、Pager 的链接卡片）：能聚焦，点击、Enter、空格都发 Click；点里面的子控件同点这一块，
/// 指针在子控件上也算悬停（子控件是各自的窗口，鼠标消息不经过这里）。默认外观是悬停 fillHover 底、sm 圆角，键盘聚焦时画焦点环；
/// 子类可以换圆角、换底色与边框的画法。
/// </summary>
class PressableFlex : FlexControl
{
    /// <summary>新建：能聚焦，读屏里是按钮（AccessibleRole 可以改成链接）。</summary>
    public PressableFlex()
    {
        AutoSize = true;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>指针在这一块上（含子控件）。</summary>
    protected bool Hovered { get; private set; }

    /// <summary>圆角半径（设备像素），默认 sm。</summary>
    protected virtual float Radius(ThemeTokens theme) => theme.Radii.Sm * this.EmPixels();

    /// <summary>画底色与边框：默认悬停时 fillHover 底。</summary>
    protected virtual void PaintSurface(Graphics graphics, ThemeTokens theme, float radius)
    {
        if (!Hovered) return;
        using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
        using var brush = new SolidBrush(theme.Colors.FillHover);
        graphics.FillPath(brush, path);
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Forward(e.Control!);
    }

    void Forward(Control child)
    {
        child.MouseClick += (_, args) => OnMouseClick(args);
        child.MouseEnter += (_, _) => SetHovered(true);
        child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        child.ControlAdded += (_, args) => Forward(args.Control!);
        foreach (Control grandchild in child.Controls) Forward(grandchild);
    }

    void SetHovered(bool value)
    {
        if (Hovered == value) return;
        Hovered = value;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        e.Handled = true;
        OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var radius = Radius(theme);
        PaintSurface(graphics, theme, radius);
        if (!Focused || !ShowFocusCues) return;
        var width = LogicalToDeviceUnits(2);
        using var path = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), radius);
        using var pen = new Pen(theme.Colors.FocusRing, width);
        graphics.DrawPath(pen, path);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new PressableAccessibleObject(this);

    sealed class PressableAccessibleObject(PressableFlex owner) : ControlAccessibleObject(owner)
    {
        public override string DefaultAction => "Press";

        public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
    }
}
