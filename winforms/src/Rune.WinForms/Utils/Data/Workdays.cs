namespace Rune.WinForms;

/// <summary>
/// 工作日历（vue-tsx-pro 的 WorkCalendar / createWorkCalendar）：周末、法定节假日与调休补班，哪天上班。
/// 节假日每年发布一次，组件库不内置：应用建一份交给 Provider 的 WorkCalendar，时限标记（DeadlineBadge）按它算工作日。
/// <code>
/// var calendar = new WorkCalendar(
///     holidays: [(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8))],   // 放假（含两端；单日写成同一天）
///     workdays: [new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 10)]);  // 调休上班的周末
/// calendar.DeadlineOf(start: verifiedOn, workdays: 10, today: today);     // 截止日、还剩几个工作日、状态
/// </code>
/// 加减与相差的天数参考 date-fns 的 addBusinessDays、differenceInBusinessDays。
/// </summary>
/// <param name="weekend">休息的星期几；不写时周六、周日。</param>
/// <param name="holidays">放假的日子（含两端）。</param>
/// <param name="workdays">调休上班的日子（落在周末的补班）。</param>
public sealed class WorkCalendar(IEnumerable<DayOfWeek>? weekend = null, IEnumerable<(DateOnly Start, DateOnly End)>? holidays = null, IEnumerable<DateOnly>? workdays = null)
{
    readonly HashSet<DayOfWeek> weekend = [.. weekend ?? [DayOfWeek.Saturday, DayOfWeek.Sunday]];
    readonly HashSet<DateOnly> off = [.. (holidays ?? []).SelectMany(range => Days(range.Start, range.End))];
    readonly HashSet<DateOnly> on = [.. workdays ?? []];

    /// <summary>只按周末（周六、周日）算的工作日历：Provider 没配置时用它。</summary>
    public static WorkCalendar Weekdays { get; } = new();

    /// <summary>这一天上不上班：调休补班的上班，放假的、周末的不上。</summary>
    /// <param name="day">日期。</param>
    public bool IsWorkday(DateOnly day) => on.Contains(day) || (!off.Contains(day) && !weekend.Contains(day.DayOfWeek));

    /// <summary>from 之后到 to（含）有几个工作日：周五到下周一是 1；to 在 from 之前时为负数（两端对调再取负）。</summary>
    /// <param name="from">起点（不算）。</param>
    /// <param name="to">终点（算）。</param>
    public int WorkdaysBetween(DateOnly from, DateOnly to)
    {
        if (to < from) return -WorkdaysBetween(to, from);
        var count = 0;
        for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
            if (IsWorkday(day)) count++;
        return count;
    }

    /// <summary>从 from 起数 count 个工作日后的那一天（count 为负往前数，为 0 时原样）。</summary>
    /// <param name="from">起点。</param>
    /// <param name="count">几个工作日。</param>
    public DateOnly AddWorkdays(DateOnly from, int count)
    {
        var step = count >= 0 ? 1 : -1;
        var day = from;
        for (var left = Math.Abs(count); left > 0;)
        {
            day = day.AddDays(step);
            if (IsWorkday(day)) left--;
        }
        return day;
    }

    /// <summary>算时限：截止日直接给出（<paramref name="due"/>），或开始日加几个工作日（<paramref name="start"/>、<paramref name="workdays"/>）。</summary>
    /// <param name="due">截止日。</param>
    /// <param name="start">开始日。</param>
    /// <param name="workdays">开始日之后几个工作日截止。</param>
    /// <param name="done">完成的日期；完成了状态是 done，剩余按完成那天算。</param>
    /// <param name="today">今天。</param>
    /// <param name="soon">剩下几个工作日以内算快到期。</param>
    /// <exception cref="ArgumentException">截止日与开始日加工作日都没给。</exception>
    public Deadline DeadlineOf(DateOnly today, DateOnly? due = null, DateOnly? start = null, int? workdays = null, DateOnly? done = null, int soon = 2)
    {
        var end = due ?? (start is { } from && workdays is { } count ? AddWorkdays(from, count) : throw new ArgumentException("写截止日（due），或开始日加工作日（start、workdays）"));
        var at = done ?? today;
        var remaining = WorkdaysBetween(at, end);
        // 是否过期按日期比：周五截止、周六还没完成也算超期（虽然中间没有工作日）
        var state = done is not null ? DeadlineState.Done
            : at > end ? DeadlineState.Overdue
            : at == end ? DeadlineState.Due
            : remaining <= soon ? DeadlineState.Soon
            : DeadlineState.Pending;
        return new Deadline(end, remaining, state);
    }

    static IEnumerable<DateOnly> Days(DateOnly start, DateOnly end)
    {
        for (var day = start; day <= end; day = day.AddDays(1)) yield return day;
    }
}

/// <summary>时限的状态（vue-tsx-pro 的 DeadlineStates）。</summary>
public enum DeadlineState
{
    /// <summary>还早。</summary>
    Pending,

    /// <summary>快到期：剩下的工作日不多于 soon。</summary>
    Soon,

    /// <summary>今天到期。</summary>
    Due,

    /// <summary>超期。</summary>
    Overdue,

    /// <summary>已完成（按完成那天算剩余或超期）。</summary>
    Done,
}

/// <summary>时限：截止日、离截止还有几个工作日（截止当天为 0，超期为负；完成了按完成那天算）、状态。</summary>
/// <param name="Due">截止日。</param>
/// <param name="Remaining">离截止还有几个工作日。</param>
/// <param name="State">状态。</param>
public readonly record struct Deadline(DateOnly Due, int Remaining, DeadlineState State);
