using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 侧边导航（vue-tsx-pro 的 NavList）：里面放 <see cref="NavItem"/>（链接）、<see cref="NavGroup"/>（可展开的一组）、<see cref="NavSection"/>（带小标题的一组）。
/// <code>
/// &lt;rune:NavList AutomationProperties.Name="后台" CurrentHref="{Binding Page}"&gt;
///   &lt;rune:NavItem Href="/" Icon="{x:Static rune:Lucide.House}"&gt;概览&lt;/rune:NavItem&gt;
///   &lt;rune:NavGroup Label="订单" Icon="{x:Static rune:Lucide.Package}"&gt;
///     &lt;rune:NavItem Href="/orders"&gt;全部订单&lt;/rune:NavItem&gt;
///   &lt;/rune:NavGroup&gt;
/// &lt;/rune:NavList&gt;
/// </code>
/// 桌面上没有路由：<see cref="CurrentHref"/> 是当前地址（TS 路由适配的 isActive 按它判断，地址相同的项是当前页），
/// 点击导航项先发 <see cref="Navigate"/>（TS 交给路由的 navigate），没有被处理就把 CurrentHref 改成那一项的地址。
/// 当前页高亮（accentSoft 底、accentText），读屏的项状态是"当前页"；含当前页的分组自动展开。
/// <see cref="Compact"/>：各项只剩图标（名称留给读屏，悬停、聚焦时用提示显示），小节标题只剩它的图标。
/// 没做：紧凑时分组悬停弹出子项（等 HoverCard；现在紧凑的分组照常展开，子项也只剩图标）。
/// </summary>
public class NavList : Flex
{
    /// <summary>当前地址：Href 与它相同的导航项是当前页。默认双向绑定。</summary>
    public static readonly DependencyProperty CurrentHrefProperty = DependencyProperty.Register(nameof(CurrentHref), typeof(string), typeof(NavList),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NavList)d).Sync()));

    /// <summary>紧凑：只显示图标。</summary>
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(NavList),
        new FrameworkPropertyMetadata(false, (d, _) => ((NavList)d).Sync()));

    /// <summary>区域里的导航都紧凑（TS 的 NavCompactContext）：沿元素树继承，AppShell 把侧栏收成图标栏时设在侧栏上。</summary>
    public static readonly DependencyProperty CompactScopeProperty = DependencyProperty.RegisterAttached("CompactScope", typeof(bool), typeof(NavList),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, (d, _) => (d as NavList)?.Sync()));

    /// <summary>点了导航项（切换之前）：标记 Handled 时不改 CurrentHref（TS 的路由接管跳转）。</summary>
    public static readonly RoutedEvent NavigateEvent = EventManager.RegisterRoutedEvent(nameof(Navigate), RoutingStrategy.Bubble, typeof(EventHandler<NavigateEventArgs>), typeof(NavList));

    /// <summary>新建：竖排、间距 space.xs。</summary>
    public NavList()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        Loaded += (_, _) => Sync();
    }

    /// <summary>当前地址。</summary>
    public string? CurrentHref { get => (string?)GetValue(CurrentHrefProperty); set => SetValue(CurrentHrefProperty, value); }

    /// <summary>紧凑。</summary>
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>实际是否紧凑：自己写了 <see cref="Compact"/>，或在紧凑的区域里。</summary>
    internal bool IsCompact => Compact || (bool)GetValue(CompactScopeProperty);

    /// <summary>区域里的导航是否都紧凑。</summary>
    public static bool GetCompactScope(DependencyObject element) => (bool)element.GetValue(CompactScopeProperty);

    /// <summary>让区域里的导航都紧凑。</summary>
    public static void SetCompactScope(DependencyObject element, bool value) => element.SetValue(CompactScopeProperty, value);

    /// <summary>点了导航项。</summary>
    public event EventHandler<NavigateEventArgs> Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <summary>属于这个导航的成员（不进到里面另一个 NavList）。</summary>
    IEnumerable<T> Members<T>() => Descendants(this).OfType<T>();

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            if (child is NavList) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>元素属于哪个 NavList（逻辑树上最近的一个）。</summary>
    internal static NavList? Owner(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            if (parent is NavList list) return list;
        return null;
    }

    /// <summary>项加进来时就同步：含当前页的分组在加载前展开，不播展开的过渡。</summary>
    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        Sync();
    }

    /// <summary>当前页、紧凑交给各项；含当前页的分组展开。</summary>
    internal void Sync()
    {
        foreach (var row in Members<NavRow>()) row.Apply(this);
        foreach (var section in Members<NavSection>()) section.Apply(this);
        foreach (var group in Members<NavGroup>()) group.Apply(this);
    }

    /// <summary>点了导航项：先发 Navigate，没有被处理就切过去。</summary>
    internal void Go(NavItem item)
    {
        var args = new NavigateEventArgs(NavigateEvent, this, item.Href);
        RaiseEvent(args);
        if (!args.Handled && item.Href is { } href) SetCurrentValue(CurrentHrefProperty, href);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavListPeer(this);

    sealed class NavListPeer(NavList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(NavList);

        // WPF 的读屏节点没有地标类型：本地化的控件类型写"导航"
        protected override string GetLocalizedControlTypeCore() => "导航";
    }
}

