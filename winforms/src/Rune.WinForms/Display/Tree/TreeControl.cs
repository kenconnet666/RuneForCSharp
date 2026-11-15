using System.Collections;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 树（vue-tsx-pro 的 Tree，WAI-ARIA Tree）：可展开的层级列表（文件夹、组织架构、分类），可单选 / 多选、勾选（父子联动、半选）、懒加载子节点。
/// <code>
/// var tree = new TreeControl { ItemsSource = folders, GetKey = node => ((Folder)node).Id, GetLabel = node => ((Folder)node).Name, GetChildren = node => ((Folder)node).Children };
/// tree.SelectionMode = SelectionMode.Single;
/// tree.SelectedChanged += (_, _) => Open(tree.Selected);
/// </code>
/// 节点是任意对象，结构由 <see cref="GetKey"/>、<see cref="GetLabel"/>、<see cref="GetChildren"/> 读取（同 <see cref="TreeData"/>）。行是画出来的（不建子控件），
/// 只画看得见的，上万个节点也不卡。
/// <list type="bullet">
/// <item>键盘：↑↓ 移动，→ 展开 / 进入第一个子节点，← 收起 / 回到父节点（从右到左排版时对调），Home / End，Enter 选中（没有选择时展开或收起），
/// 空格勾选（<see cref="Checkable"/> 时）或选中，按字母跳到以它开头的节点。</item>
/// <item>勾选：<see cref="Checked"/> 是勾上的全部节点，父子联动、半选；<see cref="CheckStrictly"/> 时各自勾选。</item>
/// <item>懒加载：<see cref="HasChildNodes"/>、<see cref="LoadChildren"/>（加载中显示加载图标），失败时收回去并报告 <see cref="LoadError"/>；数据变了后调 <see cref="Refresh"/>。</item>
/// <item>过滤：<see cref="Filter"/> 或 <see cref="FilterBy"/>：留下匹配的节点与祖先、展开祖先，匹配的加粗。</item>
/// <item>移动：订阅了 <see cref="Moved"/> 才能拖动节点（或 Alt + 方向键），用 <see cref="TreeData.Move"/> 生成新数据交回 ItemsSource；<see cref="CanDrop"/> 限制落点。</item>
/// </list>
/// 行是画的，没有上游的 renderItem（自定义内容）：要加的文字写进 <see cref="GetLabel"/>。读屏：大纲（Outline），子项是看得见的每一行。
/// </summary>
[DefaultEvent(nameof(SelectedChanged))]
[Description("树：可展开的层级列表，可选中、勾选、懒加载、拖动调整。")]
public class TreeControl : Control
{
    readonly ScrollAreaControl scroll = new() { IsViewportFocusable = false };
    readonly TreeView view;
    readonly EmptyControl empty = new() { Icon = null, TextSize = TextSize.Sm, Visible = false };
    readonly FlexControl skeleton = new() { Direction = FlexDirection.Column, Visible = false };
    readonly DelayedLoading loading;
    readonly Typeahead typeahead = new();
    readonly TreeExpansion<object> expansion;
    List<FlatTreeNode<object>> flat = [];
    Dictionary<string, int> rowOf = [];
    TreeIndex? index;
    IReadOnlyDictionary<string, CheckState>? states;
    TreeFilter? filtering;
    IReadOnlyList<string> filterExpanded = [];

