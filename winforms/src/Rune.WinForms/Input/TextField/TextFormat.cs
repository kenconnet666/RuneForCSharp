namespace Rune.WinForms;

/// <summary>
/// 输入时自动分组显示的格式（vue-tsx-pro 的 text-format）：值是去掉分隔的原文（13800001111），输入框里显示分好组的文字（138 0000 1111），交给 TextField 的 Format。
/// 手机号、身份证号、银行卡号有现成的（<see cref="Phone"/>、<see cref="IdCard"/>、<see cref="BankCard"/>），别的用 <see cref="Grouped"/> 写。
/// 只按位置分组、过滤字符，不校验内容（号段、校验位）：校验写在表单里。TS 的 inputmode（手机上弹出的键盘）桌面上没有。
/// </summary>
/// <param name="display">原文 → 显示的文字。</param>
/// <param name="parse">输入框里的文字 → 原文（去掉分隔、过滤掉不接受的字符、截到最长）。</param>
public sealed class TextFormat(Func<string, string> display, Func<string, string> parse)
{
    /// <summary>中国大陆手机号：3-4-4，11 位数字。</summary>
    public static TextFormat Phone { get; } = Grouped([3, 4, 4]);

    /// <summary>居民身份证号：6-8-4（地区、出生日期、顺序码与校验位），最后一位可以是 X（自动转大写）。</summary>
    public static TextFormat IdCard { get; } = Grouped([6, 8, 4], accept: c => char.IsAsciiDigit(c) || c is 'X' or 'x', uppercase: true);

    /// <summary>银行卡号：每 4 位一组，最长 19 位。</summary>
    public static TextFormat BankCard { get; } = Grouped([4], maxLength: 19);

    /// <summary>原文 → 显示的文字。</summary>
    /// <param name="raw">原文。</param>
    public string Display(string raw) => display(raw);

    /// <summary>输入框里的文字 → 原文。</summary>
    /// <param name="text">输入框里的文字。</param>
    public string Parse(string text) => parse(text);

    /// <summary>
    /// 按位置分组的格式（TS 的 createGroupedFormat）。
    /// </summary>
    /// <param name="groups">各组的长度：[3, 4, 4] 是 3-4-4；最后一组之后的字符再按最后一组的长度接着分。</param>
    /// <param name="accept">接受的字符，默认数字。</param>
    /// <param name="maxLength">原文最长几位；默认是各组加起来。</param>
    /// <param name="separator">组之间的分隔，默认一个空格。</param>
    /// <param name="uppercase">字母转成大写（身份证的 X）。</param>
    public static TextFormat Grouped(IReadOnlyList<int> groups, Func<char, bool>? accept = null, int? maxLength = null, string separator = " ", bool uppercase = false)
    {
        if (groups.Count == 0 || groups.Any(size => size <= 0)) throw new ArgumentException("每组至少 1 位", nameof(groups));
        accept ??= char.IsAsciiDigit;
        var limit = maxLength ?? groups.Sum();
        return new TextFormat(
            raw =>
            {
                var parts = new List<string>();
                for (var (index, rest) = (0, raw); rest.Length > 0; index++)
                {
                    var size = Math.Min(groups[Math.Min(index, groups.Count - 1)], rest.Length);
                    parts.Add(rest[..size]);
                    rest = rest[size..];
                }
                return string.Join(separator, parts);
            },
            text =>
            {
                var kept = (uppercase ? text.ToUpperInvariant() : text).Where(accept).Take(limit);
                return string.Concat(kept);
            });
    }

    /// <summary>
    /// 原文里第 <paramref name="count"/> 个字符之后，对应显示文字里的位置：重新格式化之后把光标放回原来那个字符的后面
    /// （在 1380|0001111 打一个字，光标停在新字符后面，而不是跳到最后）。
    /// </summary>
    /// <param name="raw">原文。</param>
    /// <param name="count">光标前有几个原文的字符。</param>
    public int DisplayPosition(string raw, int count)
    {
        if (count <= 0) return 0;
        var shown = Display(raw);
        var seen = 0;
        for (var index = 0; index < shown.Length; index++)
        {
            if (Parse(shown[index].ToString()).Length > 0) seen++;
            if (seen == count) return index + 1;
        }
        return shown.Length;
    }
}
