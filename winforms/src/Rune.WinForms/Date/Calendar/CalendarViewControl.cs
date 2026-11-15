using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日历的界面（vue-tsx-pro 的 CalendarView，WAI-ARIA 的日期网格）：页头（上一页、标题、下一页）、并排的月份网格、月份视图与年份视图、页脚。
/// 状态由派生类给的 <see cref="CalendarState"/>（CalendarControl、RangeCalendarControl、日期选择器的弹层共用）。
/// <list type="bullet">
/// <item>每个月是一张自己画的网格（<see cref="CalendarGridControl"/>）：只有焦点日期所在的那张在 Tab 顺序里，键盘在格子之间移动、跨月时翻页；</item>
/// <item>标题按钮切到月份视图、再切到年份视图（不比选择的粒度更细）；翻页、换视图时网格的读屏名称换成新的标题；</item>
/// <item>并排几个月时只显示本月的日期（补齐的上下月留空）；只有一个月时补齐的日期变淡、可以点。</item>
/// </list>
/// 外观同上游：翻页按钮是 ghost 的图标按钮，标题是 ghost 的按钮；星期表头与周数 textMuted；日期格 max(<see cref="CellSizeEm"/> em, 24) 见方、md 圆角、悬停 fillHover；
/// 今天 semibold 加 accentBorder 细圈；已选 accent 底、accentContrast 字；区间中间 accentText 字、底色 accentSoft 连成一条；补齐的日期 textMuted；不可选的划掉；不能选的变淡。
/// 自定义格子：<see cref="CellText"/> 换掉格子里的文字（读屏念在完整日期后面），<see cref="DrawCell"/> 自己画（同 ListBox 的 DrawItem）。
/// </summary>
[ToolboxItem(false)]
public class CalendarViewControl : Control, IScopeAware
{
    readonly List<Control> header = [];
    readonly List<CalendarGridControl> grids = [];
    string? structure;

    /// <summary>新建：没有状态时什么都不画（派生类在构造时给）。</summary>
    public CalendarViewControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.Grouping;
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

    /// <summary>在每行前显示一年里的第几周。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool ShowWeekNumbers { get; set { field = value; Rebuild(); } }

    /// <summary>总是 6 行（翻页时高度不跳），默认 true。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool FixedWeeks { get; set { field = value; Rebuild(); } } = true;

    /// <summary>一周的第一天；不写时按语言包、再按区域惯例。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public DayOfWeek? FirstDayOfWeek { get; set { field = value; Rebuild(); } }

    /// <summary>格子的边长（em 系数，不小于 24px），默认 2.25；放得下价格这类内容时调大。</summary>
    [Category("Appearance")]
    [DefaultValue(2.25f)]
    public float CellSizeEm { get; set { field = value; Rebuild(); } } = 2.25f;

