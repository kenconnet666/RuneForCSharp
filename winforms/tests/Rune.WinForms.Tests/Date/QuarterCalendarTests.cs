namespace Rune.WinForms.Tests;

/// <summary>QuarterCalendarControl：外面改值翻到那一年、读屏名称、键盘移动与换年、Enter 选中、区间与范围外的季度。今天固定为 2026-10-20（第四季度）。</summary>
public class QuarterCalendarTests
{
    static (QuarterCalendarControl Calendar, Form Window) Show(QuarterCalendarControl calendar)
    {
        calendar.State.Today = () => new DateOnly(2026, 10, 20);
        var provider = new ProviderControl { Lang = Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(calendar);
        var window = WinFormsUi.Show(provider);
        WinFormsUi.Pump();
        return (calendar, window);
    }

    static AccessibleObject Cell(QuarterCalendarControl calendar, int quarter) => calendar.Grid.AccessibilityObject.GetChild(quarter - 1)!;

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar：父级从外面改了值就翻到那一年")]
    public Task Outside_values_move_to_their_year() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new QuarterCalendarControl { Value = "2026-Q4" });
        using var _ = window;
        Assert.Equal("2026年", calendar.Header.Heading.Text);
        Assert.Equal(("2026年", AccessibleRole.Table), (calendar.Grid.AccessibilityObject.Name, calendar.Grid.AccessibilityObject.Role));
        Assert.Equal("2026年第4季度", Cell(calendar, 4).Name);
        Assert.True(Cell(calendar, 4).State.HasFlag(AccessibleStates.Selected));
        Assert.True(calendar.Grid.Cells[3] is { Today: true, Focused: true });
        calendar.Value = "2024-Q2";
        Assert.Equal("2024年", calendar.Header.Heading.Text);
        Assert.True(calendar.Grid.Cells[1].Selected);
    });

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar 的键盘")]
    public Task Keys_move_across_years_and_enter_picks() => WinFormsUi.Run(() =>
    {
        var picks = 0;
        var (calendar, window) = Show(new QuarterCalendarControl { Value = "2026-Q4" });
        using var _ = window;
        calendar.Picked += (_, _) => picks++;
        calendar.Grid.Press(Keys.Right);
        Assert.Equal("2027年", calendar.Header.Heading.Text);
        Assert.Equal("2027-Q1", calendar.State.Focused);
        calendar.Grid.Press(Keys.PageUp);
        Assert.Equal("2026年", calendar.Header.Heading.Text);
        calendar.Grid.Press(Keys.Enter);
        Assert.Equal("2026-Q1", calendar.Value);
        Assert.Equal(1, picks);
    });

    [Fact]
    public Task Range_and_limits() => WinFormsUi.Run(() =>
    {
        var (calendar, window) = Show(new QuarterCalendarControl { SelectionMode = CalendarSelectionMode.Range, Min = "2026-04-01" });
        using var _ = window;
        Assert.True(Cell(calendar, 1).State.HasFlag(AccessibleStates.Unavailable));
        Cell(calendar, 1).DoDefaultAction();
        Assert.Null(calendar.State.Anchor);
        Cell(calendar, 3).DoDefaultAction();
        Assert.Equal("请选择结束日期", calendar.Grid.AccessibleDescription);
        calendar.Header.Next.PerformClick();
        Assert.Equal("2027年", calendar.Header.Heading.Text);
        Cell(calendar, 2).DoDefaultAction();
        Assert.Equal(new IsoDateRange("2026-Q3", "2027-Q2"), calendar.RangeValue);
        Assert.True(calendar.Grid.Cells[0].InRange && calendar.Grid.Cells[1].Selected);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SelectionMode = CalendarSelectionMode.Multiple);
        WinFormsUi.Paint(calendar.Grid);
    });
}
