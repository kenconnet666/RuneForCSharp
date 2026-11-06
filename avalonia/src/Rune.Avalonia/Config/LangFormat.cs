using System.Globalization;

namespace Rune.Avalonia;

/// <summary>语言包里的数字格式：对应 vue-tsx-pro 文案里的 <c>toLocaleString(语言代码)</c>（分页的总条数等）。</summary>
public static class LangFormat
{
    /// <summary>按语言代码格式化数字：千分位、最多三位小数（同 JS 的 toLocaleString 默认）；没有这个文化时用不变文化。</summary>
    /// <param name="value">数字。</param>
    /// <param name="code">BCP 47 语言代码，如 zh-CN。</param>
    public static string Number(double value, string code)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }
        return value.ToString("#,0.###", culture);
    }
}
