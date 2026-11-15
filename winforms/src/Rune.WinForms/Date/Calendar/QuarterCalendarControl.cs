using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 季度面板（vue-tsx-pro 的 QuarterCalendar）：一年一页的四个季度（季度报表、考核周期），翻页换年；选一个季度，或选一段区间（先开始、再结束）。
/// DatePicker、DateRangePicker 的季度粒度弹出的就是它，也可以单独放在页面上。
/// <code>
/// var quarter = new QuarterCalendarControl { Value = "2026-Q4", AccessibleName = "考核季度" };
/// var span = new QuarterCalendarControl { SelectionMode = CalendarSelectionMode.Range };   // RangeValue：2026-Q1 到 2026-Q3
/// </code>
/// 外观与 CalendarControl 的月份视图一致（同一个画格子的方法）：一行四格，区间连成一条，本季度带描边。
/// 键盘：←→ 移动（到头换年；从右到左时反过来），Home / End 到第一、第四季度，Page Up / Down 换年，Enter / 空格选中；区间选到一半时悬停预览，Esc 取消。
/// 整段在 Min–Max 之外的季度不可用。状态是孪生文件 <see cref="QuarterState"/>；四格是自己画的 <see cref="QuarterGridControl"/>（读屏的表格，格子是子对象）。
/// </summary>
public class QuarterCalendarControl : Control, IScopeAware
{
    readonly IconButtonControl previous = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly IconButtonControl next = new() { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly TextControl heading = new() { Weight = TextWeight.Medium };
    readonly QuarterGridControl grid;
    bool writing;

    /// <summary>新建：今年。</summary>
    public QuarterCalendarControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.Grouping;
        State = new QuarterState
        {
            IsRange = () => SelectionMode == CalendarSelectionMode.Range,
            Selected = () => Value,
            SelectedRange = () => RangeValue,
            IsDisabled = key => DateFieldRange.IsOutOfRange(key, Min, Max, null, null),
            ReadOnly = () => IsReadOnly || !Enabled,
        };
        grid = new QuarterGridControl(this);
        previous.Click += (_, _) => TurnYear(-1);
        next.Click += (_, _) => TurnYear(1);
        Controls.AddRange([previous, heading, next, grid]);
    }

    /// <summary>字号档位，格子随之缩放；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>单选时选中的季度（2026-Q4）；没有时 null。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>选区间时选中的区间（两端是季度）；没有时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IsoDateRange? RangeValue
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>选择方式：Single（默认）或 Range。</summary>
    [Category("Behavior")]
    [DefaultValue(CalendarSelectionMode.Single)]
    public CalendarSelectionMode SelectionMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, CalendarSelectionMode.Multiple);
            field = value;
            Sync();
        }
    }

    /// <summary>最早可选的日期（ISO）：整个季度都早于它时不可用。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Min { get; set { field = value; Sync(); } }

    /// <summary>最晚可选的日期（ISO）：整个季度都晚于它时不可用。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Max { get; set { field = value; Sync(); } }

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; Sync(); } }

    /// <summary>值变了（点选、外面改值）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>选好了（单选选了一个，区间选了结束）：日期选择器据此关闭弹层。</summary>
    public event EventHandler? Picked;

    /// <summary>季度面板的状态。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal QuarterState State { get; }

    /// <summary>画四格的网格（测试用）。</summary>
    internal QuarterGridControl Grid => grid;

    /// <summary>年份标题、上一年、下一年（测试用）。</summary>
    internal (TextControl Heading, IconButtonControl Previous, IconButtonControl Next) Header => (heading, previous, next);

    internal Lang Lang => ProviderControl.LangOf(this);

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void IScopeAware.ScopeChanged() => Sync();

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Sync();
    }

    void TurnYear(int direction)
    {
        State.TurnYear(direction);
        Sync();
    }

    // 点格子写回的已经刷新过；外面改的值（快捷按钮、重置）翻到它那一年
    void SyncValue()
    {
        if (writing || !IsHandleCreated) return;
        State.SelectionChanged();
        Sync();
    }

    /// <summary>点了一格（鼠标、Enter、读屏的默认动作）：写回值，选好了发 Picked。</summary>
    internal void Activate(string key)
    {
        if (!Enabled) return;
        if (State.Activate(key) is { } picked)
        {
            writing = true;
            try
            {
                if (SelectionMode == CalendarSelectionMode.Range) RangeValue = picked;
                else Value = picked.Start;
            }
            finally
            {
                writing = false;
            }
            Picked?.Invoke(this, EventArgs.Empty);
        }
        Sync();
    }

    /// <summary>按状态刷新：年份标题、翻页按钮、四格；没有窗口句柄前不做（状态要等今天、值都给了才开始）。</summary>
    internal void Sync(bool focus = false)
    {
        if (!IsHandleCreated) return;
        var lang = Lang;
        var rtl = RightToLeft == RightToLeft.Yes;
        // 图标不随从右到左镜像：箭头按方向换，保持指向翻页的方向
        previous.Icon = rtl ? Lucide.ChevronRight : Lucide.ChevronLeft;
        next.Icon = rtl ? Lucide.ChevronLeft : Lucide.ChevronRight;
        previous.AccessibleName = lang.CalendarPreviousYear;
        next.AccessibleName = lang.CalendarNextYear;
        previous.Enabled = next.Enabled = Enabled;
        heading.Text = lang.CalendarYear(State.Year);
        grid.Sync();
        PerformLayout();
        if (focus) grid.Focus();
    }

    /// <summary>页头一行（翻页按钮、居中的年份），下面隔 xs 是四格；从右往左时页头镜像。</summary>
    (Rectangle Previous, Rectangle Heading, Rectangle Next, Rectangle Grid, Size Size) Arrange()
    {
        var xs = (int)MathF.Round(Tokens.Space.Xs * this.EmPixels());
        var gridSize = grid.GetPreferredSize(Size.Empty);
        var button = previous.PreferredSize.Width;
        var headingSize = heading.PreferredSize;
        var row = Math.Max(button, headingSize.Height);
        var width = Math.Max(gridSize.Width, 2 * button + headingSize.Width);
        var start = new Rectangle(0, (row - button) / 2, button, button);
        var end = new Rectangle(width - button, (row - button) / 2, button, button);
        if (RightToLeft == RightToLeft.Yes) (start, end) = (end, start);
        return (start, new Rectangle((width - headingSize.Width) / 2, (row - headingSize.Height) / 2, headingSize.Width, headingSize.Height), end,
            new Rectangle(0, row + xs, gridSize.Width, gridSize.Height), new Size(width, row + xs + gridSize.Height));
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Arrange().Size;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var (start, middle, end, cells, size) = Arrange();
        previous.Bounds = start;
        heading.Bounds = middle;
        next.Bounds = end;
        grid.Bounds = cells;
        if (AutoSize && Size != size) Size = size;
    }
}

