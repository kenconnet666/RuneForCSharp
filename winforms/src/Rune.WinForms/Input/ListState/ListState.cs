namespace Rune.WinForms;

/// <summary>列表里的一项：选项的显示信息，加上它来自的原始数据（自定义渲染拿到的就是它）。</summary>
/// <param name="Option">选项。</param>
/// <param name="Item">ItemsSource 里对应的那一项。</param>
public sealed record ListStateEntry(ListboxOption Option, object? Item);

/// <summary>列表的键盘操作（平台的按键先换成它，<see cref="ListState.Handle"/> 处理）。</summary>
public enum ListKey
{
    /// <summary>↓：下一项。</summary>
    Down,

    /// <summary>↑：上一项。</summary>
    Up,

    /// <summary>Home：第一项。</summary>
    Home,

    /// <summary>End：最后一项。</summary>
    End,

    /// <summary>Page Down：往下一页（10 项）。</summary>
    PageDown,

    /// <summary>Page Up：往上一页（10 项）。</summary>
    PageUp,

    /// <summary>Enter：选中高亮项。</summary>
    Enter,

    /// <summary>空格：宿主允许时选中高亮项。</summary>
    Space,
}

/// <summary>
/// 选项列表的状态（vue-tsx-pro 的 list-state，WAI-ARIA Listbox 的数据层）：选项、高亮项、已选项、键盘移动、类型搜索。
/// 不碰控件：Listbox、Select 各自决定焦点在哪（列表自己、触发按钮、搜索框），高亮项由它们画出来。
/// 选项按分组排好（没有分组的在前，各组按第一次出现的顺序，组内保持原顺序）；不可用的项能被经过、不能选中。
/// </summary>
/// <param name="onChange">选择变化：参数是新的已选值。</param>
public sealed class ListState(Action<IReadOnlyList<string>> onChange)
{
    /// <summary>一页移动几项（Page Up / Page Down）。</summary>
    const int Page = 10;

    readonly Typeahead search = new();
    IReadOnlyList<ListStateEntry> options = [];
    Dictionary<string, int> indices = [];
    HashSet<string> selectedSet = [];

    /// <summary>按分组排好的选项：渲染与键盘移动都按这个顺序。</summary>
    public IReadOnlyList<ListStateEntry> Options => options;

    /// <summary>已选的值。</summary>
    public IReadOnlyList<string> Selected
    {
        get;
        set
        {
            field = value;
            selectedSet = [.. value];
        }
    } = [];

    /// <summary>单选或多选。</summary>
    public SelectionMode Mode { get; set; } = SelectionMode.Single;

    /// <summary>至少保留一项（单选时再点已选项不取消）。</summary>
    public bool Required { get; set; }

    /// <summary>高亮（键盘或指针所在）的选项的值；选项换了以后不在了时为 null。</summary>
    public string? Active
    {
        get => field is not null && indices.ContainsKey(field) ? field : null;
        private set
        {
            if (field == value) return;
            field = value;
            ActiveChanged?.Invoke();
        }
    }

    /// <summary>高亮项变了：宿主重画、滚到看得见。</summary>
    public event Action? ActiveChanged;

    /// <summary>换数据：数据 → 选项（<see cref="ListboxOption.From"/>），按分组排好。</summary>
    /// <param name="items">数据。</param>
    /// <param name="toOption">数据 → 选项；数据是 ListboxOption 或字符串时不用写。</param>
    public void SetItems(IEnumerable<object?> items, Func<object?, ListboxOption>? toOption)
    {
        var entries = items.Select(item => new ListStateEntry(ListboxOption.From(item, toOption), item)).ToList();
        options = entries.Any(entry => entry.Option.Group is not null)
            ? [.. entries.GroupBy(entry => entry.Option.Group).OrderBy(group => group.Key is null ? 0 : 1).SelectMany(group => group)]
            : entries;
        indices = [];
        for (var index = 0; index < options.Count; index++) indices.TryAdd(options[index].Option.Value, index);
    }

    /// <summary>值在 Options 里的位置；不在时 -1。</summary>
    /// <param name="value">值。</param>
    public int IndexOf(string value) => indices.TryGetValue(value, out var index) ? index : -1;

    /// <summary>是否已选。</summary>
    /// <param name="value">值。</param>
    public bool IsSelected(string value) => selectedSet.Contains(value);

    /// <summary>设置高亮项；null 取消高亮。</summary>
    /// <param name="value">值。</param>
    public void SetActive(string? value) => Active = value;

    /// <summary>高亮已选的第一项，没有已选时高亮第一个可用项（打开下拉、列表获得焦点时）。</summary>
    public void ActivateDefault()
    {
        var usable = Enabled();
        Active = (usable.FirstOrDefault(entry => IsSelected(entry.Option.Value)) ?? usable.FirstOrDefault())?.Option.Value;
    }

