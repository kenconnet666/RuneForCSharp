using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rune.Wpf;

/// <summary>
/// 数字输入框的纯计算（vue-tsx-pro 的 normalizeNumber、snapNumeric 与 NumberField 里的缩放）：解析用户输入、对齐步长、编辑时的单位。
/// 值是 decimal：十进制精确，金额、大整数、长小数不经过浮点数（上游的 number、bigint、Decimal 三种在这里都是它）。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
public static partial class NumberRules
{
    /// <summary>
    /// 按语言解析用户输入：去掉空白与千分位，认语言的小数点与负号，也认中文输入法打出的全角数字、全角句点与负号。
    /// 空字符串是 true、值为 null；解析不了（或超出 decimal 的范围）是 false。
    /// </summary>
    /// <param name="text">输入框里的文字。</param>
    /// <param name="cultureCode">语言（Lang.Code，如 zh-CN）。</param>
    /// <param name="value">解析出的值；空时为 null。</param>
    public static bool TryParse(string text, string cultureCode, out decimal? value)
    {
        value = null;
        var format = CultureInfo.GetCultureInfo(cultureCode).NumberFormat;
        var normalized = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c)) continue;
            normalized.Append(c switch
            {
                >= '０' and <= '９' => (char)(c - '０' + '0'),
                '．' or '｡' => '.',
                '－' or '−' => '-',
                _ => c,
            });
        }
        var plain = normalized.ToString();
        if (plain.Length == 0) return true;
        var group = format.NumberGroupSeparator;
        var point = format.NumberDecimalSeparator;
        // 有些语言的千分位是不换行空格：上面已经当空白去掉了
        if (group.Length > 0 && group != point && !string.IsNullOrWhiteSpace(group)) plain = plain.Replace(group, "");
        if (point != ".") plain = plain.Replace(point, ".");
        if (format.NegativeSign != "-") plain = plain.Replace(format.NegativeSign, "-");
        if (!Plain().IsMatch(plain)) return false;
        if (!decimal.TryParse(plain, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)) return false;
        value = parsed;
        return true;
    }

    /// <summary>对齐到 min + n × step（四舍五入远离 0），再夹到 min–max。步长不大于 0 时只夹范围。</summary>
    public static decimal Snap(decimal value, decimal step, decimal? min, decimal? max)
    {
        var snapped = value;
        if (step > 0)
        {
            var origin = min ?? 0;
            snapped = origin + Math.Round((value - origin) / step, MidpointRounding.AwayFromZero) * step;
        }
        if (min is { } low && snapped < low) snapped = low;
        if (max is { } high && snapped > high) snapped = high;
        return snapped;
    }

    /// <summary>编辑时的单位：写了 Scale 按它（值是元、输入的是万元）；百分比按百分数输入（显示 85%，输入的也是 85）；否则是 1。</summary>
    public static decimal EditScale(NumericFormat? format) =>
        format?.Scale is { } scale and not 0 ? scale : format?.Style == NumericStyle.Percent ? 0.01m : 1;

    /// <summary>编辑时的文字：按编辑单位换算，不带千分位、币种与百分号，用语言的小数点；空值是空字符串。</summary>
    public static string EditText(decimal? value, NumericFormat? format, string cultureCode) =>
        value is { } number ? (number / EditScale(format)).ToString("0.############################", CultureInfo.GetCultureInfo(cultureCode)) : "";

    /// <summary>编辑单位下输入的数换回值本身的单位。</summary>
    public static decimal FromEdit(decimal typed, NumericFormat? format) => typed * EditScale(format);

    [GeneratedRegex(@"^-?(\d+\.?\d*|\.\d+)$")]
    private static partial Regex Plain();
}
