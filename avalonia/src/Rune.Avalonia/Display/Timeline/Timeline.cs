using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PlatformGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>时间线上的一项（vue-tsx-pro 的 TimelineItem）。标题、说明、操作人、详情可以是文字或控件。</summary>
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
    public IImage? Avatar { get; init; }

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
/// <see cref="Alternate"/> 左右交替排布（时间在对侧），宽度不到 32em 时排回一侧。部件名在 <see cref="TimelineParts"/>。
/// 节点 0.75em 的实心圆点、有图标时 1.5em 的圆（surface 底、sm 宽的色调描边），连接线 sm 宽的 borderMuted；节点列与内容隔 space.md，
/// 内容下方留 space.lg；标题 medium，操作人与时间一行（sm、textMuted），收起的那几项的位置是一段虚线。
/// </summary>
public class Timeline : Control
{
    /// <summary>各项，按时间先后（旧的在前）；是 INotifyCollectionChanged 时跟着变。</summary>
    public static readonly StyledProperty<IEnumerable<TimelineItem>?> ItemsProperty = AvaloniaProperty.Register<Timeline, IEnumerable<TimelineItem>?>(nameof(Items));

    /// <summary>还在进行中：最后多一项，节点是加载指示，内容是它（文字或控件）。</summary>
    public static readonly StyledProperty<object?> PendingProperty = AvaloniaProperty.Register<Timeline, object?>(nameof(Pending));

    /// <summary>左右交替排布（时间在对侧）；窄的时候仍是单侧。</summary>
    public static readonly StyledProperty<bool> AlternateProperty = AvaloniaProperty.Register<Timeline, bool>(nameof(Alternate));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Timeline, TextSize>(nameof(Size));

    /// <summary>新的在前。</summary>
    public static readonly StyledProperty<bool> ReverseProperty = AvaloniaProperty.Register<Timeline, bool>(nameof(Reverse));

    /// <summary>最多显示几项（不算进行中的一项）：只显示最新的这几项，其余收起。</summary>
    public static readonly StyledProperty<int?> MaxVisibleProperty = AvaloniaProperty.Register<Timeline, int?>(nameof(MaxVisible));

    /// <summary>收起的各项展开了没有（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<Timeline, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>窄于它时交替排布排回一侧（em）。</summary>
    const double NarrowEm = 32;

    readonly FlexOrderedList list = new() { Direction = FlexDirection.Column };
    readonly TemplateResources resources;
    INotifyCollectionChanged? observed;
    bool wide = true;

    /// <summary>新建：空的时间线。</summary>
    public Timeline()
    {
        resources = new TemplateResources(this);
        // 界面里的链接（附件、操作）悬停才有下划线
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
        LogicalChildren.Add(list);
        VisualChildren.Add(list);
    }

    /// <summary>各项。</summary>
    public IEnumerable<TimelineItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>进行中的一项。</summary>
    public object? Pending { get => GetValue(PendingProperty); set => SetValue(PendingProperty, value); }

