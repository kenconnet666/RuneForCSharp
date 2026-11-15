namespace Rune.Wpf;

/// <summary>怎样从数据里读出树的结构（vue-tsx-pro 的 TreeAccessors）。</summary>
/// <typeparam name="T">节点的类型。</typeparam>
/// <param name="GetKey">节点的 key，整棵树里唯一；第二个参数是它在同级里的位置。</param>
/// <param name="GetChildren">子节点；叶子或还没加载时返回 null 或空。</param>
/// <param name="HasChildren">有没有子节点（懒加载时子节点还没取回来也算有）；null 时看 GetChildren 是否非空。</param>
public sealed record TreeAccessors<T>(Func<T, int, string> GetKey, Func<T, IReadOnlyList<T>?> GetChildren, Func<T, bool>? HasChildren = null)
{
    /// <summary>节点能不能展开。</summary>
    public bool IsExpandable(T node) => HasChildren?.Invoke(node) ?? (GetChildren(node)?.Count ?? 0) > 0;
}

/// <summary>展开成一列的一行（vue-tsx-pro 的 FlatTreeNode）。</summary>
/// <typeparam name="T">节点的类型。</typeparam>
/// <param name="Node">原始数据。</param>
/// <param name="Key">key。</param>
/// <param name="Depth">层级，第一层为 0。</param>
/// <param name="Parent">父节点的 key；第一层为 null。</param>
/// <param name="Expandable">能不能展开。</param>
/// <param name="SetSize">同级一共几个。</param>
/// <param name="Position">在同级里排第几，从 1 开始。</param>
public sealed record FlatTreeNode<T>(T Node, string Key, int Depth, string? Parent, bool Expandable, int SetSize, int Position);

/// <summary>整棵（已加载的）树的索引：勾选联动要看到折叠着的节点。</summary>
/// <param name="Order">深度优先的顺序。</param>
/// <param name="Parents">key → 父节点的 key（第一层为 null）。</param>
/// <param name="Children">key → 已加载的子节点的 key。</param>
public sealed record TreeIndex(IReadOnlyList<string> Order, IReadOnlyDictionary<string, string?> Parents, IReadOnlyDictionary<string, IReadOnlyList<string>> Children);

/// <summary>勾选状态。</summary>
public enum CheckState
{
    /// <summary>没勾。</summary>
    Unchecked,

    /// <summary>勾上。</summary>
    Checked,

    /// <summary>半选：子孙里有勾上的，自己没有全勾上。</summary>
    Indeterminate,
}

/// <summary>勾选结果的取法（vue-tsx-pro 的 CheckStrategies，同 Ant Design 的 showCheckedStrategy）。</summary>
public enum CheckStrategy
{
    /// <summary>勾上的全部节点（默认）。</summary>
    All,

    /// <summary>父节点全勾上时只要父节点，不再列出它的子孙。</summary>
    Parent,

    /// <summary>只要叶子（没有已加载的子节点的节点）。</summary>
    Child,
}

/// <summary>移动的目标：放到哪个节点下（null 是第一层）的第几位（按移走之后的同级算，超出时放到最后）。</summary>
/// <param name="Parent">新的父节点的 key。</param>
/// <param name="Index">在新的同级里的位置。</param>
public readonly record struct TreeMoveTarget(string? Parent, int Index);

/// <summary>搜索过滤的结果：要显示的（匹配的与它们的祖先）、要展开的（匹配的祖先）、匹配的。</summary>
/// <param name="Include">要显示的节点。</param>
/// <param name="Expand">要展开的节点。</param>
/// <param name="Matched">匹配的节点。</param>
public sealed record TreeFilter(IReadOnlySet<string> Include, IReadOnlySet<string> Expand, IReadOnlySet<string> Matched);

