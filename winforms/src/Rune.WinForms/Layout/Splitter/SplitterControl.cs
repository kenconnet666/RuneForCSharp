using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 分栏（vue-tsx-pro 的 Splitter）：两栏并排（或上下），中间的分隔条拖动调整比例，一侧可以收起。收件箱（左列表、右预览）、
/// 主从视图、编辑器与预览这类"两块都要看、比例因人而异"的布局。
/// <code>
/// var splitter = new SplitterControl { Dock = DockStyle.Fill, Ratio = 40, Collapsible = PaneSide.End, Start = list, End = preview };
/// splitter.RatioChanged += (_, _) =&gt; settings.ListRatio = splitter.Ratio;
/// </code>
/// <see cref="Ratio"/> 是起始一栏占的百分比（上游的 size：Control 自己有 Size），拖动时夹在 <see cref="MinRatio"/>–<see cref="MaxRatio"/> 之间；双击分隔条回到初始比例。
/// 分隔条可聚焦（读屏是分隔符，取值是当前比例）：←/→（上下排列时 ↑/↓）每次 5%，Home / End 到最小 / 最大，
/// Enter 收起或展开可收起的那一侧；从右到左排版时起始一栏在右边，左右键也镜像。
/// 写了 <see cref="Collapsible"/> 时分隔条中间有收起、展开的小按钮（不进 Tab 顺序：键盘在分隔条上按 Enter）；收起的一栏不显示，另一栏占满；
/// 拖动或用键盘调比例时自动展开。两栏各自滚动，是浮层滚动条的 <see cref="ScrollAreaControl"/>（两个方向）；外层要给高度。
/// 分隔条占 space.md 宽，看得见的是中间一条 borderMuted 的细线，悬停、拖动时加粗变深，键盘聚焦时加粗变成强调色（粗细没有过渡）。
/// </summary>
[DefaultProperty(nameof(Ratio))]
[DefaultEvent(nameof(RatioChanged))]
[Description("分栏：两栏并排或上下，拖动分隔条调比例，一侧可以收起。")]
public class SplitterControl : Control, IScopeAware
{
    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    readonly ScrollAreaControl startPane = new() { Axis = ScrollAreaAxis.Both };
    readonly ScrollAreaControl endPane = new() { Axis = ScrollAreaAxis.Both };
    readonly IconButtonControl toggle = new() { TextSize = TextSize.Xs, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, TabStop = false };
    readonly CornerClip cornerClip = new();
    double? initialRatio;

