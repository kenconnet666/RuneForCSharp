using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>一条通知（vue-tsx-pro 的 NotificationItem）。</summary>
/// <param name="Id">标识。</param>
/// <param name="Title">标题：一句话说清发生了什么。</param>
/// <param name="Time">发生的时间。</param>
public sealed record NotificationItem(string Id, string Title, DateTimeOffset Time)
{
    /// <summary>补充说明（谁、哪张单）。</summary>
    public string? Description { get; init; }

    /// <summary>类型：按 Types 筛选。</summary>
    public string? Type { get; init; }

    /// <summary>读过了。</summary>
    public bool IsRead { get; init; }

    /// <summary>点了去哪儿（绝对地址用系统打开；应用内的跳转在 ItemClicked 里做）。</summary>
    public Uri? Href { get; init; }

    /// <summary>未读色点的色调，默认 accent；超时、被退回用 danger、warning。</summary>
    public StatusTone Tone { get; init; }
}

/// <summary>通知的一种类型（筛选按钮）。</summary>
/// <param name="Key">类型：对应 NotificationItem.Type。</param>
/// <param name="Label">名称。</param>
public sealed record NotificationType(string Key, string Label);

/// <summary>这些通知读过了（点了一条、全部标为已读）。</summary>
public sealed class NotificationsReadEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> ids) : RoutedEventArgs(routedEvent)
{
    /// <summary>读过的通知的标识。</summary>
    public IReadOnlyList<string> Ids { get; } = ids;
}

/// <summary>点了一条通知（标为已读之后）。</summary>
public sealed class NotificationClickedEventArgs(RoutedEvent routedEvent, NotificationItem item) : RoutedEventArgs(routedEvent)
{
    /// <summary>那一条。</summary>
    public NotificationItem Item { get; } = item;
}

/// <summary>
/// 通知列表（vue-tsx-pro 的 NotificationList）：标题行（"通知"与"全部标为已读"）、筛选（按类型、只看未读）、通知（新的在前）、底部；
/// 直接放在通知页面、侧边抽屉里，或由 <see cref="NotificationCenter"/> 放进弹出层。
/// <code>&lt;rune:NotificationList Items="{Binding Notices}" Types="{Binding Types}" MarkedRead="OnRead" /&gt;</code>
/// 通知存在哪、何时算已读由应用决定：组件只报告 <see cref="MarkedRead"/>，应用改了 Items 再给回来。时间写成相对现在（3 分钟前、昨天），悬停看完整时间；
/// 未读的前面一个色点（Tone，默认 accent），标题 semibold，读屏念"未读"。有 Href 的点了用系统打开那个地址。列表为空时显示 Empty（默认"没有通知"），
/// 筛完没有时说"没有符合条件的通知"。部件：<see cref="NotificationListParts"/>。
/// </summary>
public class NotificationList : Control
{
    /// <summary>通知：新的在前（组件不排序）。</summary>
    public static readonly StyledProperty<IReadOnlyList<NotificationItem>> ItemsProperty = AvaloniaProperty.Register<NotificationList, IReadOnlyList<NotificationItem>>(nameof(Items), []);

    /// <summary>类型：写了时列表上方有按类型筛选的按钮。</summary>
    public static readonly StyledProperty<IReadOnlyList<NotificationType>> TypesProperty = AvaloniaProperty.Register<NotificationList, IReadOnlyList<NotificationType>>(nameof(Types), []);

    /// <summary>标题，默认"通知"。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<NotificationList, string?>(nameof(Title));

    /// <summary>显示标题行，默认 true；false 时"全部标为已读"挪到筛选那一行（上游的 title={false}）。</summary>
    public static readonly StyledProperty<bool> ShowTitleProperty = AvaloniaProperty.Register<NotificationList, bool>(nameof(ShowTitle), true);

    /// <summary>列表下面的内容（"查看全部"）。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<NotificationList, object?>(nameof(Footer));

