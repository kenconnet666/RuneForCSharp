using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 列表框（vue-tsx-pro 的 Listbox）：在一列选项里单选或多选。数据驱动、虚拟滚动（上万个选项也只建看得见的几十行）。
/// <code>
/// &lt;rune:Listbox ItemsSource="{Binding Cities}" Value="{Binding City}" AutomationProperties.Name="城市" /&gt;
/// &lt;rune:Listbox ItemsSource="{Binding Columns}" SelectionMode="multiple" Values="{Binding Visible}" Height="256" /&gt;
/// </code>
/// 选项是 <see cref="ListboxOption"/>（值、文字、不可用、说明、图标、分组）或字符串；别的数据给 <see cref="ToOption"/>，<see cref="ItemTemplate"/> 自定义选项内容时拿到的仍是原始数据。
/// 焦点在列表上：↑↓、Home / End、Page Up / Down 移动高亮，Enter / 空格选中，按字母跳到以它开头的项（状态与键盘见 <see cref="ListState"/>）；
/// 指针悬停高亮、点击选中。分组的组名是一行小标题。没有选项时显示 <see cref="Empty"/>（默认语言包的"没有选项"），<see cref="IsLoading"/> 时换成几行骨架。
/// 自己滚动时最高 20em（写了 Height 时按它）。放进 Field 时以标签为名称，沿用必填（单选时再点已选项不取消）。部件样式键：<see cref="ListboxParts"/>。
/// </summary>
public class Listbox : FrameworkElement, IFieldControl
{
    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(Listbox),
        new FrameworkPropertyMetadata(null, (d, _) => ((Listbox)d).Rebuild()));

    /// <summary>数据 → 选项；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly DependencyProperty ToOptionProperty = DependencyProperty.Register(nameof(ToOption), typeof(Func<object?, ListboxOption>), typeof(Listbox),
        new FrameworkPropertyMetadata(null, (d, _) => ((Listbox)d).Rebuild()));

    /// <summary>选择方式，默认 single。</summary>
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(Listbox),
        new FrameworkPropertyMetadata(SelectionMode.Single, (d, _) => ((Listbox)d).SyncSelection()));

    /// <summary>单选：选中项的值（默认双向绑定）；没有选中时为空。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Listbox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Listbox)d).SyncSelection()));

    /// <summary>多选：选中项的值（默认双向绑定）。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<string>), typeof(Listbox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Listbox)d).SyncSelection()));

    /// <summary>必须有选中项：单选时再点已选项不取消；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool?), typeof(Listbox),
        new FrameworkPropertyMetadata(null, (d, _) => ((Listbox)d).SyncSelection()));

    /// <summary>只读：能聚焦、看得到选中项，不能改（查看详情、审批时）；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool?), typeof(Listbox));

    /// <summary>加载中：选项换成骨架行（很快完成时不闪）。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(Listbox),
        new FrameworkPropertyMetadata(false, (d, _) => ((Listbox)d).loading.Update(((Listbox)d).IsLoading || ((Listbox)d).loadingMore)));

    /// <summary>自定义选项内容：数据模板拿到 ItemsSource 里的原始数据。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(Listbox),
        new FrameworkPropertyMetadata(null, (d, _) => ((Listbox)d).Refresh()));

    /// <summary>没有选项时显示的内容；不写时是 Empty（文案取语言包）。</summary>
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(Listbox),
        new FrameworkPropertyMetadata(null, (d, _) => ((Listbox)d).UpdateBody()));

    /// <summary>点选了一项（点击、Enter、空格；再选已选的那项也会发）。</summary>
    public static readonly RoutedEvent PickedEvent = EventManager.RegisterRoutedEvent(nameof(Picked), RoutingStrategy.Bubble, typeof(EventHandler<ListboxPickedEventArgs>), typeof(Listbox));

    readonly LayoutGrid root = new();
    readonly ListboxRows rows;
    readonly StackPanel skeleton = new() { Visibility = Visibility.Collapsed };
    readonly ContentPresenter empty = new() { Visibility = Visibility.Collapsed };
    readonly Border focusRing = new() { Name = "PART_FocusRing", Visibility = Visibility.Collapsed, IsHitTestVisible = false, BorderThickness = new Thickness(2) };
    readonly DelayedLoading loading;
    bool syncing;
    bool loadingMore;

    static Listbox()
    {
        FocusableProperty.OverrideMetadata(typeof(Listbox), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(Listbox), new FrameworkPropertyMetadata(null));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Listbox), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Listbox)d).UpdateMetrics()));
    }

    /// <summary>新建：列表状态、虚拟滚动的行、骨架与空状态。</summary>
    public Listbox()
    {
        State = new ListState(OnSelectionChanged);
        State.ActiveChanged += OnActiveChanged;
        rows = new ListboxRows(this);
        rows.SetResourceReference(StyleProperty, ListboxParts.Rows);
        skeleton.SetResourceReference(StyleProperty, ListboxParts.Skeleton);
        loading = new DelayedLoading(shown =>
        {
            skeleton.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            UpdateBody();
        });
        for (var index = 0; index < 4; index++)
            skeleton.Children.Add(new Skeleton { Delay = index * 100, HorizontalAlignment = HorizontalAlignment.Left, Tag = 0.85 - index * 0.15 });
        // 平时行、骨架、空状态三选一（叠在同一格）；取下一页时骨架挪到行下面那一格（固定高度时行照样在第一格里滚动）
        var body = new System.Windows.Controls.Grid { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = GridLength.Auto } }, Children = { rows, skeleton } };
        root.Children.Add(body);
        root.Children.Add(empty);
        rows.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => CheckReached(e.ExtentHeight - e.VerticalOffset - e.ViewportHeight)));
        // 键盘聚焦时一圈 2 像素的焦点环画在里面：列表自己滚动，外面一圈会被滚动区域裁掉（上游 c6b0309d 同样画在里面）
        focusRing.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.FocusRing);
        root.Children.Add(focusRing);
        AddLogicalChild(root);
        AddVisualChild(root);
        GotKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus == this) focusRing.Visibility = InputManager.Current.MostRecentInputDevice is KeyboardDevice ? Visibility.Visible : Visibility.Collapsed;
            if (State.Active is null) State.ActivateDefault();
        };
        LostKeyboardFocus += (_, _) => focusRing.Visibility = Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            UpdateMetrics();
            UpdateBody();
        };
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => (Func<object?, ListboxOption>?)GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => (SelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的值。</summary>
    public IReadOnlyList<string>? Values { get => (IReadOnlyList<string>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>必选。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>选项的数据模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>空状态。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>点选了一项。</summary>
    public event EventHandler<ListboxPickedEventArgs> Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

    /// <summary>列表的状态（选项、高亮、已选、键盘）：Select 把按键转给它。</summary>
    public ListState State { get; }

    /// <summary>已经建出来的选项行（测试用）。</summary>
    internal IEnumerable<ListboxItem> RealizedItems => rows.Hosts.Select(host => host.Child).OfType<ListboxItem>();

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    bool Required => IsRequired ?? Field.Of(this)?.IsRequired ?? false;

    // 只读时键盘、点选都不响应（上游 keydown 里同不可用一起返回），只看
    bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    void IFieldControl.FieldChanged() => SyncSelection();

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 骨架行的宽度按列表宽度的比例
        var em = TextElement.GetFontSize(this);
        foreach (var bar in skeleton.Children.OfType<Skeleton>()) bar.Width = Math.Max(0, (finalSize.Width - 4 * em) * (double)bar.Tag);
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>数据 → 选项（按分组排好）→ 行：每组前插一行组名。</summary>
    void Rebuild()
    {
        // 初始化器、XAML 里 ItemsSource 可能先于 ToOption 设：数据不是选项、又还没有 ToOption 时先不建
        var items = ItemsSource?.Cast<object?>().ToList() ?? [];
        if (ToOption is null && items.Any(item => item is not (ListboxOption or string))) return;
        State.SetItems(items, ToOption);
        var list = new List<object>();
        string? group = null;
        for (var index = 0; index < State.Options.Count; index++)
        {
            var entry = State.Options[index];
            if (entry.Option.Group is { } name && name != group) list.Add(new ListboxGroupRow(name));
            group = entry.Option.Group;
            list.Add(new ListboxOptionRow(entry, index));
        }
        rows.ItemsSource = list;
        SyncSelection();
        UpdateBody();
    }

    /// <summary>值 → 列表状态的已选项。</summary>
    void SyncSelection()
    {
        if (syncing) return;
        State.Mode = SelectionMode;
        State.Required = Required;
        State.Selected = Multiple ? Values ?? [] : Value is { } value ? [value] : [];
        Refresh();
    }

    /// <summary>列表状态的选择 → 值（写回绑定）。</summary>
    void OnSelectionChanged(IReadOnlyList<string> next)
    {
        syncing = true;
        if (Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.FirstOrDefault());
        syncing = false;
        Refresh();
    }

    /// <summary>高亮项变了：重画，滚进可见范围（高亮第一项、上面是组名时滚到最顶上，组名不被挡住）。</summary>
    void OnActiveChanged()
    {
        Refresh();
        if (State.Active is not { } value || rows.ItemsSource is not IList list) return;
        var index = list.IndexOf(list.OfType<ListboxOptionRow>().FirstOrDefault(row => row.Entry.Option.Value == value));
        if (index >= 0) rows.BringIntoView(index == 1 && list[0] is ListboxGroupRow ? 0 : index);
    }

    /// <summary>已经建出来的行按状态重画。</summary>
    internal void Refresh()
    {
        foreach (var item in RealizedItems) item.Sync();
    }

    /// <summary>正在取下一页（远程选项分页）：骨架少两行、接在已有的行下面（TS 的 loading="more"）。</summary>
    internal bool IsLoadingMore
    {
        get => loadingMore;
        set
        {
            if (loadingMore == value) return;
            loadingMore = value;
            for (var index = 2; index < skeleton.Children.Count; index++) skeleton.Children[index].Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            System.Windows.Controls.Grid.SetRow(skeleton, value ? 1 : 0);
            loading.Update(IsLoading || value);
        }
    }

    /// <summary>滚到了离末尾不远的地方（5 行以内，含内容不满一屏）：参数是最后一项的序号。远程选项据此取下一页。</summary>
    internal event Action<int>? Reached;

    void CheckReached(double remaining)
    {
        if (Reached is null || State.Options.Count == 0) return;
        if (remaining <= 5 * this.Resource<double>(ThemeKeys.Size.Control) * TextElement.GetFontSize(this)) Reached(State.Options.Count - 1);
    }

    /// <summary>行、骨架、空状态三选一（取下一页时行与骨架都在）。</summary>
    void UpdateBody()
    {
        var hasRows = State.Options.Count > 0;
        var skeletonShown = skeleton.Visibility == Visibility.Visible;
        rows.Visibility = (!skeletonShown || loadingMore) && hasRows ? Visibility.Visible : Visibility.Collapsed;
        empty.Visibility = !skeletonShown && !hasRows && !IsLoading ? Visibility.Visible : Visibility.Collapsed;
        empty.Content = Empty ?? new Empty { Size = TextSize.Sm, Description = Provider.GetLang(this).SelectEmpty };
    }

    /// <summary>自己滚动时的最高高度（设备无关像素）；null 时 20em（命令面板换成 min(24rem, 窗口高度一半)）。</summary>
    internal double? ScrollMaxHeight { get; set { field = value; UpdateMetrics(); } }

    /// <summary>按 em 的尺寸：内边距 space.xs，自己滚动时最高 20em（或 <see cref="ScrollMaxHeight"/>），骨架行高 1em、上下 0.625em。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        rows.Padding = new Thickness(xs);
        skeleton.Margin = new Thickness(xs);
        rows.MaxHeight = double.IsNaN(Height) ? ScrollMaxHeight ?? 20 * em : double.PositiveInfinity;
        foreach (var bar in skeleton.Children.OfType<Skeleton>())
        {
            bar.Height = em;
            bar.Margin = new Thickness(sm, 0.625 * em, sm, 0.625 * em);
        }
    }

    /// <summary>点选了一项：切换选中、发 Picked。</summary>
    internal void Pick(string value)
    {
        if (!IsEnabled || ReadOnly) return;
        State.SetActive(value);
        State.Select(value);
        RaiseEvent(new ListboxPickedEventArgs(PickedEvent, value));
    }

    /// <summary>键盘：↑↓、Home / End、Page Up / Down、Enter / 空格（同 TS 的 handleListKeydown）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled || ReadOnly || (Keyboard.Modifiers & ~ModifierKeys.Shift) != 0) return;
        if (ToListKey(e.Key) is { } key) e.Handled = State.Handle(key, picked: value => RaiseEvent(new ListboxPickedEventArgs(PickedEvent, value)));
    }

    /// <summary>字母：类型搜索。</summary>
    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && IsEnabled && !ReadOnly && e.Text is { Length: 1 } text) e.Handled = State.Typeahead(text);
    }

    /// <summary>平台的按键 → 列表的按键；不是列表认的键时为 null。</summary>
    /// <param name="key">按键。</param>
    public static ListKey? ToListKey(Key key) => key switch
    {
        Key.Down => ListKey.Down,
        Key.Up => ListKey.Up,
        Key.Home => ListKey.Home,
        Key.End => ListKey.End,
        Key.PageDown => ListKey.PageDown,
        Key.PageUp => ListKey.PageUp,
        Key.Enter => ListKey.Enter,
        Key.Space => ListKey.Space,
        _ => null,
    };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListboxPeer(this);

    /// <summary>读屏：列表（能多选、必选）；没写名称时取所在字段的标签。</summary>
    sealed class ListboxPeer(Listbox owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
    {
        protected override string GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(Listbox);

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

        public bool CanSelectMultiple => owner.Multiple;

        public bool IsSelectionRequired => owner.Required;

        public IRawElementProviderSimple[] GetSelection() =>
            [.. owner.RealizedItems.Where(item => item.IsSelected).Select(item => ProviderFromPeer(CreatePeerForElement(item)))];
    }
}

