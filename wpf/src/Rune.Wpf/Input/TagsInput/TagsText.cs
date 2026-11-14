namespace Rune.Wpf;

/// <summary>标签输入的文字处理（vue-tsx-pro 的 TagsInput；三个库共用一份）：按分隔符拆开、文字换成值、加入时去重与限个数。</summary>
public static class TagsText
{
    /// <summary>默认的分隔符：英文、中文的逗号与分号。</summary>
    public static IReadOnlyList<string> DefaultDelimiters { get; } = [",", "，", ";", "；"];

    /// <summary>按分隔符拆开；换行、制表符总是分隔（回车、粘贴多行）。只有一段时返回原文一段。</summary>
    public static string[] Split(string text, IReadOnlyList<string> delimiters) =>
        text.Split([.. delimiters.Where(delimiter => delimiter.Length > 0), "\r\n", "\n", "\r", "\t"], StringSplitOptions.None);

    /// <summary>
    /// 一段文字换成值：去掉首尾空白；是某个建议的值或文字时取建议的值，否则允许自定义时原样，不允许时没有（null）。空白也没有。
    /// </summary>
    /// <param name="text">输入的文字。</param>
    /// <param name="options">建议（值、文字）。</param>
    /// <param name="allowCustomValue">允许不在建议里的文字。</param>
    public static string? Resolve(string text, IEnumerable<ListboxOption> options, bool allowCustomValue)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;
        foreach (var option in options)
            if (option.Value == trimmed || option.Label == trimmed) return option.Value;
        return allowCustomValue ? trimmed : null;
    }

    /// <summary>加入几个值：没有的（null）、重复的、超出上限的不加。返回加入后的列表；一个都没加时返回 null。</summary>
    /// <param name="current">已有的值。</param>
    /// <param name="values">要加入的值（<see cref="Resolve"/> 的结果）。</param>
    /// <param name="max">最多几个；null 时不限。</param>
    public static List<string>? Add(IReadOnlyList<string> current, IEnumerable<string?> values, int? max)
    {
        var next = new List<string>(current);
        foreach (var value in values)
        {
            if (value is null || next.Contains(value) || next.Count >= max) continue;
            next.Add(value);
        }
        return next.Count == current.Count ? null : next;
    }

    /// <summary>从建议里选的新值：多于上限的加入不算（取消选中照常）。返回 false 时保持原样。</summary>
    /// <param name="current">已有的值。</param>
    /// <param name="next">列表选出来的值。</param>
    /// <param name="max">最多几个。</param>
    public static bool Accepts(IReadOnlyList<string> current, IReadOnlyList<string> next, int? max) =>
        !(next.Count > max && next.Count > current.Count);
}
