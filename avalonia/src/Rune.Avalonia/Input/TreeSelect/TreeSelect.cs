using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 树形下拉选择（vue-tsx-pro 的 TreeSelect）：触发框同 <see cref="Select"/>，弹出的是一棵 <see cref="Tree"/>（组织机构、产品结构、物料分类）。
/// <code>
/// &lt;rune:TreeSelect ItemsSource="{Binding Orgs}" GetKey="{Binding KeyOf}" GetLabel="{Binding NameOf}" GetChildren="{Binding ChildrenOf}" Value="{Binding OrgId}" Searchable="True" /&gt;
/// &lt;rune:TreeSelect … SelectionMode="multiple" CheckedStrategy="child" MaxTagCount="responsive" Values="{Binding TeamIds}" /&gt;
/// </code>
/// <list type="bullet">
/// <item>单选：点节点或 Enter 选中并关闭；多选：空格或点勾选框，父子联动。<see cref="SelectBase.Values"/> 按 <see cref="CheckedStrategy"/> 放勾上的节点：
/// 全部（默认）、整枝时只放父节点、只放叶子；还没加载的已选节点原样留着。</item>
/// <item>多选的标签同 Select，但只列最上层的整枝（勾上了"研发中心"就不再列它下面的每个组），<see cref="CheckStrategy.Child"/> 时列叶子；移除一个标签取消整枝。</item>
/// <item><see cref="SelectBase.Searchable"/>：按节点文字过滤（或 <see cref="Filter"/>），命中节点的祖先一起显示并展开；搜索框里 ↓ 进到树里。
/// 给了 <see cref="Load"/> 时由服务端搜索：返回命中的节点连同祖先（一页），结果全部展开，不在本地过滤。</item>
/// <item>打开后焦点在树上（或搜索框里），键盘同 Tree；打开时展开已选节点的祖先。已选节点的文字从已加载的树里找，见过的记着，从没见过的显示成 key。</item>
/// </list>
/// 外壳、开关与部件见 <see cref="SelectBase"/>、<see cref="SelectParts"/>（树是 <see cref="SelectParts.Tree"/>）。
/// </summary>
public class TreeSelect : SelectBase
{
    /// <summary>顶层节点；有 <see cref="Load"/> 时是第一次加载之前的树（编辑页放上已选节点的那一枝）。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<TreeSelect, IEnumerable?>(nameof(ItemsSource));

    /// <summary>节点的 key，整棵树里唯一：也是选中后写回的值（必填）。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetKeyProperty = AvaloniaProperty.Register<TreeSelect, Func<object, string>?>(nameof(GetKey));

    /// <summary>节点的文字。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetLabelProperty = AvaloniaProperty.Register<TreeSelect, Func<object, string>?>(nameof(GetLabel));

    /// <summary>子节点；叶子或还没加载时 null。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> GetChildrenProperty = AvaloniaProperty.Register<TreeSelect, Func<object, IEnumerable?>?>(nameof(GetChildren));

    /// <summary>有没有子节点（懒加载时子节点还没取回来也算有）。</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasChildrenProperty = AvaloniaProperty.Register<TreeSelect, Func<object, bool>?>(nameof(HasChildren));

    /// <summary>展开还没有子节点的节点时取回来（同 Tree）。</summary>
    public static readonly StyledProperty<Func<object, Task?>?> LoadChildrenProperty = AvaloniaProperty.Register<TreeSelect, Func<object, Task?>?>(nameof(LoadChildren));

    /// <summary>不能选的节点（只能展开，如只能选到部门、不能选公司时）。</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsItemDisabledProperty = AvaloniaProperty.Register<TreeSelect, Func<object, bool>?>(nameof(IsItemDisabled));

    /// <summary>多选时值里放哪些勾上的节点，默认全部。</summary>
    public static readonly StyledProperty<CheckStrategy> CheckedStrategyProperty = AvaloniaProperty.Register<TreeSelect, CheckStrategy>(nameof(CheckedStrategy));

    /// <summary>搜索时怎样算命中（节点、搜索词）；不写时节点文字包含搜索词（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object, string, bool>?> FilterProperty = AvaloniaProperty.Register<TreeSelect, Func<object, string, bool>?>(nameof(Filter));

    /// <summary>远程搜索（同 Select 的 Load，只取第一页）：返回命中的节点连同祖先（顶层节点），结果代替 ItemsSource。</summary>
    public static readonly StyledProperty<OptionsLoader?> LoadProperty = AvaloniaProperty.Register<TreeSelect, OptionsLoader?>(nameof(Load));

    readonly Tree tree = new() { Name = SelectParts.Tree, VerticalAlignment = VerticalAlignment.Stretch };
    readonly RemoteOptions remote;
    readonly Dictionary<string, string> seen = [];
    // 已加载的整棵树：key → 节点与父节点（查已选的文字、展开已选的祖先）
    Dictionary<string, (object Node, string? Parent)> nodes = [];
    TreeIndex index = new([], new Dictionary<string, string?>(), new Dictionary<string, IReadOnlyList<string>>());
    // 自己展开的节点；服务端搜索的结果全部展开，清空搜索后回到它
    IReadOnlyList<string> expanded = [];
    bool syncing;