    /// <summary>新建：滚动区域里画出来的行；没有节点时是骨架或空状态。</summary>
    public TreeControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        // 默认按内容高（最高 20em）：放进 FlexControl 时由它按 GetPreferredSize 排；关掉 AutoSize 就用自己的大小
        AutoSize = true;
        AccessibleRole = AccessibleRole.Outline;
        expansion = new TreeExpansion<object>(() => ShownExpanded, SetExpanded, node => GetChildren?.Invoke(node)?.Cast<object>().ToList())
        {
            LoadChildren = node => LoadChildren?.Invoke(node),
            LoadError = (error, node) =>
            {
                if (LoadError is { } handler) handler(this, new TreeLoadErrorEventArgs(error, node));
                else System.Diagnostics.Trace.TraceError($"TreeControl 的子节点加载失败：{error}");
            },
        };
        expansion.LoadingChanged += () => view?.Invalidate();
        view = new TreeView(this);
        scroll.Content = view;
        for (var row = 0; row < 4; row++) skeleton.Controls.Add(new SkeletonControl { Delay = row * 100, Tag = 0.85 - row * 0.15 });
        loading = new DelayedLoading(shown =>
        {
            skeleton.Visible = shown && flat.Count == 0;
            UpdateBody();
        });
        Controls.AddRange([scroll, skeleton, empty]);
        foreach (Control child in Controls) child.MouseDown += (_, _) => Focus();
    }

    /// <summary>第一层的节点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; Refilter(); } }

    /// <summary>节点的 key，整棵树里唯一（必填）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetKey { get; set { field = value; Refilter(); } }

    /// <summary>节点的文字：显示它，按字母跳转、过滤也按它；不写时是 ToString()。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetLabel { get; set { field = value; Refilter(); } }

    /// <summary>子节点；叶子或还没加载时返回 null 或空。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, IEnumerable?>? GetChildren { get; set { field = value; Refilter(); } }

    /// <summary>有没有子节点（懒加载时子节点还没取回来也要能展开）；不写时看 GetChildren 是否非空。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, bool>? HasChildNodes { get; set { field = value; Refilter(); } }

    /// <summary>展开一个还没有子节点的节点时调用：取回子节点并放进数据里；返回的任务结束前显示加载中。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, Task?>? LoadChildren { get; set; }

    /// <summary>节点不可用：可以经过、展开，不能选中、勾选。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, bool>? IsItemDisabled { get; set { field = value; view.Invalidate(); } }

    /// <summary>文字前的图标，可以按展开状态换（文件夹开 / 合）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, bool, IconData?>? GetIcon { get; set { field = value; view.Invalidate(); } }

    /// <summary>展开的节点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Expanded
    {
        get;
        set
        {
            field = value;
            Rebuild();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>选择方式；不写时不能选中。</summary>
    [Category("Behavior")]
    public SelectionMode SelectionMode { get; set { field = value; view.Invalidate(); } }

    bool ShouldSerializeSelectionMode() => SelectionMode.Value is not null;

    void ResetSelectionMode() => SelectionMode = default;

    /// <summary>选中的节点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Selected
    {
        get;
        set
        {
            field = value;
            view.Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>每项前显示勾选框，默认父子联动。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Checkable { get; set { field = value; Rebuild(); } }

    /// <summary>勾选父子不联动：每个节点各自勾选。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool CheckStrictly { get; set { field = value; Rebuild(); } }

    /// <summary>勾上的节点（联动时含勾上的父节点与子孙）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Checked
    {
        get;
        set
        {
            field = value;
            states = index is null ? null : TreeData.CheckStates(index, value);
            view.Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>正在取节点：还没有节点时显示骨架行。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get; set { field = value; loading.Update(value); UpdateBody(); } }

    /// <summary>没有节点时的说明；不写时取语言包。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? EmptyText { get; set { field = value; UpdateBody(); } }

    /// <summary>每一层缩进多少（em），默认 1.25。</summary>
    [Category("Layout")]
    [DefaultValue(1.25)]
    public double Indent { get; set { field = value; view.Invalidate(); } } = 1.25;

    /// <summary>按文字过滤（忽略大小写）：留下匹配的节点与祖先。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Filter { get; set { field = value; Refilter(); } }

    /// <summary>自己判断的过滤；写了时代替 Filter。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, bool>? FilterBy { get; set { field = value; Refilter(); } }

    /// <summary>能不能放到这里；不写时都能放（放进自己和自己的子孙里除外）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, TreeMoveTarget, bool>? CanDrop { get; set; }

    /// <summary>展开的节点变了。</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>选中的节点变了。</summary>
    public event EventHandler? SelectedChanged;

    /// <summary>勾上的节点变了。</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>子节点加载失败：节点收回去，再展开会重试；没有订阅时写进跟踪日志。</summary>
    public event EventHandler<TreeLoadErrorEventArgs>? LoadError;

    /// <summary>拖动（或 Alt + 方向键）移动了节点：用 <see cref="TreeData.Move"/> 生成新数据交回 ItemsSource。订阅了才能拖动。</summary>
    public event EventHandler<TreeMovedEventArgs>? Moved;

    /// <summary>展开成一列的行（测试用）。</summary>
    internal IReadOnlyList<FlatTreeNode<object>> Rows => flat;

    /// <summary>画出来的行（测试用）。</summary>
    internal TreeView View => view;

    /// <summary>键盘所在的当前项（测试用）。</summary>
    internal string? Active { get; private set; }

    /// <summary>骨架与空状态（测试用）。</summary>
    internal (Control Skeleton, EmptyControl Empty) Placeholders => (skeleton, empty);

    /// <summary>滚动区域（测试用）。</summary>
    internal ScrollAreaControl ScrollPart => scroll;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced { get; private set; }

    internal float Em => this.EmPixels();

    TreeAccessors<object> Accessors => new((node, _) => GetKey?.Invoke(node) ?? node.ToString() ?? "", node => GetChildren?.Invoke(node)?.Cast<object>().ToList(), HasChildNodes);

    internal string LabelOf(object node) => GetLabel?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> Roots => ItemsSource?.Cast<object>().ToList() ?? [];

    IReadOnlyList<string> ShownExpanded => filtering is null ? Expanded : filterExpanded;

    internal bool CanMove => Moved is not null;

    void SetExpanded(IReadOnlyList<string> keys)
    {
        if (filtering is null) Expanded = keys;
        else
        {
            filterExpanded = keys;
            Rebuild();
        }
    }

    /// <summary>数据变了（取回了子节点、改了节点）：重新展开成行，再重画。</summary>
    public override void Refresh()
    {
        Rebuild();
        base.Refresh();
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
        if (view is null || GetKey is null) return;
        var previous = flat;
        flat = TreeData.Flatten(Roots, Accessors, ShownExpanded.ToHashSet(), filtering?.Include);
        rowOf = flat.Select((row, position) => (row.Key, position)).ToDictionary(pair => pair.Key, pair => pair.position);
        index = Checkable && !CheckStrictly ? TreeData.Index(Roots, Accessors) : null;
        states = index is null ? null : TreeData.CheckStates(index, Checked);
        if (Active is not null && !rowOf.ContainsKey(Active))
        {
            var parent = previous.FirstOrDefault(row => row.Key == Active)?.Parent;
            while (parent is not null && !rowOf.ContainsKey(parent)) parent = previous.FirstOrDefault(row => row.Key == parent)?.Parent;
            Active = parent ?? flat.FirstOrDefault()?.Key;
        }
        view.Rebuild();
        UpdateBody();
    }

    void UpdateBody()
    {
        if (view is null) return;
        var none = flat.Count == 0;
        skeleton.Visible = none && IsLoading && loading.Shown;
        scroll.Visible = !none;
        empty.Visible = none && !IsLoading;
        var lang = ProviderControl.LangOf(this);
        empty.Description = EmptyText ?? (filtering is null ? lang.TreeEmpty : lang.TreeNoMatch);
        if (Parent is { } parent) parent.PerformLayout(this, nameof(Bounds));
        PerformLayout();
    }

    /// <summary>某一行的状态（画的时候读）。</summary>
    internal TreeItemState StateOf(FlatTreeNode<object> row) => new(
        row.Depth,
        row.Expandable && expansion.IsExpanded(row.Key),
        Selected.Contains(row.Key),
        states?.GetValueOrDefault(row.Key) ?? (Checked.Contains(row.Key) ? CheckState.Checked : CheckState.Unchecked),
        Active == row.Key,
        IsItemDisabled?.Invoke(row.Node) ?? false,
        expansion.IsLoading(row.Key),
        filtering?.Matched.Contains(row.Key) == true,
        view.Dragging == row.Key,
        view.Drop is { } spot && spot.Key == row.Key ? spot.Zone : null);

    internal IconData? IconOf(FlatTreeNode<object> row, bool expanded) => GetIcon?.Invoke(row.Node, expanded);

    /// <summary>
    /// 画行里文字那一块（代替 GetLabel 的文字；库里的组合控件用，如 JsonViewerControl 按类型着色的键值）：画布、文字那一块的位置、节点、行的状态。
    /// 行是画的，没有上游 renderItem 那样的自定义内容；读屏、按字母跳转、过滤仍按 GetLabel。
    /// </summary>
    internal Action<Graphics, Rectangle, object, TreeItemState>? PaintContent;

    internal int ActiveRow => Active is not null && rowOf.TryGetValue(Active, out var position) ? position : -1;

    /// <summary>当前项换了：滚到看得见，重画，告诉读屏焦点到了那一项。</summary>
    void SetActive(string? key)
    {
        Active = key;
        view.Invalidate();
        if (ActiveRow is not (var position and >= 0)) return;
        var (top, height) = view.RowBounds(position);
        var offset = scroll.ScrollOffset.Y;
        var visible = scroll.ClientSize.Height;
        if (top < offset) scroll.ScrollOffset = new Point(0, top);
        else if (top + height > offset + visible) scroll.ScrollOffset = new Point(0, top + height - visible);
        if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, position);
    }

    bool DisabledAt(int position) => position < 0 || position >= flat.Count || (IsItemDisabled?.Invoke(flat[position].Node) ?? false);

    void Select(int position)
    {
        if (SelectionMode.Value is null || DisabledAt(position)) return;
        Selected = Selection.Toggle(Selected, flat[position].Key, SelectionMode, required: false);
    }

    void Check(int position)
    {
        if (!Checkable || DisabledAt(position)) return;
        var key = flat[position].Key;
        Checked = index is not null ? TreeData.ToggleChecked(index, Checked, key) : Selection.Toggle(Checked, key, SelectionMode.Multiple, required: false);
    }

    void Toggle(int position)
    {
        if (position < 0 || position >= flat.Count || !flat[position].Expandable) return;
        var row = flat[position];
        _ = expansion.SetOpen(row.Node, row.Key, !expansion.IsExpanded(row.Key));
    }

    /// <summary>点了一行：成为当前项、焦点在树上；能选时选中，能勾时勾选，否则展开或收起。</summary>
    internal void ClickItem(string key)
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

    /// <summary>得到焦点：第一次进入落在第一个已选的可见节点上，没有时落在第一项；焦点环重画。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (Active is null || !rowOf.ContainsKey(Active)) Active = flat.FirstOrDefault(row => Selected.Contains(row.Key))?.Key ?? flat.FirstOrDefault()?.Key;
        SetActive(Active);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        view.Invalidate();
        Invalidate();
    }

    /// <summary>方向键、Home / End、Enter、空格由树处理（不让窗体拿去切焦点、按默认按钮）；Alt + 方向键移动节点。</summary>
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Alt) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Modifiers == Keys.Alt && CanMove)
        {
            e.Handled = e.SuppressKeyPress = MoveByKey(e.KeyCode);
            return;
        }
        if (e.Modifiers != Keys.None || flat.Count == 0) return;
        var position = Math.Max(ActiveRow, 0);
        var row = flat[position];
        var open = row.Expandable && expansion.IsExpanded(row.Key);
        var (forward, backward) = RightToLeft == RightToLeft.Yes ? (Keys.Left, Keys.Right) : (Keys.Right, Keys.Left);
        void Go(int target) => SetActive(flat[Math.Clamp(target, 0, flat.Count - 1)].Key);
        var key = e.KeyCode;
        if (key == Keys.Down) Go(position + 1);
        else if (key == Keys.Up) Go(position - 1);
        else if (key == Keys.Home) Go(0);
        else if (key == Keys.End) Go(flat.Count - 1);
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
        else if (key == Keys.Enter)
        {
            if (SelectionMode.Value is not null) Select(position);
            else Toggle(position);
        }
        else if (key == Keys.Space)
        {
            if (Checkable) Check(position);
            else Select(position);
        }
        else return;
        // 空格的字符不再交给按字母跳转
        e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>字母：跳到以它开头的节点。</summary>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || char.IsControl(e.KeyChar) || e.KeyChar == ' ') return;
        var found = typeahead.Next(e.KeyChar.ToString(), [.. flat.Select(row => LabelOf(row.Node))], ActiveRow, Environment.TickCount64);
        if (found < 0) return;
        SetActive(flat[found].Key);
        e.Handled = true;
    }

    // —— 移动节点：指针拖动（TreeView 里），或 Alt + 方向键 ——

    FlatTreeNode<object>? RowByKey(string? key) => key is not null && rowOf.TryGetValue(key, out var position) ? flat[position] : null;

    /// <summary>key 是不是 ancestor 自己或它的子孙（看得见的行里，祖先总是看得见的）。</summary>
    bool IsWithin(string key, string ancestor)
    {
        for (var at = key; at is not null; at = RowByKey(at)?.Parent)
            if (at == ancestor) return true;
        return false;
    }

    /// <summary>落点换算成移动的目标（按移走之后的同级算）；放不下时 null。</summary>
    internal TreeMoveTarget? TargetOf(string key, string spotKey, TreeDropZone zone)
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
    internal void EmitMove(string key, TreeMoveTarget target)
    {
        if (RowByKey(key) is not { } row || Moved is null) return;
        var parent = RowByKey(target.Parent);
        // 移走之后的同级个数：同一层里移动时要去掉自己
        var count = (parent is null ? Roots.Count : GetChildren?.Invoke(parent.Node)?.Cast<object>().Count() ?? 0) - (row.Parent == target.Parent ? 1 : 0);
        Moved(this, new TreeMovedEventArgs(key, row.Node, target));
        Announced = ProviderControl.LangOf(this).TreeMoved(LabelOf(row.Node), parent is null ? null : LabelOf(parent.Node), Math.Min(target.Index, count) + 1);
        if (IsHandleCreated)
            AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent, Announced);
    }

    /// <summary>Alt + ↑↓ 在同级里移动，Alt + → 放进上一个同级的最后，Alt + ← 移到父节点后面（从右到左时左右对调）。</summary>
    bool MoveByKey(Keys key)
    {
        if (ActiveRow < 0) return false;
        var row = flat[ActiveRow];
        var (forward, backward) = RightToLeft == RightToLeft.Yes ? (Keys.Left, Keys.Right) : (Keys.Right, Keys.Left);
        TreeMoveTarget? target = null;
        if (key == Keys.Up)
        {
            if (row.Position > 1) target = new TreeMoveTarget(row.Parent, row.Position - 2);
        }
        else if (key == Keys.Down)
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

    /// <summary>四周 xs；没写高度（AutoSize）时按内容，最高 20em（或 MaximumSize 的高）；骨架行高 1em、上下 0.625em、宽度依次递减。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var em = Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var area = new Rectangle(xs, xs, Math.Max(0, ClientSize.Width - 2 * xs), Math.Max(0, ClientSize.Height - 2 * xs));
        scroll.Bounds = ClientRectangle;
        view.Padding = new Padding(xs);
        empty.Bounds = area;
        skeleton.Bounds = area;
        var sm = (int)MathF.Round(Tokens.Space.Sm * em);
        foreach (var bar in skeleton.Controls.OfType<SkeletonControl>())
        {
            bar.Margin = new Padding(sm, (int)MathF.Round(0.625f * em), sm, (int)MathF.Round(0.625f * em));
            var width = (int)Math.Max(0, (area.Width - 4 * em) * (double)bar.Tag!);
            bar.MinimumSize = bar.MaximumSize = new Size(width, (int)MathF.Round(em));
        }
    }

    /// <summary>按内容的高度，最高 20em（写了 MaximumSize 的高时按它）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var em = Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue ? proposedSize.Width : Width;
        var inner = skeleton.Visible ? skeleton.GetPreferredSize(new Size(width, 0)).Height
            : empty.Visible ? empty.GetPreferredSize(new Size(width, 0)).Height
            : view.TotalHeight;
        var cap = MaximumSize.Height > 0 ? MaximumSize.Height : (int)MathF.Round(20 * em);
        return new Size(width, Math.Min(inner + (scroll.Visible ? 0 : 2 * xs), cap));
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        view.Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        view.Rebuild();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 语言包、字号取自外层：放进界面后重新量、重新生成空状态的文字
        Rebuild();
        UpdateBody();
    }

    /// <summary>键盘聚焦时一圈 2 像素的焦点环（画在树的边上）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Focused || !ShowFocusCues) return;
        var ring = LogicalToDeviceUnits(2);
        using var pen = new Pen(ProviderControl.ThemeOf(this).Colors.FocusRing, ring);
        e.Graphics.DrawRectangle(pen, ring / 2f, ring / 2f, Width - ring, Height - ring);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TreeAccessibleObject(this);

    /// <summary>读屏：大纲，子项是看得见的每一行（层级、展开、选中、勾选、不可用、当前项有焦点）。</summary>
    sealed class TreeAccessibleObject(TreeControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Outline;

        public override int GetChildCount() => owner.flat.Count;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < owner.flat.Count ? new ItemAccessibleObject(owner, this, index) : null;

        public override AccessibleObject? GetFocused() => owner.ActiveRow >= 0 ? GetChild(owner.ActiveRow) : base.GetFocused();
    }

    /// <summary>树的一项：名称是文字，说明是层级与同级位置；默认操作同点击。</summary>
    internal sealed class ItemAccessibleObject(TreeControl owner, AccessibleObject tree, int index) : AccessibleObject
    {
        FlatTreeNode<object> Row => owner.flat[index];

        public override string? Name => owner.LabelOf(Row.Node);

        public override string? Description => $"{Row.Depth + 1} 级，第 {Row.Position} 项，共 {Row.SetSize} 项";

        public override AccessibleRole Role => AccessibleRole.OutlineItem;

        public override AccessibleObject Parent => tree;

        public override string? Value => (Row.Depth + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public override string DefaultAction => owner.SelectionMode.Value is not null ? "选中" : owner.Checkable ? "勾选" : Row.Expandable ? "展开或收起" : "";

        public override AccessibleStates State
        {
            get
            {
                var state = owner.StateOf(Row);
                var states = AccessibleStates.Selectable;
                if (Row.Expandable) states |= state.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                if (state.Selected) states |= AccessibleStates.Selected;
                if (owner.Checkable) states |= state.Checked switch { CheckState.Checked => AccessibleStates.Checked, CheckState.Indeterminate => AccessibleStates.Mixed, _ => 0 };
                if (state.Disabled) states |= AccessibleStates.Unavailable;
                if (state.Loading) states |= AccessibleStates.Busy;
                if (owner.Active == Row.Key) states |= AccessibleStates.Focused;
                return states;
            }
        }

        public override Rectangle Bounds
        {
            get
            {
                var (top, height) = owner.view.RowBounds(index);
                return owner.view.RectangleToScreen(new Rectangle(0, top, owner.view.Width, height));
            }
        }

        public override void DoDefaultAction() => owner.ClickItem(Row.Key);
    }
}

