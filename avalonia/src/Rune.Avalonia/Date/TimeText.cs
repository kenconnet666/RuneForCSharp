using System.Globalization;

namespace Rune.Avalonia;

/// <summary>日期的详略（Intl 的 dateStyle）。</summary>
public enum DateStyle
{
    /// <summary>带星期：2026年10月20日星期二、Tuesday, October 20, 2026。</summary>
    Full,

    /// <summary>月份写全：2026年10月20日、October 20, 2026。</summary>
    Long,

    /// <summary>默认：2026年10月20日、Oct 20, 2026。</summary>
    Medium,

    /// <summary>数字：2026/10/20、10/20/26。</summary>
    Short,
}

/// <summary>时间的详略（Intl 的 timeStyle）。</summary>
public enum TimeStyle
{
    /// <summary>带时区名：英文写时区的名字，中文同 Long。</summary>
    Full,

    /// <summary>带时区偏移：GMT+8 14:30:05、2:30:05 PM GMT+8。</summary>
    Long,

    /// <summary>到秒：14:30:05、2:30:05 PM。</summary>
    Medium,

    /// <summary>到分（带时间的值默认）：14:30、2:30 PM。</summary>
    Short,
}

/// <summary>相对时间的详略（Intl.RelativeTimeFormat 的 style）。</summary>
public enum RelativeTimeStyle
{
    /// <summary>默认：3 分钟前、3 minutes ago。</summary>
    Long,

    /// <summary>缩写：3分钟前、3 min. ago。</summary>
    Short,

    /// <summary>最短：3分钟前、3m ago。</summary>
    Narrow,
}

/// <summary>倒计时的精度。</summary>
public enum CountdownPrecision
{
    /// <summary>到秒（默认）：每秒更新。</summary>
    Second,

    /// <summary>到分：每分钟更新，不显示秒。</summary>
    Minute,
}

/// <summary>倒计时还剩多久，拆成天、时、分、秒（vue-tsx-pro 的 CountdownParts；这里 XxxParts 是部件名的类，所以换了名）。</summary>
/// <param name="Total">剩余的时长，到点后是 0。</param>
/// <param name="Days">整天数。</param>
/// <param name="Hours">不满一天的小时（0–23）。</param>
/// <param name="Minutes">不满一小时的分钟（0–59）。</param>
/// <param name="Seconds">不满一分钟的秒（0–59）；精度到分时总是 0。</param>
/// <param name="Ended">已经到点。</param>
public readonly record struct CountdownRemaining(TimeSpan Total, int Days, int Hours, int Minutes, int Seconds, bool Ended);

/// <summary>
/// 时间的写法（vue-tsx-pro 的 format.date、format.relative 与 Intl）：值换算成时刻与墙上时间、按语言写日期时间与相对时间、倒计时的拆分。
/// .NET 的文化格式与 Intl 的写法不同（"2026年10月20日 14:30"、"昨天"、"in 3 hr."），这里照 Intl 在 zh-CN、en-US 下的输出写；
/// 其他语言按英文的写法。三个平台各有一份逐字相同的副本。
/// </summary>
static class TimeText
{
    static readonly string[] ZhWeekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

