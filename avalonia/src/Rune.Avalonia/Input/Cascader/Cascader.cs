using System.Collections;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 级联选择（vue-tsx-pro 的 Cascader）：一列一列往下选（省 / 市 / 区、产品大类 / 小类 / 型号、物料分类），触发框里显示整条路径"浙江 / 杭州 / 西湖区"。
/// <code>
/// &lt;rune:Cascader ItemsSource="{Binding Regions}" GetKey="{Binding KeyOf}" GetLabel="{Binding NameOf}" GetChildren="{Binding ChildrenOf}" Values="{Binding Path}" /&gt;
/// </code>
/// <list type="bullet">
/// <item>值是 <see cref="SelectBase.Values"/>：从第一级到选中节点的 key 路径（key 只要在同级里唯一），没有选择时 null。</item>
/// <item>默认只能选到叶子：点有下一级的项只是展开；<see cref="ChangeOnSelect"/> 时点任何一级都选中它（并展开下一级）。</item>
/// <item>弹出层里每一级是一列，焦点在列上：↑↓ 移动（有下一级的跟着显示下一列），→ 进入下一级（懒加载的先取回来），← 回到上一级，
/// Enter 选中（叶子选中后关闭），Esc 关闭并回到触发框；从右到左排版时左右键对调。</item>
/// <item><see cref="SelectBase.Searchable"/>：输入后换成一列匹配的完整路径（只列能选的，默认是叶子），↑↓、Enter 在搜索框里操作。</item>
/// <item>懒加载：<see cref="HasChildren"/> 声明有下一级，进入时 <see cref="LoadChildren"/> 取回来放进数据里，期间显示转圈；打开时沿着已选的路径逐级取回来。</item>
/// </list>
/// 外壳、开关见 <see cref="SelectBase"/>；部件见 <see cref="CascaderParts"/>、<see cref="SelectParts"/>。
/// </summary>
public class Cascader : SelectBase
{
    /// <summary>第一级的节点。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Cascader, IEnumerable?>(nameof(ItemsSource));

    /// <summary>节点的 key，同级里唯一；第二个参数是它在同级里的位置（必填）。</summary>
    public static readonly StyledProperty<Func<object, int, string>?> GetKeyProperty = AvaloniaProperty.Register<Cascader, Func<object, int, string>?>(nameof(GetKey));

    /// <summary>节点的文字。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetLabelProperty = AvaloniaProperty.Register<Cascader, Func<object, string>?>(nameof(GetLabel));

    /// <summary>下一级；叶子或还没加载时 null。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> GetChildrenProperty = AvaloniaProperty.Register<Cascader, Func<object, IEnumerable?>?>(nameof(GetChildren));

    /// <summary>有没有下一级（懒加载时还没取回来也算有）；不写时看下一级是否非空。</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasChildrenProperty = AvaloniaProperty.Register<Cascader, Func<object, bool>?>(nameof(HasChildren));

    /// <summary>进入一个还没有下一级的节点时取回来，放进数据里；返回任务时显示加载中。</summary>
    public static readonly StyledProperty<Func<object, Task?>?> LoadChildrenProperty = AvaloniaProperty.Register<Cascader, Func<object, Task?>?>(nameof(LoadChildren));

    /// <summary>不能选、不能进入的节点。</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsItemDisabledProperty = AvaloniaProperty.Register<Cascader, Func<object, bool>?>(nameof(IsItemDisabled));

    /// <summary>每一级都能选；默认只能选到叶子。</summary>
    public static readonly StyledProperty<bool> ChangeOnSelectProperty = AvaloniaProperty.Register<Cascader, bool>(nameof(ChangeOnSelect));

    /// <summary>触发框里显示整条路径（默认），false 时只显示最后一级。</summary>
    public static readonly StyledProperty<bool> ShowAllLevelsProperty = AvaloniaProperty.Register<Cascader, bool>(nameof(ShowAllLevels), true);