/// <summary>
/// 树画出来的行（<see cref="TreeControl"/> 用，放在滚动区域里）：每行一样高（控件高度，至少 24，上下各留 1），只画与剪切区相交的行。
/// 一行：缩进、展开箭头（加载子节点时是加载图标）、勾选框、图标、文字；外观同列表行（悬停、键盘高亮 fillHover；选中 accentSoft 底、accentText 字；不可用变淡）。
/// 指针：点箭头只展开收起，点勾选框只勾选，点别处按树的规则；订阅了 Moved 时拖动（挪开 4 以上）换位置：落在一项的上四分之一放在它前面，下四分之一放在后面，中间放进它里面。
/// </summary>
sealed class TreeView : Control, IElementHost
{
    readonly TreeControl owner;
    readonly Text label = new() { Truncate = true, Tone = TextTone.Inherit };
    readonly Icon chevron = new() { Data = Lucide.ChevronRight, Tone = TextTone.Muted, FlipInRtl = true };
    readonly Icon spinner = new() { Data = Lucide.LoaderCircle, Tone = TextTone.Muted };
    readonly Icon icon = new() { Tone = TextTone.Inherit };
    readonly Icon mark = new() { StrokeWidth = 3 };
    readonly Effect paint;
    int rowHeight = 32;
    bool painting;
    Point? pressedAt;
    string? pressedKey;
    int hovered = -1;

