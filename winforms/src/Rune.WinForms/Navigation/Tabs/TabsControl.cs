using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs）：TabsControl（状态）里放 <see cref="TabListControl"/>（一排 <see cref="TabItemControl"/>）与各标签对应的 <see cref="TabPanelControl"/>。
/// <code>
/// var tabs = new TabsControl();
/// var list = new TabListControl { AccessibleName = "订单" };
/// list.Controls.Add(new TabItemControl { Value = "all", Text = "全部" });
/// tabs.Controls.AddRange([list, new TabPanelControl { Value = "all" }]);
/// </code>
/// 平台自带 TabControl / TabPage（系统画的选项卡），这里照上游拆成四层、外观照上游。没有选中值时选第一个标签；只显示当前标签的面板。
/// 键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；<see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，
/// manual 时按 Enter / 空格才切换。<see cref="Variant"/>：line（下划线）、soft（浅色底）；<see cref="Orientation"/>：horizontal、vertical。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("标签页：标签列表加对应的面板，只显示当前的。")]
public class TabsControl : FlexControl
{
    /// <summary>新建：竖排、间距 space.md。</summary>
    public TabsControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
        AutoSize = true;
    }

    /// <summary>当前标签变了。</summary>
    [Category("Action")]
    public event EventHandler? ValueChanged;

    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>方向，默认 horizontal。</summary>
    [Category("Appearance")]
    public TabsOrientation Orientation
    {
        get => field.Value is null ? TabsOrientation.Horizontal : field;
        set
        {
            field = value;
            var vertical = value == TabsOrientation.Vertical;
            Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
            Align = vertical ? FlexAlign.Start : default;
            Sync();
        }
    }

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    [Category("Behavior")]
    public TabsActivation Activation { get => field.Value is null ? TabsActivation.Automatic : field; set => field = value; }

    /// <summary>样式，默认 line。</summary>
    [Category("Appearance")]
    public TabsVariant Variant
    {
        get => field.Value is null ? TabsVariant.Line : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>字号档位；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FontFamily.Name, pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    bool ShouldSerializeOrientation() => Orientation != TabsOrientation.Horizontal;

    bool ShouldSerializeActivation() => Activation != TabsActivation.Automatic;

    bool ShouldSerializeVariant() => Variant != TabsVariant.Line;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    [Browsable(false)]
    public string? SelectedValue => Value ?? Members<TabItemControl>().FirstOrDefault()?.Value;

    /// <summary>属于这组的成员（不进到里面另一组 TabsControl）。</summary>
    internal IEnumerable<T> Members<T>() where T : Control => Descendants(this).OfType<T>();

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            if (child is TabsControl) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>元素属于哪组 TabsControl（父链上最近的一个）。</summary>
    internal static TabsControl? Owner(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent is TabsControl tabs) return tabs;
        return null;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        Sync();
    }

    /// <summary>选中状态、外观交给标签、列表与面板。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Members<TabItemControl>()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabListControl>()) list.Apply(this);
        foreach (var panel in Members<TabPanelControl>())
        {
            panel.Visible = panel.Value == selected;
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容
            SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
        PerformLayout();
    }
}

/// <summary>
/// 一排标签：line 时底边（竖排时结束边）一道 borderMuted 的线、当前标签那一段 2 像素的品牌色指示条（切换时滑过去）；soft 时 fill 底、xs 内边距、md 圆角。
/// 方向键在标签之间移动，到两端后从另一端继续（<see cref="Loop"/>）。名称写 AccessibleName。
/// </summary>
[ToolboxItem(true)]
[Description("一排标签：方向键在标签之间移动。")]
public class TabListControl : FlexControl
{
    readonly Signal<double> indicatorStart = new(0);
    readonly Signal<double> indicatorLength = new(0);
    readonly Effect paint;
    TabsControl? owner;
    TabItemControl? current;
    bool placed;

    /// <summary>新建：横排、拉伸对齐。</summary>
    public TabListControl()
    {
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PageTabList;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        paint = new Effect(Invalidate);
    }

    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Loop { get; set; } = true;

    bool IsVertical => owner?.Orientation == TabsOrientation.Vertical;

    bool IsSoft => owner?.Variant == TabsVariant.Soft;

    /// <summary>跟着 TabsControl 的样式与方向：间距、soft 的内边距与宽度。</summary>
    internal void Apply(TabsControl tabs)
    {
        owner = tabs;
        var vertical = IsVertical;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = !IsSoft && !vertical ? Space.Md : Space.Xs;
        var inset = IsSoft ? (int)MathF.Round(0.25f * Font.SizeInPoints * DeviceDpi / 72f) : 0;
        Padding = new Padding(inset);
        SyncTabStops();
        Invalidate();
    }