/// <summary>
/// 树状数据的共用逻辑（vue-tsx-pro 的 utils/data/tree，三个库共用一份）：按展开状态展开成一列行、勾选的父子联动（含半选）、
/// 搜索过滤、勾选结果的取法、到某个节点的路径、平铺的行组装成树、移动节点。数据形状不限，由 <see cref="TreeAccessors{T}"/> 读取。
/// </summary>
public static class TreeData
{
    /// <summary>按展开状态深度优先展开成一列：只有展开了的节点的子节点出现在结果里。</summary>
    /// <param name="roots">第一层。</param>
    /// <param name="accessors">读结构的方法。</param>
    /// <param name="expanded">展开的 key。</param>
    /// <param name="include">只要这些节点（搜索过滤的 Include）；null 时全要。</param>
    /// <param name="order">改写每一层的顺序：返回同级的下标按什么顺序排；null 时按数据顺序。</param>
    public static List<FlatTreeNode<T>> Flatten<T>(IReadOnlyList<T> roots, TreeAccessors<T> accessors, IReadOnlySet<string> expanded,
        IReadOnlySet<string>? include = null, Func<IReadOnlyList<T>, IReadOnlyList<int>>? order = null)
    {
        var result = new List<FlatTreeNode<T>>();
        void Visit(IReadOnlyList<T> level, int depth, string? parent)
        {
            // key 按数据里的位置取（GetKey 的位置参数），再排序、过滤
            var sorted = (order?.Invoke(level) ?? Enumerable.Range(0, level.Count).ToList())
                .Select(i => (Node: level[i], Key: accessors.GetKey(level[i], i)))
                .Where(pair => include is null || include.Contains(pair.Key))
                .ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var (node, key) = sorted[i];
                var expandable = accessors.IsExpandable(node);
                result.Add(new FlatTreeNode<T>(node, key, depth, parent, expandable, sorted.Count, i + 1));
                if (expandable && expanded.Contains(key)) Visit(accessors.GetChildren(node) ?? [], depth + 1, key);
            }
        }
        Visit(roots, 0, null);
        return result;
    }

    /// <summary>建立整棵已加载的树的索引。</summary>
    public static TreeIndex Index<T>(IReadOnlyList<T> roots, TreeAccessors<T> accessors)
    {
        var order = new List<string>();
        var parents = new Dictionary<string, string?>();
        var children = new Dictionary<string, IReadOnlyList<string>>();
        void Visit(IReadOnlyList<T> level, string? parent)
        {
            var keys = level.Select((node, i) => accessors.GetKey(node, i)).ToList();
            if (parent is not null) children[parent] = keys;
            for (var i = 0; i < level.Count; i++)
            {
                order.Add(keys[i]);
                parents[keys[i]] = parent;
                Visit(accessors.GetChildren(level[i]) ?? [], keys[i]);
            }
        }
        Visit(roots, null);
        return new TreeIndex(order, parents, children);
    }

    /// <summary>
    /// 实际勾上的节点：自己或任何祖先在 checked 里（父节点勾上之后才懒加载出来的子节点因此也算勾上），
    /// 或者已加载的子节点全勾上了（checked 由外部给出、没有写全父节点时）。
    /// </summary>
    static HashSet<string> Effective(TreeIndex index, IReadOnlyCollection<string> checkedKeys)
    {
        var listed = checkedKeys.ToHashSet();
        var result = new HashSet<string>();
        // 深度优先的顺序：正序时父节点先于子节点，倒序时子节点先于父节点
        foreach (var key in index.Order)
            if (listed.Contains(key) || (index.Parents[key] is { } parent && result.Contains(parent))) result.Add(key);
        for (var i = index.Order.Count - 1; i >= 0; i--)
            if (index.Children.TryGetValue(index.Order[i], out var children) && children.Count > 0 && children.All(result.Contains)) result.Add(index.Order[i]);
        return result;
    }

    /// <summary>勾上或取消 key：子孙跟着变；祖先的子节点全勾上时祖先也勾上，否则取消。返回勾上的全部节点（按树的顺序）。</summary>
    public static IReadOnlyList<string> ToggleChecked(TreeIndex index, IReadOnlyCollection<string> checkedKeys, string key)
    {
        var result = Effective(index, checkedKeys);
        var next = !result.Contains(key);
        void Visit(string current)
        {
            if (next) result.Add(current);
            else result.Remove(current);
            if (index.Children.TryGetValue(current, out var children)) foreach (var child in children) Visit(child);
        }
        Visit(key);
        for (var parent = index.Parents.GetValueOrDefault(key); parent is not null; parent = index.Parents.GetValueOrDefault(parent))
        {
            if (index.Children[parent].All(result.Contains)) result.Add(parent);
            else result.Remove(parent);
        }
        return [.. index.Order.Where(result.Contains)];
    }

    /// <summary>按取法给出勾上的节点（按树的顺序）。</summary>
    public static IReadOnlyList<string> CheckedByStrategy(TreeIndex index, IReadOnlyCollection<string> checkedKeys, CheckStrategy strategy)
    {
        var effective = Effective(index, checkedKeys);
        return [.. index.Order.Where(key => effective.Contains(key) && strategy switch
        {
            CheckStrategy.Child => !index.Children.TryGetValue(key, out var children) || children.Count == 0,
            CheckStrategy.Parent => index.Parents[key] is not { } parent || !effective.Contains(parent),
            _ => true,
        })];
    }

    /// <summary>每个节点的勾选状态：子孙里有勾上的、但自己没有全勾上时是半选。</summary>
    public static IReadOnlyDictionary<string, CheckState> CheckStates(TreeIndex index, IReadOnlyCollection<string> checkedKeys)
    {
        var effective = Effective(index, checkedKeys);
        var states = new Dictionary<string, CheckState>();
        // 倒序：子节点先于父节点算好
        for (var i = index.Order.Count - 1; i >= 0; i--)
        {
            var key = index.Order[i];
            if (effective.Contains(key))
            {
                states[key] = CheckState.Checked;
                continue;
            }
            var some = index.Children.TryGetValue(key, out var children) && children.Any(child => states[child] != CheckState.Unchecked);
            states[key] = some ? CheckState.Indeterminate : CheckState.Unchecked;
        }
        return states;
    }

    /// <summary>从第一层到 key 的各节点的 key（含 key 自己）；key 不在树里时为空。</summary>
    public static IReadOnlyList<string> Path(TreeIndex index, string key)
    {
        if (!index.Parents.ContainsKey(key)) return [];
        var path = new List<string>();
        for (string? current = key; current is not null; current = index.Parents[current]) path.Insert(0, current);
        return path;
    }

    /// <summary>搜索过滤：留下匹配的节点与它们的祖先，并展开祖先（只看已加载的节点）；匹配的节点的子孙不跟着留下。</summary>
    public static TreeFilter Filter<T>(IReadOnlyList<T> roots, TreeAccessors<T> accessors, Func<T, bool> match)
    {
        var include = new HashSet<string>();
        var expand = new HashSet<string>();
        var matched = new HashSet<string>();
        // 返回子树里有没有匹配的节点
        bool Visit(IReadOnlyList<T> level)
        {
            var any = false;
            for (var i = 0; i < level.Count; i++)
            {
                var key = accessors.GetKey(level[i], i);
                var self = match(level[i]);
                var below = Visit(accessors.GetChildren(level[i]) ?? []);
                if (self) matched.Add(key);
                if (below) expand.Add(key);
                if (!self && !below) continue;
                include.Add(key);
                any = true;
            }
            return any;
        }
        Visit(roots);
        return new TreeFilter(include, expand, matched);
    }

    /// <summary>
    /// 平铺的行（每行带父 key）组装成树，返回第一层；同级保持 rows 里的顺序。父行不在这批行里的当作根。key 重复或父子成环时抛出。
    /// </summary>
    /// <param name="rows">平铺的行。</param>
    /// <param name="getKey">行的 key。</param>
    /// <param name="getParent">父行的 key；根行是 null。</param>
    /// <param name="withChildren">用子节点生成一个节点（不改原行）。</param>
    public static List<T> Build<T>(IReadOnlyList<T> rows, Func<T, string> getKey, Func<T, string?> getParent, Func<T, IReadOnlyList<T>, T> withChildren)
    {
        var keys = new HashSet<string>();
        foreach (var row in rows)
            if (!keys.Add(getKey(row))) throw new ArgumentException($"TreeData.Build：key {getKey(row)} 出现了两次", nameof(rows));
        var roots = new List<T>();
        var childrenOf = new Dictionary<string, List<T>>();
        foreach (var row in rows)
        {
            var parent = getParent(row);
            if (parent is null || parent == getKey(row) || !keys.Contains(parent)) roots.Add(row);
            else (childrenOf.TryGetValue(parent, out var list) ? list : childrenOf[parent] = []).Add(row);
        }
        var built = 0;
        T Node(T row)
        {
            built++;
            return withChildren(row, [.. (childrenOf.GetValueOrDefault(getKey(row)) ?? []).Select(Node)]);
        }
        var tree = roots.Select(Node).ToList();
        if (built < rows.Count) throw new ArgumentException("TreeData.Build：有的行的父子关系成了环，到不了第一层", nameof(rows));
        return tree;
    }

    /// <summary>
    /// 把 key 移到 target（不改原数据，返回新的第一层；沿途的节点用 withChildren 重建，其余节点原样复用）。
    /// key 不存在、目标是它自己或它的子孙时返回 null。
    /// </summary>
    public static List<T>? Move<T>(IReadOnlyList<T> roots, TreeAccessors<T> accessors, Func<T, IReadOnlyList<T>, T> withChildren, string key, TreeMoveTarget target)
    {
        var index = Index(roots, accessors);
        if (!index.Parents.ContainsKey(key)) return null;
        if (target.Parent is { } targetParent && (!index.Parents.ContainsKey(targetParent) || Path(index, targetParent).Contains(key))) return null;
        var moving = default(T);
        // 先摘下来：只重建 key 的祖先
        var removePath = Path(index, key).SkipLast(1).ToHashSet();
        List<T> Remove(IReadOnlyList<T> level)
        {
            var result = new List<T>();
            for (var i = 0; i < level.Count; i++)
            {
                var nodeKey = accessors.GetKey(level[i], i);
                if (nodeKey == key) moving = level[i];
                else result.Add(removePath.Contains(nodeKey) ? withChildren(level[i], Remove(accessors.GetChildren(level[i]) ?? [])) : level[i]);
            }
            return result;
        }
        var without = Remove(roots);
        List<T> Insert(IReadOnlyList<T> level)
        {
            var next = level.ToList();
            next.Insert(Math.Clamp(target.Index, 0, next.Count), moving!);
            return next;
        }
        if (target.Parent is null) return Insert(without);
        // 再插进去：只重建目标父节点的祖先（含它自己）
        var insertPath = Path(Index(without, accessors), target.Parent).ToHashSet();
        List<T> Add(IReadOnlyList<T> level) => [.. level.Select((node, i) =>
        {
            var nodeKey = accessors.GetKey(node, i);
            if (!insertPath.Contains(nodeKey)) return node;
            var children = accessors.GetChildren(node) ?? [];
            return withChildren(node, nodeKey == target.Parent ? Insert(children) : Add(children));
        })];
        return Add(without);
    }
}

