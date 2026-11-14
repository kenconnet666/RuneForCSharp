using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Metadata;
using System.Windows.Input;

namespace Rune.Avalonia;

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
    /// <summary>点了导航项时调用的命令（MVVM）：参数是导航项的 Href；<see cref="Navigate"/> 标记 Handled 时不调用，命令不能执行时不切换。</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty = AvaloniaProperty.Register<NavList, ICommand?>(nameof(NavigateCommand));

    /// <summary>当前地址：Href 与它相同的导航项是当前页。默认双向绑定。</summary>
    public static readonly StyledProperty<string?> CurrentHrefProperty =
        AvaloniaProperty.Register<NavList, string?>(nameof(CurrentHref), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>紧凑：只显示图标。</summary>
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<NavList, bool>(nameof(Compact));

    /// <summary>区域里的导航都紧凑（TS 的 NavCompactContext）：沿逻辑树继承，AppShell 把侧栏收成图标栏时设在侧栏上。</summary>
    public static readonly AttachedProperty<bool> CompactScopeProperty = AvaloniaProperty.RegisterAttached<NavList, Control, bool>("CompactScope", inherits: true);

    /// <summary>点了导航项（切换之前）：标记 Handled 时不改 CurrentHref（TS 的路由接管跳转）。</summary>
    public static readonly RoutedEvent<NavigateEventArgs> NavigateEvent = RoutedEvent.Register<NavList, NavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>新建：竖排、间距 space.xs，读屏里是导航地标。</summary>
    public NavList()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
    }

    /// <summary>当前地址。</summary>
    public string? CurrentHref { get => GetValue(CurrentHrefProperty); set => SetValue(CurrentHrefProperty, value); }

    /// <summary>点了导航项时调用的命令。</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>紧凑。</summary>
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>实际是否紧凑：自己写了 <see cref="Compact"/>，或在紧凑的区域里。</summary>
    internal bool IsCompact => Compact || GetValue(CompactScopeProperty);

    /// <summary>区域里的导航是否都紧凑。</summary>
    public static bool GetCompactScope(Control control) => control.GetValue(CompactScopeProperty);

    /// <summary>让区域里的导航都紧凑。</summary>
    public static void SetCompactScope(Control control, bool value) => control.SetValue(CompactScopeProperty, value);

    /// <summary>点了导航项。</summary>
    public event EventHandler<NavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <summary>属于这个导航的成员（不进到里面另一个 NavList）。</summary>
    IEnumerable<T> Members<T>() where T : class => this.GetLogicalDescendants().OfType<T>().Where(member => Owner((ILogical)member) == this);

    /// <summary>元素属于哪个 NavList（逻辑树上最近的一个）。</summary>
    internal static NavList? Owner(ILogical element) => element.FindLogicalAncestorOfType<NavList>();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentHrefProperty || change.Property == CompactProperty || change.Property == CompactScopeProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
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
        var args = new NavigateEventArgs(NavigateEvent, item.Href);
        RaiseEvent(args);
        if (args.Handled || item.Href is not { } href) return;
        if (NavigateCommand is { } command)
        {
            if (!command.CanExecute(href)) return;
            command.Execute(href);
        }
        CurrentHref = href;
    }
}

/// <summary>点了导航项的参数：要去的地址；标记 Handled 时 NavList 不改当前地址。</summary>
public class NavigateEventArgs(RoutedEvent routedEvent, string? href) : RoutedEventArgs(routedEvent)
{
    /// <summary>要去的地址。</summary>
    public string? Href { get; } = href;
}

/// <summary>
/// 导航的一行（导航项与分组标题共用，TS 的 itemStyles.row 加 navListStyles.row）：图标、文字、尾部内容横排，控件高度、上下 space.xs、左右 space.md、md 圆角，
/// 中等字重，平时 textMuted，悬停 text 与 fillHover 底；当前页 accentSoft 底、accentText。紧凑时只剩图标、居中，名称在悬停与聚焦的提示里。
/// 部件：<see cref="NavRowParts"/>。
/// </summary>
public abstract class NavRow : ButtonBase
{
    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NavRow, IconData?>(nameof(Icon));

    /// <summary>文字后面的内容（计数、标记）。</summary>
    public static readonly StyledProperty<object?> EndProperty = AvaloniaProperty.Register<NavRow, object?>(nameof(End));

    /// <summary>是不是当前页（分组标题：组里有当前页）。</summary>
    public static readonly DirectProperty<NavRow, bool> IsCurrentProperty = AvaloniaProperty.RegisterDirect<NavRow, bool>(nameof(IsCurrent), row => row.IsCurrent);

