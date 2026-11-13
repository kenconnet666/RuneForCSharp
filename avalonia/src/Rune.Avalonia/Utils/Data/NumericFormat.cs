using System.Globalization;
using System.Text;

namespace Rune.Avalonia;

/// <summary>数字的样式（Intl.NumberFormat 的 style）。</summary>
public enum NumericStyle
{
    /// <summary>普通数字（默认）。</summary>
    Decimal,

    /// <summary>百分比：0.123 显示成 12%。</summary>
    Percent,

    /// <summary>金额：币种不写时取语言包的（Lang.Currency）。</summary>
    Currency,
}

/// <summary>正负号怎样显示（Intl.NumberFormat 的 signDisplay）。</summary>
public enum NumericSign
{
    /// <summary>只有负数带"-"（默认）。</summary>
    Auto,

    /// <summary>正负都带符号，0 是"+0"。</summary>
    Always,

    /// <summary>正负都带符号，0 不带（环比、涨跌）。</summary>
    ExceptZero,

    /// <summary>都不带符号。</summary>
    Never,

    /// <summary>只有负数带"-"，取舍后是 0 的不带。</summary>
    Negative,
}

/// <summary>
/// 数字的显示格式（vue-tsx-pro 的 NumericFormat：Intl.NumberFormat 的选项加上缩放与单位文字）：AnimatedNumber、Stat 等组件的数字格式属性。
/// <code>
/// new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }            // 0.123 → 12.3%
/// new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2, Suffix = "万元" }         // 155353.2 → 15.54 万元
/// new NumericFormat { Style = NumericStyle.Currency }                                       // 1234.5 → ¥1,234.50（币种取语言包）
/// </code>
/// 值是 decimal：十进制精确，金额、大整数不经过浮点数。取舍同 Intl（四舍五入远离 0）；小数位数的默认值同 Intl：
/// 普通数字 0–3 位，百分比 0 位，金额按币种（大多 2 位，日元、韩元 0 位）。符号同 Intl：百分号紧跟数字，币种符号在前，负号在最前面。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
public sealed record NumericFormat
{
    /// <summary>样式，默认普通数字。</summary>
    public NumericStyle Style { get; init; }

    /// <summary>币种（ISO 4217，如 CNY、USD）；不写时取语言包的。</summary>
    public string? Currency { get; init; }

    /// <summary>最少几位小数（不够补 0）。</summary>
    public int? MinimumFractionDigits { get; init; }

    /// <summary>最多几位小数（多的四舍五入）。</summary>
    public int? MaximumFractionDigits { get; init; }

    /// <summary>正负号，默认只有负数带。</summary>
    public NumericSign SignDisplay { get; init; }

    /// <summary>千位分隔符，默认有。</summary>
    public bool UseGrouping { get; init; } = true;

    /// <summary>先除以它再显示：10000 表示以"万"为单位（155353.2 → 15.53532，再按小数位取舍）。</summary>
    public decimal? Scale { get; init; }

    /// <summary>跟在数字后面的单位文字，如"万元"；与数字之间是不换行的空格。</summary>
    public string? Suffix { get; init; }

    /// <summary>默认格式：普通数字，0–3 位小数。</summary>
    public static NumericFormat Default { get; } = new();

    /// <summary>不写币种时小数位是 0 的币种。</summary>
    static readonly HashSet<string> WholeCurrencies = ["JPY", "KRW", "VND", "CLP", "ISK", "HUF", "TWD", "IDR"];

    /// <summary>不是这个地区自己的币种时显示的符号（同 Intl：US$、CN¥）；不在表里的显示币种代码。</summary>
    static readonly Dictionary<string, string> ForeignSymbols = new()
    {
        ["USD"] = "US$", ["CNY"] = "CN¥", ["JPY"] = "JP¥", ["EUR"] = "€", ["GBP"] = "£", ["HKD"] = "HK$", ["KRW"] = "₩", ["TWD"] = "NT$",
    };

    /// <summary>按语言格式化。</summary>
    /// <param name="value">数值。</param>
    /// <param name="cultureCode">语言（Lang.Code，如 zh-CN）。</param>
    /// <param name="defaultCurrency">不写币种时的币种（Lang.Currency）。</param>
    public string Format(decimal value, string cultureCode, string defaultCurrency)
    {
        var culture = CultureInfo.GetCultureInfo(cultureCode);
        var currency = Style == NumericStyle.Currency ? (Currency ?? defaultCurrency).ToUpperInvariant() : null;
        var (min, max) = FractionDigits(currency);
        var number = Scale is { } scale && scale != 0 && scale != 1 ? value / scale : value;
        if (Style == NumericStyle.Percent) number *= 100;
        var rounded = Math.Round(number, max, MidpointRounding.AwayFromZero);
        var digits = Math.Abs(rounded).ToString(Pattern(min, max), culture);
        var body = Style switch
        {
            NumericStyle.Percent => digits + "%",
            NumericStyle.Currency => CurrencySymbol(currency!, culture) + digits,
            _ => digits,
        };
        var sign = SignDisplay switch
        {
            NumericSign.Never => "",
            NumericSign.Always => rounded < 0 ? culture.NumberFormat.NegativeSign : "+",
            NumericSign.ExceptZero => rounded < 0 ? culture.NumberFormat.NegativeSign : rounded > 0 ? "+" : "",
            NumericSign.Negative => rounded < 0 ? culture.NumberFormat.NegativeSign : "",
            _ => rounded < 0 ? culture.NumberFormat.NegativeSign : "",
        };
        var text = sign + body;
        return string.IsNullOrEmpty(Suffix) ? text : $"{text} {Suffix}";
    }

    /// <summary>小数位数：写了的为准；只写了一头时另一头按 Intl 的默认值收拢。</summary>
    (int Min, int Max) FractionDigits(string? currency)
    {
        var (defaultMin, defaultMax) = Style switch
        {
            NumericStyle.Percent => (0, 0),
            NumericStyle.Currency => currency is not null && WholeCurrencies.Contains(currency) ? (0, 0) : (2, 2),
            _ => (0, 3),
        };
        var min = Math.Clamp(MinimumFractionDigits ?? Math.Min(defaultMin, MaximumFractionDigits ?? defaultMin), 0, 20);
        var max = Math.Clamp(MaximumFractionDigits ?? Math.Max(defaultMax, min), min, 20);
        return (min, max);
    }

    string Pattern(int min, int max)
    {
        var pattern = new StringBuilder(UseGrouping ? "#,0" : "0");
        if (max > 0) pattern.Append('.').Append('0', min).Append('#', max - min);
        return pattern.ToString();
    }

    static string CurrencySymbol(string currency, CultureInfo culture)
    {
        var local = culture.IsNeutralCulture ? null : new RegionInfo(culture.Name).ISOCurrencySymbol;
        if (local == currency) return culture.NumberFormat.CurrencySymbol;
        return ForeignSymbols.TryGetValue(currency, out var symbol) ? symbol : currency + " ";
    }
}