/// <summary>
/// 展开状态与懒加载（vue-tsx-pro 的 useTreeExpansion）：展开一个还没有子节点的节点时调用 loadChildren，加载中记下；
/// 失败时收回去（再展开会重试）。展开的 key 由调用方读写（控件的 Expanded 属性）。
/// </summary>
/// <typeparam name="T">节点的类型。</typeparam>
/// <param name="expanded">读当前展开的 key。</param>
/// <param name="setExpanded">写回展开的 key。</param>
/// <param name="getChildren">节点已加载的子节点。</param>
public sealed class TreeExpansion<T>(Func<IReadOnlyList<string>> expanded, Action<IReadOnlyList<string>> setExpanded, Func<T, IReadOnlyList<T>?> getChildren)
{
    readonly HashSet<string> loading = [];

    /// <summary>展开一个还没有子节点的节点时调用：取回子节点并放进数据里；返回的任务结束前显示加载中。</summary>
    public Func<T, Task?>? LoadChildren { get; set; }

    /// <summary>子节点加载失败：节点收回去，可以再展开重试；null 时写进跟踪日志。</summary>
    public Action<Exception, T>? LoadError { get; set; }

    /// <summary>加载状态变了（开始或结束）：控件重画加载指示。</summary>
    public event Action? LoadingChanged;