    /// <summary>只剩图标（紧凑且有图标）。</summary>
    public static readonly DirectProperty<NavRow, bool> IsIconOnlyProperty = AvaloniaProperty.RegisterDirect<NavRow, bool>(nameof(IsIconOnly), row => row.IsIconOnly);

    Badge? badge;
    Indicator? indicator;

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>文字后面的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>是不是当前页。</summary>
    public bool IsCurrent { get; protected set => SetAndRaise(IsCurrentProperty, ref field, value); }

    /// <summary>只剩图标。</summary>
    public bool IsIconOnly { get; private set => SetAndRaise(IsIconOnlyProperty, ref field, value); }

    /// <summary>计数角标的数目与色调（导航项的 Badge；0 不显示）。</summary>
    private protected virtual (int Count, StatusTone Tone) Count => (0, StatusTone.Danger);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        badge = e.NameScope.Find<Badge>(NavRowParts.Badge);
        indicator = e.NameScope.Find<Indicator>(NavRowParts.Indicator);
        Update();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) Apply(NavList.Owner(this));
        else if (change.Property == ContentProperty) Update();
    }

    /// <summary>跟着 NavList：紧凑时只剩图标（分组弹出的内容里不紧凑）。</summary>
    internal virtual void Apply(NavList? list)
    {
        IsIconOnly = list?.IsCompact == true && Icon is not null;
        Update();
    }

    /// <summary>伪类、计数角标、提示。</summary>
    private protected void Update()
    {
        PseudoClasses.Set(":current", IsCurrent);
        PseudoClasses.Set(":icon-only", IsIconOnly);
        PseudoClasses.Set(":has-icon", Icon is not null);
        var (count, tone) = Count;
        if (badge is not null)
        {
            badge.IsVisible = !IsIconOnly && count > 0;
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
            ToolTip.SetTip(this, Content);
            ToolTip.SetShowDelay(this, 200);
            Tooltip.SetPlacement(this, FloatingPlacement.Right);
        }
        else ClearValue(ToolTip.TipProperty);
    }
}

/// <summary>NavRow 模板里的部件名。</summary>
public static class NavRowParts
{
    /// <summary>图标（rune:Icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>紧凑时图标上的计数圆点（rune:Indicator）。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>文字（ContentPresenter）。</summary>
    public const string Label = "PART_ContentPresenter";

    /// <summary>尾部内容（ContentPresenter）。</summary>
    public const string End = "PART_End";

    /// <summary>计数角标（rune:Badge）。</summary>
    public const string Badge = "PART_Badge";

    /// <summary>分组标题的箭头（rune:Icon）。</summary>
    public const string Chevron = "PART_Chevron";
}

/// <summary>
/// 导航项（TS 的 NavItem）：<see cref="Href"/> 等于 NavList 的 CurrentHref 时是当前页（也可以用 <see cref="Active"/> 指定）。
/// 点击先发 Click（标记 Handled 拦下），再交给 NavList 切换。<see cref="Badge"/> 是计数角标：展开时是文字后面的实心徽标，紧凑时是图标上的小圆点。
/// 不可用（IsEnabled=False）时不能点、不能聚焦。读屏里是链接，当前页的项状态是"当前页"。
/// </summary>
public class NavItem : NavRow
{
    /// <summary>地址。</summary>
    public static readonly StyledProperty<string?> HrefProperty = AvaloniaProperty.Register<NavItem, string?>(nameof(Href));

    /// <summary>是否是当前页；不写时按 NavList 的 CurrentHref 判断。</summary>
    public static readonly StyledProperty<bool?> ActiveProperty = AvaloniaProperty.Register<NavItem, bool?>(nameof(Active));

    /// <summary>计数角标（待办、未读）；0 不显示。</summary>
    public static readonly StyledProperty<int> BadgeProperty = AvaloniaProperty.Register<NavItem, int>(nameof(Badge));

    /// <summary>角标的色调，默认 danger。</summary>
    public static readonly StyledProperty<StatusTone> BadgeToneProperty = AvaloniaProperty.Register<NavItem, StatusTone>(nameof(BadgeTone), StatusTone.Danger);

