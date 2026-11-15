using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>TimePicker：列表的时刻（步长、范围、不可选、小时制）、点选与"此刻"写回、与外壳同宽。此刻固定为 14:47。</summary>
public class TimePickerTests
{
    static TimePicker Show(TimePicker picker, Lang? lang = null)
    {
        picker.Now = () => new TimeOnly(14, 47, 30);
        var field = new Field { Label = "时间", Content = picker, Width = 240 };
        Provider.SetLang(field, lang ?? Lang.ZhCn);
        field.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(field);
        WpfUi.Flush();
        return picker;
    }

    static Listbox List(TimePicker picker) => WpfUi.Descendants(picker.Panel).OfType<Listbox>().Single();

    static IReadOnlyList<ListboxOption> Options(TimePicker picker) => List(picker).ItemsSource!.Cast<ListboxOption>().ToList();

    static void Pick(TimePicker picker, string value) => List(picker).RaiseEvent(new ListboxPickedEventArgs(Listbox.PickedEvent, value));

    static Button NowButton(TimePicker picker) => WpfUi.Descendants(picker.Panel).OfType<Button>().Single(button => button.Name == "PART_Now");

    [Fact]
    [Upstream("TimeField", "TimeField、DateField：键入不可选的时刻时外壳出错；TimePicker 的列表里那几项不可用")]
    public Task Unavailable_times_are_invalid_and_disabled_in_the_list() => WpfUi.Run(() =>
    {
        static bool Lunch(TimeOnly time) => time.Hour == 12;
        // 周六只上午营业
        static bool SaturdayAfternoon(TimeOnly time, DateOnly date) => date == new DateOnly(2026, 10, 24) && time.Hour >= 12;
        var time = new TimeField { Value = "12:30", IsTimeUnavailable = Lunch };
        var saturday = new DateField { Value = "2026-10-24T12:15", Granularity = DateFieldGranularity.Minute, IsTimeUnavailable = SaturdayAfternoon };
        var friday = new DateField { Value = "2026-10-23T12:15", Granularity = DateFieldGranularity.Minute, IsTimeUnavailable = SaturdayAfternoon };
        var picker = new TimePicker { Value = "09:00", Min = "11:00", Max = "13:00", Step = 30, IsTimeUnavailable = Lunch, IsOpen = true };
        Provider.SetLang(picker, Lang.ZhCn);
        var panel = new StackPanel { Children = { time, saturday, friday, picker } };
        panel.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(panel);
        WpfUi.Flush();
        // 只有周五那个预约不出错（TimePicker 的 09:00 早于 Min）
        Assert.Equal([true, true, false, true], new SegmentedFieldBase[] { time, saturday, friday, picker }.Select(field => field.Invalid));
        Assert.Equal(["11:00:False", "11:30:False", "12:00:True", "12:30:True", "13:00:False"], Options(picker).Select(option => $"{option.Label}:{option.IsDisabled}"));
    });

    [Fact]
    public Task Picking_a_time_writes_back_and_closes() => WpfUi.Run(() =>
    {
        var picker = Show(new TimePicker { Value = "10:30" });
        picker.IsOpen = true;
        WpfUi.Flush();
        Assert.Equal(48, Options(picker).Count);
        Assert.Equal("10:30", List(picker).Value);
        Pick(picker, "15:00");
        Assert.Equal("15:00", picker.Value);
        Assert.False(picker.IsOpen);
    });

    [Fact]
    public Task Now_writes_the_current_minute_and_respects_the_limits() => WpfUi.Run(() =>
    {
        var picker = Show(new TimePicker { Granularity = TimeFieldGranularity.Second, Step = 15, IsOpen = true });
        NowButton(picker).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal("14:47:00", picker.Value);
        Assert.False(picker.IsOpen);
        var limited = Show(new TimePicker { Max = "12:00", IsOpen = true });
        Assert.False(NowButton(limited).IsEnabled);
        Assert.Equal("此刻", NowButton(limited).Content);
    });

    [Fact]
    public Task Labels_follow_the_hour_cycle() => WpfUi.Run(() =>
    {
        var picker = Show(new TimePicker { Min = "09:00", Max = "13:00", Step = 60, HourCycle = "h12", IsOpen = true }, Lang.EnUs);
        Assert.Equal(["9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM"], Options(picker).Select(option => option.Label));
        Assert.Equal(["09:00", "10:00", "11:00", "12:00", "13:00"], Options(picker).Select(option => option.Value));
    });

    [Fact]
    public Task The_popover_matches_the_shell_width() => WpfUi.Run(() =>
    {
        var picker = Show(new TimePicker { IsOpen = true });
        WpfUi.Flush();
        Assert.Equal(picker.ActualWidth, picker.Panel.Width);
        Assert.True(List(picker).MaxHeight > 0);
    });
}