    public TreeView(TreeControl owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        foreach (var element in new Element[] { label, chevron, spinner, icon, mark }) element.Host = this;
        paint = new Effect(Invalidate);
        Cursor = Cursors.Hand;
    }

    /// <summary>正在被拖的那一项。</summary>
    public string? Dragging { get; private set; }

    /// <summary>拖动的落点。</summary>
    public (string Key, TreeDropZone Zone)? Drop { get; private set; }

    /// <summary>所有行的总高度（含上下内边距）。</summary>
    public int TotalHeight => owner.Rows.Count * rowHeight + Padding.Vertical;

    /// <summary>行的位置与高度。</summary>
    public (int Top, int Height) RowBounds(int row) => (Padding.Top + row * rowHeight, rowHeight);

    /// <summary>字号、DPI 或行变了：重新算行高与总高度。</summary>
    public void Rebuild()
    {
        var context = ElementContext.Of(this);
        rowHeight = (int)MathF.Ceiling(Math.Max(ControlHeight.Of(context, context.Em, Tokens.Size.Control), LogicalToDeviceUnits(24))) + 2 * Math.Max(1, LogicalToDeviceUnits(1));
        Height = TotalHeight;
        Parent?.PerformLayout();
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width, TotalHeight);

    // 画的时候给元素换内容只是借它们来量、来画：这时的失效通知不再要求重画，否则每画一遍都让自己失效，无限重画
    void IElementHost.InvalidateElement(Element element)
    {
        if (!painting) Invalidate();
    }