/// <summary>点选了一项的事件参数。</summary>
/// <param name="routedEvent">事件。</param>
/// <param name="value">选项的值。</param>
public sealed class ListboxPickedEventArgs(RoutedEvent routedEvent, string value) : RoutedEventArgs(routedEvent)
{
    /// <summary>选项的值。</summary>
    public string Value { get; } = value;
}

/// <summary>渲染用的一行：分组标题。</summary>
/// <param name="Label">组名。</param>
sealed record ListboxGroupRow(string Label);

/// <summary>渲染用的一行：选项。</summary>
/// <param name="Entry">选项与原始数据。</param>
/// <param name="Index">在全部选项里的位置。</param>
sealed record ListboxOptionRow(ListStateEntry Entry, int Index);

/// <summary>虚拟滚动的行：容器是 <see cref="ListboxRowHost"/>，按行的种类放组名或选项；回收复用时换内容。</summary>
sealed class ListboxRows(Listbox owner) : ItemsControl
{
    readonly List<ListboxRowHost> hosts = [];

    public IEnumerable<ListboxRowHost> Hosts => hosts.Where(host => host.IsVisible || host.Child is not null);

    protected override DependencyObject GetContainerForItemOverride()
    {
        var host = new ListboxRowHost(owner);
        hosts.Add(host);
        return host;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListboxRowHost;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item) => ((ListboxRowHost)element).Show(item);

