using System.Globalization;

namespace Rune.WinForms.Tests;

/// <summary>CalendarControl：网格与读屏名称、点选与键盘、语言与一周的第一天、可选范围、多选、并排几个月、周数与自定义格子、月份与年份视图、按月选择。今天固定为 2026-10-20（周二）。</summary>
public class CalendarTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (CalendarControl Calendar, Form Window) Show(CalendarControl calendar, Lang? lang = null)
    {
        calendar.State!.Today = () => Today;
        var provider = new ProviderControl { Lang = lang ?? Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(calendar);
        var window = WinFormsUi.Show(provider);
        WinFormsUi.Pump();
        return (calendar, window);
    }

    static DateOnly Day(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>显示着的这一格：所在的网格与状态。</summary>
    static (CalendarGridControl Grid, CalendarCellState State) Cell(CalendarControl calendar, string iso)
    {
        var date = Day(iso);
        foreach (var grid in calendar.Grids)
            foreach (var cell in grid.Cells)
                if (cell.Date == date) return (grid, cell.State);
        throw new InvalidOperationException($"{iso} 不在显示的网格里");
    }

    static string NameOf(CalendarControl calendar, string iso) => Cell(calendar, iso) is var (grid, state) ? grid.NameOf(Day(iso), state) : "";

    // 同鼠标点击：不能选的格子也会收到点击（由日历自己拦下）
    static void Click(CalendarControl calendar, string iso)
    {
        var (grid, state) = Cell(calendar, iso);
        grid.Activate(Day(iso), state);
    }

    static CalendarGridControl Focused(CalendarControl calendar) => calendar.Grids.Single(grid => grid.TabStop);

    static void Press(CalendarControl calendar, Keys keys) => Focused(calendar).Press(keys);

    [Fact]
    [Upstream("Calendar", "网格：星期表头取自 Intl")]
    public Task Grid_has_weekdays_full_date_names_and_a_single_tab_stop() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20" });
        using var _ = window;
        var grid = calendar.Grids.Single();
        Assert.Equal("2026年10月", grid.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.Table, grid.AccessibilityObject.Role);
        Assert.Equal(["一", "二", "三", "四", "五", "六", "日"], grid.Weekdays.Select(day => day.Short));
        Assert.Equal(42, grid.Cells.Count());
        var (_, selected) = Cell(calendar, "2026-10-20");
        Assert.True(selected.Selected && selected.Focused);
        Assert.True(grid.TabStop);
        Assert.Equal("今天，2026年10月20日星期二", NameOf(calendar, "2026-10-20"));
        Assert.False(Cell(calendar, "2026-10-21").State.Focused);
        // 补齐的上一个月的日期变淡，但能点
        Assert.True(Cell(calendar, "2026-09-28").State.Outside);
        Click(calendar, "2026-10-08");
        Assert.Equal("2026-10-08", calendar.Value);
        Assert.Equal(new DateOnly(2026, 10, 8), calendar.Date);
        var accessible = grid.AccessibilityObject.GetChild(0)!;
        Assert.Equal(AccessibleRole.Cell, accessible.Role);
    });

    [Fact]
    [Upstream("Calendar", "键盘：方向键移动焦点并聚焦新格子，跨月时翻页")]
    public Task Arrow_keys_move_focus_and_turn_pages() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-31" });
        using var _ = window;
        Press(calendar, Keys.Right);
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.State!.Focused);
        Assert.Equal("2026年11月", Focused(calendar).AccessibilityObject.Name);
        Press(calendar, Keys.PageUp | Keys.Shift);
        Assert.Equal(new DateOnly(2025, 11, 1), calendar.State.Focused);
        Assert.True(Cell(calendar, "2025-11-01").State.Focused);
    });

    [Fact]
    [Upstream("Calendar", "en-US：一周从周日开始，英文的月份与按钮名称")]
    public Task English_starts_on_sunday_with_english_names() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20" }, Lang.EnUs);
        using var _ = window;
        var grid = calendar.Grids.Single();
        Assert.Equal(("S", "Sunday"), grid.Weekdays[0]);
        Assert.Equal("October 2026", grid.AccessibilityObject.Name);
        Assert.Equal("Today, Tuesday, October 20, 2026", NameOf(calendar, "2026-10-20"));
        Assert.Equal("Previous month", calendar.Header[0].AccessibleName);
    });

    [Fact]
    [Upstream("Calendar", "firstDayOfWeek 覆盖语言惯例")]
    public Task First_day_of_week_overrides_the_language() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20", FirstDayOfWeek = DayOfWeek.Sunday });
        using var _ = window;
        Assert.Equal("日", calendar.Grids.Single().Weekdays[0].Short);
    });

    [Fact]
    [Upstream("Calendar", "最小最大日期与不可选日期")]
    public Task Limits_and_unavailable_dates_cannot_be_picked() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl
        {
            DefaultFocused = "2026-10-15",
            Min = "2026-10-10",
            Max = "2026-10-25",
            IsDateUnavailable = date => date == new DateOnly(2026, 10, 18),
        });
        using var _ = window;
        Assert.False(calendar.Header[0].Enabled);
        Assert.False(calendar.Header[2].Enabled);
        Assert.True(Cell(calendar, "2026-10-05").State.Disabled);
        var grid = calendar.Grids.Single();
        var cells = Enumerable.Range(0, grid.AccessibilityObject.GetChildCount()).Select(i => grid.AccessibilityObject.GetChild(i)!).ToList();
        Assert.True(cells.Single(cell => cell.Name!.StartsWith("2026年10月5日", StringComparison.Ordinal)).State.HasFlag(AccessibleStates.Unavailable));
        Assert.True(Cell(calendar, "2026-10-18").State.Unavailable);
        Click(calendar, "2026-10-18");
        Click(calendar, "2026-10-05");
        Assert.Null(calendar.Value);
    });

    [Fact]
    [Upstream("Calendar", "min / max 写成日期时间时按它的日期禁用")]
    public Task Date_time_limits_compare_by_day() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { DefaultFocused = "2026-10-15", Min = "2026-10-10T14:00", Max = "2026-10-25T09:30:00" });
        using var _ = window;
        Assert.True(Cell(calendar, "2026-10-09").State.Disabled);
        Assert.False(Cell(calendar, "2026-10-10").State.Disabled);
        Assert.True(Cell(calendar, "2026-10-26").State.Disabled);
    });

    [Fact]
    [Upstream("Calendar", "多选：每天独立切换")]
    public Task Multiple_selection_toggles_each_day() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { SelectionMode = CalendarSelectionMode.Multiple, DefaultFocused = "2026-10-01" });
        using var _ = window;
        Click(calendar, "2026-10-02");
        Click(calendar, "2026-10-09");
        Click(calendar, "2026-10-02");
        Assert.Equal(["2026-10-09"], calendar.Values);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SelectionMode = CalendarSelectionMode.Range);
    });

    [Fact]
    [Upstream("Calendar", "并排两个月：每月一张网格，补齐的日期留空，翻页按钮只在两端")]
    public Task Two_months_side_by_side() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20", VisibleMonths = 2 });
        using var _ = window;
        Assert.Equal(["2026年10月", "2026年11月"], calendar.Grids.Select(grid => grid.AccessibilityObject.Name));
        Assert.Single(calendar.Grids.SelectMany(grid => grid.Cells), cell => cell.Date == new DateOnly(2026, 10, 31));
        Assert.Equal([true, false, false, true], new[] { 0, 2, 3, 5 }.Select(i => calendar.Header[i] is IconButtonControl));
    });

    [Fact]
    [Upstream("Calendar", "周数与自定义格子")]
    public Task Week_numbers_and_custom_cells() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl
        {
            Value = "2026-10-20",
            ShowWeekNumbers = true,
            CellText = (date, cell) => cell.Selected ? $"✓{date.Day:00}" : $"{date.Day:00}",
        });
        using var _ = window;
        Assert.Equal(("40", "第 40 周"), calendar.Grids.Single().WeekNumbers[0]);
        // 自定义内容跟在完整日期后面一起念
        Assert.Equal("今天，2026年10月20日星期二✓20", NameOf(calendar, "2026-10-20"));
    });

    [Fact]
    [Upstream("Calendar", "标题切到月份视图、再到年份视图；点年份、月份逐级回到日期")]
    public Task Heading_switches_to_month_and_year_views() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20" });
        using var _ = window;
        Assert.Equal("选择月份：2026年10月", calendar.Header[1].AccessibleName);
        ((ButtonControl)calendar.Header[1]).PerformClick();
        Assert.Equal(DateUnit.Month, calendar.State!.View);
        Assert.Equal("2026年3月", NameOf(calendar, "2026-03-01"));
        ((ButtonControl)calendar.Header[1]).PerformClick();
        Assert.Equal("2020年 – 2029年", calendar.Grids.Single().AccessibilityObject.Name);
        // 年份视图是最粗的：标题只是文字
        Assert.IsType<TextControl>(calendar.Header[1]);
        Click(calendar, "2028-01-01");
        Click(calendar, "2028-02-01");
        Assert.Equal(DateUnit.Day, calendar.State.View);
        Assert.Equal(new DateOnly(2028, 2, 20), calendar.State.Focused);
        Assert.Equal("2026-10-20", calendar.Value);
    });

    [Fact]
    [Upstream("Calendar", "granularity=")]
    public Task Month_granularity_picks_a_month() => WinFormsUi.Run(() =>
    {
        var picks = new List<string>();
        var (calendar, window) = Show(new CalendarControl { Granularity = DateUnit.Month, DefaultFocused = "2026-10-01" });
        using var _ = window;
        calendar.Picked += (_, e) => picks.Add(e.Value);
        Click(calendar, "2026-04-01");
        Assert.Equal("2026-04-01", calendar.Value);
        Assert.Equal(["2026-04-01"], picks);
    });

    [Fact]
    [Upstream("Calendar", "点标题切到年份视图，点年、点月逐级回来")]
    public Task Pointer_drill_down_keeps_the_focused_month_and_day() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "1995-06-18", Max = "2026-12-31" });
        using var _ = window;
        ((ButtonControl)calendar.Header[1]).PerformClick();
        ((ButtonControl)calendar.Header[1]).PerformClick();
        Click(calendar, "1998-01-01");
        Assert.Equal(new DateOnly(1998, 6, 18), calendar.State!.Focused);
        Assert.True(Cell(calendar, "1998-06-01").State.Focused);
        Click(calendar, "1998-09-01");
        Assert.Equal(DateUnit.Day, calendar.State.View);
        Assert.Equal(new DateOnly(1998, 9, 18), calendar.State.Focused);
    });

    [Fact]
    [Upstream("Calendar", "日期网格（CalendarView）的键盘")]
    public Task Keyboard_moves_by_day_week_month_year_and_enter_selects() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "1995-06-18" });
        using var _ = window;
        void Moves(Keys keys, string expected)
        {
            Press(calendar, keys);
            Assert.Equal(Day(expected), calendar.State!.Focused);
            Assert.True(Cell(calendar, expected).Grid.TabStop);
        }
        Moves(Keys.Right, "1995-06-19");
        Moves(Keys.Down, "1995-06-26");
        Press(calendar, Keys.Up);
        Moves(Keys.Left, "1995-06-18");
        Moves(Keys.PageDown, "1995-07-18");
        Moves(Keys.PageUp | Keys.Shift, "1994-07-18");
        Moves(Keys.PageUp, "1994-06-18");
        // 这周的第一天、最后一天（中文区域一周从周一开始；1994-06-18 是周六）
        Moves(Keys.Home, "1994-06-13");
        Moves(Keys.End, "1994-06-19");
        Press(calendar, Keys.Enter);
        Assert.Equal("1994-06-19", calendar.Value);
    });

    [Fact]
    public Task Outside_values_turn_to_their_month() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20" });
        using var _ = window;
        calendar.Date = new DateOnly(2027, 3, 5);
        Assert.Equal("2027年3月", calendar.Grids.Single().AccessibilityObject.Name);
        Assert.True(Cell(calendar, "2027-03-05").State.Focused);
    });

    [Fact]
    public Task Paints_every_view_without_throwing() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new CalendarControl { Value = "2026-10-20", VisibleMonths = 2, ShowWeekNumbers = true, IsDateUnavailable = date => date.Day == 18 });
        using var _ = window;
        foreach (var grid in calendar.Grids) WinFormsUi.Paint(grid);
        ((ButtonControl)calendar.Header[1]).PerformClick();
        foreach (var grid in calendar.Grids) WinFormsUi.Paint(grid);
        Assert.True(calendar.Width > 0 && calendar.Height > 0);
    });
}
