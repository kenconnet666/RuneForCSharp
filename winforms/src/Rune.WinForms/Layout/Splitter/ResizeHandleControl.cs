using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 宽度调整手柄（vue-tsx-pro 的 ResizeHandle）：放进要调整的那个控件（面板）里，自己贴着它的结束边
/// （<see cref="Edge"/>=Start 时是起始边，如从结束边滑入的抽屉）、叠在内容上面，space.sm 宽，悬停、拖动时 borderStrong，键盘聚焦时强调色。
/// <code>
/// var panel = new Panel { Width = 220, Dock = DockStyle.Left };
/// panel.Controls.Add(new ResizeHandleControl { AccessibleName = "调整列表宽度", MinLength = 160, MaxLength = 360, DefaultLength = 220 });
/// </code>
/// 拖动或聚焦后用 ← / →（拖动的键盘替代）调整，每次 <see cref="Step"/>，从右到左排版时方向镜像（手柄也在左边）；写了 <see cref="DefaultLength"/> 时双击回到它。
/// 改的是父控件的 Width（<see cref="Orientation"/>=Horizontal 时是一条横线，贴着下边或上边，改 Height，用 ↑ / ↓），
/// 值已经夹在 <see cref="MinLength"/>–<see cref="MaxLength"/> 之间、取整像素；要记住时听 <see cref="LengthChanged"/>。
/// 读屏是分隔符，名称写在 AccessibleName 上，取值是父控件当前的宽度。
/// </summary>
[DefaultEvent(nameof(LengthChanged))]
[Description("宽度调整手柄：拖动或用方向键调整所在控件的宽度（或高度）。")]
public class ResizeHandleControl : Control
{
    Control? target;
    bool hovered;
    bool dragging;
    int from;
    Point origin;

    /// <summary>新建：贴着父控件结束边的竖线。</summary>
    public ResizeHandleControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.VSplit;
    }

    /// <summary>最小长度（像素），默认 64。</summary>
    [Category("Behavior")]
    [DefaultValue(64)]
    public int MinLength { get; set; } = 64;

    /// <summary>最大长度（像素），默认不限。</summary>
    [Category("Behavior")]
    [DefaultValue(int.MaxValue)]
    public int MaxLength { get; set; } = int.MaxValue;

    /// <summary>双击回到的长度（像素）；不写时双击不做什么。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? DefaultLength { get; set; }

    /// <summary>键盘每按一次调整多少（像素），默认 16。</summary>
    [Category("Behavior")]
    [DefaultValue(16)]
    public int Step { get; set; } = 16;

    /// <summary>手柄在父控件的哪一边，默认结束边；横线时起始是上边、结束是下边。</summary>
    [Category("Layout")]
    [DefaultValue(PaneSide.End)]
    public PaneSide Edge { get; set { field = value; Place(); } } = PaneSide.End;

    /// <summary>Vertical（默认）：竖线，调整宽度；Horizontal：横线，调整高度。</summary>
    [Category("Layout")]
    [DefaultValue(Orientation.Vertical)]
    public Orientation Orientation
    {
        get;
        set
        {
            field = value;
            Cursor = Horizontal ? Cursors.HSplit : Cursors.VSplit;
            Place();
        }
    } = Orientation.Vertical;

    /// <summary>拖动、按键调整了父控件的长度（已经夹在范围里）。</summary>
    public event EventHandler<int>? LengthChanged;

    /// <summary>拖动中。</summary>
    [Browsable(false)]
    public bool IsDragging => dragging;

    /// <summary>父控件当前的长度。</summary>
    [Browsable(false)]
    public int Length => Override is { } custom ? custom.Read() : target is null ? MinLength : Horizontal ? target.Height : target.Width;

    /// <summary>调整的不是父控件时（DrawerControl 的面板宽度）：读当前长度、写新长度；位置由使用方摆。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal (Func<int> Read, Action<int> Write)? Override { get; set; }

    bool Horizontal => Orientation == Orientation.Horizontal;

    bool RightToLeftLayout => RightToLeft == RightToLeft.Yes;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (target is not null) target.Layout -= OnTargetLayout;
        target = Parent;
        if (target is not null) target.Layout += OnTargetLayout;
        Place();
    }

    void OnTargetLayout(object? sender, LayoutEventArgs e) => Place();

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Place();
    }

    /// <summary>贴边并叠在最上面：竖线在结束（起始）边、上下铺满；横线在下（上）边、左右铺满。</summary>
    void Place()
    {
        if (target is null || Override is not null) return;
        var thickness = (int)Math.Round(Tokens.Space.Sm * this.EmPixels());
        var client = target.ClientSize;
        // 竖线：结束边在右（从右到左排版时在左）
        var atRight = (Edge == PaneSide.End) != RightToLeftLayout;
        Bounds = Horizontal
            ? new Rectangle(0, Edge == PaneSide.End ? client.Height - thickness : 0, client.Width, thickness)
            : new Rectangle(atRight ? client.Width - thickness : 0, 0, thickness, client.Height);
        BringToFront();
    }

    /// <summary>改父控件的长度（夹在范围里、取整），通知使用方。</summary>
    internal void SetLength(double length)
    {
        var next = (int)SplitterScale.FitLength(length, MinLength, MaxLength);
        if (Override is { } custom) custom.Write(next);
        else if (target is null) return;
        else if (Horizontal) target.Height = next;
        else target.Width = next;
        LengthChanged?.Invoke(this, next);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    /// <summary>键盘：竖线 ← / →（从右到左排版时镜像）、横线 ↑ / ↓，每次一个 Step。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double delta = (e.KeyCode, Horizontal) switch
        {
            (Keys.Right, false) or (Keys.Down, true) => Step,
            (Keys.Left, false) or (Keys.Up, true) => -Step,
            _ => 0,
        };
        if (delta == 0) return;
        e.Handled = true;
        SetLength(Length + SplitterScale.LengthDelta(delta, Edge, !Horizontal && RightToLeftLayout));
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (e.Clicks >= 2)
        {
            if (DefaultLength is { } length) SetLength(length);
            return;
        }
        // 按屏幕坐标量：父控件与手柄都随拖动移动、变大
        origin = PointToScreen(e.Location);
        from = Length;
        dragging = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        var point = PointToScreen(e.Location);
        var delta = Horizontal ? point.Y - origin.Y : point.X - origin.X;
        SetLength(from + SplitterScale.LengthDelta(delta, Edge, !Horizontal && RightToLeftLayout));
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture) return;
        dragging = false;
        Invalidate();
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
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Place();
    }

    /// <summary>悬停、拖动时 borderStrong，键盘聚焦时强调色，平时透明。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        Color? fill = Focused && ShowFocusCues ? colors.Accent : hovered || dragging ? colors.BorderStrong : null;
        if (fill is not { } color) return;
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && target is not null) target.Layout -= OnTargetLayout;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ResizeHandleAccessibleObject(this);

    /// <summary>读屏：分隔符，取值是父控件的长度（可写）。</summary>
    sealed class ResizeHandleAccessibleObject(ResizeHandleControl handle) : ControlAccessibleObject(handle)
    {
        public override AccessibleRole Role => AccessibleRole.Separator;

        public override string? Value
        {
            get => handle.Length.ToString(CultureInfo.CurrentCulture);
            set
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) handle.SetLength(number);
            }
        }

        public override AccessibleStates State => base.State | AccessibleStates.Focusable;
    }
}
