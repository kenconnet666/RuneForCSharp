using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 树（vue-tsx-pro 的 Tree，WAI-ARIA Tree）：可展开的层级列表（文件夹、组织架构、分类），可单选 / 多选、勾选（父子联动、半选）、懒加载子节点。
/// 数据驱动，始终虚拟滚动（上万个节点也只建看得见的几十行）。
/// <code>
/// &lt;rune:Tree ItemsSource="{Binding Folders}" GetKey="{Binding KeyOf}" GetLabel="{Binding NameOf}" GetChildren="{Binding ChildrenOf}"
///            Expanded="{Binding Open}" SelectionMode="single" Selected="{Binding Current}" AutomationProperties.Name="目录" /&gt;
/// </code>
/// 节点是任意对象，结构由 <see cref="GetKey"/>、<see cref="GetLabel"/>、<see cref="GetChildren"/> 读取（同 <see cref="TreeData"/>）。
/// <list type="bullet">
/// <item>键盘：焦点在树上。↑↓ 移动，→ 展开 / 进入第一个子节点，← 收起 / 回到父节点（从右到左排版时对调），Home / End，Enter 选中（没有选择时展开或收起），
/// 空格勾选（<see cref="Checkable"/> 时）或选中，按字母跳到以它开头的节点。</item>
/// <item>勾选：<see cref="Checked"/> 是勾上的全部节点；勾上父节点会勾上它的子孙，子节点全勾上时父节点也勾上，只勾了一部分时父节点半选；
/// <see cref="CheckStrictly"/> 时各自勾选。</item>
/// <item>懒加载：<see cref="HasChildren"/> 说有子节点、<see cref="GetChildren"/> 还没有时，展开会调用 <see cref="LoadChildren"/>（加载中显示加载指示），
/// 失败时收回去并报告 <see cref="LoadError"/>。数据变了（取回了子节点）后调 <see cref="Refresh"/> 或换一个 ItemsSource。</item>
/// <item>过滤：<see cref="Filter"/>（按文字，忽略大小写）或 <see cref="FilterBy"/>：留下匹配的节点与祖先、展开祖先，匹配的加粗；清空后回到原来的展开状态。</item>
/// <item>移动：订阅了 <see cref="Moved"/> 才能拖动节点（或 Alt + 方向键）改变层级与顺序，用 <see cref="TreeData.Move"/> 生成新数据交回 ItemsSource；<see cref="CanDrop"/> 限制落点。</item>
/// </list>
/// 读屏：树（名称写 AutomationProperties.Name），每项是树节点（展开 / 收起、选中、勾选的模式），层级与同级位置写在说明里。部件：<see cref="TreeParts"/>。
/// </summary>
public class Tree : Control
{
    /// <summary>第一层的节点。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Tree, IEnumerable?>(nameof(ItemsSource));

    /// <summary>节点的 key，整棵树里唯一（必填）。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetKeyProperty = AvaloniaProperty.Register<Tree, Func<object, string>?>(nameof(GetKey));

    /// <summary>节点的文字：默认显示它，按字母跳转、过滤也按它；不写时是 ToString()。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetLabelProperty = AvaloniaProperty.Register<Tree, Func<object, string>?>(nameof(GetLabel));

    /// <summary>子节点；叶子或还没加载时返回 null 或空。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> GetChildrenProperty = AvaloniaProperty.Register<Tree, Func<object, IEnumerable?>?>(nameof(GetChildren));

    /// <summary>有没有子节点（懒加载时子节点还没取回来也要能展开）；不写时看 GetChildren 是否非空。</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasChildrenProperty = AvaloniaProperty.Register<Tree, Func<object, bool>?>(nameof(HasChildren));

    /// <summary>展开一个还没有子节点的节点时调用：取回子节点并放进数据里；返回的任务结束前显示加载中。</summary>
    public static readonly StyledProperty<Func<object, Task?>?> LoadChildrenProperty = AvaloniaProperty.Register<Tree, Func<object, Task?>?>(nameof(LoadChildren));

