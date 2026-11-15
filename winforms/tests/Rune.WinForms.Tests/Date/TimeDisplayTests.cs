using Microsoft.Extensions.Time.Testing;

namespace Rune.WinForms.Tests;

/// <summary>显示时间的组件：DateTimeText、RelativeTime、Countdown，共用的时刻换算与相对时间的单位（TimeText）。</summary>
public class TimeDisplayTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T08:00:00Z");
    static readonly Lang Shanghai = Lang.ZhCn with { TimeZone = "Asia/Shanghai" };
    static readonly TimeZoneInfo Zone = TimeText.Zone("Asia/Shanghai");

    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Shanghai };
        host.Controls.AddRange(children);
        return host;
    }

    [Fact]
    public void Instant_keeps_offsets_and_reads_wall_times_in_the_zone()
    {
        Assert.Equal(Now, TimeText.Instant("2026-10-21T08:00:00Z", Zone));
        Assert.Equal(Now, TimeText.Instant("2026-10-21T16:00", Zone));
        Assert.Equal(Now, TimeText.Instant(new DateTime(2026, 10, 21, 16, 0, 0), Zone));
        Assert.Equal(Now, TimeText.Instant(new DateTimeOffset(2026, 10, 21, 16, 0, 0, TimeSpan.FromHours(8)), TimeZoneInfo.Utc));
        Assert.Null(TimeText.Instant("not a date", Zone));
    }

    [Fact]
    public void Relative_unit_switches_at_the_moment_thresholds()
    {
        Assert.Equal((-30, "second"), TimeText.RelativeUnit(TimeSpan.FromSeconds(-30)));
        Assert.Equal((-1, "minute"), TimeText.RelativeUnit(TimeSpan.FromSeconds(-50)));
        Assert.Equal((3, "hour"), TimeText.RelativeUnit(TimeSpan.FromHours(3)));
        Assert.Equal((-1, "day"), TimeText.RelativeUnit(TimeSpan.FromDays(-1)));
        Assert.Equal((-1, "year"), TimeText.RelativeUnit(TimeSpan.FromDays(-400)));
        Assert.Equal((0, "second"), TimeText.RelativeUnit(TimeSpan.FromMilliseconds(-100)));
    }

    [Fact]
    public void Wording_follows_intl_in_chinese_and_english()
    {
        var wall = new DateTime(2026, 10, 20, 14, 30, 5);
        var offset = TimeSpan.FromHours(8);
        Assert.Equal("2026/10/20 14:30:05", TimeText.Format(wall, offset, Zone, "zh-CN", DateStyle.Short, TimeStyle.Medium));
        Assert.Equal("2026年10月20日 GMT+8 14:30:05", TimeText.Format(wall, offset, Zone, "zh-CN", DateStyle.Long, TimeStyle.Long));
        Assert.Equal("Tuesday, October 20, 2026 at 2:30 PM", TimeText.Format(wall, offset, Zone, "en-US", DateStyle.Full, TimeStyle.Short));
        Assert.Equal("Oct 20, 2026, 2:30 PM", TimeText.Format(wall, offset, Zone, "en-US", DateStyle.Medium, TimeStyle.Short));
        Assert.Equal("10/20/26", TimeText.Format(wall, offset, Zone, "en-US", DateStyle.Short, null));
        Assert.Equal(["现在", "前天", "上个月", "3个月后", "30秒前"], new[]
        {
            TimeText.Relative(TimeSpan.Zero, "zh-CN", RelativeTimeStyle.Long),
            TimeText.Relative(TimeSpan.FromDays(-2), "zh-CN", RelativeTimeStyle.Long),
            TimeText.Relative(TimeSpan.FromDays(-30), "zh-CN", RelativeTimeStyle.Long),
            TimeText.Relative(TimeSpan.FromDays(90), "zh-CN", RelativeTimeStyle.Long),
            TimeText.Relative(TimeSpan.FromSeconds(-30), "zh-CN", RelativeTimeStyle.Short),
        });
        Assert.Equal(["30 seconds ago", "in 3 hr.", "2d ago", "yesterday", "last yr.", "in 2mo"], new[]
        {
            TimeText.Relative(TimeSpan.FromSeconds(-30), "en-US", RelativeTimeStyle.Long),
            TimeText.Relative(TimeSpan.FromHours(3), "en-US", RelativeTimeStyle.Short),
            TimeText.Relative(TimeSpan.FromDays(-2), "en-US", RelativeTimeStyle.Narrow),
            TimeText.Relative(TimeSpan.FromDays(-1), "en-US", RelativeTimeStyle.Narrow),
            TimeText.Relative(TimeSpan.FromDays(-365), "en-US", RelativeTimeStyle.Short),
            TimeText.Relative(TimeSpan.FromDays(60), "en-US", RelativeTimeStyle.Narrow),
        });
    }

    [Fact]
    [Upstream("DateTime", "time 元素：datetime 写机器可读的值；只有日期时不显示时间")]
    public Task Machine_value_and_date_only_values_without_time() => WinFormsUi.Run(() =>
    {
        DateTimeTextControl[] times =
        [
            new() { Value = "2026-10-20" },
            new() { Value = new DateOnly(2026, 10, 20), DateStyle = DateStyle.Full },
            new() { Value = new DateTime(2026, 10, 20, 14, 30, 0) },
            new() { Value = new DateTime(2026, 10, 20, 14, 30, 0), Format = "M" },
        ];
        var host = Host(times);
        using var form = WinFormsUi.Show(host);

        Assert.Equal(["2026-10-20", "2026-10-20", "2026-10-20T14:30:00", "2026-10-20T14:30:00"], times.Select(time => time.MachineValue));
        Assert.Equal(["2026年10月20日", "2026年10月20日星期二", "2026年10月20日 14:30", "10月20日"], times.Select(time => time.Text));
        Assert.Equal(AccessibleRole.StaticText, times[0].AccessibilityObject.Role);
        // 换语言包跟着换
        host.Lang = Lang.EnUs;
        Assert.StartsWith("Oct 20, 2026, 2:30", times[2].Text);
    });

    [Fact]
    [Upstream("RelativeTime", "RelativeTime：措辞用 numeric auto；跨过阈值换单位；threshold 以外写日期；title 是完整的日期时间")]
    public Task Relative_wording_thresholds_and_full_date_tooltip() => WinFormsUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var recent = new RelativeTimeControl { TimeProvider = clock, Value = Now.AddSeconds(-30) };
        var yesterday = new RelativeTimeControl { TimeProvider = clock, Value = Now.AddDays(-1) };
        var old = new RelativeTimeControl { TimeProvider = clock, Value = Now.AddDays(-10), Threshold = TimeSpan.FromDays(7) };
        using var form = WinFormsUi.Show(Host(recent, yesterday, old));

        Assert.Equal("30秒钟前", recent.Text);
        Assert.Equal("昨天", yesterday.Text);
        Assert.Matches(@"^2026年10月\d+日$", old.Text);
        Assert.StartsWith("2026年10月21日星期三", recent.ShownTooltip);
        Assert.StartsWith("2026年10月21日星期三", recent.AccessibilityObject.Description);
        // 秒级每秒更新：20 秒后过了 45 秒，换成分钟
        clock.Advance(TimeSpan.FromSeconds(20));
        WinFormsUi.Pump();
        Assert.Equal("1分钟前", recent.Text);
    });

    [Fact]
    public Task Fixed_now_does_not_tick() => WinFormsUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var time = new RelativeTimeControl { TimeProvider = clock, Value = Now.AddMinutes(-3), Now = Now };
        using var form = WinFormsUi.Show(Host(time));

        clock.Advance(TimeSpan.FromHours(2));
        WinFormsUi.Pump();
        Assert.Equal("3分钟前", time.Text);
    });

    [Fact]
    [Upstream("Countdown", "Countdown：时:分:秒，超过一天加天数；到点停在 0、onEnd 只调用一次")]
    public Task Countdown_clock_days_and_one_end_event() => WinFormsUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var ended = 0;
        CountdownControl[] timers =
        [
            new() { TimeProvider = clock, To = Now.AddSeconds(3) },
            new() { TimeProvider = clock, To = Now.AddDays(2).AddSeconds(3723) },
            new() { TimeProvider = clock, To = Now.AddMinutes(90), Precision = CountdownPrecision.Minute },
            new() { TimeProvider = clock, To = Now.AddSeconds(3), Formatter = left => $"还剩 {left.Seconds} 秒" },
        ];
        timers[0].Ended += (_, _) => ended++;
        using var form = WinFormsUi.Show(Host(timers));

        Assert.Equal(["00:00:03", "2天 01:02:03", "01:30", "还剩 3 秒"], timers.Select(timer => timer.DisplayText));
        Assert.Equal(AccessibleRole.StaticText, timers[1].AccessibilityObject.Role);
        Assert.Equal("2天 01:02:03", timers[1].AccessibilityObject.Name);
        clock.Advance(TimeSpan.FromSeconds(5));
        WinFormsUi.Pump();
        Assert.Equal("00:00:00", timers[0].DisplayText);
        Assert.Equal("还剩 0 秒", timers[3].DisplayText);
        clock.Advance(TimeSpan.FromSeconds(5));
        WinFormsUi.Pump();
        Assert.Equal(1, ended);
    });

    [Fact]
    [Upstream("Countdown", "Countdown variant=")]
    public Task Blocks_put_days_and_each_clock_unit_in_its_own_segment() => WinFormsUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var timer = new CountdownControl { TimeProvider = clock, To = Now.AddDays(2).AddSeconds(3723), Variant = CountdownVariant.Blocks, AccessibleName = "距抢购开始" };
        using var form = WinFormsUi.Show(Host(timer));

        Assert.Equal(["2天", "01", "02", "03"], timer.Element.Segments);
        Assert.Equal(2, timer.Element.SeparatorCount);
        Assert.Equal("距抢购开始", timer.AccessibilityObject.Name);
        // 格子比文字宽、比文字高（底色的内边距）
        var plain = new CountdownControl { TimeProvider = clock, To = Now.AddDays(2).AddSeconds(3723), AutoSize = true };
        form.Controls.Add(plain);
        Assert.True(timer.Height > plain.Height);
        clock.Advance(TimeSpan.FromSeconds(1));
        WinFormsUi.Pump();
        Assert.Equal(["2天", "01", "02", "02"], timer.Element.Segments);
    });

    [Fact]
    public Task Real_clock_ticks_back_on_the_ui_thread() => WinFormsUi.Run(() =>
    {
        var timer = new CountdownControl { To = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds() + 30) };
        using var form = WinFormsUi.Show(Host(timer));
        var first = timer.DisplayText;

        // 系统时钟的回调在线程池上：投递回界面线程再刷新。目标在整秒上：对齐到整秒的下一跳一定少一秒（向上取整）
        // 下一跳在一秒多一点之后；CI 的机器忙时界面线程晚些才轮到，等到变了为止（最多 3 秒）
        WinFormsUi.Until(() => timer.DisplayText != first);
    });

    [Fact]
    [Upstream("RelativeTime", "countdownParts：按精度向上取整，到点才是 0")]
    public void Split_rounds_up_to_the_precision()
    {
        var almost = TimeText.Split(TimeSpan.FromMilliseconds(400), CountdownPrecision.Second);
        Assert.Equal((1, false), (almost.Seconds, almost.Ended));
        var minute = TimeText.Split(TimeSpan.FromSeconds(61), CountdownPrecision.Minute);
        Assert.Equal((2, 0), (minute.Minutes, minute.Seconds));
        var over = TimeText.Split(TimeSpan.FromMilliseconds(-5), CountdownPrecision.Second);
        Assert.Equal((TimeSpan.Zero, 0, true), (over.Total, over.Seconds, over.Ended));
    }
}