    /// <summary>没有通知时显示的内容，默认是"没有通知"。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<NotificationList, object?>(nameof(Empty));

    /// <summary>相对时间以哪个时刻为现在，默认此刻；演示数据固定日期时传。</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty = AvaloniaProperty.Register<NotificationList, DateTimeOffset?>(nameof(Now));

    /// <summary>列表最高多少（超过时在列表里滚动）；默认不限（NotificationCenter 里是 24rem）。</summary>
    public static readonly StyledProperty<double> ListMaxHeightProperty = AvaloniaProperty.Register<NotificationList, double>(nameof(ListMaxHeight), double.PositiveInfinity);

    /// <summary>这些通知读过了。</summary>
    public static readonly RoutedEvent<NotificationsReadEventArgs> MarkedReadEvent = RoutedEvent.Register<NotificationList, NotificationsReadEventArgs>(nameof(MarkedRead), RoutingStrategies.Bubble);

    /// <summary>点了一条。</summary>
    public static readonly RoutedEvent<NotificationClickedEventArgs> ItemClickedEvent = RoutedEvent.Register<NotificationList, NotificationClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch };
    readonly Border head = new() { Name = NotificationListParts.Head };
    readonly Text title = new() { Name = NotificationListParts.Title, VerticalAlignment = VerticalAlignment.Center };
    readonly Button markAll = new() { Name = NotificationListParts.MarkAll, Size = TextSize.Xs, Variant = ButtonVariant.Ghost };
    readonly Flex filters = new() { Name = NotificationListParts.Filters, Wrap = true, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly ToggleGroup types = new() { Name = NotificationListParts.Types, Size = TextSize.Xs, Value = "all", IsRequired = true };
    readonly Switch unreadOnly = new() { Name = NotificationListParts.UnreadOnly, Size = TextSize.Sm };
    readonly ScrollArea scroll = new() { Name = NotificationListParts.Scroll };
    readonly StackPanel list = new() { Name = NotificationListParts.List };
    readonly Border empty = new() { Name = NotificationListParts.Empty };
    readonly Border footer = new() { Name = NotificationListParts.Footer, IsVisible = false };
    readonly ContentControl footerContent = new() { HorizontalAlignment = HorizontalAlignment.Center };

    /// <summary>新建：标题行、筛选、列表、空状态与底部竖排。</summary>
    public NotificationList()
    {
        var headRow = new AvaloniaGrid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { title, markAll } };
        AvaloniaGrid.SetColumn(markAll, 1);
        head.Child = headRow;
        markAll.Click += (_, _) =>
        {
            var ids = Items.Where(item => !item.IsRead).Select(item => item.Id).ToList();
            if (ids.Count > 0) RaiseEvent(new NotificationsReadEventArgs(MarkedReadEvent, ids));
        };
        // "只看未读"靠结束边（没有标题行时"全部标为已读"跟在它后面）
        var spacer = new Border();
        Flex.SetGrow(spacer, true);
        filters.Children.AddRange([types, spacer, unreadOnly]);
        types.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) Rebuild();
        };
        unreadOnly.PropertyChanged += (_, e) =>
        {
            if (e.Property == ChoiceBase.IsCheckedProperty) Rebuild();
        };
        scroll.Content = list;
        footer.Child = footerContent;
        root.Children.AddRange([head, filters, scroll, empty, footer]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>通知。</summary>
    public IReadOnlyList<NotificationItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>类型。</summary>
    public IReadOnlyList<NotificationType> Types { get => GetValue(TypesProperty); set => SetValue(TypesProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>显示标题行。</summary>
    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }

    /// <summary>列表下面的内容。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>没有通知时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>相对时间的"现在"。</summary>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <summary>列表最高多少。</summary>
    public double ListMaxHeight { get => GetValue(ListMaxHeightProperty); set => SetValue(ListMaxHeightProperty, value); }

    /// <summary>这些通知读过了。</summary>
    public event EventHandler<NotificationsReadEventArgs>? MarkedRead
    {
        add => AddHandler(MarkedReadEvent, value);
        remove => RemoveHandler(MarkedReadEvent, value);
    }

    /// <summary>点了一条。</summary>
    public event EventHandler<NotificationClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    /// <summary>显示着的每一条（测试用）。</summary>
    internal IEnumerable<NotificationRow> Rows => list.Children.OfType<NotificationRow>();

    /// <summary>类型筛选与"只看未读"（测试用）。</summary>
    internal (ToggleGroup Types, Switch UnreadOnly, Button MarkAll) Filters => (types, unreadOnly, markAll);

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    double Gap(string key) => this.Resource<double>(key) * Em;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == TypesProperty || change.Property == TitleProperty || change.Property == ShowTitleProperty
            || change.Property == FooterProperty || change.Property == EmptyProperty || change.Property == NowProperty || change.Property == ListMaxHeightProperty
            || change.Property == Provider.LangProperty || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)
            Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    /// <summary>文字、筛选按钮、内边距（Border 没有字号，em 在这里算），再按筛选重建列表。</summary>
    void Rebuild()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        var (sm, md, lg) = (Gap(ThemeKeys.Space.Sm), Gap(ThemeKeys.Space.Md), Gap(ThemeKeys.Space.Lg));
        title.Text = Title ?? messages.Notifications;
        head.IsVisible = ShowTitle;
        head.Padding = new Thickness(md, sm);
        filters.Margin = new Thickness(md, sm);
        empty.Padding = new Thickness(0, lg);
        footer.Padding = new Thickness(0, sm);
        markAll.Content = messages.NotificationsMarkAllRead;
        markAll.IsEnabled = Items.Any(item => !item.IsRead);
        // 没有标题行时"全部标为已读"跟在筛选的最后
        var markAllParent = ShowTitle ? (Panel)head.Child! : filters;
        if (markAll.Parent != markAllParent)
        {
            ((Panel)markAll.Parent!).Children.Remove(markAll);
            markAllParent.Children.Add(markAll);
        }
        unreadOnly.Content = messages.NotificationsUnreadOnly;
        AutomationProperties.SetName(types, messages.Notifications);
        var selected = types.Value;
        types.Children.Clear();
        types.Children.Add(new ToggleItem { Value = "all", Content = messages.NotificationsAll });
        foreach (var kind in Types) types.Children.Add(new ToggleItem { Value = kind.Key, Content = kind.Label });
        types.IsVisible = Types.Count > 0;
        types.Value = Types.Any(kind => kind.Key == selected) ? selected : "all";
        footerContent.Content = Footer;
        footer.IsVisible = Footer is not null;
        scroll.MaxHeight = ListMaxHeight;

        var type = types.Value is "all" or null ? null : types.Value;
        var shown = Items.Where(item => (type is null || item.Type == type) && (unreadOnly.IsChecked != true || !item.IsRead)).ToList();
        list.Children.Clear();
        foreach (var item in shown)
        {
            var row = new NotificationRow(item, Now, messages.NotificationUnreadMark) { Name = NotificationListParts.Item, Padding = new Thickness(md, sm) };
            row.Click += (_, _) => Select(item);
            list.Children.Add(row);
        }
        scroll.IsVisible = shown.Count > 0;
        empty.IsVisible = shown.Count == 0;
        // 有通知、只是筛掉了：说"没有符合条件的"，不让人以为通知被清空了
        empty.Child = shown.Count > 0 ? null
            : Items.Count > 0 ? new Empty { Size = TextSize.Sm, Title = messages.NotificationsNoMatch }
            : Empty as Control ?? new Empty { Size = TextSize.Sm, Title = Empty as string ?? messages.NotificationsEmpty };
    }

    /// <summary>点了一条：没读过时报告读过了，交给 ItemClicked；有绝对地址时用系统打开。</summary>
    void Select(NotificationItem item)
    {
        if (!item.IsRead) RaiseEvent(new NotificationsReadEventArgs(MarkedReadEvent, [item.Id]));
        RaiseEvent(new NotificationClickedEventArgs(ItemClickedEvent, item));
        if (item.Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(this, href);
    }

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
}