    /// <summary>路径各级之间的分隔，默认" / "。</summary>
    public static readonly StyledProperty<string> SeparatorProperty = AvaloniaProperty.Register<Cascader, string>(nameof(Separator), " / ");

    /// <summary>自己画触发框里的文字：参数是路径上各级的文字与节点（还没加载的节点文字是 key、节点是 null），返回行内文字。</summary>
    public static readonly StyledProperty<Func<IReadOnlyList<string>, IReadOnlyList<object?>, IEnumerable<Inline>>?> RenderValueProperty =
        AvaloniaProperty.Register<Cascader, Func<IReadOnlyList<string>, IReadOnlyList<object?>, IEnumerable<Inline>>?>(nameof(RenderValue));

    /// <summary>搜索时怎样算匹配：参数是路径上的节点与输入；不写时路径文字包含输入（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<IReadOnlyList<object>, string, bool>?> FilterProperty =
        AvaloniaProperty.Register<Cascader, Func<IReadOnlyList<object>, string, bool>?>(nameof(Filter));

    readonly StackPanel columns = new() { Name = CascaderParts.Columns, Orientation = Orientation.Horizontal };
    readonly ScrollArea scroller = new() { Axis = ScrollAreaAxis.X, IsViewportFocusable = false };
    readonly List<CascaderColumn> shown = [];
    readonly HashSet<object> loading = [];
    // 当前展开的路径（各列的高亮项）
    List<string> path = [];
    IReadOnlyList<PathHit>? hits;
    int hit;
    int? focusedColumn;

