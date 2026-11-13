namespace Rune.Wpf.Tests;

/// <summary>工作日：周末、节假日与调休；from 之后到 to 的工作日数（反向为负）；往后、往前数工作日；时限的状态。</summary>
public class WorkdaysTests
{
    static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // 2026-10：1 日周四；国庆 1–8 日放假，9 月 27 日（周日）与 10 月 10 日（周六）调休上班
    static readonly WorkCalendar National = new(holidays: [(D("2026-10-01"), D("2026-10-08"))], workdays: [D("2026-09-27"), D("2026-10-10")]);

    static readonly WorkCalendar Weekdays = WorkCalendar.Weekdays;

    [Fact]
    [Upstream("createWorkCalendar", "周末不上班；节假日（含区间）不上班；调休的周末上班")]
    public void Weekends_holidays_and_swapped_workdays()
    {
        Assert.True(Weekdays.IsWorkday(D("2026-10-23"))); // 周五
        Assert.False(Weekdays.IsWorkday(D("2026-10-24"))); // 周六
        Assert.False(National.IsWorkday(D("2026-10-05"))); // 国庆中间的周一
        Assert.True(National.IsWorkday(D("2026-10-10"))); // 调休的周六
        Assert.True(National.IsWorkday(D("2026-09-27"))); // 调休的周日
    }

    [Fact]
    [Upstream("workdaysBetween", "不含起点、含终点：周五到下周一是 1；同一天是 0；反向为负")]
    public void Between_excludes_the_start_and_includes_the_end()
    {
        Assert.Equal(1, Weekdays.WorkdaysBetween(D("2026-10-23"), D("2026-10-26")));
        Assert.Equal(0, Weekdays.WorkdaysBetween(D("2026-10-23"), D("2026-10-23")));
        Assert.Equal(-1, Weekdays.WorkdaysBetween(D("2026-10-26"), D("2026-10-23")));
    }

    [Fact]
    [Upstream("workdaysBetween", "按工作日历跳过节假日、算上调休")]
    public void Between_follows_the_calendar()
    {
        // 9-30（周三）到 10-12（周一）：国庆放假，10-09（周五）、10-10（调休周六）、10-12 上班
        Assert.Equal(3, National.WorkdaysBetween(D("2026-09-30"), D("2026-10-12")));
        Assert.Equal(8, Weekdays.WorkdaysBetween(D("2026-09-30"), D("2026-10-12")));
    }

    [Fact]
    [Upstream("addWorkdays", "往后、往前数；0 原样；跳过节假日")]
    public void Adding_counts_forward_and_backward()
    {
        Assert.Equal(D("2026-10-26"), Weekdays.AddWorkdays(D("2026-10-23"), 1));
        Assert.Equal(D("2026-10-23"), Weekdays.AddWorkdays(D("2026-10-26"), -1));
        Assert.Equal(D("2026-10-24"), Weekdays.AddWorkdays(D("2026-10-24"), 0));
        Assert.Equal(D("2026-10-10"), National.AddWorkdays(D("2026-09-30"), 2));
    }

    [Fact]
    [Upstream("addWorkdays", "与 workdaysBetween 互逆")]
    public void Adding_and_counting_are_inverse()
    {
        foreach (var count in new[] { 1, 5, 10, 23 })
            Assert.Equal(count, National.WorkdaysBetween(D("2026-09-28"), National.AddWorkdays(D("2026-09-28"), count)));
    }

    [Fact]
    [Upstream("deadlineOf", "开始日加工作日得出截止日；按今天给出剩余与状态")]
    public void Deadlines_from_a_start_and_workdays()
    {
        var start = D("2026-10-12"); // 周一；10 个工作日后是 10-26（周一）
        Assert.Equal(new Deadline(D("2026-10-26"), 8, DeadlineState.Pending), Weekdays.DeadlineOf(D("2026-10-14"), start: start, workdays: 10));
        Assert.Equal(DeadlineState.Soon, Weekdays.DeadlineOf(D("2026-10-22"), start: start, workdays: 10).State);
        Assert.Equal(DeadlineState.Due, Weekdays.DeadlineOf(D("2026-10-26"), start: start, workdays: 10).State);
        var late = Weekdays.DeadlineOf(D("2026-10-27"), start: start, workdays: 10);
        Assert.Equal((-1, DeadlineState.Overdue), (late.Remaining, late.State));
    }

    [Fact]
    [Upstream("deadlineOf", "截止日过了但中间没有工作日（周五截止、周六）：仍是超期")]
    public void A_weekend_after_the_due_day_is_still_overdue() =>
        Assert.Equal(new Deadline(D("2026-10-23"), 0, DeadlineState.Overdue), Weekdays.DeadlineOf(D("2026-10-24"), due: D("2026-10-23")));

    [Fact]
    [Upstream("deadlineOf", "完成了按完成那天算：提前完成剩余为正，晚了为负")]
    public void Done_counts_from_the_day_it_was_done()
    {
        Assert.Equal(new Deadline(D("2026-10-23"), 2, DeadlineState.Done), Weekdays.DeadlineOf(D("2026-10-30"), due: D("2026-10-23"), done: D("2026-10-21")));
        Assert.Equal(-2, Weekdays.DeadlineOf(D("2026-10-30"), due: D("2026-10-23"), done: D("2026-10-27")).Remaining);
    }
}
