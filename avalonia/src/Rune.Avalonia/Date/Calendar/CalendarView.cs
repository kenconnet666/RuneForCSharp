using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>
/// 日历的界面（vue-tsx-pro 的 CalendarView，WAI-ARIA 的日期网格）：页头（上一页、标题、下一页）、并排的月份网格、月份视图与年份视图、页脚。
/// 状态由派生类给的 <see cref="CalendarState"/>（Calendar、RangeCalendar、日期选择器的弹层共用）。
/// <list type="bullet">
/// <item>每格是一个按钮（读屏念完整日期，今天加"今天"），只有焦点日期那一格在 Tab 顺序里，键盘移动后焦点跟过去；</item>
/// <item>标题按钮切到月份视图、再切到年份视图（不比选择的粒度更细）；翻页、换视图时播报新的标题；</item>
/// <item>并排几个月时只显示本月的日期（补齐的上下月留空）；只有一个月时补齐的日期变淡、可以点。</item>
/// </list>
/// 外观：页头三列（翻页按钮、居中的标题按钮：medium 字重、md 圆角、悬停 fillHover）；星期表头与周数 textMuted；
/// 日期格 max(2.25em, 24) 见方、md 圆角、悬停 fillHover；今天 semibold 加 accentBorder 细圈；已选 accent 底、accentContrast 字；
/// 区间中间的格透明、accentText 字，底色是格子那一层的 accentSoft 连成一条（两端与每行首尾是圆的）；补齐的日期 textMuted；不可选的划掉；不能选的变淡。
/// 部件：<see cref="CalendarViewParts"/>，默认外观在 CalendarView.axaml。
/// </summary>
[PseudoClasses(":invalid")]
public class CalendarView : CalendarSurface
{
    /// <summary>在每行前显示一年里的第几周。</summary>
    public static readonly StyledProperty<bool> ShowWeekNumbersProperty = AvaloniaProperty.Register<CalendarView, bool>(nameof(ShowWeekNumbers));

    /// <summary>总是 6 行（翻页时高度不跳），默认 true。</summary>
    public static readonly StyledProperty<bool> FixedWeeksProperty = AvaloniaProperty.Register<CalendarView, bool>(nameof(FixedWeeks), true);

    /// <summary>一周的第一天；不写时按语言包、再按区域惯例。</summary>
    public static readonly StyledProperty<DayOfWeek?> FirstDayOfWeekProperty = AvaloniaProperty.Register<CalendarView, DayOfWeek?>(nameof(FirstDayOfWeek));

    /// <summary>页脚：今天、清除这类按钮。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<CalendarView, object?>(nameof(Footer));

    /// <summary>
    /// 自定义日期格的内容（价格、标记点）：拿到日期与这一格的状态，返回文字或控件；不写时是日期数字。读屏先念完整日期再念这里的文字。
    /// </summary>
    public static readonly StyledProperty<Func<DateOnly, CalendarCellState, object>?> CellContentProperty =
        AvaloniaProperty.Register<CalendarView, Func<DateOnly, CalendarCellState, object>?>(nameof(CellContent));

    readonly Flex months = new() { Name = CalendarViewParts.Months, Wrap = true, Gap = Space.Xl, Align = FlexAlign.Start };
    readonly LiveRegion live = new() { Height = 0 };
    readonly LiveRegion hint = new() { Height = 0 };
    readonly ContentControl footer = new() { Name = CalendarViewParts.Footer, IsVisible = false };
    readonly List<CellEntry> entries = [];
    string? structure;
    string? announced;
    DateOnly? hovered;

    /// <summary>建好的一格：重画时按状态原地刷新（不重建，悬停预览时指针下的元素不换）。</summary>
    sealed record CellEntry(Border Holder, CalendarCellButton Button, DateOnly? Month, string Label, bool RowStart, bool RowEnd);

    /// <summary>新建：没有状态时什么都不画（派生类在构造时给）。</summary>
    public CalendarView()
    {
        Root.Children.AddRange([months, live, hint, footer]);
        AddHandler(KeyDownEvent, OnGridKeyDown);
    }

    /// <summary>显示周数。</summary>
    public bool ShowWeekNumbers { get => GetValue(ShowWeekNumbersProperty); set => SetValue(ShowWeekNumbersProperty, value); }

