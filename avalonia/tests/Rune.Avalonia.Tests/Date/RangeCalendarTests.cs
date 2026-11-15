using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>RangeCalendar：先点开始、悬停预览、再点结束；不能夹着不可选的日期；按月选写回整月；Esc 取消；快捷区间。今天固定为 2026-10-20（周二）。</summary>
public class RangeCalendarTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (RangeCalendar Calendar, Window Window) Show(RangeCalendar calendar)
    {
        calendar.State!.Today = () => Today;
        Provider.SetLang(calendar, Lang.ZhCn);
        return (calendar, HeadlessUi.Show(calendar));
    }

    static CalendarCellButton Cell(CalendarView calendar, string iso) => calendar.Cells.First(cell => cell.Date == DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture));

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Fact]
    [Upstream("RangeCalendar", "先点开始、悬停预览、再点结束")]
    public Task Start_hover_preview_then_end() => HeadlessUi.Run(() =>
    {
        var picks = new List<IsoDateRange>();
        var (calendar, window) = Show(new RangeCalendar { DefaultFocused = "2026-10-01" });
        calendar.Picked += (_, e) => picks.Add(e.Value);
        Click(Cell(calendar, "2026-10-15"));
        Assert.Equal("请选择结束日期", AutomationProperties.GetHelpText(Cell(calendar, "2026-10-15")));
        Assert.Contains(calendar.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "请选择结束日期");
        // 点了之后网格重建：排好版再按位置移动指针
        HeadlessUi.Flush(window);
        window.MouseMove(Center(Cell(calendar, "2026-10-12"), window));
        Assert.True(Cell(calendar, "2026-10-13").State.InRange);
        Assert.Contains("in-range", ((Control)Cell(calendar, "2026-10-13").Parent!).Classes);
        Click(Cell(calendar, "2026-10-12"));
        Assert.Equal(new IsoDateRange("2026-10-12", "2026-10-15"), calendar.Value);
        Assert.Equal(new DateRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15)), calendar.Range);
        Assert.Single(picks);
        Assert.True(Cell(calendar, "2026-10-14").State.Selected);
        Assert.Contains(":middle", Cell(calendar, "2026-10-14").Classes);
        Assert.Contains(":selected", Cell(calendar, "2026-10-12").Classes);
    });

    [Fact]
    [Upstream("RangeCalendar", "区间里不能夹着不可选的日期")]
    public Task Range_cannot_span_unavailable_dates() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new RangeCalendar { DefaultFocused = "2026-10-01", IsDateUnavailable = date => date == new DateOnly(2026, 10, 18) });
        Click(Cell(calendar, "2026-10-15"));
        Assert.Contains(":off", Cell(calendar, "2026-10-20").Classes);
        Click(Cell(calendar, "2026-10-20"));
        Assert.Null(calendar.Value);
        // 允许时可以跨过（请假跨过周末）
        calendar.AllowsUnavailableInRange = true;
        Click(Cell(calendar, "2026-10-20"));
        Assert.Equal(new IsoDateRange("2026-10-15", "2026-10-20"), calendar.Value);
    });

    [Fact]
    [Upstream("RangeCalendar", "RangeCalendar 按月选：写回整月")]
    public Task Month_granularity_writes_whole_months() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new RangeCalendar { Value = new IsoDateRange("2026-01-01", "2026-01-31"), Granularity = DateUnit.Month });
        Click(Cell(calendar, "2026-03-01"));
        Click(Cell(calendar, "2026-05-01"));
        Assert.Equal(new IsoDateRange("2026-03-01", "2026-05-31"), calendar.Value);
    });

    [Fact]
    public Task Escape_cancels_a_half_picked_range() => HeadlessUi.Run(() =>
    {
        var (calendar, window) = Show(new RangeCalendar { DefaultFocused = "2026-10-01" });
        Cell(calendar, "2026-10-15").Focus();
        Click(Cell(calendar, "2026-10-15"));
        Assert.NotNull(calendar.State!.Anchor);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Null(calendar.State.Anchor);
        Assert.Null(calendar.Value);
    });

    [Fact]
    public Task Outside_values_and_presets_turn_to_their_month() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new RangeCalendar { VisibleMonths = 2 });
        calendar.Range = CalendarState.PresetRange(DateRangePreset.LastMonth, Today, DayOfWeek.Monday);
        Assert.Equal(new IsoDateRange("2026-09-01", "2026-09-30"), calendar.Value);
        Assert.True(Cell(calendar, "2026-09-15").State.InRange);
        Assert.Equal("上月", CalendarText.PresetName(DateRangePreset.LastMonth, Lang.ZhCn));
        Assert.Equal("最近 7 天", CalendarText.PresetName(DateRangePreset.Last7Days, Lang.ZhCn));
    });
}