/// <summary>
/// 季度面板的四格（<see cref="QuarterCalendarControl"/> 建的）：自己画，焦点在这一个控件上，←→ 在格子之间移动。
/// 读屏：表格，名称是年份；每格是子对象（名称"2026年第4季度"，状态是已选、不可用、焦点）。
/// </summary>
[ToolboxItem(false)]
public sealed class QuarterGridControl : Control
{
    readonly QuarterCalendarControl owner;
    int? hovered;

    internal QuarterGridControl(QuarterCalendarControl owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        AccessibleRole = AccessibleRole.Table;
        Cursor = Cursors.Hand;
    }

    /// <summary>四格的状态（测试用）。</summary>
    internal IReadOnlyList<QuarterCellState> Cells => [.. Enumerable.Range(1, 4).Select(State.Cell)];

    QuarterState State => owner.State;

    float Em => this.EmPixels();

    /// <summary>一格的大小：与日期网格同宽（7 × max(2.25em, 24)）分四份，高 max(3em, 24)。</summary>
    Size CellSize
    {
        get
        {
            var target = Tokens.Size.MinTarget * DeviceDpi / 96f;
            return new Size((int)MathF.Ceiling(7 * MathF.Max(2.25f * Em, target) / 4), (int)MathF.Ceiling(MathF.Max(3 * Em, target)));
        }
    }