    /// <summary>地址。</summary>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>是否是当前页。</summary>
    public bool? Active { get => GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    /// <summary>计数角标。</summary>
    public int Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>角标的色调。</summary>
    public StatusTone BadgeTone { get => GetValue(BadgeToneProperty); set => SetValue(BadgeToneProperty, value); }

    private protected override (int Count, StatusTone Tone) Count => (Badge, BadgeTone);

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(NavItem);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        NavList.Owner(this)?.Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HrefProperty || change.Property == ActiveProperty) NavList.Owner(this)?.Sync();
        else if (change.Property == BadgeProperty || change.Property == BadgeToneProperty) Update();
    }

    internal override void Apply(NavList? list)
    {
        IsCurrent = Active ?? (Href is not null && Href == list?.CurrentHref);
        base.Apply(list);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下），再交给 NavList。</summary>
    protected override void OnClick()
    {
        if (IsDisabled || IsLoading) return;
        var click = new RoutedEventArgs(ClickEvent);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        NavList.Owner(this)?.Go(this);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavItemPeer(this);

    sealed class NavItemPeer(NavItem owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(NavItem);

        protected override string? GetItemStatusCore() => owner.IsCurrent ? DesktopMessages.For(Provider.GetLang(owner)).CurrentPage : base.GetItemStatusCore();
    }
}

/// <summary>
/// 可展开的导航分组（TS 的 NavGroup）：标题行（图标、文字、朝行尾的箭头，展开时朝下）加收起的子列表（左边缩进 space.lg）。
/// 默认收起；组里有当前页时自动展开（离开时不自动收起）。标题的读屏是可展开的按钮。
/// </summary>
public class NavGroup : Control
{
    /// <summary>分组标题。</summary>
    public static readonly StyledProperty<object?> LabelProperty = AvaloniaProperty.Register<NavGroup, object?>(nameof(Label));

    /// <summary>标题前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NavGroup, IconData?>(nameof(Icon));

    /// <summary>是否展开（默认双向绑定）；含当前页时自动展开。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<NavGroup, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    readonly Flex root = new() { Direction = FlexDirection.Column };
    readonly NavGroupHeader header;
    readonly Collapse collapse = new() { Name = NavGroupParts.Content };
    readonly Flex list = new() { Name = NavGroupParts.List, Direction = FlexDirection.Column, Gap = Space.Xs };

    /// <summary>新建：标题行与收起的子列表。</summary>
    public NavGroup()
    {
        header = new NavGroupHeader(this) { Name = NavGroupParts.Trigger };
        collapse.Content = list;
        root.Children.AddRange([header, collapse]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Items.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<Control>() ?? []) list.Children.Remove(item);
            foreach (var item in e.NewItems?.OfType<Control>() ?? []) list.Children.Add(item);
            NavList.Owner(this)?.Sync();
        };
        collapse.Bind(Collapse.IsOpenProperty, this.GetObservable(IsOpenProperty));
    }

    /// <summary>组里的导航项。</summary>
    [Content]
    public AvaloniaList<Control> Items { get; } = [];

    /// <summary>分组标题。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>标题前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标题行（测试用）。</summary>
    internal NavGroupHeader Header => header;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty) header.Content = Label;
        else if (change.Property == IconProperty) header.Icon = Icon;
        else if (change.Property == IsOpenProperty) header.Turn(IsOpen);
    }

    /// <summary>组里有当前页：标题行高亮（紧凑时）并展开。</summary>
    internal void Apply(NavList owner)
    {
        var current = Items.OfType<NavItem>().Any(item => item.IsCurrent);
        header.SetCurrent(current && owner.IsCompact);
        if (current) SetCurrentValue(IsOpenProperty, true);
        // 子列表左边缩进 space.lg、上面 space.xs；紧凑时不缩进（TS 的 compactNested）
        var em = TextElement.GetFontSize(this);
        list.Margin = new Thickness(owner.IsCompact ? 0 : this.Resource<double>(ThemeKeys.Space.Lg) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em, 0, 0);
    }

    /// <summary>展开、收起。</summary>
    internal void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);
}

/// <summary>NavGroup 里的部件名（样式里用 <c>rune|NavGroup rune|NavGroupHeader#PART_Trigger</c> 这样选中）。</summary>
public static class NavGroupParts
{
    /// <summary>标题行（rune:NavGroupHeader）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>展开区（rune:Collapse）。</summary>
    public const string Content = "PART_Content";

    /// <summary>里面的列表（rune:Flex）。</summary>
    public const string List = "PART_List";

    /// <summary>标题文字（标题行模板里的 ContentPresenter）。</summary>
    public const string Label = NavRowParts.Label;

    /// <summary>标题图标（标题行模板里的 rune:Icon）。</summary>
    public const string Icon = NavRowParts.Icon;
}

/// <summary>分组的标题行：点了展开、收起；尾部是箭头（展开时转到朝下）。</summary>
public sealed class NavGroupHeader : NavRow
{
    readonly NavGroup owner;
    Icon? chevron;

    internal NavGroupHeader(NavGroup group) => owner = group;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(NavGroupHeader);

