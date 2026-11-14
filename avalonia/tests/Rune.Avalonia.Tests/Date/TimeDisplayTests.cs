using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Rune.Avalonia.Tests;

/// <summary>显示时间的组件：DateTimeText、RelativeTime、Countdown，共用的时刻换算与相对时间的单位（TimeText）。</summary>
public class TimeDisplayTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T08:00:00Z");
    static readonly Lang Shanghai = Lang.ZhCn with { TimeZone = "Asia/Shanghai" };
    static readonly TimeZoneInfo Zone = TimeText.Zone("Asia/Shanghai");

    static StackPanel Host(params Control[] children)
    {
        var host = new StackPanel();
        Provider.SetLang(host, Shanghai);
        host.Children.AddRange(children);
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
    public Task Machine_value_and_date_only_values_without_time() => HeadlessUi.Run(() =>
    {
        DateTimeText[] times =
        [
            new() { Value = "2026-10-20" },
            new() { Value = new DateOnly(2026, 10, 20), DateStyle = DateStyle.Full },
            new() { Value = new DateTime(2026, 10, 20, 14, 30, 0) },
            new() { Value = new DateTime(2026, 10, 20, 14, 30, 0), Format = "M" },
        ];
        HeadlessUi.Show(Host(times));

        Assert.Equal(["2026-10-20", "2026-10-20", "2026-10-20T14:30:00", "2026-10-20T14:30:00"], times.Select(time => time.MachineValue));
        Assert.Equal(["2026年10月20日", "2026年10月20日星期二", "2026年10月20日 14:30", "10月20日"], times.Select(time => time.Text));
        // 换语言包跟着换
        Provider.SetLang(times[2], Lang.EnUs);
        Assert.StartsWith("Oct 20, 2026, 2:30", times[2].Text);
    });

    [Fact]
    [Upstream("RelativeTime", "RelativeTime：措辞用 numeric auto；跨过阈值换单位；threshold 以外写日期；title 是完整的日期时间")]
    public Task Relative_wording_thresholds_and_full_date_tooltip() => HeadlessUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var recent = new RelativeTime { TimeProvider = clock, Value = Now.AddSeconds(-30) };
        var yesterday = new RelativeTime { TimeProvider = clock, Value = Now.AddDays(-1) };
        var old = new RelativeTime { TimeProvider = clock, Value = Now.AddDays(-10), Threshold = TimeSpan.FromDays(7) };
        HeadlessUi.Show(Host(recent, yesterday, old));

        Assert.Equal("30秒钟前", recent.Text);
        Assert.Equal("昨天", yesterday.Text);
        Assert.Matches(@"^2026年10月\d+日$", old.Text);
        Assert.StartsWith("2026年10月21日星期三", (string)ToolTip.GetTip(recent)!);
        Assert.StartsWith("2026年10月21日星期三", AutomationProperties.GetHelpText(recent));
        // 秒级每秒更新：20 秒后过了 45 秒，换成分钟
        clock.Advance(TimeSpan.FromSeconds(20));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("1分钟前", recent.Text);
    });

    [Fact]
    public Task Fixed_now_does_not_tick() => HeadlessUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var time = new RelativeTime { TimeProvider = clock, Value = Now.AddMinutes(-3), Now = Now };
        HeadlessUi.Show(Host(time));

        clock.Advance(TimeSpan.FromHours(2));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3分钟前", time.Text);
    });

    [Fact]
    [Upstream("Countdown", "Countdown：时:分:秒，超过一天加天数；到点停在 0、onEnd 只调用一次")]
    public Task Countdown_clock_days_and_one_end_event() => HeadlessUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var ended = 0;
        Countdown[] timers =
        [
            new() { TimeProvider = clock, To = Now.AddSeconds(3) },
            new() { TimeProvider = clock, To = Now.AddDays(2).AddSeconds(3723) },
            new() { TimeProvider = clock, To = Now.AddMinutes(90), Precision = CountdownPrecision.Minute },
            new() { TimeProvider = clock, To = Now.AddSeconds(3), Formatter = left => $"还剩 {left.Seconds} 秒" },
        ];
        timers[0].Ended += (_, _) => ended++;
        HeadlessUi.Show(Host(timers));

        Assert.Equal(["00:00:03", "2天 01:02:03", "01:30", "还剩 3 秒"], timers.Select(timer => timer.DisplayText));
        var peer = timers[1].Peer();
        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Equal("timer", AutomationProperties.GetItemType(timers[1]));
        Assert.Equal("2天 01:02:03", peer.GetName());
        clock.Advance(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("00:00:00", timers[0].DisplayText);
        Assert.Equal("还剩 0 秒", timers[3].DisplayText);
        clock.Advance(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, ended);
    });

    [Fact]
    [Upstream("Countdown", "Countdown variant=")]
    public Task Blocks_put_days_and_each_clock_unit_in_its_own_segment() => HeadlessUi.Run(() =>
    {
        var clock = new FakeTimeProvider(Now);
        var timer = new Countdown { TimeProvider = clock, To = Now.AddDays(2).AddSeconds(3723), Variant = CountdownVariant.Blocks };
        AutomationProperties.SetName(timer, "距抢购开始");
        HeadlessUi.Show(Host(timer));

        var segments = timer.BlocksPart.Children.OfType<Border>().ToList();
        Assert.All(segments, segment => Assert.Equal(CountdownParts.Segment, segment.Name));
        Assert.Equal(["2天", "01", "02", "03"], segments.Select(segment => ((Text)segment.Child!).Text));
        Assert.Equal(2, timer.BlocksPart.Children.OfType<Text>().Count(text => text.Name == CountdownParts.Separator));
        Assert.Equal("距抢购开始", timer.Peer().GetName());
        // 每秒只换文字，不重建格子
        clock.Advance(TimeSpan.FromSeconds(1));
        Dispatcher.UIThread.RunJobs();
        Assert.Same(segments[3], timer.BlocksPart.Children.OfType<Border>().Last());
        Assert.Equal("02", ((Text)segments[3].Child!).Text);
    });

    [Fact]
    public Task Real_clock_ticks_back_on_the_ui_thread() => HeadlessUi.Run(() =>
    {
        var timer = new Countdown { To = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds() + 30) };
        HeadlessUi.Show(Host(timer));
        var first = timer.DisplayText;

        // 系统时钟的回调在线程池上：投递回界面线程再刷新。目标在整秒上：对齐到整秒的下一跳一定少一秒（向上取整）
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(1300));
        Assert.NotEqual(first, timer.DisplayText);
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