    void IElementHost.InvalidateLayout(Element element)
    {
        if (!painting) Invalidate();
    }

    /// <summary>一行里各部分的位置：缩进后是箭头（1.5em 见方），勾选框（1em，右边多留 xs），图标，文字。</summary>
    (Rectangle Box, Rectangle Toggle, Rectangle Check, int Content) Parts(int position, FlatTreeNode<object> row)
    {
        var em = owner.Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var gap = Math.Max(1, LogicalToDeviceUnits(1));
        var (top, height) = RowBounds(position);
        var box = new Rectangle(Padding.Left, top + gap, Math.Max(0, Width - Padding.Horizontal), height - 2 * gap);
        var x = box.Left + xs + (int)Math.Round(row.Depth * owner.Indent * em);
        var size = (int)Math.Round(1.5 * em);
        var toggle = new Rectangle(x, box.Top + (box.Height - size) / 2, size, size);
        x = toggle.Right + xs;
        var check = Rectangle.Empty;
        if (owner.Checkable)
        {
            var side = (int)Math.Round(em);
            check = new Rectangle(x, box.Top + (box.Height - side) / 2, side, side);
            x = check.Right + 2 * xs;
        }
        return (Mirror(box), Mirror(toggle), check == Rectangle.Empty ? check : Mirror(check), x);
    }