/// <summary>点了导航项的参数：要去的地址；标记 Handled 时 NavList 不改当前地址。</summary>
public class NavigateEventArgs(RoutedEvent routedEvent, object source, string? href) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>要去的地址。</summary>
    public string? Href { get; } = href;
}

/// <summary>
/// 导航的一行（导航项与分组标题共用，TS 的 itemStyles.row 加 navListStyles.row）：图标、文字、尾部内容横排，控件高度、上下 space.xs、左右 space.md、md 圆角，
/// 中等字重，平时 textMuted，悬停 text 与 fillHover 底；当前页 accentSoft 底、accentText。紧凑时只剩图标、居中，名称在悬停与聚焦的提示里。
/// 模板部件：PART_Icon、PART_Indicator、PART_End、PART_Badge、PART_Chevron（NavList.xaml）。
/// </summary>
public abstract class NavRow : ButtonBase
{
    /// <summary>文字前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(NavRow),
        new FrameworkPropertyMetadata(null, (d, _) => ((NavRow)d).Apply(NavList.Owner(d))));

    /// <summary>文字后面的内容（计数、标记）。</summary>
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(object), typeof(NavRow));

    static readonly DependencyPropertyKey IsCurrentKey = DependencyProperty.RegisterReadOnly(nameof(IsCurrent), typeof(bool), typeof(NavRow), new PropertyMetadata(false));

    /// <summary>是不是当前页（分组标题：组里有当前页）。</summary>
    public static readonly DependencyProperty IsCurrentProperty = IsCurrentKey.DependencyProperty;

    static readonly DependencyPropertyKey IsIconOnlyKey = DependencyProperty.RegisterReadOnly(nameof(IsIconOnly), typeof(bool), typeof(NavRow), new PropertyMetadata(false));

    /// <summary>只剩图标（紧凑且有图标）。</summary>
    public static readonly DependencyProperty IsIconOnlyProperty = IsIconOnlyKey.DependencyProperty;

    Badge? badge;
    Indicator? indicator;

    /// <summary>新建：挂上来时让所在的 NavList 重算。</summary>
    protected NavRow() => Loaded += (_, _) => NavList.Owner(this)?.Sync();

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字后面的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>是不是当前页。</summary>
    public bool IsCurrent { get => (bool)GetValue(IsCurrentProperty); protected set => SetValue(IsCurrentKey, value); }

    /// <summary>只剩图标。</summary>
    public bool IsIconOnly => (bool)GetValue(IsIconOnlyProperty);

    /// <summary>计数角标的数目与色调（导航项的 Badge；0 不显示）。</summary>
    private protected virtual (int Count, StatusTone Tone) Count => (0, StatusTone.Danger);

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        badge = GetTemplateChild("PART_Badge") as Badge;
        indicator = GetTemplateChild("PART_Indicator") as Indicator;
        Update();
    }

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        Update();
    }

    /// <summary>跟着 NavList：紧凑时只剩图标。</summary>
    internal virtual void Apply(NavList? list)
    {
        SetValue(IsIconOnlyKey, list?.IsCompact == true && Icon is not null);
        Update();
    }

    /// <summary>计数角标与提示。</summary>
    private protected void Update()
    {
        var (count, tone) = Count;
        if (badge is not null)
        {
            badge.Visibility = !IsIconOnly && count > 0 ? Visibility.Visible : Visibility.Collapsed;
            badge.Content = count > 99 ? "99+" : count.ToString();
            badge.Tone = tone;
        }
        if (indicator is not null)
        {
            // 紧凑：图标右上角一个小圆点（图标太小，叠不下数字），读屏照样念出数目
            indicator.Dot = IsIconOnly && count > 0;
            indicator.Tone = tone;
            indicator.Label = count > 0 ? Provider.GetLang(this).IndicatorCount(count) : null;
        }
        // 只剩图标：名称在提示里，悬停 200 毫秒、聚焦时显示在右边
        if (IsIconOnly)
        {
            ToolTip = Content;
            ToolTipService.SetInitialShowDelay(this, 200);
            Tooltip.SetPlacement(this, FloatingPlacement.Right);
        }
        else ClearValue(ToolTipProperty);
    }
}

