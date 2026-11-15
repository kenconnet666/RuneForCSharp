using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>DatePicker：快捷选项、"今天"（按粒度的文字、补 00:00、保留时间）、选季度与选周、打开关闭（按钮、Esc、只读）。今天固定为 2026-10-20。</summary>
public class DatePickerTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static DatePicker Show(DatePicker picker)
    {
        picker.Today = () => Today;
        var field = new Field { Label = "日期", Content = picker };
        Provider.SetLang(field, Lang.ZhCn);
        field.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(field);
        WpfUi.Flush();
        return picker;
    }

    static void Click(UIElement button)
    {
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfUi.Flush();
    }

    static void Press(Key key)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    static IEnumerable<T> InPanel<T>(DatePicker picker) => WpfUi.Descendants(picker.Panel).OfType<T>();

    static Button PanelButton(DatePicker picker, string text) => InPanel<Button>(picker).Single(button => button.Content as string == text);

    [Fact]
    [Upstream("DatePicker", "快捷选项：点了写回并关闭")]
    public Task Presets_write_back_and_close() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker
        {
            Value = "2026-10-01",
            Max = "2026-12-31",
            Presets = [new DatePreset("国庆", "2026-10-01"), new DatePreset("元旦", () => "2027-01-01"), new DatePreset("月底", "2026-10-31")],
        });
        picker.IsOpen = true;
        WpfUi.Flush();
        Assert.Equal(3, InPanel<Button>(picker).Count(button => button.Name == "PART_Preset"));
        Assert.Equal(ToggleState.On, ((IToggleProvider)PanelButton(picker, "国庆").Peer().GetPattern(PatternInterface.Toggle)).ToggleState);
        Assert.False(PanelButton(picker, "元旦").IsEnabled);
        Click(PanelButton(picker, "月底"));
        Assert.Equal("2026-10-31", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”写回今天（带时间的粒度补 00:00）")]
    public Task Today_writes_today_by_granularity() => WpfUi.Run(() =>
    {
        var day = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, IsOpen = true });
        Click(PanelButton(day, "今天"));
        Assert.Equal("2026-10-20T00:00", day.Value);
        var month = Show(new DatePicker { Granularity = DateFieldGranularity.Month, IsOpen = true });
        Click(PanelButton(month, "本月"));
        Assert.Equal("2026-10", month.Value);
    });

    [Fact]
    [Upstream("DatePicker", "“今天”和当前值同一种类型写回")]
    public Task Today_keeps_the_time_of_the_value() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, Value = "2020-01-02T09:30", IsOpen = true });
        Click(PanelButton(picker, "今天"));
        Assert.Equal("2026-10-20T09:30", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "showToday={false} 时没有“今天”")]
    public Task Today_can_be_hidden() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { ShowToday = false, IsOpen = true });
        Assert.DoesNotContain(InPanel<Button>(picker), button => button.Name == "PART_Today");
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"quarter\"")]
    public Task Quarter_granularity_uses_the_quarter_panel() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q2", Max = "2026-08-15", IsOpen = true });
        var cells = InPanel<QuarterCalendar>(picker).Single().Cells.ToList();
        Assert.True(cells[1].IsMarked);
        // 第四季度整段晚于 Max：不可用；第三季度有一部分在范围内：可选
        Assert.True(cells[3].IsOff);
        Assert.NotNull(PanelButton(picker, "本季度"));
        Click(cells[2]);
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DatePicker", "DatePicker granularity=\"week\"")]
    public Task Week_granularity_picks_the_whole_iso_week() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Granularity = DateFieldGranularity.Week, Value = "2026-W43", IsOpen = true });
        var calendar = InPanel<Calendar>(picker).Single();
        Assert.Equal(DayOfWeek.Monday, calendar.FirstDayOfWeek);
        Assert.True(calendar.ShowWeekNumbers);
        CalendarCellButton Cell(int day) => calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 10, day));
        Assert.True(Cell(19).IsMarked);
        Assert.True(Cell(21).IsHighlighted);
        Assert.False(Cell(26).IsHighlighted);
        Click(Cell(29));
        Assert.Equal("2026-W44", picker.Value);
    });

    [Fact]
    [Upstream("DatePicker", "季度：←→、Page Up、Home / End 移动，Enter 选中")]
    public Task Quarter_keys_in_the_popover() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q4" });
        Click(picker.Trigger);
        var quarter = InPanel<QuarterCalendar>(picker).Single();
        Assert.True(quarter.Cells.ElementAt(3).IsKeyboardFocused);
        foreach (var key in new[] { Key.Right, Key.PageUp, Key.End, Key.Left, Key.Home, Key.Right, Key.Right }) Press(key);
        Assert.Equal("2026-Q3", quarter.State.Focused);
        Press(Key.Enter);
        Assert.Equal("2026-Q3", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    public Task Opens_from_the_button_and_closes_on_escape() => WpfUi.Run(() =>
    {
        var reasons = new List<(bool, OpenChangeReason)>();
        var picker = Show(new DatePicker { Value = "2026-10-08" });
        picker.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        Assert.Equal("选择日期", AutomationProperties.GetName(picker.Trigger));
        Click(picker.Trigger);
        Assert.True(picker.IsOpen);
        var calendar = InPanel<Calendar>(picker).Single();
        Assert.True(calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 10, 8)).IsKeyboardFocused);
        Press(Key.Escape);
        Assert.False(picker.IsOpen);
        Assert.True(picker.Trigger.IsKeyboardFocused);
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape)], reasons);
    });

    [Fact]
    public Task Read_only_and_disabled_cannot_open() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Value = "2026-10-01", IsReadOnly = true });
        Assert.False(picker.Trigger.IsEnabled);
        picker.IsReadOnly = false;
        Assert.True(picker.Trigger.IsEnabled);
        picker.IsEnabled = false;
        Assert.False(picker.Trigger.IsEnabled);
    });

    [Fact]
    public Task Picking_a_day_with_time_moves_focus_to_the_hour() => WpfUi.Run(() =>
    {
        var picker = Show(new DatePicker { Granularity = DateFieldGranularity.Minute, Value = "2026-10-01T14:30" });
        Click(picker.Trigger);
        Click(InPanel<Calendar>(picker).Single().Cells.First(cell => cell.Date == new DateOnly(2026, 10, 9)));
        Assert.Equal("2026-10-09T14:30", picker.Value);
        Assert.False(picker.IsOpen);
        Assert.True(picker.Group!.Boxes.Single(box => box.Type == DateSegmentType.Hour).IsKeyboardFocused);
    });
}