    protected override void ClearContainerForItemOverride(DependencyObject element, object item) => ((ListboxRowHost)element).Show(null);

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

/// <summary>一行的容器：组名（<c>Text</c>，部件样式 <see cref="ListboxParts.GroupLabel"/>）或选项（<see cref="ListboxItem"/>）。</summary>
sealed class ListboxRowHost(Listbox owner) : Decorator
{
    Text? label;
    ListboxItem? item;

    public void Show(object? row)
    {
        switch (row)
        {
            case ListboxGroupRow group:
                label ??= CreateLabel();
                label.Text = group.Label;
                // 上 space.sm、下 space.xs、左右 space.sm（按组名自己的字号）
                var em = label.FontSize;
                label.Margin = new Thickness(0.5 * em, 0.5 * em, 0.5 * em, 0.25 * em);
                Child = label;
                break;
            case ListboxOptionRow option:
                item ??= new ListboxItem(owner);
                item.Row = option;
                Child = item;
                break;
            default:
                Child = null;
                break;
        }
    }

    static Text CreateLabel()
    {
        var text = new Text { Size = TextSize.Sm, Tone = TextTone.Muted };
        text.SetResourceReference(StyleProperty, ListboxParts.GroupLabel);
        // 只是视觉上的标题：组名经各选项的说明（HelpText）念出
        AutomationProperties.SetIsOffscreenBehavior(text, IsOffscreenBehavior.Offscreen);
        return text;
    }
}

/// <summary>
/// 列表里的一个选项（<see cref="Listbox"/> 建的，不单独用）：图标、文字（下面一行说明）、已选标记。不可聚焦，焦点留在列表、触发按钮或搜索框上。
/// 主题样式按 <see cref="IsActive"/>（高亮）、<see cref="IsSelected"/>、IsEnabled（不可用的选项）换底色、文字颜色。
/// </summary>
public class ListboxItem : Control
{
    static readonly DependencyPropertyKey IsActiveKey = DependencyProperty.RegisterReadOnly(nameof(IsActive), typeof(bool), typeof(ListboxItem), new PropertyMetadata(false));

