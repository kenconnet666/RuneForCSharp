using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日历上的文字（上游用 Intl 按语言生成）：标题（2026年10月 / October 2026）、读屏念的完整日期（2026年10月20日星期二）、星期表头、月份与年份格。
/// .NET 的区域格式（ICU）与 Intl 大体一致：最短的星期名就是 Intl 的 narrow（S、M / 一、二 / ح、ن）；完整日期没有星期时补在后面。
/// </summary>
public static class CalendarText
{
    static bool EastAsian(CultureInfo culture) => culture.TwoLetterISOLanguageName is "zh" or "ja" or "ko";

    /// <summary>一页的标题：日视图是年月，月视图是年，年视图是十年的两端。</summary>
    /// <param name="page">这一页（日视图是那个月的 1 日）。</param>
    /// <param name="view">视图。</param>
    /// <param name="culture">语言。</param>
    /// <param name="lang">语言包（年份的写法）。</param>
    public static string Title(DateOnly page, DateUnit view, CultureInfo culture, Lang lang) => view switch
    {
        DateUnit.Day => YearMonth(page, culture),
        DateUnit.Month => Year(page.Year, culture, lang),
        _ => $"{Year(page.Year / 10 * 10, culture, lang)} – {Year(page.Year / 10 * 10 + 9, culture, lang)}",
    };

    /// <summary>年月：区域的年月格式（zh-CN 2026年10月、en-US October 2026）。</summary>
    public static string YearMonth(DateOnly date, CultureInfo culture) => date.ToString(culture.DateTimeFormat.YearMonthPattern, culture);

    /// <summary>年份：中日韩语言按语言包（2026年），其余是数字。</summary>
    public static string Year(int year, CultureInfo culture, Lang lang) =>
        EastAsian(culture) ? lang.CalendarYear(year) : year.ToString(CultureInfo.InvariantCulture);

    /// <summary>读屏念的完整日期（Intl 的 dateStyle: full）：长日期格式，没有星期时补上（2026年10月20日星期二、Tuesday, October 20, 2026）。</summary>
    public static string FullDate(DateOnly date, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.LongDatePattern;
        if (pattern.Contains("dddd", StringComparison.Ordinal)) return date.ToString(pattern, culture);
        return date.ToString(pattern, culture) + (EastAsian(culture) ? "" : ", ") + culture.DateTimeFormat.GetDayName(date.DayOfWeek);
    }

    /// <summary>星期表头：从一周的第一天起七天，最短的写法（一、二 / M、T）；long 时是全称，给悬停提示。</summary>
    public static IReadOnlyList<string> WeekdayNames(CultureInfo culture, DayOfWeek first, bool full = false) =>
        [.. Enumerable.Range(0, 7).Select(offset => (DayOfWeek)(((int)first + offset) % 7)).Select(day => full
            ? culture.DateTimeFormat.GetDayName(day)
            : culture.DateTimeFormat.GetShortestDayName(day))];

    /// <summary>月份格里的月名（10月 / Oct）。</summary>
    public static string MonthShort(DateOnly month, CultureInfo culture) => culture.DateTimeFormat.GetAbbreviatedMonthName(month.Month);

    /// <summary>日期格里的数字。</summary>
    public static string DayNumber(DateOnly date) => date.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>快捷区间的名字（语言包的 DateRangePreset，键是上游的 camelCase 名：last7Days）。</summary>
    public static string PresetName(DateRangePreset preset, Lang lang)
    {
        var name = preset.ToString();
        return lang.DateRangePreset[char.ToLowerInvariant(name[0]) + name[1..]];
    }

    /// <summary>语言包的代码对应的区域（读不出时用当前界面文化）。</summary>
    public static CultureInfo CultureOf(Lang lang)
    {
        try
        {
            return CultureInfo.GetCultureInfo(lang.Code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
    }

    /// <summary>语言包给的一周第一天（0 是周日），没写时按区域惯例（zh-CN 周一，en-US 周日）。</summary>
    public static DayOfWeek FirstDayOf(Lang lang, DayOfWeek? own = null) =>
        own ?? (lang.FirstDayOfWeek is { } day ? (DayOfWeek)(((int)day % 7 + 7) % 7) : CultureOf(lang).DateTimeFormat.FirstDayOfWeek);
}
