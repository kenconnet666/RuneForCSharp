namespace Rune.WinForms.Tests;

/// <summary>TimePickerControl：列表的时刻（步长、范围、不可选、小时制）、点选与"此刻"写回、与外壳同宽。此刻固定为 14:47。</summary>
public class TimePickerTests
{
    static (TimePickerControl Picker, Form Window) Show(TimePickerControl picker, Lang? lang = null)
    {
        picker.Now = () => new TimeOnly(14, 47, 30);
        var field = new FieldControl { Label = "时间", Content = picker, Dock = DockStyle.Top };
        var provider = new ProviderControl { Lang = lang ?? Lang.ZhCn, Dock = DockStyle.Fill };
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

    static void Open(TimePickerControl picker)
    {
        picker.IsOpen = true;
        WinFormsUi.Pump();
    }

    static ListboxControl List(TimePickerControl picker) => All(picker.Surface).OfType<ListboxControl>().Single();

    static IReadOnlyList<ListboxOption> Options(TimePickerControl picker) => List(picker).ItemsSource!.Cast<ListboxOption>().ToList();

    static ButtonControl NowButton(TimePickerControl picker) => All(picker.Surface).OfType<ButtonControl>().Single(button => button.Text == "此刻" || button.Text == "Now");

    [Fact]
    [Upstream("TimeField", "TimeField、DateField：键入不可选的时刻时外壳出错；TimePicker 的列表里那几项不可用")]
    public Task Unavailable_times_are_invalid_and_disabled_in_the_list() => WinFormsUi.Run(() =>
    {
        static bool Lunch(TimeOnly time) => time.Hour == 12;
        // 周六只上午营业
        static bool SaturdayAfternoon(TimeOnly time, DateOnly date) => date == new DateOnly(2026, 10, 24) && time.Hour >= 12;
        var time = new TimeFieldControl { Value = "12:30", IsTimeUnavailable = Lunch };
        var saturday = new DateFieldControl { Value = "2026-10-24T12:15", Granularity = DateFieldGranularity.Minute, IsTimeUnavailable = SaturdayAfternoon };
        var friday = new DateFieldControl { Value = "2026-10-23T12:15", Granularity = DateFieldGranularity.Minute, IsTimeUnavailable = SaturdayAfternoon };
        var (picker, window) = Show(new TimePickerControl { Value = "09:00", Min = "11:00", Max = "13:00", Step = 30, IsTimeUnavailable = Lunch });
        using var _ = window;
        // 只有周五那个预约不出错（TimePicker 的 09:00 早于 Min）
        Assert.Equal([true, true, false, true], new SegmentedFieldControlBase[] { time, saturday, friday, picker }.Select(field => field.Invalid));
        Open(picker);
        Assert.Equal(["11:00:False", "11:30:False", "12:00:True", "12:30:True", "13:00:False"], Options(picker).Select(option => $"{option.Label}:{option.IsDisabled}"));
    });

    [Fact]
    public Task Picking_a_time_writes_back_and_closes() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new TimePickerControl { Value = "10:30" });
        using var _ = window;
        Open(picker);
        Assert.Equal(48, Options(picker).Count);
        Assert.Equal("10:30", List(picker).Value);
        List(picker).Pick("15:00");
        Assert.Equal("15:00", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    public Task Now_writes_the_current_minute_and_respects_the_limits() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new TimePickerControl { Granularity = TimeFieldGranularity.Second, Step = 15 });
        using var _ = window;
        Open(picker);
        NowButton(picker).PerformClick();
        Assert.Equal("14:47:00", picker.Value);
        Assert.False(picker.IsOpen);
        var (limited, other) = Show(new TimePickerControl { Max = "12:00" });
        using var __ = other;
        Open(limited);
        Assert.False(NowButton(limited).Enabled);
    });

    [Fact]
    public Task Labels_follow_the_hour_cycle() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new TimePickerControl { Min = "09:00", Max = "13:00", Step = 60, HourCycle = "h12" }, Lang.EnUs);
        using var _ = window;
        Open(picker);
        Assert.Equal(["9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM"], Options(picker).Select(option => option.Label));
        Assert.Equal(["09:00", "10:00", "11:00", "12:00", "13:00"], Options(picker).Select(option => option.Value));
    });

    [Fact]
    public Task The_popover_matches_the_shell_width() => WinFormsUi.Run(() =>
    {
        var (picker, window) = Show(new TimePickerControl());
        using var _ = window;
        Open(picker);
        // 弹层与外壳同宽；内容窗口是弹层去掉两边的 xs 内边距与描边
        var inset = (int)MathF.Round(0.25f * picker.EmPixels()) + FloatingSurface.Border(ElementContext.Of(picker));
        Assert.Equal(picker.Width - 2 * inset, picker.Surface.Width);
        Assert.Equal(picker.Surface.Width, List(picker).Width);
        Assert.True(List(picker).Height > 0);
    });
}