    /// <summary>页脚：今天、清除这类按钮。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Footer
    {
        get;
        set
        {
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is not null) Controls.Add(value);
            PerformLayout();
        }
    }

    /// <summary>自定义日期格的文字（价格、标记）：拿到日期与这一格的状态；返回 null 时是日期数字。读屏先念完整日期再念这里的文字。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<DateOnly, CalendarCellState, string?>? CellText { get; set { field = value; Rebuild(); } }

    /// <summary>画日期格（底色、今天的细圈已经画好）：设 <see cref="DrawCalendarCellEventArgs.Handled"/> 后不再画默认的文字。</summary>
    public event EventHandler<DrawCalendarCellEventArgs>? DrawCell;

    /// <summary>选择无效（多选、区间里有不可选的日期）。</summary>
    [Browsable(false)]
    public bool IsInvalid => State?.Invalid ?? false;

    /// <summary>日历状态（派生类给）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal CalendarState? State { get; private protected set; }

    /// <summary>每月一张的网格（测试用）。</summary>
    internal IReadOnlyList<CalendarGridControl> Grids => grids;

    /// <summary>页头里的控件（测试用）：上一页、标题、下一页（并排几个月时每月一组）。</summary>
    internal IReadOnlyList<Control> Header => header;

    internal Lang Lang => ProviderControl.LangOf(this);

    internal CultureInfo Culture => CalendarText.CultureOf(Lang);

    /// <summary>一周的第一天：自己写的，否则语言包的，否则区域惯例。</summary>
    internal DayOfWeek FirstDay => CalendarText.FirstDayOf(Lang, FirstDayOfWeek);

    internal float Em => this.EmPixels();

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void IScopeAware.ScopeChanged() => Rebuild();

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Rebuild();
    }

    internal void RaiseDrawCell(DrawCalendarCellEventArgs e) => DrawCell?.Invoke(this, e);

    internal bool OwnerDraws => DrawCell is not null;

    /// <summary>
    /// 状态变了（选了、翻页、换视图、外面改值）：页面没换时只重画格子，换了页、视图或选项时重建页头与网格；focus 时把焦点交给焦点日期所在的网格。
    /// </summary>
    /// <param name="focus">把焦点移到焦点格。</param>
    internal void Rebuild(bool focus = false)
    {
        // 没有窗口句柄前不建：属性初始化的先后不定，状态要等各项（DefaultFocused、今天）都给了才开始
        if (State is not { } state || !IsHandleCreated) return;
        var lang = Lang;
        var view = state.View;
        var shown = state.Months;
        var key = string.Join("|", view, string.Join(",", shown), ShowWeekNumbers, FixedWeeks, FirstDay, lang.Code, Enabled, Font.Size, RightToLeft, CellSizeEm, CellText is null);
        if (key != structure)
        {
            structure = key;
            Build(state, lang, view, shown);
        }
        foreach (var grid in grids) grid.Sync();
        if (focus) grids.FirstOrDefault(grid => grid.TabStop)?.Focus();
    }

    void Build(CalendarState state, Lang lang, DateUnit view, IReadOnlyList<DateOnly> shown)
    {
        SuspendLayout();
        foreach (var control in header.Concat<Control>(grids)) control.Dispose();
        header.Clear();
        grids.Clear();
        var culture = Culture;
        var rtl = RightToLeft == RightToLeft.Yes;
        var coarser = view == DateUnit.Day ? DateUnit.Month : view == DateUnit.Month ? DateUnit.Year : (DateUnit?)null;
        var (previousLabel, nextLabel) = view switch
        {
            DateUnit.Day => (lang.CalendarPreviousMonth, lang.CalendarNextMonth),
            DateUnit.Month => (lang.CalendarPreviousYear, lang.CalendarNextYear),
            _ => (lang.CalendarPreviousDecade, lang.CalendarNextDecade),
        };
        for (var index = 0; index < shown.Count; index++)
        {
            var month = shown[index];
            var title = CalendarText.Title(month, view, culture, lang);
            // 图标不随从右到左镜像：箭头按方向换，保持指向翻页的方向
            header.Add(index == 0 ? PageButton(rtl ? Lucide.ChevronRight : Lucide.ChevronLeft, previousLabel, -1) : new Control { Visible = false });
            if (coarser is { } next && Enabled)
            {
                var button = new ButtonControl { Text = title, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = next == DateUnit.Month ? lang.CalendarChooseMonth(title) : lang.CalendarChooseYear(title) };
                button.Click += (_, _) =>
                {
                    state.SetView(next);
                    Rebuild(focus: true);
                };
                header.Add(button);
            }
            // 已经是最粗的视图（或不可用）：标题只是文字
            else header.Add(new TextControl { Text = title, Weight = TextWeight.Medium });
            header.Add(index == shown.Count - 1 ? PageButton(rtl ? Lucide.ChevronLeft : Lucide.ChevronRight, nextLabel, 1) : new Control { Visible = false });
            grids.Add(new CalendarGridControl(this, month, view != DateUnit.Day, shown.Count == 1, title));
        }
        Controls.AddRange([.. header, .. grids]);
        ResumeLayout();
        PerformLayout();
    }

    IconButtonControl PageButton(IconData icon, string label, int direction)
    {
        var button = new IconButtonControl { Icon = icon, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = label, Enabled = Enabled && State!.CanTurnPage(direction) };
        button.Click += (_, _) =>
        {
            State!.TurnPage(direction);
            Rebuild();
            // 到头时按钮变得不可用：焦点留在日历里
            if (!button.IsDisposed && button.Enabled) button.Focus();
            else grids.FirstOrDefault(grid => grid.TabStop)?.Focus();
        };
        return button;
    }

    /// <summary>各块的位置：每个月一列（页头一行三格、下面是网格），并排的月份隔 xl；页脚在下面，隔 sm。</summary>
    (List<Rectangle> Header, List<Rectangle> Grids, Rectangle Footer, Size Size) Arrange()
    {
        var em = Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var sm = (int)MathF.Round(Tokens.Space.Sm * em);
        var xl = (int)MathF.Round(Tokens.Space.Xl * em);
        var rtl = RightToLeft == RightToLeft.Yes;
        var headers = new List<Rectangle>();
        var gridBounds = new List<Rectangle>();
        var x = 0;
        var height = 0;
        for (var index = 0; index < grids.Count; index++)
        {
            var grid = grids[index].PreferredSize;
            var (previous, heading, next) = (header[3 * index], header[3 * index + 1], header[3 * index + 2]);
            var button = Math.Max(previous.Visible ? previous.PreferredSize.Width : 0, next.Visible ? next.PreferredSize.Width : 0);
            if (button == 0) button = new IconButtonControl().PreferredSize.Width;
            var row = Math.Max(heading.PreferredSize.Height, button);
            var width = Math.Max(grid.Width, 2 * button + heading.PreferredSize.Width);
            var headingSize = heading.PreferredSize;
            headers.Add(new Rectangle(x, (row - button) / 2, button, button));
            headers.Add(new Rectangle(x + (width - headingSize.Width) / 2, (row - headingSize.Height) / 2, headingSize.Width, headingSize.Height));
            headers.Add(new Rectangle(x + width - button, (row - button) / 2, button, button));
            gridBounds.Add(new Rectangle(x + (width - grid.Width) / 2, row + xs, grid.Width, grid.Height));
            height = Math.Max(height, row + xs + grid.Height);
            x += width + xl;
        }
        var total = Math.Max(0, x - xl);
        if (rtl)
        {
            // 从右往左：整排镜像（第一个月、上一页在右边）
            headers = [.. headers.Select(r => new Rectangle(total - r.Right, r.Y, r.Width, r.Height))];
            gridBounds = [.. gridBounds.Select(r => new Rectangle(total - r.Right, r.Y, r.Width, r.Height))];
        }
        var footer = Rectangle.Empty;
        if (Footer is { Visible: true } control)
        {
            var size = control.GetPreferredSize(new Size(total, 0));
            footer = new Rectangle(0, height + sm, Math.Max(total, size.Width), size.Height);
            height = footer.Bottom;
            total = Math.Max(total, footer.Width);
        }
        return (headers, gridBounds, footer, new Size(total, height));
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => grids.Count == 0 ? Size.Empty : Arrange().Size;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (grids.Count == 0) return;
        var (headers, gridBounds, footer, size) = Arrange();
        for (var i = 0; i < header.Count; i++) header[i].Bounds = headers[i];
        for (var i = 0; i < grids.Count; i++) grids[i].Bounds = gridBounds[i];
        if (Footer is { Visible: true } control) control.Bounds = footer;
        if (AutoSize && Size != size) Size = size;
    }
}