    /// <summary>左右交替。</summary>
    public bool Alternate { get => GetValue(AlternateProperty); set => SetValue(AlternateProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>新的在前。</summary>
    public bool Reverse { get => GetValue(ReverseProperty); set => SetValue(ReverseProperty, value); }

    /// <summary>最多显示几项。</summary>
    public int? MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }

    /// <summary>收起的各项展开了。</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>显示的各行（测试用）：项、进行中的一项、展开按钮那一行，按显示的顺序。</summary>
    internal IReadOnlyList<TimelineRow> Rows => [.. list.Children.OfType<TimelineRow>()];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            if (observed is not null) observed.CollectionChanged -= OnItemsChanged;
            observed = Items as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnItemsChanged;
            Rebuild();
        }
        else if (change.Property == PendingProperty || change.Property == AlternateProperty || change.Property == ReverseProperty
                 || change.Property == MaxVisibleProperty || change.Property == IsExpandedProperty) Rebuild();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == BoundsProperty)
        {
            // 容器查询（TS 的 @container (max-width: 32em)）：宽度跨过 32em 时换排布
            var next = Bounds.Width >= NarrowEm * TextElement.GetFontSize(this);
            if (next == wide) return;
            wide = next;
            if (Alternate) Rebuild();
        }
    }

    void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

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
        // 先放开旧行里使用方给的元素，新行才放得进去
        foreach (var row in Rows) row.Detach();
        list.Children.Clear();
        var rows = new List<Control>();
        if (hidden > 0 && !Reverse) rows.Add(ToggleRow(hidden));
        for (var index = 0; index < chronological.Count; index++)
        {
            var last = index == chronological.Count - 1 && !toggleAtEnd;
            rows.Add(new TimelineRow(chronological[index].Item, chronological[index].Pending, last, alternated, alternated && index % 2 == 1));
        }
        if (toggleAtEnd) rows.Add(ToggleRow(hidden));
        list.Children.AddRange(rows);
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    TimelineRow ToggleRow(int hidden)
    {
        var lang = Provider.GetLang(this);
        var button = new Button
        {
            Name = TimelineParts.Toggle, Variant = ButtonVariant.Link, Size = TextSize.Sm,
            IconEnd = IsExpanded ? Lucide.ChevronUp : Lucide.ChevronDown,
            Content = IsExpanded ? lang.TimelineShowLess : lang.TimelineShowMore(hidden),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.SetValue(Popover.TriggerExpandedProperty, IsExpanded);
        button.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
        return new TimelineRow(button);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TimelinePeer(this);

    /// <summary>时间线自己不占读屏节点：里面的有序列表才是。</summary>
    sealed class TimelinePeer(Timeline owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>时间线的一行（有序列表的一项）：节点列（节点与连到下一项的线）+ 内容；交替排布时三列，时间在对侧。</summary>
public sealed class TimelineRow : PlatformGrid
{
    readonly Panel rail = new() { Name = TimelineParts.Rail };
    readonly Border line = new() { Name = TimelineParts.Line, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel content = new() { Name = TimelineParts.Content };
    bool hasLine;

    /// <summary>一项：节点、标题、操作人与时间、说明、详情。</summary>
    internal TimelineRow(TimelineItem item, bool pending, bool last, bool alternated, bool end)
    {
        Item = item;
        IsPending = pending;
        hasLine = !last;
        Columns(alternated, end);
        Marker = pending
            ? new Spinner { Name = TimelineParts.Pending, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top }
            : new Border { Name = TimelineParts.Marker, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Child = item.Icon is { } icon ? new Icon { Data = icon } : null };
        rail.Children.Add(line);
        rail.Children.Add(Marker);
        line.IsVisible = hasLine;
        Children.Add(rail);
        Children.Add(content);
        content.Children.Add(Part(item.Title ?? "", TimelineParts.Title, title: true));
        var time = item.Time is { Length: > 0 } text ? TimeText(text, item.DateTime) : null;
        // 交替排布：时间在对侧，没有时间也占一格（保持三列对齐）
        if (alternated)
        {
            Control opposite = (Control?)time ?? new Border();
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
                actor.Children.Add(new Avatar { Name = TimelineParts.Avatar, Source = item.Avatar, DisplayName = item.Actor as string, Decorative = true });
            actor.Children.Add(item.Actor as Control ?? new Text { Text = item.Actor.ToString() });
            Meta = new Flex { Name = TimelineParts.Meta, Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { actor } };
            if (time is not null) Meta.Children.Add(time);
            content.Children.Add(Meta);
        }
        else if (time is not null) content.Children.Add(time);
        Time ??= time;
        if (item.Description is not null) content.Children.Add(Part(item.Description, TimelineParts.Description, title: false));
        if (item.Details is not null) content.Children.Add(Details = DetailsPart(item.Details));
        // 交替排布时偶数项的内容在节点左侧、靠结束边对齐（TS 的 text-align: end）
        if (end)
            foreach (var part in content.Children)
            {
                part.HorizontalAlignment = HorizontalAlignment.Right;
                if (part is TextBlock block) block.TextAlignment = TextAlignment.Right;
            }
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
        // 进行中的一项：读屏念"正在加载"（TS 的 aria-busy）
        if (pending) AutomationProperties.SetItemStatus(this, "busy");
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    internal TimelineRow(Button toggle)
    {
        Item = null;
        Toggle = toggle;
        Columns(false, false);
        var dashed = new Line { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), Stretch = Stretch.Fill, StrokeDashArray = [3, 3], HorizontalAlignment = HorizontalAlignment.Center };
        dashed.Bind(Shape.StrokeProperty, this.GetResourceObservable(ThemeKeys.Brush.BorderMuted));
        rail.Children.Add(dashed);
        Children.Add(rail);
        content.Children.Add(toggle);
        Children.Add(content);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    /// <summary>这一行的项；展开按钮那一行为 null。</summary>
    public TimelineItem? Item { get; }

    /// <summary>是进行中的一项。</summary>
    public bool IsPending { get; }

    /// <summary>节点（圆点、图标或加载指示）。</summary>
    internal Control? Marker { get; }

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

    /// <summary>节点列一列、内容一列；交替时三列，偶数项的内容在节点左侧、靠结束边对齐。</summary>
    void Columns(bool alternated, bool end)
    {
        ColumnDefinitions = alternated ? new ColumnDefinitions("*,Auto,*") : new ColumnDefinitions("Auto,*");
        SetColumn(rail, alternated ? 1 : 0);
        SetColumn(content, alternated ? (end ? 0 : 2) : 1);
    }

    static Control Part(object value, string name, bool title)
    {
        if (value is Control control) return control;
        Text text = title ? new Text { Weight = TextWeight.Medium } : new Text();
        text.Name = name;
        text.Text = value.ToString();
        return text;
    }

    static Small TimeText(string text, DateTimeOffset? at)
    {
        var time = new Small { Name = TimelineParts.Time, Text = text, Tone = TextTone.Muted };
        if (at is { } value) AutomationProperties.SetHelpText(time, value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return time;
    }

    /// <summary>详情：一个像链接的"查看详情"按钮，点开才建内容。</summary>
    Collapsible DetailsPart(object details)
    {
        var trigger = new Button { Variant = ButtonVariant.Link, Size = TextSize.Sm, IconEnd = Lucide.ChevronDown, HorizontalAlignment = HorizontalAlignment.Left };
        var collapsible = new Collapsible { Name = TimelineParts.Details, Trigger = trigger };
        collapsible.PropertyChanged += (_, e) =>
        {
            if (e.Property != Collapsible.IsOpenProperty) return;
            trigger.IconEnd = collapsible.IsOpen ? Lucide.ChevronUp : Lucide.ChevronDown;
            // 打开时才建（TS 的 lazy）
            if (collapsible.IsOpen && collapsible.Content is null) collapsible.Content = details as Control ?? new Text { Text = details.ToString() };
        };
        trigger.AttachedToVisualTree += (_, _) => trigger.Content = Provider.GetLang(trigger).TimelineDetails;
        return collapsible;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
    }

    /// <summary>按 em 的尺寸与颜色：节点、连接线、间距（Grid、Border 上没有字号，主题里的 em 算不出来）。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        var thin = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
        ColumnSpacing = Space(ThemeKeys.Space.Md);
        rail.MinWidth = 1.5 * em;
        content.Spacing = Space(ThemeKeys.Space.Xs);
        content.Margin = new Thickness(0, 0, 0, Space(ThemeKeys.Space.Lg));
        line.Width = thin;
        line.Bind(Border.BackgroundProperty, this.GetResourceObservable(ThemeKeys.Brush.BorderMuted));
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
            marker.Margin = new Thickness(0, Item.Icon is null ? 0.375 * em : 0);
            if (Item.Icon is null) marker.Bind(Border.BackgroundProperty, this.GetResourceObservable(tone));
            else
            {
                marker.Bind(Border.BackgroundProperty, this.GetResourceObservable(ThemeKeys.Brush.Surface));
                marker.Bind(Border.BorderBrushProperty, this.GetResourceObservable(tone));
                marker.BorderThickness = new Thickness(thin);
                if (marker.Child is Icon icon) icon.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(tone));
            }
            // 线从节点下面接到这一行的底（连到下一项）
            line.Margin = new Thickness(0, size + marker.Margin.Top * 2, 0, 0);
        }
        else if (Marker is Spinner spinner)
        {
            spinner.Margin = new Thickness(0, 0.25 * em);
            spinner.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(ThemeKeys.Brush.Accent));
        }
        if (Time is not null) Time.Margin = Time.Parent == this ? new Thickness(0, 0.125 * em, 0, 0) : default;
        if (Meta is { } meta)
        {
            var small = this.Resource<double>(ThemeKeys.FontSize.Sm) * em / this.Resource<double>(ThemeKeys.FontSize.Md);
            TextElement.SetFontSize(meta, small);
            // 头像 1.75em：Avatar 按自己字号的 2.5 倍定边长，字号给 0.7em
            if (meta.Children.OfType<Flex>().FirstOrDefault()?.Children.OfType<Avatar>().FirstOrDefault() is { } avatar)
                avatar.Size = new TextSize((1.75 / 2.5 * small).ToString(System.Globalization.CultureInfo.InvariantCulture));
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
}

/// <summary>Timeline 的部件名：样式里用 <c>rune|Timeline Border#PART_Marker</c> 这样选中。</summary>
public static class TimelineParts
{
    /// <summary>节点与连接线所在的列（TS 的 propsRail）。</summary>
    public const string Rail = "PART_Rail";

    /// <summary>节点（TS 的 propsMarker）。</summary>
    public const string Marker = "PART_Marker";

    /// <summary>连接线（TS 的 propsLine）。</summary>
    public const string Line = "PART_Line";

    /// <summary>内容（TS 的 propsContent）。</summary>
    public const string Content = "PART_Content";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>操作人与时间那一行（TS 的 propsMeta）。</summary>
    public const string Meta = "PART_Meta";

    /// <summary>时间（TS 的 propsTime）。</summary>
    public const string Time = "PART_Time";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>详情（TS 的 propsDetails）。</summary>
    public const string Details = "PART_Details";

    /// <summary>展开其余各项的按钮（TS 的 propsToggle）。</summary>
    public const string Toggle = "PART_Toggle";

    /// <summary>操作人头像（TS 的 propsAvatar）。</summary>
    public const string Avatar = "PART_Avatar";

    /// <summary>进行中那一项的加载指示（TS 的 propsPending）。</summary>
    public const string Pending = "PART_Pending";
}
