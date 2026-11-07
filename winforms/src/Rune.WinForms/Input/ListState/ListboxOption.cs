namespace Rune.WinForms;

/// <summary>
/// 列表里的一个选项（vue-tsx-pro 的 ListboxOption）：复选组、单选组、Listbox、Select 的选项。
/// ItemsSource 里是 ListboxOption、字符串（值与文字都是它）时直接用；任意数据时给 ToOption 取出值、文字与说明。
/// </summary>
/// <param name="Value">值：选中后写回的就是它，同一列表里不能重复。</param>
/// <param name="Label">显示的文字。</param>
public sealed record ListboxOption(string Value, string Label)
{
    /// <summary>文字下方的说明。</summary>
    public string? Description { get; init; }

    /// <summary>不可用：能被方向键经过（读屏听得到），不能选中。</summary>
    public bool IsDisabled { get; init; }

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get; init; }

    /// <summary>分组名：同名的选项排在一组，组名显示在组的第一行。</summary>
    public string? Group { get; init; }

    /// <summary>一项数据的选项：给了 toOption 用它；没给时数据本身是 ListboxOption 或字符串。</summary>
    /// <param name="item">数据。</param>
    /// <param name="toOption">数据 → 选项。</param>
    internal static ListboxOption From(object? item, Func<object?, ListboxOption>? toOption) => toOption is not null ? toOption(item) : item switch
    {
        ListboxOption option => option,
        string text => new ListboxOption(text, text),
        _ => throw new InvalidOperationException($"选项 {item?.GetType().Name ?? "null"} 不是 ListboxOption 或字符串：给组设 ToOption 取出值与文字。"),
    };
}
