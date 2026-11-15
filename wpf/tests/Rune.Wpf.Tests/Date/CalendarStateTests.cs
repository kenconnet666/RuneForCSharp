namespace Rune.Wpf.Tests;

/// <summary>CalendarState：单选 / 区间 / 多选、区间预览与不可选日期、翻页、三种视图、日期网格的键盘操作（周一、周日开始）。</summary>
public class CalendarStateTests
{
    static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    static string[] Iso(IEnumerable<DateOnly> dates) => [.. dates.Select(date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))];

    sealed class Setup
    {
        public readonly CalendarState State;
        public IReadOnlyList<DateOnly> Value;

        public Setup(CalendarSelectionMode mode = CalendarSelectionMode.Single, string[]? selected = null, DateLimits? limits = null,
            DayOfWeek first = DayOfWeek.Monday, DateUnit unit = DateUnit.Day, int visibleMonths = 1)
        {
            Value = [.. (selected ?? []).Select(D)];
            State = new CalendarState
            {
                Mode = () => mode,
                Selected = () => Value,
                Changed = next => Value = next,
                Today = () => D("2026-10-20"),
                Limits = () => limits ?? new DateLimits(),
                FirstDayOfWeek = () => first,
                Unit = () => unit,
                VisibleMonths = () => visibleMonths,
            };
        }

        public string[] Values => Iso(Value);

        public bool Press(CalendarKey key, bool shift = false, bool rtl = false) => State.HandleKey(key, shift, rtl);

        public string Focused => Iso([State.Focused])[0];
    }

    [Fact]
    [Upstream("calendar-state", "单选：焦点从已选日期开始；点另一天替换，再点同一天不取消")]
    public void Single_selection_replaces_and_reselecting_keeps_it()
    {
        var setup = new Setup(selected: ["2026-10-05"]);
        Assert.Equal("2026-10-05", setup.Focused);
        setup.State.Activate(D("2026-10-08"));
        Assert.Equal(["2026-10-08"], setup.Values);
        setup.State.Activate(D("2026-10-08"));
        Assert.Equal(["2026-10-08"], setup.Values);
    }

    [Fact]
    [Upstream("calendar-state", "多选：每天独立切换")]
    public void Multiple_selection_toggles_each_day()
    {
        var setup = new Setup(CalendarSelectionMode.Multiple);
        setup.State.Activate(D("2026-10-01"));
        setup.State.Activate(D("2026-10-03"));
        setup.State.Activate(D("2026-10-01"));
        Assert.Equal(["2026-10-03"], setup.Values);
    }

    [Fact]
    [Upstream("calendar-state", "区间：先点开始，悬停时预览，再点结束（反着点也排好序）")]
    public void Ranges_preview_on_hover_and_are_ordered()
    {
        var setup = new Setup(CalendarSelectionMode.Range);
        setup.State.Activate(D("2026-10-15"));
        Assert.Equal(D("2026-10-15"), setup.State.Anchor);
        setup.State.Hover(D("2026-10-10"));
        Assert.Equal(D("2026-10-10"), setup.State.Range?.Start);
        Assert.True(setup.State.Cell(D("2026-10-12")).InRange);
        Assert.True(setup.State.Cell(D("2026-10-10")).RangeStart);
        setup.State.Activate(D("2026-10-10"));
        Assert.Equal(["2026-10-10", "2026-10-15"], setup.Values);
        Assert.Null(setup.State.Anchor);
    }

    [Fact]
    [Upstream("calendar-state", "区间里不能夹着不可选的日期：选了开始后，越过不可选日期的那一侧都不能选")]
    public void Ranges_cannot_cross_unavailable_days()
    {
        var setup = new Setup(CalendarSelectionMode.Range, limits: new DateLimits(IsUnavailable: day => day == D("2026-10-18")));
        setup.State.Activate(D("2026-10-15"));
        Assert.False(setup.State.Cell(D("2026-10-17")).Disabled);
        Assert.True(setup.State.Cell(D("2026-10-19")).Disabled);
        // 悬停在越界的日期上：预览停在边界
        setup.State.Hover(D("2026-10-25"));
        Assert.Equal(D("2026-10-17"), setup.State.Range?.End);
        setup.State.Activate(D("2026-10-19"));
        Assert.Empty(setup.Values);
        setup.State.Activate(D("2026-10-17"));
        Assert.Equal(["2026-10-15", "2026-10-17"], setup.Values);
    }

    [Fact]
    [Upstream("calendar-state", "Esc 取消选到一半的区间；没有选到一半时不处理（交给弹层）")]
    public void Escape_cancels_a_half_picked_range()
    {
        var setup = new Setup(CalendarSelectionMode.Range);
        Assert.False(setup.Press(CalendarKey.Escape));
        setup.State.Activate(D("2026-10-15"));
        Assert.True(setup.Press(CalendarKey.Escape));
        Assert.Null(setup.State.Anchor);
    }

    [Fact]
    [Upstream("calendar-state", "范围外、不可选的日期不能选；已选的日期不能选时 invalid")]
    public void Disabled_days_cannot_be_picked_and_mark_invalid()
    {
        var setup = new Setup(selected: ["2026-10-12"], limits: new DateLimits(Min: D("2026-10-10"), IsUnavailable: day => day.Day == 12));
        Assert.True(setup.State.Invalid);
        setup.State.Activate(D("2026-10-05"));
        Assert.Equal(["2026-10-12"], setup.Values);
        var cell = setup.State.Cell(D("2026-10-12"));
        Assert.True(cell.Disabled && cell.Unavailable);
    }

    [Fact]
    [Upstream("calendar-state", "焦点移出当前页时翻页；并排两个月时往前移出，焦点所在的月排在最后")]
    public void Focus_turns_the_page_and_keeps_the_month_order()
    {
        var setup = new Setup(selected: ["2026-10-01"], visibleMonths: 2);
        Assert.Equal(["2026-10-01", "2026-11-01"], Iso(setup.State.Months));
        setup.State.Focus(D("2026-11-30"));
        Assert.Equal(D("2026-10-01"), setup.State.Page);
        setup.State.Focus(D("2026-12-01"));
        Assert.Equal(["2026-12-01", "2027-01-01"], Iso(setup.State.Months));
        setup.State.Focus(D("2026-11-30"));
        Assert.Equal(["2026-10-01", "2026-11-01"], Iso(setup.State.Months));
    }

    [Fact]
    [Upstream("calendar-state", "翻页按钮一次翻并排的月数；整页在可选范围外时不能翻")]
    public void Page_turns_respect_the_limits()
    {
        var setup = new Setup(visibleMonths: 2, limits: new DateLimits(D("2026-09-15"), D("2026-12-10")));
        Assert.True(setup.State.CanTurnPage(-1));
        setup.State.TurnPage(-1);
        Assert.Equal(["2026-08-01", "2026-09-01"], Iso(setup.State.Months));
        Assert.Equal("2026-09-15", setup.Focused);
        Assert.False(setup.State.CanTurnPage(-1));
        setup.State.TurnPage(1);
        setup.State.TurnPage(1);
        Assert.Equal(["2026-12-01", "2027-01-01"], Iso(setup.State.Months));
        Assert.False(setup.State.CanTurnPage(1));
    }

    [Fact]
    [Upstream("calendar-state", "年 → 月 → 日：在粗的视图里点一格是进到这一格，焦点保留原来的日")]
    public void Coarser_views_drill_down_keeping_the_day()
    {
        var setup = new Setup(selected: ["2026-10-20"]);
        setup.State.SetView(DateUnit.Year);
        Assert.Equal(D("2020-01-01"), setup.State.Page);
        Assert.True(setup.State.Cell(D("2019-01-01")).Outside);
        setup.State.Activate(D("2028-01-01"));
        Assert.Equal((DateUnit.Month, "2028-10-20"), (setup.State.View, setup.Focused));
        setup.State.Activate(D("2028-02-01"));
        Assert.Equal((DateUnit.Day, "2028-02-20"), (setup.State.View, setup.Focused));
        Assert.Equal(["2026-10-20"], setup.Values);
    }

    [Fact]
    [Upstream("calendar-state", "选的单位从日换成月：视图跟着换，点一格是选中这个月")]
    public void Switching_the_unit_switches_the_view()
    {
        var unit = DateUnit.Day;
        IReadOnlyList<DateOnly> value = [D("2026-10-20")];
        var state = new CalendarState { Selected = () => value, Changed = next => value = next, Today = () => D("2026-10-20"), Unit = () => unit };
        Assert.Equal(DateUnit.Day, state.View);
        unit = DateUnit.Month;
        state.UnitChanged();
        Assert.Equal((DateUnit.Month, D("2026-01-01")), (state.View, state.Page));
        state.Activate(D("2026-03-01"));
        Assert.Equal(DateUnit.Month, state.View);
        Assert.Equal((2026, 3), (value[0].Year, value[0].Month));
    }

    [Fact]
    [Upstream("calendar-state", "粗的视图里聚焦一格（点击前浏览器先聚焦）：焦点保留原来的日；父级改值时焦点就是那天")]
    public void Focusing_coarse_cells_keeps_the_day_and_outside_values_move_focus()
    {
        var setup = new Setup(selected: ["1995-06-18"]);
        setup.State.SetView(DateUnit.Year);
        setup.State.Focus(D("2001-01-01"));
        Assert.Equal("2001-06-18", setup.Focused);
        setup.State.Activate(D("2001-01-01"));
        setup.State.Focus(D("2001-09-01"));
        Assert.Equal("2001-09-18", setup.Focused);
        setup.State.Activate(D("2001-09-01"));
        Assert.Equal((DateUnit.Day, "2001-09-18"), (setup.State.View, setup.Focused));
        // 这个月没有那天：取最后一天
        setup.State.Focus(D("2001-10-31"));
        setup.State.SetView(DateUnit.Month);
        setup.State.Focus(D("2001-02-01"));
        Assert.Equal("2001-02-28", setup.Focused);
        setup.Value = [D("2030-03-05")];
        setup.State.SelectionChanged();
        Assert.Equal("2030-03-05", setup.Focused);
    }

    [Fact]
    [Upstream("calendar-state", "选择粒度是月（MonthPicker）：月份视图里点一格就是选中，不能切到日视图")]
    public void Month_unit_selects_in_the_month_view()
    {
        var setup = new Setup(unit: DateUnit.Month);
        Assert.Equal(DateUnit.Month, setup.State.View);
        setup.State.SetView(DateUnit.Day);
        Assert.Equal(DateUnit.Month, setup.State.View);
        setup.State.Activate(D("2026-03-01"));
        Assert.Equal(["2026-03-01"], setup.Values);
        Assert.True(setup.State.Cell(D("2026-03-01")).Selected);
        Assert.True(setup.State.Cell(D("2026-10-01")).Today);
    }

    [Fact]
    [Upstream("calendar-state", "方向键一天、一周；从右到左排版时左右反过来")]
    public void Arrows_move_a_day_or_a_week()
    {
        var setup = new Setup(selected: ["2026-10-20"]);
        setup.Press(CalendarKey.Right);
        Assert.Equal("2026-10-21", setup.Focused);
        setup.Press(CalendarKey.Down);
        Assert.Equal("2026-10-28", setup.Focused);
        setup.Press(CalendarKey.Right, rtl: true);
        Assert.Equal("2026-10-27", setup.Focused);
    }

    [Fact]
    [Upstream("calendar-state", "Page Up / Down 一个月，加 Shift 一年；月末夹到下个月的最后一天")]
    public void Page_keys_move_a_month_or_a_year()
    {
        var setup = new Setup(selected: ["2026-01-31"]);
        setup.Press(CalendarKey.PageDown);
        Assert.Equal("2026-02-28", setup.Focused);
        setup.Press(CalendarKey.PageDown, shift: true);
        Assert.Equal("2027-02-28", setup.Focused);
        Assert.Equal(D("2027-02-01"), setup.State.Page);
    }

    [Theory]
    [Upstream("calendar-state", "Home / End 到这周两端")]
    [InlineData(DayOfWeek.Monday, "2026-10-19", "2026-10-25")]
    [InlineData(DayOfWeek.Sunday, "2026-10-18", "2026-10-24")]
    public void Home_and_end_go_to_the_week_ends(DayOfWeek first, string home, string end)
    {
        var setup = new Setup(selected: ["2026-10-20"], first: first);
        setup.Press(CalendarKey.Home);
        Assert.Equal(home, setup.Focused);
        setup.Press(CalendarKey.End);
        Assert.Equal(end, setup.Focused);
    }

    [Fact]
    [Upstream("calendar-state", "不越过最小最大日期；Enter / 空格选中")]
    public void Keys_stay_within_limits_and_enter_selects()
    {
        var setup = new Setup(limits: new DateLimits(Max: D("2026-10-22")));
        setup.Press(CalendarKey.Down);
        Assert.Equal("2026-10-22", setup.Focused);
        setup.Press(CalendarKey.Enter);
        Assert.Equal(["2026-10-22"], setup.Values);
    }

    [Fact]
    [Upstream("calendar-state", "月份视图：←→ 一个月，↑↓ 三个月，Home / End 到 1 月 / 12 月，Page 一年")]
    public void Month_view_keys()
    {
        var setup = new Setup(unit: DateUnit.Month, selected: ["2026-05-01"]);
        setup.Press(CalendarKey.Down);
        Assert.Equal(8, setup.State.Focused.Month);
        setup.Press(CalendarKey.End);
        Assert.Equal(12, setup.State.Focused.Month);
        setup.Press(CalendarKey.Right);
        Assert.Equal("2027-01-01", setup.Focused);
        Assert.Equal(2027, setup.State.Page.Year);
        setup.Press(CalendarKey.PageUp);
        Assert.Equal(2026, setup.State.Focused.Year);
    }

    [Fact]
    [Upstream("calendar-state", "年份视图：Home / End 到十年的两端，Page 十年")]
    public void Year_view_keys()
    {
        var setup = new Setup(unit: DateUnit.Year, selected: ["2026-01-01"]);
        setup.Press(CalendarKey.Home);
        Assert.Equal(2020, setup.State.Focused.Year);
        setup.Press(CalendarKey.End);
        Assert.Equal(2029, setup.State.Focused.Year);
        setup.Press(CalendarKey.PageDown);
        Assert.Equal((2039, 2030), (setup.State.Focused.Year, setup.State.Page.Year));
    }

    [Fact]
    public void Grids_and_week_numbers()
    {
        var weeks = CalendarState.MonthGrid(D("2026-10-01"), DayOfWeek.Monday);
        Assert.Equal((6, D("2026-09-28"), D("2026-11-08")), (weeks.Count, weeks[0][0], weeks[^1][^1]));
        Assert.Equal(5, CalendarState.MonthGrid(D("2026-10-01"), DayOfWeek.Monday, fixedWeeks: false).Count);
        Assert.Equal(12, CalendarState.YearsOfDecade(D("2026-01-01")).Count);
        Assert.Equal(43, CalendarState.WeekOfYear(D("2026-10-20"), DayOfWeek.Monday));
        Assert.Equal(1, CalendarState.WeekOfYear(D("2026-01-01"), DayOfWeek.Sunday));
    }
}