/// <summary>
/// 导航项（TS 的 NavItem）：<see cref="Href"/> 等于 NavList 的 CurrentHref 时是当前页（也可以用 <see cref="Active"/> 指定）。
/// 点击先发 Click（标记 Handled 拦下），再交给 NavList 切换。<see cref="Badge"/> 是计数角标：展开时是文字后面的实心徽标，紧凑时是图标上的小圆点。
/// 不可用（IsEnabled=False）时不能点、不能聚焦。读屏里是链接，当前页的项状态是"当前页"。
/// </summary>
public class NavItem : NavRow
{
    /// <summary>地址。</summary>
    public static readonly DependencyProperty HrefProperty = DependencyProperty.Register(nameof(Href), typeof(string), typeof(NavItem),
        new FrameworkPropertyMetadata(null, (d, _) => NavList.Owner(d)?.Sync()));

    /// <summary>是否是当前页；不写时按 NavList 的 CurrentHref 判断。</summary>
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(nameof(Active), typeof(bool?), typeof(NavItem),
        new FrameworkPropertyMetadata(null, (d, _) => NavList.Owner(d)?.Sync()));

    /// <summary>计数角标（待办、未读）；0 不显示。</summary>
    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(nameof(Badge), typeof(int), typeof(NavItem),
        new FrameworkPropertyMetadata(0, (d, _) => ((NavItem)d).Update()));

    /// <summary>角标的色调，默认 danger。</summary>
    public static readonly DependencyProperty BadgeToneProperty = DependencyProperty.Register(nameof(BadgeTone), typeof(StatusTone), typeof(NavItem),
        new FrameworkPropertyMetadata(StatusTone.Danger, (d, _) => ((NavItem)d).Update()));