    /// <summary>标签：可见的。</summary>
    List<TabItemControl> Items() => [.. Controls.OfType<TabItemControl>().Where(tab => tab.Visible)];

    /// <summary>整组一个 Tab 停靠点：当前聚焦过的，没有时选中的，再没有时第一个可用的。</summary>
    void SyncTabStops()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current
            : items.FirstOrDefault(tab => tab.IsSelected && tab.Enabled) ?? items.FirstOrDefault(tab => tab.Enabled);
        foreach (var item in items) item.TabStop = item == stop;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control!.GotFocus += OnItemFocused;
        TabsControl.Owner(this)?.Sync();
    }

    void OnItemFocused(object? sender, EventArgs e)
    {
        current = sender as TabItemControl;
        SyncTabStops();
    }

    /// <summary>方向键、Home、End 在标签之间移动焦点（不可用的跳过）；自动切换时切到新聚焦的标签。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        var items = Items().Where(tab => tab.Enabled).ToList();
        var index = items.FindIndex(item => item.Focused);
        if (index < 0) return base.ProcessDialogKey(keyData);
        var rtl = RightToLeft == RightToLeft.Yes;
        int? target = keyData switch
        {
            Keys.Right when !IsVertical => rtl ? index - 1 : index + 1,
            Keys.Left when !IsVertical => rtl ? index + 1 : index - 1,
            Keys.Down when IsVertical => index + 1,
            Keys.Up when IsVertical => index - 1,
            Keys.Home => 0,
            Keys.End => items.Count - 1,
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => index,
            _ => null,
        };
        if (target is not { } next) return base.ProcessDialogKey(keyData);
        if (next == index) return true;
        if (next < 0 || next >= items.Count)
        {
            if (!Loop) return true;
            next = (next + items.Count) % items.Count;
        }
        current = items[next];
        SyncTabStops();
        current.Focus();
        if (owner?.Activation == TabsActivation.Automatic && current.Value is { } value) owner.Value = value;
        return true;
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        PlaceIndicator();
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（Animator，durations.md、standard）。</summary>
    internal void PlaceIndicator()
    {
        if (owner is null || IsSoft || Items().FirstOrDefault(tab => tab.IsSelected) is not { } selected) return;
        var (start, length) = IsVertical ? (selected.Top, selected.Height) : (selected.Left, selected.Width);
        var duration = placed ? Tokens.Duration.Md : TimeSpan.Zero;
        placed = true;
        Animator.Animate(indicatorStart, start, duration, Easing.Standard);
        Animator.Animate(indicatorLength, length, duration, Easing.Standard);
    }

    /// <summary>soft：fill 底、md 圆角。line：分隔线与 2 像素的品牌色指示条。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        if (owner is null) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (IsSoft)
        {
            // 底色只在标签后面（列表本身被拉满一行，上游的列表按内容宽）
            var items = Items();
            if (items.Count == 0) return;
            var right = items.Max(tab => tab.Right) + Padding.Right;
            var bottom = items.Max(tab => tab.Bottom) + Padding.Bottom;
            var radius = ProviderControl.ThemeOf(this).Radii.Md * Font.SizeInPoints * DeviceDpi / 72f;
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, IsVertical ? Width - 0.5f : right - 0.5f, bottom - 0.5f), radius);
            using var fill = new SolidBrush(colors.Fill);
            graphics.FillPath(fill, path);
            return;
        }
        var line = LogicalToDeviceUnits(1);
        var bar = LogicalToDeviceUnits(2);
        using (var divider = new SolidBrush(colors.BorderMuted))
            graphics.FillRectangle(divider, IsVertical ? new Rectangle(Width - line, 0, line, Height) : new Rectangle(0, Height - line, Width, line));
        var start = (float)indicatorStart.Value;
        var length = (float)indicatorLength.Value;
        if (length <= 0) return;
        using var accent = new SolidBrush(colors.Accent);
        graphics.FillRectangle(accent, IsVertical ? new RectangleF(Width - bar, start, bar, length) : new RectangleF(start, Height - bar, length, bar));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Animator.Stop(indicatorStart);
            Animator.Stop(indicatorLength);
            paint.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// 标签：里面放文字（<see cref="Control.Text"/>）或子控件（文字加计数），中等字重（GDI 只有常规与粗体，按常规画）；平时 textMuted、悬停与选中时 text；
/// 控件高度、左右 space.md、sm 圆角；soft 时选中的标签 surface 底；键盘聚焦时里侧焦点环。点击、Enter、空格切换（<see cref="TabClick"/> 先执行，
/// <see cref="TabClickEventArgs.Cancel"/> 拦下）。读屏里是选项卡。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TabClick))]
[Description("标签：点击切换到对应的面板。")]
public class TabItemControl : FlexControl
{
    TabsControl? owner;
    bool hovered;

