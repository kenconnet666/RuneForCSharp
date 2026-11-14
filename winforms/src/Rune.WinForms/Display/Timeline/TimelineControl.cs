using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>时间线上的一项（vue-tsx-pro 的 TimelineItem）。标题、说明、操作人、详情可以是文字或控件。</summary>
public sealed class TimelineItem
{
    /// <summary>标题。</summary>
    public object? Title { get; init; }

    /// <summary>时间（显示的文字）。</summary>
    public string? Time { get; init; }

    /// <summary>机器可读的时间：时间的读屏描述（TS 写到 time 的 datetime 上）。</summary>
    public DateTimeOffset? DateTime { get; init; }

    /// <summary>说明：一段文字，或字段差异这类内容。</summary>
    public object? Description { get; init; }

    /// <summary>操作人：显示在时间前面；是文字时带头像（没有 <see cref="Avatar"/> 时显示首字母）。</summary>
    public object? Actor { get; init; }

    /// <summary>操作人的头像。</summary>
    public Image? Avatar { get; init; }

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
/// var progress = new TimelineControl { Items = events, Pending = "正在派送" };
/// var log = new TimelineControl { Items = records, Reverse = true, MaxVisible = 4 };
/// </code>
/// <see cref="Items"/> 按时间先后给（旧的在前）；<see cref="Reverse"/> 时新的在前，进行中的一项（<see cref="Pending"/>，节点是加载指示）也在最前。
/// 操作记录：项写 Actor（操作人，带头像或首字母）显示在时间前面；长的内容放 Details，项里一个"查看详情"的按钮点开才显示。
/// 记录多时写 <see cref="MaxVisible"/>：只显示最新的几项，其余收起在"展开其余 N 条"后面（按钮在旧的那一端，展开后是"收起"；<see cref="ExpandedChanged"/> 报告）。
/// <see cref="Alternate"/> 左右交替排布（时间在对侧），宽度不到 32em 时排回一侧。应用里统一的档位写在 ProviderControl 的 <see cref="TimelineDefaults"/>。
/// 外观见 <see cref="TimelineRowControl"/>。
/// </summary>
[DefaultEvent(nameof(ExpandedChanged))]
public class TimelineControl : FlexOrderedListControl, IScopeAware
{
    /// <summary>窄于它时交替排布排回一侧（em）。</summary>
    const float NarrowEm = 32;

    bool? alternate;
    TextSize size;
    INotifyCollectionChanged? observed;
    bool wide = true;

    /// <summary>新建：空的时间线，竖排。</summary>
    public TimelineControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        // 界面里的链接（附件、操作）悬停才有下划线
        LinkControl.SetUnderlineScope(this, LinkUnderline.Hover);
    }

    /// <summary>各项，按时间先后（旧的在前）；是 INotifyCollectionChanged 时跟着变。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable<TimelineItem>? Items
    {
        get;
        set
        {
            if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
            field = value;
            observed = value as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
            Rebuild();
        }
    }

    /// <summary>还在进行中：最后多一项，节点是加载指示，内容是它（文字或控件）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Pending { get; set { field = value; Rebuild(); } }

    /// <summary>左右交替排布（时间在对侧）；窄的时候仍是单侧。不写时取作用域的默认值。</summary>
    [Category("Appearance")]
    public bool Alternate { get => alternate ?? ScopeDefaults?.Alternate ?? false; set { alternate = value; Rebuild(); } }

