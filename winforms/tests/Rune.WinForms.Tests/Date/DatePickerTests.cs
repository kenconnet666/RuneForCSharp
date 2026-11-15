namespace Rune.WinForms.Tests;

/// <summary>DatePickerControl：快捷选项、"今天"（按粒度的文字、补 00:00、保留时间）、选季度与选周、打开关闭（按钮、Alt + ↓、Esc、只读）。今天固定为 2026-10-20。</summary>
public class DatePickerTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (DatePickerControl Picker, Form Window) Show(DatePickerControl picker)
    {
        picker.Today = () => Today;
        var field = new FieldControl { Label = "日期", Content = picker, Dock = DockStyle.Top };
        var provider = new ProviderControl { Lang = Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(field);
        var window = WinFormsUi.Show(provider);
        WinFormsUi.Pump();
        return (picker, window);
    }

    static IEnumerable<Control> All(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in All(child)) yield return nested;
        }
    }

    static IEnumerable<T> InPanel<T>(DatePickerControl picker) => All(picker.Surface).OfType<T>();

    static ButtonControl PanelButton(DatePickerControl picker, string text) => InPanel<ButtonControl>(picker).Single(button => button.Text == text);

    static void Open(DatePickerControl picker)
    {
        picker.Trigger.PerformClick();
        WinFormsUi.Pump();
    }

    [Fact]
    [Upstream("DatePicker", "快捷选项：点了写回并关闭")]
    public Task Presets_write_back_and_close() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl
        {
            Value = "2026-10-01",
            Max = "2026-12-31",
            Presets = [new DatePreset("国庆", "2026-10-01"), new DatePreset("元旦", () => "2027-01-01"), new DatePreset("月底", "2026-10-31")],
        });
        using var _ = window;
        Open(picker);
        Assert.Equal(3, InPanel<PickerPresetButtonControl>(picker).Count());
        Assert.True(PanelButton(picker, "国庆").AccessibilityObject.State.HasFlag(AccessibleStates.Pressed));
        Assert.False(PanelButton(picker, "元旦").Enabled);
        PanelButton(picker, "月底").PerformClick();
        Assert.Equal("2026-10-31", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”写回今天（带时间的粒度补 00:00）")]
    public Task Today_writes_today_by_granularity() => WinFormsUi.Run(() =>
    {
        var (day, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Minute });
        using var _ = window;
        Open(day);
        PanelButton(day, "今天").PerformClick();
        Assert.Equal("2026-10-20T00:00", day.Value);
        var (month, other) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Month });
        using var __ = other;
        Open(month);
        PanelButton(month, "本月").PerformClick();
        Assert.Equal("2026-10", month.Value);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”和当前值同一种类型写回")]
    public Task Today_keeps_the_time_of_the_value() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Minute, Value = "2020-01-02T09:30" });
        using var _ = window;
        Open(picker);
        PanelButton(picker, "今天").PerformClick();
        Assert.Equal("2026-10-20T09:30", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "showToday={false} 时没有“今天”")]
    public Task Today_can_be_hidden() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { ShowToday = false });
        using var _ = window;
        Open(picker);
        Assert.DoesNotContain(InPanel<ButtonControl>(picker), button => button.Text == "今天");
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"quarter\"")]
    public Task Quarter_granularity_uses_the_quarter_panel() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q2", Max = "2026-08-15" });
        using var _ = window;
        Open(picker);
        var quarter = InPanel<QuarterCalendarControl>(picker).Single();
        Assert.True(quarter.Grid.Cells[1].Selected);
        // 第四季度整段晚于 Max：不可用；第三季度有一部分在范围内：可选
        Assert.True(quarter.Grid.Cells[3].Disabled);
        Assert.NotNull(PanelButton(picker, "本季度"));
        quarter.Activate("2026-Q3");
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"week\"")]
    public Task Week_granularity_picks_the_whole_iso_week() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Week, Value = "2026-W43" });
        using var _ = window;
        Open(picker);
        var calendar = InPanel<CalendarControl>(picker).Single();
        Assert.Equal(DayOfWeek.Monday, calendar.FirstDayOfWeek);
        Assert.True(calendar.ShowWeekNumbers);
        var grid = calendar.Grids.Single();
        var cells = grid.Cells.ToDictionary(cell => cell.Date, cell => cell.State);
        Assert.True(cells[new DateOnly(2026, 10, 19)].Selected);
        Assert.True(calendar.Highlighted!(new DateOnly(2026, 10, 21)));
        Assert.False(calendar.Highlighted(new DateOnly(2026, 10, 26)));
        grid.Activate(new DateOnly(2026, 10, 29), cells[new DateOnly(2026, 10, 29)]);
        Assert.Equal("2026-W44", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "季度：←→、Page Up、Home / End 移动，Enter 选中")]
    public Task Quarter_keys_in_the_popover() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q4" });
        using var _ = window;
        Open(picker);
        var quarter = InPanel<QuarterCalendarControl>(picker).Single();
        foreach (var key in new[] { Keys.Right, Keys.PageUp, Keys.End, Keys.Left, Keys.Home, Keys.Right, Keys.Right }) quarter.Grid.Press(key);
        Assert.Equal("2026-Q3", quarter.State.Focused);
        quarter.Grid.Press(Keys.Enter);
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    public Task Opens_from_the_button_and_alt_down() => WinFormsUi.Run(() =>
    {
        var reasons = new List<(bool, OpenChangeReason)>();
        var (picker, window) = Show(new DatePickerControl { Value = "2026-10-08" });
        using var _ = window;
        picker.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        Assert.Equal("选择日期", picker.Trigger.AccessibleName);
        Open(picker);
        Assert.True(picker.IsOpen);
        Assert.True(picker.Surface.Visible);
        picker.IsOpen = false;
        Assert.False(picker.Surface.Visible);
        picker.Group.Focus();
        var e = new KeyEventArgs(Keys.Down | Keys.Alt);
        typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(picker.Group, [e]);
        Assert.True(picker.IsOpen);
        Assert.Equal([(true, OpenChangeReason.Trigger), (true, OpenChangeReason.Trigger)], reasons);
        picker.IsOpen = false;
    });

    [Fact]
    public Task Read_only_and_disabled_cannot_open() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Value = "2026-10-01", IsReadOnly = true });
        using var _ = window;
        Assert.False(picker.Trigger.Enabled);
        picker.IsOpen = true;
        Assert.False(picker.IsOpen);
        picker.IsReadOnly = false;
        Assert.True(picker.Trigger.Enabled);
    });

    [Fact]
    public Task Picking_a_day_with_time_keeps_the_time() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DatePickerControl { Granularity = DateFieldGranularity.Minute, Value = "2026-10-01T14:30" });
        using var _ = window;
        Open(picker);
        var grid = InPanel<CalendarControl>(picker).Single().Grids.Single();
        var cell = grid.Cells.First(item => item.Date == new DateOnly(2026, 10, 9));
        grid.Activate(cell.Date, cell.State);
        Assert.Equal("2026-10-09T14:30", picker.Value);
        Assert.False(picker.IsOpen);
        Assert.Equal(DateSegmentType.Hour, picker.Segments.FocusedSegment);
    });
}
