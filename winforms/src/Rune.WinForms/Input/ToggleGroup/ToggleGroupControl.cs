using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 分段控件（vue-tsx-pro 的 ToggleGroup）：一组切换按钮 <see cref="ToggleItemControl"/>，单选像分段控件（视图切换、时间范围），多选像格式按钮（加粗、斜体）。
/// <code>
/// var view = new ToggleGroupControl { Value = "list", IsRequired = true, AccessibleName = "视图" };
/// view.Controls.AddRange([new ToggleItemControl { Value = "list", Icon = Lucide.List, Text = "列表" }, new ToggleItemControl { Value = "grid", Text = "卡片" }]);
/// </code>
/// 单选时 <see cref="Value"/> 是选中项的值，选中项下面一块 accentSoft 底、accentBorder 描边的滑块（切换时滑过去）；
/// 多选（<see cref="SelectionMode"/>=multiple）时 <see cref="Values"/> 是选中项的值的列表，每项各自开关（选中的是 surface 外观）。
/// <see cref="IsRequired"/> 时至少保留一项。外观：fill 底、md 圆角、四周与项之间 space.xs。整组一个 Tab 停靠点（选中项），方向键在组内移动；
/// 在工具栏里时焦点交给工具栏。放进 FieldControl 时以标签为名称，沿用字段的必填、只读。读屏里每项是按下或弹起的按钮。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("分段控件：一组切换按钮，单选或多选。")]
public class ToggleGroupControl : FlexControl, IFieldControl
{
    readonly Signal<double> left = new(0);
    readonly Signal<double> top = new(0);
    readonly Signal<double> width = new(0);
    readonly Signal<double> height = new(0);
    readonly Effect paint;
    Control? current;
    bool placed;

    /// <summary>新建：横排、间距 space.xs，四周 space.xs。</summary>
    public ToggleGroupControl()
    {
        Gap = Space.Xs;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        paint = new Effect(Invalidate);
    }

    /// <summary>单选的值变了。</summary>
    [Category("Property Changed")]
    public event EventHandler? ValueChanged;

    /// <summary>多选的值变了。</summary>
    [Category("Property Changed")]
    public event EventHandler? ValuesChanged;