    /// <summary>新建：左右并排、各占一半。</summary>
    public SplitterControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor | ControlStyles.ContainerControl, true);
        BackColor = Color.Transparent;
        Divider = new SplitterHandleControl(this);
        // 收起按钮在最上面：叠在分隔线与两栏上
        Controls.AddRange([toggle, Divider, startPane, endPane]);
        toggle.Click += (_, _) => IsCollapsed = !IsCollapsed;
        // 盖住分隔线要不透明的底；悬停换成同样不透明的 fillHover（描边按钮自己的悬停色会透出线）
        toggle.MouseEnter += (_, _) => PaintToggle();
        toggle.MouseLeave += (_, _) => PaintToggle();
        ApplyState();
    }

    /// <summary>起始一栏（左或上）的内容。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Start { get => startPane.Content; set => startPane.Content = value; }

    /// <summary>结束一栏（右或下）的内容。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? End { get => endPane.Content; set => endPane.Content = value; }

    /// <summary>两栏的排法，默认左右并排。</summary>
    [Category("Appearance")]
    public SplitterOrientation Orientation { get => field.Value is null ? SplitterOrientation.Horizontal : field; set { field = value; ApplyState(); } }

    /// <summary>起始一栏占的百分比，默认 50。</summary>
    [Category("Behavior")]
    [DefaultValue(50d)]
    public double Ratio
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            PerformLayout();
            Divider.Changed();
            RatioChanged?.Invoke(this, EventArgs.Empty);
        }
    } = 50;

    /// <summary>起始一栏最少占多少（百分比），默认 20。</summary>
    [Category("Behavior")]
    [DefaultValue(20d)]
    public double MinRatio { get; set; } = 20;

    /// <summary>起始一栏最多占多少（百分比），默认 80。</summary>
    [Category("Behavior")]
    [DefaultValue(80d)]
    public double MaxRatio { get; set; } = 80;

    /// <summary>哪一侧可以收起；null（默认）时不能收起。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public PaneSide? Collapsible { get; set { field = value; ApplyState(); } }

    /// <summary>可收起的那一侧收着。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsCollapsed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            ApplyState();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>分隔条的读屏名称；不写时取语言包（调整分栏）。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public string? Label { get; set; }

    /// <summary>外面一圈边框、圆角 lg 与表面色：独立成块（收件箱的列表与预览）。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Bordered { get; set { field = value; ApplyState(); } }

    /// <summary>比例变了（拖动、按键、双击、代码设的）。</summary>
    public event EventHandler? RatioChanged;

    /// <summary>收起、展开了。</summary>
    public event EventHandler? CollapsedChanged;

    /// <summary>分隔条（Control 自己有 Handle：窗口句柄）。</summary>
    [Browsable(false)]
    public SplitterHandleControl Divider { get; }

    /// <summary>起始一栏（测试用）。</summary>
    internal ScrollAreaControl StartPane => startPane;

    /// <summary>结束一栏（测试用）。</summary>
    internal ScrollAreaControl EndPane => endPane;

    /// <summary>收起、展开按钮（测试用）。</summary>
    internal IconButtonControl Toggle => toggle;

    bool ShouldSerializeOrientation() => Orientation != SplitterOrientation.Horizontal;

    internal bool Vertical => Orientation.Value == "vertical";

    /// <summary>起始一栏在右边：从右到左排版、左右并排时（WinForms 的子控件不随 RightToLeft 镜像，这里自己镜像）。</summary>
    internal bool Mirrored => RightToLeft == RightToLeft.Yes && !Vertical;

    /// <summary>收起着的那一栏。</summary>
    internal PaneSide? Hidden => IsCollapsed ? Collapsible : null;

    float Em => this.EmPixels();

    /// <summary>边框的粗细（Bordered 时 1 像素）。</summary>
    int Line => Bordered ? (int)Math.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f) : 0;

    /// <summary>分隔条的粗细：space.md。</summary>
    internal int TrackThickness => (int)Math.Round(Tokens.Space.Md * Em);

    /// <summary>双击回到的比例：第一次排版时的比例（TS 的初始 size）。</summary>
    internal double InitialRatio => initialRatio ?? Ratio;

    void IScopeAware.ScopeChanged()
    {
        ApplyState();
        Invalidate(true);
    }

    /// <summary>收起的一栏不显示；收起按钮的箭头指向收起的方向、名称是"收起""展开"。</summary>
    void ApplyState()
    {
        startPane.Visible = Hidden != PaneSide.Start;
        endPane.Visible = Hidden != PaneSide.End;
        toggle.Visible = Collapsible is not null;
        if (Collapsible is { } side)
        {
            // 箭头按看到的方向：起始一栏在右边时左右互换
            var toStart = SplitterScale.TogglePointsToStart(side, IsCollapsed);
            toggle.Icon = Vertical ? toStart ? Lucide.ChevronUp : Lucide.ChevronDown : toStart != Mirrored ? Lucide.ChevronLeft : Lucide.ChevronRight;
            var lang = ProviderControl.LangOf(this);
            toggle.AccessibleName = IsCollapsed ? lang.SplitterExpand : lang.SplitterCollapse;
        }
        PaintToggle();
        Divider.Changed();
        PerformLayout();
        Invalidate();
    }

    void PaintToggle()
    {
        var colors = ProviderControl.ThemeOf(this).Colors;
        var hover = toggle.ClientRectangle.Contains(toggle.PointToClient(MousePosition)) && toggle.Visible;
        toggle.Sx = new ButtonSx { Background = hover ? colors.FillHover : colors.Surface };
    }

    /// <summary>改比例：夹在范围里；收着的一侧被拖出来、用键盘调比例时展开。</summary>
    internal void SetRatio(double ratio)
    {
        Ratio = SplitterScale.Fit(ratio, MinRatio, MaxRatio);
        IsCollapsed = false;
    }

    /// <summary>键盘：Enter 收起或展开；方向键（从右到左排版时左右镜像）、Home、End 调比例。</summary>
    internal bool Press(Keys key)
    {
        if (key == Keys.Enter && Collapsible is not null)
        {
            IsCollapsed = !IsCollapsed;
            return true;
        }
        var rtl = Mirrored;
        SplitterKey? action = (key, Vertical) switch
        {
            (Keys.Right, false) => rtl ? SplitterKey.Shrink : SplitterKey.Grow,
            (Keys.Left, false) => rtl ? SplitterKey.Grow : SplitterKey.Shrink,
            (Keys.Down, true) => SplitterKey.Grow,
            (Keys.Up, true) => SplitterKey.Shrink,
            (Keys.Home, _) => SplitterKey.Home,
            (Keys.End, _) => SplitterKey.End,
            _ => null,
        };
        if (action is not { } pressed) return false;
        SetRatio(SplitterScale.Target(pressed, Ratio, MinRatio, MaxRatio));
        return true;
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        initialRatio ??= Ratio;
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        ApplyState();
    }

    /// <summary>两栏与分隔条：分隔条占 space.md，剩下的按比例分（收起的一栏不占位置）；收起按钮在分隔条中间；Bordered 时都在边框里面。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var line = Line;
        var inner = new Rectangle(line, line, Math.Max(0, Width - 2 * line), Math.Max(0, Height - 2 * line));
        var main = Vertical ? inner.Height : inner.Width;
        var track = TrackThickness;
        var (start, _) = SplitterScale.Panes(main - track, Ratio, Hidden);
        var startLength = (int)Math.Round(start);
        var endLength = Math.Max(0, main - track - startLength);
        Rectangle Slot(int offset, int length)
        {
            if (Vertical) return new Rectangle(inner.X, inner.Y + offset, inner.Width, length);
            // 起始一栏在右边时从右往左排
            var x = Mirrored ? inner.Right - offset - length : inner.X + offset;
            return new Rectangle(x, inner.Y, length, inner.Height);
        }
        startPane.Bounds = Slot(0, startLength);
        var handle = Slot(startLength, track);
        Divider.Bounds = handle;
        endPane.Bounds = Slot(startLength + track, endLength);
        var button = toggle.GetPreferredSize(Size.Empty);
        toggle.Bounds = new Rectangle(handle.X + (handle.Width - button.Width) / 2, handle.Y + (handle.Height - button.Height) / 2, button.Width, button.Height);
        cornerClip.Apply([startPane, endPane], inner, Bordered ? Math.Max(0, Radius.Lg.In(ProviderControl.ThemeOf(this).Radii) ?? 0) * Em - line : 0);
    }

    /// <summary>Bordered：surface 底、border 色的圆角边框。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Bordered) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var line = Line;
        var radius = (Radius.Lg.In(ProviderControl.ThemeOf(this).Radii) ?? 0) * Em;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), radius);
        using (var brush = new SolidBrush(colors.Surface)) graphics.FillPath(brush, shape);
        using (var pen = new Pen(colors.Border, line)) graphics.DrawPath(pen, shape);
    }
}