    /// <summary>节点不可用：可以经过、展开，不能选中、勾选。</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsItemDisabledProperty = AvaloniaProperty.Register<Tree, Func<object, bool>?>(nameof(IsItemDisabled));

    /// <summary>文字前的图标，可以按展开状态换（文件夹开 / 合）。</summary>
    public static readonly StyledProperty<Func<object, bool, IconData?>?> GetIconProperty = AvaloniaProperty.Register<Tree, Func<object, bool, IconData?>?>(nameof(GetIcon));

    /// <summary>展开的节点（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ExpandedProperty = AvaloniaProperty.Register<Tree, IReadOnlyList<string>>(nameof(Expanded), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>选择方式；不写时不能选中。</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<Tree, SelectionMode>(nameof(SelectionMode));

    /// <summary>选中的节点（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> SelectedProperty = AvaloniaProperty.Register<Tree, IReadOnlyList<string>>(nameof(Selected), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>每项前显示勾选框，默认父子联动。</summary>
    public static readonly StyledProperty<bool> CheckableProperty = AvaloniaProperty.Register<Tree, bool>(nameof(Checkable));

    /// <summary>勾选父子不联动：每个节点各自勾选。</summary>
    public static readonly StyledProperty<bool> CheckStrictlyProperty = AvaloniaProperty.Register<Tree, bool>(nameof(CheckStrictly));

    /// <summary>勾上的节点（联动时含勾上的父节点与子孙；默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> CheckedProperty = AvaloniaProperty.Register<Tree, IReadOnlyList<string>>(nameof(Checked), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>正在取节点：还没有节点时显示骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Tree, bool>(nameof(IsLoading));

    /// <summary>没有节点时显示的内容；不写时是 Empty（文案取语言包）。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Tree, object?>(nameof(Empty));

    /// <summary>每一层缩进多少（em），默认 1.25。</summary>
    public static readonly StyledProperty<double> IndentProperty = AvaloniaProperty.Register<Tree, double>(nameof(Indent), 1.25);

    /// <summary>按文字过滤（忽略大小写）：留下匹配的节点与祖先。</summary>
    public static readonly StyledProperty<string?> FilterProperty = AvaloniaProperty.Register<Tree, string?>(nameof(Filter));

    /// <summary>自己判断的过滤；写了时代替 Filter。</summary>
    public static readonly StyledProperty<Func<object, bool>?> FilterByProperty = AvaloniaProperty.Register<Tree, Func<object, bool>?>(nameof(FilterBy));

    /// <summary>能不能放到这里（如文件不能放进文件里）；不写时都能放（放进自己和自己的子孙里除外）。</summary>
    public static readonly StyledProperty<Func<object, TreeMoveTarget, bool>?> CanDropProperty = AvaloniaProperty.Register<Tree, Func<object, TreeMoveTarget, bool>?>(nameof(CanDrop));

    /// <summary>文字部分的数据模板（TS 的 renderItem）：拿到节点。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Tree, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>字号：缩进、行高按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<Tree>();

    readonly ScrollArea scroll = new() { Name = TreeParts.Scroll, IsViewportFocusable = false };
    readonly ItemsControl rows = new() { Name = TreeParts.Rows };
    readonly StackPanel skeleton = new() { Name = TreeParts.Skeleton, IsVisible = false };
    readonly ContentPresenter empty = new() { Name = TreeParts.Empty, IsVisible = false };
    readonly LiveRegion live = new();
    readonly DelayedLoading loading;
    readonly Typeahead typeahead = new();
    TreeExpansion<object> expansion;
    List<FlatTreeNode<object>> flat = [];
    Dictionary<string, int> rowOf = [];
    TreeIndex? index;
    IReadOnlyDictionary<string, CheckState>? states;
    TreeFilter? filtering;
    IReadOnlyList<string> filterExpanded = [];
    string? active;
    bool focused;
    string? dragging;
    (string Key, TreeDropZone Zone)? drop;
    EventHandler<TreeMovedEventArgs>? moved;