/// <summary>
/// 一个月（或一年、十年）的网格：自己画的格子，键盘在格子之间移动（焦点只在这一张网格上，格子是读屏的子对象）。
/// 读屏：表格，名称是这一页的标题；每格是子对象（名称是完整日期，今天加"今天"，自定义文字跟在后面），周数是行表头。
/// </summary>
[ToolboxItem(false)]
public sealed class CalendarGridControl : Control
{
    readonly CalendarViewControl owner;
    readonly bool period;
    readonly bool single;
    List<Slot> slots = [];
    DateOnly? hovered;

    internal CalendarGridControl(CalendarViewControl owner, DateOnly page, bool period, bool single, string title)
    {
        this.owner = owner;
        Page = page;
        this.period = period;
        this.single = single;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Table;
        AccessibleName = title;
        Cursor = Cursors.Hand;
    }

    /// <summary>这一页（日视图是那个月的 1 日；月、年视图是 State.Page）。</summary>
    internal DateOnly Page { get; }

    /// <summary>显示着的格子（测试用）：日期、状态、位置。</summary>
    internal IEnumerable<(DateOnly Date, CalendarCellState State, Rectangle Bounds)> Cells => slots.Where(slot => slot.Cell is not null).Select(slot => (slot.Date, slot.Cell!, slot.Bounds));

