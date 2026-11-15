using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Calendar：网格与读屏名称、点选与键盘、语言与一周的第一天、可选范围、多选、并排几个月、周数与自定义格子、月份与年份视图、按月选择。今天固定为 2026-10-20（周二）。</summary>
public class CalendarTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static Calendar Show(Calendar calendar, Lang? lang = null)
    {
        calendar.State!.Today = () => Today;
        Provider.SetLang(calendar, lang ?? Lang.ZhCn);
        calendar.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(calendar);
        WpfUi.Flush();
        return calendar;
    }

    static void Press(Key key)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    static CalendarCellButton Cell(Calendar calendar, string iso) => calendar.Cells.First(cell => cell.Date == DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture));

    static IEnumerable<CalendarCellButton> CellsOf(Calendar calendar, string iso) => calendar.Cells.Where(cell => cell.Date == DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture));

    static List<Panel> Grids(Calendar calendar) => [.. calendar.Grids];

    static string? NameOf(UIElement element) => element.Peer().GetName();

    // 同鼠标点击：不能选的格子也会收到点击（由日历自己拦下）
    static void Click(UIElement button)
    {
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfUi.Flush();
    }

    static List<FrameworkElement> Named(Calendar calendar, string name) => [.. calendar.Named(name)];

    [Fact]
    [Upstream("Calendar", "网格：星期表头取自 Intl")]
    public Task Grid_has_weekdays_full_date_names_and_a_single_tab_stop() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20" });
        var grid = Grids(calendar).Single();
        Assert.Equal("2026年10月", NameOf(grid));
        Assert.Equal(["一", "二", "三", "四", "五", "六", "日"], Named(calendar, "PART_Weekday").Cast<TextBlock>().Select(text => text.Text));
        Assert.Equal(7 + 6 * 7, grid.Children.Count);
        var selected = Cell(calendar, "2026-10-20");
        Assert.True(selected.State.Selected);
        Assert.True(selected.IsMarked);
        Assert.True(selected.IsTabStop);
        Assert.Equal("今天，2026年10月20日星期二", NameOf(selected));
        Assert.False(Cell(calendar, "2026-10-21").IsTabStop);
        // 补齐的上一个月的日期变淡，但能点
        Assert.True(Cell(calendar, "2026-09-28").IsOutside);
        Click(Cell(calendar, "2026-10-08"));
        Assert.Equal("2026-10-08", calendar.Value);
        Assert.Equal(new DateOnly(2026, 10, 8), calendar.Date);
    });

    [Fact]
    [Upstream("Calendar", "键盘：方向键移动焦点并聚焦新格子，跨月时翻页")]
    public Task Arrow_keys_move_focus_and_turn_pages() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-31" });
        Cell(calendar, "2026-10-31").Focus();
        Press(Key.Right);
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.State!.Focused);
        Assert.Equal("2026年11月", NameOf(Grids(calendar).Single()));
        Assert.True(Cell(calendar, "2026-11-01").IsKeyboardFocused);
        // 测试里造不出按住的 Shift（Keyboard.Modifiers 读真实键盘）：加 Shift 一年由 CalendarState 的测试覆盖
        Press(Key.PageUp);
        Assert.Equal(new DateOnly(2026, 10, 1), calendar.State.Focused);
        Assert.True(Cell(calendar, "2026-10-01").IsKeyboardFocused);
    });

    [Fact]
    [Upstream("Calendar", "en-US：一周从周日开始，英文的月份与按钮名称")]
    public Task English_starts_on_sunday_with_english_names() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20" }, Lang.EnUs);
        var first = (TextBlock)Named(calendar, "PART_Weekday")[0];
        Assert.Equal("S", first.Text);
        Assert.Equal("Sunday", first.ToolTip);
        Assert.Equal("October 2026", NameOf(Grids(calendar).Single()));
        Assert.Equal("Today, Tuesday, October 20, 2026", NameOf(Cell(calendar, "2026-10-20")));
        Assert.Equal("Previous month", NameOf(Named(calendar, "PART_Previous").Single()));
    });

    [Fact]
    [Upstream("Calendar", "firstDayOfWeek 覆盖语言惯例")]
    public Task First_day_of_week_overrides_the_language() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20", FirstDayOfWeek = DayOfWeek.Sunday });
        Assert.Equal("日", ((TextBlock)Named(calendar, "PART_Weekday")[0]).Text);
    });

    [Fact]
    [Upstream("Calendar", "最小最大日期与不可选日期")]
    public Task Limits_and_unavailable_dates_cannot_be_picked() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar
        {
            DefaultFocused = "2026-10-15",
            Min = "2026-10-10",
            Max = "2026-10-25",
            IsDateUnavailable = date => date == new DateOnly(2026, 10, 18),
        });
        Assert.False(Named(calendar, "PART_Previous").Single().IsEnabled);
        Assert.False(Named(calendar, "PART_Next").Single().IsEnabled);
        Assert.True(Cell(calendar, "2026-10-05").IsOff);
        Assert.False(Cell(calendar, "2026-10-05").Peer().IsEnabled());
        Assert.True(Cell(calendar, "2026-10-18").IsUnavailable);
        Click(Cell(calendar, "2026-10-18"));
        Click(Cell(calendar, "2026-10-05"));
        Assert.Null(calendar.Value);
    });

    [Fact]
    [Upstream("Calendar", "min / max 写成日期时间时按它的日期禁用")]
    public Task Date_time_limits_compare_by_day() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { DefaultFocused = "2026-10-15", Min = "2026-10-10T14:00", Max = "2026-10-25T09:30:00" });
        Assert.True(Cell(calendar, "2026-10-09").IsOff);
        Assert.False(Cell(calendar, "2026-10-10").IsOff);
        Assert.True(Cell(calendar, "2026-10-26").IsOff);
    });

    [Fact]
    [Upstream("Calendar", "多选：每天独立切换")]
    public Task Multiple_selection_toggles_each_day() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { SelectionMode = CalendarSelectionMode.Multiple, DefaultFocused = "2026-10-01" });
        Click(Cell(calendar, "2026-10-02"));
        Click(Cell(calendar, "2026-10-09"));
        Click(Cell(calendar, "2026-10-02"));
        Assert.Equal(["2026-10-09"], calendar.Values);
        Assert.Throws<ArgumentException>(() => calendar.SelectionMode = CalendarSelectionMode.Range);
    });

    [Fact]
    [Upstream("Calendar", "并排两个月：每月一张网格，补齐的日期留空，翻页按钮只在两端")]
    public Task Two_months_side_by_side() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20", VisibleMonths = 2 });
        Assert.Equal(["2026年10月", "2026年11月"], Grids(calendar).Select(NameOf));
        Assert.Single(CellsOf(calendar, "2026-10-31"));
        Assert.Single(Named(calendar, "PART_Previous"));
        Assert.Single(Named(calendar, "PART_Next"));
    });

    [Fact]
    [Upstream("Calendar", "周数与自定义格子")]
    public Task Week_numbers_and_custom_cells() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar
        {
            Value = "2026-10-20",
            ShowWeekNumbers = true,
            CellContent = (date, cell) => cell.Selected ? $"✓{date.Day:00}" : $"{date.Day:00}",
        });
        var numbers = Named(calendar, "PART_WeekNumber");
        Assert.Equal("第 40 周", NameOf(numbers[1]));
        // 自定义内容跟在完整日期后面一起念
        Assert.Equal("今天，2026年10月20日星期二✓20", NameOf(Cell(calendar, "2026-10-20")));
    });

    [Fact]
    [Upstream("Calendar", "标题切到月份视图、再到年份视图；点年份、月份逐级回到日期")]
    public Task Heading_switches_to_month_and_year_views() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20" });
        FrameworkElement Heading() => Named(calendar, "PART_Heading").Single();
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
    public Task Month_granularity_picks_a_month() => WpfUi.Run(() =>
    {
        var picks = new List<string>();
        var calendar = Show(new Calendar { Granularity = DateUnit.Month, DefaultFocused = "2026-10-01" });
        calendar.Picked += (_, e) => picks.Add(e.Value);
        Click(Cell(calendar, "2026-04-01"));
        Assert.Equal("2026-04-01", calendar.Value);
        Assert.Equal(["2026-04-01"], picks);
    });

    [Fact]
    [Upstream("Calendar", "点标题切到年份视图，点年、点月逐级回来")]
    public Task Pointer_drill_down_keeps_the_focused_month_and_day() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "1995-06-18", Max = "2026-12-31" });
        FrameworkElement Heading() => Named(calendar, "PART_Heading").Single();
        Click(Heading());
        Click(Heading());
        // 指针点击：先聚焦、再点
        void Press(string iso)
        {
            var cell = Cell(calendar, iso);
            cell.Focus();
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
    public Task Keyboard_moves_by_day_week_month_year_and_enter_selects() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "1995-06-18" });
        Cell(calendar, "1995-06-18").Focus();
        void Moves(Key key, string expected)
        {
            Press(key);
            Assert.True(Cell(calendar, expected).IsKeyboardFocused, expected);
        }
        Moves(Key.Right, "1995-06-19");
        Moves(Key.Down, "1995-06-26");
        Press(Key.Up);
        Moves(Key.Left, "1995-06-18");
        Moves(Key.PageDown, "1995-07-18");
        // 加 Shift 一年由 CalendarState 的测试覆盖（测试里造不出按住的 Shift）
        Moves(Key.PageUp, "1995-06-18");
        Moves(Key.PageUp, "1995-05-18");
        // 这周的第一天、最后一天（中文区域一周从周一开始；1995-05-18 是周四）
        Moves(Key.Home, "1995-05-15");
        Moves(Key.End, "1995-05-21");
        Press(Key.Enter);
        Assert.Equal("1995-05-21", calendar.Value);
    });

    [Fact]
    public Task Outside_values_turn_to_their_month() => WpfUi.Run(() =>
    {
        var calendar = Show(new Calendar { Value = "2026-10-20" });
        calendar.Date = new DateOnly(2027, 3, 5);
        WpfUi.Flush();
        Assert.Equal("2027年3月", NameOf(Grids(calendar).Single()));
        Assert.True(Cell(calendar, "2027-03-05").IsTabStop);
    });
}
