namespace Rune.WinForms.Tests;

/// <summary>QuarterState（季度面板的状态，孪生文件）：一开始的年份与焦点、键盘移动与换年、单选与区间、悬停预览、Esc、范围外的季度、外面改值。今天固定为 2026-10-20（第四季度）。</summary>
public class QuarterStateTests
{
    static QuarterState New(string? value = null, IsoDateRange? range = null, bool isRange = false, Func<string, bool>? disabled = null)
    {
        var state = new QuarterState
        {
            IsRange = () => isRange,
            Today = () => new DateOnly(2026, 10, 20),
            IsDisabled = disabled ?? (_ => false),
        };
        state.Selected = () => value;
        state.SelectedRange = () => range;
        return state;
    }

    [Fact]
    public void Starts_on_the_selected_quarter_or_this_quarter()
    {
        Assert.Equal(("2026-Q4", 2026), (New().Focused, New().Year));
        Assert.Equal(("2024-Q2", 2024), (New("2024-Q2").Focused, New("2024-Q2").Year));
        Assert.True(New().Cell(4).Today);
        Assert.Equal("2026-Q4", QuarterState.Of(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Keys_move_across_years_and_pages()
    {
        var state = New("2026-Q4");
        Assert.True(state.HandleKey(CalendarKey.Right));
        Assert.Equal(("2027-Q1", 2027), (state.Focused, state.Year));
        state.HandleKey(CalendarKey.PageUp);
        Assert.Equal("2026-Q1", state.Focused);
        state.HandleKey(CalendarKey.End);
        Assert.Equal("2026-Q4", state.Focused);
        state.HandleKey(CalendarKey.Right, rtl: true);
        Assert.Equal("2026-Q3", state.Focused);
        state.HandleKey(CalendarKey.Home);
        Assert.Equal("2026-Q1", state.Focused);
        Assert.False(state.HandleKey(CalendarKey.Up));
        Assert.False(state.HandleKey(CalendarKey.Escape));
    }

    [Fact]
    public void Turning_the_year_keeps_focus_and_puts_q1_in_tab_order()
    {
        var state = New("2026-Q3");
        state.TurnYear(1);
        Assert.Equal(("2026-Q3", 2027), (state.Focused, state.Year));
        Assert.True(state.Cell(1).Focused);
        Assert.False(state.Cell(3).Focused);
    }

    [Fact]
    public void Single_pick_returns_the_quarter_and_skips_disabled_ones()
    {
        var state = New(disabled: key => key == "2026-Q1");
        Assert.Equal(new IsoDateRange("2026-Q2", "2026-Q2"), state.Activate("2026-Q2"));
        Assert.Null(state.Activate("2026-Q1"));
        Assert.True(state.Cell(1).Disabled);
        state.ReadOnly = () => true;
        Assert.Null(state.Activate("2026-Q3"));
    }

    [Fact]
    public void Range_pick_previews_on_hover_and_orders_the_ends()
    {
        var state = New(isRange: true);
        Assert.Null(state.Activate("2026-Q3"));
        Assert.Equal("2026-Q3", state.Anchor);
        state.Hover("2026-Q1");
        Assert.True(state.Cell(2).InRange);
        Assert.True(state.Cell(1).RangeStart && state.Cell(3).RangeEnd);
        Assert.Equal(new IsoDateRange("2026-Q1", "2026-Q3"), state.Activate("2026-Q1"));
        Assert.Null(state.Anchor);
    }

    [Fact]
    public void Escape_cancels_a_half_picked_range()
    {
        var state = New(isRange: true);
        state.Activate("2026-Q2");
        Assert.True(state.HandleKey(CalendarKey.Escape));
        Assert.Null(state.Anchor);
        Assert.False(state.Cell(2).InRange);
    }

    [Fact]
    public void Outside_values_move_to_their_year()
    {
        var value = "2026-Q4";
        var state = new QuarterState { Selected = () => value, Today = () => new DateOnly(2026, 10, 20) };
        Assert.Equal(2026, state.Year);
        value = "2024-Q2";
        state.SelectionChanged();
        Assert.Equal(("2024-Q2", 2024), (state.Focused, state.Year));
        Assert.True(state.Cell(2).Selected);
    }
}