    /// <summary>星期表头（测试用）：短的写法与全称。</summary>
    internal IReadOnlyList<(string Short, string Full)> Weekdays { get; private set; } = [];

    /// <summary>周数（测试用）：数字与读屏名称。</summary>
    internal IReadOnlyList<(string Text, string Name)> WeekNumbers => [.. slots.Where(slot => slot.WeekNumber is not null).Select(slot => (slot.WeekNumber!, owner.Lang.CalendarWeekNumber(int.Parse(slot.WeekNumber!, CultureInfo.InvariantCulture))))];

    CalendarState State => owner.State!;

    float Em => this.EmPixels();

    float Side => MathF.Max(owner.CellSizeEm * Em, Tokens.Size.MinTarget * DeviceDpi / 96f);

    /// <summary>一格（或表头、周数）：日期格有状态；补齐留空的格只有位置。</summary>
    sealed record Slot(DateOnly Date, CalendarCellState? Cell, Rectangle Bounds, bool RowStart, bool RowEnd, string? WeekNumber = null, string? Header = null, string? Tip = null);

    /// <summary>按状态重算格子（选中、焦点、悬停预览变了），重画；焦点日期在这一页时进 Tab 顺序。</summary>
    internal void Sync()
    {
        slots = Layout();
        TabStop = slots.Any(slot => slot.Cell is { Focused: true, Outside: false });
        AccessibleDescription = State.Anchor is null ? null : owner.Lang.CalendarPickEnd;
        Size = GetPreferredSize(Size.Empty);
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
    }

    new List<Slot> Layout()
    {
        var result = new List<Slot>();
        var state = State;
        var side = (int)MathF.Ceiling(Side);
        if (period)
        {
            var items = state.View == DateUnit.Month ? CalendarState.MonthsOfYear(state.Page) : CalendarState.YearsOfDecade(state.Page);
            var width = 7 * side / 3;
            var height = (int)MathF.Ceiling(MathF.Max(3 * Em, Tokens.Size.MinTarget * DeviceDpi / 96f));
            for (var i = 0; i < items.Count; i++)
                result.Add(new Slot(items[i], state.Cell(items[i]), new Rectangle(i % 3 * width, i / 3 * height, width, height), i % 3 == 0, i % 3 == 2));
            return Mirror(result);
        }
        var culture = owner.Culture;
        var first = owner.FirstDay;
        var weeks = CalendarState.MonthGrid(Page, first, owner.FixedWeeks);
        var names = CalendarText.WeekdayNames(culture, first);
        var full = CalendarText.WeekdayNames(culture, first, full: true);
        Weekdays = [.. names.Zip(full)];
        var pad = (int)MathF.Round(Tokens.Space.Xs * Em);
        var headerHeight = Font.Height + 2 * pad;
        var weekColumn = owner.ShowWeekNumbers
            ? weeks.Select(week => CalendarState.WeekOfYear(week[0], first).ToString(CultureInfo.InvariantCulture)).Append(owner.Lang.CalendarWeek)
                .Max(text => TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width) + pad
            : 0;
        if (owner.ShowWeekNumbers) result.Add(new Slot(default, null, new Rectangle(0, 0, weekColumn - pad, headerHeight), false, false, Header: owner.Lang.CalendarWeek));
        for (var i = 0; i < 7; i++) result.Add(new Slot(default, null, new Rectangle(weekColumn + i * side, 0, side, headerHeight), false, false, Header: names[i], Tip: full[i]));
        for (var row = 0; row < weeks.Count; row++)
        {
            var week = weeks[row];
            var y = headerHeight + row * side;
            if (owner.ShowWeekNumbers)
                result.Add(new Slot(default, null, new Rectangle(0, y, weekColumn - pad, side), false, false, WeekNumber: CalendarState.WeekOfYear(week[0], first).ToString(CultureInfo.InvariantCulture)));
            for (var i = 0; i < 7; i++)
            {
                var date = week[i];
                var bounds = new Rectangle(weekColumn + i * side, y, side, side);
                // 并排几个月时补齐的日期留空：同一天不在两张网格里重复出现
                result.Add(date.Month != Page.Month && !single ? new Slot(date, null, bounds, false, false) : new Slot(date, state.Cell(date, Page), bounds, i == 0, i == 6));
            }
        }
        return Mirror(result);
    }