    /// <summary>新建：弹出层里是树。</summary>
    public TreeSelect()
    {
        remote = new RemoteOptions(node => node is null ? "" : KeyOf(node), OnRemoteChanged);
        tree.GetKey = KeyOf;
        tree.GetLabel = node => GetLabel?.Invoke(node) ?? node.ToString() ?? "";
        tree.GetChildren = node => GetChildren?.Invoke(node);
        SetBody(tree);
        tree.PropertyChanged += (_, e) =>
        {
            if (syncing) return;
            if (e.Property == Tree.SelectedProperty) OnTreeSelected();
            else if (e.Property == Tree.CheckedProperty) SetChecked(tree.Checked);
            else if (e.Property == Tree.ExpandedProperty && !RemoteResults) expanded = tree.Expanded;
        };
        Search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) OnSearchChanged();
        };
    }

    /// <summary>顶层节点。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>节点的 key。</summary>
    public Func<object, string>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>节点的文字。</summary>
    public Func<object, string>? GetLabel { get => GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>子节点。</summary>
    public Func<object, IEnumerable?>? GetChildren { get => GetValue(GetChildrenProperty); set => SetValue(GetChildrenProperty, value); }

    /// <summary>有没有子节点。</summary>
    public Func<object, bool>? HasChildren { get => GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }

    /// <summary>懒加载子节点。</summary>
    public Func<object, Task?>? LoadChildren { get => GetValue(LoadChildrenProperty); set => SetValue(LoadChildrenProperty, value); }

    /// <summary>不能选的节点。</summary>
    public Func<object, bool>? IsItemDisabled { get => GetValue(IsItemDisabledProperty); set => SetValue(IsItemDisabledProperty, value); }

    /// <summary>多选时值里放哪些勾上的节点。</summary>
    public CheckStrategy CheckedStrategy { get => GetValue(CheckedStrategyProperty); set => SetValue(CheckedStrategyProperty, value); }

    /// <summary>搜索时怎样算命中。</summary>
    public Func<object, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>远程搜索。</summary>
    public OptionsLoader? Load { get => GetValue(LoadProperty); set => SetValue(LoadProperty, value); }

    /// <summary>弹出层里的树（测试用）。</summary>
    internal Tree Tree => tree;

    string KeyOf(object node) => GetKey?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> ChildrenOf(object node) => GetChildren?.Invoke(node)?.Cast<object>().ToList() ?? [];

    string SearchText => (Search.Text ?? "").Trim();

    /// <summary>服务端搜索的结果：只有命中的节点与它们的祖先，全部展开。</summary>
    bool RemoteResults => Load is not null && SearchText.Length > 0;

    /// <summary>当前的树：远程请求到的，没有时是 ItemsSource。</summary>
    IReadOnlyList<object> Roots => [.. remote.ItemsOr(ItemsSource?.Cast<object?>().ToList() ?? []).OfType<object>()];

    /// <inheritdoc />
    protected override double MinPanelWidth => 16 * FontSize;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == GetKeyProperty || change.Property == GetChildrenProperty) Rebuild();
        else if (change.Property == GetLabelProperty || change.Property == CheckedStrategyProperty) Sync();
        else if (change.Property == HasChildrenProperty) tree.HasChildren = HasChildren;
        else if (change.Property == LoadChildrenProperty) tree.LoadChildren = LoadChildren;
        else if (change.Property == IsItemDisabledProperty) tree.IsItemDisabled = IsItemDisabled;
        else if (change.Property == FilterProperty) UpdateFilter();
        else if (change.Property == LoadProperty)
        {
            remote.Load = Load;
            OnRemoteChanged();
        }
        else if (change.Property == IsLoadingProperty || change.Property == EmptyProperty) UpdateState();
    }

    /// <summary>数据变了：重建 key 的索引（勾选联动要看到折叠着的节点），记下见过的文字。</summary>
    void Rebuild()
    {
        var roots = Roots;
        nodes = [];
        void Visit(IReadOnlyList<object> level, string? parent)
        {
            foreach (var node in level)
            {
                var key = KeyOf(node);
                nodes[key] = (node, parent);
                if (GetLabel is { } label) seen[key] = label(node);
                Visit(ChildrenOf(node), key);
            }
        }
        Visit(roots, null);
        index = TreeData.Index(roots, new TreeAccessors<object>((node, _) => KeyOf(node), ChildrenOf));
        syncing = true;
        tree.ItemsSource = roots;
        syncing = false;
        UpdateFilter();
        Sync();
    }

    void OnRemoteChanged()
    {
        UpdateState();
        Rebuild();
    }

    /// <summary>加载中、空状态：加载失败是"加载失败，请重试"，有搜索词是"没有匹配的选项"。</summary>
    void UpdateState()
    {
        var lang = Provider.GetLang(this);
        tree.IsLoading = IsLoading || remote.Loading;
        tree.Empty = remote.Failed ? lang.ListLoadFailed : SearchText.Length > 0 ? lang.SelectNoMatch : Empty;
    }

    /// <summary>本地搜索交给树的过滤（命中的节点与祖先、展开祖先、命中的加粗）；服务端搜索的结果已经筛好了，不再过滤。</summary>
    void UpdateFilter()
    {
        var text = SearchText;
        var local = Searchable && text.Length > 0 && Load is null;
        syncing = true;
        tree.Filter = local && Filter is null ? text : null;
        tree.FilterBy = local && Filter is { } filter ? node => filter(node, text) : null;
        tree.Expanded = RemoteResults ? [.. nodes.Keys] : expanded;
        syncing = false;
    }

    void OnSearchChanged()
    {
        remote.SetQuery(SearchText);
        UpdateFilter();
        UpdateState();
    }

    /// <summary>值 → 树（单选是选中、多选是勾选），再同步外壳。</summary>
    protected override void Sync()
    {
        syncing = true;
        tree.SelectionMode = Multiple ? default : SelectionMode.Single;
        tree.Checkable = Multiple;
        if (Multiple) tree.Checked = [.. Selected];
        else tree.Selected = [.. Selected];
        syncing = false;
        base.Sync();
    }

    /// <summary>按取法整理勾上的节点：已加载的按树的顺序与父子关系，还没加载（或远程搜索换掉了）的原样跟在后面。</summary>
    IReadOnlyList<string> ByStrategy(IReadOnlyCollection<string> keys, CheckStrategy strategy) =>
        [.. TreeData.CheckedByStrategy(index, keys, strategy), .. keys.Where(key => !index.Parents.ContainsKey(key))];

    /// <summary>勾选变了（树、移除标签）：按 <see cref="CheckedStrategy"/> 写回，还没加载的已选节点保留。</summary>
    void SetChecked(IReadOnlyCollection<string> keys)
    {
        var unknown = Selected.Where(key => !index.Parents.ContainsKey(key));
        SetCurrentValue(ValuesProperty, (IReadOnlyList<string>)[.. TreeData.CheckedByStrategy(index, keys, CheckedStrategy), .. unknown]);
    }

    /// <summary>触发框里列出的：多选时只列最上层的整枝（父节点也勾上了的不重复列），child 时列叶子。</summary>
    protected override IReadOnlyList<string> Shown =>
        Multiple ? ByStrategy(Selected, CheckedStrategy == CheckStrategy.Child ? CheckStrategy.Child : CheckStrategy.Parent) : Selected;

    /// <summary>已选节点的文字：从已加载的树里找，远程搜索换掉了的按见过的，从没见过的就是 key。</summary>
    protected override string LabelOf(string value) =>
        nodes.TryGetValue(value, out var found) && GetLabel is { } label ? label(found.Node) : seen.TryGetValue(value, out var known) ? known : value;

    /// <summary>移除一个标签：取消整枝。</summary>
    protected override void Remove(string value) => SetChecked(TreeData.ToggleChecked(index, Selected, value));

    /// <summary>单选时树里选中了：写回并关闭（焦点回到触发框）。</summary>
    void OnTreeSelected()
    {
        if (Multiple) return;
        if (tree.Selected is [var key, ..]) SetCurrentValue(ValueProperty, key);
        SetOpen(false, OpenChangeReason.Select);
        Focus(NavigationMethod.Tab);
    }

    /// <summary>打开：清空搜索词，展开已选节点的祖先（已选的看得见）。</summary>
    protected override void Show(bool last = false)
    {
        Search.Text = "";
        var ancestors = expanded.ToHashSet();
        foreach (var key in Selected)
            for (var at = nodes.GetValueOrDefault(key).Parent; at is not null; at = nodes.GetValueOrDefault(at).Parent)
                ancestors.Add(at);
        expanded = [.. ancestors];
        UpdateFilter();
        SetOpen(true, OpenChangeReason.Trigger);
    }

    /// <inheritdoc />
    protected override void Toggle()
    {
        remote.SetOpen(IsOpen);
        base.Toggle();
    }

    /// <summary>打开后：焦点进搜索框，没有搜索框时进树。</summary>
    protected override void OnOpened()
    {
        if (WithSearch) base.OnOpened();
        else tree.Focus(NavigationMethod.Tab);
    }

    /// <summary>搜索框里 ↓ 进到树里。</summary>
    protected override void OnSearchKey(KeyEventArgs e)
    {
        if (e.Key != Key.Down) return;
        tree.Focus(NavigationMethod.Tab);
        e.Handled = true;
    }

    /// <summary>按 em 的尺寸：树最高 20em（桌面的弹出层不会按窗口剩下的高度收），弹出层至少 16em 宽（放得下几层缩进）。</summary>
    protected override void UpdateMetrics()
    {
        base.UpdateMetrics();
        tree.MaxHeight = 20 * FontSize;
    }
}
