using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 侧边导航（vue-tsx-pro 的 NavList）：里面放 <see cref="NavItemControl"/>（链接）、<see cref="NavGroupControl"/>（可展开的一组）、<see cref="NavSectionControl"/>（带小标题的一组）。
/// <code>
/// var nav = new NavListControl { AccessibleName = "后台", CurrentHref = "/" };
/// var orders = new NavGroupControl { Label = "订单", Icon = Lucide.Package };
/// orders.Items.Add(new NavItemControl { Href = "/orders", Text = "全部订单" });
/// nav.Controls.AddRange([new NavItemControl { Href = "/", Icon = Lucide.House, Text = "概览" }, orders]);
/// </code>
/// 桌面上没有路由：<see cref="CurrentHref"/> 是当前地址（TS 路由适配的 isActive 按它判断，地址相同的项是当前页），
/// 点击导航项先发 <see cref="Navigate"/>（TS 交给路由的 navigate），没有被处理就把 CurrentHref 改成那一项的地址。
/// 当前页高亮（accentSoft 底、accentText），读屏里是选中的链接；含当前页的分组自动展开。
/// <see cref="Compact"/>：各项只剩图标（名称留给读屏，悬停、聚焦时用提示显示），小节标题只剩它的图标。
/// 没做：紧凑时分组悬停弹出子项（等 HoverCard；现在紧凑的分组照常展开，子项也只剩图标）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Navigate))]
[Description("侧边导航：导航项、可展开的分组与带小标题的小节。")]
public class NavListControl : FlexControl
{
    readonly TooltipProvider tips = new() { Delay = 200 };

    /// <summary>新建：竖排、间距 space.xs。</summary>
    public NavListControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>点了导航项（切换之前）：设 Handled 时不改 CurrentHref（TS 的路由接管跳转）。</summary>
    [Category("Action")]
    public event EventHandler<NavigateEventArgs>? Navigate;

    /// <summary>当前地址变了。</summary>
    [Category("Property Changed")]
    public event EventHandler? CurrentHrefChanged;