    // 从右往左：整张网格镜像（第一列在右边）
    List<Slot> Mirror(List<Slot> result)
    {
        if (RightToLeft != RightToLeft.Yes && owner.RightToLeft != RightToLeft.Yes) return result;
        var width = result.Count == 0 ? 0 : result.Max(slot => slot.Bounds.Right);
        return [.. result.Select(slot => slot with { Bounds = new Rectangle(width - slot.Bounds.Right, slot.Bounds.Y, slot.Bounds.Width, slot.Bounds.Height), RowStart = slot.RowEnd, RowEnd = slot.RowStart })];
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) =>
        slots.Count == 0 ? Size.Empty : new Size(slots.Max(slot => slot.Bounds.Right), slots.Max(slot => slot.Bounds.Bottom));

    /// <summary>格子读屏念的名称：完整日期（今天加"今天"），自定义文字跟在后面。</summary>
    internal string NameOf(DateOnly date, CalendarCellState cell)
    {
        var lang = owner.Lang;
        var culture = owner.Culture;
        var label = !period ? CalendarText.FullDate(date, culture) : State.View == DateUnit.Month ? CalendarText.YearMonth(date, culture) : CalendarText.Year(date.Year, culture, lang);
        var spoken = cell.Today ? lang.CalendarToday(label) : label;
        return !period && owner.CellText?.Invoke(date, cell) is { } extra ? spoken + extra : spoken;
    }

    string TextOf(DateOnly date, CalendarCellState cell) => period
        ? State.View == DateUnit.Month ? CalendarText.MonthShort(date, owner.Culture) : CalendarText.Year(date.Year, owner.Culture, owner.Lang)
        : owner.CellText?.Invoke(date, cell) ?? CalendarText.DayNumber(date);

