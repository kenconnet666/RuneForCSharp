namespace Rune.Avalonia;

/// <summary>NativeSelect 的选项按组排（同上游的 grouped）：没有组的在前，各组按第一次出现的顺序；组名是一行不能选的标题。</summary>
static class NativeSelectGroups
{
    /// <summary>选项：ListboxOption 或字符串（值与文字相同）。</summary>
    /// <param name="items">选项。</param>
    public static List<ListboxOption> Options(IEnumerable<object?>? items) => [.. (items ?? []).Select(item => ListboxOption.From(item, null))];

    /// <summary>按组排好：(组名, 这一组的选项)，没有组的那一段组名为 null、排在最前。</summary>
    /// <param name="options">选项。</param>
    public static List<(string? Group, List<ListboxOption> Options)> Grouped(IEnumerable<ListboxOption> options)
    {
        var groups = new List<(string? Group, List<ListboxOption> Options)>();
        foreach (var option in options)
        {
            var index = groups.FindIndex(group => group.Group == option.Group);
            if (index < 0)
            {
                // 没有组的那一段放在最前
                index = option.Group is null ? 0 : groups.Count;
                groups.Insert(index, (option.Group, []));
            }
            groups[index].Options.Add(option);
        }
        return groups;
    }
}
