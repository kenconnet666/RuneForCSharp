using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Rune.WinForms;

/// <summary>creatable 时列表末尾那一项"新建"（不是 ItemsSource 里的数据）：选中后值就是输入的文字。</summary>
/// <param name="Text">输入的文字。</param>
public sealed record SelectCreation(string Text);

/// <summary>
/// 下拉选择弹出层里显示哪些选项（vue-tsx-pro 的 Select 的过滤与 creatable）：有搜索词时按显示文字匹配（忽略大小写与重音，同 useTextMatcher）
/// 或按自定义的过滤；creatable 时输入的文字没有同值、同名（忽略大小写）的选项就在末尾加一项 <see cref="SelectCreation"/>。
/// </summary>
public static class SelectSearch
{
    /// <summary>显示文字包含搜索词：忽略大小写与重音（é 当 e）。</summary>
    /// <param name="label">显示的文字。</param>
    /// <param name="query">搜索词。</param>
    public static bool Matches(string label, string query) => Fold(label).Contains(Fold(query), StringComparison.Ordinal);

    /// <summary>
    /// 弹出层里的数据：搜索词不空且在本地过滤时只留匹配的（给了 <paramref name="filter"/> 用它）；creatable 时可能在末尾多一项"新建"。
    /// </summary>
    /// <param name="items">全部数据。</param>
    /// <param name="toOption">数据 → 选项。</param>
    /// <param name="query">搜索词。</param>
    /// <param name="localFilter">在本地过滤（服务端按搜索词过滤时为 false）。</param>
    /// <param name="filter">自定义的匹配；null 时按显示文字。</param>
    /// <param name="creatable">可以新建。</param>
    public static IReadOnlyList<object?> Shown(IReadOnlyList<object?> items, Func<object?, ListboxOption> toOption, string query,
        bool localFilter, Func<object?, string, bool>? filter, bool creatable)
    {
        var shown = query.Length > 0 && localFilter
            ? [.. items.Where(item => filter?.Invoke(item, query) ?? Matches(toOption(item).Label, query))]
            : items.ToList();
        var text = query.Trim();
        if (creatable && text.Length > 0 && !items.Select(toOption).Any(option =>
                option.Value == text || string.Equals(option.Label, text, StringComparison.OrdinalIgnoreCase)))
            shown.Add(new SelectCreation(text));
        return shown;
    }

    static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }
}

/// <summary>
/// 多选时触发框里最多显示几个标签（vue-tsx-pro 的 maxTagCount）：一个数，其余收进"+N"；或 <see cref="Responsive"/>：排成一行、按宽度放，放不下的收起。
/// XAML 里写数字或 responsive。
/// </summary>
[TypeConverter(typeof(TagLimitConverter))]
public readonly record struct TagLimit
{
    TagLimit(int? count) => Count = count;

    /// <summary>最多几个；null 是按宽度（<see cref="Responsive"/>）。</summary>
    public int? Count { get; }

    /// <summary>按宽度放：一行，放不下的收进"+N"（至少显示一个）。</summary>
    public static TagLimit Responsive { get; } = new(null);

    /// <summary>最多显示 <paramref name="count"/> 个。</summary>
    /// <param name="count">个数（小于 0 按 0）。</param>
    public static TagLimit Of(int count) => new(Math.Max(0, count));

    /// <summary>数字即个数。</summary>
    /// <param name="count">个数。</param>
    public static implicit operator TagLimit(int count) => Of(count);

    /// <inheritdoc />
    public override string ToString() => Count?.ToString(CultureInfo.InvariantCulture) ?? "responsive";
}

/// <summary>XAML、设计器里写的 "responsive" 或数字 → <see cref="TagLimit"/>。</summary>
public sealed class TagLimitConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        string text when text.Trim().Equals("responsive", StringComparison.OrdinalIgnoreCase) => TagLimit.Responsive,
        string text when int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) => TagLimit.Of(count),
        string text => throw new FormatException($"maxTagCount 是数字或 responsive，不是 \"{text}\"。"),
        _ => base.ConvertFrom(context, culture, value),
    };
}