    /// <summary>新建：滚动区域里一列虚拟滚动的行；没有节点时是骨架或空状态。</summary>
    public Tree()
    {
        expansion = NewExpansion();
        loading = new DelayedLoading(shown =>
        {
            skeleton.IsVisible = shown && flat.Count == 0;
            UpdateBody();
        });
        rows.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        rows.ItemTemplate = new FuncDataTemplate<FlatTreeNode<object>>((_, _) => new TreeItem(this), supportsRecycling: true);
        for (var row = 0; row < 4; row++)
            skeleton.Children.Add(new Skeleton { Delay = row * 100, HorizontalAlignment = HorizontalAlignment.Left, Tag = 0.85 - row * 0.15 });
        scroll.Content = new StackPanel { Children = { rows, skeleton, empty } };
        LogicalChildren.AddRange([scroll, live]);
        VisualChildren.AddRange([scroll, live]);
        Focusable = true;
        AutomationProperties.SetAccessibilityView(live, AccessibilityView.Raw);
    }

    /// <summary>第一层的节点。</summary>
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

    /// <summary>节点不可用。</summary>
    public Func<object, bool>? IsItemDisabled { get => GetValue(IsItemDisabledProperty); set => SetValue(IsItemDisabledProperty, value); }

    /// <summary>文字前的图标。</summary>
    public Func<object, bool, IconData?>? GetIcon { get => GetValue(GetIconProperty); set => SetValue(GetIconProperty, value); }

