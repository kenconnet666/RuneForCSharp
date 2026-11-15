using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>DateRangePicker：快捷区间（内置与自己的）、跟随外面的值、按月、日期时间、按季度、按周；两组之间的焦点、颠倒出错、读屏名称。今天固定为 2026-10-20。</summary>
public class DateRangePickerTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (DateRangePicker Picker, Window Window) Show(DateRangePicker picker)
    {
        picker.Today = () => Today;
        var field = new Field { Label = "区间", Content = picker };
        Provider.SetLang(field, Lang.ZhCn);
        var window = HeadlessUi.Show(field);
        HeadlessUi.Flush(window);
        return (picker, window);
    }

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static IEnumerable<T> InPanel<T>(DateRangePicker picker) => picker.Panel.GetLogicalDescendants().OfType<T>();

    static Button PanelButton(DateRangePicker picker, string text) => InPanel<Button>(picker).Single(button => button.Content as string == text);

    static CalendarCellButton Day(DateRangePicker picker, int year, int month, int day) =>
        InPanel<RangeCalendar>(picker).Single().Cells.First(cell => cell.Date == new DateOnly(year, month, day));

    static string Texts(DateRangePicker picker) => string.Join("|", picker.Groups.Select(group => group!.Text));

    static void Reopen(DateRangePicker picker, Window window)
    {
        picker.IsOpen = true;
        HeadlessUi.Flush(window);
    }

    [Fact]
    [Upstream("DateRangePicker", "自定义快捷区间与内置的名字混用")]
    public Task Builtin_and_custom_presets() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePicker
        {
            IsOpen = true,
            Presets =
            [
                DateRangePreset.Today,
                new RangePreset("第三季度", new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30))),
                new RangePreset("上个结算周期", () => new DateRange(new DateOnly(2026, 9, 26), new DateOnly(2026, 10, 25))),
            ],
        });
        Assert.NotNull(PanelButton(picker, "今天"));
        Click(PanelButton(picker, "第三季度"));
        Assert.Equal(new IsoDateRange("2026-07-01", "2026-09-30"), picker.Value);
        Assert.False(picker.IsOpen);
        Reopen(picker, window);
        Assert.Equal(ToggleState.On, ((IToggleProvider)PanelButton(picker, "第三季度").Peer()).ToggleState);
    });

    [Fact]
    [Upstream("DateRangePicker", "清空后父级再给值、再清空，输入框都跟着")]
    public Task Follows_outside_values_after_clearing() => HeadlessUi.Run(() =>
    {
        var range = new IsoDateRange("2026-10-01", "2026-10-07");
        var (picker, window) = Show(new DateRangePicker { Value = range, Clearable = true });
        var filled = Texts(picker);
        Assert.Contains("2026", filled);
        Click(picker.GetVisualDescendants().OfType<ClearButton>().Single());
        HeadlessUi.Flush(window);
        Assert.Null(picker.Value);
        var empty = Texts(picker);
        Assert.DoesNotContain("2026", empty);
        picker.Value = range;
        HeadlessUi.Flush(window);
        Assert.Equal(filled, Texts(picker));
        picker.Value = null;
        HeadlessUi.Flush(window);
        Assert.Equal(empty, Texts(picker));
        picker.Value = range;
        HeadlessUi.Flush(window);
        Assert.Equal(filled, Texts(picker));
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker 按月：值是 YYYY-MM；快捷区间截成月")]
    public Task Month_granularity_writes_months() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePicker
        {
            Granularity = DateFieldGranularity.Month,
            IsOpen = true,
            Presets = [new RangePreset("上半年", new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)))],
        });
        Assert.All(picker.Lanes, lane => Assert.DoesNotContain(DateSegmentType.Day, lane.Fields));
        var calendar = InPanel<RangeCalendar>(picker).Single();
        Click(calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 2, 1)));
        Click(calendar.Cells.First(cell => cell.Date == new DateOnly(2026, 4, 1)));
        Assert.Equal(new IsoDateRange("2026-02", "2026-04"), picker.Value);
        Reopen(picker, window);
        Click(PanelButton(picker, "上半年"));
        Assert.Equal(new IsoDateRange("2026-01", "2026-06"), picker.Value);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker 日期时间")]
    public Task Date_time_ranges_keep_their_times() => HeadlessUi.Run(() =>
    {
        var (picker, _) = Show(new DateRangePicker { Value = new IsoDateRange("2026-10-05T08:30", "2026-10-06T18:00"), Granularity = DateFieldGranularity.Minute, IsOpen = true });
        Click(Day(picker, 2026, 10, 10));
        Click(Day(picker, 2026, 10, 12));
        Assert.Equal(new IsoDateRange("2026-10-10T08:30", "2026-10-12T18:00"), picker.Value);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker granularity=\"quarter\"")]
    public Task Quarter_granularity_picks_across_years() => HeadlessUi.Run(() =>
    {
        var (picker, _) = Show(new DateRangePicker { Granularity = DateFieldGranularity.Quarter, IsOpen = true });
        var quarter = InPanel<QuarterCalendar>(picker).Single();
        Click(quarter.Cells.ElementAt(2));
        Click(quarter.GetLogicalDescendants().OfType<IconButton>().Single(button => button.Name == CalendarViewParts.Next));
        Click(quarter.Cells.ElementAt(1));
        Assert.Equal(new IsoDateRange("2026-Q3", "2027-Q2"), picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker granularity=\"week\"")]
    public Task Week_granularity_writes_weeks() => HeadlessUi.Run(() =>
    {
        var (picker, _) = Show(new DateRangePicker { Value = new IsoDateRange("2026-W40", "2026-W41"), Granularity = DateFieldGranularity.Week, IsOpen = true });
        Assert.Equal(DayOfWeek.Monday, InPanel<RangeCalendar>(picker).Single().FirstDayOfWeek);
        Click(Day(picker, 2026, 10, 21));
        Click(Day(picker, 2026, 10, 29));
        Assert.Equal(new IsoDateRange("2026-W43", "2026-W44"), picker.Value);
    });

    [Fact]
    public Task Typing_moves_from_start_to_end_and_reversed_ranges_are_invalid() => HeadlessUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePicker());
        Assert.Equal(["区间 开始日期", "区间 结束日期"], picker.Groups.Select(group => AutomationProperties.GetName(group!)));
        picker.Groups[0]!.Boxes[0].Focus();
        window.KeyTextInput("20261020");
        Assert.True(picker.Groups[1]!.Boxes[0].IsFocused);
        Assert.Null(picker.Value);
        window.KeyTextInput("20261001");
        Assert.Equal(new IsoDateRange("2026-10-20", "2026-10-01"), picker.Value);
        Assert.True(picker.Invalid);
        Assert.Contains(":invalid", picker.Classes);
        // 结束那组第一段删空后再删：回到开始那组的最后一段
        picker.Groups[1]!.Boxes[0].Focus();
        for (var i = 0; i < 5; i++) window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Assert.Null(picker.Value);
        Assert.True(picker.Groups[0]!.Boxes[^1].IsFocused);
    });
}
