using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 面包屑（vue-tsx-pro 的 Breadcrumbs）：当前页在站点层级里的位置。最后一项是当前页（读屏的项状态是"当前页"，不是链接），
/// 其余有地址的是链接（次要色，悬停时下划线），分隔符默认是右向的 chevron，从右到左排版时翻转。
/// <code>
/// &lt;rune:Breadcrumbs Items="{Binding Path}" MaxItems="4" /&gt;
/// </code>
/// 路径太长时用 <see cref="MaxItems"/> 折叠中间部分（保留第一项与最后两项）：折叠处是一个菜单按钮，被折叠的各级在菜单里（同 Radix Themes）。
/// 应用内的层级（文件夹、组织架构的下钻）没有地址：订阅了 <see cref="Navigate"/> 或写了 <see cref="NavigateCommand"/> 时，没有地址的各级是链接样式的按钮，
/// 点了交给它们，当前一级的项状态是"当前位置"。名称默认取语言包（面包屑），写 AutomationProperties.Name 可以改。
/// </summary>
public class Breadcrumbs : Control
{
    /// <summary>路径，从根到当前页；最后一项是当前页。</summary>
    public static readonly StyledProperty<IReadOnlyList<BreadcrumbItem>?> ItemsProperty =
        AvaloniaProperty.Register<Breadcrumbs, IReadOnlyList<BreadcrumbItem>?>(nameof(Items));

    /// <summary>分隔符的图标；不写时是右向的 chevron。</summary>
    public static readonly StyledProperty<IconData?> SeparatorProperty = AvaloniaProperty.Register<Breadcrumbs, IconData?>(nameof(Separator));

    /// <summary>超过这么多项时折叠中间部分；不写时不折叠。</summary>
    public static readonly StyledProperty<int?> MaxItemsProperty = AvaloniaProperty.Register<Breadcrumbs, int?>(nameof(MaxItems));

    /// <summary>点了没有地址的一级时调用的命令（MVVM）：参数是那一级的 <see cref="BreadcrumbItem"/>；<see cref="Navigate"/> 标记 Handled 时不调用。</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty = AvaloniaProperty.Register<Breadcrumbs, ICommand?>(nameof(NavigateCommand));

    /// <summary>点了没有地址的一级（应用内的层级导航）。</summary>
    public static readonly RoutedEvent<BreadcrumbNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<Breadcrumbs, BreadcrumbNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    readonly Flex list = new() { Name = BreadcrumbsParts.List, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Gap = Space.Xs };
    int navigateHandlers;

    /// <summary>新建：横排、放不下时换行。读屏里是导航地标。</summary>
    public Breadcrumbs()
    {
        LogicalChildren.Add(list);
        VisualChildren.Add(list);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
    }

