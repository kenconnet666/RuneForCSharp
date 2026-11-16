using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 穿梭框的共同部分（vue-tsx-pro 的 Transfer 与 TreeTransfer 共用的 renderPanel、renderActions）：左右两边的外框（全选、标题、计数、搜索框、内容）、
/// 中间两个按钮、移动与播报、窄时上下排。两边的内容（列表或树）与哪些项能勾、能移由子类给出。
/// </summary>
public abstract class TransferBase : Control
{
    /// <summary>右边（已选）的值，按移过去的先后排列（TS 的 _value）；默认双向绑定。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ValuesProperty =
        AvaloniaProperty.Register<TransferBase, IReadOnlyList<string>>(nameof(Values), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>左右两边的标题；不写时取语言包（可选、已选）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> TitlesProperty = AvaloniaProperty.Register<TransferBase, IReadOnlyList<string>?>(nameof(Titles));

    /// <summary>两边带搜索框。</summary>
    public static readonly StyledProperty<bool> SearchableProperty = AvaloniaProperty.Register<TransferBase, bool>(nameof(Searchable));

    /// <summary>窄于它时上下排（40rem）。</summary>
    const double StackBelow = 40 * 16;

    readonly TransferSide[] sides;
    readonly Flex actions = new() { Name = TransferParts.Actions, Direction = FlexDirection.Column, Justify = FlexJustify.Center, Gap = Space.Sm };
    readonly IconButton toTarget = new() { Name = TransferParts.Action, Icon = Lucide.ChevronRight, Variant = ButtonVariant.Soft, Size = TextSize.Sm };
    readonly IconButton toSource = new() { Name = TransferParts.Action, Icon = Lucide.ChevronLeft, Variant = ButtonVariant.Soft, Size = TextSize.Sm };
    readonly LiveRegion announcer = new();
    readonly TransferLayout layout;
    // 子类在构造里建好两边的内容之后才能同步
    bool ready;

    /// <summary>新建：左右两边（内容由 <see cref="CreateBody"/> 给出）与中间的按钮。</summary>
    protected TransferBase()
    {
        sides = [new TransferSide(this, 0), new TransferSide(this, 1)];
        actions.Children.AddRange([toTarget, toSource]);
        toTarget.Click += (_, _) => Move(0, sides[0].Checked);
        toSource.Click += (_, _) => Move(1, sides[1].Checked);
        layout = new TransferLayout(this) { Children = { sides[0].Panel, actions, sides[1].Panel, announcer } };
        LogicalChildren.Add(layout);
        VisualChildren.Add(layout);
    }

    /// <summary>右边的值。</summary>
    public IReadOnlyList<string> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>左右两边的标题。</summary>
    public IReadOnlyList<string>? Titles { get => GetValue(TitlesProperty); set => SetValue(TitlesProperty, value); }

    /// <summary>两边带搜索框。</summary>
    public bool Searchable { get => GetValue(SearchableProperty); set => SetValue(SearchableProperty, value); }

    /// <summary>两边的部件（测试用）：0 左、1 右。</summary>
    internal (Control Body, Checkbox CheckAll, SearchField Search, Text Count) SideParts(int side) => (sides[side].Body, sides[side].CheckAll, sides[side].Search, sides[side].Count);

    /// <summary>读屏播报的文字（测试用）。</summary>
    internal string? Announcement => announcer.Text;

    /// <summary>语言包。</summary>
    protected Lang Lang => Provider.GetLang(this);

    /// <summary>一边的标题。</summary>
    protected string TitleOf(int side) => Titles?.ElementAtOrDefault(side) is { } title ? title : side == 0 ? Lang.TransferSource : Lang.TransferTarget;

    /// <summary>一边搜索框里的文字。</summary>
    protected string SearchOf(int side) => sides[side].Search.Text ?? "";

    /// <summary>一边勾上的值。</summary>
    protected IReadOnlyList<string> CheckedOf(int side) => sides[side].Checked;

    /// <summary>改一边勾上的值（内容里勾选变了时）。</summary>
    protected void SetChecked(int side, IReadOnlyList<string> keys) => sides[side].Checked = keys;

    /// <summary>建一边的内容（Listbox 或 Tree），由外框放在搜索框下面、占满剩下的高度。</summary>
    protected abstract Control CreateBody(int side);

    /// <summary>一边看得见、能勾的值：全选只管它们。</summary>
    protected abstract IReadOnlyCollection<string> Selectable(int side);

    /// <summary>一边一共几项（计数的分母）。</summary>
    protected abstract int Total(int side);

    /// <summary>一边的内容跟着数据、搜索与勾选更新。</summary>
    protected abstract void SyncBody(int side);

    /// <summary>这一项能不能从 from 这一边移走（不在这一边、不可用的不能）。</summary>
    protected abstract bool CanMove(string key, int from);

    /// <summary>子类建好内容后调用：此后数据、勾选变了就同步。</summary>
    protected void Ready()
    {
        ready = true;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty || change.Property == TitlesProperty || change.Property == SearchableProperty
            || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty) Sync();
    }