    /// <summary>当前地址：Href 与它相同的导航项是当前页。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? CurrentHref
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            CurrentHrefChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>紧凑：只显示图标。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Compact
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>所在区域要求紧凑（TS 的 NavCompactContext）：AppShellControl 把侧栏收成图标栏时设。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ScopeCompact
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
        }
    }

    /// <summary>实际是否紧凑：自己写了 <see cref="Compact"/>，或所在区域要求紧凑。</summary>
    internal bool IsCompact => Compact || ScopeCompact;

    /// <summary>只剩图标的项用的提示。</summary>
    internal TooltipProvider Tips => tips;

    /// <summary>属于这个导航的成员（不进到里面另一个 NavListControl）。</summary>
    IEnumerable<T> Members<T>(Control? within = null) => Descendants(within ?? this).Prepend(within ?? this).OfType<T>();

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            if (child is NavListControl) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>控件属于哪个 NavListControl（父链上最近的一个）。</summary>
    internal static NavListControl? Owner(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent is NavListControl list) return list;
        return null;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        // 只给新加的这一支套状态：一组组往里加时每次都整棵重套，一百多项的侧栏是平方级
        Sync(e.Control);
    }

    /// <summary>内边距、控件高度、缩进都按字号：字体（从窗体继承来的也算）或 DPI 变了重新算。</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Sync();
    }

    /// <summary>当前页、紧凑交给各项（within 给出时只管这一支）；含当前页的分组展开。</summary>
    internal void Sync(Control? within = null)
    {
        // 套状态时每一行都会让所在的容器重排：先把列表与各组挂起，最后只排一次（不然 n 行就是 n 次整个列表的测量，几百页的侧栏要卡几十秒）
        Control[] containers = [this, .. Members<NavSectionControl>(within), .. Members<NavGroupControl>(within)];
        foreach (var container in containers) container.SuspendLayout();
        foreach (var row in Members<NavRowControl>(within)) row.Apply(this);
        foreach (var section in Members<NavSectionControl>(within)) section.Apply(this);
        foreach (var group in Members<NavGroupControl>(within)) group.Apply(this);
        foreach (var container in containers) container.ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>点了导航项：先发 Navigate，没有被处理就切过去。</summary>
    internal void Go(NavItemControl item)
    {
        var args = new NavigateEventArgs(item.Href);
        Navigate?.Invoke(this, args);
        if (!args.Handled && item.Href is { } href) CurrentHref = href;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>点了导航项的参数：要去的地址；设 <see cref="Handled"/> 时 NavListControl 不改当前地址。</summary>
public sealed class NavigateEventArgs(string? href) : EventArgs
{
    /// <summary>要去的地址。</summary>
    public string? Href { get; } = href;

    /// <summary>路由接管了跳转：不改当前地址。</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// 导航的一行（导航项与分组标题共用，TS 的 itemStyles.row 加 navListStyles.row）：图标、文字、尾部内容横排，控件高度、上下 space.xs、左右 space.md、md 圆角，
/// 中等字重（GDI 按常规画），平时 textMuted，悬停 text 与 fillHover 底；当前页 accentSoft 底、accentText；键盘聚焦时里侧焦点环。
/// 紧凑时只剩图标、居中，名称在悬停与聚焦的提示里。
/// </summary>
public abstract class NavRowControl : FlexControl
{
    readonly IndicatorControl indicator = new() { AutoSize = true, Visible = false };
    readonly IconControl icon = new();
    readonly TextControl label = new() { Truncate = true };
    readonly BadgeControl badge = new() { Variant = StatusVariant.Solid, Radius = Radius.Full, TextSize = TextSize.Xs, Visible = false };
    private protected readonly IconControl Chevron = new() { Data = Lucide.ChevronRight, Visible = false };
    Control? end;
    bool hovered;

    /// <summary>新建：图标、文字、计数角标、箭头横排，竖直居中。</summary>
    protected NavRowControl()
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        indicator.Controls.Add(icon);
        Controls.AddRange([indicator, label, badge, Chevron]);
        SetGrow(label, true);
        foreach (Control child in Controls) Forward(child);
    }

    /// <summary>文字前的图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon
    {
        get;
        set
        {
            field = value;
            icon.Data = value;
            indicator.Visible = value is not null;
            Apply(NavListControl.Owner(this));
        }
    }

    /// <summary>文字后面的内容（计数、标记）。</summary>
    [Browsable(false)]
    [DefaultValue(null)]
    public Control? End
    {
        get => end;
        set
        {
            if (end is not null) Controls.Remove(end);
            end = value;
            if (value is null) return;
            Controls.Add(value);
            Controls.SetChildIndex(value, Controls.GetChildIndex(badge));
            Forward(value);
        }
    }

    /// <summary>是不是当前页（分组标题：组里有当前页）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCurrent { get; protected set; }

    /// <summary>只剩图标（紧凑且有图标）。</summary>
    [Browsable(false)]
    public bool IsIconOnly { get; private set; }

    /// <summary>文字。</summary>
    [AllowNull]
    public override string Text
    {
        get => label.Text;
        set
        {
            label.Text = value ?? "";
            base.Text = value;
        }
    }

    /// <summary>计数角标的数目与色调（导航项的 Badge；0 不显示）。</summary>
    private protected virtual (int Count, StatusTone Tone) Count => (0, StatusTone.Danger);

    /// <summary>平时 textMuted，悬停 text，当前页 accentText，不可用 textDisabled；里面的文字、图标跟着它。</summary>
    public override Color ForeColor
    {
        get
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            return !Enabled ? colors.TextDisabled : IsCurrent ? colors.AccentText : hovered ? colors.Text : colors.TextMuted;
        }
        set => base.ForeColor = value;
    }

    float Em => this.EmPixels();

    /// <summary>子控件也算这一行：点它们同点这一行，指针在它们上面也算悬停。</summary>
    void Forward(Control child)
    {
        child.MouseClick += (_, args) => OnMouseClick(args);
        child.MouseEnter += (_, _) => SetHovered(true);
        child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        foreach (Control grandchild in child.Controls) Forward(grandchild);
    }

    /// <summary>跟着 NavListControl：紧凑时只剩图标；内边距、控件高度按字号。</summary>
    internal virtual void Apply(NavListControl? list)
    {
        IsIconOnly = list?.IsCompact == true && Icon is not null;
        var em = Em;
        var (inline, block) = ((int)MathF.Round(0.75f * em), (int)MathF.Round(0.25f * em));
        Padding = IsIconOnly ? new Padding(0, block, 0, block) : new Padding(inline, block, inline, block);
        Justify = IsIconOnly ? FlexJustify.Center : FlexJustify.Start;
        MinimumSize = new Size(0, (int)MathF.Ceiling(ControlHeight.Of(ElementContext.Of(this), em, Tokens.Size.Control)));
        label.Visible = !IsIconOnly;
        if (end is not null) end.Visible = !IsIconOnly;
        Update(list);
    }

    /// <summary>计数角标与提示。</summary>
    private protected void Update(NavListControl? list)
    {
        var (count, tone) = Count;
        badge.Visible = !IsIconOnly && count > 0;
        badge.Text = count > 99 ? "99+" : count.ToString();
        badge.Tone = tone;
        // 紧凑：图标右上角一个小圆点（图标太小，叠不下数字），读屏照样念出数目
        indicator.Dot = IsIconOnly && count > 0;
        indicator.Tone = tone;
        indicator.Label = count > 0 ? ProviderControl.LangOf(this).IndicatorCount(count) : null;
        // 只剩图标：名称在提示里，悬停 200 毫秒、聚焦时显示在右边
        if (list is not null)
        {
            list.Tips.SetTooltip(this, IsIconOnly ? Text : null);
            list.Tips.SetTooltipPlacement(this, FloatingPlacement.Right);
        }
        OnForeColorChanged(EventArgs.Empty);
        Invalidate(true);
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        OnForeColorChanged(EventArgs.Empty);
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHovered(true);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Activate();
    }

    /// <summary>点了这一行（导航项去那一页，分组标题展开收起）。</summary>
    public abstract void Activate();

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        Activate();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        OnForeColorChanged(EventArgs.Empty);
    }

    /// <summary>底色（悬停 fillHover；当前页 accentSoft，悬停 accentSoftHover）、md 圆角、键盘聚焦时的焦点环。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var radius = theme.Radii.Md * Em;
        Color? fill = !Enabled ? null : IsCurrent ? hovered ? theme.Colors.AccentSoftHover : theme.Colors.AccentSoft : hovered ? theme.Colors.FillHover : null;
        if (fill is { } color)
        {
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
        if (Focused && ShowFocusCues)
        {
            var width = LogicalToDeviceUnits(2);
            using var path = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), radius);
            using var pen = new Pen(theme.Colors.FocusRing, width);
            graphics.DrawPath(pen, path);
        }
    }
}