    /// <summary>选择方式，默认 single。</summary>
    [Category("Behavior")]
    public SelectionMode SelectionMode
    {
        get => field.Value is null ? SelectionMode.Single : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>单选：选中项的值；没有选中时为空。</summary>
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

    /// <summary>多选：选中项的值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string>? Values
    {
        get;
        set
        {
            field = value;
            Sync();
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>必须有选中项：最后一项不能取消；在必填的 FieldControl 里默认开启。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsRequired { get; set; }

    /// <summary>整组只读：选中项照常显示、按钮能聚焦，点了不切换。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>方向，默认 horizontal。</summary>
    [Category("Appearance")]
    public ToggleGroupOrientation Orientation
    {
        get => field.Value is null ? ToggleGroupOrientation.Horizontal : field;
        set
        {
            field = value;
            Direction = value == ToggleGroupOrientation.Vertical ? FlexDirection.Column : FlexDirection.Row;
            Sync();
        }
    }

    /// <summary>字号档位，按钮随之缩放；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>按下时的波纹；不写时沿用全局配置。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? Ripple
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    bool ShouldSerializeSelectionMode() => SelectionMode != SelectionMode.Single;

    bool ShouldSerializeOrientation() => Orientation != ToggleGroupOrientation.Horizontal;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    /// <summary>单选：选中底色由滑块画，项本身不画。</summary>
    internal bool Sliding => !Multiple;

    /// <summary>必选：自己写了的优先，其次所在字段的。</summary>
    internal bool Required => IsRequired ?? FieldControl.Of(this)?.IsRequired ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    /// <summary>选中的值（单选时最多一个；单选的空字符串算没有选中）。</summary>
    [Browsable(false)]
    public IReadOnlyList<string> SelectedValues => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

    List<ToggleItemControl> Items() => [.. Controls.OfType<ToggleItemControl>().Where(item => item.Visible)];

    bool InToolbar()
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
            if (parent is ToolbarControl) return true;
        return false;
    }

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control!.GotFocus += OnItemFocused;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        e.Control!.GotFocus -= OnItemFocused;
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <summary>四周 space.xs（按字号）：字体或 DPI 变了重新算。</summary>
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

    void OnItemFocused(object? sender, EventArgs e)
    {
        current = sender as Control;
        SyncTabStops();
    }

    /// <summary>选中状态、外观交给各项；内边距、Tab 停靠点。</summary>
    internal void Sync()
    {
        var selected = SelectedValues;
        foreach (var item in Controls.OfType<ToggleItemControl>()) item.Apply(this, item.Value is { } value && selected.Contains(value));
        Padding = new Padding((int)MathF.Round(0.25f * Font.SizeInPoints * DeviceDpi / 72f));
        SyncTabStops();
        PerformLayout();
        Invalidate();
    }

    /// <summary>整组一个 Tab 停靠点：聚焦过的，没有时选中项，再没有时第一个可用的；在工具栏里交给工具栏。</summary>
    void SyncTabStops()
    {
        if (InToolbar()) return;
        var items = Items().Where(item => item.Enabled).ToList();
        var stop = current is not null && items.Contains(current) ? current : items.FirstOrDefault(item => item.IsSelected) ?? items.FirstOrDefault();
        foreach (var item in Controls.OfType<ToggleItemControl>()) item.TabStop = item == stop;
    }

    /// <summary>切换一项（单选、多选的规则见 <see cref="Selection.Toggle"/>）；只读时不切换。</summary>
    internal void Toggle(string value)
    {
        if (ReadOnly) return;
        var selected = SelectedValues;
        var next = Selection.Toggle(selected, value, SelectionMode, Required);
        if (ReferenceEquals(next, selected)) return;
        if (Multiple) Values = next;
        else Value = next.FirstOrDefault();
    }

    /// <summary>方向键、Home、End 在项之间移动焦点（不改变选择）；到两端后从另一端继续。在工具栏里时交给工具栏。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (InToolbar()) return base.ProcessDialogKey(keyData);
        var items = Items().Where(item => item.Enabled).ToList();
        var index = items.FindIndex(item => item.Focused);
        if (index < 0) return base.ProcessDialogKey(keyData);
        var vertical = Orientation == ToggleGroupOrientation.Vertical;
        var rtl = RightToLeft == RightToLeft.Yes;
        int? target = keyData switch
        {
            Keys.Right when !vertical => rtl ? index - 1 : index + 1,
            Keys.Left when !vertical => rtl ? index + 1 : index - 1,
            Keys.Down when vertical => index + 1,
            Keys.Up when vertical => index - 1,
            Keys.Home => 0,
            Keys.End => items.Count - 1,
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => index,
            _ => null,
        };
        if (target is not { } next) return base.ProcessDialogKey(keyData);
        next = (next + items.Count) % items.Count;
        current = items[next];
        SyncTabStops();
        current.Focus();
        return true;
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        PlaceIndicator();
    }

    /// <summary>滑块对准选中项：第一次直接到位，之后滑过去（durations.md、standard）。</summary>
    void PlaceIndicator()
    {
        if (!Sliding || Items().FirstOrDefault(item => item.IsSelected) is not { Width: > 0 } selected) return;
        var duration = placed ? Tokens.Duration.Md : TimeSpan.Zero;
        placed = true;
        Animator.Animate(left, selected.Left, duration, Easing.Standard);
        Animator.Animate(top, selected.Top, duration, Easing.Standard);
        Animator.Animate(width, selected.Width, duration, Easing.Standard);
        Animator.Animate(height, selected.Height, duration, Easing.Standard);
    }

    /// <summary>fill 底、md 圆角；单选时选中项下面 accentSoft 底、xs 宽的 accentBorder 描边、sm 圆角的滑块。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var em = Font.SizeInPoints * DeviceDpi / 72f;
        using (var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), theme.Radii.Md * em))
        using (var fill = new SolidBrush(theme.Colors.Fill))
            graphics.FillPath(fill, path);
        var (x, y, w, h) = ((float)left.Value, (float)top.Value, (float)width.Value, (float)height.Value);
        if (!Sliding || SelectedValues.Count == 0 || w <= 0) return;
        var line = LogicalToDeviceUnits(1);
        var radius = Items().FirstOrDefault(item => item.IsSelected)?.ItemEm * theme.Radii.Sm ?? theme.Radii.Sm * em;
        using var outer = Drawing.RoundedRectangle(new RectangleF(x, y, w, h), radius);
        using (var border = new SolidBrush(theme.Colors.AccentBorder)) graphics.FillPath(border, outer);
        using var inner = Drawing.RoundedRectangle(new RectangleF(x + line, y + line, w - 2 * line, h - 2 * line), Math.Max(0, radius - line));
        using var soft = new SolidBrush(theme.Colors.AccentSoft);
        graphics.FillPath(soft, inner);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var signal in new[] { left, top, width, height }) Animator.Stop(signal);
            paint.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GroupAccessibleObject(this);

    sealed class GroupAccessibleObject(ToggleGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Help => FieldControl.DescribedBy(owner, base.Help);

        public override string? Name => base.Name is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;
    }
}

