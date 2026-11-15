using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>Calendar：网格与读屏名称、点选与键盘、语言与一周的第一天、可选范围、多选、并排几个月、周数与自定义格子、月份与年份视图、按月选择。今天固定为 2026-10-20（周二）。</summary>
public class CalendarTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (Calendar Calendar, Window Window) Show(Calendar calendar, Lang? lang = null)
    {
        calendar.State!.Today = () => Today;
        Provider.SetLang(calendar, lang ?? Lang.ZhCn);
        return (calendar, HeadlessUi.Show(calendar));
    }

    static CalendarCellButton Cell(Calendar calendar, string iso) => calendar.Cells.First(cell => cell.Date == DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture));

    static IEnumerable<CalendarCellButton> CellsOf(Calendar calendar, string iso) => calendar.Cells.Where(cell => cell.Date == DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture));

    static List<Panel> Grids(Calendar calendar) => [.. calendar.GetLogicalDescendants().OfType<Panel>().Where(grid => grid.Name == CalendarViewParts.Grid)];

    static string? NameOf(Control control) => control.Peer().GetName();

    // 同鼠标点击：不能选的格子也会收到点击（由日历自己拦下）
    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static List<Control> Named(Calendar calendar, string name) => [.. calendar.GetLogicalDescendants().OfType<Control>().Where(control => control.Name == name)];

    [Fact]
    [Upstream("Calendar", "网格：星期表头取自 Intl")]
    public Task Grid_has_weekdays_full_date_names_and_a_single_tab_stop() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20" });
        var grid = Grids(calendar).Single();
        Assert.Equal("2026年10月", NameOf(grid));
        Assert.Equal(["一", "二", "三", "四", "五", "六", "日"], Named(calendar, CalendarViewParts.Weekday).Cast<TextBlock>().Select(text => text.Text));
        Assert.Equal(7 + 6 * 7, grid.Children.Count);
        var selected = Cell(calendar, "2026-10-20");
        Assert.True(selected.State.Selected);
        Assert.Contains(":selected", selected.Classes);
        Assert.True(selected.IsTabStop);
        Assert.Equal("今天，2026年10月20日星期二", NameOf(selected));
        Assert.False(Cell(calendar, "2026-10-21").IsTabStop);
        // 补齐的上一个月的日期变淡，但能点
        Assert.Contains(":outside", Cell(calendar, "2026-09-28").Classes);
        Click(Cell(calendar, "2026-10-08"));
        Assert.Equal("2026-10-08", calendar.Value);
        Assert.Equal(new DateOnly(2026, 10, 8), calendar.Date);
    });

    [Fact]
    [Upstream("Calendar", "键盘：方向键移动焦点并聚焦新格子，跨月时翻页")]
    public Task Arrow_keys_move_focus_and_turn_pages() => HeadlessUi.Run(() =>
    {
        var (calendar, window) = Show(new Calendar { Value = "2026-10-31" });
        Cell(calendar, "2026-10-31").Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.State!.Focused);
        Assert.Equal("2026年11月", NameOf(Grids(calendar).Single()));
        Assert.True(Cell(calendar, "2026-11-01").IsFocused);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.Shift);
        Assert.Equal(new DateOnly(2025, 11, 1), calendar.State.Focused);
        Assert.True(Cell(calendar, "2025-11-01").IsFocused);
    });

    [Fact]
    [Upstream("Calendar", "en-US：一周从周日开始，英文的月份与按钮名称")]
    public Task English_starts_on_sunday_with_english_names() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20" }, Lang.EnUs);
        var first = (TextBlock)Named(calendar, CalendarViewParts.Weekday)[0];
        Assert.Equal("S", first.Text);
        Assert.Equal("Sunday", ToolTip.GetTip(first));
        Assert.Equal("October 2026", NameOf(Grids(calendar).Single()));
        Assert.Equal("Today, Tuesday, October 20, 2026", NameOf(Cell(calendar, "2026-10-20")));
        Assert.Equal("Previous month", NameOf(Named(calendar, CalendarViewParts.Previous).Single()));
    });

    [Fact]
    [Upstream("Calendar", "firstDayOfWeek 覆盖语言惯例")]
    public Task First_day_of_week_overrides_the_language() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20", FirstDayOfWeek = DayOfWeek.Sunday });
        Assert.Equal("日", ((TextBlock)Named(calendar, CalendarViewParts.Weekday)[0]).Text);
    });

    [Fact]
    [Upstream("Calendar", "最小最大日期与不可选日期")]
    public Task Limits_and_unavailable_dates_cannot_be_picked() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar
        {
            DefaultFocused = "2026-10-15",
            Min = "2026-10-10",
            Max = "2026-10-25",
            IsDateUnavailable = date => date == new DateOnly(2026, 10, 18),
        });
        Assert.False(Named(calendar, CalendarViewParts.Previous).Single().IsEnabled);
        Assert.False(Named(calendar, CalendarViewParts.Next).Single().IsEnabled);
        Assert.Contains(":off", Cell(calendar, "2026-10-05").Classes);
        Assert.False(Cell(calendar, "2026-10-05").Peer().IsEnabled());
        Assert.Contains(":unavailable", Cell(calendar, "2026-10-18").Classes);
        Click(Cell(calendar, "2026-10-18"));
        Click(Cell(calendar, "2026-10-05"));
        Assert.Null(calendar.Value);
    });

    [Fact]
    [Upstream("Calendar", "min / max 写成日期时间时按它的日期禁用")]
    public Task Date_time_limits_compare_by_day() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { DefaultFocused = "2026-10-15", Min = "2026-10-10T14:00", Max = "2026-10-25T09:30:00" });
        Assert.Contains(":off", Cell(calendar, "2026-10-09").Classes);
        Assert.DoesNotContain(":off", Cell(calendar, "2026-10-10").Classes);
        Assert.Contains(":off", Cell(calendar, "2026-10-26").Classes);
    });

    [Fact]
    [Upstream("Calendar", "多选：每天独立切换")]
    public Task Multiple_selection_toggles_each_day() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { SelectionMode = CalendarSelectionMode.Multiple, DefaultFocused = "2026-10-01" });
        Click(Cell(calendar, "2026-10-02"));
        Click(Cell(calendar, "2026-10-09"));
        Click(Cell(calendar, "2026-10-02"));
        Assert.Equal(["2026-10-09"], calendar.Values);
        Assert.Throws<ArgumentException>(() => calendar.SelectionMode = CalendarSelectionMode.Range);
    });

    [Fact]
    [Upstream("Calendar", "并排两个月：每月一张网格，补齐的日期留空，翻页按钮只在两端")]
    public Task Two_months_side_by_side() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20", VisibleMonths = 2 });
        Assert.Equal(["2026年10月", "2026年11月"], Grids(calendar).Select(NameOf));
        Assert.Single(CellsOf(calendar, "2026-10-31"));
        Assert.Single(Named(calendar, CalendarViewParts.Previous));
        Assert.Single(Named(calendar, CalendarViewParts.Next));
    });

    [Fact]
    [Upstream("Calendar", "周数与自定义格子")]
    public Task Week_numbers_and_custom_cells() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar
        {
            Value = "2026-10-20",
            ShowWeekNumbers = true,
            CellContent = (date, cell) => cell.Selected ? $"✓{date.Day:00}" : $"{date.Day:00}",
        });
        var numbers = Named(calendar, CalendarViewParts.WeekNumber);
        Assert.Equal("第 40 周", NameOf(numbers[1]));
        // 自定义内容跟在完整日期后面一起念
        Assert.Equal("今天，2026年10月20日星期二✓20", NameOf(Cell(calendar, "2026-10-20")));
    });

    [Fact]
    [Upstream("Calendar", "标题切到月份视图、再到年份视图；点年份、月份逐级回到日期")]
    public Task Heading_switches_to_month_and_year_views() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20" });
        Control Heading() => Named(calendar, CalendarViewParts.Heading).Single();
        Assert.Equal("选择月份：2026年10月", NameOf(Heading()));
        Click(Heading());
        Assert.Equal(DateUnit.Month, calendar.State!.View);
        Assert.Equal("3月", Cell(calendar, "2026-03-01").Content);
        Assert.Equal("2026年3月", NameOf(Cell(calendar, "2026-03-01")));
        Click(Heading());
        Assert.Equal("2020年 – 2029年", NameOf(Grids(calendar).Single()));
        // 年份视图是最粗的：标题只是文字
        Assert.IsNotType<ButtonBase>(Heading());
        Click(Cell(calendar, "2028-01-01"));
        Click(Cell(calendar, "2028-02-01"));
        Assert.Equal(DateUnit.Day, calendar.State.View);
        Assert.Equal(new DateOnly(2028, 2, 20), calendar.State.Focused);
        Assert.Equal("2026-10-20", calendar.Value);
    });

    [Fact]
    [Upstream("Calendar", "granularity=")]
    public Task Month_granularity_picks_a_month() => HeadlessUi.Run(() =>
    {
        var picks = new List<string>();
        var (calendar, _) = Show(new Calendar { Granularity = DateUnit.Month, DefaultFocused = "2026-10-01" });
        calendar.Picked += (_, e) => picks.Add(e.Value);
        Click(Cell(calendar, "2026-04-01"));
        Assert.Equal("2026-04-01", calendar.Value);
        Assert.Equal(["2026-04-01"], picks);
    });

    [Fact]
    [Upstream("Calendar", "点标题切到年份视图，点年、点月逐级回来")]
    public Task Pointer_drill_down_keeps_the_focused_month_and_day() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "1995-06-18", Max = "2026-12-31" });
        Control Heading() => Named(calendar, CalendarViewParts.Heading).Single();
        Click(Heading());
        Click(Heading());
        // 指针点击：先聚焦、再点
        void Press(string iso)
        {
            var cell = Cell(calendar, iso);
            cell.Focus(NavigationMethod.Pointer);
            Click(cell);
        }
        Press("1998-01-01");
        Assert.Equal(new DateOnly(1998, 6, 18), calendar.State!.Focused);
        Assert.True(Cell(calendar, "1998-06-01").IsTabStop);
        Press("1998-09-01");
        Assert.Equal(DateUnit.Day, calendar.State.View);
        Assert.Equal(new DateOnly(1998, 9, 18), calendar.State.Focused);
    });

    [Fact]
    [Upstream("Calendar", "日期网格（CalendarView）的键盘")]
    public Task Keyboard_moves_by_day_week_month_year_and_enter_selects() => HeadlessUi.Run(() =>
    {
        var (calendar, window) = Show(new Calendar { Value = "1995-06-18" });
        Cell(calendar, "1995-06-18").Focus();
        void Press(PhysicalKey key, string expected, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.KeyPressQwerty(key, modifiers);
            Assert.True(Cell(calendar, expected).IsFocused, expected);
        }
        Press(PhysicalKey.ArrowRight, "1995-06-19");
        Press(PhysicalKey.ArrowDown, "1995-06-26");
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Press(PhysicalKey.ArrowLeft, "1995-06-18");
        Press(PhysicalKey.PageDown, "1995-07-18");
        Press(PhysicalKey.PageUp, "1994-07-18", RawInputModifiers.Shift);
        Press(PhysicalKey.PageUp, "1994-06-18");
        // 这周的第一天、最后一天（中文区域一周从周一开始；1994-06-18 是周六）
        Press(PhysicalKey.Home, "1994-06-13");
        Press(PhysicalKey.End, "1994-06-19");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal("1994-06-19", calendar.Value);
    });

    [Fact]
    public Task Outside_values_turn_to_their_month() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new Calendar { Value = "2026-10-20" });
        calendar.Date = new DateOnly(2027, 3, 5);
        Assert.Equal("2027年3月", NameOf(Grids(calendar).Single()));
        Assert.True(Cell(calendar, "2027-03-05").IsTabStop);
    });
}
