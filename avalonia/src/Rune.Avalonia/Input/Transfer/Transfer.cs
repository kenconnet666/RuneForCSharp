using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 穿梭框（vue-tsx-pro 的 Transfer）：左边是可选的，右边是已选的，勾上之后点中间的按钮移过去（分配权限、指派成员、挑选导出的列）。
/// <code>
/// &lt;rune:Transfer ItemsSource="{Binding Users}" ToOption="{Binding UserOption}" Values="{Binding MemberIds}" Searchable="True" /&gt;
/// </code>
/// <list type="bullet">
/// <item>每一边是一个可多选的 <see cref="Listbox"/>（焦点、方向键、空格勾选、虚拟滚动同 Listbox），上方是全选框、标题与"勾上 / 总数"，
/// <see cref="Searchable"/> 时带搜索框（按选项文字）。全选只管当前看得见（搜索后留下）的项。</item>
/// <item>中间两个按钮把勾上的项移到另一边（没有勾上的时不可用），双击一项直接移过去；移动后读屏播报移了几项。不可用的选项勾不上，也就移不动。</item>
/// <item><see cref="Values"/> 是右边的值，按移过去的先后排列；ItemsSource 里找不到的值不显示（也不会被删掉）。</item>
/// <item>窄于 40rem 时上下排，中间的按钮横排、箭头转成上下。</item>
/// </list>
/// 部件：<see cref="TransferParts"/>。
/// </summary>
public class Transfer : Control
{
    /// <summary>全部数据（两边一起）：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Transfer, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Transfer, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>右边（已选）的值，按移过去的先后排列（TS 的 _value）；默认双向绑定。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ValuesProperty =
        AvaloniaProperty.Register<Transfer, IReadOnlyList<string>>(nameof(Values), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>左右两边的标题；不写时取语言包（可选、已选）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> TitlesProperty = AvaloniaProperty.Register<Transfer, IReadOnlyList<string>?>(nameof(Titles));

    /// <summary>两边带搜索框。</summary>
    public static readonly StyledProperty<bool> SearchableProperty = AvaloniaProperty.Register<Transfer, bool>(nameof(Searchable));

    /// <summary>搜索时怎样算匹配：参数是数据与输入；不写时选项文字包含输入（忽略大小写与重音，同 Select 的搜索）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Transfer, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>窄于它时上下排（40rem）。</summary>
    const double StackBelow = 40 * 16;

    readonly Side[] sides;
    readonly Flex actions = new() { Name = TransferParts.Actions, Direction = FlexDirection.Column, Justify = FlexJustify.Center, Gap = Space.Sm };
    readonly IconButton toTarget = new() { Name = TransferParts.Action, Icon = Lucide.ChevronRight, Variant = ButtonVariant.Soft, Size = TextSize.Sm };
    readonly IconButton toSource = new() { Name = TransferParts.Action, Icon = Lucide.ChevronLeft, Variant = ButtonVariant.Soft, Size = TextSize.Sm };
    readonly LiveRegion announcer = new();
    readonly Layout layout;
    List<(object? Item, ListboxOption Option)> entries = [];

    /// <summary>新建：左右两边与中间的按钮。</summary>
    public Transfer()
    {
        sides = [new Side(this, 0), new Side(this, 1)];
        actions.Children.AddRange([toTarget, toSource]);
        toTarget.Click += (_, _) => Move(0, sides[0].Checked);
        toSource.Click += (_, _) => Move(1, sides[1].Checked);
        layout = new Layout { Children = { sides[0].Panel, actions, sides[1].Panel, announcer } };
        LogicalChildren.Add(layout);
        VisualChildren.Add(layout);
    }

    /// <summary>全部数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>右边的值。</summary>
    public IReadOnlyList<string> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>左右两边的标题。</summary>
    public IReadOnlyList<string>? Titles { get => GetValue(TitlesProperty); set => SetValue(TitlesProperty, value); }

    /// <summary>两边带搜索框。</summary>
    public bool Searchable { get => GetValue(SearchableProperty); set => SetValue(SearchableProperty, value); }

    /// <summary>搜索的匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>两边（测试用）：0 左、1 右。</summary>
    internal (Listbox List, Checkbox CheckAll, SearchField Search, Text Count) SideParts(int side) => (sides[side].List, sides[side].CheckAll, sides[side].Search, sides[side].Count);

    /// <summary>读屏播报的文字（测试用）。</summary>
    internal string? Announcement => announcer.Text;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty)
        {
            entries = [.. (ItemsSource?.Cast<object?>() ?? []).Select(item => (item, OptionOf(item)))];
            Sync();
        }
        else if (change.Property == ValuesProperty || change.Property == TitlesProperty || change.Property == SearchableProperty || change.Property == FilterProperty
                 || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty) Sync();
    }

    ListboxOption OptionOf(object? item) => item switch
    {
        ListboxOption option => option,
        _ when ToOption is { } map => map(item),
        _ => new ListboxOption(item?.ToString() ?? "", item?.ToString() ?? ""),
    };

    string TitleOf(int side) => Titles?.ElementAtOrDefault(side) is { } title ? title : side == 0 ? Lang.TransferSource : Lang.TransferTarget;

    Lang Lang => Provider.GetLang(this);

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

    bool Matches((object? Item, ListboxOption Option) entry, string query)
    {
        var text = query.Trim();
        if (text.Length == 0) return true;
        return Filter is { } filter ? filter(entry.Item, text) : SelectSearch.Matches(entry.Option.Label, text);
    }

    /// <summary>两边的列表、全选、计数、标题与中间按钮的可用跟着数据、已选与勾选。</summary>
    void Sync()
    {
        foreach (var side in sides) side.Sync();
        var enabled = IsEffectivelyEnabled;
        toTarget.IsEnabled = enabled && sides[0].Checked.Count > 0;
        toSource.IsEnabled = enabled && sides[1].Checked.Count > 0;
        AutomationProperties.SetName(toTarget, Lang.TransferMoveTo(TitleOf(1)));
        AutomationProperties.SetName(toSource, Lang.TransferMoveTo(TitleOf(0)));
    }

    /// <summary>把 keys 从 from 这一边移到另一边：不可用的、不在这一边的跳过；移完播报。</summary>
    void Move(int from, IReadOnlyList<string> keys)
    {
        var chosen = Values.ToHashSet();
        var byValue = entries.GroupBy(entry => entry.Option.Value).ToDictionary(group => group.Key, group => group.First().Option);
        var movable = keys.Where(key => byValue.TryGetValue(key, out var option) && !option.IsDisabled && chosen.Contains(key) == (from == 1)).ToList();
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

    /// <summary>一边：外框（border、md 圆角、surface 底，20em 高）里是标题栏（全选、标题、计数）、搜索框与列表。</summary>
    sealed class Side
    {
        readonly Transfer owner;
        readonly int index;
        readonly Text title = new() { Name = TransferParts.Title, Weight = TextWeight.Medium, Truncate = true };
        readonly Flex header;
        readonly Border headerBox = new();
        readonly Border search = new() { Name = TransferParts.Search };
        bool syncing;

        public Side(Transfer owner, int index)
        {
            (this.owner, this.index) = (owner, index);
            CheckAll.PropertyChanged += (_, e) =>
            {
                if (syncing || e.Property != Checkbox.IsCheckedProperty) return;
                var selectable = Selectable();
                var others = Checked.Where(key => !selectable.Contains(key));
                Checked = CheckAll.IsChecked ? [.. others, .. selectable] : [.. others];
            };
            List.PropertyChanged += (_, e) =>
            {
                if (!syncing && e.Property == Listbox.ValuesProperty) Checked = List.Values ?? [];
            };
            // 双击一项直接移过去
            List.DoubleTapped += (_, e) =>
            {
                if ((e.Source as Visual)?.FindAncestorOfType<ListboxItem>(includeSelf: true)?.Option is { } option) owner.Move(index, [option.Value]);
            };
            Search.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextField.TextProperty) owner.Sync();
            };
            search.Child = Search;
            Flex.SetGrow(title, true);
            header = new Flex { Name = TransferParts.Header, Align = FlexAlign.Center, Gap = Space.Sm, Children = { CheckAll, title, Count } };
            headerBox.Child = header;
            var body = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(headerBox, Dock.Top);
            DockPanel.SetDock(search, Dock.Top);
            body.Children.AddRange([headerBox, search, List]);
            Panel.Child = body;
            Panel.SizeChanged += (_, _) => Metrics();
            Panel.AttachedToVisualTree += (_, _) => Metrics();
        }

        public Border Panel { get; } = new() { Name = TransferParts.Panel, ClipToBounds = true };

        public Checkbox CheckAll { get; } = new() { Name = TransferParts.CheckAll };

        public Text Count { get; } = new() { Name = TransferParts.Count, Size = TextSize.Sm, Tone = TextTone.Muted };

        public SearchField Search { get; } = new() { Variant = ControlVariant.Soft, Size = TextSize.Sm };

        public Listbox List { get; } = new() { Name = TransferParts.List, SelectionMode = SelectionMode.Multiple, VerticalAlignment = VerticalAlignment.Stretch };

        /// <summary>勾上的值。</summary>
        public IReadOnlyList<string> Checked
        {
            get;
            set
            {
                field = value;
                owner.Sync();
            }
        } = [];

        /// <summary>看得见的项。</summary>
        List<(object? Item, ListboxOption Option)> Shown() => [.. owner.SideEntries(index).Where(entry => owner.Matches(entry, Search.Text ?? ""))];

        /// <summary>看得见、能勾的值：全选只管它们。</summary>
        HashSet<string> Selectable() => [.. Shown().Where(entry => !entry.Option.IsDisabled).Select(entry => entry.Option.Value)];

        public void Sync()
        {
            syncing = true;
            var lang = owner.Lang;
            var name = owner.TitleOf(index);
            var shown = Shown();
            var selectable = Selectable();
            var all = owner.SideEntries(index);
            // 移走、被搜索藏起来的项不再算勾上
            var valid = all.Select(entry => entry.Option.Value).ToHashSet();
            if (Checked.Any(key => !valid.Contains(key))) Checked = [.. Checked.Where(valid.Contains)];
            List.ItemsSource = shown.Select(entry => (object?)entry.Option).ToList();
            List.Values = Checked;
            title.Text = name;
            AutomationProperties.SetName(List, name);
            AutomationProperties.SetName(Panel, name);
            Count.Text = lang.TransferCount(Checked.Count, all.Count);
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
    sealed class Layout : Panel
    {
        bool Stacked(double width) => width < StackBelow;

        protected override Size MeasureOverride(Size availableSize)
        {
            var transfer = (Transfer)Parent!;
            var gap = transfer.Resource<double>(ThemeKeys.Space.Md) * TextElement.GetFontSize(transfer);
            var stacked = Stacked(availableSize.Width);
            transfer.actions.Direction = stacked ? FlexDirection.Row : FlexDirection.Column;
            foreach (var button in new[] { transfer.toTarget, transfer.toSource }) button.RenderTransform = stacked ? new global::Avalonia.Media.RotateTransform(90) : null;
            var (left, actions, right) = (Children[0], Children[1], Children[2]);
            actions.Measure(Size.Infinity);
            Children[3].Measure(Size.Infinity);
            if (stacked)
            {
                left.Measure(availableSize.WithHeight(double.PositiveInfinity));
                right.Measure(availableSize.WithHeight(double.PositiveInfinity));
                return new Size(availableSize.Width, left.DesiredSize.Height + actions.DesiredSize.Height + right.DesiredSize.Height + 2 * gap);
            }
            var side = Math.Max(0, (availableSize.Width - actions.DesiredSize.Width - 2 * gap) / 2);
            left.Measure(new Size(side, availableSize.Height));
            right.Measure(new Size(side, availableSize.Height));
            return new Size(double.IsInfinity(availableSize.Width) ? 2 * Math.Max(left.DesiredSize.Width, right.DesiredSize.Width) + actions.DesiredSize.Width + 2 * gap : availableSize.Width,
                Math.Max(Math.Max(left.DesiredSize.Height, right.DesiredSize.Height), actions.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var transfer = (Transfer)Parent!;
            var gap = transfer.Resource<double>(ThemeKeys.Space.Md) * TextElement.GetFontSize(transfer);
            var (left, actions, right) = (Children[0], Children[1], Children[2]);
            Children[3].Arrange(new Rect(0, 0, 0, 0));
            if (Stacked(finalSize.Width))
            {
                var y = 0d;
                left.Arrange(new Rect(0, y, finalSize.Width, left.DesiredSize.Height));
                y += left.DesiredSize.Height + gap;
                actions.Arrange(new Rect((finalSize.Width - actions.DesiredSize.Width) / 2, y, actions.DesiredSize.Width, actions.DesiredSize.Height));
                y += actions.DesiredSize.Height + gap;
                right.Arrange(new Rect(0, y, finalSize.Width, right.DesiredSize.Height));
                return finalSize;
            }
            var side = Math.Max(0, (finalSize.Width - actions.DesiredSize.Width - 2 * gap) / 2);
            left.Arrange(new Rect(0, 0, side, finalSize.Height));
            actions.Arrange(new Rect(side + gap, 0, actions.DesiredSize.Width, finalSize.Height));
            right.Arrange(new Rect(finalSize.Width - side, 0, side, finalSize.Height));
            return finalSize;
        }
    }
}

/// <summary>Transfer 的部件名（TS 的 propsPanel、propsSearch、propsAction、propsCheckAll、propsList 与 transferRecipe 的部件）。</summary>
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

    /// <summary>中间的按钮（Flex）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>移过去的按钮（IconButton，soft、sm）。</summary>
    public const string Action = "PART_Action";
}
