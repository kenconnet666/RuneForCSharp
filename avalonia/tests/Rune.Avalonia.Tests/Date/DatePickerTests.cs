using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>DatePicker：快捷选项、"今天"（按粒度的文字、补 00:00、保留时间）、选季度与选周、打开关闭（按钮、Alt + ↓、Esc、只读）。今天固定为 2026-10-20。</summary>
public class DatePickerTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (DatePicker Picker, Window Window) Show(DatePicker picker)
    {
        picker.Today = () => Today;
        var field = new Field { Label = "日期", Content = picker };
        Provider.SetLang(field, Lang.ZhCn);
        return (picker, HeadlessUi.Show(field));
    }

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static IEnumerable<T> InPanel<T>(DatePicker picker) => picker.Panel.GetLogicalDescendants().OfType<T>();

    static Button PanelButton(DatePicker picker, string text) => InPanel<Button>(picker).Single(button => button.Content as string == text);

    [Fact]
    [Upstream("DatePicker", "快捷选项：点了写回并关闭")]
    public Task Presets_write_back_and_close() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker
        {
            Value = "2026-10-01",
            Max = "2026-12-31",
            Presets = [new DatePreset("国庆", "2026-10-01"), new DatePreset("元旦", () => "2027-01-01"), new DatePreset("月底", "2026-10-31")],
        });
        picker.IsOpen = true;
        HeadlessUi.Flush(window);
        Assert.Equal(3, InPanel<Button>(picker).Count(button => button.Name == PickerParts.Preset));
        Assert.Equal(ToggleState.On, ((IToggleProvider)PanelButton(picker, "国庆").Peer()).ToggleState);
        Assert.False(PanelButton(picker, "元旦").IsEnabled);
        Click(PanelButton(picker, "月底"));
        Assert.Equal("2026-10-31", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”写回今天（带时间的粒度补 00:00）")]
    public Task Today_writes_today_by_granularity() => HeadlessUi.Run(() =>
    {
        var (day, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, IsOpen = true });
        HeadlessUi.Flush(window);
        Click(PanelButton(day, "今天"));
        Assert.Equal("2026-10-20T00:00", day.Value);
        var (month, other) = Show(new DatePicker { Granularity = DateFieldGranularity.Month, IsOpen = true });
        HeadlessUi.Flush(other);
        Click(PanelButton(month, "本月"));
        Assert.Equal("2026-10", month.Value);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”和当前值同一种类型写回")]
    public Task Today_keeps_the_time_of_the_value() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, Value = "2020-01-02T09:30", IsOpen = true });
        HeadlessUi.Flush(window);
        Click(PanelButton(picker, "今天"));
        Assert.Equal("2026-10-20T09:30", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "showToday={false} 时没有“今天”")]
    public Task Today_can_be_hidden() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { ShowToday = false, IsOpen = true });
        HeadlessUi.Flush(window);
        Assert.DoesNotContain(InPanel<Button>(picker), button => button.Name == PickerParts.Today);
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"quarter\"")]
    public Task Quarter_granularity_uses_the_quarter_panel() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q2", Max = "2026-08-15", IsOpen = true });
        HeadlessUi.Flush(window);
        var quarter = InPanel<QuarterCalendar>(picker).Single();
        var cells = quarter.Cells.ToList();
        Assert.Contains(":selected", cells[1].Classes);
        // 第四季度整段晚于 Max：不可用；第三季度有一部分在范围内：可选
        Assert.Contains(":off", cells[3].Classes);
        Assert.Equal("本季度", PanelButton(picker, "本季度").Content);
        Click(cells[2]);
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"week\"")]
    public Task Week_granularity_picks_the_whole_iso_week() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Week, Value = "2026-W43", IsOpen = true });
        HeadlessUi.Flush(window);
        var calendar = InPanel<Calendar>(picker).Single();
        Assert.Equal(DayOfWeek.Monday, calendar.FirstDayOfWeek);
        Assert.True(calendar.ShowWeekNumbers);
        CalendarCellButton Cell(int day) => calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 10, day));
        Assert.Contains(":selected", Cell(19).Classes);
        Assert.Contains(":highlighted", Cell(21).Classes);
        Assert.DoesNotContain(":highlighted", Cell(26).Classes);
        Click(Cell(29));
        Assert.Equal("2026-W44", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "季度：←→、Page Up、Home / End 移动，Enter 选中")]
    public Task Quarter_keys_in_the_popover() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q4" });
        Click(picker.Trigger);
        HeadlessUi.Flush(window);
        var quarter = InPanel<QuarterCalendar>(picker).Single();
        Assert.True(quarter.Cells.ElementAt(3).IsFocused);
        foreach (var key in new[] { PhysicalKey.ArrowRight, PhysicalKey.PageUp, PhysicalKey.End, PhysicalKey.ArrowLeft, PhysicalKey.Home, PhysicalKey.ArrowRight, PhysicalKey.ArrowRight })
            window.KeyPressQwerty(key, RawInputModifiers.None);
        Assert.Equal("2026-Q3", quarter.State.Focused);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    public Task Opens_from_the_button_and_alt_down_and_closes_on_escape() => HeadlessUi.Run(() =>
    {
        var reasons = new List<(bool, OpenChangeReason)>();
        var (picker, window) = Show(new DatePicker { Value = "2026-10-08" });
        picker.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        Assert.Equal("选择日期", AutomationProperties.GetName(picker.Trigger));
        Click(picker.Trigger);
        HeadlessUi.Flush(window);
        Assert.True(picker.IsOpen);
        var calendar = InPanel<Calendar>(picker).Single();
        Assert.True(calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 10, 8)).IsFocused);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(picker.IsOpen);
        Assert.True(picker.Trigger.IsFocused);
        picker.Group!.Boxes[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        Assert.True(picker.IsOpen);
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape), (true, OpenChangeReason.Trigger)], reasons);
        Assert.Equal(AutomationControlType.Button, picker.Trigger.Peer().GetAutomationControlType());
    });

    [Fact]
    public Task Read_only_and_disabled_cannot_open() => HeadlessUi.Run(() =>
    {
        var (picker, _) = Show(new DatePicker { Value = "2026-10-01", IsReadOnly = true });
        Assert.False(picker.Trigger.IsEnabled);
        picker.IsReadOnly = false;
        Assert.True(picker.Trigger.IsEnabled);
        picker.IsEnabled = false;
        Assert.False(picker.Trigger.IsEffectivelyEnabled);
    });

    [Fact]
    public Task Picking_a_day_with_time_moves_focus_to_the_hour() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, Value = "2026-10-01T14:30" });
        Click(picker.Trigger);
        HeadlessUi.Flush(window);
        Click(InPanel<Calendar>(picker).Single().Cells.First(cell => cell.Date == new DateOnly(2026, 10, 9)));
        Assert.Equal("2026-10-09T14:30", picker.Value);
        Assert.False(picker.IsOpen);
        Assert.True(picker.Group!.Boxes.Single(box => box.Type == DateSegmentType.Hour).IsFocused);
    });
}