/// <summary>切换按钮组里的一项（元素）：同按钮的外观，默认中性色的 ghost、sm 圆角；没有文字时是方的。</summary>
public class ToggleItem : Button
{
    /// <inheritdoc />
    protected override ButtonVariant DefaultVariant => ButtonVariant.Ghost;

    /// <inheritdoc />
    protected override StatusTone DefaultTone => StatusTone.Neutral;

    /// <inheritdoc />
    protected override bool Square => string.IsNullOrEmpty(Content);
}

/// <summary>
/// 切换按钮组里的一项（TS 的 ToggleItem）：没选中是中性色的 ghost；选中时品牌色，单选时底色是组的滑块，多选时是 surface。
/// 只有图标（没有 Text）时是方的，读屏名称写 AccessibleName。点击先发 Click，再交给组切换。读屏里是按下或弹起的按钮。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[Description("切换按钮组里的一项：点了按下或弹起。")]
public class ToggleItemControl : ButtonControl<ToggleItem>
{
    ToggleGroupControl? owner;

    /// <summary>新建：sm 圆角。</summary>
    public ToggleItemControl() => Radius = Radius.Sm;

    /// <summary>这一项的值。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            field = value;
            owner?.Sync();
        }
    }

    /// <summary>选中着（按下）。</summary>
    [Browsable(false)]
    public bool IsSelected { get; private set; }

    /// <summary>这一项的 em（像素），按自己的字号档位。</summary>
    internal float ItemEm => TextSize.Pixels is { } pixels ? pixels * DeviceDpi / 96f : Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>跟着组：选中状态、外观（样式、色调、字号、波纹）。</summary>
    internal void Apply(ToggleGroupControl group, bool selected)
    {
        owner = group;
        IsSelected = selected;
        Variant = selected && !group.Sliding ? ButtonVariant.Surface : ButtonVariant.Ghost;
        Tone = selected ? StatusTone.Accent : StatusTone.Neutral;
        if (group.TextSize.Value is not null) TextSize = group.TextSize;
        Ripple = group.ReadOnly ? false : group.Ripple;
        // 只读、没选中的项：不像能点
        Cursor = group.ReadOnly && !selected ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    /// <summary>点击：先发 Click，再交给组切换（不可用、加载中时都不切）。</summary>
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled && !IsLoading && Value is { } value) owner?.Toggle(value);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibleObject(this);

    sealed class ToggleAccessibleObject(ToggleItemControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : owner.Text;

        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleStates State => base.State | (owner.IsSelected ? AccessibleStates.Pressed : 0);

        public override string DefaultAction => owner.IsSelected ? "弹起" : "按下";

        public override void DoDefaultAction() => owner.PerformClick();
    }
}
