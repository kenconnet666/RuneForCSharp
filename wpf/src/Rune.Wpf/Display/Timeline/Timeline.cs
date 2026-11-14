using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>时间线上的一项（vue-tsx-pro 的 TimelineItem）。标题、说明、操作人、详情可以是文字或元素。</summary>
public sealed class TimelineItem
{
    /// <summary>标题。</summary>
    public object? Title { get; init; }

    /// <summary>时间（显示的文字）。</summary>
    public string? Time { get; init; }

    /// <summary>机器可读的时间：读屏把它作为时间的帮助文字（TS 写到 time 的 datetime 上）。</summary>
    public DateTimeOffset? DateTime { get; init; }

    /// <summary>说明：一段文字，或字段差异这类内容。</summary>
    public object? Description { get; init; }

    /// <summary>操作人：显示在时间前面；是文字时带头像（没有 <see cref="Avatar"/> 时显示首字母）。</summary>
    public object? Actor { get; init; }

    /// <summary>操作人的头像。</summary>
    public ImageSource? Avatar { get; init; }

    /// <summary>收起的详情：项里一个"查看详情"的按钮，点开才显示（打开时才建）。</summary>
    public object? Details { get; init; }

    /// <summary>节点的图标；不写时是圆点。</summary>
    public IconData? Icon { get; init; }

    /// <summary>节点的色调，默认 neutral。</summary>
    public StatusTone Tone { get; init; }
}