    /// <summary>展开的节点。</summary>
    public IReadOnlyList<string> Expanded { get => GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>选中的节点。</summary>
    public IReadOnlyList<string> Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    /// <summary>显示勾选框。</summary>
    public bool Checkable { get => GetValue(CheckableProperty); set => SetValue(CheckableProperty, value); }

    /// <summary>勾选父子不联动。</summary>
    public bool CheckStrictly { get => GetValue(CheckStrictlyProperty); set => SetValue(CheckStrictlyProperty, value); }

    /// <summary>勾上的节点。</summary>
    public IReadOnlyList<string> Checked { get => GetValue(CheckedProperty); set => SetValue(CheckedProperty, value); }

    /// <summary>正在取节点。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>空状态。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>每一层缩进多少（em）。</summary>
    public double Indent { get => GetValue(IndentProperty); set => SetValue(IndentProperty, value); }

    /// <summary>按文字过滤。</summary>
    public string? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>自己判断的过滤。</summary>
    public Func<object, bool>? FilterBy { get => GetValue(FilterByProperty); set => SetValue(FilterByProperty, value); }

    /// <summary>能不能放到这里。</summary>
    public Func<object, TreeMoveTarget, bool>? CanDrop { get => GetValue(CanDropProperty); set => SetValue(CanDropProperty, value); }

    /// <summary>文字部分的数据模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>子节点加载失败：节点收回去，再展开会重试；没有订阅时写进跟踪日志。</summary>
    public event EventHandler<TreeLoadErrorEventArgs>? LoadError;

    /// <summary>拖动（或 Alt + 方向键）移动了节点：用 <see cref="TreeData.Move"/> 生成新数据交回 ItemsSource。订阅了才能拖动。</summary>
    public event EventHandler<TreeMovedEventArgs>? Moved
    {
        add => moved += value;
        remove => moved -= value;
    }

    /// <summary>展开成一列的行（测试用）。</summary>
    internal IReadOnlyList<FlatTreeNode<object>> Rows => flat;

    /// <summary>已经建出来的行（测试用）。</summary>
    internal IEnumerable<TreeItem> RealizedItems => rows.GetRealizedContainers().Select(container => (container as ContentPresenter)?.Child).OfType<TreeItem>();

    /// <summary>键盘所在的当前项（测试用）。</summary>
    internal string? Active => active;

    /// <summary>骨架与空状态（测试用）。</summary>
    internal (Control Skeleton, ContentPresenter Empty) Placeholders => (skeleton, empty);

    /// <summary>滚动区域（测试用）。</summary>
    internal ScrollArea ScrollPart => scroll;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    /// <summary>键盘高亮只在树有焦点时。</summary>
    internal bool IsHighlighted(string key) => focused && active == key;

    TreeAccessors<object> Accessors => new((node, _) => GetKey?.Invoke(node) ?? node.ToString() ?? "", node => GetChildren?.Invoke(node)?.Cast<object>().ToList(), HasChildren);

    string LabelOf(object node) => GetLabel?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> Roots => ItemsSource?.Cast<object>().ToList() ?? [];

    IReadOnlyList<string> ShownExpanded => filtering is null ? Expanded : filterExpanded;

    TreeExpansion<object> NewExpansion()
    {
        var created = new TreeExpansion<object>(() => ShownExpanded, keys =>
        {
            if (filtering is null) SetCurrentValue(ExpandedProperty, keys);
            else
            {
                filterExpanded = keys;
                Rebuild();
            }
        }, node => GetChildren?.Invoke(node)?.Cast<object>().ToList())
        {
            LoadChildren = node => LoadChildren?.Invoke(node),
            LoadError = (error, node) =>
            {
                if (LoadError is { } handler) handler(this, new TreeLoadErrorEventArgs(error, node));
                else System.Diagnostics.Trace.TraceError($"Tree 的子节点加载失败：{error}");
            },
        };
        created.LoadingChanged += RefreshRows;
        return created;
    }

    /// <summary>数据变了（取回了子节点、改了节点）：重新展开成行。</summary>
    public void Refresh() => Rebuild();

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Refilter();
        UpdateMetrics();
        loading.Update(IsLoading);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!((global::Avalonia.LogicalTree.ILogical)this).IsAttachedToLogicalTree) return;
        if (change.Property == ItemsSourceProperty || change.Property == GetKeyProperty || change.Property == GetChildrenProperty || change.Property == HasChildrenProperty
            || change.Property == FilterProperty || change.Property == FilterByProperty || change.Property == GetLabelProperty) Refilter();
        else if (change.Property == ExpandedProperty || change.Property == CheckableProperty || change.Property == CheckStrictlyProperty) Rebuild();
        else if (change.Property == CheckedProperty)
        {
            states = index is null ? null : TreeData.CheckStates(index, Checked);
            RefreshRows();
        }
        else if (change.Property == SelectedProperty || change.Property == SelectionModeProperty || change.Property == IndentProperty || change.Property == GetIconProperty
                 || change.Property == IsItemDisabledProperty || change.Property == ItemTemplateProperty) RefreshRows();
        else if (change.Property == IsLoadingProperty) loading.Update(IsLoading);
        else if (change.Property == EmptyProperty) UpdateBody();
        else if (change.Property == FontSizeProperty || change.Property == HeightProperty || change.Property == MaxHeightProperty) UpdateMetrics();
    }

    /// <summary>过滤变了：算匹配的节点；一开始展开匹配项的祖先（可以再收起），清空后回到原来的 Expanded。</summary>
    void Refilter()
    {
        var match = FilterBy ?? (Filter is { } text && text.Trim() is { Length: > 0 } query
            ? node => LabelOf(node).Contains(query, StringComparison.CurrentCultureIgnoreCase)
            : null);
        filtering = match is null ? null : TreeData.Filter(Roots, Accessors, match);
        filterExpanded = filtering is null ? [] : [.. filtering.Expand];
        Rebuild();
    }

    /// <summary>按展开状态展开成行；勾选联动要整棵树的索引；当前项所在的父节点被收起时移到还看得见的最近祖先。</summary>
    void Rebuild()
    {
        var previous = flat;
        flat = TreeData.Flatten(Roots, Accessors, ShownExpanded.ToHashSet(), filtering?.Include);
        rowOf = flat.Select((row, position) => (row.Key, position)).ToDictionary(pair => pair.Key, pair => pair.position);
        index = Checkable && !CheckStrictly ? TreeData.Index(Roots, Accessors) : null;
        states = index is null ? null : TreeData.CheckStates(index, Checked);
        if (active is not null && !rowOf.ContainsKey(active))
        {
            var parent = previous.FirstOrDefault(row => row.Key == active)?.Parent;
            while (parent is not null && !rowOf.ContainsKey(parent)) parent = previous.FirstOrDefault(row => row.Key == parent)?.Parent;
            active = parent ?? flat.FirstOrDefault()?.Key;
        }
        rows.ItemsSource = flat;
        skeleton.IsVisible = IsLoading && flat.Count == 0 && skeleton.IsVisible;
        UpdateBody();
        RefreshRows();
    }

    /// <summary>已经建出来的行按状态重画。</summary>
    void RefreshRows()
    {
        foreach (var item in RealizedItems) item.Sync();
    }

    /// <summary>没有节点时：加载中是骨架行，否则是空状态。</summary>
    void UpdateBody()
    {
        var none = flat.Count == 0;
        rows.IsVisible = !none;
        skeleton.IsVisible = none && IsLoading && skeleton.IsVisible;
        empty.IsVisible = none && !IsLoading;
        empty.Content = Empty ?? new Empty { Size = TextSize.Sm, Icon = null, Description = filtering is null ? Provider.GetLang(this).TreeEmpty : Provider.GetLang(this).TreeNoMatch };
    }

    /// <summary>四周 xs 的边；不写 Height 时最高 20em（超出时滚动）；骨架行 1em 高、上下 0.625em、宽度逐行递减。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        ((Panel)scroll.Content!).Margin = new Thickness(xs);
        scroll.MaxHeight = double.IsNaN(Height) && double.IsInfinity(MaxHeight) ? 20 * em : double.PositiveInfinity;
        foreach (var bar in skeleton.Children.OfType<Skeleton>())
        {
            bar.Height = em;
            bar.Margin = new Thickness(sm, 0.625 * em);
        }
        RefreshRows();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 骨架逐行变短：85%、70%……
        foreach (var bar in skeleton.Children.OfType<Skeleton>()) bar.Width = Math.Max(0, (double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width) * (double)bar.Tag!);
        scroll.Measure(availableSize);
        return scroll.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        scroll.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>某一行的状态（行自己读）。</summary>
    internal TreeItemState StateOf(FlatTreeNode<object> row) => new(
        row.Depth,
        row.Expandable && expansion.IsExpanded(row.Key),
        Selected.Contains(row.Key),
        states?.GetValueOrDefault(row.Key) ?? (Checked.Contains(row.Key) ? CheckState.Checked : CheckState.Unchecked),
        active == row.Key,
        IsItemDisabled?.Invoke(row.Node) ?? false,
        expansion.IsLoading(row.Key),
        filtering?.Matched.Contains(row.Key) == true,
        dragging == row.Key,
        drop is { } spot && spot.Key == row.Key ? spot.Zone : null);

    internal (string Label, IconData? Icon, IDataTemplate? Template) ContentOf(FlatTreeNode<object> row, bool expanded) =>
        (LabelOf(row.Node), GetIcon?.Invoke(row.Node, expanded), ItemTemplate);

    int ActiveRow => active is not null && rowOf.TryGetValue(active, out var position) ? position : -1;

    /// <summary>当前项换了：滚到看得见，行重画。</summary>
    void SetActive(string? key)
    {
        active = key;
        if (ActiveRow is var position and >= 0) rows.ScrollIntoView(position);
        RefreshRows();
    }

    bool DisabledAt(int position) => position < 0 || position >= flat.Count || (IsItemDisabled?.Invoke(flat[position].Node) ?? false);

    internal void Select(int position)
    {
        if (SelectionMode.Value is null || DisabledAt(position)) return;
        SetCurrentValue(SelectedProperty, Selection.Toggle(Selected, flat[position].Key, SelectionMode, required: false));
        RefreshRows();
    }

    internal void Check(int position)
    {
        if (!Checkable || DisabledAt(position)) return;
        var key = flat[position].Key;
        SetCurrentValue(CheckedProperty, index is not null ? TreeData.ToggleChecked(index, Checked, key) : Selection.Toggle(Checked, key, SelectionMode.Multiple, required: false));
    }

    internal void Toggle(int position)
    {
        if (position < 0 || position >= flat.Count || !flat[position].Expandable) return;
        var row = flat[position];
        _ = expansion.SetOpen(row.Node, row.Key, !expansion.IsExpanded(row.Key));
    }

    /// <summary>点了一行：成为当前项、焦点在树上；能选时选中，能勾时勾选，否则展开或收起。</summary>
    internal void Click(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        SetActive(key);
        Focus(NavigationMethod.Pointer);
        if (SelectionMode.Value is not null) Select(position);
        else if (Checkable) Check(position);
        else Toggle(position);
    }

    /// <summary>点了展开箭头、勾选框：只做那一件事。</summary>
    internal void ClickToggle(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        SetActive(key);
        Focus(NavigationMethod.Pointer);
        Toggle(position);
    }

    internal void ClickCheck(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        SetActive(key);
        Focus(NavigationMethod.Pointer);
        Check(position);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focused = true;
        // 第一次进入：落在第一个已选的可见节点上，没有时落在第一项
        if (active is null || !rowOf.ContainsKey(active)) active = flat.FirstOrDefault(row => Selected.Contains(row.Key))?.Key ?? flat.FirstOrDefault()?.Key;
        SetActive(active);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focused = false;
        RefreshRows();
    }

    /// <summary>方向键、Home / End、Enter、空格；Alt + 方向键移动节点。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.KeyModifiers == KeyModifiers.Alt && moved is not null)
        {
            e.Handled = MoveByKey(e.Key);
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None || flat.Count == 0) return;
        var position = Math.Max(ActiveRow, 0);
        var row = flat[position];
        var open = row.Expandable && expansion.IsExpanded(row.Key);
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var (forward, backward) = rtl ? (Key.Left, Key.Right) : (Key.Right, Key.Left);
        void Go(int target) => SetActive(flat[Math.Clamp(target, 0, flat.Count - 1)].Key);
        if (e.Key == Key.Down) Go(position + 1);
        else if (e.Key == Key.Up) Go(position - 1);
        else if (e.Key == Key.Home) Go(0);
        else if (e.Key == Key.End) Go(flat.Count - 1);
        else if (e.Key == forward)
        {
            if (!row.Expandable) { }
            else if (!open) _ = expansion.SetOpen(row.Node, row.Key, true);
            // 已展开：进入第一个子节点（子节点还没加载出来时不动）
            else if (position + 1 < flat.Count && flat[position + 1].Parent == row.Key) Go(position + 1);
        }
        else if (e.Key == backward)
        {
            if (open) _ = expansion.SetOpen(row.Node, row.Key, false);
            else if (row.Parent is { } parent) SetActive(parent);
        }
        else if (e.Key == Key.Enter)
        {
            if (SelectionMode.Value is not null) Select(position);
            else Toggle(position);
        }
        else if (e.Key == Key.Space)
        {
            if (Checkable) Check(position);
            else Select(position);
        }
        else return;
        e.Handled = true;
    }

    /// <summary>按字母跳到以它开头的节点。</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || e.Text is not { Length: 1 } text || text == " ") return;
        var found = typeahead.Next(text, [.. flat.Select(row => LabelOf(row.Node))], ActiveRow, Environment.TickCount64);
        if (found < 0) return;
        SetActive(flat[found].Key);
        e.Handled = true;
    }

    // —— 移动节点：指针拖动，或 Alt + 方向键 ——

    FlatTreeNode<object>? RowByKey(string? key) => key is not null && rowOf.TryGetValue(key, out var position) ? flat[position] : null;

    /// <summary>key 是不是 ancestor 自己或它的子孙（看得见的行里，祖先总是看得见的）。</summary>
    bool IsWithin(string key, string ancestor)
    {
        for (var at = key; at is not null; at = RowByKey(at)?.Parent)
            if (at == ancestor) return true;
        return false;
    }

    /// <summary>落点换算成移动的目标（按移走之后的同级算）；放不下时 null。</summary>
    TreeMoveTarget? TargetOf(string key, string spotKey, TreeDropZone zone)
    {
        if (RowByKey(key) is not { } moving || RowByKey(spotKey) is not { } row || IsWithin(spotKey, key)) return null;
        TreeMoveTarget target;
        if (zone == TreeDropZone.Inside) target = new TreeMoveTarget(row.Key, int.MaxValue);
        else
        {
            var offset = zone == TreeDropZone.Before ? row.Position - 1 : row.Position;
            // 同一层里从前面移过来：移走之后，后面的都往前挪了一位
            var shift = moving.Parent == row.Parent && moving.Position <= offset ? 1 : 0;
            target = new TreeMoveTarget(row.Parent, offset - shift);
        }
        return CanDrop is { } can && !can(moving.Node, target) ? null : target;
    }

    /// <summary>报告移动，并告诉读屏移到了哪里。</summary>
    void EmitMove(string key, TreeMoveTarget target)
    {
        if (RowByKey(key) is not { } row || moved is null) return;
        var parent = RowByKey(target.Parent);
        // 移走之后的同级个数：同一层里移动时要去掉自己
        var count = (parent is null ? Roots.Count : GetChildren?.Invoke(parent.Node)?.Cast<object>().Count() ?? 0) - (row.Parent == target.Parent ? 1 : 0);
        moved(this, new TreeMovedEventArgs(key, row.Node, target));
        live.Announce(Provider.GetLang(this).TreeMoved(LabelOf(row.Node), parent is null ? null : LabelOf(parent.Node), Math.Min(target.Index, count) + 1));
    }

    /// <summary>Alt + ↑↓ 在同级里移动，Alt + → 放进上一个同级的最后，Alt + ← 移到父节点后面（从右到左时左右对调）。</summary>
    bool MoveByKey(Key key)
    {
        if (ActiveRow < 0) return false;
        var row = flat[ActiveRow];
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var (forward, backward) = rtl ? (Key.Left, Key.Right) : (Key.Right, Key.Left);
        TreeMoveTarget? target = null;
        if (key == Key.Up)
        {
            if (row.Position > 1) target = new TreeMoveTarget(row.Parent, row.Position - 2);
        }
        else if (key == Key.Down)
        {
            if (row.Position < row.SetSize) target = new TreeMoveTarget(row.Parent, row.Position);
        }
        else if (key == forward)
        {
            if (flat.FirstOrDefault(item => item.Parent == row.Parent && item.Position == row.Position - 1) is { } previous)
            {
                target = new TreeMoveTarget(previous.Key, int.MaxValue);
                // 展开新的父节点：移过去之后还看得见
                _ = expansion.SetOpen(previous.Node, previous.Key, true);
            }
        }
        else if (key == backward)
        {
            if (RowByKey(row.Parent) is { } parent) target = new TreeMoveTarget(parent.Parent, parent.Position);
        }
        else return false;
        if (target is { } found && (CanDrop is not { } can || can(row.Node, found))) EmitMove(row.Key, found);
        return true;
    }

    /// <summary>拖动：按下的那一行；挪开 4 以上才算拖。</summary>
    Point? pressedAt;
    string? pressedKey;

    internal void Press(string key, Point at)
    {
        if (moved is null) return;
        pressedKey = key;
        pressedAt = at;
    }

    /// <summary>拖动中：指针下的那一项的上四分之一放在它前面，下四分之一放在后面，中间放进它里面；靠近上下边缘时滚动。</summary>
    internal void DragTo(Point at, IPointer pointer)
    {
        if (pressedKey is not { } key || pressedAt is not { } from) return;
        if (dragging is null)
        {
            if (Math.Abs(at.X - from.X) + Math.Abs(at.Y - from.Y) < 4) return;
            dragging = key;
            pointer.Capture(this);
        }
        TreeDropZone? zone = null;
        string? spot = null;
        if (this.InputHitTest(at) is Visual hit && hit.FindAncestorOfType<TreeItem>(includeSelf: true) is { Row: { } row } item)
        {
            var ratio = item.TranslatePoint(default, this) is { } origin ? (at.Y - origin.Y) / Math.Max(1, item.Bounds.Height) : 0.5;
            zone = ratio < 0.25 ? TreeDropZone.Before : ratio > 0.75 ? TreeDropZone.After : TreeDropZone.Inside;
            spot = row.Key;
        }
        if (at.Y < 32) scroll.Offset = scroll.Offset.WithY(Math.Max(0, scroll.Offset.Y - 12));
        else if (at.Y > Bounds.Height - 32) scroll.Offset = scroll.Offset.WithY(scroll.Offset.Y + 12);
        drop = spot is not null && zone is { } z && TargetOf(key, spot, z) is not null ? (spot, z) : null;
        RefreshRows();
    }

    internal void Release()
    {
        var spot = drop;
        var key = dragging;
        pressedKey = null;
        pressedAt = null;
        if (key is null) return;
        dragging = null;
        drop = null;
        RefreshRows();
        if (spot is { } found && TargetOf(key, found.Key, found.Zone) is { } target) EmitMove(key, target);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pressedKey is not null) DragTo(e.GetPosition(this), e.Pointer);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        Release();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TreePeer(this);

    /// <summary>读屏：树。</summary>
    sealed class TreePeer(Tree owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;
    }
}