    /// <summary>语言包的时区（IANA 或 Windows 的 id）；没写或找不到时是本机时区。</summary>
    public static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrEmpty(id)) return TimeZoneInfo.Local;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    /// <summary>
    /// 值换算成墙上时间：DateTimeOffset 与 UTC、本地的 DateTime 是时刻（按时区换算），未指定的 DateTime 是墙上时间，DateOnly 只有日期；
    /// 字符串按 ISO 8601（带 Z 或偏移的是时刻）。认不出时为 null。
    /// </summary>
    /// <returns>墙上时间、有没有时间、所在时区的偏移、机器可读的写法（同 TS 的 time datetime：时刻写 UTC 的 ISO，其余原样）。</returns>
    public static (DateTime Wall, bool HasTime, TimeSpan Offset, string Machine)? Resolve(object? value, TimeZoneInfo zone)
    {
        switch (value)
        {
            case DateOnly date:
                return (date.ToDateTime(TimeOnly.MinValue), false, zone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)), date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            case DateTimeOffset instant:
                return FromInstant(instant, zone);
            case DateTime { Kind: DateTimeKind.Unspecified } wall:
                return (wall, true, zone.GetUtcOffset(wall), wall.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            case DateTime time:
                return FromInstant(new DateTimeOffset(time), zone);
            case string text:
                // 字符串的机器可读写法就是它自己
                return Parse(text, zone) is { } parsed ? parsed with { Machine = text } : null;
            default:
                return null;
        }

        static (DateTime Wall, bool HasTime, TimeSpan Offset, string Machine)? Parse(string text, TimeZoneInfo zone)
        {
            if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return Resolve(date, zone);
            if (HasOffset(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant)) return FromInstant(instant, zone);
            if (text.Contains('T') && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)) return Resolve(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), zone);
            return null;
        }

        static (DateTime, bool, TimeSpan, string) FromInstant(DateTimeOffset instant, TimeZoneInfo zone)
        {
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            return (local.DateTime, true, local.Offset, instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        }

        // 时间部分（T 之后）带 Z 或 ±偏移的是时刻
        static bool HasOffset(string text) => text.IndexOf('T') is > 0 and var t && (text.EndsWith('Z') || text.LastIndexOfAny(['+', '-']) > t);
    }

    /// <summary>DateTimeText 的文字与机器可读的值：不写格式时只有日期的值显示日期，带时间的再加上时分；认不出的值为 null。</summary>
    /// <param name="value">值。</param>
    /// <param name="lang">语言包（语言与时区）。</param>
    /// <param name="date">日期的详略，默认 Medium。</param>
    /// <param name="time">时间的详略；不写时带时间的值用 Short，只有日期的值不写。</param>
    /// <param name="format">.NET 的格式字符串（TS 的 options），按语言包的文化写；写了时忽略详略。</param>
    public static (string Text, string Machine)? Describe(object? value, Lang lang, DateStyle? date, TimeStyle? time, string? format)
    {
        var zone = Zone(lang.TimeZone);
        if (Resolve(value, zone) is not { } resolved) return null;
        var text = format is not null
            ? resolved.Wall.ToString(format, Culture(lang.Code))
            : Format(resolved.Wall, resolved.Offset, zone, lang.Code, date ?? DateStyle.Medium, time ?? (resolved.HasTime ? TimeStyle.Short : null));
        return (text, resolved.Machine);
    }

    /// <summary>RelativeTime 的文字、提示（完整的日期时间）、机器可读的值与多久更新一次；认不出的值为 null。</summary>
    /// <param name="value">值。</param>
    /// <param name="now">现在。</param>
    /// <param name="lang">语言包。</param>
    /// <param name="style">详略。</param>
    /// <param name="threshold">相差超过多久就改写日期（Medium），之后不再更新；null 时一直是相对时间。</param>
    public static (string Text, string Title, string Machine, TimeSpan? Every)? DescribeRelative(object? value, DateTimeOffset now, Lang lang, RelativeTimeStyle style, TimeSpan? threshold)
    {
        var zone = Zone(lang.TimeZone);
        if (Resolve(value, zone) is not { } resolved) return null;
        var diff = new DateTimeOffset(resolved.Wall, resolved.Offset) - now;
        var absolute = threshold is { } limit && diff.Duration() > limit;
        var title = Format(resolved.Wall, resolved.Offset, zone, lang.Code, DateStyle.Full, resolved.HasTime ? TimeStyle.Short : null);
        return absolute
            ? (Format(resolved.Wall, resolved.Offset, zone, lang.Code, DateStyle.Medium, null), title, resolved.Machine, null)
            : (Relative(diff, lang.Code, style), title, resolved.Machine, RelativeEvery(diff));
    }

    /// <summary>倒计时还剩多久与多久更新一次（到秒的每秒、到分的每分钟，到点后不再更新）；认不出的目标为 null。</summary>
    public static (CountdownRemaining Remaining, TimeSpan? Every)? Remaining(object? to, DateTimeOffset now, Lang lang, CountdownPrecision precision)
    {
        if (Instant(to, Zone(lang.TimeZone)) is not { } target) return null;
        TimeSpan? every = now >= target ? null : precision == CountdownPrecision.Minute ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1);
        return (Split(target - now, precision), every);
    }

    /// <summary>倒计时的各段：天数（满一天时，按语言写"2天"，否则 null）与两位的时、分（精度到秒时还有秒）。</summary>
    public static (string? Days, string[] Clock) Segments(CountdownRemaining parts, CountdownPrecision precision, string lang)
    {
        string[] clock = precision == CountdownPrecision.Second
            ? [$"{parts.Hours:00}", $"{parts.Minutes:00}", $"{parts.Seconds:00}"]
            : [$"{parts.Hours:00}", $"{parts.Minutes:00}"];
        return (parts.Days > 0 ? Days(parts.Days, lang) : null, clock);
    }

    /// <summary>倒计时默认的文字："01:02:03"，满一天时前面加天数："2天 01:02:03"。</summary>
    public static string CountdownText(CountdownRemaining parts, CountdownPrecision precision, string lang)
    {
        var (days, clock) = Segments(parts, precision, lang);
        var time = string.Join(":", clock);
        return days is null ? time : $"{days} {time}";
    }

    /// <summary>值对应的时刻：墙上时间按时区换算（TS 的 toInstant）；认不出时为 null。</summary>
    public static DateTimeOffset? Instant(object? value, TimeZoneInfo zone) =>
        value is DateTimeOffset instant ? instant
        : Resolve(value, zone) is { } resolved ? new DateTimeOffset(DateTime.SpecifyKind(resolved.Wall, DateTimeKind.Unspecified), resolved.Offset)
        : null;

    /// <summary>按语言写日期与时间（Intl.DateTimeFormat 的 dateStyle、timeStyle）；两个都不写时按 Medium 的日期。</summary>
    /// <param name="wall">墙上时间。</param>
    /// <param name="offset">所在时区的偏移（Long、Full 的时间写它）。</param>
    /// <param name="zone">时区（英文 Full 的时间写它的名字）。</param>
    /// <param name="lang">语言代码。</param>
    /// <param name="date">日期的详略；null 时不写日期。</param>
    /// <param name="time">时间的详略；null 时不写时间。</param>
    public static string Format(DateTime wall, TimeSpan offset, TimeZoneInfo zone, string lang, DateStyle? date, TimeStyle? time)
    {
        if (date is null && time is null) date = DateStyle.Medium;
        var zh = IsChinese(lang);
        var en = CultureInfo.GetCultureInfo("en-US");
        var datePart = date switch
        {
            null => null,
            DateStyle.Short => zh ? $"{wall.Year}/{wall.Month}/{wall.Day}" : wall.ToString("M/d/yy", en),
            DateStyle.Medium => zh ? $"{wall.Year}年{wall.Month}月{wall.Day}日" : wall.ToString("MMM d, yyyy", en),
            DateStyle.Long => zh ? $"{wall.Year}年{wall.Month}月{wall.Day}日" : wall.ToString("MMMM d, yyyy", en),
            _ => zh ? $"{wall.Year}年{wall.Month}月{wall.Day}日{ZhWeekdays[(int)wall.DayOfWeek]}" : wall.ToString("dddd, MMMM d, yyyy", en),
        };
        var gmt = Gmt(offset);
        var timePart = time switch
        {
            null => null,
            TimeStyle.Short => zh ? wall.ToString("HH:mm", en) : wall.ToString("h:mm tt", en),
            TimeStyle.Medium => zh ? wall.ToString("HH:mm:ss", en) : wall.ToString("h:mm:ss tt", en),
            // 中文的时区写在前面；英文 Full 写时区的名字（中文没有可靠的本地化名字，同 Long）
            TimeStyle.Long => zh ? $"{gmt} {wall.ToString("HH:mm:ss", en)}" : $"{wall.ToString("h:mm:ss tt", en)} {gmt}",
            _ => zh ? $"{gmt} {wall.ToString("HH:mm:ss", en)}" : $"{wall.ToString("h:mm:ss tt", en)} {zone.StandardName}",
        };
        if (datePart is null) return timePart!;
        if (timePart is null) return datePart;
        // 英文：带月份全称的日期与时间之间是 "at"，其余是逗号
        return zh ? $"{datePart} {timePart}" : date is DateStyle.Full or DateStyle.Long ? $"{datePart} at {timePart}" : $"{datePart}, {timePart}";
    }

    /// <summary>时区偏移的写法（Intl 的 GMT+8、GMT+5:30，零是 GMT）。</summary>
    static string Gmt(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "GMT";
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0 ? $"GMT{sign}{abs.Hours}" : $"GMT{sign}{abs.Hours}:{abs.Minutes:00}";
    }

    /// <summary>相差的时长 → 相对时间的数值与单位（45 秒以内用秒、45 分钟以内用分、22 小时以内用时、26 天以内用天、320 天以内用月，参考 moment.js）。</summary>
    public static (int Value, string Unit) RelativeUnit(TimeSpan diff)
    {
        var seconds = diff.TotalSeconds;
        var (unit, size) = Math.Abs(seconds) switch
        {
            < 45 => ("second", 1d),
            < 45 * 60 => ("minute", 60d),
            < 22 * 3600 => ("hour", 3600d),
            < 26 * 86_400 => ("day", 86_400d),
            < 320 * 86_400 => ("month", 30 * 86_400d),
            _ => ("year", 365 * 86_400d),
        };
        // 同 JS 的 Math.round：.5 往正方向取
        return ((int)Math.Floor(seconds / size + 0.5), unit);
    }

    /// <summary>相对现在的写法（Intl.RelativeTimeFormat，numeric: auto：昨天、明天、去年，而不是 1 天前）。</summary>
    /// <param name="diff">值减去现在（过去是负的）。</param>
    /// <param name="lang">语言代码。</param>
    /// <param name="style">详略。</param>
    public static string Relative(TimeSpan diff, string lang, RelativeTimeStyle style)
    {
        var (value, unit) = RelativeUnit(diff);
        return IsChinese(lang) ? Chinese(value, unit, style) : English(value, unit, style);
    }

    static string Chinese(int value, string unit, RelativeTimeStyle style)
    {
        var word = (unit, value) switch
        {
            ("second", 0) => "现在",
            ("day", 0) => "今天",
            ("day", -1) => "昨天",
            ("day", 1) => "明天",
            ("day", -2) => "前天",
            ("day", 2) => "后天",
            ("month", 0) => "本月",
            ("month", -1) => "上个月",
            ("month", 1) => "下个月",
            ("year", 0) => "今年",
            ("year", -1) => "去年",
            ("year", 1) => "明年",
            _ => null,
        };
        if (word is not null) return word;
        var name = unit switch
        {
            "second" => style == RelativeTimeStyle.Long ? "秒钟" : "秒",
            "minute" => "分钟",
            "hour" => "小时",
            "day" => "天",
            "month" => "个月",
            _ => "年",
        };
        return $"{Math.Abs(value)}{name}{(value < 0 ? "前" : "后")}";
    }

    static string English(int value, string unit, RelativeTimeStyle style)
    {
        var abbreviated = style != RelativeTimeStyle.Long;
        var word = (unit, value) switch
        {
            ("second", 0) => "now",
            ("day", 0) => "today",
            ("day", -1) => "yesterday",
            ("day", 1) => "tomorrow",
            ("month", 0) => abbreviated ? "this mo." : "this month",
            ("month", -1) => abbreviated ? "last mo." : "last month",
            ("month", 1) => abbreviated ? "next mo." : "next month",
            ("year", 0) => abbreviated ? "this yr." : "this year",
            ("year", -1) => abbreviated ? "last yr." : "last year",
            ("year", 1) => abbreviated ? "next yr." : "next year",
            _ => null,
        };
        if (word is not null) return word;
        var count = Math.Abs(value);
        var amount = style switch
        {
            RelativeTimeStyle.Narrow => unit switch
            {
                "second" => $"{count}s",
                "minute" => $"{count}m",
                "hour" => $"{count}h",
                "day" => $"{count}d",
                "month" => $"{count}mo",
                _ => $"{count}y",
            },
            RelativeTimeStyle.Short => unit switch
            {
                "second" => $"{count} sec.",
                "minute" => $"{count} min.",
                "hour" => $"{count} hr.",
                "day" => count == 1 ? "1 day" : $"{count} days",
                "month" => $"{count} mo.",
                _ => $"{count} yr.",
            },
            _ => count == 1 ? $"1 {unit}" : $"{count} {unit}s",
        };
        return value < 0 ? $"{amount} ago" : $"in {amount}";
    }

    /// <summary>天数的写法（Intl 的 unit: day、unitDisplay: long）：2天、2 days。</summary>
    public static string Days(int days, string lang) => IsChinese(lang) ? $"{days}天" : days == 1 ? "1 day" : $"{days} days";

    /// <summary>剩余的时长 → 天、时、分、秒（按精度向上取整：还剩 0.4 秒时显示 1 秒，到点才是 0）。</summary>
    public static CountdownRemaining Split(TimeSpan remaining, CountdownPrecision precision)
    {
        var total = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        var unit = precision == CountdownPrecision.Minute ? 60_000d : 1000d;
        var seconds = (long)(Math.Ceiling(total.TotalMilliseconds / unit) * unit / 1000);
        return new CountdownRemaining(total, (int)(seconds / 86_400), (int)(seconds / 3600 % 24), (int)(seconds / 60 % 60), (int)(seconds % 60), remaining <= TimeSpan.Zero);
    }

    /// <summary>相对时间多久更新一次（随单位变）：秒级每秒、分钟级每 10 秒、小时级每分钟、更远的每小时。</summary>
    static TimeSpan RelativeEvery(TimeSpan diff) => RelativeUnit(diff).Unit switch
    {
        "second" => TimeSpan.FromSeconds(1),
        "minute" => TimeSpan.FromSeconds(10),
        "hour" => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromHours(1),
    };

    static bool IsChinese(string lang) => lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>语言代码对应的文化（Format 的格式字符串用）；没有这个文化时用不变文化。</summary>
    static CultureInfo Culture(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
