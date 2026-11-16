using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 树形表格（vue-tsx-pro 的 TreeDataTable）：有层级的行（部门 → 小组 → 成员、文件夹 → 文件），第一列带缩进与展开按钮。
/// <code>
/// &lt;rune:TreeDataTable ItemsSource="{Binding Departments}" Columns="{Binding Columns}" GetKey="{Binding UnitKey}"
///                     GetChildren="{Binding ChildrenOf}" Expanded="{Binding Open}" AutomationProperties.Name="组织架构" /&gt;
/// </code>
/// 建在 <see cref="DataTable"/> 上：列定义、排序、勾选、列宽、虚拟滚动、加载与空状态、外观都一样，<see cref="DataTable.ItemsSource"/> 是第一层。
/// 这里只做两件事：按展开状态把树展开成一列行（同级之间按 Sort 排序，MultiSort 时按 Sorts；ManualSort 时按数据里的顺序），以及第一列前面的缩进与展开按钮。
/// <list type="bullet">
/// <item>懒加载：<see cref="HasChildren"/> 说有子项、<see cref="GetChildren"/> 还没有时，展开会调用 <see cref="LoadChildren"/>（加载中按钮显示加载指示），
/// 调用方把取到的子项放进数据里，任务结束后表格重新展开；失败时这一行收回去并报告 <see cref="LoadError"/>。数据在别处变了时调 <see cref="Refresh"/>。</item>
/// <item>多选时勾选父子联动：勾上父行连带子孙，子行全勾上时父行也勾上，只勾一部分时父行半选；<see cref="CheckStrictly"/> 时各自勾选。</item>
/// <item>展开按钮的名称是"行名 的子项"（第一列的文字），读屏念展开与否。层级由缩进与按钮表达（同上游，不是 treegrid：那要求逐格的方向键导航）。</item>
/// </list>
/// 部件：DataTable 的全部部件（<see cref="DataTableParts"/>），加上 <see cref="TreeDataTableParts"/>。
/// </summary>
public class TreeDataTable : DataTable
{
    /// <summary>一行的子项；叶子或还没加载时返回 null 或空。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> GetChildrenProperty = AvaloniaProperty.Register<TreeDataTable, Func<object, IEnumerable?>?>(nameof(GetChildren));

    /// <summary>这一行有没有子项（懒加载时子项还没取回来也要显示展开按钮）；不写时看 GetChildren 是否非空。</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasChildrenProperty = AvaloniaProperty.Register<TreeDataTable, Func<object, bool>?>(nameof(HasChildren));

    /// <summary>展开一个还没有子项的行时调用：取回子项并放进数据里；返回的任务结束前按钮显示加载中。</summary>
    public static readonly StyledProperty<Func<object, Task?>?> LoadChildrenProperty = AvaloniaProperty.Register<TreeDataTable, Func<object, Task?>?>(nameof(LoadChildren));