    static NavItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NavItem), new FrameworkPropertyMetadata(typeof(NavItem)));

    /// <summary>地址。</summary>
    public string? Href { get => (string?)GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>是否是当前页。</summary>
    public bool? Active { get => (bool?)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    /// <summary>计数角标。</summary>
    public int Badge { get => (int)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>角标的色调。</summary>
    public StatusTone BadgeTone { get => (StatusTone)GetValue(BadgeToneProperty); set => SetValue(BadgeToneProperty, value); }

    private protected override (int Count, StatusTone Tone) Count => (Badge, BadgeTone);

    internal override void Apply(NavList? list)
    {
        IsCurrent = Active ?? (Href is not null && Href == list?.CurrentHref);
        base.Apply(list);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下），再交给 NavList。</summary>
    protected override void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        var click = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        NavList.Owner(this)?.Go(this);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavItemPeer(this);

    sealed class NavItemPeer(NavItem owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(NavItem);

        protected override string GetItemStatusCore() => owner.IsCurrent ? "当前页" : base.GetItemStatusCore();
    }
}

/// <summary>
/// 可展开的导航分组（TS 的 NavGroup）：标题行（图标、文字、朝行尾的箭头，展开时朝下）加收起的子列表（左边缩进 space.lg）。
/// 默认收起；组里有当前页时自动展开（离开时不自动收起）。标题的读屏是可展开的按钮。
/// </summary>
[ContentProperty(nameof(Items))]
public class NavGroup : FrameworkElement
{
    /// <summary>分组标题。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(object), typeof(NavGroup),
        new FrameworkPropertyMetadata(null, (d, _) => ((NavGroup)d).OnHeaderChanged()));

    /// <summary>标题前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(NavGroup),
        new FrameworkPropertyMetadata(null, (d, _) => ((NavGroup)d).OnHeaderChanged()));

    /// <summary>是否展开（默认双向绑定）；含当前页时自动展开。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(NavGroup),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NavGroup)d).OnOpenChanged()));

    readonly Flex root = new() { Direction = FlexDirection.Column };
    readonly NavGroupHeader header;
    readonly Collapse collapse = new();
    readonly Flex list = new() { Direction = FlexDirection.Column, Gap = Space.Xs };

    /// <summary>新建：标题行与收起的子列表。</summary>
    public NavGroup()
    {
        header = new NavGroupHeader(this);
        header.SetResourceReference(StyleProperty, NavGroupParts.Trigger);
        collapse.SetResourceReference(StyleProperty, NavGroupParts.Content);
        list.SetResourceReference(StyleProperty, NavGroupParts.List);
        collapse.Content = list;
        root.Children.Add(header);
        root.Children.Add(collapse);
        AddLogicalChild(root);
        AddVisualChild(root);
        Items.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<UIElement>() ?? []) list.Children.Remove(item);
            foreach (var item in e.NewItems?.OfType<UIElement>() ?? []) list.Children.Add(item);
            NavList.Owner(this)?.Sync();
        };
    }

    /// <summary>组里的导航项。</summary>
    public ObservableCollection<UIElement> Items { get; } = [];

    /// <summary>分组标题。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>标题前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标题行（测试用）。</summary>
    internal NavGroupHeader Header => header;

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
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    void OnHeaderChanged()
    {
        header.Content = Label;
        header.Icon = Icon;
    }

    void OnOpenChanged()
    {
        collapse.IsOpen = IsOpen;
        header.Turn(IsOpen);
    }

    /// <summary>组里有当前页：标题行高亮（紧凑时）并展开。</summary>
    internal void Apply(NavList owner)
    {
        var current = Items.OfType<NavItem>().Any(item => item.IsCurrent);
        header.SetCurrent(current && owner.IsCompact);
        if (current) SetCurrentValue(IsOpenProperty, true);
        // 子列表左边缩进 space.lg、上面 space.xs；紧凑时不缩进（TS 的 compactNested）
        var em = TextElement.GetFontSize(this);
        list.Margin = new Thickness(owner.IsCompact ? 0 : (TryFindResource("Rune.Space.Lg") as double? ?? 1) * em, (TryFindResource("Rune.Space.Xs") as double? ?? 0.25) * em, 0, 0);
    }

    /// <summary>展开、收起。</summary>
    internal void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);
}

