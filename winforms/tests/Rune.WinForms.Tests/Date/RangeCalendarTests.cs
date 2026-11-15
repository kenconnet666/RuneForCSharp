using System.Globalization;

namespace Rune.WinForms.Tests;

/// <summary>RangeCalendarControl：先点开始、悬停预览、再点结束；不能夹着不可选的日期；按月选写回整月；Esc 取消；快捷区间。今天固定为 2026-10-20（周二）。</summary>
public class RangeCalendarTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (RangeCalendarControl Calendar, Form Window) Show(RangeCalendarControl calendar)
    {
        calendar.State!.Today = () => Today;
        var provider = new ProviderControl { Lang = Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(calendar);
        var window = WinFormsUi.Show(provider);
        WinFormsUi.Pump();
        return (calendar, window);
    }

    static DateOnly Day(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    static (CalendarGridControl Grid, CalendarCellState State, Rectangle Bounds) Cell(CalendarViewControl calendar, string iso)
    {
        var date = Day(iso);
        foreach (var grid in calendar.Grids)
            foreach (var cell in grid.Cells)
                if (cell.Date == date) return (grid, cell.State, cell.Bounds);
        throw new InvalidOperationException($"{iso} 不在显示的网格里");
    }

    static void Click(CalendarViewControl calendar, string iso)
    {
        var (grid, state, _) = Cell(calendar, iso);
        grid.Activate(Day(iso), state);
    }

    static void Hover(CalendarViewControl calendar, string iso) => Cell(calendar, iso).Grid.PointAt(Day(iso));

    [Fact]
    [Upstream("RangeCalendar", "先点开始、悬停预览、再点结束")]
    public Task Start_hover_preview_then_end() => WinFormsUi.Run(() =>
    {
        var picks = new List<IsoDateRange>();
        var (calendar, window) = Show(new RangeCalendarControl { DefaultFocused = "2026-10-01" });
        using var _ = window;
        calendar.Picked += (_, e) => picks.Add(e.Value);
        Click(calendar, "2026-10-15");
        var grid = calendar.Grids.Single();
        Assert.Equal("请选择结束日期", grid.AccessibleDescription);
        Hover(calendar, "2026-10-12");
        Assert.True(Cell(calendar, "2026-10-13").State.InRange);
        Click(calendar, "2026-10-12");
        Assert.Equal(new IsoDateRange("2026-10-12", "2026-10-15"), calendar.Value);
        Assert.Equal(new DateRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15)), calendar.Range);
        Assert.Single(picks);
        Assert.True(Cell(calendar, "2026-10-14").State.Selected);
        Assert.Null(grid.AccessibleDescription);
    });

    [Fact]
    [Upstream("RangeCalendar", "区间里不能夹着不可选的日期")]
    public Task Range_cannot_span_unavailable_dates() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new RangeCalendarControl { DefaultFocused = "2026-10-01", IsDateUnavailable = date => date == new DateOnly(2026, 10, 18) });
        using var _ = window;
        Click(calendar, "2026-10-15");
        Assert.True(Cell(calendar, "2026-10-20").State.Disabled);
        Click(calendar, "2026-10-20");
        Assert.Null(calendar.Value);
        // 允许时可以跨过（请假跨过周末）
        calendar.AllowsUnavailableInRange = true;
        Click(calendar, "2026-10-20");
        Assert.Equal(new IsoDateRange("2026-10-15", "2026-10-20"), calendar.Value);
    });

    [Fact]
    [Upstream("RangeCalendar", "RangeCalendar 按月选：写回整月")]
    public Task Month_granularity_writes_whole_months() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new RangeCalendarControl { Value = new IsoDateRange("2026-01-01", "2026-01-31"), Granularity = DateUnit.Month });
        using var _ = window;
        Click(calendar, "2026-03-01");
        Click(calendar, "2026-05-01");
        Assert.Equal(new IsoDateRange("2026-03-01", "2026-05-31"), calendar.Value);
    });

    [Fact]
    public Task Escape_cancels_a_half_picked_range() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new RangeCalendarControl { DefaultFocused = "2026-10-01" });
        using var _ = window;
        Click(calendar, "2026-10-15");
        Assert.NotNull(calendar.State!.Anchor);
        calendar.Grids.Single(grid => grid.TabStop).Press(Keys.Escape);
        Assert.Null(calendar.State.Anchor);
        Assert.Null(calendar.Value);
    });

    [Fact]
    public Task Outside_values_and_presets_turn_to_their_month() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new RangeCalendarControl { VisibleMonths = 2 });
        using var _ = window;
        calendar.Range = CalendarState.PresetRange(DateRangePreset.LastMonth, Today, DayOfWeek.Monday);
        Assert.Equal(new IsoDateRange("2026-09-01", "2026-09-30"), calendar.Value);
        Assert.True(Cell(calendar, "2026-09-15").State.InRange);
        Assert.Equal("上月", CalendarText.PresetName(DateRangePreset.LastMonth, Lang.ZhCn));
        foreach (var grid in calendar.Grids) WinFormsUi.Paint(grid);
    });
}
