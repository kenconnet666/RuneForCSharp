using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 横向导航（vue-tsx-pro 的 NavBar，顶部菜单）：一行链接，有子项的是下拉菜单；放不下时后面的收进"更多"菜单。后台的顶栏、门户的主导航。
/// <code>
/// &lt;rune:NavBar Items="{Binding Menu}" CurrentHref="{Binding Page}" AutomationProperties.Name="主导航" /&gt;
/// </code>
/// 各项是 <see cref="NavMenuItem"/>（同一份菜单数据可以同时给侧栏的 NavMenu；这里只认一层子项）。侧边的多级导航用 NavList。
/// 当前页同 NavList：Href 等于 <see cref="CurrentHref"/> 的是当前页，链接是 accentText、底边一道 2 像素的强调色线；子项是当前页时它的菜单按钮也高亮。
/// 点了链接或菜单里的子项先发 <see cref="Navigate"/>，没有被处理就调 <see cref="NavigateCommand"/>、把 CurrentHref 改成那一项的地址。
/// 下拉菜单是 <see cref="Menu"/>（↑↓ 移动、Esc 关闭回到按钮）。放不下时量出前几项，其余收进"更多"（有子项的项按分组列出），藏起来的项不占位、读屏与 Tab 跳过。
/// 部件：<see cref="NavBarParts"/>；链接、菜单按钮、"更多"都是 <see cref="NavBarButton"/>。
/// </summary>
public class NavBar : Control
{
    /// <summary>各项。</summary>
    public static readonly StyledProperty<IReadOnlyList<NavMenuItem>> ItemsProperty = AvaloniaProperty.Register<NavBar, IReadOnlyList<NavMenuItem>>(nameof(Items), []);

