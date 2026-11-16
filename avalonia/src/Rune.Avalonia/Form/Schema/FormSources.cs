namespace Rune.Avalonia;

/// <summary>一个选项来源（vue-tsx-pro 的 OptionSource）：固定的列表（字典），或按搜索词远程取（供应商、件号）。</summary>
public sealed record OptionSource
{
    /// <summary>固定的列表。</summary>
    public IReadOnlyList<SchemaOption>? Items { get; init; }

    /// <summary>按搜索词远程取：搜索词、取消。</summary>
    public Func<string, CancellationToken, Task<IReadOnlyList<SchemaOption>>>? Search { get; init; }

    /// <summary>固定的列表。</summary>
    public static implicit operator OptionSource(SchemaOption[] items) => new() { Items = items };
}

/// <summary>
/// 表单的数据来源（vue-tsx-pro 的 FormSources，宿主登记、按名字引用）：选择题的选项（字典、远程搜索）、人员与部门、上传。
/// 在 <see cref="Provider.FormSourcesProperty"/> 登记一次，SchemaForm、SchemaView 的 Sources 可以局部覆盖（<see cref="Merge"/>）。
/// </summary>
public sealed record FormSources
{
    /// <summary>选项来源：名字 → 列表或远程搜索；字段的 SchemaField 特性写 Source = "名字"。</summary>
    public IReadOnlyDictionary<string, OptionSource> Options { get; init; } = new Dictionary<string, OptionSource>();

    /// <summary>人员、部门（SchemaControl.People）。</summary>
    public PeopleSource? People { get; init; }

    /// <summary>上传（SchemaControl.File）。</summary>
    public UploadHandler? Upload { get; init; }

    static readonly Dictionary<string, OptionSource> Empty = [];

    /// <summary>上面的盖过下面的：选项按名字合并，人员、上传有写的用上面的。</summary>
    public static FormSources Merge(FormSources? below, FormSources? above) => new()
    {
        Options = (below?.Options ?? Empty).Concat(above?.Options ?? Empty).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value),
        People = above?.People ?? below?.People,
        Upload = above?.Upload ?? below?.Upload,
    };

    /// <summary>选项的文字：字段的固定选项或登记的字典里找，找不到时是值本身。</summary>
    public string OptionText(SchemaField field, object? value)
    {
        var text = value switch
        {
            null => "",
            Enum member => member.ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        };
        var options = (field.Source is { } name && Options.TryGetValue(name, out var source) ? source.Items : null) ?? field.Options ?? [];
        return options.FirstOrDefault(option => option.Value == text)?.Label ?? text;
    }
}