/// <summary>
/// 导航项（TS 的 NavItem）：<see cref="Href"/> 等于 NavListControl 的 CurrentHref 时是当前页（也可以用 <see cref="Active"/> 指定）。
/// 点击先发 Click，再交给 NavListControl 切换。<see cref="Badge"/> 是计数角标：展开时是文字后面的实心徽标，紧凑时是图标上的小圆点。
/// 不可用（Enabled=false）时不能点、不能聚焦。读屏里是链接，当前页是选中的。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[Description("导航项：点了去对应的页面；当前页高亮。")]
public class NavItemControl : NavRowControl
{
    /// <summary>新建：读屏里是链接。</summary>
    public NavItemControl() => AccessibleRole = AccessibleRole.Link;

    /// <summary>地址。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Href
    {
        get;
        set
        {
            field = value;
            NavListControl.Owner(this)?.Sync();
        }
    }

    /// <summary>是否是当前页；不写时按 NavListControl 的 CurrentHref 判断。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? Active
    {
        get;
        set
        {
            field = value;
            NavListControl.Owner(this)?.Sync();
        }
    }

    /// <summary>计数角标（待办、未读）；0 不显示。</summary>
    [Category("Appearance")]
    [DefaultValue(0)]
    public int Badge
    {
        get;
        set
        {
            field = value;
            Update(NavListControl.Owner(this));
        }
    }

    /// <summary>角标的色调，默认 danger。</summary>
    [Category("Appearance")]
    public StatusTone BadgeTone
    {
        get => field.Value is null ? StatusTone.Danger : field;
        set
        {
            field = value;
            Update(NavListControl.Owner(this));
        }
    }