/// <summary>NavGroup 部件的样式键（NavList.xaml 里是默认外观）。</summary>
public static class NavGroupParts
{
    /// <summary>标题行。</summary>
    public static readonly PartKey Trigger = new(typeof(NavGroup), nameof(Trigger), typeof(NavGroupHeader));

    /// <summary>展开区。</summary>
    public static readonly PartKey Content = new(typeof(NavGroup), nameof(Content), typeof(Collapse));

    /// <summary>里面的列表。</summary>
    public static readonly PartKey List = new(typeof(NavGroup), nameof(List), typeof(Flex));

    /// <summary>标题文字（同导航行的文字）。</summary>
    public static readonly PartKey Label = NavRowParts.Label;

    /// <summary>标题图标（同导航行的图标）。</summary>
    public static readonly PartKey Icon = NavRowParts.Icon;
}

/// <summary>分组的标题行：点了展开、收起；尾部是箭头（展开时转到朝下）。</summary>
public sealed class NavGroupHeader : NavRow
{
    readonly NavGroup owner;
    readonly RotateTransform turn = new();
    Icon? chevron;

    static NavGroupHeader() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NavGroupHeader), new FrameworkPropertyMetadata(typeof(NavGroupHeader)));

    internal NavGroupHeader(NavGroup group) => owner = group;

    internal void SetCurrent(bool value)
    {
        IsCurrent = value;
        Update();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        chevron = GetTemplateChild("PART_Chevron") as Icon;
        if (chevron is not null)
        {
            chevron.RenderTransformOrigin = new Point(0.5, 0.5);
            chevron.RenderTransform = turn;
        }
        turn.Angle = Angle(owner.IsOpen);
        ShowChevron();
    }

    internal override void Apply(NavList? list)
    {
        base.Apply(list);
        ShowChevron();
    }

    /// <summary>箭头：只剩图标时不显示。</summary>
    void ShowChevron()
    {
        if (chevron is not null) chevron.Visibility = IsIconOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>朝行尾的箭头，展开时转 90 度朝下（从右往左排版时反着转）；durations.sm、standard。</summary>
    double Angle(bool open) => !open ? 0 : FlowDirection == FlowDirection.RightToLeft ? -90 : 90;

    internal void Turn(bool open) =>
        turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(Angle(open), Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction,
        });

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsDisabled) return;
        base.OnClick();
        owner.Toggle();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    sealed class HeaderPeer(NavGroupHeader owner) : ButtonBaseAutomationPeer(owner), IExpandCollapseProvider
    {
        protected override string GetClassNameCore() => nameof(NavGroupHeader);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

        public ExpandCollapseState ExpandCollapseState => owner.owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand() => owner.owner.IsOpen = true;

        public void Collapse() => owner.owner.IsOpen = false;
    }
}

