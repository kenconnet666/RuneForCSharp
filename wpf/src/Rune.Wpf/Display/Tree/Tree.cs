using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 读屏：树（名称写 AutomationProperties.Name），每项是树节点（展开 / 收起、选中、勾选的模式），层级与同级位置写在说明里。部件样式键：<see cref="TreeParts"/>。
/// </summary>
public class Tree : FrameworkElement
{
    /// <summary>第一层的节点。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = Register(nameof(ItemsSource), typeof(IEnumerable), null, Refilters);

    /// <summary>节点的 key，整棵树里唯一（必填）。</summary>
    public static readonly DependencyProperty GetKeyProperty = Register(nameof(GetKey), typeof(Func<object, string>), null, Refilters);

    /// <summary>节点的文字：默认显示它，按字母跳转、过滤也按它；不写时是 ToString()。</summary>
    public static readonly DependencyProperty GetLabelProperty = Register(nameof(GetLabel), typeof(Func<object, string>), null, Refilters);

    /// <summary>子节点；叶子或还没加载时返回 null 或空。</summary>
    public static readonly DependencyProperty GetChildrenProperty = Register(nameof(GetChildren), typeof(Func<object, IEnumerable?>), null, Refilters);

    /// <summary>有没有子节点（懒加载时子节点还没取回来也要能展开）；不写时看 GetChildren 是否非空。</summary>
    public static readonly DependencyProperty HasChildrenProperty = Register(nameof(HasChildren), typeof(Func<object, bool>), null, Refilters);

    /// <summary>展开一个还没有子节点的节点时调用：取回子节点并放进数据里；返回的任务结束前显示加载中。</summary>
    public static readonly DependencyProperty LoadChildrenProperty = Register(nameof(LoadChildren), typeof(Func<object, Task?>), null, null);

    /// <summary>节点不可用：可以经过、展开，不能选中、勾选。</summary>
    public static readonly DependencyProperty IsItemDisabledProperty = Register(nameof(IsItemDisabled), typeof(Func<object, bool>), null, Repaints);

    /// <summary>文字前的图标，可以按展开状态换（文件夹开 / 合）。</summary>
    public static readonly DependencyProperty GetIconProperty = Register(nameof(GetIcon), typeof(Func<object, bool, IconData?>), null, Repaints);