    /// <summary>两边与中间按钮的可用跟着数据、已选与勾选。</summary>
    protected void Sync()
    {
        if (!ready) return;
        foreach (var side in sides) side.Sync();
        var enabled = IsEffectivelyEnabled;
        toTarget.IsEnabled = enabled && sides[0].Checked.Count > 0;
        toSource.IsEnabled = enabled && sides[1].Checked.Count > 0;
        AutomationProperties.SetName(toTarget, Lang.TransferMoveTo(TitleOf(1)));
        AutomationProperties.SetName(toSource, Lang.TransferMoveTo(TitleOf(0)));
    }

    /// <summary>把 keys 从 from 这一边移到另一边：能移的才移；移完播报。</summary>
    protected void Move(int from, IReadOnlyList<string> keys)
    {
        var movable = keys.Where(key => CanMove(key, from)).Distinct().ToList();
        if (!IsEffectivelyEnabled || movable.Count == 0) return;
        var moving = movable.ToHashSet();
        sides[from].Checked = [.. sides[from].Checked.Where(key => !moving.Contains(key))];
        SetCurrentValue(ValuesProperty, from == 0 ? [.. Values, .. movable] : Values.Where(key => !moving.Contains(key)).ToList());
        announcer.Announce(Lang.TransferMoved(movable.Count, TitleOf(from == 0 ? 1 : 0)));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        layout.Measure(availableSize);
        return layout.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        layout.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>一边：外框（border、md 圆角、surface 底，20em 高）里是标题栏（全选、标题、计数）、搜索框与内容。</summary>
    sealed class TransferSide
    {
        readonly TransferBase owner;
        readonly int index;
        readonly Text title = new() { Name = TransferParts.Title, Weight = TextWeight.Medium, Truncate = true };
        readonly Border headerBox = new() { Name = TransferParts.Header };
        readonly Border search = new() { Name = TransferParts.Search };
        bool syncing;

        public TransferSide(TransferBase owner, int index)
        {
            (this.owner, this.index) = (owner, index);
            Body = owner.CreateBody(index);
            CheckAll.PropertyChanged += (_, e) =>
            {
                if (syncing || e.Property != Checkbox.IsCheckedProperty) return;
                var selectable = owner.Selectable(index);
                var others = Checked.Where(key => !selectable.Contains(key));
                Checked = CheckAll.IsChecked ? [.. others, .. selectable] : [.. others];
            };
            Search.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextField.TextProperty) owner.Sync();
            };
            search.Child = Search;
            Flex.SetGrow(title, true);
            headerBox.Child = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { CheckAll, title, Count } };
            var body = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(headerBox, Dock.Top);
            DockPanel.SetDock(search, Dock.Top);
            body.Children.AddRange([headerBox, search, Body]);
            Panel.Child = body;
            Panel.SizeChanged += (_, _) => Metrics();
            Panel.AttachedToVisualTree += (_, _) => Metrics();
        }

        public Border Panel { get; } = new() { Name = TransferParts.Panel, ClipToBounds = true };

        public Control Body { get; }

        public Checkbox CheckAll { get; } = new() { Name = TransferParts.CheckAll };

        public Text Count { get; } = new() { Name = TransferParts.Count, Size = TextSize.Sm, Tone = TextTone.Muted };

        public SearchField Search { get; } = new() { Variant = ControlVariant.Soft, Size = TextSize.Sm };

        /// <summary>勾上的值。</summary>
        public IReadOnlyList<string> Checked
        {
            get;
            set
            {
                field = value;
                if (!syncing) owner.Sync();
            }
        } = [];

        public void Sync()
        {
            syncing = true;
            var lang = owner.Lang;
            var name = owner.TitleOf(index);
            owner.SyncBody(index);
            var selectable = owner.Selectable(index);
            title.Text = name;
            AutomationProperties.SetName(Body, name);
            AutomationProperties.SetName(Panel, name);
            Count.Text = lang.TransferCount(Checked.Count, owner.Total(index));
            var visibleChecked = Checked.Count(selectable.Contains);
            CheckAll.IsChecked = selectable.Count > 0 && visibleChecked == selectable.Count;
            CheckAll.IsIndeterminate = visibleChecked > 0 && visibleChecked < selectable.Count;
            CheckAll.IsEnabled = selectable.Count > 0;
            AutomationProperties.SetName(CheckAll, lang.TransferSelectAll(name));
            search.IsVisible = owner.Searchable;
            AutomationProperties.SetName(Search, $"{lang.SelectSearch} {name}");
            syncing = false;
        }

        /// <summary>em 尺寸：20em 高、md 圆角；标题栏上下 xs、左右 sm，下边一道 borderMuted；搜索框外 xs。</summary>
        void Metrics()
        {
            var em = TextElement.GetFontSize(owner);
            double Token(string key) => owner.Resource<double>(key) * em;
            Panel.Height = 20 * em;
            Panel.CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md));
            Panel.BorderThickness = new Thickness(1);
            Panel.Bind(Border.BorderBrushProperty, Panel.GetResourceObservable(ThemeKeys.Brush.Border), BindingPriority.Template);
            Panel.Bind(Border.BackgroundProperty, Panel.GetResourceObservable(ThemeKeys.Brush.Surface), BindingPriority.Template);
            headerBox.Padding = new Thickness(Token(ThemeKeys.Space.Sm), Token(ThemeKeys.Space.Xs));
            headerBox.BorderThickness = new Thickness(0, 0, 0, 1);
            headerBox.Bind(Border.BorderBrushProperty, headerBox.GetResourceObservable(ThemeKeys.Brush.BorderMuted), BindingPriority.Template);
            search.Padding = new Thickness(Token(ThemeKeys.Space.Xs));
        }
    }

    /// <summary>左、中、右三栏（中间按内容宽、两边平分）；窄于 40rem 时上下排、中间的按钮横排、箭头转成上下。</summary>
    sealed class TransferLayout(TransferBase owner) : Panel
    {
        double Gap => owner.Resource<double>(ThemeKeys.Space.Md) * TextElement.GetFontSize(owner);

        protected override Size MeasureOverride(Size availableSize)
        {
            var stacked = availableSize.Width < StackBelow;
            owner.actions.Direction = stacked ? FlexDirection.Row : FlexDirection.Column;
            foreach (var button in new[] { owner.toTarget, owner.toSource }) button.RenderTransform = stacked ? new global::Avalonia.Media.RotateTransform(90) : null;
            var (left, actions, right) = (Children[0], Children[1], Children[2]);
            actions.Measure(Size.Infinity);
            Children[3].Measure(Size.Infinity);
            if (stacked)
            {
                left.Measure(availableSize.WithHeight(double.PositiveInfinity));
                right.Measure(availableSize.WithHeight(double.PositiveInfinity));
                return new Size(availableSize.Width, left.DesiredSize.Height + actions.DesiredSize.Height + right.DesiredSize.Height + 2 * Gap);
            }
            var side = Math.Max(0, (availableSize.Width - actions.DesiredSize.Width - 2 * Gap) / 2);
            left.Measure(new Size(side, availableSize.Height));
            right.Measure(new Size(side, availableSize.Height));
            return new Size(double.IsInfinity(availableSize.Width) ? 2 * Math.Max(left.DesiredSize.Width, right.DesiredSize.Width) + actions.DesiredSize.Width + 2 * Gap : availableSize.Width,
                Math.Max(Math.Max(left.DesiredSize.Height, right.DesiredSize.Height), actions.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (left, actions, right) = (Children[0], Children[1], Children[2]);
            Children[3].Arrange(default);
            if (finalSize.Width < StackBelow)
            {
                var y = 0d;
                left.Arrange(new Rect(0, y, finalSize.Width, left.DesiredSize.Height));
                y += left.DesiredSize.Height + Gap;
                actions.Arrange(new Rect((finalSize.Width - actions.DesiredSize.Width) / 2, y, actions.DesiredSize.Width, actions.DesiredSize.Height));
                y += actions.DesiredSize.Height + Gap;
                right.Arrange(new Rect(0, y, finalSize.Width, right.DesiredSize.Height));
                return finalSize;
            }
            var side = Math.Max(0, (finalSize.Width - actions.DesiredSize.Width - 2 * Gap) / 2);
            left.Arrange(new Rect(0, 0, side, finalSize.Height));
            actions.Arrange(new Rect(side + Gap, 0, actions.DesiredSize.Width, finalSize.Height));
            right.Arrange(new Rect(finalSize.Width - side, 0, side, finalSize.Height));
            return finalSize;
        }
    }

    /// <summary>可多选的列表（两边的 Listbox）：勾选写回这一边，双击一项直接移过去。</summary>
    private protected Listbox CreateList(int side)
    {
        var list = new Listbox { Name = TransferParts.List, SelectionMode = SelectionMode.Multiple, VerticalAlignment = VerticalAlignment.Stretch };
        list.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValuesProperty && !(list.Values ?? []).SequenceEqual(CheckedOf(side))) SetChecked(side, list.Values ?? []);
        };
        list.DoubleTapped += (_, e) =>
        {
            if ((e.Source as Visual)?.FindAncestorOfType<ListboxItem>(includeSelf: true)?.Option is { } option) Move(side, [option.Value]);
        };
        return list;
    }

    /// <summary>选项文字包含输入（忽略大小写与重音，同 Select 的搜索）；没有输入时都算。</summary>
    private protected static bool TextMatches(string label, string query) => query.Trim() is not { Length: > 0 } text || SelectSearch.Matches(label, text);
}