    /// <summary>新建：内容居中、间距 space.xs。</summary>
    public TabItemControl()
    {
        Align = FlexAlign.Center;
        Justify = FlexJustify.Center;
        Gap = Space.Xs;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PageTab;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        // 没选中时透出列表（soft 的 fill 底）
        BackColor = Color.Transparent;
    }

    /// <summary>点击了标签（切换之前；设 Cancel 拦下切换）。</summary>
    [Category("Action")]
    public event EventHandler<TabClickEventArgs>? TabClick;

    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get; set; }

    /// <summary>选中着。</summary>
    [Browsable(false)]
    public bool IsSelected { get; private set; }

    /// <summary>跟着 TabsControl 的选中值与样式。</summary>
    internal void Apply(TabsControl tabs, bool selected)
    {
        owner = tabs;
        IsSelected = selected;
        var em = Font.SizeInPoints * DeviceDpi / 72f;
        var inline = (int)MathF.Round(0.75f * em);
        var line = tabs.Variant != TabsVariant.Soft;
        var vertical = tabs.Orientation == TabsOrientation.Vertical;
        // line：底边（竖排时结束边）留 2 像素给指示条
        var bar = line ? LogicalToDeviceUnits(2) : 0;
        Padding = vertical ? new Padding(inline, 0, inline + bar, 0) : new Padding(inline, 0, inline, bar);
        MinimumSize = new Size(0, (int)MathF.Ceiling(ControlHeight.Of(ElementContext.Of(this), em, Tokens.Size.Control)));
        Justify = vertical ? FlexJustify.Start : FlexJustify.Center;
        ForeColor = Color.Empty;
        Invalidate();
    }

    /// <summary>不小于控件高度（FlexControl 自己算想要的大小，平台的最小尺寸要再并上）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    /// <inheritdoc />
    public override Color ForeColor
    {
        get
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            return IsSelected || hovered ? colors.Text : colors.TextMuted;
        }
        set => base.ForeColor = value;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        // 里面的文字、计数也算标签的一部分：点它们同点标签
        e.Control!.MouseClick += (_, args) => OnMouseClick(args);
        e.Control.MouseEnter += (_, _) => SetHovered(true);
        e.Control.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
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

    /// <summary>切到这个标签：先发 <see cref="TabClick"/>（设 Cancel 拦下），不可用时不切。</summary>
    public void Activate()
    {
        if (!Enabled || Value is not { } value) return;
        var args = new TabClickEventArgs();
        TabClick?.Invoke(this, args);
        if (args.Cancel) return;
        if (owner is not null) owner.Value = value;
    }

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

    /// <summary>soft 的选中底色（surface）、键盘聚焦时的焦点环；不可用时半透明由文字颜色体现。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = theme.Radii.Sm * Font.SizeInPoints * DeviceDpi / 72f;
        if (owner?.Variant == TabsVariant.Soft && IsSelected)
        {
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), radius);
            using var fill = new SolidBrush(theme.Colors.Surface);
            graphics.FillPath(fill, path);
        }
        if (Focused && ShowFocusCues)
        {
            var width = LogicalToDeviceUnits(2);
            using var path = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), radius);
            using var pen = new Pen(theme.Colors.FocusRing, width);
            graphics.DrawPath(pen, path);
        }
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TabAccessibleObject(this);

    sealed class TabAccessibleObject(TabItemControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name
            : owner.Text is { Length: > 0 } text ? text : string.Join(" ", owner.Controls.Cast<Control>().Select(child => child.Text).Where(text => text.Length > 0));

        public override AccessibleStates State => base.State | (owner.IsSelected ? AccessibleStates.Selected : 0) | AccessibleStates.Selectable;

        public override string DefaultAction => "切换";

        public override void DoDefaultAction() => owner.Activate();
    }
}

/// <summary>点击标签的参数：设 <see cref="Cancel"/> 拦下切换（TS 的 preventDefault）。</summary>
public sealed class TabClickEventArgs : EventArgs
{
    /// <summary>拦下这次切换。</summary>
    public bool Cancel { get; set; }
}

/// <summary>标签面板：只显示当前标签的那一个；读屏名称是对应标签的文字。</summary>
[ToolboxItem(true)]
[Description("标签面板：对应标签选中时显示。")]
public class TabPanelControl : FlexControl
{
    /// <summary>新建：竖排。</summary>
    public TabPanelControl()
    {
        Direction = FlexDirection.Column;
        AutoSize = true;
        AccessibleRole = AccessibleRole.PropertyPage;
    }

    /// <summary>对应标签的值。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value { get; set; }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new PanelAccessibleObject(this);

    sealed class PanelAccessibleObject(TabPanelControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name
            : TabsControl.Owner(owner)?.Members<TabItemControl>().FirstOrDefault(tab => tab.Value == owner.Value)?.AccessibilityObject.Name;
    }
}