    /// <summary>字号档位；不写时取作用域的默认值，再没有就跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get => size.Or(ScopeDefaults?.Size, default);
        set
        {
            size = value;
            ApplySize();
        }
    }

    /// <summary>新的在前。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Reverse { get; set { field = value; Rebuild(); } }

    /// <summary>最多显示几项（不算进行中的一项）：只显示最新的这几项，其余收起。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? MaxVisible { get; set { field = value; Rebuild(); } }

    /// <summary>收起的各项展开了没有。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    bool ShouldSerializeAlternate() => alternate is not null;

    bool ShouldSerializeTextSize() => size.Value is not null;

    /// <summary>收起的各项展开或收起了。</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>显示的各行（测试用）：项、进行中的一项、展开按钮那一行，按显示的顺序。</summary>
    internal IReadOnlyList<TimelineRowControl> Rows => [.. Controls.OfType<TimelineRowControl>()];

    TimelineDefaults? ScopeDefaults => ProviderControl.Of(this)?.ResolveDefaults<TimelineDefaults>();

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    void IScopeAware.ScopeChanged()
    {
        ApplySize();
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 作用域的默认值要放进界面以后才拿得到
        ApplySize();
        Rebuild();
    }

    void ApplySize()
    {
        if (TextSize.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
        else ResetFont();
    }

    /// <summary>容器查询（TS 的 @container (max-width: 32em)）：宽度跨过 32em 时换排布。</summary>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        var next = Width >= NarrowEm * this.EmPixels();
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
        SuspendLayout();
        // 先放开旧行里使用方给的控件，新行才放得进去（Controls.Clear 不释放它们）
        foreach (var row in Rows)
        {
            row.Detach();
            row.Dispose();
        }
        Controls.Clear();
        var rows = new List<Control>();
        if (hidden > 0 && !Reverse) rows.Add(ToggleRow(hidden));
        for (var index = 0; index < chronological.Count; index++)
        {
            var last = index == chronological.Count - 1 && !toggleAtEnd;
            rows.Add(new TimelineRowControl(chronological[index].Item, chronological[index].Pending, last, alternated, alternated && index % 2 == 1));
        }
        if (toggleAtEnd) rows.Add(ToggleRow(hidden));
        Controls.AddRange([.. rows]);
        ResumeLayout();
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    TimelineRowControl ToggleRow(int hidden)
    {
        var lang = ProviderControl.LangOf(this);
        var button = new ButtonControl
        {
            Variant = ButtonVariant.Link, TextSize = TextSize.Sm,
            IconEnd = IsExpanded ? Lucide.ChevronUp : Lucide.ChevronDown,
            Text = IsExpanded ? lang.TimelineShowLess : lang.TimelineShowMore(hidden),
            Expanded = IsExpanded,
        };
        button.Click += (_, _) => IsExpanded = !IsExpanded;
        return new TimelineRowControl(button);
    }
}

