using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 时间选择器列表里的时刻（vue-tsx-pro TimePicker 的 options）：从 min（默认 00:00）起每 step 分钟一项，到 max（默认 23:59）为止；
/// 文字按语言的 12 / 24 小时制（24 小时制补零：09:00，同浏览器 timeStyle short），值是 HH:mm（精确到秒时 HH:mm:ss）。
/// </summary>
static class TimeSlots
{
    /// <summary>列表里的时刻。</summary>
    /// <param name="min">最早（null 时 00:00）。</param>
    /// <param name="max">最晚（null 时 23:59）。</param>
    /// <param name="step">相邻两项隔几分钟（小于 1 时按 1）。</param>
    public static IEnumerable<TimeOnly> Between(TimeOnly? min, TimeOnly? max, int step)
    {
        var first = Minutes(min ?? new TimeOnly(0, 0));
        var last = Minutes(max ?? new TimeOnly(23, 59));
        for (var minutes = first; minutes <= last; minutes += Math.Max(step, 1)) yield return new TimeOnly(minutes / 60, minutes % 60);
    }

    /// <summary>列表里的文字：12 小时制用语言的上午 / 下午（9:00 AM、上午9:00），24 小时制补零（09:00）。</summary>
    /// <param name="time">时刻。</param>
    /// <param name="culture">语言。</param>
    /// <param name="hourCycle">"h12" 或 "h23"；null 时按语言的短时间格式。</param>
    public static string Label(TimeOnly time, CultureInfo culture, string? hourCycle)
    {
        var pattern = culture.DateTimeFormat.ShortTimePattern;
        var hour12 = hourCycle is null ? pattern.Contains('h') : hourCycle == "h12";
        if (!hour12) return time.ToString("HH:mm", CultureInfo.InvariantCulture);
        // 语言的短时间格式本来就是 12 小时制时照用（en-US 的 h:mm tt）；否则上午 / 下午放前面（zh-CN 的 tth:mm，同浏览器）
        return time.ToString(pattern.Contains('h') ? pattern : "tth:mm", culture);
    }

    /// <summary>时刻 → 值（HH:mm，精确到秒时补 :00）。</summary>
    public static string Iso(TimeOnly time, bool seconds) => time.ToString(seconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>值 → 时刻（HH:mm 或 HH:mm:ss）；不是时间时 null。</summary>
    public static TimeOnly? Parse(string? iso) =>
        iso is not null && TimeOnly.TryParseExact(iso, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
}