    bool ShouldSerializeBadgeTone() => BadgeTone != StatusTone.Danger;

    private protected override (int Count, StatusTone Tone) Count => (Badge, BadgeTone);

    internal override void Apply(NavListControl? list)
    {
        IsCurrent = Active ?? (Href is not null && Href == list?.CurrentHref);
        base.Apply(list);
    }

    /// <summary>去这一项的地址（交给 NavListControl）；不可用时不去。</summary>
    public override void Activate()
    {
        if (!Enabled) return;
        NavListControl.Owner(this)?.Go(this);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ItemAccessibleObject(this);

    sealed class ItemAccessibleObject(NavItemControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : owner.Text;

        public override AccessibleStates State => base.State | (owner.IsCurrent ? AccessibleStates.Selected : 0) | AccessibleStates.Linked;

        public override string DefaultAction => "跳转";

        public override void DoDefaultAction() => owner.Activate();
    }
}

/// <summary>
/// 可展开的导航分组（TS 的 NavGroup）：标题行（图标、文字、朝行尾的箭头，展开时朝下）加收起的子列表（左边缩进 space.lg）。
/// 默认收起；组里有当前页时自动展开（离开时不自动收起）。组里的导航项加到 <see cref="Items"/>。
/// </summary>
[ToolboxItem(true)]
[Description("可展开的导航分组：标题行加收起的子列表。")]
public class NavGroupControl : FlexControl
{
    readonly NavGroupHeader header;
    readonly CollapseControl collapse = new();
    readonly FlexControl list = new() { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true };

    /// <summary>新建：标题行与收起的子列表。</summary>
    public NavGroupControl()
    {
        Direction = FlexDirection.Column;
        AutoSize = true;
        header = new NavGroupHeader(this);
        collapse.Controls.Add(list);
        Controls.AddRange([header, collapse]);
        list.ControlAdded += (_, e) => NavListControl.Owner(this)?.Sync(e.Control);
    }

    /// <summary>展开、收起了。</summary>
    [Category("Property Changed")]
    public event EventHandler? IsOpenChanged;

    /// <summary>组里的导航项。</summary>
    [Browsable(false)]
    public ControlCollection Items => list.Controls;

    /// <summary>分组标题。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Label { get => header.Text; set => header.Text = value; }

    /// <summary>标题前的图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon { get => header.Icon; set => header.Icon = value; }