/// <summary>
/// 一条通知（<see cref="NotificationList"/> 建的）：三列（0.5rem 的未读色点、标题与说明、相对时间），上边一条 borderMuted，悬停 fillHover；
/// 伪类 <c>:unread</c>（标题 semibold）。读屏是按钮（有地址时是链接），名称前面加"未读"。
/// </summary>
public sealed class NotificationRow : ButtonBase
{
    internal NotificationRow(NotificationItem item, DateTimeOffset? now, string unreadMark)
    {
        Item = item;
        PseudoClasses.Set(":unread", !item.IsRead);
        var dot = new Border { Name = NotificationListParts.Dot, Width = 8, VerticalAlignment = VerticalAlignment.Top };
        if (!item.IsRead) dot.Child = new StatusDot { Tone = item.Tone.Value is null ? StatusTone.Accent : item.Tone, VerticalAlignment = VerticalAlignment.Center };
        var body = new StackPanel { Name = NotificationListParts.Body, Spacing = 2 };
        body.Children.Add(new Text { Name = NotificationListParts.ItemTitle, Text = item.Title, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        if (item.Description is { Length: > 0 } description) body.Children.Add(new Text { Name = NotificationListParts.Description, Text = description, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        var time = new RelativeTime { Name = NotificationListParts.Time, Value = item.Time, Now = now };
        var grid = new AvaloniaGrid { ColumnDefinitions = new ColumnDefinitions("8,*,Auto"), ColumnSpacing = 8, Children = { dot, body, time } };
        AvaloniaGrid.SetColumn(body, 1);
        AvaloniaGrid.SetColumn(time, 2);
        Content = grid;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(this, (item.IsRead ? "" : unreadMark + " ") + item.Title + (item.Description is { Length: > 0 } more ? "，" + more : ""));
    }

    /// <summary>这一条通知。</summary>
    public NotificationItem Item { get; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ButtonBase);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    sealed class RowPeer(NotificationRow owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Item.Href is null ? AutomationControlType.Button : AutomationControlType.Hyperlink;
    }
}

/// <summary>NotificationList 的部件名（TS 的部件）。外层（TS 的 root）是列表自己。</summary>
public static class NotificationListParts
{
    /// <summary>标题行（TS 的 propsHead）：Border，上下 sm、左右 md，下边一条 border。</summary>
    public const string Head = "PART_Head";

    /// <summary>标题：Text，medium。</summary>
    public const string Title = "PART_Title";

    /// <summary>"全部标为已读"（TS 的 propsMarkAll）：Button，xs、ghost。</summary>
    public const string MarkAll = "PART_MarkAll";

    /// <summary>筛选行（TS 的 propsFilters）：Flex，换行、隔 sm，上下 sm、左右 md。</summary>
    public const string Filters = "PART_Filters";

    /// <summary>类型筛选（TS 的 propsTypes）：ToggleGroup，xs。</summary>
    public const string Types = "PART_Types";

    /// <summary>"只看未读"（TS 的 propsUnreadOnly）：Switch，sm，靠结束边。</summary>
    public const string UnreadOnly = "PART_UnreadOnly";

    /// <summary>列表外面的滚动区域（TS 的 propsScroll）：ScrollArea。</summary>
    public const string Scroll = "PART_Scroll";

    /// <summary>列表（TS 的 propsList）：StackPanel。</summary>
    public const string List = "PART_List";

    /// <summary>一条（TS 的 propsItem）：NotificationRow，上下 sm、左右 md。</summary>
    public const string Item = "PART_Item";

    /// <summary>未读色点所在的一列：Border，0.5rem 宽。</summary>
    public const string Dot = "PART_Dot";

    /// <summary>标题与说明：StackPanel，隔 xxs。</summary>
    public const string Body = "PART_Body";

    /// <summary>一条的标题：Text，sm（未读时 semibold）。</summary>
    public const string ItemTitle = "PART_ItemTitle";

    /// <summary>一条的说明：Text，xs、textMuted。</summary>
    public const string Description = "PART_Description";

    /// <summary>相对时间：RelativeTime，xs、textMuted、不换行。</summary>
    public const string Time = "PART_Time";

    /// <summary>空状态（TS 的 propsEmpty）：Border，上下 lg。</summary>
    public const string Empty = "PART_Empty";

    /// <summary>底部（TS 的 propsFooter）：Border，上下 sm、居中，上边一条 border。</summary>
    public const string Footer = "PART_Footer";
}

/// <summary>
/// 通知中心（vue-tsx-pro 的 NotificationCenter）：顶栏的铃铛（右上角是未读数），点开是 <see cref="NotificationList"/>；点了一条就收起。
/// <code>&lt;rune:NotificationCenter Items="{Binding Notices}" Types="{Binding Types}" MarkedRead="OnRead" /&gt;</code>
/// 铃铛的读屏名称带未读数（"通知，未读 2 条"）；面板 26rem 宽（窗口窄时少 2rem）、没有内边距，列表最高 24rem。和 Toast 的分工：Toast 是此刻发生的事、几秒后消失；通知是留着、可以回头看的。
/// </summary>
public class NotificationCenter : Control
{
    /// <summary>通知。</summary>
    public static readonly StyledProperty<IReadOnlyList<NotificationItem>> ItemsProperty = NotificationList.ItemsProperty.AddOwner<NotificationCenter>();

    /// <summary>类型。</summary>
    public static readonly StyledProperty<IReadOnlyList<NotificationType>> TypesProperty = NotificationList.TypesProperty.AddOwner<NotificationCenter>();

    /// <summary>列表下面的内容。</summary>
    public static readonly StyledProperty<object?> FooterProperty = NotificationList.FooterProperty.AddOwner<NotificationCenter>();

    /// <summary>没有通知时显示的内容。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = NotificationList.EmptyProperty.AddOwner<NotificationCenter>();

    /// <summary>相对时间的"现在"。</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty = NotificationList.NowProperty.AddOwner<NotificationCenter>();

    /// <summary>面板开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = Popover.IsOpenProperty.AddOwner<NotificationCenter>();

    /// <summary>弹出位置，默认 bottom-end（铃铛在顶栏右边）。</summary>
    public static readonly StyledProperty<FloatingPlacement> PlacementProperty = Popover.PlacementProperty.AddOwner<NotificationCenter>(new StyledPropertyMetadata<FloatingPlacement>(FloatingPlacement.BottomEnd));

    /// <summary>与铃铛的距离，同 Popover。</summary>
    public static readonly StyledProperty<double> OffsetProperty = Popover.OffsetProperty.AddOwner<NotificationCenter>();

    /// <summary>指向铃铛的小箭头，同 Popover。</summary>
    public static readonly StyledProperty<bool> ArrowProperty = Popover.ArrowProperty.AddOwner<NotificationCenter>();

    /// <summary>用户开、关了面板（同 Popover 的 OpenChanged）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<NotificationCenter, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    readonly IconButton trigger = new() { Name = NotificationCenterParts.Trigger, Icon = Lucide.Bell };
    readonly Indicator indicator = new() { Name = NotificationCenterParts.Indicator };
    readonly NotificationList list = new();
    readonly FlushPopover popover;

    /// <summary>新建：铃铛（带未读数）与弹出层里的列表。</summary>
    public NotificationCenter()
    {
        indicator.Content = trigger;
        popover = new FlushPopover { Trigger = indicator, Content = list };
        popover[!!Popover.IsOpenProperty] = this[!!IsOpenProperty];
        popover[!Popover.PlacementProperty] = this[!PlacementProperty];
        popover[!Popover.OffsetProperty] = this[!OffsetProperty];
        popover[!Popover.ArrowProperty] = this[!ArrowProperty];
        popover.OpenChanged += (_, e) => RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, e.IsOpen, e.Reason));
        foreach (var (mine, theirs) in new (AvaloniaProperty, AvaloniaProperty)[] { (ItemsProperty, NotificationList.ItemsProperty), (TypesProperty, NotificationList.TypesProperty), (FooterProperty, NotificationList.FooterProperty), (EmptyProperty, NotificationList.EmptyProperty), (NowProperty, NotificationList.NowProperty) })
            list[!theirs] = this[!mine];
        // 列表在弹出层里，事件冒泡到不了这里（弹出层的根不在它的可视树里）：在列表上接住，从自己再发一次；点了一条就收起
        list.MarkedRead += (_, e) =>
        {
            e.Handled = true;
            RaiseEvent(new NotificationsReadEventArgs(NotificationList.MarkedReadEvent, e.Ids));
        };
        list.ItemClicked += (_, e) =>
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
            RaiseEvent(new NotificationClickedEventArgs(NotificationList.ItemClickedEvent, e.Item));
        };
        LogicalChildren.Add(popover);
        VisualChildren.Add(popover);
    }

    /// <summary>通知。</summary>
    public IReadOnlyList<NotificationItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>类型。</summary>
    public IReadOnlyList<NotificationType> Types { get => GetValue(TypesProperty); set => SetValue(TypesProperty, value); }

    /// <summary>列表下面的内容。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>没有通知时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>相对时间的"现在"。</summary>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <summary>面板开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>与铃铛的距离。</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>指向铃铛的小箭头。</summary>
    public bool Arrow { get => GetValue(ArrowProperty); set => SetValue(ArrowProperty, value); }

    /// <summary>用户开、关了面板。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>这些通知读过了（面板里的列表报告的）。</summary>
    public event EventHandler<NotificationsReadEventArgs>? MarkedRead
    {
        add => AddHandler(NotificationList.MarkedReadEvent, value);
        remove => RemoveHandler(NotificationList.MarkedReadEvent, value);
    }

    /// <summary>点了一条（面板里的列表报告的）。</summary>
    public event EventHandler<NotificationClickedEventArgs>? ItemClicked
    {
        add => AddHandler(NotificationList.ItemClickedEvent, value);
        remove => RemoveHandler(NotificationList.ItemClickedEvent, value);
    }

    /// <summary>面板里的列表（测试用）。</summary>
    internal NotificationList List => list;

    /// <summary>铃铛（测试用）。</summary>
    internal IconButton Trigger => trigger;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == Provider.LangProperty || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    /// <summary>未读数与铃铛的名称；面板 min(26rem, 窗口宽 − 2rem)、列表最高 24rem。</summary>
    void Sync()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        var unread = Items.Count(item => !item.IsRead);
        indicator.Count = unread;
        indicator.Label = messages.NotificationsUnread(unread);
        AutomationProperties.SetName(trigger, messages.NotificationsUnread(unread));
        var window = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? double.PositiveInfinity;
        list.Width = Math.Min(416, window - 32);
        list.ListMaxHeight = 384;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        popover.Measure(availableSize);
        return popover.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        popover.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>没有内边距的弹出框：列表的标题行、分隔线贴着面板的边。</summary>
    sealed class FlushPopover : Popover
    {
        private protected override Thickness PanelPadding(double em) => default;
    }
}

/// <summary>NotificationCenter 的部件名（TS 的部件）。</summary>
public static class NotificationCenterParts
{
    /// <summary>铃铛按钮（TS 的 propsTrigger）：IconButton。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>未读数（TS 的 indicator 部件）：Indicator，包住铃铛。</summary>
    public const string Indicator = "PART_Indicator";
}
