namespace Rune.WinForms.Tests;

/// <summary>DateRangePickerControl：快捷区间（内置与自己的）、跟随外面的值、按月、日期时间、按季度、按周；两组之间的焦点、颠倒出错、读屏名称。今天固定为 2026-10-20。</summary>
public class DateRangePickerTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static (DateRangePickerControl Picker, Form Window) Show(DateRangePickerControl picker)
    {
        picker.Today = () => Today;
        var field = new FieldControl { Label = "区间", Content = picker, Dock = DockStyle.Top };
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

    static IEnumerable<T> InPanel<T>(DateRangePickerControl picker) => All(picker.Surface).OfType<T>();

    static ButtonControl PanelButton(DateRangePickerControl picker, string text) => InPanel<ButtonControl>(picker).Single(button => button.Text == text);

    static void Open(DateRangePickerControl picker)
    {
        picker.Trigger.PerformClick();
        WinFormsUi.Pump();
    }

    /// <summary>选好后马上再打开：按钮刚关上的 300 ms 里点它不再打开（防止点外面关掉后又打开），改开关属性。</summary>
    static void Reopen(DateRangePickerControl picker)
    {
        picker.IsOpen = true;
        WinFormsUi.Pump();
    }

    /// <summary>点日历里的一天（按日、按周时是那一天，按月时是那个月的 1 日）。</summary>
    static void Pick(DateRangePickerControl picker, DateOnly date)
    {
        foreach (var grid in InPanel<RangeCalendarControl>(picker).Single().Grids)
            foreach (var cell in grid.Cells)
                if (cell.Date == date)
                {
                    grid.Activate(cell.Date, cell.State);
                    return;
                }
        throw new InvalidOperationException($"{date} 不在显示的网格里");
    }

    static string Texts(DateRangePickerControl picker) => string.Join("|", picker.Groups.Select(group => group.SegmentText));

    [Fact]
    [Upstream("DateRangePicker", "自定义快捷区间与内置的名字混用")]
    public Task Builtin_and_custom_presets() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl
        {
            Presets =
            [
                DateRangePreset.Today,
                new RangePreset("第三季度", new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30))),
                new RangePreset("上个结算周期", () => new DateRange(new DateOnly(2026, 9, 26), new DateOnly(2026, 10, 25))),
            ],
        });
        using var _ = window;
        Open(picker);
        Assert.NotNull(PanelButton(picker, "今天"));
        PanelButton(picker, "第三季度").PerformClick();
        Assert.Equal(new IsoDateRange("2026-07-01", "2026-09-30"), picker.Value);
        Assert.False(picker.IsOpen);
        Reopen(picker);
        Assert.True(PanelButton(picker, "第三季度").AccessibilityObject.State.HasFlag(AccessibleStates.Pressed));
    });

    [Fact]
    [Upstream("DateRangePicker", "清空后父级再给值、再清空，输入框都跟着")]
    public Task Follows_outside_values_after_clearing() => WinFormsUi.Run(() =>
    {
        var range = new IsoDateRange("2026-10-01", "2026-10-07");
        var (picker, window) = Show(new DateRangePickerControl { Value = range, Clearable = true });
        using var _ = window;
        var filled = Texts(picker);
        Assert.Contains("2026", filled);
        picker.ClearButton.PerformClick();
        Assert.Null(picker.Value);
        var empty = Texts(picker);
        Assert.DoesNotContain("2026", empty);
        picker.Value = range;
        Assert.Equal(filled, Texts(picker));
        picker.Value = null;
        Assert.Equal(empty, Texts(picker));
        picker.Value = range;
        Assert.Equal(filled, Texts(picker));
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker 按月：值是 YYYY-MM；快捷区间截成月")]
    public Task Month_granularity_writes_months() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl
        {
            Granularity = DateFieldGranularity.Month,
            Presets = [new RangePreset("上半年", new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)))],
        });
        using var _ = window;
        Assert.All(picker.Lanes, lane => Assert.DoesNotContain(DateSegmentType.Day, lane.Fields));
        Open(picker);
        Pick(picker, new DateOnly(2026, 2, 1));
        Pick(picker, new DateOnly(2026, 4, 1));
        Assert.Equal(new IsoDateRange("2026-02", "2026-04"), picker.Value);
        Reopen(picker);
        PanelButton(picker, "上半年").PerformClick();
        Assert.Equal(new IsoDateRange("2026-01", "2026-06"), picker.Value);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker 日期时间")]
    public Task Date_time_ranges_keep_their_times() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl { Value = new IsoDateRange("2026-10-05T08:30", "2026-10-06T18:00"), Granularity = DateFieldGranularity.Minute });
        using var _ = window;
        Open(picker);
        Pick(picker, new DateOnly(2026, 10, 10));
        Pick(picker, new DateOnly(2026, 10, 12));
        Assert.Equal(new IsoDateRange("2026-10-10T08:30", "2026-10-12T18:00"), picker.Value);
        Assert.Equal(DateSegmentType.Hour, picker.Lanes[0].FocusedSegment);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker granularity=\"quarter\"")]
    public Task Quarter_granularity_picks_across_years() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl { Granularity = DateFieldGranularity.Quarter });
        using var _ = window;
        Open(picker);
        var quarter = InPanel<QuarterCalendarControl>(picker).Single();
        quarter.Activate("2026-Q3");
        quarter.Header.Next.PerformClick();
        Assert.Equal("2027年", quarter.Header.Heading.Text);
        quarter.Activate("2027-Q2");
        Assert.Equal(new IsoDateRange("2026-Q3", "2027-Q2"), picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    [Upstream("DateRangePicker", "DateRangePicker granularity=\"week\"")]
    public Task Week_granularity_writes_weeks() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl { Value = new IsoDateRange("2026-W40", "2026-W41"), Granularity = DateFieldGranularity.Week });
        using var _ = window;
        Open(picker);
        Assert.Equal(DayOfWeek.Monday, InPanel<RangeCalendarControl>(picker).Single().FirstDayOfWeek);
        Pick(picker, new DateOnly(2026, 10, 21));
        Pick(picker, new DateOnly(2026, 10, 29));
        Assert.Equal(new IsoDateRange("2026-W43", "2026-W44"), picker.Value);
    });

    [Fact]
    public Task Typing_moves_from_start_to_end_and_reversed_ranges_are_invalid() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new DateRangePickerControl());
        using var _ = window;
        Assert.Equal(["区间 开始日期", "区间 结束日期"], picker.Groups.Select(group => group.AccessibleName));
        picker.Lanes[0].SetFocused(DateSegmentType.Year);
        picker.Groups[0].TypeText("20261020");
        Assert.Equal(DateSegmentType.Year, picker.Lanes[1].FocusedSegment);
        Assert.Null(picker.Value);
        picker.Groups[1].TypeText("20261001");
        Assert.Equal(new IsoDateRange("2026-10-20", "2026-10-01"), picker.Value);
        Assert.True(picker.Invalid);
        // 结束那组第一段删空后再删：回到开始那组的最后一段
        picker.Lanes[0].SetFocused(DateSegmentType.Year);
        picker.Lanes[1].SetFocused(DateSegmentType.Year);
        for (var i = 0; i < 5; i++) picker.Groups[1].Press(Keys.Back);
        Assert.Null(picker.Value);
        Assert.Equal(DateSegmentType.Day, picker.Lanes[0].FocusedSegment);
    });
}