    /// <summary>新建：弹出层里是横排的各列。</summary>
    public Cascader()
    {
        scroller.Content = columns;
        SetBody(scroller);
        Search.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            hit = 0;
            UpdateColumns();
        };
    }

    /// <summary>第一级的节点。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>节点的 key。</summary>
    public Func<object, int, string>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>节点的文字。</summary>
    public Func<object, string>? GetLabel { get => GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>下一级。</summary>
    public Func<object, IEnumerable?>? GetChildren { get => GetValue(GetChildrenProperty); set => SetValue(GetChildrenProperty, value); }

    /// <summary>有没有下一级。</summary>
    public Func<object, bool>? HasChildren { get => GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }

    /// <summary>懒加载下一级。</summary>
    public Func<object, Task?>? LoadChildren { get => GetValue(LoadChildrenProperty); set => SetValue(LoadChildrenProperty, value); }

    /// <summary>不能选、不能进入的节点。</summary>
    public Func<object, bool>? IsItemDisabled { get => GetValue(IsItemDisabledProperty); set => SetValue(IsItemDisabledProperty, value); }

    /// <summary>每一级都能选。</summary>
    public bool ChangeOnSelect { get => GetValue(ChangeOnSelectProperty); set => SetValue(ChangeOnSelectProperty, value); }

    /// <summary>触发框里显示整条路径。</summary>
    public bool ShowAllLevels { get => GetValue(ShowAllLevelsProperty); set => SetValue(ShowAllLevelsProperty, value); }

    /// <summary>路径各级之间的分隔。</summary>
    public string Separator { get => GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    /// <summary>自己画触发框里的文字。</summary>
    public Func<IReadOnlyList<string>, IReadOnlyList<object?>, IEnumerable<Inline>>? RenderValue { get => GetValue(RenderValueProperty); set => SetValue(RenderValueProperty, value); }

    /// <summary>搜索的匹配。</summary>
    public Func<IReadOnlyList<object>, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>弹出层里看得见的各列（测试用）。</summary>
    internal IReadOnlyList<CascaderColumn> Columns => shown;

    IReadOnlyList<object> Roots => ItemsSource?.Cast<object>().ToList() ?? [];

    string KeyOf(object node, int index) => GetKey?.Invoke(node, index) ?? node.ToString() ?? "";

    string LabelText(object node) => GetLabel?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> ChildrenOf(object node) => GetChildren?.Invoke(node)?.Cast<object>().ToList() ?? [];

    bool HasLoadedChildren(object node) => ChildrenOf(node).Count > 0;

    /// <summary>有没有下一级（懒加载时还没取回来也算有）。</summary>
    internal bool Expandable(object node) => HasChildren?.Invoke(node) ?? HasLoadedChildren(node);

    internal bool Disabled(object node) => IsItemDisabled?.Invoke(node) ?? false;

    internal bool IsLoadingChildren(object node) => loading.Contains(node);

    /// <summary>值是一条路径：触发框里列出的是这一条（单选的样子）。</summary>
    protected override IReadOnlyList<string> Shown => Values is { Count: > 0 } keys ? [string.Join('\0', keys)] : [];

    /// <summary>触发框里的文字：整条路径（或最后一级）；还没加载的那几级按 key 显示。</summary>
    protected override string LabelOf(string value)
    {
        var labels = PathLabels(Values ?? []);
        return ShowAllLevels ? string.Join(Separator, labels) : labels.LastOrDefault() ?? "";
    }

    List<string> PathLabels(IReadOnlyList<string> keys)
    {
        var nodes = NodesAlong(keys);
        return [.. keys.Select((key, index) => index < nodes.Count ? LabelText(nodes[index]) : key)];
    }

    /// <summary>沿着 keys 找到各级的节点（找不到的地方停下：还没加载，或数据变了）。</summary>
    List<object> NodesAlong(IReadOnlyList<string> keys)
    {
        var found = new List<object>();
        var level = Roots;
        foreach (var key in keys)
        {
            var node = level.Where((item, index) => KeyOf(item, index) == key).FirstOrDefault();
            if (node is null) break;
            found.Add(node);
            level = ChildrenOf(node);
        }
        return found;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == GetKeyProperty || change.Property == GetLabelProperty || change.Property == GetChildrenProperty
            || change.Property == HasChildrenProperty || change.Property == IsItemDisabledProperty || change.Property == ChangeOnSelectProperty || change.Property == FilterProperty)
        {
            Sync();
            UpdateColumns();
        }
        else if (change.Property == ShowAllLevelsProperty || change.Property == SeparatorProperty || change.Property == RenderValueProperty) Sync();
        else if (change.Property == ValuesProperty) UpdateColumns();
    }

    /// <summary>外壳的文字；有 <see cref="RenderValue"/> 时换成它画的行内文字。</summary>
    protected override void Sync()
    {
        base.Sync();
        if (ValueText is not { } text || RenderValue is not { } render || Values is not { Count: > 0 } keys) return;
        var nodes = NodesAlong(keys);
        text.Inlines = [.. render(PathLabels(keys), [.. keys.Select((_, index) => index < nodes.Count ? nodes[index] : null)])];
    }

    /// <summary>清空：值是 null。</summary>
    protected override void ClearSelection() => SetCurrentValue(ValuesProperty, null);

    /// <summary>要显示的各列：第一级，以及路径上每个已加载（或正在加载）下一级的节点的下一级。</summary>
    List<(object? Parent, IReadOnlyList<object> Items)> ColumnData()
    {
        var list = new List<(object?, IReadOnlyList<object>)> { (null, Roots) };
        foreach (var node in NodesAlong(path))
        {
            if (!Expandable(node) || !(HasLoadedChildren(node) || loading.Contains(node))) break;
            list.Add((node, ChildrenOf(node)));
        }
        return list;
    }

    /// <summary>弹出层的内容：有搜索词时是一列匹配的路径，否则是各列；数据没变的列只更新状态（保留滚动位置）。</summary>
    void UpdateColumns()
    {
        hits = SearchHits();
        if (hits is { } found)
        {
            Keep(1);
            Column(0).ShowHits(found, hit, string.Join('\0', Values ?? []));
            return;
        }
        var data = ColumnData();
        Keep(data.Count);
        var chosen = Values ?? [];
        for (var level = 0; level < data.Count; level++)
        {
            // 这一列在已选路径上：前面各级都与已选的一致
            var onChosenPath = path.Take(level).Select((key, index) => chosen.ElementAtOrDefault(index) == key).All(same => same);
            var label = data[level].Parent is { } parent ? LabelText(parent) : Field.Of(this)?.LabelText;
            Column(level).ShowNodes(data[level].Parent, data[level].Items, path.ElementAtOrDefault(level), onChosenPath ? chosen.ElementAtOrDefault(level) : null,
                focusedColumn == level, label);
        }
    }

    CascaderColumn Column(int level)
    {
        while (shown.Count <= level)
        {
            var column = new CascaderColumn(this, shown.Count);
            shown.Add(column);
            columns.Children.Add(column);
        }
        return shown[level];
    }

    void Keep(int count)
    {
        for (var level = shown.Count - 1; level >= count; level--)
        {
            columns.Children.Remove(shown[level]);
            shown.RemoveAt(level);
        }
        for (var level = 0; level < shown.Count; level++) shown[level].IsLast = level == count - 1;
    }

    /// <summary>取回节点的下一级；有了（或取回来了）返回 true。</summary>
    async Task<bool> EnsureChildren(object node)
    {
        if (HasLoadedChildren(node)) return true;
        if (LoadChildren is not { } load || loading.Contains(node)) return false;
        var pending = load(node);
        if (pending is null) return HasLoadedChildren(node);
        loading.Add(node);
        UpdateColumns();
        try
        {
            await pending;
        }
        catch (Exception error)
        {
            Trace.TraceWarning($"级联选择的下一级加载失败：{error}");
            return false;
        }
        finally
        {
            loading.Remove(node);
            UpdateColumns();
        }
        return HasLoadedChildren(node);
    }

    /// <summary>高亮第 level 列的一项：它后面的路径换成它（有已加载的下一级时显示出来）。</summary>
    void Activate(int level, string key)
    {
        path = [.. path.Take(level), key];
        UpdateColumns();
    }

    /// <summary>进入一项的下一级：先取回来，再高亮下一列的第一项并把焦点移过去。</summary>
    async Task Enter(int level, object node, string key)
    {
        Activate(level, key);
        if (!Expandable(node) || !await EnsureChildren(node)) return;
        // 等待期间换了高亮项：不再进入
        if (path.ElementAtOrDefault(level) != key) return;
        var children = ChildrenOf(node);
        var first = children.Select((child, index) => (child, index)).FirstOrDefault(pair => !Disabled(pair.child));
        if (first.child is null) return;
        Activate(level + 1, KeyOf(first.child, first.index));
        FocusColumn(level + 1);
    }

    void FocusColumn(int level)
    {
        if (level < shown.Count) shown[level].Focus(NavigationMethod.Tab);
    }

    /// <summary>选中一条路径；选到叶子（或从搜索结果里选）时关闭。</summary>
    void Pick(IReadOnlyList<string> keys, object node, bool close)
    {
        SetCurrentValue(ValuesProperty, keys.ToList());
        if (close || !Expandable(node)) SetOpen(false, OpenChangeReason.Select);
    }

    /// <summary>点一项：有下一级的展开（ChangeOnSelect 时同时选中），叶子选中。</summary>
    internal void Click(int level, object node, string key)
    {
        if (Disabled(node)) return;
        FocusColumn(level);
        var keys = path.Take(level).Append(key).ToList();
        if (!Expandable(node))
        {
            Pick(keys, node, close: true);
            return;
        }
        if (ChangeOnSelect) Pick(keys, node, close: false);
        Activate(level, key);
        _ = EnsureChildren(node);
    }

    /// <summary>点搜索结果的一条：选中并关闭。</summary>
    internal void ClickHit(int index)
    {
        if (hits?.ElementAtOrDefault(index) is { Disabled: false } chosen) Pick(chosen.Keys, chosen.Nodes[^1], close: true);
    }

    /// <summary>列上的键盘（见类的说明）。</summary>
    internal void ColumnKeyDown(int level, KeyEventArgs e)
    {
        if (e.KeyModifiers is not KeyModifiers.None and not KeyModifiers.Shift) return;
        if (level >= shown.Count || shown[level].Items is not { } list) return;
        var current = list.Select((item, index) => KeyOf(item, index)).ToList().IndexOf(path.ElementAtOrDefault(level) ?? "\0");
        var enabled = list.Select((item, index) => (item, index)).Where(pair => !Disabled(pair.item)).Select(pair => pair.index).ToList();
        void Go(int? index)
        {
            if (index is { } at) Activate(level, KeyOf(list[at], at));
        }
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var node = current >= 0 ? list[current] : null;
        switch (e.Key)
        {
            case Key.Down:
                Go(enabled.Cast<int?>().FirstOrDefault(index => index > current) ?? enabled.Cast<int?>().FirstOrDefault());
                break;
            case Key.Up:
                Go(enabled.Cast<int?>().LastOrDefault(index => index < current) ?? enabled.Cast<int?>().LastOrDefault());
                break;
            case Key.Home:
                Go(enabled.Cast<int?>().FirstOrDefault());
                break;
            case Key.End:
                Go(enabled.Cast<int?>().LastOrDefault());
                break;
            case Key.Right or Key.Left when (e.Key == Key.Right) != rtl:
                if (node is not null) _ = Enter(level, node, path[level]);
                break;
            case Key.Right or Key.Left:
                if (level > 0) FocusColumn(level - 1);
                break;
            case Key.Enter:
                if (node is null || Disabled(node)) break;
                var keys = path.Take(level + 1).ToList();
                if (!Expandable(node)) Pick(keys, node, close: true);
                else
                {
                    if (ChangeOnSelect) Pick(keys, node, close: false);
                    _ = Enter(level, node, path[level]);
                }
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>列得到焦点：第一次进入这一列时高亮第一个能选的。</summary>
    internal void ColumnFocused(int level, bool focused)
    {
        focusedColumn = focused ? level : focusedColumn == level ? null : focusedColumn;
        if (focused && path.ElementAtOrDefault(level) is null && level < shown.Count && shown[level].Items is { } list)
        {
            var first = list.Select((item, index) => (item, index)).FirstOrDefault(pair => !Disabled(pair.item));
            if (first.item is not null)
            {
                Activate(level, KeyOf(first.item, first.index));
                return;
            }
        }
        UpdateColumns();
    }

    /// <summary>搜索结果的一条：整条路径。</summary>
    internal sealed record PathHit(IReadOnlyList<string> Keys, IReadOnlyList<object> Nodes, string Label, bool Disabled);

    /// <summary>搜索：匹配的完整路径，只列能选的（默认是叶子）；没有搜索词时 null。</summary>
    IReadOnlyList<PathHit>? SearchHits()
    {
        var text = (Search.Text ?? "").Trim();
        if (!Searchable || text.Length == 0) return null;
        var found = new List<PathHit>();
        void Visit(IReadOnlyList<object> level, IReadOnlyList<string> keys, IReadOnlyList<object> nodes, bool off)
        {
            for (var index = 0; index < level.Count; index++)
            {
                var node = level[index];
                List<string> nextKeys = [.. keys, KeyOf(node, index)];
                List<object> nextNodes = [.. nodes, node];
                var nextOff = off || Disabled(node);
                var label = string.Join(Separator, nextNodes.Select(LabelText));
                var selectable = ChangeOnSelect || !Expandable(node);
                if (selectable && (Filter is { } filter ? filter(nextNodes, text) : SelectSearch.Matches(label, text))) found.Add(new PathHit(nextKeys, nextNodes, label, nextOff));
                Visit(ChildrenOf(node), nextKeys, nextNodes, nextOff);
            }
        }
        Visit(Roots, [], [], false);
        return found;
    }

    /// <summary>搜索框里：没有搜索词时 ↓ 进入列；有搜索词时 ↑↓ 移动高亮、Enter 选中。</summary>
    protected override void OnSearchKey(KeyEventArgs e)
    {
        if (hits is not { } list)
        {
            if (e.Key != Key.Down) return;
            FocusColumn(Math.Max(path.Count - 1, 0));
            e.Handled = true;
            return;
        }
        var enabled = list.Select((item, index) => (item, index)).Where(pair => !pair.item.Disabled).Select(pair => pair.index).ToList();
        switch (e.Key)
        {
            case Key.Down:
                hit = enabled.Cast<int?>().FirstOrDefault(index => index > hit) ?? enabled.FirstOrDefault();
                break;
            case Key.Up:
                hit = enabled.Cast<int?>().LastOrDefault(index => index < hit) ?? enabled.LastOrDefault();
                break;
            case Key.Enter:
                ClickHit(hit);
                break;
            default:
                return;
        }
        e.Handled = true;
        UpdateColumns();
    }

    /// <summary>打开：清空搜索词，从已选的路径开始；沿着已选的路径逐级取回来（懒加载），已选的那一列看得见。</summary>
    protected override async void Show(bool last = false)
    {
        Search.Text = "";
        hit = 0;
        path = [.. Values ?? []];
        UpdateColumns();
        SetOpen(true, OpenChangeReason.Trigger);
        foreach (var node in NodesAlong(path).SkipLast(1))
            if (!await EnsureChildren(node)) break;
    }

    /// <summary>打开后：焦点进搜索框，没有搜索框时进已选的那一列。</summary>
    protected override void OnOpened()
    {
        if (WithSearch) base.OnOpened();
        else FocusColumn(Math.Max(path.Count - 1, 0));
    }

    /// <summary>按 em 的尺寸：各列最高 20em（桌面的弹出层不会按窗口剩下的高度收）。</summary>
    protected override void UpdateMetrics()
    {
        base.UpdateMetrics();
        scroller.MaxHeight = 20 * FontSize;
    }
}

/// <summary>
/// 级联选择弹出层里的一列（<see cref="Cascader"/> 建的，不单独用）：读屏是列表，焦点在列上，高亮项是当前路径上的那一项。
/// 最窄 10em、最宽 16em，四周 xs，列与列之间一道 borderMuted；搜索结果是一整列、不限宽。
/// </summary>
public sealed class CascaderColumn : Border
{
    readonly Cascader owner;
    readonly int level;
    readonly StackPanel rows = new();
    readonly ScrollArea scroll = new() { IsViewportFocusable = false };

    internal CascaderColumn(Cascader owner, int level)
    {
        (this.owner, this.level) = (owner, level);
        Name = CascaderParts.Column;
        Focusable = true;
        FocusAdorner = null;
        scroll.Content = rows;
        Child = scroll;
        KeyDown += (_, e) => owner.ColumnKeyDown(level, e);
        GotFocus += (_, _) => owner.ColumnFocused(level, true);
        LostFocus += (_, _) => owner.ColumnFocused(level, false);
    }

    /// <summary>这一列的节点（搜索结果时 null）。</summary>
    internal IReadOnlyList<object>? Items { get; private set; }

    /// <summary>这一列的行（测试用）。</summary>
    internal IEnumerable<CascaderOption> Options => rows.Children.OfType<CascaderOption>();

    /// <summary>最后一列：右边没有分隔线。</summary>
    internal bool IsLast { get; set { field = value; Metrics(); } }

    bool results;

    /// <summary>显示一级的节点：<paramref name="active"/> 是路径上的那一项，<paramref name="selected"/> 是已选路径上的那一项。</summary>
    internal void ShowNodes(object? parent, IReadOnlyList<object> items, string? active, string? selected, bool focused, string? label)
    {
        results = false;
        if (!ReferenceEquals(Items, items) || rows.Children.Count != items.Count + (parent is not null && owner.IsLoadingChildren(parent) && items.Count == 0 ? 1 : 0))
        {
            Items = items;
            rows.Children.Clear();
            if (parent is not null && owner.IsLoadingChildren(parent) && items.Count == 0)
                rows.Children.Add(new Border { Name = CascaderParts.Empty, Child = new Spinner { Label = Provider.GetLang(owner).Loading } });
            for (var index = 0; index < items.Count; index++) rows.Children.Add(new CascaderOption(owner, level, index, items[index]));
        }
        foreach (var option in Options)
        {
            var key = option.Key;
            option.Update(active == key, selected == key, focused && active == key, owner.Expandable(option.Node), owner.IsLoadingChildren(option.Node));
            if (active == key) option.BringIntoView();
        }
        AutomationProperties.SetName(this, label);
        // 读屏的"忙"：正在取这一列
        AutomationProperties.SetItemStatus(this, parent is not null && owner.IsLoadingChildren(parent) ? Provider.GetLang(owner).Loading : null);
        Metrics();
    }

    /// <summary>显示搜索结果：一列完整路径。</summary>
    internal void ShowHits(IReadOnlyList<Cascader.PathHit> hits, int highlighted, string chosen)
    {
        results = true;
        Items = null;
        rows.Children.Clear();
        var lang = Provider.GetLang(owner);
        if (hits.Count == 0) rows.Children.Add(new Border { Name = CascaderParts.Empty, Child = new TextBlock { Text = lang.SelectNoMatch } });
        for (var index = 0; index < hits.Count; index++)
        {
            var option = new CascaderOption(owner, -1, index, hits[index].Nodes[^1], hits[index].Label);
            option.Update(index == highlighted, string.Join('\0', hits[index].Keys) == chosen, false, false, false, hits[index].Disabled);
            rows.Children.Add(option);
        }
        AutomationProperties.SetName(this, lang.SelectSearch);
        Metrics();
    }

    /// <summary>em 尺寸：最窄 10em、最宽 16em（搜索结果不限宽），四周 xs；列与列之间 borderMuted；没有匹配、加载中的那一行上下左右 sm、textMuted。</summary>
    void Metrics()
    {
        var em = TextElement.GetFontSize(owner);
        MinWidth = results ? 0 : 10 * em;
        MaxWidth = results ? double.PositiveInfinity : 16 * em;
        rows.Margin = new Thickness(owner.Resource<double>(ThemeKeys.Space.Xs) * em);
        BorderThickness = FlowDirection == FlowDirection.RightToLeft ? new Thickness(IsLast ? 0 : 1, 0, 0, 0) : new Thickness(0, 0, IsLast ? 0 : 1, 0);
        BorderBrush = owner.Resource<IBrush>(ThemeKeys.Brush.BorderMuted);
        foreach (var empty in rows.Children.OfType<Border>().Where(child => child.Name == CascaderParts.Empty))
        {
            empty.Padding = new Thickness(owner.Resource<double>(ThemeKeys.Space.Sm) * em);
            TextElement.SetForeground(empty, owner.Resource<IBrush>(ThemeKeys.Brush.TextMuted));
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ColumnPeer(this);

    sealed class ColumnPeer(CascaderColumn owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// 级联选择的一项（<see cref="CascaderColumn"/> 建的）：外观同列表行（控件高度、上下 xs、左右 sm、sm 圆角，文字放不下省略）；
/// 路径上的那一项、悬停 fillHover，列有焦点时它画焦点环；已选 accentText；不可用变淡。尾部：有下一级的是箭头（取下一级时转圈），已选的叶子是对勾。
/// </summary>
public sealed class CascaderOption : Border
{
    readonly Cascader owner;
    readonly TextBlock label = new() { Name = CascaderParts.Label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly Icon chevron = new() { Data = Lucide.ChevronRight };
    readonly Icon check = new() { Data = Lucide.Check };
    readonly Spinner spinner = new() { Size = TextSize.Sm };
    readonly int level;
    readonly int index;
    bool active;
    bool selected;
    bool ring;
    bool disabled;

    internal CascaderOption(Cascader owner, int level, int index, object node, string? text = null)
    {
        (this.owner, this.level, this.index, Node) = (owner, level, index, node);
        Name = CascaderParts.Option;
        Key = level < 0 ? "" : owner.GetKey?.Invoke(node, index) ?? node.ToString() ?? "";
        label.Text = text ?? owner.GetLabel?.Invoke(node) ?? node.ToString();
        var layout = new DockPanel();
        foreach (var end in new Control[] { chevron, check, spinner })
        {
            DockPanel.SetDock(end, Dock.Right);
            layout.Children.Add(end);
        }
        layout.Children.Add(label);
        Child = layout;
        Cursor = new Cursor(StandardCursorType.Hand);
        Tapped += (_, e) =>
        {
            e.Handled = true;
            if (level < 0) owner.ClickHit(index);
            else owner.Click(level, Node, Key);
        };
    }

    /// <summary>节点（搜索结果里是路径末端的节点）。</summary>
    public object Node { get; }

    /// <summary>同级里的 key（搜索结果里是空字符串）。</summary>
    public string Key { get; }

    /// <summary>在当前路径上（或搜索结果里高亮着）。</summary>
    internal bool IsActive => active;

    /// <summary>在已选的路径上。</summary>
    internal bool IsSelected => selected;

    internal void Update(bool active, bool selected, bool ring, bool branch, bool loading, bool? off = null)
    {
        (this.active, this.selected, this.ring) = (active, selected, ring);
        disabled = off ?? owner.Disabled(Node);
        chevron.IsVisible = branch && !loading;
        spinner.IsVisible = branch && loading;
        check.IsVisible = !branch && selected;
        Paint();
    }

    void Paint()
    {
        var em = TextElement.GetFontSize(owner);
        double Space(string key) => owner.Resource<double>(key) * em;
        MinHeight = Math.Max(ControlHeight.Of(em, owner.Resource<double>(ThemeKeys.Size.Control)), owner.Resource<double>(ThemeKeys.Size.MinTarget));
        Padding = new Thickness(Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Xs));
        CornerRadius = new CornerRadius(owner.Resource<double>(ThemeKeys.Radius.Sm) * em);
        label.LineHeight = owner.Resource<double>(ThemeKeys.LineHeight.Normal) * em;
        label.Margin = new Thickness(0, 0, Space(ThemeKeys.Space.Sm), 0);
        chevron.Foreground = owner.Resource<IBrush>(ThemeKeys.Brush.TextMuted);
        chevron.RenderTransform = FlowDirection == FlowDirection.RightToLeft ? new ScaleTransform(-1, 1) : null;
        Background = (active || IsPointerOver) && !disabled ? owner.Resource<IBrush>(ThemeKeys.Brush.FillHover) : Brushes.Transparent;
        BorderThickness = new Thickness(ring ? owner.Resource<double>(ThemeKeys.BorderWidth.Sm) : 0);
        BorderBrush = ring ? owner.Resource<IBrush>(ThemeKeys.Brush.FocusRing) : null;
        TextElement.SetForeground(this, owner.Resource<IBrush>(selected ? ThemeKeys.Brush.AccentText : ThemeKeys.Brush.Text));
        Opacity = disabled ? owner.Resource<double>(ThemeKeys.Opacity.Disabled) : 1;
        Cursor = new Cursor(disabled ? StandardCursorType.No : StandardCursorType.Hand);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Paint();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Paint();
    }

    /// <summary>焦点留在列上。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    /// <summary>读屏：列表项，已选的是"选中"；有下一级的能展开。</summary>
    sealed class OptionPeer(CascaderOption owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => owner.label.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => owner.selected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (owner.level < 0) owner.owner.ClickHit(owner.index);
            else owner.owner.Click(owner.level, owner.Node, owner.Key);
        }
    }
}

/// <summary>Cascader 的部件名（弹出层里的；外壳的见 <see cref="SelectParts"/>）。</summary>
public static class CascaderParts
{
    /// <summary>各列的横排（列多了横向滚动）。</summary>
    public const string Columns = "PART_Columns";

    /// <summary>每一列（读屏的列表）。</summary>
    public const string Column = "PART_Column";

    /// <summary>每个选项。</summary>
    public const string Option = "PART_Option";

    /// <summary>选项的文字。</summary>
    public const string Label = "PART_Label";

    /// <summary>没有匹配、加载中的那一行。</summary>
    public const string Empty = "PART_Empty";
}