/// <summary>落点：放在这一项的前面、后面或里面。</summary>
public enum TreeDropZone
{
    /// <summary>前面。</summary>
    Before,

    /// <summary>后面。</summary>
    After,

    /// <summary>里面（最后一个子节点）。</summary>
    Inside,
}

/// <summary>一行的渲染状态（vue-tsx-pro 的 TreeItemRenderState 加上控件自己的）。</summary>
/// <param name="Depth">层级，第一层为 0。</param>
/// <param name="Expanded">展开着。</param>
/// <param name="Selected">已选。</param>
/// <param name="Checked">勾选状态。</param>
/// <param name="Active">键盘所在的当前项。</param>
/// <param name="Disabled">不可用。</param>
/// <param name="Loading">正在加载子节点。</param>
/// <param name="Matched">过滤时匹配。</param>
/// <param name="Dragging">正在被拖。</param>
/// <param name="Drop">拖动的落点在这一项上。</param>
public readonly record struct TreeItemState(int Depth, bool Expanded, bool Selected, CheckState Checked, bool Active, bool Disabled, bool Loading, bool Matched, bool Dragging, TreeDropZone? Drop);

/// <summary><see cref="Tree.LoadError"/> 的参数。</summary>
/// <param name="error">失败的原因。</param>
/// <param name="node">要加载子节点的那个节点。</param>
public sealed class TreeLoadErrorEventArgs(Exception error, object node) : EventArgs
{
    /// <summary>失败的原因。</summary>
    public Exception Error { get; } = error;