    /// <summary>当前地址：Href 与它相同的项是当前页。默认双向绑定。</summary>
    public static readonly StyledProperty<string?> CurrentHrefProperty =
        AvaloniaProperty.Register<NavBar, string?>(nameof(CurrentHref), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>点了链接时调用的命令（MVVM）：参数是地址；<see cref="Navigate"/> 标记 Handled 时不调用，命令不能执行时不切换。</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty = AvaloniaProperty.Register<NavBar, ICommand?>(nameof(NavigateCommand));

    /// <summary>点了链接（切换之前）：标记 Handled 时不改 CurrentHref（TS 交给路由的跳转，使用方的 onClick 可以拦下）。</summary>
    public static readonly RoutedEvent<NavigateEventArgs> NavigateEvent = RoutedEvent.Register<NavBar, NavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    readonly Strip strip;
    readonly NavBarButton moreButton = new() { Name = NavBarParts.More, Icon = Lucide.Ellipsis };
    readonly Menu more = new() { Placement = FloatingPlacement.BottomEnd };
    readonly List<(NavMenuItem Item, NavBarButton Button)> buttons = [];

    /// <summary>新建：一行项，最后是"更多"。</summary>
    public NavBar()
    {
        strip = new Strip(this) { Name = NavBarParts.List };
        more.Trigger = moreButton;
        // 菜单内容按打开时收起来的项建（OpenChanged 不报告代码里改的 IsOpen，看属性本身）
        more.PropertyChanged += (_, e) =>
        {
            if (e.Property == Menu.IsOpenProperty && more.IsOpen) FillMore();
        };
        LogicalChildren.Add(strip);
        VisualChildren.Add(strip);
    }

    /// <summary>各项。</summary>
    public IReadOnlyList<NavMenuItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>当前地址。</summary>
    public string? CurrentHref { get => GetValue(CurrentHrefProperty); set => SetValue(CurrentHrefProperty, value); }

    /// <summary>点了链接时调用的命令。</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>点了链接（切换之前）。</summary>
    public event EventHandler<NavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <summary>排得下的项数（测试用）。</summary>
    internal int VisibleCount => strip.Visible;

    /// <summary>"更多"菜单（测试用）。</summary>
    internal Menu More => more;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty) Rebuild();
        else if (change.Property == CurrentHrefProperty) SyncCurrent();
        else if (change.Property == Provider.LangProperty) moreButton.SetValue(AutomationProperties.NameProperty, Provider.GetLang(this).NavBarMore, BindingPriority.Template);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        moreButton.SetValue(AutomationProperties.NameProperty, Provider.GetLang(this).NavBarMore, BindingPriority.Template);
    }

    void Rebuild()
    {
        strip.Children.Clear();
        buttons.Clear();
        foreach (var item in Items)
        {
            var button = new NavBarButton
            {
                Name = item.Children is { Count: > 0 } ? NavBarParts.Trigger : NavBarParts.Link,
                Icon = item.Icon, Badge = item.Badge, IsEnabled = !item.Disabled, Content = item.Label,
            };
            buttons.Add((item, button));
            if (item.Children is { Count: > 0 } children)
            {
                button.ShowChevron = true;
                var menu = new Menu { Name = NavBarParts.Item, Trigger = button };
                menu.Items.AddRange(Links(children));
                strip.Children.Add(menu);
            }
            else
            {
                button.Click += (_, _) => Go(item.Href);
                strip.Children.Add(new Panel { Name = NavBarParts.Item, Children = { button } });
            }
        }
        strip.Children.Add(more);
        SyncCurrent();
    }

    /// <summary>菜单里的链接（下拉菜单、"更多"共用）：当前页的读屏状态是"当前页"，有计数时文字后面跟角标。</summary>
    IEnumerable<Control> Links(IEnumerable<NavMenuItem> items) => items.Select(item =>
    {
        var link = new MenuItem { Icon = item.Icon, IsEnabled = !item.Disabled, Content = item.Badge > 0 ? new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Text { Text = item.Label }, CountBadge(item.Badge) } } : item.Label };
        if (IsCurrent(item)) AutomationProperties.SetItemStatus(link, DesktopMessages.For(Provider.GetLang(this)).CurrentPage);
        link.Click += (_, _) => Go(item.Href);
        return (Control)link;
    });

    /// <summary>计数角标：xs、实底、danger、全圆角，超过 99 写 99+。</summary>
    internal static Badge CountBadge(int count) =>
        new() { Name = NavBarParts.Badge, Size = TextSize.Xs, Variant = StatusVariant.Solid, Tone = StatusTone.Danger, Radius = Radius.Full, Content = count > 99 ? "99+" : count.ToString() };

    /// <summary>"更多"菜单：收起来的各项，有子项的按分组列出。</summary>
    void FillMore()
    {
        more.Items.Clear();
        foreach (var item in Items.Skip(strip.Visible))
        {
            if (item.Children is { Count: > 0 } children)
            {
                var group = new MenuGroup { Label = item.Label };
                group.Items.AddRange(Links(children));
                more.Items.Add(group);
            }
            else more.Items.AddRange(Links([item]));
        }
    }

    bool IsCurrent(NavMenuItem item) => item.Active ?? (item.Href is not null && item.Href == CurrentHref);

    /// <summary>这一项或它的子项是当前页。</summary>
    bool HasCurrent(NavMenuItem item) => IsCurrent(item) || (item.Children ?? []).Any(HasCurrent);

    /// <summary>当前页的高亮：链接、子项是当前页的菜单按钮、收起来的项里有当前页时的"更多"。</summary>
    void SyncCurrent()
    {
        foreach (var (item, button) in buttons) button.IsCurrent = HasCurrent(item);
        moreButton.IsCurrent = Items.Skip(strip.Visible).Any(HasCurrent);
    }

    /// <summary>点了链接：先发 Navigate，没有被处理就切过去。</summary>
    void Go(string? target)
    {
        var args = new NavigateEventArgs(NavigateEvent, target);
        RaiseEvent(args);
        if (args.Handled || target is null) return;
        if (NavigateCommand is { } command)
        {
            if (!command.CanExecute(target)) return;
            command.Execute(target);
        }
        CurrentHref = target;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        strip.Measure(availableSize);
        return strip.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        strip.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    /// 一行项（列间距 xs，高度拉齐）：全部放得下时"更多"藏起来；放不下时从前往后排到留出"更多"的位置，其余藏起来（不占位、读屏与 Tab 跳过）。
    /// 藏起来的项量不出宽度，用它上次显示时的宽度。
    /// </summary>
    sealed class Strip(NavBar owner) : Panel
    {
        readonly Dictionary<Control, double> widths = [];

        public int Visible { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var gap = 0.25 * TextElement.GetFontSize(this);
            var items = Children.Take(Children.Count - 1).ToList();
            var height = 0d;
            foreach (var item in items.Where(item => item.IsVisible))
            {
                item.Measure(Size.Infinity);
                widths[item] = item.DesiredSize.Width;
                height = Math.Max(height, item.DesiredSize.Height);
            }
            var total = items.Sum(item => widths.GetValueOrDefault(item)) + gap * Math.Max(0, items.Count - 1);
            var visible = items.Count;
            if (total > availableSize.Width)
            {
                owner.moreButton.IsVisible = true;
                owner.more.Measure(Size.Infinity);
                var used = owner.more.DesiredSize.Width;
                visible = 0;
                while (visible < items.Count && used + widths.GetValueOrDefault(items[visible]) + gap <= availableSize.Width)
                    used += widths.GetValueOrDefault(items[visible++]) + gap;
            }
            for (var i = 0; i < items.Count; i++) items[i].IsVisible = i < visible;
            owner.more.IsVisible = visible < items.Count;
            if (Visible != visible)
            {
                Visible = visible;
                owner.SyncCurrent();
            }
            var shown = Children.Where(child => child.IsVisible).ToList();
            foreach (var child in shown)
            {
                child.Measure(Size.Infinity);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(shown.Sum(child => child.DesiredSize.Width) + gap * Math.Max(0, shown.Count - 1), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var gap = 0.25 * TextElement.GetFontSize(this);
            var x = 0d;
            foreach (var child in Children.Where(child => child.IsVisible))
            {
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width + gap;
            }
            return finalSize;
        }
    }
}

/// <summary>
/// NavBar 的一个按钮（链接、有子项时的菜单按钮、"更多"）：图标、文字、计数角标、下拉箭头横排隔 xs，控件高度、左右 space.md、md 圆角，中等字重；
/// 平时 textMuted，悬停 text 与 fillHover 底；当前页 accentText（悬停也不变），底边一道 borderWidths.sm 的强调色线、下面两个角不圆；不可用时半透明。
/// </summary>
[PseudoClasses(":current")]
public class NavBarButton : ButtonBase
{
    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NavBarButton, IconData?>(nameof(Icon));

    /// <summary>计数角标，0 不显示。</summary>
    public static readonly StyledProperty<int> BadgeProperty = AvaloniaProperty.Register<NavBarButton, int>(nameof(Badge));

    /// <summary>文字后面的下拉箭头（有子项时）。</summary>
    public static readonly StyledProperty<bool> ShowChevronProperty = AvaloniaProperty.Register<NavBarButton, bool>(nameof(ShowChevron));

    /// <summary>是不是当前页（菜单按钮：子项里有当前页）。</summary>
    public static readonly DirectProperty<NavBarButton, bool> IsCurrentProperty =
        AvaloniaProperty.RegisterDirect<NavBarButton, bool>(nameof(IsCurrent), button => button.IsCurrent);

    Border? root;
    Badge? badge;

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>计数角标。</summary>
    public int Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>下拉箭头。</summary>
    public bool ShowChevron { get => GetValue(ShowChevronProperty); set => SetValue(ShowChevronProperty, value); }

    /// <summary>是不是当前页。</summary>
    public bool IsCurrent
    {
        get;
        internal set
        {
            if (!SetAndRaise(IsCurrentProperty, ref field, value)) return;
            PseudoClasses.Set(":current", value);
            ApplyRadius();
        }
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(NavBarButton);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        root = e.NameScope.Find<Border>(NavBarParts.Root);
        badge = e.NameScope.Find<Badge>(NavBarParts.Badge);
        ApplyBadge();
        ApplyRadius();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty) ApplyRadius();
        else if (change.Property == BadgeProperty) ApplyBadge();
    }

    void ApplyBadge()
    {
        if (badge is null) return;
        badge.IsVisible = Badge > 0;
        badge.Content = Badge > 99 ? "99+" : Badge.ToString();
    }

    /// <summary>当前页时下面两个角不圆（底边的强调色线拉直）。</summary>
    void ApplyRadius()
    {
        if (root is null) return;
        var r = CornerRadius;
        root.CornerRadius = IsCurrent ? new CornerRadius(r.TopLeft, r.TopRight, 0, 0) : r;
    }
}

/// <summary>NavBar 的部件名（TS 的 propsList、propsItem、propsLink、propsTrigger、propsMore）。</summary>
public static class NavBarParts
{
    /// <summary>一行项（TS 的 ul）。</summary>
    public const string List = "PART_List";

    /// <summary>每一项的外层（TS 的 li）：链接外的 Panel，或有子项时的 Menu。</summary>
    public const string Item = "PART_Item";

    /// <summary>链接（NavBarButton）。</summary>
    public const string Link = "PART_Link";

    /// <summary>有子项时的菜单按钮（NavBarButton，带下拉箭头）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>"更多"按钮（NavBarButton，Ellipsis 图标，名称取语言包）。</summary>
    public const string More = "PART_More";

    /// <summary>计数角标（xs、实底、danger、全圆角，超过 99 写 99+）。</summary>
    public const string Badge = "PART_Badge";

    /// <summary>NavBarButton 模板里的外框（底色、圆角）。</summary>
    public const string Root = "PART_Root";

    /// <summary>NavBarButton 模板里当前页底边的强调色线。</summary>
    public const string Underline = "PART_Underline";
}