    /// <summary>从右到左排版时左右镜像。</summary>
    Rectangle Mirror(Rectangle rectangle) => RightToLeft == RightToLeft.Yes ? rectangle with { X = Width - rectangle.Right } : rectangle;

    /// <summary>只画与剪切区相交的行。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        painting = true;
        try
        {
            PaintRows(e);
        }
        finally
        {
            painting = false;
        }
    }

    void PaintRows(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var context = ElementContext.Of(this);
        var colors = context.Theme.Colors;
        var em = owner.Em;
        var xs = (int)MathF.Round(Tokens.Space.Xs * em);
        var sm = (int)MathF.Round(Tokens.Space.Sm * em);
        var radius = context.Theme.Radii.Sm * em;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var rows = owner.Rows;
        var first = Math.Max(0, (e.ClipRectangle.Top - Padding.Top) / Math.Max(1, rowHeight));
        var last = Math.Min(rows.Count - 1, (e.ClipRectangle.Bottom - Padding.Top) / Math.Max(1, rowHeight));
        for (var position = first; position <= last; position++)
        {
            var row = rows[position];
            var state = owner.StateOf(row);
            var (box, toggle, check, contentX) = Parts(position, row);
            var highlighted = (owner.Focused && state.Active) || (hovered == position && !state.Disabled);
            Color? background = state.Drop == TreeDropZone.Inside ? colors.AccentSoft
                : state.Selected ? (hovered == position ? colors.AccentSoftHover : colors.AccentSoft)
                : highlighted ? colors.FillHover : null;
            using (var shape = Drawing.RoundedRectangle(box, radius))
            {
                if (background is { } fill)
                {
                    using var brush = new SolidBrush(fill);
                    graphics.FillPath(brush, shape);
                }
                if (state.Drop == TreeDropZone.Inside)
                {
                    using var pen = new Pen(colors.Accent, Math.Max(2, LogicalToDeviceUnits(2)));
                    graphics.DrawPath(pen, shape);
                }
            }
            // 不可用、被拖：文字、图标混进底色（控件不能半透明）
            var fade = state.Disabled ? Tokens.Opacity.Disabled : state.Dragging ? 0.5f : 1f;
            Color Dim(Color color) => fade < 1 ? AppShellControl.Blend(Color.FromArgb((int)(255 * fade), color), colors.Background) : color;
            var foreground = Dim(state.Selected ? colors.AccentText : colors.Text);
            var faded = context with { ForeColor = foreground };
            if (state.Loading || row.Expandable)
            {
                var element = state.Loading ? spinner : chevron;
                var size = element.Measure(context, toggle.Size);
                element.Bounds = new Rectangle(toggle.Left + (toggle.Width - size.Width) / 2, toggle.Top + (toggle.Height - size.Height) / 2, size.Width, size.Height);
                var saved = graphics.Save();
                if (!state.Loading && state.Expanded)
                {
                    // 展开时箭头转 90°
                    var center = new PointF(toggle.Left + toggle.Width / 2f, toggle.Top + toggle.Height / 2f);
                    graphics.TranslateTransform(center.X, center.Y);
                    graphics.RotateTransform(90);
                    graphics.TranslateTransform(-center.X, -center.Y);
                }
                element.Paint(graphics, context with { ForeColor = Dim(colors.TextMuted) });
                graphics.Restore(saved);
            }
            if (owner.Checkable)
            {
                // 勾选框画成 Checkbox 的样子：选中、半选时 accent 实底、accentContrast 的对勾或横线
                var marked = state.Checked != CheckState.Unchecked;
                using var shape = Drawing.RoundedRectangle(check, context.Theme.Radii.Sm * em);
                using (var fill = new SolidBrush(Dim(marked ? colors.Accent : colors.Surface))) graphics.FillPath(fill, shape);
                using (var pen = new Pen(Dim(marked ? colors.Accent : colors.BorderStrong), Math.Max(1, LogicalToDeviceUnits(1)))) graphics.DrawPath(pen, shape);
                if (marked)
                {
                    mark.Data = state.Checked == CheckState.Indeterminate ? Lucide.Minus : Lucide.Check;
                    var side = (int)Math.Round(0.875 * em);
                    mark.Bounds = new Rectangle(check.Left + (check.Width - side) / 2, check.Top + (check.Height - side) / 2, side, side);
                    mark.Paint(graphics, context with { ForeColor = colors.AccentContrast });
                }
            }
            var x = contentX;
            var right = box.Right - sm;
            if (owner.IconOf(row, state.Expanded) is { } data)
            {
                icon.Data = data;
                var size = icon.Measure(context, new Size(right - x, box.Height));
                icon.Bounds = Mirror(new Rectangle(x, box.Top + (box.Height - size.Height) / 2, size.Width, size.Height));
                icon.Paint(graphics, faded);
                x += size.Width + xs;
            }
            if (owner.PaintContent is { } custom) custom(graphics, Mirror(Rectangle.FromLTRB(x, box.Top, Math.Max(x, right), box.Bottom)), row.Node, state);
            else
            {
                label.Content = owner.LabelOf(row.Node);
                label.Weight = state.Matched ? TextWeight.Semibold : default;
                var labelSize = label.Measure(context, new Size(Math.Max(0, right - x), int.MaxValue));
                label.Bounds = Mirror(new Rectangle(x, box.Top + (box.Height - labelSize.Height) / 2, Math.Max(0, right - x), labelSize.Height));
                label.Paint(graphics, faded);
            }
            if (state.Drop is { } zone && zone != TreeDropZone.Inside)
            {
                // 放在前后：一条 accent 的线，从这一层的缩进处开始
                var start = box.Left + (int)Math.Round((row.Depth * owner.Indent + 1.75) * em);
                var thick = Math.Max(2, LogicalToDeviceUnits(2));
                var y = zone == TreeDropZone.Before ? box.Top - thick / 2 : box.Bottom - thick / 2;
                using var line = new SolidBrush(colors.Accent);
                graphics.FillRectangle(line, Mirror(new Rectangle(start, y, Math.Max(0, box.Right - start), thick)));
            }
        }
    }

    int RowAt(Point point) => point.Y < Padding.Top ? -1 : (point.Y - Padding.Top) / Math.Max(1, rowHeight) is var row && row < owner.Rows.Count ? row : -1;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var row = RowAt(e.Location);
        if (row != hovered)
        {
            hovered = row;
            Invalidate();
        }
        if (pressedKey is not { } key || pressedAt is not { } from || e.Button != MouseButtons.Left) return;
        if (Dragging is null)
        {
            if (Math.Abs(e.X - from.X) + Math.Abs(e.Y - from.Y) < LogicalToDeviceUnits(4)) return;
            Dragging = key;
        }
        Drop = null;
        if (row >= 0)
        {
            var (top, height) = RowBounds(row);
            var ratio = (e.Y - top) / (double)Math.Max(1, height);
            var zone = ratio < 0.25 ? TreeDropZone.Before : ratio > 0.75 ? TreeDropZone.After : TreeDropZone.Inside;
            if (owner.TargetOf(key, owner.Rows[row].Key, zone) is not null) Drop = (owner.Rows[row].Key, zone);
        }
        // 拖到靠近上下边缘时滚动
        if (Parent?.Parent is ScrollAreaControl scroll)
        {
            var visible = PointToScreen(e.Location).Y - scroll.PointToScreen(Point.Empty).Y;
            var edge = LogicalToDeviceUnits(32);
            if (visible < edge) scroll.ScrollOffset = scroll.ScrollOffset with { Y = Math.Max(0, scroll.ScrollOffset.Y - LogicalToDeviceUnits(12)) };
            else if (visible > scroll.ClientSize.Height - edge) scroll.ScrollOffset = scroll.ScrollOffset with { Y = scroll.ScrollOffset.Y + LogicalToDeviceUnits(12) };
        }
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        owner.Focus();
        if (e.Button != MouseButtons.Left || RowAt(e.Location) is not (var row and >= 0)) return;
        pressedAt = e.Location;
        pressedKey = owner.CanMove ? owner.Rows[row].Key : null;
    }

    /// <summary>松开：拖了就放下；没拖时点箭头只展开收起，点勾选框只勾选，点别处按树的规则。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var dragged = Dragging;
        var spot = Drop;
        pressedKey = null;
        pressedAt = null;
        Dragging = null;
        Drop = null;
        if (dragged is not null)
        {
            Invalidate();
            if (spot is { } found && owner.TargetOf(dragged, found.Key, found.Zone) is { } target) owner.EmitMove(dragged, target);
            return;
        }
        if (e.Button != MouseButtons.Left || RowAt(e.Location) is not (var position and >= 0)) return;
        var row = owner.Rows[position];
        var (_, toggle, check, _) = Parts(position, row);
        if (row.Expandable && toggle.Contains(e.Location)) owner.ClickToggle(row.Key);
        else if (owner.Checkable && check.Contains(e.Location)) owner.ClickCheck(row.Key);
        else owner.ClickItem(row.Key);
    }

    /// <summary>某一行里箭头、勾选框、文字的中点（测试用）。</summary>
    internal (Point Toggle, Point Check, Point Label) Points(int position)
    {
        var (box, toggle, check, x) = Parts(position, owner.Rows[position]);
        static Point Center(Rectangle rectangle) => new(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
        return (Center(toggle), Center(check), new Point(Math.Min(box.Right - 2, x + 8), box.Top + box.Height / 2));
    }
}

/// <summary>拖动的落点在一项的哪里。</summary>
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

/// <summary><see cref="TreeControl.LoadError"/> 的参数。</summary>
/// <param name="error">失败的原因。</param>
/// <param name="node">要加载子节点的那个节点。</param>
public sealed class TreeLoadErrorEventArgs(Exception error, object node) : EventArgs
{
    /// <summary>失败的原因。</summary>
    public Exception Error { get; } = error;

    /// <summary>节点。</summary>
    public object Node { get; } = node;
}

/// <summary><see cref="TreeControl.Moved"/> 的参数（vue-tsx-pro 的 TreeMove）。</summary>
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