    /// <summary>节点。</summary>
    public object Node { get; } = node;
}

/// <summary><see cref="Tree.Moved"/> 的参数（vue-tsx-pro 的 TreeMove）。</summary>
/// <param name="key">移动的节点的 key。</param>
/// <param name="node">移动的节点。</param>
/// <param name="target">放到哪个节点下的第几位（按移走之后的同级算）。</param>
public sealed class TreeMovedEventArgs(string key, object node, TreeMoveTarget target) : EventArgs
{
    /// <summary>移动的节点的 key。</summary>
    public string Key { get; } = key;

    /// <summary>移动的节点。</summary>
    public object Node { get; } = node;

    /// <summary>目标。</summary>
    public TreeMoveTarget Target { get; } = target;
}

/// <summary>Tree 的部件名。</summary>
public static class TreeParts
{
    /// <summary>外层的滚动区域（TS 的 propsScrollArea）。</summary>
    public const string Scroll = "PART_Scroll";

    /// <summary>虚拟滚动的行（ItemsControl）。</summary>
    public const string Rows = "PART_Rows";

    /// <summary>每一项（TS 的 propsItem）。</summary>
    public const string Item = "PART_Item";

    /// <summary>展开箭头（TS 的 propsToggle）。</summary>
    public const string Toggle = "PART_Toggle";

    /// <summary>勾选框（TS 的 propsCheckbox）。</summary>
    public const string Checkbox = "PART_Checkbox";

    /// <summary>文字（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";

    /// <summary>加载中的骨架行（TS 的 propsSkeleton）。</summary>
    public const string Skeleton = "PART_Skeleton";

    /// <summary>空状态（TS 的 propsEmpty）。</summary>
    public const string Empty = "PART_Empty";
}