    /// <summary>展开的行的 key（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ExpandedProperty =
        AvaloniaProperty.Register<TreeDataTable, IReadOnlyList<string>>(nameof(Expanded), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>每一层缩进多少（em），默认 1.25。</summary>
    public static readonly StyledProperty<double> IndentProperty = AvaloniaProperty.Register<TreeDataTable, double>(nameof(Indent), 1.25);

    /// <summary>多选时勾选父子不联动：每一行各自勾选。</summary>
    public static readonly StyledProperty<bool> CheckStrictlyProperty = AvaloniaProperty.Register<TreeDataTable, bool>(nameof(CheckStrictly));

    readonly TreeExpansion<object> expansion;
    Dictionary<string, FlatTreeNode<object>> byKey = [];
    Linkage? linkage;
    LinkState? states;

    /// <summary>新建。</summary>
    public TreeDataTable()
    {
        expansion = new TreeExpansion<object>(() => Expanded, keys => SetCurrentValue(ExpandedProperty, keys), ChildrenOf)
        {
            LoadChildren = node => LoadChildren?.Invoke(node),
            LoadError = (error, node) =>
            {
                if (LoadError is { } handler) handler(this, new TreeLoadErrorEventArgs(error, node));
                else System.Diagnostics.Trace.TraceError($"TreeDataTable 的子项加载失败：{error}");
            },
        };
        // 开始加载时按钮换成加载指示；加载完时取回的子项放进了数据里，重新展开
        expansion.LoadingChanged += Refresh;
    }

    /// <summary>一行的子项。</summary>
    public Func<object, IEnumerable?>? GetChildren { get => GetValue(GetChildrenProperty); set => SetValue(GetChildrenProperty, value); }

    /// <summary>这一行有没有子项。</summary>
    public Func<object, bool>? HasChildren { get => GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }

    /// <summary>懒加载子项。</summary>
    public Func<object, Task?>? LoadChildren { get => GetValue(LoadChildrenProperty); set => SetValue(LoadChildrenProperty, value); }

    /// <summary>展开的行。</summary>
    public IReadOnlyList<string> Expanded { get => GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }

    /// <summary>每一层的缩进（em）。</summary>
    public double Indent { get => GetValue(IndentProperty); set => SetValue(IndentProperty, value); }

    /// <summary>勾选父子不联动。</summary>
    public bool CheckStrictly { get => GetValue(CheckStrictlyProperty); set => SetValue(CheckStrictlyProperty, value); }

    /// <summary>子项加载失败（LoadChildren 的任务出错）：这一行收回去，再展开会重试；没有订阅时写进跟踪日志。</summary>
    public event EventHandler<TreeLoadErrorEventArgs>? LoadError;

    /// <summary>数据变了（取回了子项、改了某一行）：重新展开成行。</summary>
    public void Refresh()
    {
        // 表体重建会换掉各行：焦点在展开按钮上时（键盘展开）跟到新的那个按钮上
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is IconButton { Name: TreeDataTableParts.Toggle, Tag: string key } ? key : null;
        base.Refresh(header: false);
        if (focused is null) return;
        Dispatcher.UIThread.Post(() => ToggleOf(focused)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    /// <summary>某一行的展开按钮（测试用）；没建或是叶子时为 null。</summary>
    internal IconButton? ToggleOf(string key) =>
        Surface.Realized.Values.FirstOrDefault(row => row.Row is { } data && KeyText(data) == key)?
            .GetVisualDescendants().OfType<IconButton>().FirstOrDefault(button => button.Name == TreeDataTableParts.Toggle);

    TreeAccessors<object> Accessors => new((node, _) => KeyText(node), ChildrenOf, HasChildren);

    IReadOnlyList<object>? ChildrenOf(object node) => GetChildren?.Invoke(node)?.Cast<object>().ToList();

    /// <summary>按展开状态深度优先展开成一列；同级之间按当前排序（ManualSort 时按数据里的顺序）。</summary>
    internal override IReadOnlyList<object> Ordered(IReadOnlyList<object> roots)
    {
        var sorts = ActiveSorts;
        var flat = TreeData.Flatten(roots, Accessors, Expanded.ToHashSet(),
            order: ManualSort ? null : level => TableColumns.SortOrder(level, Columns, sorts, Lang.Code) ?? [.. Enumerable.Range(0, level.Count)]);
        byKey = [];
        foreach (var item in flat) byKey[item.Key] = item;
        // 数据可能变了：联动的索引下次用到时重建
        (linkage, states) = (null, null);
        return [.. flat.Select(item => item.Node)];
    }

    /// <summary>缩进（层级 × Indent em）与展开按钮；叶子用同样宽的占位让文字对齐。</summary>
    internal override Control? LeadingOf(object row, int index)
    {
        var key = KeyText(row);
        var item = byKey.GetValueOrDefault(key);
        var leading = new Flex { Align = FlexAlign.Center, Gap = Space.Xs };
        var depth = item?.Depth ?? 0;
        if (depth > 0) leading.Children.Add(Em(new Control(), depth * Indent));
        if (item?.Expandable != true)
        {
            leading.Children.Add(Em(new Control { Name = TreeDataTableParts.Placeholder }, 1.5));
            return leading;
        }
        var open = expansion.IsExpanded(key);
        var toggle = DataTableRowView.DisclosureToggle(TreeDataTableParts.Toggle, Lang.TreeToggle(DataTableRowView.LabelOf(this, row)));
        toggle.Tag = key;
        toggle.IsLoading = expansion.IsLoading(key);
        toggle.VerticalAlignment = VerticalAlignment.Center;
        DataTableRowView.SetDisclosure(toggle, open, FlowDirection);
        toggle.Click += (_, _) => _ = expansion.SetOpen(row, key, !open);
        leading.Children.Add(Em(toggle, 1.5));
        return leading;

        // 宽度按这一格的字号算（同上游的 em）
        static Control Em(Control control, double width)
        {
            control.AttachedToVisualTree += (_, _) => control.Width = width * TextElement.GetFontSize(control);
            return control;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // 联动的状态先作废，基类随后重画勾选框时按新的算
        if (change.Property == SelectedProperty) states = null;
        else if (change.Property == SelectionModeProperty || change.Property == CheckStrictlyProperty) (linkage, states) = (null, null);
        base.OnPropertyChanged(change);
        if (change.Property == ExpandedProperty || change.Property == GetChildrenProperty || change.Property == HasChildrenProperty || change.Property == IndentProperty) Refresh();
        else if (change.Property == CheckStrictlyProperty) Surface.SyncRows();
    }

    // ---- 多选的父子联动 ----

    /// <summary>整棵已加载的树的索引（含折叠着的行）与 key 的文字 → 原来的 key；不联动时为 null。</summary>
    sealed record Linkage(TreeIndex Index, Dictionary<string, object> Keys);

    /// <summary>按 Selected 推出的勾选状态与显示为勾上的 key。</summary>
    sealed record LinkState(IReadOnlyDictionary<string, CheckState> States, HashSet<object> Checked);

    Linkage? Linked
    {
        get
        {
            if (SelectionMode != Rune.Avalonia.SelectionMode.Multiple || CheckStrictly) return null;
            if (linkage is not null) return linkage;
            var roots = ItemsSource?.Cast<object>().ToList() ?? [];
            var keys = new Dictionary<string, object>();
            void Walk(IReadOnlyList<object> level)
            {
                foreach (var node in level)
                {
                    keys[KeyText(node)] = KeyOf(node);
                    if (ChildrenOf(node) is { } children) Walk(children);
                }
            }
            Walk(roots);
            return linkage = new Linkage(TreeData.Index(roots, Accessors), keys);
        }
    }

    LinkState? States
    {
        get
        {
            if (Linked is not { } linked) return null;
            if (states is not null) return states;
            var map = TreeData.CheckStates(linked.Index, [.. Selected.Select(Text)]);
            var checkedKeys = map.Where(pair => pair.Value == CheckState.Checked).Select(pair => linked.Keys.GetValueOrDefault(pair.Key, pair.Key)).ToHashSet();
            return states = new LinkState(map, checkedKeys);
        }
    }

    static string Text(object key) => Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? "";

    /// <inheritdoc />
    internal override IReadOnlyCollection<object> CheckedKeys => States?.Checked ?? base.CheckedKeys;

    /// <inheritdoc />
    internal override bool IsPartial(object row) => States?.States.GetValueOrDefault(KeyText(row)) == CheckState.Indeterminate;

    /// <summary>只变了一行时按它联动子孙与祖先；全选、取消全选原样写回。</summary>
    internal override IReadOnlyList<object> LinkSelection(IReadOnlyList<object> next)
    {
        if (Linked is not { } linked) return next;
        var before = CheckedKeys.Select(Text).ToList();
        var after = next.Select(Text).ToHashSet();
        var had = before.ToHashSet();
        List<string> changed = [.. after.Where(key => !had.Contains(key)), .. before.Where(key => !after.Contains(key))];
        if (changed.Count != 1) return next;
        return [.. TreeData.ToggleChecked(linked.Index, before, changed[0]).Select(key => linked.Keys.GetValueOrDefault(key, key))];
    }
}

/// <summary>TreeDataTable 在 DataTable 之外的部件名。</summary>
public static class TreeDataTableParts
{
    /// <summary>展开按钮（TS 的 propsToggle，IconButton）。</summary>
    public const string Toggle = "PART_Toggle";

    /// <summary>叶子行的占位（与展开按钮同宽，让文字对齐）。</summary>
    public const string Placeholder = "PART_Placeholder";
}