    /// <summary>是否展开。</summary>
    public bool IsExpanded(string key) => expanded().Contains(key);

    /// <summary>是否正在加载子节点。</summary>
    public bool IsLoading(string key) => loading.Contains(key);

    /// <summary>展开或收起；展开一个还没有子节点的节点时懒加载。</summary>
    public async Task SetOpen(T node, string key, bool open)
    {
        if (IsExpanded(key) == open) return;
        if (!open)
        {
            Collapse(key);
            return;
        }
        setExpanded([.. expanded(), key]);
        // 正在加载（收起后又展开）：等那一次，不重复请求
        if (LoadChildren is null || loading.Contains(key) || (getChildren(node)?.Count ?? 0) > 0) return;
        if (LoadChildren(node) is not { } pending) return;
        loading.Add(key);
        LoadingChanged?.Invoke();
        try
        {
            await pending;
        }
        catch (Exception error)
        {
            // 失败时收回去：留着展开却没有子节点，看起来像是本来就没有；再展开会重试
            Collapse(key);
            if (LoadError is { } report) report(error, node);
            else System.Diagnostics.Trace.TraceError($"树的子节点加载失败：{error}");
        }
        finally
        {
            loading.Remove(key);
            LoadingChanged?.Invoke();
        }
    }

    void Collapse(string key) => setExpanded([.. expanded().Where(item => item != key)]);
}
