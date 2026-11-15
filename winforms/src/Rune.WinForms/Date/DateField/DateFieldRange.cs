using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日期值与范围（上游 DateField 的 isPeriodDisabled、timeBound）：一段（年、季度、月、周、日）整段都在 Min–Max 之外、或按日选时是不可选的日期，就是超出范围；
/// Min / Max 带时间时，同一天里再比时分秒；不可选的时刻按那一天判断。DatePicker、DateRangePicker 也用。
/// </summary>
public static class DateFieldRange
{
    /// <summary>ISO 值代表的那一段日期（第一天到最后一天）；格式不对时 null。</summary>
    public static (DateOnly Start, DateOnly End)? Period(string iso)
    {
        static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);
        if (iso.Length >= 10 && DateOnly.TryParseExact(iso[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return (day, day);
        if (iso.Length == 7 && iso[4] == '-' && iso[5] is 'Q')
        {
            var start = new DateOnly(Number(iso[..4]), (Number(iso[6..]) - 1) * 3 + 1, 1);
            return (start, start.AddMonths(3).AddDays(-1));
        }
        if (iso.Length == 8 && iso[5] == 'W')
        {
            var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(Number(iso[..4]), Number(iso[6..]), DayOfWeek.Monday));
            return (monday, monday.AddDays(6));
        }
        if (iso.Length == 7 && DateOnly.TryParseExact(iso + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)) return (month, month.AddMonths(1).AddDays(-1));
        if (iso.Length == 4 && int.TryParse(iso, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year >= 1) return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        return null;
    }

    /// <summary>ISO 值代表的时刻（带时间的取时间，只有日期的是那一天的零点）。</summary>
    public static DateTime? Instant(string iso) =>
        iso.Contains('T') && System.DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time
        : Period(iso) is { } period ? period.Start.ToDateTime(TimeOnly.MinValue) : null;

    /// <summary>这个值能不能选：先按日期比较（含不可选的日期）；Min / Max 带时间时同一天里再比时分秒；带时间时还看不可选的时刻。</summary>
    public static bool IsOutOfRange(string iso, string? min, string? max, Func<DateOnly, bool>? dateUnavailable, Func<TimeOnly, DateOnly, bool>? timeUnavailable)
    {
        if (Period(iso) is not { } period) return false;
        if (max is not null && Period(max) is { } high && period.Start > high.End) return true;
        if (min is not null && Period(min) is { } low && period.End < low.Start) return true;
        var timed = iso.Contains('T');
        if ((timed || iso.Length == 10) && dateUnavailable?.Invoke(period.Start) == true) return true;
        if (!timed || Instant(iso) is not { } at) return false;
        if (min is { } lower && lower.Contains('T') && Instant(lower) is { } earliest && at < earliest) return true;
        if (max is { } upper && upper.Contains('T') && Instant(upper) is { } latest && at > latest) return true;
        return timeUnavailable?.Invoke(TimeOnly.FromDateTime(at), DateOnly.FromDateTime(at)) == true;
    }
}