    /// <summary>第几季度（1–4）所在的格子；从右往左时第一季度在右边。</summary>
    Rectangle BoundsOf(int quarter)
    {
        var size = CellSize;
        var column = owner.RightToLeft == RightToLeft.Yes ? 4 - quarter : quarter - 1;
        return new Rectangle(column * size.Width, 0, size.Width, size.Height);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => new(4 * CellSize.Width, CellSize.Height);

    /// <summary>状态变了：读屏名称、提示与重画。</summary>
    internal void Sync()
    {
        AccessibleName = owner.Lang.CalendarYear(State.Year);
        AccessibleDescription = State.Anchor is null ? null : owner.Lang.CalendarPickEnd;
        Size = GetPreferredSize(Size.Empty);
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }

    /// <summary>读屏念的季度：2026年第4季度。</summary>
    internal string NameOf(int quarter) => owner.Lang.DateFieldPeriodPatterns.Quarter
        .Replace("{year}", State.Year.ToString(CultureInfo.InvariantCulture)).Replace("{quarter}", quarter.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = theme.Radii.Md * Em;
        var range = State.IsRange();
        var rtl = owner.RightToLeft == RightToLeft.Yes;
        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var cell = State.Cell(quarter);
            var bounds = BoundsOf(quarter);
            var ends = cell.RangeStart || cell.RangeEnd;
            var first = quarter == 1 || cell.RangeStart;
            var last = quarter == 4 || cell.RangeEnd;
            var look = new CalendarCellLook(range ? ends : cell.Selected, cell.InRange && !ends, cell.InRange, rtl ? last : first, rtl ? first : last,
                cell.Today, false, false, !owner.Enabled || cell.Disabled, hovered == quarter);
            var fore = CalendarCellPainter.PaintBackground(e.Graphics, this, colors, radius, bounds, look, owner.Enabled);
            CalendarCellPainter.PaintText(e.Graphics, owner.Lang.CalendarQuarter(quarter), Font, bounds, fore, look);
            if (Focused && ShowFocusCues && cell.Focused) CalendarCellPainter.PaintFocus(e.Graphics, this, colors, radius, bounds);
        }
    }

    int? HitTest(Point point) => Enumerable.Range(1, 4).Cast<int?>().FirstOrDefault(quarter => BoundsOf(quarter!.Value).Contains(point));

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointAt(HitTest(e.Location));
    }

    /// <summary>指针移到了第几季度（null 是格子以外）：悬停的底色；区间选到一半时预览。</summary>
    internal void PointAt(int? quarter)
    {
        if (quarter == hovered) return;
        hovered = quarter;
        State.Hover(quarter is { } at ? State.Cell(at).Key : null);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        PointAt(null);
    }

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is { } quarter) owner.Activate(State.Cell(quarter).Key);
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
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Escape
        || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Press(e.KeyData)) e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>按键：←→、Home / End、Page Up / Down 移动焦点，Enter / 空格选中，Esc 取消选到一半的区间。返回是否处理了。</summary>
    internal bool Press(Keys keys)
    {
        if (!owner.Enabled || (keys & (Keys.Control | Keys.Alt)) != 0) return false;
        var code = keys & Keys.KeyCode;
        if (code is Keys.Enter or Keys.Space)
        {
            owner.Activate(State.Focused);
            return true;
        }
        CalendarKey? key = code switch
        {
            Keys.Left => CalendarKey.Left,
            Keys.Right => CalendarKey.Right,
            Keys.PageUp => CalendarKey.PageUp,
            Keys.PageDown => CalendarKey.PageDown,
            Keys.Home => CalendarKey.Home,
            Keys.End => CalendarKey.End,
            Keys.Escape => CalendarKey.Escape,
            _ => null,
        };
        if (key is not { } pressed || !State.HandleKey(pressed, owner.RightToLeft == RightToLeft.Yes)) return false;
        owner.Sync(focus: true);
        return true;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GridAccessibleObject(this);

    sealed class GridAccessibleObject(QuarterGridControl owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount() => 4;

        public override AccessibleObject? GetChild(int index) => index is >= 0 and < 4 ? new CellAccessibleObject(owner, this, index + 1) : null;

        public override AccessibleObject? GetFocused() =>
            owner.Focused && Enumerable.Range(1, 4).FirstOrDefault(quarter => owner.State.Cell(quarter).Focused) is var quarter and > 0
                ? new CellAccessibleObject(owner, this, quarter)
                : base.GetFocused();
    }

    sealed class CellAccessibleObject(QuarterGridControl owner, AccessibleObject parent, int quarter) : AccessibleObject
    {
        QuarterCellState Cell => owner.State.Cell(quarter);

        public override AccessibleObject Parent => parent;

        public override AccessibleRole Role => AccessibleRole.Cell;

        public override string Name => owner.NameOf(quarter);

        public override string? Description => owner.AccessibleDescription;

        public override string DefaultAction => "Select";

        public override AccessibleStates State => AccessibleStates.Selectable | AccessibleStates.Focusable
            | (Cell.Selected ? AccessibleStates.Selected : 0)
            | (Cell.Focused && owner.Focused ? AccessibleStates.Focused : 0)
            | (!owner.owner.Enabled || Cell.Disabled ? AccessibleStates.Unavailable : 0);

        public override Rectangle Bounds => owner.RectangleToScreen(owner.BoundsOf(quarter));

        public override void DoDefaultAction() => owner.owner.Activate(Cell.Key);
    }
}