/// <summary>
/// 带小标题的一组导航链接（TS 的 NavSection）：一直展开、不能收起。标题行与链接行同一套内边距（图标、文字上下对齐），文字 sm、中等字重、
/// 字距 wide、text 颜色，图标 textMuted；小节之间留 space.lg。标题是这组链接的读屏名称。紧凑时标题只剩图标，上面一条 borderMuted 的细线。
/// 部件样式键：<see cref="NavSectionParts"/>。
/// </summary>
[ContentProperty(nameof(Items))]
public class NavSection : FrameworkElement
{
    /// <summary>小节标题，同时是这组链接的读屏名称。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavSection),
        new FrameworkPropertyMetadata(null, (d, _) => ((NavSection)d).OnLabelChanged()));

    /// <summary>标题前的图标：只是标记、不能点；紧凑时标题只剩它。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(NavSection),
        new FrameworkPropertyMetadata(null, (d, _) => ((NavSection)d).OnIconChanged()));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Border labelRow = new();
    readonly Flex labelContent = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new() { Visibility = Visibility.Collapsed };
    readonly Text text = new();
    readonly Flex list = new() { Direction = FlexDirection.Column, Gap = Space.Xs };

    /// <summary>新建：标题行与链接列表。</summary>
    public NavSection()
    {
        list.SetResourceReference(StyleProperty, NavSectionParts.List);
        labelRow.SetResourceReference(StyleProperty, NavSectionParts.Label);
        icon.SetResourceReference(StyleProperty, NavSectionParts.Icon);
        text.SetResourceReference(StyleProperty, NavSectionParts.Text);
        labelContent.Children.Add(icon);
        labelContent.Children.Add(text);
        labelRow.Child = labelContent;
        root.Children.Add(labelRow);
        root.Children.Add(list);
        AddLogicalChild(root);
        AddVisualChild(root);
        Items.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<UIElement>() ?? []) list.Children.Remove(item);
            foreach (var item in e.NewItems?.OfType<UIElement>() ?? []) list.Children.Add(item);
            NavList.Owner(this)?.Sync();
        };
    }

    /// <summary>小节里的导航项。</summary>
    public ObservableCollection<UIElement> Items { get; } = [];

    /// <summary>小节标题。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>标题前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>链接列表（测试用）。</summary>
    internal Flex List => list;

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
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    void OnLabelChanged()
    {
        text.Text = Label;
        AutomationProperties.SetName(list, Label ?? "");
    }

    void OnIconChanged()
    {
        icon.Data = Icon;
        icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>紧凑时只剩图标、居中，上面一条细线；第一节之外上面留 space.lg。</summary>
    internal void Apply(NavList? owner)
    {
        var iconOnly = owner?.IsCompact == true && Icon is not null;
        text.Visibility = iconOnly ? Visibility.Collapsed : Visibility.Visible;
        labelContent.Justify = iconOnly ? FlexJustify.Center : FlexJustify.Start;
        var em = TextElement.GetFontSize(this);
        // 与链接行同一套内边距（上下 space.xs、左右 space.md）；紧凑时上面多留到 space.sm
        labelRow.Padding = iconOnly ? new Thickness(0, 0.5 * em, 0, 0.25 * em) : new Thickness(0.75 * em, 0.25 * em, 0.75 * em, 0.25 * em);
        labelRow.BorderThickness = iconOnly ? new Thickness(0, 1, 0, 0) : default;
        var first = VisualTreeHelper.GetParent(this) is Panel panel && panel.Children.OfType<UIElement>().FirstOrDefault(child => child.Visibility == Visibility.Visible) == this;
        Margin = new Thickness(0, first ? 0 : (TryFindResource("Rune.Space.Lg") as double? ?? 1) * em, 0, 0);
    }
}

/// <summary>NavSection 部件的样式键（NavList.xaml 里是默认外观）。</summary>
public static class NavSectionParts
{
    /// <summary>标题行。</summary>
    public static readonly PartKey Label = new(typeof(NavSection), nameof(Label), typeof(Border));

    /// <summary>标题的图标。</summary>
    public static readonly PartKey Icon = new(typeof(NavSection), nameof(Icon), typeof(Icon));

    /// <summary>标题文字。</summary>
    public static readonly PartKey Text = new(typeof(NavSection), nameof(Text), typeof(Text));

    /// <summary>链接列表。</summary>
    public static readonly PartKey List = new(typeof(NavSection), nameof(List), typeof(Flex));
}

/// <summary>导航行模板里部件的样式键（NavList.xaml 里是默认外观）。</summary>
public static class NavRowParts
{
    /// <summary>文字前的图标。</summary>
    public static readonly PartKey Icon = new(typeof(NavRow), nameof(Icon), typeof(Icon));

    /// <summary>文字。</summary>
    public static readonly PartKey Label = new(typeof(NavRow), nameof(Label), typeof(ContentPresenter));

    /// <summary>分组标题的箭头（模板里的元素名，没有样式键）。</summary>
    public const string Chevron = "PART_Chevron";
}