    /// <summary>高亮（键盘或指针所在）。</summary>
    public static readonly DependencyProperty IsActiveProperty = IsActiveKey.DependencyProperty;

    static readonly DependencyPropertyKey IsSelectedKey = DependencyProperty.RegisterReadOnly(nameof(IsSelected), typeof(bool), typeof(ListboxItem), new PropertyMetadata(false));

    /// <summary>已选。</summary>
    public static readonly DependencyProperty IsSelectedProperty = IsSelectedKey.DependencyProperty;

    readonly Listbox owner;
    readonly Border root = new();
    readonly Flex layout = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new();
    readonly StackPanel content = new() { Name = "PART_Content" };
    readonly TextBlock label = new() { TextTrimming = TextTrimming.CharacterEllipsis, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
    readonly Text description = new() { Size = TextSize.Sm, Tone = TextTone.Muted, TextWrapping = TextWrapping.Wrap };
    readonly ContentPresenter custom = new();
    readonly Icon check = new() { Name = "PART_Check", Data = Lucide.Check };
    bool pressed;

    static ListboxItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListboxItem), new FrameworkPropertyMetadata(typeof(ListboxItem)));
        FocusableProperty.OverrideMetadata(typeof(ListboxItem), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建一行。</summary>
    /// <param name="owner">所在的列表。</param>
    internal ListboxItem(Listbox owner)
    {
        this.owner = owner;
        check.SetResourceReference(StyleProperty, ListboxParts.Check);
        content.SetResourceReference(StyleProperty, ListboxParts.Content);
        content.Children.Add(label);
        content.Children.Add(description);
        content.Children.Add(custom);
        Flex.SetGrow(content, true);
        layout.Children.Add(icon);
        layout.Children.Add(content);
        layout.Children.Add(check);
        root.Child = layout;
        root.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { Source = this });
        AddVisualChild(root);
        AddLogicalChild(root);
    }