/// <summary>
/// 穿梭框（vue-tsx-pro 的 Transfer）：左边是可选的，右边是已选的，勾上之后点中间的按钮移过去（分配权限、指派成员、挑选导出的列）。
/// <code>
/// &lt;rune:Transfer ItemsSource="{Binding Users}" ToOption="{Binding UserOption}" Values="{Binding MemberIds}" Searchable="True" /&gt;
/// </code>
/// <list type="bullet">
/// <item>每一边是一个可多选的 <see cref="Listbox"/>（焦点、方向键、空格勾选、虚拟滚动同 Listbox），上方是全选框、标题与"勾上 / 总数"，
/// <see cref="TransferBase.Searchable"/> 时带搜索框（按选项文字）。全选只管当前看得见（搜索后留下）的项。</item>
/// <item>中间两个按钮把勾上的项移到另一边（没有勾上的时不可用），双击一项直接移过去；移动后读屏播报移了几项。不可用的选项勾不上，也就移不动。</item>
/// <item><see cref="TransferBase.Values"/> 是右边的值，按移过去的先后排列；ItemsSource 里找不到的值不显示（也不会被删掉）。</item>
/// <item>窄于 40rem 时上下排，中间的按钮横排、箭头转成上下。左边是一棵树时用 <see cref="TreeTransfer"/>。</item>
/// </list>
/// 部件：<see cref="TransferParts"/>。
/// </summary>
public class Transfer : TransferBase
{
    /// <summary>全部数据（两边一起）：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Transfer, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Transfer, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>搜索时怎样算匹配：参数是数据与输入；不写时选项文字包含输入（忽略大小写与重音，同 Select 的搜索）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Transfer, Func<object?, string, bool>?>(nameof(Filter));

