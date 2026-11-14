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
/// 面包屑（vue-tsx-pro 的 Breadcrumbs）：当前页在站点层级里的位置。最后一项是当前页（读屏的项状态是"当前页"，不是链接），
/// 其余有地址的是链接（次要色，悬停时下划线），分隔符默认是右向的 chevron，从右到左排版时翻转。
/// <code>
/// &lt;rune:Breadcrumbs Items="{Binding Path}" MaxItems="4" /&gt;
/// </code>
/// 路径太长时用 <see cref="MaxItems"/> 折叠中间部分（保留第一项与最后两项）：折叠处是一个菜单按钮，被折叠的各级在菜单里（同 Radix Themes）。
/// 应用内的层级（文件夹、组织架构的下钻）没有地址：订阅了 <see cref="Navigate"/> 或写了 <see cref="NavigateCommand"/> 时，没有地址的各级是链接样式的按钮，
/// 点了交给它们，当前一级的项状态是"当前位置"。名称默认取语言包（面包屑），写 AutomationProperties.Name 可以改。
/// 部件样式键：<see cref="BreadcrumbsParts"/>。
/// </summary>
public class Breadcrumbs : FrameworkElement
{
    /// <summary>路径，从根到当前页；最后一项是当前页。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<BreadcrumbItem>), typeof(Breadcrumbs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Breadcrumbs)d).Rebuild()));