    /// <summary>路径。</summary>
    public IReadOnlyList<BreadcrumbItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>分隔符的图标。</summary>
    public IconData? Separator { get => GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    /// <summary>超过这么多项时折叠中间部分。</summary>
    public int? MaxItems { get => GetValue(MaxItemsProperty); set => SetValue(MaxItemsProperty, value); }

    /// <summary>点了没有地址的一级时调用的命令。</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>点了没有地址的一级；订阅了它，没有地址的各级才是按钮。</summary>
    public event EventHandler<BreadcrumbNavigateEventArgs>? Navigate
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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == SeparatorProperty || change.Property == MaxItemsProperty || change.Property == NavigateCommandProperty)
            Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
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
            var row = new Flex { Name = shown[position] < 0 ? BreadcrumbsParts.CollapsedItem : BreadcrumbsParts.Item, Align = FlexAlign.Center, Gap = Space.Xs };
            row.Children.Add(shown[position] < 0 ? Collapsed(items, lang) : Entry(items, shown[position]));
            if (position < shown.Count - 1) row.Children.Add(SeparatorIcon());
            list.Children.Add(row);
        }
    }

    /// <summary>一级：当前页、链接、按钮（应用内导航）或纯文字。</summary>
    Control Entry(IReadOnlyList<BreadcrumbItem> items, int index)
    {
        var item = items[index];
        if (index == items.Count - 1)
        {
            var current = new Border { Name = BreadcrumbsParts.Current, Child = Label(item) };
            AutomationProperties.SetItemStatus(current, Navigable ? "当前位置" : "当前页");
            return current;
        }
        if (item.Href is { } href)
            return new Link { Name = BreadcrumbsParts.Link, Href = new Uri(href, UriKind.RelativeOrAbsolute), Tone = TextTone.Muted, Underline = LinkUnderline.Hover, Content = Label(item) };
        if (!Navigable) return Label(item);
        var button = new Button { Name = BreadcrumbsParts.Button, Variant = ButtonVariant.Link, Content = Label(item) };
        button.Click += (_, _) => RaiseNavigate(item, index);
        return button;
    }

    /// <summary>折叠处：菜单按钮，被折叠的各级在菜单里（有地址的是链接项，没有的交给 Navigate）。</summary>
    Control Collapsed(IReadOnlyList<BreadcrumbItem> items, Lang lang)
    {
        var expand = new IconButton { Name = BreadcrumbsParts.Expand, Icon = Lucide.Ellipsis, Size = TextSize.Sm };
        AutomationProperties.SetName(expand, lang.BreadcrumbsExpand);
        var menu = new Menu { Name = BreadcrumbsParts.Menu, Trigger = expand };
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

    /// <summary>图标加文字。</summary>
    static Flex Label(BreadcrumbItem item)
    {
        var label = new Flex { Name = BreadcrumbsParts.Label, Align = FlexAlign.Center, Gap = Space.Xs };
        if (item.Icon is { } icon) label.Children.Add(new Icon { Data = icon });
        label.Children.Add(new TextBlock { Text = item.Label });
        return label;
    }

    /// <summary>分隔符：读屏不念（TS 的 aria-hidden）。</summary>
    Icon SeparatorIcon()
    {
        var icon = new Icon { Name = BreadcrumbsParts.Separator, Data = Separator ?? Lucide.ChevronRight, FlipInRtl = Separator is null };
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        return icon;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbsPeer(this);

    /// <summary>名称没写时取语言包（面包屑），换语言后跟着换。</summary>
    sealed class BreadcrumbsPeer(Breadcrumbs owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Breadcrumbs;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    /// <summary>先发 Navigate，没有被处理就执行命令。</summary>
    void RaiseNavigate(BreadcrumbItem item, int index)
    {
        var args = new BreadcrumbNavigateEventArgs(NavigateEvent, item, index);
        RaiseEvent(args);
        if (!args.Handled && NavigateCommand is { } command && command.CanExecute(item)) command.Execute(item);
    }
}

/// <summary>面包屑的一级。</summary>
/// <param name="Label">显示的文字。</param>
/// <param name="Href">地址；最后一项（当前页）、应用内的层级不需要。</param>
/// <param name="Icon">文字前的图标。</param>
public sealed record BreadcrumbItem(string Label, string? Href = null, IconData? Icon = null);

/// <summary>点了面包屑里没有地址的一级。</summary>
public class BreadcrumbNavigateEventArgs(RoutedEvent routedEvent, BreadcrumbItem item, int index) : RoutedEventArgs(routedEvent)
{
    /// <summary>点的那一级。</summary>
    public BreadcrumbItem Item { get; } = item;

    /// <summary>那一级在 Items 里的位置。</summary>
    public int Index { get; } = index;
}

/// <summary>Breadcrumbs 的部件名（样式选择器用）。</summary>
public static class BreadcrumbsParts
{
    /// <summary>列表（rune:Flex，横排、换行）。</summary>
    public const string List = "PART_List";

    /// <summary>每一项（rune:Flex：链接或文字，后面是分隔符）。</summary>
    public const string Item = "PART_Item";

    /// <summary>折叠处那一项（rune:Flex：折叠菜单，后面是分隔符）。</summary>
    public const string CollapsedItem = "PART_CollapsedItem";

    /// <summary>链接（rune:Link：有地址的各级）。</summary>
    public const string Link = "PART_Link";

    /// <summary>按钮（rune:Button，link 样式：应用内导航时没有地址的各级）。</summary>
    public const string Button = "PART_Button";

    /// <summary>被折叠各级的菜单（rune:Menu）。</summary>
    public const string Menu = "PART_Menu";

    /// <summary>当前页（Border，最后一项）。</summary>
    public const string Current = "PART_Current";

    /// <summary>图标加文字（rune:Flex）。</summary>
    public const string Label = "PART_Label";

    /// <summary>分隔符（rune:Icon）。</summary>
    public const string Separator = "PART_Separator";

    /// <summary>折叠按钮（rune:IconButton，菜单的触发器）。</summary>
    public const string Expand = "PART_Expand";
}