    internal void SetCurrent(bool value)
    {
        IsCurrent = value;
        Update();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        chevron = e.NameScope.Find<Icon>(NavRowParts.Chevron);
        Turn(owner.IsOpen);
    }

    /// <summary>箭头：朝行尾，展开时转 90 度朝下（从右往左排版时反着转）。</summary>
    internal void Turn(bool open)
    {
        PseudoClasses.Set(":open", open);
        if (chevron is not null)
            chevron.RenderTransform = TransformOperations.Parse(open ? FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft ? "rotate(-90deg)" : "rotate(90deg)" : "rotate(0deg)");
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsDisabled) return;
        base.OnClick();
        owner.Toggle();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    sealed class HeaderPeer(NavGroupHeader owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        protected override string GetClassNameCore() => nameof(NavGroupHeader);

        public ExpandCollapseState ExpandCollapseState => owner.owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => owner.owner.IsOpen = true;

        public void Collapse() => owner.owner.IsOpen = false;
    }
}

/// <summary>
/// 带小标题的一组导航链接（TS 的 NavSection）：一直展开、不能收起。标题行与链接行同一套内边距（图标、文字上下对齐），文字 sm、中等字重、
/// 字距 wide、text 颜色，图标 textMuted；小节之间留 space.lg。标题是这组链接的读屏名称。紧凑时标题只剩图标，上面一条 borderMuted 的细线。
/// </summary>
public class NavSection : Control
{
    /// <summary>小节标题，同时是这组链接的读屏名称。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<NavSection, string?>(nameof(Label));

    /// <summary>标题前的图标：只是标记、不能点；紧凑时标题只剩它。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NavSection, IconData?>(nameof(Icon));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Border labelRow = new() { Name = NavSectionParts.Label };
    readonly Flex labelContent = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new() { Name = NavSectionParts.Icon, IsVisible = false };
    readonly TextBlock text = new() { Name = NavSectionParts.Text };
    readonly Flex list = new() { Name = NavSectionParts.List, Direction = FlexDirection.Column, Gap = Space.Xs };

    /// <summary>新建：标题行与链接列表。</summary>
    public NavSection()
    {
        labelContent.Children.AddRange([icon, text]);
        labelRow.Child = labelContent;
        root.Children.AddRange([labelRow, list]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Items.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<Control>() ?? []) list.Children.Remove(item);
            foreach (var item in e.NewItems?.OfType<Control>() ?? []) list.Children.Add(item);
            NavList.Owner(this)?.Sync();
        };
    }

    /// <summary>小节里的导航项。</summary>
    [Content]
    public AvaloniaList<Control> Items { get; } = [];

    /// <summary>小节标题。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>标题前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>链接列表（测试用）。</summary>
    internal Flex List => list;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            text.Text = Label;
            AutomationProperties.SetName(list, Label);
        }
        else if (change.Property == IconProperty)
        {
            icon.Data = Icon;
            icon.IsVisible = Icon is not null;
        }
        else if (change.Property == TextElement.FontSizeProperty) Apply(NavList.Owner(this));
    }

    /// <summary>紧凑时只剩图标、居中，上面一条细线；第一节之外上面留 space.lg。</summary>
    internal void Apply(NavList? owner)
    {
        var iconOnly = owner?.IsCompact == true && Icon is not null;
        PseudoClasses.Set(":icon-only", iconOnly);
        text.IsVisible = !iconOnly;
        labelContent.Justify = iconOnly ? FlexJustify.Center : FlexJustify.Start;
        var em = TextElement.GetFontSize(this);
        // 与链接行同一套内边距（上下 space.xs、左右 space.md）；紧凑时上面多留到 space.sm
        labelRow.Padding = iconOnly ? new Thickness(0, 0.5 * em, 0, 0.25 * em) : new Thickness(0.75 * em, 0.25 * em);
        labelRow.BorderThickness = iconOnly ? new Thickness(0, 1, 0, 0) : default;
        var first = Parent is Panel panel && panel.Children.OfType<Control>().FirstOrDefault(child => child.IsVisible) == this;
        Margin = new Thickness(0, first ? 0 : this.Resource<double>(ThemeKeys.Space.Lg) * em, 0, 0);
    }
}

/// <summary>NavSection 里的部件名。</summary>
public static class NavSectionParts
{
    /// <summary>标题行（Border）。</summary>
    public const string Label = "PART_Label";

    /// <summary>标题的图标（rune:Icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>标题文字（TextBlock）。</summary>
    public const string Text = "PART_Text";

    /// <summary>链接列表（rune:Flex）。</summary>
    public const string List = "PART_List";
}