    bool Off(CalendarCellState cell) => !owner.Enabled || cell.Disabled;

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var em = Em;
        var radius = theme.Radii.Md * em;
        var range = State.Mode() == CalendarSelectionMode.Range;
        var dim = Drawing.Dimmer(this, owner.Enabled, colors);
        foreach (var slot in slots)
        {
            if (slot.Header is { } header)
            {
                TextRenderer.DrawText(g, header, Font, slot.Bounds, dim(colors.TextMuted), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                continue;
            }
            if (slot.WeekNumber is { } number)
            {
                TextRenderer.DrawText(g, number, Font, slot.Bounds, dim(colors.TextMuted), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                continue;
            }
            if (slot.Cell is not { } cell) continue;
            var bounds = (RectangleF)slot.Bounds;
            var ends = range && (cell.RangeStart || cell.RangeEnd);
            var marked = range ? ends : cell.Selected;
            var middle = cell.InRange && !ends;
            var off = Off(cell);
            if (cell.InRange)
            {
                // 区间的底色画在格子上，相邻的格连成一条；两端与每行的首尾是圆的
                var left = slot.RowStart || (RightToLeft == RightToLeft.Yes ? cell.RangeEnd : cell.RangeStart);
                var right = slot.RowEnd || (RightToLeft == RightToLeft.Yes ? cell.RangeStart : cell.RangeEnd);
                using var band = Band(bounds, left ? radius : 0, right ? radius : 0);
                using var soft = new SolidBrush(colors.AccentSoft);
                g.FillPath(soft, band);
            }
            var hover = hovered == slot.Date && !off;
            Color? fill = marked ? (hover ? colors.AccentHover : colors.Accent) : middle ? (hover ? colors.AccentSoftHover : null) : hover ? colors.FillHover : null;
            using var shape = Drawing.RoundedRectangle(bounds, MathF.Min(radius, bounds.Height / 2));
            if (fill is { } color)
            {
                using var brush = new SolidBrush(dim(color));
                g.FillPath(brush, shape);
            }
            if (cell.Today && !marked)
            {
                using var ring = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, -0.5f, -0.5f), MathF.Min(radius, bounds.Height / 2));
                using var pen = new Pen(dim(colors.AccentBorder), 1);
                g.DrawPath(pen, ring);
            }
            var fore = marked ? colors.AccentContrast : middle ? colors.AccentText : cell.Outside || cell.Unavailable ? colors.TextMuted : colors.Text;
            // 不能选的变淡（opacities.disabled）；整个日历不可用时 dim 已经混过一次
            if (off && owner.Enabled) fore = Drawing.Mix(BackOf(colors), fore, Tokens.Opacity.Disabled);
            fore = dim(fore);
            var args = new DrawCalendarCellEventArgs(g, slot.Bounds, slot.Date, cell, fore, Font);
            if (!period && owner.OwnerDraws) owner.RaiseDrawCell(args);
            if (!args.Handled)
            {
                var style = (cell.Today ? FontStyle.Bold : FontStyle.Regular) | (cell.Unavailable ? FontStyle.Strikeout : FontStyle.Regular);
                var font = style == FontStyle.Regular ? Font : Fonts.Get(Font.FamilyName(), Font.SizeInPoints, style);
                TextRenderer.DrawText(g, TextOf(slot.Date, cell), font, slot.Bounds, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            }
            if (Focused && ShowFocusCues && cell.Focused && !cell.Outside)
            {
                var width = 2 * DeviceDpi / 96f;
                using var ring = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, -width / 2, -width / 2), MathF.Min(radius, bounds.Height / 2));
                using var pen = new Pen(colors.FocusRing, width);
                g.DrawPath(pen, ring);
            }
        }
    }

    Color BackOf(ThemeColors colors)
    {
        for (Control? control = this; control is not null; control = control.Parent)
            if (control.BackColor.A == 255) return control.BackColor;
        return colors.Background;
    }

    static GraphicsPath Band(RectangleF bounds, float left, float right)
    {
        var path = new GraphicsPath();
        if (left > 0) path.AddArc(bounds.Left, bounds.Top, 2 * left, 2 * left, 180, 90);
        else path.AddLine(bounds.Left, bounds.Top, bounds.Left, bounds.Top);
        if (right > 0)
        {
            path.AddArc(bounds.Right - 2 * right, bounds.Top, 2 * right, 2 * right, 270, 90);
            path.AddArc(bounds.Right - 2 * right, bounds.Bottom - 2 * right, 2 * right, 2 * right, 0, 90);
        }
        else
        {
            path.AddLine(bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);
        }
        if (left > 0) path.AddArc(bounds.Left, bounds.Bottom - 2 * left, 2 * left, 2 * left, 90, 90);
        else path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Bottom);
        path.CloseFigure();
        return path;
    }

    Slot? HitTest(Point point) => slots.FirstOrDefault(slot => slot.Cell is not null && slot.Bounds.Contains(point));

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var date = HitTest(e.Location)?.Date;
        if (date == hovered) return;
        hovered = date;
        if (date is { } target && State.Anchor is not null)
        {
            // 区间选到一半：预览跟着指针（并排的几张网格一起重画）
            State.Hover(target);
            owner.Rebuild();
            return;
        }
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = null;
        State.Hover(null);
        if (State.Anchor is not null) owner.Rebuild();
        else Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || HitTest(e.Location) is not { Cell: { } cell } slot) return;
        Activate(slot.Date, cell);
    }

    /// <summary>点选一格（读屏的默认动作、Enter、空格、鼠标）：不能选的不理。</summary>
    internal void Activate(DateOnly date, CalendarCellState cell)
    {
        if (Off(cell)) return;
        State.Activate(date);
        owner.Rebuild(focus: true);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
        if (slots.FirstOrDefault(slot => slot.Cell is { Focused: true }) is { } focused)
            AccessibilityNotifyClients(AccessibleEvents.Focus, slots.Where(slot => slot.Cell is not null).ToList().IndexOf(focused));
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Escape
        || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Press(e.KeyData)) e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>按键：方向键、Page Up / Down（加 Shift 一年）、Home / End 移动焦点，Enter / 空格选中，Esc 取消选到一半的区间。返回是否处理了。</summary>
    /// <param name="keys">按键（含修饰键）。</param>
    internal bool Press(Keys keys)
    {
        if (!owner.Enabled || (keys & (Keys.Control | Keys.Alt)) != 0) return false;
        var code = keys & Keys.KeyCode;
        if (code is Keys.Enter or Keys.Space)
        {
            if (slots.FirstOrDefault(slot => slot.Cell is { Focused: true }) is not { Cell: { } focused } slot) return false;
            Activate(slot.Date, focused);
            return true;
        }
        CalendarKey? key = code switch
        {
            Keys.Left => CalendarKey.Left,
            Keys.Right => CalendarKey.Right,
            Keys.Up => CalendarKey.Up,
            Keys.Down => CalendarKey.Down,
            Keys.PageUp => CalendarKey.PageUp,
            Keys.PageDown => CalendarKey.PageDown,
            Keys.Home => CalendarKey.Home,
            Keys.End => CalendarKey.End,
            Keys.Escape => CalendarKey.Escape,
            _ => null,
        };
        if (key is not { } pressed || !State.HandleKey(pressed, (keys & Keys.Shift) != 0, owner.RightToLeft == RightToLeft.Yes)) return false;
        owner.Rebuild(focus: true);
        return true;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GridAccessibleObject(this);

    sealed class GridAccessibleObject(CalendarGridControl owner) : ControlAccessibleObject(owner)
    {
        List<Slot> Items => [.. owner.slots.Where(slot => slot.Cell is not null || slot.WeekNumber is not null)];

        public override int GetChildCount() => Items.Count;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < Items.Count ? new CellAccessibleObject(owner, this, Items[index]) : null;

        public override AccessibleObject? GetFocused() =>
            owner.Focused && Items.FirstOrDefault(slot => slot.Cell is { Focused: true }) is { } focused ? new CellAccessibleObject(owner, this, focused) : base.GetFocused();
    }

    sealed class CellAccessibleObject(CalendarGridControl owner, AccessibleObject parent, Slot slot) : AccessibleObject
    {
        public override AccessibleObject Parent => parent;

        public override AccessibleRole Role => slot.WeekNumber is null ? AccessibleRole.Cell : AccessibleRole.RowHeader;

        public override string Name => slot.Cell is { } cell ? owner.NameOf(slot.Date, cell) : owner.owner.Lang.CalendarWeekNumber(int.Parse(slot.WeekNumber!, CultureInfo.InvariantCulture));

        public override string? Description => slot.Cell is not null ? owner.AccessibleDescription : null;

        public override string DefaultAction => slot.Cell is null ? "" : "Select";

        public override AccessibleStates State => slot.Cell is not { } cell ? AccessibleStates.ReadOnly
            : AccessibleStates.Selectable | AccessibleStates.Focusable
              | (cell.Selected || cell.InRange ? AccessibleStates.Selected : 0)
              | (cell.Focused && owner.Focused ? AccessibleStates.Focused : 0)
              | (owner.Off(cell) ? AccessibleStates.Unavailable : 0);

        public override Rectangle Bounds => owner.RectangleToScreen(slot.Bounds);

        public override void DoDefaultAction()
        {
            if (slot.Cell is { } cell) owner.Activate(slot.Date, cell);
        }
    }
}

/// <summary>画日历的一格（<see cref="CalendarViewControl.DrawCell"/>）。</summary>
/// <param name="graphics">画布。</param>
/// <param name="bounds">格子的位置。</param>
/// <param name="date">日期。</param>
/// <param name="state">格子的状态。</param>
/// <param name="foreColor">默认的文字色（已选、区间、补齐、不可用都算过了）。</param>
/// <param name="font">字体。</param>
public sealed class DrawCalendarCellEventArgs(Graphics graphics, Rectangle bounds, DateOnly date, CalendarCellState state, Color foreColor, Font font) : EventArgs
{
    /// <summary>画布。</summary>
    public Graphics Graphics { get; } = graphics;

    /// <summary>格子的位置。</summary>
    public Rectangle Bounds { get; } = bounds;

    /// <summary>日期。</summary>
    public DateOnly Date { get; } = date;

    /// <summary>格子的状态。</summary>
    public CalendarCellState State { get; } = state;

    /// <summary>默认的文字色。</summary>
    public Color ForeColor { get; } = foreColor;

    /// <summary>字体。</summary>
    public Font Font { get; } = font;

    /// <summary>自己画完了：不再画默认的文字。</summary>
    public bool Handled { get; set; }
}
