using System.Globalization;

namespace Rune.Avalonia;

/// <summary>日期的选择方式（vue-tsx-pro 的 CalendarSelectionModes）。</summary>
public enum CalendarSelectionMode
{
    /// <summary>单选一天。</summary>
    Single,

    /// <summary>选一个区间：先点开始、再点结束，中间悬停时预览。</summary>
    Range,

    /// <summary>多选：每天独立切换。</summary>
    Multiple,
}

/// <summary>日历的粒度与视图（vue-tsx-pro 的 DateUnit），从粗到细。</summary>
public enum DateUnit
{
    /// <summary>年：一页是一个十年。</summary>
    Year,

    /// <summary>月：一页是一年的各月。</summary>
    Month,

    /// <summary>日：一页是一个月的日期网格。</summary>
    Day,
}

/// <summary>日期区间，两端都包含。</summary>
/// <param name="Start">开始。</param>
/// <param name="End">结束。</param>
public readonly record struct DateRange(DateOnly Start, DateOnly End);

/// <summary>ISO 字符串的区间（vue-tsx-pro 的 DateRange&lt;string&gt;）：RangeCalendar、DateRangePicker 的值，两端都包含。</summary>
/// <param name="Start">开始（ISO：2026-10-12）。</param>
/// <param name="End">结束（ISO）。</param>
public sealed record IsoDateRange(string Start, string End)
{
    /// <summary>日期区间；两端写得不对时 null。</summary>
    public DateRange? Dates =>
        DateOnly.TryParseExact(Start.Length >= 10 ? Start[..10] : Start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
        && DateOnly.TryParseExact(End.Length >= 10 ? End[..10] : End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            ? new DateRange(start, end)
            : null;

    /// <summary>日期区间写成 ISO。</summary>
    public static IsoDateRange From(DateRange range) => new(range.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), range.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

/// <summary>快捷区间（vue-tsx-pro 的 DateRangePreset）：名字取语言包的 DateRangePreset，区间按今天与一周的第一天算（<see cref="CalendarState.PresetRange"/>）。</summary>
public enum DateRangePreset
{
    /// <summary>今天。</summary>
    Today,

    /// <summary>昨天。</summary>
    Yesterday,

    /// <summary>最近 7 天（含今天）。</summary>
    Last7Days,

    /// <summary>最近 30 天（含今天）。</summary>
    Last30Days,

    /// <summary>本周（按一周的第一天）。</summary>
    ThisWeek,

    /// <summary>本月。</summary>
    ThisMonth,

    /// <summary>上月。</summary>
    LastMonth,

    /// <summary>今年。</summary>
    ThisYear,
}

/// <summary>可选范围与不可选的日期（vue-tsx-pro 的 DateLimits）：Calendar、DatePicker 共用。</summary>
/// <param name="Min">最早可选的日期。</param>
/// <param name="Max">最晚可选的日期。</param>
/// <param name="IsUnavailable">不可选的日期（已订满、节假日）：仍能用方向键经过，读屏能听到，不能选。</param>
public sealed record DateLimits(DateOnly? Min = null, DateOnly? Max = null, Func<DateOnly, bool>? IsUnavailable = null);

/// <summary>一格的状态（vue-tsx-pro 的 CalendarCellState）：画格子用。</summary>
/// <param name="Selected">已选（区间里是两端与中间的每一天）。</param>
/// <param name="Disabled">不能选：范围外、不可选、或区间选到一半时越过了不可选的日期。</param>
/// <param name="Unavailable">是不可选的日期：画删除线。</param>
/// <param name="Today">今天（这个月、今年）。</param>
/// <param name="Focused">焦点所在的格。</param>
/// <param name="Outside">不在这一页：月份网格里补齐的上下月、年份视图里补上的两年。</param>
/// <param name="RangeStart">区间的开始。</param>
/// <param name="RangeEnd">区间的结束。</param>
/// <param name="InRange">在区间里（含两端；选到一半时是预览的区间）。</param>
public sealed record CalendarCellState(bool Selected, bool Disabled, bool Unavailable, bool Today, bool Focused, bool Outside, bool RangeStart, bool RangeEnd, bool InRange);

/// <summary>日期网格里与平台无关的按键（CalendarState.HandleKey）。</summary>
public enum CalendarKey
{
    /// <summary>←。</summary>
    Left,

    /// <summary>→。</summary>
    Right,

    /// <summary>↑。</summary>
    Up,

    /// <summary>↓。</summary>
    Down,

    /// <summary>Page Up。</summary>
    PageUp,

    /// <summary>Page Down。</summary>
    PageDown,

    /// <summary>Home。</summary>
    Home,

    /// <summary>End。</summary>
    End,

    /// <summary>Enter 或空格：选中（或进到下一级视图）。</summary>
    Enter,

    /// <summary>Esc：取消选到一半的区间。</summary>
    Escape,
}

/// <summary>
/// 日历的状态（vue-tsx-pro 的 useCalendarState，WAI-ARIA 日期网格的数据层，参考 React Aria 的 useCalendarState / useRangeCalendarState）：
/// 焦点日期、显示哪一页、单选 / 区间 / 多选、区间悬停预览、键盘操作。不碰界面：控件画网格，焦点日期那一格可以 Tab 到，键盘移动后把焦点移过去。
/// 三种视图：Day（月份网格）、Month（一年的各月）、Year（一个十年）。<see cref="Unit"/> 是选择的粒度：在比它粗的视图里点一格是"进到这一格"（年 → 月 → 日），不是选中。
/// 选项都是读取函数，值跟着外面变；外面改了已选的日期时调 <see cref="SelectionChanged"/>，改了粒度时调 <see cref="UnitChanged"/>（上游靠 watch）。
/// </summary>
public sealed class CalendarState
{
    DateUnit view;
    DateOnly? anchor;
    DateOnly? hovered;
    DateOnly focused;
    DateOnly page;
    bool started;

    /// <summary>选择方式。</summary>
    public Func<CalendarSelectionMode> Mode { get; set; } = () => CalendarSelectionMode.Single;

    /// <summary>已选的日期：单选一个、区间是 [开始, 结束]、多选若干；没有时空列表。</summary>
    public Func<IReadOnlyList<DateOnly>> Selected { get; set; } = () => [];

    /// <summary>选了日期：单选 [日期]（再点已选的那天也会调用），区间 [开始, 结束]（已排好序），多选是新的列表。</summary>
    public Action<IReadOnlyList<DateOnly>> Changed { get; set; } = _ => { };

    /// <summary>今天：没有已选日期时焦点从这里开始。</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>可选范围与不可选的日期。</summary>
    public Func<DateLimits> Limits { get; set; } = () => NoLimits;

    /// <summary>一周的第一天：Home / End、月份网格都按它。</summary>
    public Func<DayOfWeek> FirstDayOfWeek { get; set; } = () => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>选择的粒度，默认 Day；选月份时是 Month，选年份时是 Year。</summary>
    public Func<DateUnit> Unit { get; set; } = () => DateUnit.Day;

    /// <summary>并排显示几个月（只在 Day 视图），默认 1。</summary>
    public Func<int> VisibleMonths { get; set; } = () => 1;

    /// <summary>区间里允许夹着不可选的日期，默认 false：选了开始之后，越过不可选日期的那一侧都不能再选。</summary>
    public Func<bool> AllowsUnavailableInRange { get; set; } = () => false;

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    public Func<bool> ReadOnly { get; set; } = () => false;

    /// <summary>没有已选日期时一开始显示哪天所在的页（如酒店预订默认显示下个月）；null 时是今天。</summary>
    public Func<DateOnly?> DefaultFocused { get; set; } = () => null;

    static readonly DateLimits NoLimits = new();

    /// <summary>当前视图。</summary>
    public DateUnit View => Started().view;

    /// <summary>焦点日期：总是精确到天，切到月、年视图再回来时回到原来那天。</summary>
    public DateOnly Focused => Started().focused;

    /// <summary>当前页的第一格：日视图是第一个月的 1 日，月视图是这年 1 月 1 日，年视图是十年的第一年。</summary>
    public DateOnly Page => Started().page;

    /// <summary>区间选到一半：已经点了的开始。</summary>
    public DateOnly? Anchor => Started().anchor;

    /// <summary>日视图里并排的各月（每月 1 日）；其他视图只有 Page。</summary>
    public IReadOnlyList<DateOnly> Months =>
        View != DateUnit.Day ? [Page] : [.. Enumerable.Range(0, Count).Select(index => Page.AddMonths(index))];

    int Count => Math.Max(1, VisibleMonths());

    /// <summary>区间：已选的，或选到一半时从开始到悬停 / 焦点日期的预览。</summary>
    public DateRange? Range
    {
        get
        {
            if (Mode() != CalendarSelectionMode.Range) return null;
            if (Anchor is not { } start) return SelectedRange;
            return Ordered(start, Clamp(hovered ?? focused, EffectiveLimits));
        }
    }

    /// <summary>已选的日期有不能选的（范围外、不可选，或区间里夹着不可选的日期）。</summary>
    public bool Invalid
    {
        get
        {
            var limits = Limits();
            if (Selected().Any(date => IsDisabled(date, limits, Unit()))) return true;
            return SelectedRange is { } picked && !AllowsUnavailableInRange() && HasUnavailable(picked, limits.IsUnavailable);
        }
    }

    DateRange? SelectedRange => Mode() == CalendarSelectionMode.Range && Selected() is [var start, var end, ..] ? Ordered(start, end) : null;

    /// <summary>第一次用到时取初始的焦点与页（初始化器里的选项都设好了）。</summary>
    CalendarState Started()
    {
        if (started) return this;
        started = true;
        view = Unit();
        focused = Clamp(Selected().FirstOrDefault() is var first && Selected().Count > 0 ? first : DefaultFocused() ?? Today(), Limits());
        page = PageOf(focused, view);
        return this;
    }

    /// <summary>区间选到一半、不允许夹着不可选的日期时：开始两侧最近的不可选日期之内才能选。</summary>
    DateLimits EffectiveLimits
    {
        get
        {
            var limits = Limits();
            if (anchor is not { } start || limits.IsUnavailable is not { } unavailable || AllowsUnavailableInRange()) return limits;
            DateOnly? Bound(int step, DateOnly? edge)
            {
                // 最多找一年：再远的不会出现在同一次选择里
                var date = start;
                for (var offset = 0; offset < 366; offset++)
                {
                    var next = date.AddDays(step);
                    if (edge is { } limit && next.CompareTo(limit) * step > 0) return limit;
                    if (unavailable(next)) return date;
                    date = next;
                }
                return edge;
            }
            return limits with { Min = Bound(-1, limits.Min), Max = Bound(1, limits.Max) };
        }
    }

    /// <summary>日期所在页的第一格。</summary>
    static DateOnly PageOf(DateOnly date, DateUnit unit) => unit switch
    {
        DateUnit.Year => new DateOnly(Math.Max(1, date.Year / 10 * 10), 1, 1),
        DateUnit.Month => new DateOnly(date.Year, 1, 1),
        _ => new DateOnly(date.Year, date.Month, 1),
    };

    /// <summary>一页的跨度加到日期上。</summary>
    DateOnly AddPage(DateOnly date, DateUnit unit, int direction) => unit switch
    {
        DateUnit.Year => date.AddYears(10 * direction),
        DateUnit.Month => date.AddYears(direction),
        _ => date.AddMonths(Count * direction),
    };

    DateOnly PageEnd => AddPage(page, view, 1).AddDays(-1);

    /// <summary>月、年视图里的一格换成焦点那天落在这格里的日期：保留原来的月、日（这个月没有那天时取最后一天）。</summary>
    DateOnly KeepFocusedDay(DateOnly cell) => view switch
    {
        DateUnit.Year => Set(focused, cell.Year, focused.Month),
        DateUnit.Month => Set(focused, cell.Year, cell.Month),
        _ => cell,
    };

    static DateOnly Set(DateOnly date, int year, int month) => new(year, month, Math.Min(date.Day, DateTime.DaysInMonth(year, month)));

    /// <summary>焦点移到这一天（夹到可选范围内），移出当前页时翻页。</summary>
    void MoveFocus(DateOnly date)
    {
        focused = IsOutOfRange(date, EffectiveLimits, view) ? Clamp(date, EffectiveLimits) : date;
        FollowFocus();
    }

    /// <summary>焦点移出当前页时翻页：往前移出时焦点所在的月排在最后，往后移出时排在最前（同 React Aria）。</summary>
    void FollowFocus()
    {
        if (focused >= page && focused <= PageEnd) return;
        var backward = focused < page;
        page = backward && view == DateUnit.Day ? new DateOnly(focused.Year, focused.Month, 1).AddMonths(-(Count - 1)) : PageOf(focused, view);
    }

    /// <summary>切换视图（不能比选择的粒度更细）。</summary>
    /// <param name="next">视图。</param>
    public void SetView(DateUnit next)
    {
        Started();
        if (next > Unit()) return;
        view = next;
        page = PageOf(focused, next);
    }

    /// <summary>
    /// 移动焦点日期（夹到可选范围内），移出当前页时翻页。月、年视图里 date 代表它所在的那一格（某月、某年），焦点保留原来的日（和月）。
    /// </summary>
    /// <param name="date">日期。</param>
    public void Focus(DateOnly date) => Started().MoveFocus(KeepFocusedDay(date));

    /// <summary>这个方向还能不能翻页（整页都在可选范围之外时不能）。</summary>
    /// <param name="direction">1 往后，-1 往前。</param>
    public bool CanTurnPage(int direction)
    {
        Started();
        var limits = Limits();
        if (direction < 0) return limits.Min is not { } min || page.AddDays(-1) >= min;
        return limits.Max is not { } max || AddPage(page, view, 1) <= max;
    }

    /// <summary>翻页：日视图一次翻并排的月数，月视图一年，年视图十年；焦点跟着走。</summary>
    /// <param name="direction">1 往后，-1 往前。</param>
    public void TurnPage(int direction)
    {
        if (!CanTurnPage(direction)) return;
        page = AddPage(page, view, direction);
        focused = Clamp(AddPage(focused, view, direction), EffectiveLimits);
        // 夹回范围后可能不在翻到的这页了：焦点落到这页里可选的第一天
        if (focused < page || focused > PageEnd) focused = Clamp(page, EffectiveLimits);
    }

    /// <summary>点了一格：当前视图是选择的粒度时选中，否则进到这一格（年 → 月 → 日）。</summary>
    /// <param name="date">日期。</param>
    public void Activate(DateOnly date)
    {
        Started();
        if (view == Unit())
        {
            Focus(date);
            Select(date);
            return;
        }
        // 进到下一级视图：焦点保留原来的月、日
        var next = KeepFocusedDay(date);
        view += 1;
        focused = Clamp(next, EffectiveLimits);
        page = PageOf(focused, view);
    }

    /// <summary>指针悬停的日期：区间选到一半时预览；离开网格时传 null。</summary>
    /// <param name="date">日期。</param>
    public void Hover(DateOnly? date) => Started().hovered = date;

    /// <summary>取消选到一半的区间（Esc）。</summary>
    public void CancelRange()
    {
        anchor = null;
        hovered = null;
    }

    /// <summary>外面改了已选的日期（快捷按钮、重置）且不在这一页：翻到它那里（焦点就是那天，不保留原来的日）。</summary>
    public void SelectionChanged()
    {
        Started();
        if (Selected().Count > 0 && Selected()[0] is var date && (date < page || date > PageEnd)) MoveFocus(date);
    }

    /// <summary>选的粒度换了（按日改成按月）：视图回到新的粒度（停在原来的视图上时它可能比新粒度更细，再点一格就没有下一级可进）。</summary>
    public void UnitChanged()
    {
        Started();
        view = Unit();
        page = PageOf(focused, view);
    }

    void Select(DateOnly date)
    {
        if (ReadOnly() || IsDisabled(date, EffectiveLimits, Unit())) return;
        var current = Selected();
        var unit = Unit();
        switch (Mode())
        {
            case CalendarSelectionMode.Single:
                // 再点已选的那天也通知：日期选择器据此关闭弹层
                Changed([date]);
                return;
            case CalendarSelectionMode.Multiple:
                Changed(current.Any(item => Same(item, date, unit)) ? [.. current.Where(item => !Same(item, date, unit))] : [.. current, date]);
                return;
            default:
                if (anchor is not { } start)
                {
                    anchor = date;
                    return;
                }
                var picked = Ordered(start, date);
                anchor = null;
                hovered = null;
                Changed([picked.Start, picked.End]);
                return;
        }
    }

    /// <summary>一格的状态；month 是这格所在的月份网格（判断是不是补齐的上下月）。</summary>
    /// <param name="date">日期（月、年视图里是那个月、那年的第一天）。</param>
    /// <param name="month">这格所在的月份网格。</param>
    public CalendarCellState Cell(DateOnly date, DateOnly? month = null)
    {
        Started();
        var current = view;
        var preview = Range;
        var inRange = preview is { } range && Compare(date, range.Start, current) >= 0 && Compare(date, range.End, current) <= 0;
        return new CalendarCellState(
            Selected: Mode() == CalendarSelectionMode.Range ? inRange : Selected().Any(item => Same(item, date, current)),
            Disabled: IsDisabled(date, EffectiveLimits, current),
            Unavailable: current == DateUnit.Day && Limits().IsUnavailable?.Invoke(date) == true,
            Today: Same(date, Today(), current),
            Focused: Same(date, focused, current),
            Outside: current == DateUnit.Day ? month is { } shown && !Same(date, shown, DateUnit.Month) : current == DateUnit.Year && date.Year / 10 != page.Year / 10,
            RangeStart: preview is { } a && Same(date, a.Start, current),
            RangeEnd: preview is { } b && Same(date, b.End, current),
            InRange: inRange);
    }

    /// <summary>
    /// 日期网格的键盘操作（WAI-ARIA Date Picker Dialog 的网格部分）：日视图里 ←→ 一天（从右到左排版时反过来）、↑↓ 一周，Page Up / Down 一个月、加 Shift 一年，
    /// Home / End 到这周的第一天 / 最后一天；月、年视图里 ←→ 一格、↑↓ 一行（3 格），Page Up / Down 翻一页，Home / End 到这页的两端；
    /// Enter / 空格选中（或进到下一级视图）；Esc 取消选到一半的区间（没有时不处理，交给弹层关闭）。返回是否处理了。
    /// </summary>
    /// <param name="key">按键。</param>
    /// <param name="shift">按着 Shift。</param>
    /// <param name="rtl">从右到左排版。</param>
    public bool HandleKey(CalendarKey key, bool shift = false, bool rtl = false)
    {
        Started();
        var date = focused;
        var forward = rtl ? -1 : 1;
        switch (key)
        {
            case CalendarKey.Enter:
                Activate(date);
                return true;
            case CalendarKey.Escape:
                if (anchor is null) return false;
                CancelRange();
                return true;
        }
        if (view == DateUnit.Day)
        {
            var first = FirstDayOfWeek();
            Focus(key switch
            {
                CalendarKey.Right => date.AddDays(forward),
                CalendarKey.Left => date.AddDays(-forward),
                CalendarKey.Down => date.AddDays(7),
                CalendarKey.Up => date.AddDays(-7),
                CalendarKey.PageDown => shift ? date.AddYears(1) : date.AddMonths(1),
                CalendarKey.PageUp => shift ? date.AddYears(-1) : date.AddMonths(-1),
                CalendarKey.Home => StartOfWeek(date, first),
                _ => StartOfWeek(date, first).AddDays(6),
            });
            return true;
        }
        // 月、年视图：3 列的网格，一格是一个月或一年；年视图的一页是十年（前后补上的两年属于别的页）
        DateOnly By(int amount) => view == DateUnit.Month ? date.AddMonths(amount) : date.AddYears(amount);
        Focus(key switch
        {
            CalendarKey.Right => By(forward),
            CalendarKey.Left => By(-forward),
            CalendarKey.Down => By(3),
            CalendarKey.Up => By(-3),
            CalendarKey.PageDown => AddPage(date, view, 1),
            CalendarKey.PageUp => AddPage(date, view, -1),
            CalendarKey.Home => view == DateUnit.Month ? Set(date, date.Year, 1) : Set(date, page.Year, date.Month),
            _ => view == DateUnit.Month ? Set(date, date.Year, 12) : Set(date, page.Year + 9, date.Month),
        });
        return true;
    }

    // ───────── 日期的比较、范围与网格 ─────────

    /// <summary>按粒度比较：小于 0 表示 a 在前。</summary>
    public static int Compare(DateOnly a, DateOnly b, DateUnit unit) => unit switch
    {
        DateUnit.Year => a.Year.CompareTo(b.Year),
        DateUnit.Month => (a.Year * 12 + a.Month).CompareTo(b.Year * 12 + b.Month),
        _ => a.CompareTo(b),
    };

    /// <summary>是不是同一天 / 同一月 / 同一年。</summary>
    public static bool Same(DateOnly a, DateOnly b, DateUnit unit) => Compare(a, b, unit) == 0;

    /// <summary>在范围外（按粒度）。</summary>
    public static bool IsOutOfRange(DateOnly date, DateLimits limits, DateUnit unit = DateUnit.Day) =>
        limits.Min is { } min && Compare(date, min, unit) < 0 || limits.Max is { } max && Compare(date, max, unit) > 0;

    /// <summary>不能选：在范围外，或是不可选的日期（只在天的粒度上判断 IsUnavailable）。</summary>
    public static bool IsDisabled(DateOnly date, DateLimits limits, DateUnit unit = DateUnit.Day) =>
        IsOutOfRange(date, limits, unit) || unit == DateUnit.Day && limits.IsUnavailable?.Invoke(date) == true;

    /// <summary>把日期夹到 Min–Max 之间。</summary>
    public static DateOnly Clamp(DateOnly date, DateLimits limits) =>
        limits.Min is { } min && date < min ? min : limits.Max is { } max && date > max ? max : date;

    /// <summary>两天排好先后的区间。</summary>
    public static DateRange Ordered(DateOnly a, DateOnly b) => a <= b ? new DateRange(a, b) : new DateRange(b, a);

    /// <summary>区间里有不可选的日期。</summary>
    public static bool HasUnavailable(DateRange range, Func<DateOnly, bool>? isUnavailable)
    {
        if (isUnavailable is null) return false;
        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
            if (isUnavailable(date)) return true;
        return false;
    }

    /// <summary>按今天算出快捷区间（vue-tsx-pro 的 presetRange）。</summary>
    /// <param name="preset">快捷区间。</param>
    /// <param name="today">今天。</param>
    /// <param name="first">一周的第一天（本周用）。</param>
    public static DateRange PresetRange(DateRangePreset preset, DateOnly today, DayOfWeek first)
    {
        var month = new DateOnly(today.Year, today.Month, 1);
        return preset switch
        {
            DateRangePreset.Today => new(today, today),
            DateRangePreset.Yesterday => new(today.AddDays(-1), today.AddDays(-1)),
            DateRangePreset.Last7Days => new(today.AddDays(-6), today),
            DateRangePreset.Last30Days => new(today.AddDays(-29), today),
            DateRangePreset.ThisWeek => new(StartOfWeek(today, first), StartOfWeek(today, first).AddDays(6)),
            DateRangePreset.ThisMonth => new(month, month.AddMonths(1).AddDays(-1)),
            DateRangePreset.LastMonth => new(month.AddMonths(-1), month.AddDays(-1)),
            DateRangePreset.ThisYear => new(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }

    /// <summary>这一天所在的周的第一天。</summary>
    public static DateOnly StartOfWeek(DateOnly date, DayOfWeek first) => date.AddDays(-(((int)date.DayOfWeek - (int)first + 7) % 7));

    /// <summary>一个月的日期网格：按周分行，首尾用上个月、下个月的日期补齐；fixedWeeks 时总是 6 行。</summary>
    public static IReadOnlyList<IReadOnlyList<DateOnly>> MonthGrid(DateOnly month, DayOfWeek first, bool fixedWeeks = true)
    {
        var start = StartOfWeek(new DateOnly(month.Year, month.Month, 1), first);
        var last = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        var weeks = new List<IReadOnlyList<DateOnly>>();
        for (var day = start; weeks.Count < 6; day = day.AddDays(7))
        {
            if (!fixedWeeks && day > last) break;
            var row = day;
            weeks.Add([.. Enumerable.Range(0, 7).Select(offset => row.AddDays(offset))]);
        }
        return weeks;
    }

    /// <summary>一年的各月（每月 1 日）。</summary>
    public static IReadOnlyList<DateOnly> MonthsOfYear(DateOnly date) => [.. Enumerable.Range(1, 12).Select(month => new DateOnly(date.Year, month, 1))];

    /// <summary>年份视图的一页：所在的十年，前后各补一年（共 12 年，排成 4 × 3；同 Ant Design 的年份面板）。</summary>
    public static IReadOnlyList<DateOnly> YearsOfDecade(DateOnly date) =>
        [.. Enumerable.Range(-1, 12).Select(offset => date.Year / 10 * 10 + offset).Where(year => year is >= 1 and <= 9999).Select(year => new DateOnly(year, 1, 1))];

    /// <summary>
    /// 一年里的第几周：一周从周一开始时按 ISO 8601（含 1 月 4 日的那周是第 1 周），其他（周日、周六开始）按含 1 月 1 日的那周是第 1 周（美国、日本的惯例）。
    /// </summary>
    public static int WeekOfYear(DateOnly date, DayOfWeek first)
    {
        var monday = first == DayOfWeek.Monday;
        var weekStart = StartOfWeek(date, first);
        // 一周归属的年份：ISO 按这周的周四，其他按这周的最后一天（含 1 月 1 日的周就算新年的第 1 周）
        var anchor = weekStart.AddDays(monday ? 3 : 6);
        var firstWeek = StartOfWeek(new DateOnly(anchor.Year, 1, monday ? 4 : 1), first);
        return (weekStart.DayNumber - firstWeek.DayNumber) / 7 + 1;
    }
}