    readonly Listbox[] lists = new Listbox[2];
    List<(object? Item, ListboxOption Option)> entries = [];

    /// <summary>新建。</summary>
    public Transfer() => Ready();

    /// <summary>全部数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>搜索的匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <inheritdoc />
    protected override Control CreateBody(int side) => lists[side] = CreateList(side);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty)
        {
            entries = [.. (ItemsSource?.Cast<object?>() ?? []).Select(item => (item, OptionOf(item)))];
            Sync();
        }
        else if (change.Property == FilterProperty) Sync();
    }

    ListboxOption OptionOf(object? item) => item switch
    {
        ListboxOption option => option,
        _ when ToOption is { } map => map(item),
        _ => new ListboxOption(item?.ToString() ?? "", item?.ToString() ?? ""),
    };

    /// <summary>一边的全部项：左边按数据的顺序，右边按移过去的先后。</summary>
    List<(object? Item, ListboxOption Option)> SideEntries(int side)
    {
        if (side == 0)
        {
            var chosen = Values.ToHashSet();
            return [.. entries.Where(entry => !chosen.Contains(entry.Option.Value))];
        }
        var byValue = entries.GroupBy(entry => entry.Option.Value).ToDictionary(group => group.Key, group => group.First());
        return [.. Values.Where(byValue.ContainsKey).Select(key => byValue[key])];
    }

    /// <summary>看得见的项：搜索后留下的。</summary>
    List<(object? Item, ListboxOption Option)> Shown(int side) =>
        [.. SideEntries(side).Where(entry => SearchOf(side).Trim() is not { Length: > 0 } text || (Filter is { } filter ? filter(entry.Item, text) : TextMatches(entry.Option.Label, text)))];

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> Selectable(int side) => Shown(side).Where(entry => !entry.Option.IsDisabled).Select(entry => entry.Option.Value).ToHashSet();

    /// <inheritdoc />
    protected override int Total(int side) => SideEntries(side).Count;

    /// <inheritdoc />
    protected override void SyncBody(int side)
    {
        // 移走的项不再算勾上
        var valid = SideEntries(side).Select(entry => entry.Option.Value).ToHashSet();
        if (CheckedOf(side).Any(key => !valid.Contains(key))) SetChecked(side, [.. CheckedOf(side).Where(valid.Contains)]);
        lists[side].ItemsSource = Shown(side).Select(entry => (object?)entry.Option).ToList();
        lists[side].Values = CheckedOf(side);
    }

    /// <inheritdoc />
    protected override bool CanMove(string key, int from) =>
        entries.FirstOrDefault(entry => entry.Option.Value == key) is { Option: { } option } && !option.IsDisabled && Values.Contains(key) == (from == 1);
}