    /// <summary>展开的节点（默认双向绑定）。</summary>
    public static readonly DependencyProperty ExpandedProperty = DependencyProperty.Register(nameof(Expanded), typeof(IReadOnlyList<string>), typeof(Tree),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tree)d).Rebuild()));

    /// <summary>选择方式；不写时不能选中。</summary>
    public static readonly DependencyProperty SelectionModeProperty = Register(nameof(SelectionMode), typeof(SelectionMode), default(SelectionMode), Repaints);

    /// <summary>选中的节点（默认双向绑定）。</summary>
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(IReadOnlyList<string>), typeof(Tree),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tree)d).RefreshRows()));

    /// <summary>每项前显示勾选框，默认父子联动。</summary>
    public static readonly DependencyProperty CheckableProperty = Register(nameof(Checkable), typeof(bool), false, (d, _) => ((Tree)d).Rebuild());

    /// <summary>勾选父子不联动：每个节点各自勾选。</summary>
    public static readonly DependencyProperty CheckStrictlyProperty = Register(nameof(CheckStrictly), typeof(bool), false, (d, _) => ((Tree)d).Rebuild());

    /// <summary>勾上的节点（联动时含勾上的父节点与子孙；默认双向绑定）。</summary>
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register(nameof(Checked), typeof(IReadOnlyList<string>), typeof(Tree),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tree)d).OnCheckedChanged()));

    /// <summary>正在取节点：还没有节点时显示骨架行。</summary>
    public static readonly DependencyProperty IsLoadingProperty = Register(nameof(IsLoading), typeof(bool), false, (d, _) => ((Tree)d).loading.Update(((Tree)d).IsLoading));

    /// <summary>没有节点时显示的内容；不写时是 Empty（文案取语言包）。</summary>
    public static readonly DependencyProperty EmptyProperty = Register(nameof(Empty), typeof(object), null, (d, _) => ((Tree)d).UpdateBody());

    /// <summary>每一层缩进多少（em），默认 1.25。</summary>
    public static readonly DependencyProperty IndentProperty = Register(nameof(Indent), typeof(double), 1.25, Repaints);

    /// <summary>按文字过滤（忽略大小写）：留下匹配的节点与祖先。</summary>
    public static readonly DependencyProperty FilterProperty = Register(nameof(Filter), typeof(string), null, Refilters);

    /// <summary>自己判断的过滤；写了时代替 Filter。</summary>
    public static readonly DependencyProperty FilterByProperty = Register(nameof(FilterBy), typeof(Func<object, bool>), null, Refilters);

    /// <summary>能不能放到这里（如文件不能放进文件里）；不写时都能放（放进自己和自己的子孙里除外）。</summary>
    public static readonly DependencyProperty CanDropProperty = Register(nameof(CanDrop), typeof(Func<object, TreeMoveTarget, bool>), null, null);

    /// <summary>文字部分的数据模板（TS 的 renderItem）：拿到节点。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = Register(nameof(ItemTemplate), typeof(DataTemplate), null, Repaints);

    static DependencyProperty Register(string name, Type type, object? initial, PropertyChangedCallback? changed) =>
        DependencyProperty.Register(name, type, typeof(Tree), new FrameworkPropertyMetadata(initial, changed));

    static void Refilters(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Tree)d).Refilter();

    static void Repaints(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Tree)d).RefreshRows();

    readonly System.Windows.Controls.Grid root = new();
    readonly TreeRows rows;
    readonly StackPanel skeleton = new() { Visibility = Visibility.Collapsed };
    readonly ContentPresenter empty = new() { Visibility = Visibility.Collapsed };
    readonly Border focusRing = new() { BorderThickness = new Thickness(2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    readonly LiveRegion live = new();
    readonly DelayedLoading loading;
    readonly Typeahead typeahead = new();
    readonly TreeExpansion<object> expansion;
    List<FlatTreeNode<object>> flat = [];
    Dictionary<string, int> rowOf = [];
    TreeIndex? index;
    IReadOnlyDictionary<string, CheckState>? states;
    TreeFilter? filtering;
    IReadOnlyList<string> filterExpanded = [];
    string? active;
    string? dragging;
    (string Key, TreeDropZone Zone)? drop;
    EventHandler<TreeMovedEventArgs>? moved;

    /// <summary>新建：一列虚拟滚动的行；没有节点时是骨架或空状态。</summary>
    public Tree()
    {
        expansion = new TreeExpansion<object>(() => ShownExpanded, SetExpanded, node => GetChildren?.Invoke(node)?.Cast<object>().ToList())
        {
            LoadChildren = node => LoadChildren?.Invoke(node),
            LoadError = (error, node) =>
            {
                if (LoadError is { } handler) handler(this, new TreeLoadErrorEventArgs(error, node));
                else System.Diagnostics.Trace.TraceError($"Tree 的子节点加载失败：{error}");
            },
        };
        expansion.LoadingChanged += RefreshRows;
        rows = new TreeRows(this);
        rows.SetResourceReference(StyleProperty, TreeParts.Rows);
        loading = new DelayedLoading(_ => UpdateBody());
        for (var row = 0; row < 4; row++)
            skeleton.Children.Add(new Skeleton { Delay = row * 100, HorizontalAlignment = HorizontalAlignment.Left, Tag = 0.85 - row * 0.15 });
        focusRing.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.FocusRing);
        AutomationProperties.SetIsOffscreenBehavior(live, IsOffscreenBehavior.Offscreen);
        foreach (UIElement child in new UIElement[] { rows, skeleton, empty, focusRing, live }) root.Children.Add(child);
        AddLogicalChild(root);
        AddVisualChild(root);
        Focusable = true;
        FocusVisualStyle = null;
        Loaded += (_, _) =>
        {
            Refilter();
            loading.Update(IsLoading);
        };
    }

    /// <summary>第一层的节点。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>节点的 key。</summary>
    public Func<object, string>? GetKey { get => (Func<object, string>?)GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>节点的文字。</summary>
    public Func<object, string>? GetLabel { get => (Func<object, string>?)GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>子节点。</summary>
    public Func<object, IEnumerable?>? GetChildren { get => (Func<object, IEnumerable?>?)GetValue(GetChildrenProperty); set => SetValue(GetChildrenProperty, value); }

    /// <summary>有没有子节点。</summary>
    public Func<object, bool>? HasChildren { get => (Func<object, bool>?)GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }

    /// <summary>懒加载子节点。</summary>
    public Func<object, Task?>? LoadChildren { get => (Func<object, Task?>?)GetValue(LoadChildrenProperty); set => SetValue(LoadChildrenProperty, value); }

    /// <summary>节点不可用。</summary>
    public Func<object, bool>? IsItemDisabled { get => (Func<object, bool>?)GetValue(IsItemDisabledProperty); set => SetValue(IsItemDisabledProperty, value); }

    /// <summary>文字前的图标。</summary>
    public Func<object, bool, IconData?>? GetIcon { get => (Func<object, bool, IconData?>?)GetValue(GetIconProperty); set => SetValue(GetIconProperty, value); }

    /// <summary>展开的节点。</summary>
    public IReadOnlyList<string> Expanded { get => (IReadOnlyList<string>)GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => (SelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>选中的节点。</summary>
    public IReadOnlyList<string> Selected { get => (IReadOnlyList<string>)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    /// <summary>显示勾选框。</summary>
    public bool Checkable { get => (bool)GetValue(CheckableProperty); set => SetValue(CheckableProperty, value); }

    /// <summary>勾选父子不联动。</summary>
    public bool CheckStrictly { get => (bool)GetValue(CheckStrictlyProperty); set => SetValue(CheckStrictlyProperty, value); }

    /// <summary>勾上的节点。</summary>
    public IReadOnlyList<string> Checked { get => (IReadOnlyList<string>)GetValue(CheckedProperty); set => SetValue(CheckedProperty, value); }

    /// <summary>正在取节点。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>空状态。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>每一层缩进多少（em）。</summary>
    public double Indent { get => (double)GetValue(IndentProperty); set => SetValue(IndentProperty, value); }

    /// <summary>按文字过滤。</summary>
    public string? Filter { get => (string?)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>自己判断的过滤。</summary>
    public Func<object, bool>? FilterBy { get => (Func<object, bool>?)GetValue(FilterByProperty); set => SetValue(FilterByProperty, value); }

    /// <summary>能不能放到这里。</summary>
    public Func<object, TreeMoveTarget, bool>? CanDrop { get => (Func<object, TreeMoveTarget, bool>?)GetValue(CanDropProperty); set => SetValue(CanDropProperty, value); }

    /// <summary>文字部分的数据模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

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
    internal IEnumerable<TreeItem> RealizedItems => rows.Items_;

    /// <summary>键盘所在的当前项（测试用）。</summary>
    internal string? Active => active;

    /// <summary>骨架与空状态（测试用）。</summary>
    internal (FrameworkElement Skeleton, ContentPresenter Empty) Placeholders => (skeleton, empty);

    /// <summary>行的容器（测试用）。</summary>
    internal ItemsControl RowsPart => rows;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    internal double Em => TextElement.GetFontSize(this);

    /// <summary>键盘高亮只在树有键盘焦点时。</summary>
    internal bool IsHighlighted(string key) => IsKeyboardFocused && active == key;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { root }.GetEnumerator();

    TreeAccessors<object> Accessors => new((node, _) => GetKey?.Invoke(node) ?? node.ToString() ?? "", node => GetChildren?.Invoke(node)?.Cast<object>().ToList(), HasChildren);

    string LabelOf(object node) => GetLabel?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> Roots => ItemsSource?.Cast<object>().ToList() ?? [];

    IReadOnlyList<string> ShownExpanded => filtering is null ? Expanded : filterExpanded;

    void SetExpanded(IReadOnlyList<string> keys)
    {
        if (filtering is null) SetCurrentValue(ExpandedProperty, keys);
        else
        {
            filterExpanded = keys;
            Rebuild();
        }
    }

    /// <summary>数据变了（取回了子节点、改了节点）：重新展开成行。</summary>
    public void Refresh() => Rebuild();

    /// <summary>过滤变了：算匹配的节点；一开始展开匹配项的祖先（可以再收起），清空后回到原来的 Expanded。</summary>
    void Refilter()
    {
        if (!IsLoaded) return;
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
        if (!IsLoaded) return;
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
        UpdateBody();
        RefreshRows();
    }

    void OnCheckedChanged()
    {
        states = index is null ? null : TreeData.CheckStates(index, Checked);
        RefreshRows();
    }

    /// <summary>已经建出来的行按状态重画。</summary>
    internal void RefreshRows()
    {
        foreach (var item in RealizedItems) item.Sync();
    }

    /// <summary>没有节点时：加载中是骨架行（防闪烁），否则是空状态。</summary>
    void UpdateBody()
    {
        var none = flat.Count == 0;
        rows.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        skeleton.Visibility = none && IsLoading && loading.Shown ? Visibility.Visible : Visibility.Collapsed;
        empty.Visibility = none && !IsLoading ? Visibility.Visible : Visibility.Collapsed;
        var lang = Provider.GetLang(this);
        empty.Content = Empty ?? new Empty { Size = TextSize.Sm, Icon = null, Description = filtering is null ? lang.TreeEmpty : lang.TreeNoMatch };
    }

    /// <summary>四周 xs 的边；不写 Height、MaxHeight 时最高 20em（超出时滚动）；骨架行 1em 高、上下 0.625em、宽度逐行递减。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = Em;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        rows.Padding = new Thickness(xs);
        rows.MaxHeight = double.IsNaN(Height) && double.IsInfinity(MaxHeight) ? 20 * em : double.PositiveInfinity;
        foreach (var bar in skeleton.Children.OfType<Skeleton>())
        {
            bar.Height = em;
            bar.Margin = new Thickness(sm, 0.625 * em, sm, 0.625 * em);
            bar.Width = Math.Max(0, (double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width) * (double)bar.Tag);
        }
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
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

    internal (string Label, IconData? Icon, DataTemplate? Template) ContentOf(FlatTreeNode<object> row, bool expanded) =>
        (LabelOf(row.Node), GetIcon?.Invoke(row.Node, expanded), ItemTemplate);

    int ActiveRow => active is not null && rowOf.TryGetValue(active, out var position) ? position : -1;

    /// <summary>当前项换了：滚到看得见，行重画。</summary>
    void SetActive(string? key)
    {
        active = key;
        if (ActiveRow is var position and >= 0) rows.BringIntoView(position);
        RefreshRows();
    }

    bool DisabledAt(int position) => position < 0 || position >= flat.Count || (IsItemDisabled?.Invoke(flat[position].Node) ?? false);

    void Select(int position)
    {
        if (SelectionMode.Value is null || DisabledAt(position)) return;
        SetCurrentValue(SelectedProperty, Selection.Toggle(Selected, flat[position].Key, SelectionMode, required: false));
    }

    void Check(int position)
    {
        if (!Checkable || DisabledAt(position)) return;
        var key = flat[position].Key;
        SetCurrentValue(CheckedProperty, index is not null ? TreeData.ToggleChecked(index, Checked, key) : Selection.Toggle(Checked, key, SelectionMode.Multiple, required: false));
    }

    void Toggle(int position)
    {
        if (position < 0 || position >= flat.Count || !flat[position].Expandable) return;
        var row = flat[position];
        _ = expansion.SetOpen(row.Node, row.Key, !expansion.IsExpanded(row.Key));
    }

    /// <summary>点了一行：成为当前项、焦点在树上；能选时选中，能勾时勾选，否则展开或收起。</summary>
    internal void Click(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        Focus();
        SetActive(key);
        if (SelectionMode.Value is not null) Select(position);
        else if (Checkable) Check(position);
        else Toggle(position);
    }

    /// <summary>点了展开箭头：只展开或收起。</summary>
    internal void ClickToggle(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        Focus();
        SetActive(key);
        Toggle(position);
    }

    /// <summary>点了勾选框：只勾选。</summary>
    internal void ClickCheck(string key)
    {
        if (!rowOf.TryGetValue(key, out var position)) return;
        Focus();
        SetActive(key);
        Check(position);
    }

    /// <inheritdoc />
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (e.NewFocus != this) return;
        focusRing.Visibility = InputManager.Current.MostRecentInputDevice is KeyboardDevice ? Visibility.Visible : Visibility.Collapsed;
        // 第一次进入：落在第一个已选的可见节点上，没有时落在第一项
        if (active is null || !rowOf.ContainsKey(active)) active = flat.FirstOrDefault(row => Selected.Contains(row.Key))?.Key ?? flat.FirstOrDefault()?.Key;
        SetActive(active);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusRing.Visibility = Visibility.Collapsed;
        RefreshRows();
    }

    /// <summary>方向键、Home / End、Enter、空格；Alt + 方向键移动节点（Alt 时按键是 System，取 SystemKey）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && moved is not null)
        {
            e.Handled = MoveByKey(key);
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None || flat.Count == 0) return;
        var position = Math.Max(ActiveRow, 0);
        var row = flat[position];
        var open = row.Expandable && expansion.IsExpanded(row.Key);
        var (forward, backward) = FlowDirection == FlowDirection.RightToLeft ? (Key.Left, Key.Right) : (Key.Right, Key.Left);
        void Go(int target) => SetActive(flat[Math.Clamp(target, 0, flat.Count - 1)].Key);
        if (key == Key.Down) Go(position + 1);
        else if (key == Key.Up) Go(position - 1);
        else if (key == Key.Home) Go(0);
        else if (key == Key.End) Go(flat.Count - 1);
        else if (key == forward)
        {
            if (!row.Expandable) { }
            else if (!open) _ = expansion.SetOpen(row.Node, row.Key, true);
            // 已展开：进入第一个子节点（子节点还没加载出来时不动）
            else if (position + 1 < flat.Count && flat[position + 1].Parent == row.Key) Go(position + 1);
        }
        else if (key == backward)
        {
            if (open) _ = expansion.SetOpen(row.Node, row.Key, false);
            else if (row.Parent is { } parent) SetActive(parent);
        }
        else if (key == Key.Enter)
        {
            if (SelectionMode.Value is not null) Select(position);
            else Toggle(position);
        }
        else if (key == Key.Space)
        {
            if (Checkable) Check(position);
            else Select(position);
        }
        else return;
        e.Handled = true;
    }

    /// <summary>按字母跳到以它开头的节点。</summary>
    protected override void OnTextInput(TextCompositionEventArgs e)
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
    internal bool MoveByKey(Key key)
    {
        if (ActiveRow < 0) return false;
        var row = flat[ActiveRow];
        var (forward, backward) = FlowDirection == FlowDirection.RightToLeft ? (Key.Left, Key.Right) : (Key.Right, Key.Left);
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

    Point? pressedAt;
    string? pressedKey;

    /// <summary>拖动：按下的那一行；挪开 4 以上才算拖（坐标相对树）。</summary>
    internal void Press(string key, Point at)
    {
        if (moved is null) return;
        pressedKey = key;
        pressedAt = at;
    }

    /// <summary>拖动中：指针下的那一项的上四分之一放在它前面，下四分之一放在后面，中间放进它里面；靠近上下边缘时滚动。</summary>
    internal void DragTo(Point at)
    {
        if (pressedKey is not { } key || pressedAt is not { } from) return;
        if (dragging is null)
        {
            if (Math.Abs(at.X - from.X) + Math.Abs(at.Y - from.Y) < 4) return;
            dragging = key;
            CaptureMouse();
        }
        drop = null;
        if (InputHitTest(at) is DependencyObject hit && FindItem(hit) is { Row: { } row } item)
        {
            var ratio = item.TranslatePoint(default, this) is var origin ? (at.Y - origin.Y) / Math.Max(1, item.ActualHeight) : 0.5;
            var zone = ratio < 0.25 ? TreeDropZone.Before : ratio > 0.75 ? TreeDropZone.After : TreeDropZone.Inside;
            if (TargetOf(key, row.Key, zone) is not null) drop = (row.Key, zone);
        }
        if (rows.ScrollViewer is { } scroller)
        {
            if (at.Y < 32) scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 12);
            else if (at.Y > ActualHeight - 32) scroller.ScrollToVerticalOffset(scroller.VerticalOffset + 12);
        }
        RefreshRows();
    }

    static TreeItem? FindItem(DependencyObject? node)
    {
        for (; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is TreeItem item) return item;
        return null;
    }

    /// <summary>松开：放在落点上（报告移动）。</summary>
    internal void Release()
    {
        var spot = drop;
        var key = dragging;
        pressedKey = null;
        pressedAt = null;
        if (key is null) return;
        dragging = null;
        drop = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        RefreshRows();
        if (spot is { } found && TargetOf(key, found.Key, found.Zone) is { } target) EmitMove(key, target);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (pressedKey is not null && e.LeftButton == MouseButtonState.Pressed) DragTo(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (dragging is not null) e.Handled = true;
        Release();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TreePeer(this);

    /// <summary>读屏：树。</summary>
    sealed class TreePeer(Tree owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;

        protected override string GetClassNameCore() => nameof(Tree);
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

/// <summary>虚拟滚动的行：容器就是 <see cref="TreeItem"/>，回收复用时换行。</summary>
sealed class TreeRows(Tree owner) : ItemsControl
{
    readonly List<TreeItem> items = [];

    public IEnumerable<TreeItem> Items_ => items.Where(item => item.Row is not null);

    /// <summary>模板里的滚动区域。</summary>
    public ScrollViewer? ScrollViewer => VisualTreeHelper.GetChildrenCount(this) > 0 ? VisualTreeHelper.GetChild(this, 0) as ScrollViewer : null;

    protected override DependencyObject GetContainerForItemOverride()
    {
        var item = new TreeItem(owner);
        items.Add(item);
        return item;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeItem;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item) => ((TreeItem)element).Row = (FlatTreeNode<object>)item;

    protected override void ClearContainerForItemOverride(DependencyObject element, object item) => ((TreeItem)element).Row = null;

    /// <summary>第 <paramref name="index"/> 行滚进可见范围（虚拟化的面板上，那一行可能还没建）。</summary>
    public void BringIntoView(int index)
    {
        if (FindPanel(this) is { } panel) panel.BringIndexIntoViewPublic(index);
        (ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement)?.BringIntoView();
    }

    static VirtualizingStackPanel? FindPanel(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is VirtualizingStackPanel panel) return panel;
            if (FindPanel(child) is { } found) return found;
        }
        return null;
    }
}

/// <summary>树的部件样式键（DESIGN §7）。</summary>
public static class TreeParts
{
    /// <summary>虚拟滚动的行（ItemsControl：模板里是浮层滚动条的 ScrollArea；TS 的 propsScrollArea）。</summary>
    public static readonly PartKey Rows = new(typeof(Tree), nameof(Rows), typeof(ItemsControl));
}
