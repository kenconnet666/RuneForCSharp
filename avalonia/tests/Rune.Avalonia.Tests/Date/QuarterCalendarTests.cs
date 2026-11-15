using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>QuarterCalendar：外面改值翻到那一年、读屏名称、键盘移动与换年、Enter 选中、区间与范围外的季度。今天固定为 2026-10-20（第四季度）。</summary>
public class QuarterCalendarTests
{
    static (QuarterCalendar Calendar, Window Window) Show(QuarterCalendar calendar)
    {
        calendar.State.Today = () => new DateOnly(2026, 10, 20);
        Provider.SetLang(calendar, Lang.ZhCn);
        return (calendar, HeadlessUi.Show(calendar));
    }

    static CalendarCellButton Cell(QuarterCalendar calendar, int quarter) => calendar.Cells.ElementAt(quarter - 1);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string? Heading(QuarterCalendar calendar) => calendar.Part<Text>(CalendarViewParts.Heading).Text;

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar：父级从外面改了值就翻到那一年")]
    public Task Outside_values_move_to_their_year() => HeadlessUi.Run(() =>
    {
        var (calendar, window) = Show(new QuarterCalendar { Value = "2026-Q4" });
        Assert.Equal("2026年", Heading(calendar));
        Assert.Equal("2026年第4季度", AutomationProperties.GetName(Cell(calendar, 4)));
        Assert.Equal("四季度", Cell(calendar, 4).Content);
        Assert.True(Cell(calendar, 4).IsTabStop);
        Assert.Contains(":selected", Cell(calendar, 4).Classes);
        Assert.Contains(":today", Cell(calendar, 4).Classes);
        calendar.Value = "2024-Q2";
        HeadlessUi.Flush(window);
        Assert.Equal("2024年", Heading(calendar));
        Assert.Contains(":selected", Cell(calendar, 2).Classes);
    });

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar 的键盘")]
    public Task Keys_move_across_years_and_enter_picks() => HeadlessUi.Run(() =>
    {
        var picks = 0;
        var (calendar, window) = Show(new QuarterCalendar { Value = "2026-Q4" });
        calendar.Picked += (_, _) => picks++;
        Cell(calendar, 4).Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal("2027年", Heading(calendar));
        Assert.True(Cell(calendar, 1).IsFocused);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("2026年", Heading(calendar));
        Assert.True(Cell(calendar, 1).IsFocused);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal("2026-Q1", calendar.Value);
        Assert.Equal(1, picks);
    });

    [Fact]
    public Task Range_and_limits() => HeadlessUi.Run(() =>
    {
        var (calendar, _) = Show(new QuarterCalendar { SelectionMode = CalendarSelectionMode.Range, Min = "2026-04-01" });
        Assert.Contains(":off", Cell(calendar, 1).Classes);
        Click(Cell(calendar, 1));
        Assert.Null(calendar.State.Anchor);
        Click(Cell(calendar, 3));
        Assert.Equal("请选择结束日期", AutomationProperties.GetHelpText(Cell(calendar, 2)));
        Click(calendar.Part<IconButton>(CalendarViewParts.Next));
        Assert.Equal("2027年", Heading(calendar));
        Click(Cell(calendar, 2));
        Assert.Equal(new IsoDateRange("2026-Q3", "2027-Q2"), calendar.RangeValue);
        Assert.Contains("in-range", ((Control)Cell(calendar, 1).Parent!).Classes);
        Assert.Contains(":selected", Cell(calendar, 2).Classes);
        Assert.Throws<ArgumentException>(() => calendar.SelectionMode = CalendarSelectionMode.Multiple);
    });
}