/// <summary>
/// 分栏的分隔条（TS 的 role="separator"）：可聚焦，能拖；看得见的是中间一条细线，平时 borderMuted、border 宽，
/// 悬停、拖动时 borderStrong、加粗到 md，键盘聚焦时强调色、md。读屏是分隔符，取值是起始一栏的比例（收起起始一栏时 0、收起结束一栏时 100）。
/// </summary>
public sealed class SplitterHandleControl : Control
{
    readonly SplitterControl owner;
    bool hovered;
    bool dragging;
    double from;
    Point origin;

    internal SplitterHandleControl(SplitterControl owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
    }

    /// <summary>读屏念的当前值。</summary>
    public double Value => SplitterScale.ValueNow(owner.Ratio, owner.Hidden);

    /// <summary>拖动中（指针离开了分隔条也保持加粗变深）。</summary>
    public bool IsDragging => dragging;

    /// <summary>比例、方向变了：光标跟着，读屏的取值变了。</summary>
    internal void Changed()
    {
        Cursor = owner.Vertical ? Cursors.HSplit : Cursors.VSplit;
        Invalidate();
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && owner.Press(e.KeyCode)) e.Handled = true;
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
        // 只认主键
        if (e.Button != MouseButtons.Left) return;
        if (e.Clicks >= 2)
        {
            owner.Ratio = SplitterScale.Fit(owner.InitialRatio, owner.MinRatio, owner.MaxRatio);
            return;
        }
        // 按分栏的坐标量：分隔条自己随拖动移动
        origin = owner.PointToClient(PointToScreen(e.Location));
        from = owner.Ratio;
        dragging = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        var point = owner.PointToClient(PointToScreen(e.Location));
        var vertical = owner.Vertical;
        var delta = vertical ? point.Y - origin.Y : (point.X - origin.X) * (owner.Mirrored ? -1 : 1);
        owner.SetRatio(SplitterScale.Drag(from, delta, vertical ? owner.Height : owner.Width, owner.MinRatio, owner.MaxRatio));
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

    /// <summary>线：平时 xs、borderMuted，悬停、拖动 md、borderStrong，键盘聚焦 md、强调色。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var focusVisible = Focused && ShowFocusCues;
        var strong = hovered || dragging || focusVisible;
        var color = focusVisible ? colors.Accent : strong ? colors.BorderStrong : colors.BorderMuted;
        var line = Math.Max(1, (int)Math.Round((strong ? Tokens.BorderWidth.Md : Tokens.BorderWidth.Xs) * DeviceDpi / 96f));
        var rect = owner.Vertical ? new Rectangle(0, (Height - line) / 2, Width, line) : new Rectangle((Width - line) / 2, 0, line, Height);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, rect);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new HandleAccessibleObject(this);

    /// <summary>读屏：分隔符，名称是 Label 或语言包；取值是起始一栏的比例（可写）。</summary>
    sealed class HandleAccessibleObject(SplitterHandleControl handle) : ControlAccessibleObject(handle)
    {
        SplitterControl Splitter => handle.owner;

        public override AccessibleRole Role => AccessibleRole.Separator;

        public override string Name => Splitter.Label ?? ProviderControl.LangOf(Splitter).SplitterResize;

        public override string? Value
        {
            get => handle.Value.ToString(CultureInfo.CurrentCulture);
            set
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) Splitter.SetRatio(number);
            }
        }

        public override AccessibleStates State => base.State | AccessibleStates.Focusable;
    }
}