    /// <summary>高亮。</summary>
    public bool IsActive => (bool)GetValue(IsActiveProperty);

    /// <summary>已选。</summary>
    public bool IsSelected => (bool)GetValue(IsSelectedProperty);

    /// <summary>这一行的选项。</summary>
    public ListboxOption? Option => Row?.Entry.Option;

    internal ListboxOptionRow? Row
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <summary>按选项与状态更新：内容、状态、读屏名称；尺寸按 em（控件高度、上下 space.xs、左右 space.sm、sm 圆角，行高 normal）。</summary>
    internal void Sync()
    {
        if (Row is not { } row) return;
        var option = row.Entry.Option;
        var template = owner.ItemTemplate;
        icon.Data = option.Icon;
        icon.Visibility = template is null && option.Icon is not null ? Visibility.Visible : Visibility.Collapsed;
        label.Text = option.Label;
        label.Visibility = template is null ? Visibility.Visible : Visibility.Collapsed;
        description.Text = option.Description;
        description.Visibility = template is null && option.Description is not null ? Visibility.Visible : Visibility.Collapsed;
        custom.Visibility = template is not null ? Visibility.Visible : Visibility.Collapsed;
        custom.ContentTemplate = template;
        custom.Content = template is null ? null : row.Entry.Item;
        SetValue(IsSelectedKey, owner.State.IsSelected(option.Value));
        SetValue(IsActiveKey, owner.State.Active == option.Value);
        check.Opacity = IsSelected ? 1 : 0;
        IsEnabled = !option.IsDisabled;
        AutomationProperties.SetName(this, option.Label);
        AutomationProperties.SetHelpText(this, option.Group ?? "");
        var em = FontSize;
        label.LineHeight = this.Resource<double>(ThemeKeys.LineHeight.Normal) * em;
        root.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em,
            this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em);
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        root.MinHeight = ControlHeight.Of(em, this.Resource<double>(ThemeKeys.Size.Control));
    }

    /// <summary>指针移过：高亮（不可用的收不到指针）。</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Option is { } option && owner.State.Active != option.Value) owner.State.SetActive(option.Value);
    }

    /// <summary>按下不抢焦点（焦点留在列表、触发按钮或搜索框上）。</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        pressed = true;
        e.Handled = true;
    }

    /// <summary>在这一行上按下、松开：选中（松开只投递给指针下面的元素）。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (pressed && Option is { } option) owner.Pick(option.Value);
        pressed = false;
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressed = false;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListboxItemPeer(this);

    /// <summary>读屏：列表项，选中状态与"选中"操作。</summary>
    sealed class ListboxItemPeer(ListboxItem owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(ListboxItem);

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public bool IsSelected => owner.IsSelected;

        public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(CreatePeerForElement(owner.owner));

        public void Select()
        {
            if (owner.Option is { } option && !IsSelected) owner.owner.Pick(option.Value);
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            if (owner.Option is { } option && IsSelected) owner.owner.Pick(option.Value);
        }
    }
}

/// <summary>列表框的部件样式键（DESIGN §7）。</summary>
public static class ListboxParts
{
    /// <summary>虚拟滚动的行（ItemsControl：模板里是浮层滚动条的 ScrollArea）。</summary>
    public static readonly PartKey Rows = new(typeof(Listbox), nameof(Rows), typeof(ItemsControl));

    /// <summary>分组标题（TS 的 propsGroupLabel）。</summary>
    public static readonly PartKey GroupLabel = new(typeof(Listbox), nameof(GroupLabel), typeof(Text));

    /// <summary>加载中的骨架行（TS 的 propsSkeleton）。</summary>
    public static readonly PartKey Skeleton = new(typeof(Listbox), nameof(Skeleton), typeof(StackPanel));

    /// <summary>选项里文字与说明的那一列。</summary>
    public static readonly PartKey Content = new(typeof(Listbox), nameof(Content), typeof(StackPanel));

    /// <summary>已选标记（TS 的 propsCheck）。</summary>
    public static readonly PartKey Check = new(typeof(Listbox), nameof(Check), typeof(Icon));
}