    /// <summary>分隔符的图标；不写时是右向的 chevron。</summary>
    public static readonly DependencyProperty SeparatorProperty = DependencyProperty.Register(nameof(Separator), typeof(IconData), typeof(Breadcrumbs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Breadcrumbs)d).Rebuild()));

    /// <summary>超过这么多项时折叠中间部分；不写时不折叠。</summary>
    public static readonly DependencyProperty MaxItemsProperty = DependencyProperty.Register(nameof(MaxItems), typeof(int?), typeof(Breadcrumbs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Breadcrumbs)d).Rebuild()));

    /// <summary>点了没有地址的一级时调用的命令（MVVM）：参数是那一级的 <see cref="BreadcrumbItem"/>；<see cref="Navigate"/> 标记 Handled 时不调用。</summary>
    public static readonly DependencyProperty NavigateCommandProperty = DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand), typeof(Breadcrumbs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Breadcrumbs)d).Rebuild()));

    /// <summary>点了没有地址的一级（应用内的层级导航）。</summary>
    public static readonly RoutedEvent NavigateEvent =
        EventManager.RegisterRoutedEvent(nameof(Navigate), RoutingStrategy.Bubble, typeof(EventHandler<BreadcrumbNavigateEventArgs>), typeof(Breadcrumbs));

    readonly Flex list = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Gap = Space.Xs };
    int navigateHandlers;

    /// <summary>新建：横排、放不下时换行。</summary>
    public Breadcrumbs()
    {
        list.SetResourceReference(StyleProperty, BreadcrumbsParts.List);
        AddLogicalChild(list);
        AddVisualChild(list);
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>路径。</summary>
    public IReadOnlyList<BreadcrumbItem>? Items { get => (IReadOnlyList<BreadcrumbItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>分隔符的图标。</summary>
    public IconData? Separator { get => (IconData?)GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    /// <summary>超过这么多项时折叠中间部分。</summary>
    public int? MaxItems { get => (int?)GetValue(MaxItemsProperty); set => SetValue(MaxItemsProperty, value); }

    /// <summary>点了没有地址的一级时调用的命令。</summary>
    public ICommand? NavigateCommand { get => (ICommand?)GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>点了没有地址的一级；订阅了它，没有地址的各级才是按钮。</summary>
    public event EventHandler<BreadcrumbNavigateEventArgs> Navigate
    {
        add
        {
            AddHandler(NavigateEvent, value);
            navigateHandlers++;
            Rebuild();
        }
        remove
        {
            RemoveHandler(NavigateEvent, value);
            navigateHandlers--;
            Rebuild();
        }
    }

    /// <summary>没有地址的各级是按钮（TS 写了 onNavigate）。</summary>
    bool Navigable => navigateHandlers > 0 || NavigateCommand is not null;

    /// <summary>各项（测试用）：链接、按钮、当前页或纯文字，折叠处是菜单。</summary>
    internal Flex List => list;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { list }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => list;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        list.Measure(availableSize);
        return list.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        list.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>按路径重建各项：折叠时留第一项与最后两项，中间换成菜单按钮（maxItems 写得比 3 小也一样，再多出来的才有东西可折）。</summary>
    void Rebuild()
    {
        list.Children.Clear();
        var lang = Provider.GetLang(this);
        var items = Items ?? [];
        var collapse = MaxItems is { } max && items.Count > Math.Max(max, 3);
        IReadOnlyList<int> shown = collapse ? [0, -1, items.Count - 2, items.Count - 1] : [.. Enumerable.Range(0, items.Count)];
        for (var position = 0; position < shown.Count; position++)
        {
            var row = new Flex { Align = FlexAlign.Center, Gap = Space.Xs };
            row.SetResourceReference(StyleProperty, shown[position] < 0 ? BreadcrumbsParts.CollapsedItem : BreadcrumbsParts.Item);
            row.Children.Add(shown[position] < 0 ? Collapsed(items, lang) : Entry(items, shown[position]));
            if (position < shown.Count - 1) row.Children.Add(SeparatorIcon());
            list.Children.Add(row);
        }
    }

    /// <summary>一级：当前页、链接、按钮（应用内导航）或纯文字。</summary>
    UIElement Entry(IReadOnlyList<BreadcrumbItem> items, int index)
    {
        var item = items[index];
        if (index == items.Count - 1)
        {
            var current = new Border { Child = Label(item) };
            current.SetResourceReference(StyleProperty, BreadcrumbsParts.Current);
            AutomationProperties.SetItemStatus(current, Navigable ? "当前位置" : "当前页");
            return current;
        }
        if (item.Href is { } href) return LinkTo(item, href);
        if (!Navigable) return Label(item);
        var button = new Button { Variant = ButtonVariant.Link, Content = Label(item) };
        button.SetResourceReference(StyleProperty, BreadcrumbsParts.Button);
        button.Click += (_, _) => RaiseNavigate(item, index);
        return button;
    }

    /// <summary>折叠处：菜单按钮，被折叠的各级在菜单里（有地址的是链接项，没有的交给 Navigate）。</summary>
    UIElement Collapsed(IReadOnlyList<BreadcrumbItem> items, Lang lang)
    {
        var expand = new IconButton { Icon = Lucide.Ellipsis, Size = TextSize.Sm };
        expand.SetResourceReference(StyleProperty, BreadcrumbsParts.Expand);
        AutomationProperties.SetName(expand, lang.BreadcrumbsExpand);
        var menu = new Menu { Trigger = expand };
        menu.SetResourceReference(StyleProperty, BreadcrumbsParts.Menu);
        for (var index = 1; index < items.Count - 2; index++)
        {
            var hidden = items[index];
            if (hidden.Href is { } href)
            {
                menu.Items.Add(new MenuLinkItem { Href = new Uri(href, UriKind.RelativeOrAbsolute), Icon = hidden.Icon, Content = hidden.Label });
                continue;
            }
            var entry = new MenuItem { Icon = hidden.Icon, Content = hidden.Label, IsEnabled = Navigable };
            var at = index;
            entry.Click += (_, _) => RaiseNavigate(hidden, at);
            menu.Items.Add(entry);
        }
        return menu;
    }

    /// <summary>
    /// 链接（次要色，悬停时下划线）：WPF 的 Link 是行内元素，放在 Text 里；图标是行内的控件，与文字之间一个空格（约 0.25em，上游的 gap xs），
    /// 下划线只画在文字上。
    /// </summary>
    static Text LinkTo(BreadcrumbItem item, string href)
    {
        var link = new Link { Href = new Uri(href, UriKind.RelativeOrAbsolute), Tone = TextTone.Muted, Underline = LinkUnderline.Hover };
        link.SetResourceReference(FrameworkContentElement.StyleProperty, BreadcrumbsParts.Link);
        if (item.Icon is { } icon) link.Inlines.Add(new InlineUIContainer(new Icon { Data = icon }) { BaselineAlignment = BaselineAlignment.Center });
        link.Inlines.Add(new Run(item.Icon is null ? item.Label : " " + item.Label));
        return new Text { Inlines = { link } };
    }

    /// <summary>图标加文字。</summary>
    static Flex Label(BreadcrumbItem item)
    {
        var label = new Flex { Align = FlexAlign.Center, Gap = Space.Xs };
        label.SetResourceReference(StyleProperty, BreadcrumbsParts.Label);
        if (item.Icon is { } icon) label.Children.Add(new Icon { Data = icon });
        label.Children.Add(new TextBlock { Text = item.Label });
        return label;
    }

    /// <summary>分隔符（没有名称的图标，读屏不念）。</summary>
    Icon SeparatorIcon()
    {
        var icon = new Icon { Data = Separator ?? Lucide.ChevronRight, FlipInRtl = Separator is null };
        icon.SetResourceReference(StyleProperty, BreadcrumbsParts.Separator);
        return icon;
    }

    /// <summary>先发 Navigate，没有被处理就执行命令。</summary>
    void RaiseNavigate(BreadcrumbItem item, int index)
    {
        var args = new BreadcrumbNavigateEventArgs(NavigateEvent, this, item, index);
        RaiseEvent(args);
        if (!args.Handled && NavigateCommand is { } command && command.CanExecute(item)) command.Execute(item);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbsPeer(this);

    sealed class BreadcrumbsPeer(Breadcrumbs owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Breadcrumbs);

        // WPF 的读屏节点没有地标类型：本地化的控件类型写"导航"
        protected override string GetLocalizedControlTypeCore() => "导航";

        /// <summary>名称没写时取语言包（面包屑），换语言后跟着换。</summary>
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Breadcrumbs;
    }
}

/// <summary>面包屑的一级。</summary>
/// <param name="Label">显示的文字。</param>
/// <param name="Href">地址；最后一项（当前页）、应用内的层级不需要。</param>
/// <param name="Icon">文字前的图标。</param>
public sealed record BreadcrumbItem(string Label, string? Href = null, IconData? Icon = null);

/// <summary>点了面包屑里没有地址的一级。</summary>
public class BreadcrumbNavigateEventArgs(RoutedEvent routedEvent, object source, BreadcrumbItem item, int index) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>点的那一级。</summary>
    public BreadcrumbItem Item { get; } = item;

    /// <summary>那一级在 Items 里的位置。</summary>
    public int Index { get; } = index;
}

/// <summary>Breadcrumbs 的部件样式键。</summary>
public static class BreadcrumbsParts
{
    /// <summary>列表（横排、换行）。</summary>
    public static readonly PartKey List = new(typeof(Breadcrumbs), nameof(List), typeof(Flex));

    /// <summary>每一项（链接或文字，后面是分隔符）。</summary>
    public static readonly PartKey Item = new(typeof(Breadcrumbs), nameof(Item), typeof(Flex));

    /// <summary>折叠处那一项（折叠菜单，后面是分隔符）。</summary>
    public static readonly PartKey CollapsedItem = new(typeof(Breadcrumbs), nameof(CollapsedItem), typeof(Flex));

    /// <summary>链接（有地址的各级）。</summary>
    public static readonly PartKey Link = new(typeof(Breadcrumbs), nameof(Link), typeof(Link));

    /// <summary>按钮（link 样式：应用内导航时没有地址的各级）。</summary>
    public static readonly PartKey Button = new(typeof(Breadcrumbs), nameof(Button), typeof(Button));

    /// <summary>被折叠各级的菜单。</summary>
    public static readonly PartKey Menu = new(typeof(Breadcrumbs), nameof(Menu), typeof(Menu));

    /// <summary>当前页（最后一项）。</summary>
    public static readonly PartKey Current = new(typeof(Breadcrumbs), nameof(Current), typeof(Border));

    /// <summary>图标加文字。</summary>
    public static readonly PartKey Label = new(typeof(Breadcrumbs), nameof(Label), typeof(Flex));

    /// <summary>分隔符。</summary>
    public static readonly PartKey Separator = new(typeof(Breadcrumbs), nameof(Separator), typeof(Icon));

    /// <summary>折叠按钮（菜单的触发器）。</summary>
    public static readonly PartKey Expand = new(typeof(Breadcrumbs), nameof(Expand), typeof(IconButton));
}