    /// <summary>移动高亮 ±n 项（到头时停住），跳过不可用的项；没有高亮时往下从第一项、往上从最后一项开始。</summary>
    /// <param name="step">几项。</param>
    public void Move(int step)
    {
        var usable = Enabled();
        if (usable.Count == 0) return;
        var index = usable.FindIndex(entry => entry.Option.Value == Active);
        var next = index < 0 ? step > 0 ? 0 : usable.Count - 1 : Math.Clamp(index + step, 0, usable.Count - 1);
        Active = usable[next].Option.Value;
    }

    /// <summary>高亮第一个（<paramref name="last"/> 时最后一个）可用项。</summary>
    /// <param name="last">最后一个。</param>
    public void MoveToEnd(bool last)
    {
        var usable = Enabled();
        if (usable.Count > 0) Active = usable[last ? usable.Count - 1 : 0].Option.Value;
    }

    /// <summary>切换一项的选中（不可用的项忽略）；返回是否发生了变化。</summary>
    /// <param name="value">值。</param>
    public bool Select(string value)
    {
        var index = IndexOf(value);
        if (index < 0 || options[index].Option.IsDisabled) return false;
        var next = Selection.Toggle(Selected, value, Mode, Required);
        if (ReferenceEquals(next, Selected)) return false;
        Selected = next;
        onChange(next);
        return true;
    }

    /// <summary>类型搜索：按下一个字符，高亮以它开头的项；返回是否命中。</summary>
    /// <param name="key">按下的字符。</param>
    public bool Typeahead(string key)
    {
        var usable = Enabled();
        var index = usable.FindIndex(entry => entry.Option.Value == Active);
        var match = search.Next(key, [.. usable.Select(entry => entry.Option.Label)], index, Environment.TickCount64);
        if (match < 0) return false;
        Active = usable[match].Option.Value;
        return true;
    }

    /// <summary>
    /// 列表的键盘操作：↑↓ 移动、Home / End、Page Up / Down、Enter（与 <paramref name="space"/> 时的空格）选中。
    /// 返回是否处理了这个按键；选中时 <paramref name="picked"/> 收到被选的值（再选已选的那项也会收到：Select 据此关闭）。
    /// </summary>
    /// <param name="key">按键。</param>
    /// <param name="space">空格也选中（列表、Select；搜索框里空格是文字）。</param>
    /// <param name="picked">点选了一项。</param>
    public bool Handle(ListKey key, bool space = true, Action<string>? picked = null)
    {
        switch (key)
        {
            case ListKey.Down: Move(1); return true;
            case ListKey.Up: Move(-1); return true;
            case ListKey.PageDown: Move(Page); return true;
            case ListKey.PageUp: Move(-Page); return true;
            case ListKey.Home: MoveToEnd(last: false); return true;
            case ListKey.End: MoveToEnd(last: true); return true;
            case ListKey.Space when !space: return false;
        }
        if (Active is not { } value) return key == ListKey.Space;
        Select(value);
        picked?.Invoke(value);
        return true;
    }

    List<ListStateEntry> Enabled() => [.. options.Where(entry => !entry.Option.IsDisabled)];
}

/// <summary>
/// 类型搜索（vue-tsx-pro 的 typeahead，WAI-ARIA 的 type-ahead）：在列表、菜单里直接按字母，跳到以它开头的项。
/// 连续输入拼成一个词（"ex" 跳到 Export）；间隔超过 <see cref="Timeout"/> 后重新开始；反复按同一个字母在以它开头的项之间轮换。
/// 时间由调用方给（TS 用定时器清空缓冲；这里比较两次按键的时间，不依赖平台的计时器）。
/// </summary>
public sealed class Typeahead
{
    /// <summary>两次按键间隔多久之后重新开始（毫秒）。</summary>
    public const int Timeout = 500;

    string query = "";
    long last = long.MinValue / 2;

    /// <summary>处理一次按键，返回该落到的项的序号；不是单个可打印字符、或没有匹配时返回 -1。</summary>
    /// <param name="key">按下的字符。</param>
    /// <param name="labels">各项的文字。</param>
    /// <param name="current">当前项的序号（没有时 -1）。</param>
    /// <param name="now">按键的时间（毫秒，单调递增）。</param>
    public int Next(string key, IReadOnlyList<string> labels, int current, long now)
    {
        // 只认单个可打印字符；空格开头时不算（空格是激活键）
        if (key.Length != 1 || char.IsControl(key[0]) || (key == " " && (query.Length == 0 || now - last > Timeout))) return -1;
        if (now - last > Timeout) query = "";
        last = now;
        query += key.ToLowerInvariant();
        var first = query[..1];
        var repeated = query.All(c => c == first[0]);
        var search = repeated ? first : query;
        var start = repeated ? current + 1 : Math.Max(current, 0);
        for (var offset = 0; offset < labels.Count; offset++)
        {
            var index = (start + offset) % labels.Count;
            if (labels[index].Trim().ToLowerInvariant().StartsWith(search, StringComparison.Ordinal)) return index;
        }
        return -1;
    }
}