    /// <summary>总是 6 行。</summary>
    public bool FixedWeeks { get => GetValue(FixedWeeksProperty); set => SetValue(FixedWeeksProperty, value); }

    /// <summary>一周的第一天。</summary>
    public DayOfWeek? FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>页脚。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>自定义日期格的内容。</summary>
    public Func<DateOnly, CalendarCellState, object>? CellContent { get => GetValue(CellContentProperty); set => SetValue(CellContentProperty, value); }

    /// <summary>浅色底的日期（伪类 <c>:highlighted</c>）：DatePicker 选周时已选那一周的另外六天（日历只把周一当作已选的那一格）。</summary>
    internal Func<DateOnly, bool>? Highlighted
    {
        get;
        set
        {
            field = value;
            Rebuild();
        }
    }

    /// <summary>日历状态（派生类给）。</summary>
    internal CalendarState? State { get; private protected set; }

    /// <summary>显示着的日期格（测试用）。</summary>
    internal IEnumerable<CalendarCellButton> Cells => months.GetLogicalDescendants().OfType<CalendarCellButton>();

    /// <summary>一周的第一天：自己写的，否则语言包的，否则区域惯例。</summary>
    internal DayOfWeek FirstDay => CalendarText.FirstDayOf(Lang, FirstDayOfWeek);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowWeekNumbersProperty || change.Property == FixedWeeksProperty || change.Property == FirstDayOfWeekProperty
            || change.Property == CellContentProperty || change.Property == Provider.LangProperty || change.Property == IsEnabledProperty
            || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)
            Rebuild();
        else if (change.Property == FooterProperty)
        {
            footer.Content = Footer;
            footer.IsVisible = Footer is not null;
        }
    }

    /// <summary>
    /// 状态变了（选了、翻页、换视图、外面改值、悬停预览）：页面没换时只按状态刷新格子，换了页、视图或选项时重建页头与网格；
    /// focus 时把焦点交给新的焦点格（键盘移动后）。
    /// </summary>
    /// <param name="focus">把焦点移到焦点格。</param>
    internal void Rebuild(bool focus = false)
    {
        // 没进界面前不画：属性初始化的先后不定，状态要等各项（DefaultFocused、今天）都给了才开始
        if (State is not { } state || VisualRoot is null) return;
        var lang = Lang;
        var shown = state.Months;
        var view = state.View;
        var key = string.Join("|", view, string.Join(",", shown), ShowWeekNumbers, FixedWeeks, FirstDay, lang.Code, IsEffectivelyEnabled, Em, CellContent?.GetHashCode());
        if (key != structure)
        {
            structure = key;
            Build(state, lang, view, shown);
        }
        Sync(state, lang);
        if (focus) Cells.FirstOrDefault(cell => cell.IsTabStop)?.Focus(NavigationMethod.Directional);
    }

    void Build(CalendarState state, Lang lang, DateUnit view, IReadOnlyList<DateOnly> shown)
    {
        var culture = Culture;
        months.Children.Clear();
        entries.Clear();
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
            var header = new AvaloniaGrid { Name = CalendarViewParts.Header, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            header.Children.Add(index == 0 ? PageButton(Lucide.ChevronLeft, previousLabel, -1, CalendarViewParts.Previous) : Spacer());
            Control heading;
            if (coarser is { } next && IsEffectivelyEnabled)
            {
                var button = new ButtonBase { Name = CalendarViewParts.Heading, Content = title, HorizontalAlignment = HorizontalAlignment.Center };
                AutomationProperties.SetName(button, next == DateUnit.Month ? lang.CalendarChooseMonth(title) : lang.CalendarChooseYear(title));
                button.Click += (_, _) =>
                {
                    state.SetView(next);
                    Rebuild();
                };
                heading = button;
            }
            // 已经是最粗的视图（或不可用）：标题只是文字
            else heading = new Text { Name = CalendarViewParts.Heading, Text = title, Weight = TextWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            AvaloniaGrid.SetColumn(heading, 1);
            header.Children.Add(heading);
            var after = index == shown.Count - 1 ? PageButton(Lucide.ChevronRight, nextLabel, 1, CalendarViewParts.Next) : Spacer();
            AvaloniaGrid.SetColumn(after, 2);
            header.Children.Add(after);
            var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { header, view == DateUnit.Day ? DayGrid(state, month, shown.Count == 1, title) : PeriodGrid(state, title) } };
            months.Children.Add(column);
        }
        // 翻页、换视图时播报新的标题；不放在标题按钮上，免得聚焦按钮时重复念
        var titles = string.Join("，", shown.Select(month => CalendarText.Title(month, view, culture, lang)));
        if (titles != announced)
        {
            if (announced is not null) live.Announce(titles);
            announced = titles;
        }
    }

    /// <summary>按状态刷新每一格：选中、焦点、区间与预览、不能选（读屏名称里的"今天"、自定义内容也跟着状态）。</summary>
    void Sync(CalendarState state, Lang lang)
    {
        PseudoClasses.Set(":invalid", state.Invalid);
        hint.Text = state.Anchor is null ? "" : lang.CalendarPickEnd;
        var range = state.Mode() == CalendarSelectionMode.Range;
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * Em;
        foreach (var (holder, button, month, label, rowStart, rowEnd) in entries)
        {
            var cell = state.Cell(button.Date, month);
            var ends = range && (cell.RangeStart || cell.RangeEnd);
            button.Apply(cell, marked: range ? ends : cell.Selected, middle: cell.InRange && !ends, off: !IsEffectivelyEnabled || cell.Disabled,
                highlighted: month is not null && !cell.Selected && Highlighted?.Invoke(button.Date) == true);
            button.IsTabStop = cell.Focused && !cell.Outside;
            var content = month is null ? null : CellContent?.Invoke(button.Date, cell);
            if (content is not null) button.Content = content;
            var spoken = cell.Today ? lang.CalendarToday(label) : label;
            AutomationProperties.SetName(button, content is null ? spoken : spoken + SpokenText(content));
            AutomationProperties.SetHelpText(button, state.Anchor is null ? null : lang.CalendarPickEnd);
            holder.Classes.Set("in-range", cell.InRange);
            // 区间的底色画在格子上，相邻的格连成一条；两端与每行的首尾是圆的
            var left = rowStart || cell.RangeStart;
            var right = rowEnd || cell.RangeEnd;
            holder.CornerRadius = cell.InRange ? new CornerRadius(left ? radius : 0, right ? radius : 0, right ? radius : 0, left ? radius : 0) : default;
        }
    }

    Control PageButton(IconData icon, string label, int direction, string name)
    {
        var button = new IconButton { Name = name, Icon = icon, IsEnabled = IsEffectivelyEnabled && State!.CanTurnPage(direction) };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) =>
        {
            State!.TurnPage(direction);
            Rebuild();
        };
        return button;
    }

    // 占住按钮的位置：并排几个月时中间的月没有翻页按钮，标题仍居中
    Control Spacer() => new Border { Width = Math.Max(this.Resource<double>(ThemeKeys.Size.Control) * Em, this.Resource<double>(ThemeKeys.Size.MinTarget)) };

    double CellSide => Math.Max(2.25 * Em, this.Resource<double>(ThemeKeys.Size.MinTarget));

    Control DayGrid(CalendarState state, DateOnly month, bool single, string title)
    {
        var culture = Culture;
        var first = FirstDay;
        var weeks = CalendarState.MonthGrid(month, first, FixedWeeks);
        var columns = ShowWeekNumbers ? 8 : 7;
        // 列宽、行高按内容（表头行比日期行矮、周数列比日期列窄，同 TS 的 table）
        var grid = new AvaloniaGrid { Name = CalendarViewParts.Grid };
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var i = 0; i <= weeks.Count; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        void Add(Control child)
        {
            var index = grid.Children.Count;
            AvaloniaGrid.SetRow(child, index / columns);
            AvaloniaGrid.SetColumn(child, index % columns);
            grid.Children.Add(child);
        }
        AutomationProperties.SetName(grid, title);
        if (ShowWeekNumbers) Add(new Text { Name = CalendarViewParts.WeekNumber, Text = Lang.CalendarWeek, Tone = TextTone.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var names = CalendarText.WeekdayNames(culture, first);
        var full = CalendarText.WeekdayNames(culture, first, full: true);
        for (var i = 0; i < 7; i++)
        {
            var weekday = new Text { Name = CalendarViewParts.Weekday, Text = names[i], Tone = TextTone.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0.25 * Em) };
            ToolTip.SetTip(weekday, full[i]);
            AutomationProperties.SetAccessibilityView(weekday, AccessibilityView.Raw);
            Add(weekday);
        }
        foreach (var week in weeks)
        {
            if (ShowWeekNumbers)
            {
                var number = CalendarState.WeekOfYear(week[0], first);
                var label = new WeekNumberText { Name = CalendarViewParts.WeekNumber, Text = number.ToString(CultureInfo.InvariantCulture), Tone = TextTone.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                AutomationProperties.SetName(label, Lang.CalendarWeekNumber(number));
                Add(label);
            }
            for (var i = 0; i < 7; i++)
            {
                var date = week[i];
                // 并排几个月时补齐的日期留空：同一天不在两张网格里重复出现
                if (date.Month != month.Month && !single)
                {
                    Add(new Border { Width = CellSide, Height = CellSide });
                    continue;
                }
                Add(Cell(state, date, month, CalendarText.FullDate(date, culture), CalendarText.DayNumber(date), i == 0, i == 6));
            }
        }
        grid.PointerExited += (_, _) =>
        {
            hovered = null;
            state.Hover(null);
            if (state.Anchor is not null) Rebuild();
        };
        return grid;
    }

    Control PeriodGrid(CalendarState state, string title)
    {
        var culture = Culture;
        var month = state.View == DateUnit.Month;
        var items = month ? CalendarState.MonthsOfYear(state.Page) : CalendarState.YearsOfDecade(state.Page);
        var grid = new UniformGrid { Name = CalendarViewParts.Grid, Columns = 3, Width = 7 * CellSide };
        AutomationProperties.SetName(grid, title);
        for (var i = 0; i < items.Count; i++)
        {
            var date = items[i];
            var label = month ? CalendarText.YearMonth(date, culture) : CalendarText.Year(date.Year, culture, Lang);
            var text = month ? CalendarText.MonthShort(date, culture) : CalendarText.Year(date.Year, culture, Lang);
            grid.Children.Add(Cell(state, date, null, label, text, i % 3 == 0, i % 3 == 2));
        }
        return grid;
    }

    /// <summary>一格：外层是画区间那一条的 Border，里面是按钮；状态由 Sync 给。month 为 null 时是月份、年份格。</summary>
    Control Cell(CalendarState state, DateOnly date, DateOnly? month, string label, string text, bool rowStart, bool rowEnd)
    {
        var period = month is null;
        var button = new CalendarCellButton(date, period)
        {
            Name = CalendarViewParts.CellButton,
            Content = text,
            HorizontalAlignment = period ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
        };
        button.Click += (_, _) =>
        {
            if (!IsEffectivelyEnabled || state.Cell(date, month).Disabled) return;
            state.Activate(date);
            Rebuild(focus: true);
        };
        // 点击、程序聚焦到别的格子时，键盘从这一格接着走
        button.GotFocus += (_, _) =>
        {
            if (state.Cell(date, month).Focused) return;
            state.Focus(date);
        };
        button.PointerEntered += (_, _) =>
        {
            if (state.Anchor is null || hovered == date) return;
            hovered = date;
            state.Hover(date);
            Rebuild();
        };
        var holder = new Border { Name = CalendarViewParts.Cell, Child = button };
        entries.Add(new CellEntry(holder, button, month, label, rowStart, rowEnd));
        return holder;
    }

    /// <summary>自定义内容里读屏要念的文字：字符串本身、控件的读屏名称，否则是里面的文字。</summary>
    static string SpokenText(object? content) => content switch
    {
        string text => text,
        // 同 aria-hidden：读屏已经念了完整日期，格子里的数字不重复念
        Control control when AutomationProperties.GetAccessibilityView(control) == AccessibilityView.Raw => "",
        Control control when AutomationProperties.GetName(control) is { Length: > 0 } name => name,
        TextBlock block => block.Text ?? "",
        ContentControl holder => SpokenText(holder.Content),
        Decorator decorator => SpokenText(decorator.Child),
        Panel panel => string.Concat(panel.Children.Select(SpokenText)),
        _ => "",
    };

    void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (State is not { } state || !IsEffectivelyEnabled || e.Source is not CalendarCellButton) return;
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        CalendarKey? key = e.Key switch
        {
            Key.Left => CalendarKey.Left,
            Key.Right => CalendarKey.Right,
            Key.Up => CalendarKey.Up,
            Key.Down => CalendarKey.Down,
            Key.PageUp => CalendarKey.PageUp,
            Key.PageDown => CalendarKey.PageDown,
            Key.Home => CalendarKey.Home,
            Key.End => CalendarKey.End,
            Key.Escape => CalendarKey.Escape,
            _ => null,
        };
        if (key is not { } pressed || !state.HandleKey(pressed, (e.KeyModifiers & KeyModifiers.Shift) != 0, FlowDirection == FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Rebuild(focus: true);
    }
}

/// <summary>周数：看到的是数字，读屏念"第 40 周"（TS 的行表头 aria-label；TextBlock 的读屏名称默认是它的文字）。</summary>
sealed class WeekNumberText : Text
{
    protected override Type StyleKeyOverride => typeof(Text);

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    sealed class Peer(WeekNumberText owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => AutomationProperties.GetName(Owner) ?? owner.Text;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
    }
}

/// <summary>
/// 日历的一格（<see cref="CalendarView"/> 建的）：日期、月份或年份按钮。伪类 <c>:selected</c>（已选或区间两端）、<c>:middle</c>（区间中间）、<c>:today</c>、
/// <c>:outside</c>（补齐的上下月）、<c>:unavailable</c>（划掉）、<c>:off</c>（不能选）、<c>:period</c>（月份、年份格）。
/// </summary>
[PseudoClasses(":selected", ":middle", ":highlighted", ":today", ":outside", ":unavailable", ":off", ":period")]
public sealed class CalendarCellButton : ButtonBase
{
    internal CalendarCellButton(DateOnly date, bool period)
    {
        Date = date;
        PseudoClasses.Set(":period", period);
    }

    /// <summary>这一格的日期（月份、年份格是那个月、那年的第一天）。</summary>
    public DateOnly Date { get; }

    /// <summary>这一格的状态。</summary>
    public CalendarCellState State { get; private set; } = null!;

    /// <summary>换成新的状态（CalendarView 刷新时）。</summary>
    internal void Apply(CalendarCellState state, bool marked, bool middle, bool off, bool highlighted = false)
    {
        PseudoClasses.Set(":highlighted", highlighted);
        State = state;
        PseudoClasses.Set(":today", state.Today);
        PseudoClasses.Set(":outside", state.Outside);
        PseudoClasses.Set(":unavailable", state.Unavailable);
        PseudoClasses.Set(":selected", marked);
        PseudoClasses.Set(":middle", middle);
        PseudoClasses.Set(":off", off);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ButtonBase);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    /// <summary>读屏：格子（选中状态、不可用）。</summary>
    sealed class CellPeer(CalendarCellButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

        protected override bool IsEnabledCore() => !owner.PseudoClasses.Contains(":off") && base.IsEnabledCore();
    }
}

/// <summary>CalendarView 的部件名（TS 的部件）。外层（TS 的 root）就是日历自己。</summary>
public static class CalendarViewParts
{
    /// <summary>并排的月份（TS 的 propsMonths）：Flex。</summary>
    public const string Months = "PART_Months";

    /// <summary>页头（TS 的 propsHeader）：三列的 Grid。</summary>
    public const string Header = "PART_Header";

    /// <summary>上一页（TS 的 propsPrevious）：IconButton。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>下一页（TS 的 propsNext）：IconButton。</summary>
    public const string Next = "PART_Next";

    /// <summary>标题（TS 的 propsHeading）：ButtonBase（最粗的视图时是 Text）。</summary>
    public const string Heading = "PART_Heading";

    /// <summary>一张网格（TS 的 propsGrid）：日期是 Grid（列宽、行高按内容），月份、年份是三列的 UniformGrid。</summary>
    public const string Grid = "PART_Grid";

    /// <summary>星期表头（TS 的 propsWeekday）：Text，textMuted。</summary>
    public const string Weekday = "PART_Weekday";

    /// <summary>周数（TS 的 propsWeekNumber）：Text，textMuted。</summary>
    public const string WeekNumber = "PART_WeekNumber";

    /// <summary>一格（TS 的 propsCell）：Border，区间那一条画在它上面（类 in-range）。</summary>
    public const string Cell = "PART_Cell";

    /// <summary>格子里的按钮（TS 的 propsCellButton）：CalendarCellButton。</summary>
    public const string CellButton = "PART_CellButton";

    /// <summary>页脚（TS 的 propsFooter）：ContentControl。</summary>
    public const string Footer = "PART_Footer";
}