/// <summary>
/// 左边是一棵树的穿梭框（vue-tsx-pro 的 TreeTransfer）：左边是勾选的 <see cref="Tree"/>（父子联动、可搜索），勾上部门就是它下面的全部团队；
/// 只移叶子，<see cref="TransferBase.Values"/> 是叶子的 key；已经移过去的叶子在树上勾上且不可用。右边是已选的列表，同 Transfer。
/// <code>
/// &lt;rune:TreeTransfer ItemsSource="{Binding Orgs}" GetKey="{Binding KeyOf}" GetLabel="{Binding NameOf}" GetChildren="{Binding ChildrenOf}" Values="{Binding TeamIds}" /&gt;
/// </code>
/// </summary>
public class TreeTransfer : TransferBase
{
    /// <summary>第一层的节点。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<TreeTransfer, IEnumerable?>(nameof(ItemsSource));

    /// <summary>节点的 key，整棵树里唯一（必填）。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetKeyProperty = AvaloniaProperty.Register<TreeTransfer, Func<object, string>?>(nameof(GetKey));

    /// <summary>节点的文字。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetLabelProperty = AvaloniaProperty.Register<TreeTransfer, Func<object, string>?>(nameof(GetLabel));

    /// <summary>子节点。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> GetChildrenProperty = AvaloniaProperty.Register<TreeTransfer, Func<object, IEnumerable?>?>(nameof(GetChildren));

    readonly Tree tree = new() { Name = TransferParts.Tree, Checkable = true, VerticalAlignment = VerticalAlignment.Stretch };
    TreeIndex index = new([], new Dictionary<string, string?>(), new Dictionary<string, IReadOnlyList<string>>());
    Dictionary<string, object> nodes = [];
    List<string> leaves = [];
    bool syncingTree;

    /// <summary>新建：左边的树、右边的列表。</summary>
    public TreeTransfer()
    {
        // 已经移过去的叶子勾上且不可用
        tree.IsItemDisabled = node => !IsEffectivelyEnabled || Values.Contains(KeyOf(node));
        tree.PropertyChanged += (_, e) =>
        {
            if (syncingTree || e.Property != Tree.CheckedProperty) return;
            var chosen = Values.ToHashSet();
            SetChecked(0, [.. TreeData.CheckedByStrategy(index, tree.Checked, CheckStrategy.Child).Where(key => !chosen.Contains(key))]);
        };
        Ready();
    }