/// <summary>
/// 时间线的一行（有序列表的一项）：节点列（节点与连到下一项的线，画出来的）+ 内容；交替排布时三列，时间在对侧。
/// 节点 0.75em 的实心圆点、有图标时 1.5em 的圆（surface 底、sm 宽的色调描边），连接线 sm 宽的 borderMuted；节点列与内容隔 space.md，
/// 内容下方留 space.lg、各部分隔 space.xs；标题 medium，操作人与时间一行（sm、textMuted），收起的那几项的位置是一段虚线。
/// </summary>
public sealed class TimelineRowControl : Control
{
    readonly FlexControl content = new() { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly Control? opposite;
    readonly bool alternated;
    readonly bool end;
    readonly bool hasLine;

    /// <summary>一项：节点、标题、操作人与时间、说明、详情。</summary>
    internal TimelineRowControl(TimelineItem item, bool pending, bool last, bool alternated, bool end)
    {
        Init();
        Item = item;
        IsPending = pending;
        this.alternated = alternated;
        this.end = end;
        hasLine = !last;
        if (pending)
        {
            Spinner = new SpinnerControl { PropsIcon = new IconProps { Tone = TextTone.Accent } };
            Controls.Add(Spinner);
        }
        content.Controls.Add(Part(item.Title ?? "", title: true));
        var time = item.Time is { Length: > 0 } text ? TimeText(text, item.DateTime) : null;
        // 交替排布：时间在对侧，没有时间也占一格（保持三列对齐）
        if (alternated)
        {
            if (time is not null)
            {
                time.Align = end ? TextAlign.Start : TextAlign.End;
                opposite = time;
                Controls.Add(time);
            }
            Time = time;
            time = null;
        }
        if (item.Actor is not null)
        {
            var actor = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs };
            if (item.Avatar is not null || item.Actor is string)
                // 头像 1.75em（sm 字）：Avatar 按字号的 2.5 倍定边长，字号给 0.7 倍
                actor.Controls.Add(new AvatarControl
                {
                    Source = item.Avatar, Fallback = item.Actor is string name ? AvatarInitials.Of(name) : null,
                    TextSize = new TextSize((Tokens.FontSize.Sm * 1.75f / 2.5f).ToString(CultureInfo.InvariantCulture)),
                });
            actor.Controls.Add(item.Actor as Control ?? new TextControl { Text = item.Actor.ToString(), TextSize = TextSize.Sm });
            Meta = new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true };
            Meta.Controls.Add(actor);
            if (time is not null) Meta.Controls.Add(time);
            content.Controls.Add(Meta);
        }
        else if (time is not null) content.Controls.Add(time);
        Time ??= time;
        if (item.Description is not null) content.Controls.Add(Part(item.Description, title: false));
        if (item.Details is not null) content.Controls.Add(Details = DetailsPart(item.Details));
        // 交替排布时偶数项的内容在节点左侧、靠结束边对齐（TS 的 text-align: end）
        if (end)
        {
            content.Align = FlexAlign.End;
            foreach (var part in content.Controls.OfType<TextControlBase<Text>>()) part.Align = TextAlign.End;
        }
        Controls.Add(content);
    }

    /// <summary>收起的那几项所在的位置：节点列一段虚线，内容是展开、收起的按钮。</summary>
    internal TimelineRowControl(ButtonControl toggle)
    {
        Init();
        Toggle = toggle;
        content.Align = FlexAlign.Start;
        content.Controls.Add(toggle);
        Controls.Add(content);
    }

    void Init()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        // 自动大小：flex 容器只量自动大小的子控件，其余按当前大小
        AutoSize = true;
        AccessibleRole = AccessibleRole.ListItem;
    }

    /// <summary>这一行的项；展开按钮那一行为 null。</summary>
    public TimelineItem? Item { get; }

    /// <summary>是进行中的一项。</summary>
    public bool IsPending { get; }

    /// <summary>连到下一项的线（最后一项没有）。</summary>
    internal bool HasLine => hasLine;

    /// <summary>进行中的加载指示。</summary>
    internal SpinnerControl? Spinner { get; }

    /// <summary>时间。</summary>
    internal TextControlBase<Small>? Time { get; }

    /// <summary>操作人与时间那一行。</summary>
    internal FlexControl? Meta { get; }

    /// <summary>详情。</summary>
    internal CollapsibleControl? Details { get; }

    /// <summary>展开其余各项的按钮。</summary>
    internal ButtonControl? Toggle { get; }

    /// <summary>内容。</summary>
    internal FlexControl Content => content;

    /// <summary>放开使用方给的控件（标题、说明、操作人、详情）：重建时它们要放进新的一行，一个控件只能有一个父控件。</summary>
    internal void Detach()
    {
        if (Meta is not null)
        {
            foreach (var inner in Meta.Controls.OfType<FlexControl>().ToList()) inner.Controls.Clear();
            Meta.Controls.Clear();
        }
        if (Details is not null) Details.Content = null;
        content.Controls.Clear();
    }

    static Control Part(object value, bool title) =>
        value as Control ?? (title ? new TextControl { Text = value.ToString(), Weight = TextWeight.Medium } : new TextControl { Text = value.ToString() });

    static SmallControl TimeText(string text, DateTimeOffset? at) => new()
    {
        Text = text, Tone = TextTone.Muted,
        AccessibleDescription = at?.ToString("O", CultureInfo.InvariantCulture),
    };

    /// <summary>详情：一个像链接的"查看详情"按钮，点开才建内容。</summary>
    CollapsibleControl DetailsPart(object details)
    {
        var trigger = new ButtonControl { Variant = ButtonVariant.Link, TextSize = TextSize.Sm, IconEnd = Lucide.ChevronDown, Expanded = false };
        var collapsible = new CollapsibleControl { Trigger = trigger };
        collapsible.OpenChanged += (_, _) =>
        {
            trigger.IconEnd = collapsible.IsOpen ? Lucide.ChevronUp : Lucide.ChevronDown;
            trigger.Expanded = collapsible.IsOpen;
            // 打开时才建（TS 的 lazy）
            if (collapsible.IsOpen && collapsible.Content is null) collapsible.Content = details as Control ?? new TextControl { Text = details.ToString() };
        };
        trigger.Text = ProviderControl.LangOf(this).TimelineDetails;
        return collapsible;
    }

    float Em => this.EmPixels();

    /// <summary>节点的边长：有图标时 1.5em 的圆，圆点 0.75em，加载指示 1em。</summary>
    float MarkerSize => Toggle is not null ? 0 : IsPending ? Em : Item?.Icon is null ? 0.75f * Em : 1.5f * Em;

    /// <summary>节点上面的空：圆点 0.375em（对齐第一行文字的中线），加载指示 0.25em。</summary>
    float MarkerTop => IsPending ? 0.25f * Em : Item?.Icon is null && Toggle is null ? 0.375f * Em : 0;

    float RailWidth => Math.Max(1.5f * Em, MarkerSize);

    float Gap => Tokens.Space.Md * Em;

    /// <summary>三列（交替）时一侧的宽度；两列时内容的宽度。</summary>
    int ContentWidth(int width) => Math.Max(0, (int)(alternated ? (width - RailWidth - 2 * Gap) / 2 : width - RailWidth - Gap));

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        // 不限宽时按内容的自然宽度（节点列 + 间距 + 内容；交替时两侧一样宽）
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : NaturalWidth();
        var inner = content.GetPreferredSize(new Size(ContentWidth(width), 0)).Height;
        var side = opposite?.GetPreferredSize(new Size(ContentWidth(width), 0)).Height ?? 0;
        var height = Math.Max(Math.Max(inner, side), (int)Math.Ceiling(MarkerTop * 2 + MarkerSize));
        if (Toggle is not null) height = Math.Max(height, (int)Math.Ceiling(2 * Em));
        return new Size(width, height + (int)Math.Round(Tokens.Space.Lg * Em));
    }

    int NaturalWidth()
    {
        var side = Math.Max(content.GetPreferredSize(Size.Empty).Width, opposite?.GetPreferredSize(Size.Empty).Width ?? 0);
        return (int)Math.Ceiling(alternated ? 2 * side + RailWidth + 2 * Gap : side + RailWidth + Gap);
    }

    /// <summary>节点列在起始边（交替时在中间），内容在它旁边；交替时偶数项的内容在左、时间在右。从右往左排版时整行镜像。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var width = ContentWidth(Width);
        var railX = alternated ? width + Gap : 0;
        var contentX = alternated ? (end ? 0 : railX + RailWidth + Gap) : RailWidth + Gap;
        var oppositeX = end ? railX + RailWidth + Gap : 0;
        Place(content, contentX, 0, width, content.GetPreferredSize(new Size(width, 0)).Height);
        if (opposite is not null) Place(opposite, oppositeX, (int)Math.Round(0.125f * Em), width, opposite.GetPreferredSize(new Size(width, 0)).Height);
        if (Spinner is not null)
        {
            var size = Spinner.GetPreferredSize(Size.Empty);
            Place(Spinner, railX + (RailWidth - size.Width) / 2, MarkerTop, size.Width, size.Height);
        }
    }

    void Place(Control control, float x, float y, int width, int height)
    {
        var left = RightToLeft == RightToLeft.Yes ? Width - x - width : x;
        control.Bounds = new Rectangle((int)Math.Round(left), (int)Math.Round(y), width, height);
    }

    /// <summary>节点列：圆点或图标节点、连到这一行底边的线；展开按钮那一行是一段虚线。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var em = Em;
        var thin = Tokens.BorderWidth.Sm * DeviceDpi / 96f;
        var railX = alternated ? ContentWidth(Width) + Gap : 0;
        var center = railX + RailWidth / 2;
        if (RightToLeft == RightToLeft.Yes) center = Width - center;
        if (Toggle is not null)
        {
            using var dashed = new Pen(colors.BorderMuted, thin) { DashStyle = DashStyle.Dash };
            graphics.DrawLine(dashed, center, 0, center, Height);
            return;
        }
        var size = MarkerSize;
        var top = MarkerTop;
        if (hasLine)
        {
            using var line = new SolidBrush(colors.BorderMuted);
            var from = top * 2 + size;
            graphics.FillRectangle(line, center - thin / 2, from, thin, Math.Max(0, Height - from));
        }
        if (IsPending || Item is null) return;
        var tone = ToneColor(colors, Item.Tone);
        var marker = new RectangleF(center - size / 2, top, size, size);
        if (Item.Icon is not { } icon)
        {
            using var dot = new SolidBrush(tone);
            graphics.FillEllipse(dot, marker);
            return;
        }
        using (var surface = new SolidBrush(colors.Surface)) graphics.FillEllipse(surface, marker);
        using (var ring = new Pen(tone, thin)) graphics.DrawEllipse(ring, RectangleF.Inflate(marker, -thin / 2, -thin / 2));
        // 图标 1em，画在圆里正中
        var state = graphics.Save();
        graphics.TranslateTransform(center - em / 2, top + (size - em) / 2);
        graphics.ScaleTransform(em / IconData.Size, em / IconData.Size);
        using var path = icon.CreatePath();
        using var stroke = new Pen(tone, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawPath(stroke, path);
        graphics.Restore(state);
    }

    /// <summary>节点的色调：neutral 是 borderStrong，其余是各色调的文字色（accent 是实色）。</summary>
    static Color ToneColor(ThemeColors colors, StatusTone tone) => tone.Value switch
    {
        "accent" => colors.Accent,
        "info" => colors.InfoText,
        "success" => colors.SuccessText,
        "warning" => colors.WarningText,
        "danger" => colors.DangerText,
        _ => colors.BorderStrong,
    };

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new RowAccessibleObject(this);

    /// <summary>读屏：有序列表的一项；进行中的一项是忙的（TS 的 aria-busy）。</summary>
    sealed class RowAccessibleObject(TimelineRowControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.IsPending ? AccessibleStates.Busy : AccessibleStates.None);
    }
}

/// <summary>Timeline 的默认值（上游的 components.Timeline.defaults）：写在 ProviderControl 上，里面的时间线没写的档位取它。</summary>
public sealed record TimelineDefaults : IScopeRecord<TimelineDefaults>
{
    /// <summary>左右交替排布。</summary>
    public bool? Alternate { get; init; }

    /// <summary>字号档位。</summary>
    public TextSize? Size { get; init; }

    /// <inheritdoc />
    public TimelineDefaults Merge(TimelineDefaults? over) => over is null ? this : new()
    {
        Alternate = over.Alternate ?? Alternate,
        Size = over.Size ?? Size,
    };
}
