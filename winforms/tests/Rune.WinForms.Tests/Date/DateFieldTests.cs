using System.ComponentModel.DataAnnotations;

namespace Rune.WinForms.Tests;

/// <summary>DateFieldControl、TimeFieldControl：段的顺序与读屏、逐段输入、填全才写回、↑↓ 与 Backspace、外部改值、超出范围出错、清空、语言与粒度、不可用、表单重置与 Enter 提交。</summary>
public class DateFieldTests
{
    static (T Control, Form Window) Show<T>(T control, string label = "日期", Lang? lang = null) where T : SegmentedFieldControl
    {
        var field = new FieldControl { Label = label, Content = control, Dock = DockStyle.Top };
        var provider = new ProviderControl { Lang = lang ?? Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(field);
        var window = WinFormsUi.Show(provider);
        control.Group.Focus();
        WinFormsUi.Pump();
        return (control, window);
    }

    static string Text(SegmentedFieldControl field) => field.Group.SegmentText;

    [Fact]
    [Upstream("DateField", "zh-CN 年/月/日")]
    public Task Segments_are_named_spin_buttons_in_culture_order() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl());
        using var _ = window;
        Assert.Equal("年/月/日", Text(date));
        var accessible = date.Group.AccessibilityObject;
        var segments = Enumerable.Range(0, accessible.GetChildCount()).Select(i => accessible.GetChild(i)!).ToList();
        Assert.Equal(["日期 年", "日期 月", "日期 日"], segments.Select(segment => segment.Name));
        Assert.All(segments, segment => Assert.Equal(AccessibleRole.SpinButton, segment.Role));
        Assert.Equal("未填写", segments[0].Value);
    });

    [Fact]
    [Upstream("DateField", "逐段输入：满了自动跳段、焦点跟过去；填全才写回 ISO")]
    public Task Typing_moves_focus_and_writes_iso_when_complete() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl());
        using var _ = window;
        date.Group.TypeText("2026");
        Assert.Equal(DateSegmentType.Month, date.Segments.FocusedSegment);
        date.Group.TypeText("10");
        Assert.Null(date.Value);
        date.Group.TypeText("20");
        Assert.Equal(("2026-10-20", new DateOnly(2026, 10, 20)), (date.Value, date.Date));
    });

    [Fact]
    [Upstream("DateField", "↑↓ 加减并回绕；Backspace 删空后写回 null")]
    public Task Arrows_wrap_and_backspace_clears_the_value() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl { Value = "2026-12-20" });
        using var _ = window;
        date.Segments.SetFocused(DateSegmentType.Month);
        date.Group.Press(Keys.Up);
        Assert.Equal("2026-01-20", date.Value);
        date.Segments.SetFocused(DateSegmentType.Day);
        date.Group.Press(Keys.Back);
        date.Group.Press(Keys.Back);
        Assert.Null(date.Value);
    });

    [Fact]
    [Upstream("DateField", "外部改值时各段跟着变")]
    public Task Outside_values_show_in_the_segments() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl { Value = "2026-10-20" });
        using var _ = window;
        date.Date = new DateOnly(2025, 1, 2);
        Assert.Equal("2025/01/02", Text(date));
    });

    [Fact]
    [Upstream("DateField", "超出 min / max、不可选的日期：外壳显示出错，各段 aria-invalid")]
    public Task Out_of_range_and_unavailable_dates_are_invalid() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl { Value = "2026-10-20", Max = "2026-10-01" });
        using var _ = window;
        Assert.True(date.Group.IsInvalid);
        date.Max = null;
        date.IsDateUnavailable = day => day.DayOfWeek == DayOfWeek.Tuesday;
        Assert.True(date.Group.IsInvalid);
        date.IsDateUnavailable = null;
        Assert.False(date.Group.IsInvalid);
    });

    [Fact]
    [Upstream("DateField", "min / max 带时间时同一天里比时分；只给日期时那一整天都可选")]
    public Task Timed_limits_compare_within_the_day() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T08:30", Min = "2026-10-20T09:00" });
        using var _ = window;
        Assert.True(date.Group.IsInvalid);
        date.Min = "2026-10-20";
        Assert.False(date.Group.IsInvalid);
    });

    [Fact]
    [Upstream("DateField", "清空按钮、隐藏的表单值")]
    public Task The_clear_button_empties_the_value() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl { Value = "2026-10-20", Clearable = true });
        using var _ = window;
        Assert.True(date.ClearButton.Visible);
        date.ClearButton.PerformClick();
        Assert.Null(date.Value);
        Assert.Equal("年/月/日", Text(date));
        Assert.False(date.ClearButton.Visible);
    });

    [Fact]
    [Upstream("DateField", "en-US 月/日/年；granularity=")]
    public Task English_order_and_granularities() => WinFormsUi.Run(() =>
    {
        var day = new DateFieldControl { Value = "2026-10-20", Dock = DockStyle.Top };
        var month = new DateFieldControl { Granularity = DateFieldGranularity.Month, Value = "2026-10", Dock = DockStyle.Top };
        var minute = new DateFieldControl { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30", Dock = DockStyle.Top };
        var provider = new ProviderControl { Lang = Lang.EnUs, Dock = DockStyle.Fill };
        provider.Controls.AddRange([day, month, minute]);
        using var window = WinFormsUi.Show(provider);
        Assert.Equal(["10/20/2026", "10/2026", "10/20/2026, 2:30 PM"], new[] { day, month, minute }.Select(Text));
    });

    [Fact]
    [Upstream("DateField", "不可用：段不能聚焦")]
    public Task Disabled_segments_cannot_take_focus() => WinFormsUi.Run(() =>
    {
        var (date, window) = Show(new DateFieldControl());
        using var _ = window;
        date.Enabled = false;
        Assert.False(date.Group.CanSelect);
    });

    [Fact]
    [Upstream("TimeField", "zh-CN 24 小时制：打 1430 得到 14:30")]
    public Task Twenty_four_hour_time() => WinFormsUi.Run(() =>
    {
        var (time, window) = Show(new TimeFieldControl(), "提醒时间");
        using var _ = window;
        time.Group.TypeText("1430");
        Assert.Equal(("14:30", new TimeOnly(14, 30)), (time.Value, time.Time));
    });

    [Fact]
    [Upstream("TimeField", "en-US 12 小时制：打 a / p 切换上午下午，值仍是 24 小时制")]
    public Task Twelve_hour_time_keeps_a_24_hour_value() => WinFormsUi.Run(() =>
    {
        var (time, window) = Show(new TimeFieldControl { Value = "09:15" }, "At", Lang.EnUs);
        using var _ = window;
        Assert.Equal("9:15 AM", Text(time));
        time.Segments.SetFocused(DateSegmentType.DayPeriod);
        time.Group.TypeText("p");
        Assert.Equal("21:15", time.Value);
    });

    [Fact]
    [Upstream("TimeField", "秒、min / max")]
    [Upstream("TimeField", "TimeField：Time 进、Time 出")]
    public Task Seconds_and_limits() => WinFormsUi.Run(() =>
    {
        var (time, window) = Show(new TimeFieldControl { Granularity = TimeFieldGranularity.Second, Value = "08:30:15", Min = "09:00", Max = "18:00" });
        using var _ = window;
        Assert.Equal("08:30:15", Text(time));
        Assert.True(time.Group.IsInvalid);
        time.Time = new TimeOnly(12, 0);
        Assert.Equal("12:00:00", time.Value);
        Assert.False(time.Group.IsInvalid);
    });

    public sealed class Meeting
    {
        [Required]
        public string? Day { get; set; }
    }

    [Fact]
    [Upstream("TimeField", "在表单里：打了一半时表单重置，各段清空；Enter 写回并提交表单")]
    public Task Form_reset_drops_partial_input_and_enter_submits() => WinFormsUi.Run(() =>
    {
        var submitted = 0;
        var controller = new FormController<Meeting>(new Meeting(), () => Lang.ZhCn)
        {
            OnSubmit = _ =>
            {
                submitted++;
                return Task.CompletedTask;
            },
        };
        var date = new DateFieldControl();
        var form = new FormControl { Controller = controller, Dock = DockStyle.Fill };
        form.Controls.Add(new FieldControl { Path = "Day", Label = "日期", Content = date, Dock = DockStyle.Top });
        using var window = WinFormsUi.Show(form);
        date.Group.Focus();
        WinFormsUi.Pump();

        date.Group.TypeText("2026");
        Assert.True(date.Segments.Partial);
        controller.Reset();
        Assert.Equal("年/月/日", Text(date));
        date.Segments.SetFocused(DateSegmentType.Year);
        date.Group.TypeText("20261020");
        Assert.Equal("2026-10-20", controller.Values.Day);
        date.Group.Press(Keys.Enter);
        WinFormsUi.Until(() => submitted == 1);
    });

    [Fact]
    [Upstream("TimeField", "季度、周：按语言包的排法显示")]
    public Task Quarters_and_weeks_use_the_language_patterns() => WinFormsUi.Run(() =>
    {
        var (quarter, window) = Show(new DateFieldControl { Granularity = DateFieldGranularity.Quarter });
        using var _ = window;
        Assert.Equal("––年第––季度", Text(quarter));
        quarter.Segments.SetFocused(DateSegmentType.Year);
        quarter.Group.TypeText("20264");
        Assert.Equal("2026-Q4", quarter.Value);
        Assert.Equal("2026年第4季度", Text(quarter));
    });
}