    /// <summary>是否展开；含当前页时自动展开。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsOpen
    {
        get => collapse.IsOpen;
        set
        {
            if (collapse.IsOpen == value) return;
            collapse.IsOpen = value;
            header.Turn(value);
            IsOpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>标题行（测试用）。</summary>
    internal NavRowControl Header => header;

    /// <summary>组里有当前页：标题行高亮（紧凑时）并展开；子列表左边缩进 space.lg、上面 space.xs，紧凑时不缩进。</summary>
    internal void Apply(NavListControl owner)
    {
        var current = list.Controls.OfType<NavItemControl>().Any(item => item.IsCurrent);
        header.SetCurrent(current && owner.IsCompact);
        if (current) IsOpen = true;
        var em = this.EmPixels();
        list.Padding = new Padding(owner.IsCompact ? 0 : (int)MathF.Round(Tokens.Space.Lg * em), (int)MathF.Round(Tokens.Space.Xs * em), 0, 0);
    }

    /// <summary>分组的标题行：点了展开、收起；尾部是箭头（展开时转到朝下）。读屏里是可展开的按钮。</summary>
    sealed class NavGroupHeader : NavRowControl
    {
        readonly NavGroupControl owner;

        public NavGroupHeader(NavGroupControl group)
        {
            owner = group;
            AccessibleRole = AccessibleRole.PushButton;
        }

        internal void SetCurrent(bool value)
        {
            IsCurrent = value;
            Update(NavListControl.Owner(this));
        }

        internal override void Apply(NavListControl? list)
        {
            base.Apply(list);
            Chevron.Visible = !IsIconOnly;
        }

        /// <summary>箭头：朝行尾，展开时朝下（GDI 的图标不转动，换图形）。</summary>
        internal void Turn(bool open) => Chevron.Data = open ? Lucide.ChevronDown : RightToLeft == RightToLeft.Yes ? Lucide.ChevronLeft : Lucide.ChevronRight;

        public override void Activate()
        {
            if (Enabled) owner.IsOpen = !owner.IsOpen;
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessibleObject(this);

        sealed class HeaderAccessibleObject(NavGroupHeader header) : ControlAccessibleObject(header)
        {
            public override AccessibleStates State => base.State | (header.owner.IsOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed);

            public override string DefaultAction => header.owner.IsOpen ? "收起" : "展开";

            public override void DoDefaultAction() => header.Activate();
        }
    }
}

/// <summary>
/// 带小标题的一组导航链接（TS 的 NavSection）：一直展开、不能收起。标题行与链接行同一套内边距（图标、文字上下对齐），文字 sm、中等字重（GDI 按常规画）、
/// text 颜色，图标 textMuted；小节之间留 space.lg。标题是这组链接的读屏名称。紧凑时标题只剩图标，上面一条 borderMuted 的细线。小节里的导航项加到 <see cref="Items"/>。
/// </summary>
[ToolboxItem(true)]
[Description("带小标题的一组导航链接：一直展开。")]
public class NavSectionControl : FlexControl
{
    readonly SectionLabel labelRow = new();
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl text = new() { TextSize = TextSize.Sm };
    readonly FlexControl list = new() { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true, AccessibleRole = AccessibleRole.List };

    /// <summary>新建：标题行与链接列表。</summary>
    public NavSectionControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        AutoSize = true;
        labelRow.Controls.AddRange([icon, text]);
        Controls.AddRange([labelRow, list]);
        list.ControlAdded += (_, e) => NavListControl.Owner(this)?.Sync(e.Control);
    }

    /// <summary>小节里的导航项。</summary>
    [Browsable(false)]
    public ControlCollection Items => list.Controls;

    /// <summary>小节标题，同时是这组链接的读屏名称。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    public string Label
    {
        get => text.Text;
        set
        {
            text.Text = value;
            list.AccessibleName = value;
        }
    }

    /// <summary>标题前的图标：只是标记、不能点；紧凑时标题只剩它。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon
    {
        get => icon.Data;
        set
        {
            icon.Data = value;
            icon.Visible = value is not null;
        }
    }

    /// <summary>链接列表（测试用）。</summary>
    internal FlexControl List => list;

    /// <summary>紧凑时只剩图标、居中，上面一条细线；第一节之外上面留 space.lg。</summary>
    internal void Apply(NavListControl owner)
    {
        var iconOnly = owner.IsCompact && Icon is not null;
        text.Visible = !iconOnly;
        labelRow.Justify = iconOnly ? FlexJustify.Center : FlexJustify.Start;
        labelRow.Divider = iconOnly;
        var em = this.EmPixels();
        var (inline, block) = ((int)MathF.Round(0.75f * em), (int)MathF.Round(0.25f * em));
        // 与链接行同一套内边距（上下 space.xs、左右 space.md）；紧凑时上面多留到 space.sm，再加细线
        labelRow.Padding = iconOnly ? new Padding(0, (int)MathF.Round(0.5f * em) + LogicalToDeviceUnits(1), 0, block) : new Padding(inline, block, inline, block);
        // 文字的行高按 normal（sm 的 1.5 倍）算，GDI 的行距更小
        labelRow.MinimumSize = new Size(0, iconOnly ? 0 : (int)MathF.Round(1.5f * Tokens.FontSize.Sm * DeviceDpi / 96f) + 2 * block);
        var first = Parent?.Controls.Cast<Control>().FirstOrDefault(child => child.Visible) == this;
        Padding = new Padding(0, first ? 0 : (int)MathF.Round(Tokens.Space.Lg * em), 0, 0);
    }

    /// <summary>标题行：紧凑时上面一条 borderMuted 的细线。</summary>
    sealed class SectionLabel : FlexControl
    {
        public SectionLabel()
        {
            Align = FlexAlign.Center;
            Gap = Space.Sm;
            AutoSize = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Divider
        {
            get;
            set
            {
                field = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Divider) return;
            using var line = new SolidBrush(ProviderControl.ThemeOf(this).Colors.BorderMuted);
            e.Graphics.FillRectangle(line, 0, 0, Width, LogicalToDeviceUnits(1));
        }
    }
}