/// <summary>
/// 时间线（vue-tsx-pro 的 Timeline，有序列表）：按时间排列的事件（订单进度、操作记录、审批流转、版本历史）。每项一个节点（圆点或图标，带色调）
/// 与标题、时间、说明，节点之间连一条线。
/// <code>
/// &lt;rune:Timeline Items="{Binding Events}" Pending="正在派送" /&gt;
/// &lt;rune:Timeline Items="{Binding Log}" Reverse="True" MaxVisible="4" /&gt;
/// </code>
/// <see cref="Items"/> 按时间先后给（旧的在前）；<see cref="Reverse"/> 时新的在前，进行中的一项（<see cref="Pending"/>，节点是加载指示）也在最前。
/// 操作记录：项写 Actor（操作人，带头像或首字母）显示在时间前面；长的内容放 Details，项里一个"查看详情"的按钮点开才显示。
/// 记录多时写 <see cref="MaxVisible"/>：只显示最新的几项，其余收起在"展开其余 N 条"后面（按钮在旧的那一端，展开后是"收起"；<see cref="IsExpanded"/> 可绑定）。
/// <see cref="Alternate"/> 左右交替排布（时间在对侧），宽度不到 32em 时排回一侧。外观见 <see cref="TimelineRow"/>。
/// </summary>
public class Timeline : CompositeControl
{
    /// <summary>各项，按时间先后（旧的在前）；是 INotifyCollectionChanged 时跟着变。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable<TimelineItem>), typeof(Timeline),
        new PropertyMetadata(null, (d, e) => ((Timeline)d).OnItemsChanged(e.OldValue as INotifyCollectionChanged)));

    /// <summary>还在进行中：最后多一项，节点是加载指示，内容是它（文字或元素）。</summary>
    public static readonly DependencyProperty PendingProperty = DependencyProperty.Register(nameof(Pending), typeof(object), typeof(Timeline), new PropertyMetadata(null, OnRebuild));

    /// <summary>左右交替排布（时间在对侧）；窄的时候仍是单侧。</summary>
    public static readonly DependencyProperty AlternateProperty = DependencyProperty.Register(nameof(Alternate), typeof(bool), typeof(Timeline), new PropertyMetadata(false, OnRebuild));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Timeline),
        new PropertyMetadata(default(TextSize), (d, _) => ((Timeline)d).ApplySize()));

    /// <summary>新的在前。</summary>
    public static readonly DependencyProperty ReverseProperty = DependencyProperty.Register(nameof(Reverse), typeof(bool), typeof(Timeline), new PropertyMetadata(false, OnRebuild));

    /// <summary>最多显示几项（不算进行中的一项）：只显示最新的这几项，其余收起。</summary>
    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(nameof(MaxVisible), typeof(int?), typeof(Timeline), new PropertyMetadata(null, OnRebuild));

    /// <summary>收起的各项展开了没有（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(Timeline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRebuild));

    /// <summary>窄于它时交替排布排回一侧（em）。</summary>
    const double NarrowEm = 32;

    readonly FlexOrderedList list = new() { Direction = FlexDirection.Column };
    bool wide = true;

    /// <summary>新建：空的时间线。</summary>
    public Timeline()
    {
        Root = list;
        // 界面里的链接（附件、操作）悬停才有下划线
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        SizeChanged += (_, _) => UpdateWide();
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>各项。</summary>
    public IEnumerable<TimelineItem>? Items { get => (IEnumerable<TimelineItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>进行中的一项。</summary>
    public object? Pending { get => GetValue(PendingProperty); set => SetValue(PendingProperty, value); }

    /// <summary>左右交替。</summary>
    public bool Alternate { get => (bool)GetValue(AlternateProperty); set => SetValue(AlternateProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>新的在前。</summary>
    public bool Reverse { get => (bool)GetValue(ReverseProperty); set => SetValue(ReverseProperty, value); }

    /// <summary>最多显示几项。</summary>
    public int? MaxVisible { get => (int?)GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }

    /// <summary>收起的各项展开了。</summary>
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>显示的各行（测试用）：项、进行中的一项、展开按钮那一行，按显示的顺序。</summary>
    internal IReadOnlyList<TimelineRow> Rows => [.. list.Children.OfType<TimelineRow>()];

    static void OnRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).Rebuild();

    void OnItemsChanged(INotifyCollectionChanged? old)
    {
        if (old is not null) old.CollectionChanged -= OnCollectionChanged;
        if (Items is INotifyCollectionChanged observed) observed.CollectionChanged += OnCollectionChanged;
        Rebuild();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>字号档位换成主题的字号资源（像素值直接用）；不写时继承外面的。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(FontSizeProperty, key);
        else ClearValue(FontSizeProperty);
    }

    /// <summary>容器查询（TS 的 @container (max-width: 32em)）：宽度跨过 32em 时换排布。</summary>
    void UpdateWide()
    {
        var next = ActualWidth >= NarrowEm * FontSize;
        if (next == wide) return;
        wide = next;
        if (Alternate) Rebuild();
    }

    /// <summary>按项、进行中、收起与顺序重建各行（同 TS 每次渲染重算：收起的在旧的那一端，倒序时在最后）。</summary>
    void Rebuild()
    {
        var items = Items?.ToList() ?? [];
        var hidden = MaxVisible is { } max && items.Count > max ? items.Count - max : 0;
        var shown = hidden > 0 && !IsExpanded ? items.Skip(hidden) : items;
        var chronological = shown.Select(item => (Item: item, Pending: false)).ToList();
        if (Pending is not null && Pending is not "") chronological.Add((new TimelineItem { Title = Pending }, true));
        if (Reverse) chronological.Reverse();
        var toggleAtEnd = hidden > 0 && Reverse;
        var alternated = Alternate && wide;
        foreach (var row in Rows) row.Detach();
        list.Children.Clear();
        if (hidden > 0 && !Reverse) list.Children.Add(ToggleRow(hidden));
        for (var index = 0; index < chronological.Count; index++)
        {
            var last = index == chronological.Count - 1 && !toggleAtEnd;
            list.Children.Add(new TimelineRow(chronological[index].Item, chronological[index].Pending, last, alternated, alternated && index % 2 == 1));
        }
        if (toggleAtEnd) list.Children.Add(ToggleRow(hidden));
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    TimelineRow ToggleRow(int hidden)
    {
        var lang = Provider.GetLang(this);
        var button = new Button
        {
            Variant = ButtonVariant.Link, Size = TextSize.Sm,
            IconEnd = IsExpanded ? Lucide.ChevronUp : Lucide.ChevronDown,
            Content = IsExpanded ? lang.TimelineShowLess : lang.TimelineShowMore(hidden),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.SetValue(Popover.TriggerExpandedProperty, IsExpanded);
        button.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
        return new TimelineRow(button);
    }
}

/// <summary>
/// 时间线的一行（有序列表的一项）：节点列（节点与连到下一项的线）+ 内容；交替排布时三列，时间在对侧。
/// 节点 0.75em 的实心圆点、有图标时 1.5em 的圆（surface 底、sm 宽的色调描边），连接线 sm 宽的 borderMuted；节点列与内容隔 space.md，
/// 内容下方留 space.lg、各部分隔 space.xs；标题 medium，操作人与时间一行（sm、textMuted），收起的那几项的位置是一段虚线。
/// </summary>
public sealed class TimelineRow : LayoutGrid
{
    readonly LayoutGrid rail = new();
    readonly Border line = new() { HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel content = new();
    readonly bool alternated;
    readonly bool hasLine;

    /// <summary>一项：节点、标题、操作人与时间、说明、详情。</summary>
    internal TimelineRow(TimelineItem item, bool pending, bool last, bool alternated, bool end)
    {
        Item = item;
        IsPending = pending;
        this.alternated = alternated;
        hasLine = !last;
        Columns(end);
        Marker = pending
            ? new Spinner { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top }
            : new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Child = item.Icon is { } icon ? new Icon { Data = icon } : null };
        line.Visibility = hasLine ? Visibility.Visible : Visibility.Collapsed;
        rail.Children.Add(line);
        rail.Children.Add(Marker);
        Children.Add(rail);
        Children.Add(content);
        content.Children.Add(Part(item.Title ?? "", title: true));
        var time = item.Time is { Length: > 0 } text ? TimeText(text, item.DateTime) : null;
        // 交替排布：时间在对侧，没有时间也占一格（保持三列对齐）
        if (alternated)
        {
            FrameworkElement opposite = (FrameworkElement?)time ?? new Border();
            SetColumn(opposite, end ? 2 : 0);
            opposite.HorizontalAlignment = end ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            Children.Add(opposite);
            Time = time;
            time = null;
        }
        if (item.Actor is not null)
        {
            var actor = new Flex { Align = FlexAlign.Center, Gap = Space.Xs };
            if (item.Avatar is not null || item.Actor is string)
                actor.Children.Add(new Avatar { Source = item.Avatar, DisplayName = item.Actor as string, Decorative = true });
            actor.Children.Add(item.Actor as UIElement ?? new Text { Text = item.Actor.ToString() });
            Meta = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { actor } };
            if (time is not null) Meta.Children.Add(time);
            content.Children.Add(Meta);
        }
        else if (time is not null) content.Children.Add(time);
        Time ??= time;
        if (item.Description is not null) content.Children.Add(Part(item.Description, title: false));
        if (item.Details is not null) content.Children.Add(Details = DetailsPart(item.Details));
        // 交替排布时偶数项的内容在节点左侧、靠结束边对齐（TS 的 text-align: end）
        if (end)
            foreach (FrameworkElement part in content.Children)
            {
                part.HorizontalAlignment = HorizontalAlignment.Right;
                if (part is TextBlock block) block.TextAlignment = TextAlignment.Right;
            }
        // 进行中的一项：读屏念"正在加载"（TS 的 aria-busy）
        if (pending) AutomationProperties.SetItemStatus(this, "busy");
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    internal TimelineRow(Button toggle)
    {
        Toggle = toggle;
        Columns(false);
        var dashed = new Line { Y2 = 1, Stretch = Stretch.Fill, StrokeDashArray = [3, 3], HorizontalAlignment = HorizontalAlignment.Center };
        dashed.SetResourceReference(Shape.StrokeProperty, ThemeKeys.Brush.BorderMuted);
        rail.Children.Add(dashed);
        Children.Add(rail);
        content.Children.Add(toggle);
        Children.Add(content);
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>这一行的项；展开按钮那一行为 null。</summary>
    public TimelineItem? Item { get; }

    /// <summary>是进行中的一项。</summary>
    public bool IsPending { get; }

    /// <summary>节点（圆点、图标或加载指示）。</summary>
    internal FrameworkElement? Marker { get; }

    /// <summary>连到下一项的线（最后一项没有）。</summary>
    internal Border? Line => hasLine ? line : null;

    /// <summary>时间。</summary>
    internal TextBlock? Time { get; }

    /// <summary>操作人与时间那一行。</summary>
    internal Flex? Meta { get; }

    /// <summary>详情。</summary>
    internal Collapsible? Details { get; }

    /// <summary>展开其余各项的按钮。</summary>
    internal Button? Toggle { get; }

    /// <summary>内容。</summary>
    internal StackPanel Content => content;

    /// <summary>放开使用方给的元素（标题、说明、操作人、详情）：重建时它们要放进新的一行，一个元素只能有一个父元素。</summary>
    internal void Detach()
    {
        // 先放开里层（操作人在 Meta 里的那个 Flex 里），再放开外层
        if (Meta is not null)
        {
            foreach (var inner in Meta.Children.OfType<Panel>().ToList()) inner.Children.Clear();
            Meta.Children.Clear();
        }
        if (Details is not null) Details.Content = null;
        content.Children.Clear();
        Children.Clear();
    }

    /// <summary>节点列一列、内容一列；交替时三列，偶数项的内容在节点左侧。</summary>
    void Columns(bool end)
    {
        var count = alternated ? 3 : 2;
        for (var i = 0; i < count; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = i == (alternated ? 1 : 0) ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        SetColumn(rail, alternated ? 1 : 0);
        SetColumn(content, alternated ? (end ? 0 : 2) : 1);
    }

    static UIElement Part(object value, bool title)
    {
        if (value is UIElement element) return element;
        return title ? new Text { Text = value.ToString(), Weight = TextWeight.Medium } : new Text { Text = value.ToString() };
    }

    static Small TimeText(string text, DateTimeOffset? at)
    {
        var time = new Small { Text = text, Tone = TextTone.Muted };
        if (at is { } value) AutomationProperties.SetHelpText(time, value.ToString("O", CultureInfo.InvariantCulture));
        return time;
    }

    /// <summary>详情：一个像链接的"查看详情"按钮，点开才建内容。</summary>
    static Collapsible DetailsPart(object details)
    {
        var trigger = new Button { Variant = ButtonVariant.Link, Size = TextSize.Sm, IconEnd = Lucide.ChevronDown, HorizontalAlignment = HorizontalAlignment.Left };
        var collapsible = new Collapsible { Trigger = trigger };
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Collapsible.IsOpenProperty, typeof(Collapsible)).AddValueChanged(collapsible, (_, _) =>
        {
            trigger.IconEnd = collapsible.IsOpen ? Lucide.ChevronUp : Lucide.ChevronDown;
            // 打开时才建（TS 的 lazy）
            if (collapsible.IsOpen && collapsible.Content is null) collapsible.Content = details as UIElement ?? new Text { Text = details.ToString() };
        });
        trigger.Loaded += (_, _) => trigger.Content = Provider.GetLang(trigger).TimelineDetails;
        return collapsible;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextElement.FontSizeProperty && IsLoaded) UpdateMetrics();
    }

    /// <summary>按 em 的尺寸与颜色：节点、连接线、间距。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        var thin = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
        var gap = Space(ThemeKeys.Space.Md);
        rail.MinWidth = 1.5 * em;
        rail.Margin = new Thickness(alternated ? gap : 0, 0, gap, 0);
        content.Margin = new Thickness(0, 0, 0, Space(ThemeKeys.Space.Lg));
        foreach (FrameworkElement part in content.Children.Cast<FrameworkElement>().Skip(1)) part.Margin = new Thickness(0, Space(ThemeKeys.Space.Xs), 0, 0);
        line.Width = thin;
        line.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.BorderMuted);
        if (Toggle is not null)
        {
            rail.MinHeight = 2 * em;
            if (rail.Children[0] is Shape dashed) dashed.StrokeThickness = thin;
            return;
        }
        if (Item is null) return;
        var tone = ToneKey(Item.Tone);
        if (Marker is Border marker)
        {
            var size = Item.Icon is null ? 0.75 * em : 1.5 * em;
            marker.Width = marker.Height = size;
            marker.CornerRadius = new CornerRadius(size / 2);
            marker.Margin = new Thickness(0, Item.Icon is null ? 0.375 * em : 0, 0, 0);
            if (Item.Icon is null) marker.SetResourceReference(Border.BackgroundProperty, tone);
            else
            {
                marker.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Surface);
                marker.SetResourceReference(Border.BorderBrushProperty, tone);
                marker.BorderThickness = new Thickness(thin);
                if (marker.Child is Icon icon) icon.SetResourceReference(TextElement.ForegroundProperty, tone);
            }
            // 线从节点下面接到这一行的底（连到下一项）
            line.Margin = new Thickness(0, size + marker.Margin.Top * 2, 0, 0);
        }
        else if (Marker is Spinner spinner)
        {
            spinner.Margin = new Thickness(0, 0.25 * em, 0, 0.25 * em);
            spinner.SetResourceReference(Control.ForegroundProperty, ThemeKeys.Brush.Accent);
        }
        if (Time is not null && Time.Parent == this) Time.Margin = new Thickness(0, 0.125 * em, 0, 0);
        if (Meta is not null)
        {
            var small = this.Resource<double>(ThemeKeys.FontSize.Sm) * em / this.Resource<double>(ThemeKeys.FontSize.Md);
            TextElement.SetFontSize(Meta, small);
            // 头像 1.75em：Avatar 按自己字号的 2.5 倍定边长，字号给 0.7em
            if (Meta.Children.OfType<Flex>().FirstOrDefault()?.Children.OfType<Avatar>().FirstOrDefault() is { } avatar) avatar.FontSize = 1.75 / 2.5 * small;
        }
    }

    /// <summary>节点的色调：neutral 是 borderStrong，其余是各色调的文字色（accent 是实色）。</summary>
    static string ToneKey(StatusTone tone) => tone.Value switch
    {
        "accent" => ThemeKeys.Brush.Accent,
        "info" => ThemeKeys.Brush.InfoText,
        "success" => ThemeKeys.Brush.SuccessText,
        "warning" => ThemeKeys.Brush.WarningText,
        "danger" => ThemeKeys.Brush.DangerText,
        _ => ThemeKeys.Brush.BorderStrong,
    };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>读屏：有序列表的一项。</summary>
    sealed class RowPeer(TimelineRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(TimelineRow);
    }
}