    /// <summary>第一层的节点。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>节点的 key。</summary>
    public Func<object, string>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>节点的文字。</summary>
    public Func<object, string>? GetLabel { get => GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>子节点。</summary>
    public Func<object, IEnumerable?>? GetChildren { get => GetValue(GetChildrenProperty); set => SetValue(GetChildrenProperty, value); }

    /// <summary>左边的树（测试用）。</summary>
    internal Tree Tree => tree;

    /// <inheritdoc />
    protected override Control CreateBody(int side) => side == 0 ? tree : CreateList(1);

    string KeyOf(object node) => GetKey?.Invoke(node) ?? node.ToString() ?? "";

    string LabelOf(object node) => GetLabel?.Invoke(node) ?? node.ToString() ?? "";

    IReadOnlyList<object> ChildrenOf(object node) => GetChildren?.Invoke(node)?.Cast<object>().ToList() ?? [];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == GetKeyProperty || change.Property == GetLabelProperty || change.Property == GetChildrenProperty)
        {
            var roots = ItemsSource?.Cast<object>().ToList() ?? [];
            index = TreeData.Index(roots, new TreeAccessors<object>((node, _) => KeyOf(node), ChildrenOf));
            nodes = [];
            void Visit(IReadOnlyList<object> level)
            {
                foreach (var node in level)
                {
                    nodes[KeyOf(node)] = node;
                    Visit(ChildrenOf(node));
                }
            }
            Visit(roots);
            leaves = [.. index.Order.Where(key => !index.Children.TryGetValue(key, out var children) || children.Count == 0)];
            tree.ItemsSource = ItemsSource;
            tree.GetKey = KeyOf;
            tree.GetLabel = LabelOf;
            tree.GetChildren = node => GetChildren?.Invoke(node);
            Sync();
        }
    }

    bool Matches(string key, int side) => nodes.TryGetValue(key, out var node) && TextMatches(LabelOf(node), SearchOf(side));

    /// <summary>左边能勾的叶子：还没移过去的。</summary>
    List<string> SourceLeaves()
    {
        var chosen = Values.ToHashSet();
        return [.. leaves.Where(key => !chosen.Contains(key))];
    }

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> Selectable(int side) =>
        side == 0 ? SourceLeaves().Where(key => Matches(key, 0)).ToHashSet() : Values.Where(key => nodes.ContainsKey(key) && Matches(key, 1)).ToHashSet();

    /// <inheritdoc />
    protected override int Total(int side) => side == 0 ? SourceLeaves().Count : Values.Count(nodes.ContainsKey);

    /// <inheritdoc />
    protected override void SyncBody(int side)
    {
        var chosen = Values.ToHashSet();
        if (side == 0)
        {
            if (CheckedOf(0).Any(chosen.Contains)) SetChecked(0, [.. CheckedOf(0).Where(key => !chosen.Contains(key))]);
            syncingTree = true;
            tree.Filter = SearchOf(0);
            // 树上勾着的：已经移过去的与这一边勾上的
            tree.Checked = [.. Values, .. CheckedOf(0)];
            tree.Refresh();
            syncingTree = false;
            return;
        }
        if (CheckedOf(1).Any(key => !chosen.Contains(key))) SetChecked(1, [.. CheckedOf(1).Where(chosen.Contains)]);
        var target = (Listbox)SideParts(1).Body;
        target.ItemsSource = Values.Where(key => nodes.ContainsKey(key) && Matches(key, 1)).Select(key => (object?)new ListboxOption(key, LabelOf(nodes[key]))).ToList();
        target.Values = CheckedOf(1);
    }

    /// <inheritdoc />
    protected override bool CanMove(string key, int from) => nodes.ContainsKey(key) && Values.Contains(key) == (from == 1);
}

/// <summary>Transfer、TreeTransfer 的部件名（TS 的 propsPanel、propsSearch、propsAction、propsCheckAll、propsList、propsTree 与 transferRecipe 的部件）。</summary>
public static class TransferParts
{
    /// <summary>每一边的外框（TS 的 section）：列表的高度改它的 Height。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>标题栏：全选、标题、计数。</summary>
    public const string Header = "PART_Header";

    /// <summary>全选的勾选框：只管当前看得见的项。</summary>
    public const string CheckAll = "PART_CheckAll";

    /// <summary>标题（中等字重，放不下省略）。</summary>
    public const string Title = "PART_Title";

    /// <summary>"勾上 / 总数"（sm、muted）。</summary>
    public const string Count = "PART_Count";

    /// <summary>搜索框的外层（四周 xs）。</summary>
    public const string Search = "PART_Search";

    /// <summary>列表（可多选的 Listbox）。</summary>
    public const string List = "PART_List";

    /// <summary>TreeTransfer 左边的树（勾选的 Tree）。</summary>
    public const string Tree = "PART_Tree";

    /// <summary>中间的按钮（Flex）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>移过去的按钮（IconButton，soft、sm）。</summary>
    public const string Action = "PART_Action";
}
