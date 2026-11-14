using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 手风琴（vue-tsx-pro 的 Accordion）：一组可展开收起的 <see cref="AccordionItemControl"/>，单开或多开（WAI-ARIA 的 Accordion 模式）。
/// <code>
/// var faq = new AccordionControl { Value = "ship", AccessibleName = "常见问题" };
/// faq.Controls.Add(new AccordionItemControl { Value = "ship", Title = "多久发货？", Content = new ParagraphControl { Text = "付款后 48 小时内。" } });
/// </code>
/// 单开时 <see cref="Value"/> 是展开那项的值，再点可以收起（<see cref="Collapsible"/>）；多开时 <see cref="Values"/> 是展开着的值；变了发 <see cref="ValueChanged"/>。
/// 标题都在 Tab 顺序里，另外 ↑ / ↓ 在标题之间移动（到头后循环），Home / End 到第一个、最后一个。不可用用平台的 Enabled。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("手风琴：一组可展开收起的项。")]
public class AccordionControl : FlexControl
{
    /// <summary>新建：竖排、项拉满宽度，读屏里是一个组。</summary>
    public AccordionControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>单开（默认）或多开。</summary>
    [Category("Behavior")]
    public SelectionMode SelectionMode { get; set { field = value; Sync(); } } = SelectionMode.Single;

    /// <summary>单开时展开那项的值。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Value { get; set { field = value; Sync(); } }

    /// <summary>多开时展开着的值。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string>? Values { get; set { field = value; Sync(); } }

    /// <summary>单开时允许把唯一展开的那项也收起，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Collapsible { get; set; } = true;

    /// <summary>用户展开或收起了一项（Value 或 Values 变了）。</summary>
    public event EventHandler? ValueChanged;

    bool ShouldSerializeSelectionMode() => SelectionMode != SelectionMode.Single;

    void ResetSelectionMode() => SelectionMode = SelectionMode.Single;

    /// <summary>展开着的值（两种方式合在一起看）。</summary>
    internal IReadOnlyList<string> Expanded => SelectionMode == SelectionMode.Multiple ? Values ?? [] : Value is { } value ? [value] : [];

    IEnumerable<AccordionItemControl> Items => Controls.Cast<Control>().OfType<AccordionItemControl>();

    /// <summary>展开或收起一项（单开 / 多开的规则同 ToggleGroup：<see cref="Selection.Toggle"/>）。</summary>
    internal void Toggle(string item)
    {
        var next = Selection.Toggle(Expanded, item, SelectionMode, required: !Collapsible);
        if (SelectionMode == SelectionMode.Multiple) Values = next;
        else Value = next.Count > 0 ? next[0] : null;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>↑ / ↓ 在标题之间移动（循环），Home / End 到两端。</summary>
    internal void MoveFocus(AccordionItemControl from, Keys key)
    {
        var triggers = Items.Where(item => item.Visible && item.Enabled).ToList();
        var index = triggers.IndexOf(from);
        if (index < 0) return;
        var next = key switch
        {
            Keys.Down => triggers[(index + 1) % triggers.Count],
            Keys.Up => triggers[(index - 1 + triggers.Count) % triggers.Count],
            Keys.Home => triggers[0],
            Keys.End => triggers[^1],
            _ => null,
        };
        next?.Trigger.Focus();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is AccordionItemControl item) item.Sync();
    }

    void Sync()
    {
        foreach (var item in Items) item.Sync();
    }
}

/// <summary>
/// 手风琴里的一项（vue-tsx-pro 的 AccordionItem）：标题按钮 + 展开收起的内容（<see cref="CollapseControl"/>）。底边一条 borderMuted 的线。
/// 标题一行占满宽度：上下 space.md、左右 space.sm、sm 圆角，悬停 fill 底，medium 字重，结束边是展开箭头（展开时翻转）；焦点环画在里面。
/// 标题是文字（<see cref="Title"/>），要放图标、计数时用 <see cref="TitleContent"/>；内容是 <see cref="Content"/>。读屏：标题按钮带展开、收起的状态。
/// </summary>
[ToolboxItem(true)]
[Description("手风琴里的一项：标题与展开收起的内容。")]
public class AccordionItemControl : FlexControl
{
    readonly TextControl title = new() { Weight = TextWeight.Medium };
    readonly IconControl chevron = new() { Data = Lucide.ChevronDown, Tone = TextTone.Muted };
    readonly CollapseControl collapse = new();
    readonly Panel body = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };

    /// <summary>新建：标题按钮在上，内容在下。</summary>
    public AccordionItemControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Trigger.Controls.AddRange([title, chevron]);
        Trigger.SetGrow(title, true);
        Trigger.Click += (_, _) => Owner?.Toggle(Value ?? "");
        Trigger.Navigate += key => Owner?.MoveFocus(this, key);
        collapse.Controls.Add(body);
        Controls.AddRange([Trigger, collapse]);
        UpdatePadding();
    }

    /// <summary>这一项的值。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Value { get; set { field = value; Sync(); } }

    /// <summary>标题文字（也是展开内容的读屏名称）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title
    {
        get;
        set
        {
            field = value;
            title.Text = value ?? "";
            Trigger.AccessibleName = value;
            collapse.AccessibleName = value;
        }
    }

    /// <summary>富标题（图标、计数）：给了时代替 Title 的文字显示在标题里；读屏名称仍取 Title。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? TitleContent
    {
        get;
        set
        {
            if (field is not null) Trigger.Controls.Remove(field);
            field = value;
            title.Visible = value is null;
            if (value is null) return;
            Trigger.Controls.Add(value);
            Trigger.Controls.SetChildIndex(value, 0);
            Trigger.SetGrow(value, true);
        }
    }

    /// <summary>内容。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            body.Controls.Clear();
            field = value;
            if (value is null) return;
            value.Dock = DockStyle.Top;
            body.Controls.Add(value);
        }
    }

    /// <summary>标题按钮。</summary>
    internal AccordionTrigger Trigger { get; } = new();

    /// <summary>展开着。</summary>
    [Browsable(false)]
    public bool IsOpen => Owner?.Expanded.Contains(Value ?? "") == true;

    AccordionControl? Owner => Parent as AccordionControl;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdatePadding();
    }

    /// <summary>标题上下 space.md、左右 space.sm；内容左右 space.sm、下面 space.md（按字号算）。</summary>
    void UpdatePadding()
    {
        var em = this.EmPixels();
        var (sm, md) = ((int)MathF.Round(0.5f * em), (int)MathF.Round(0.75f * em));
        Trigger.Padding = new Padding(sm, md, sm, md);
        body.Padding = new Padding(sm, 0, sm, md);
        // 底边的线画在自己的内边距里：展开着的内容区（子控件是窗口）会盖住画在它下面的东西
        Padding = new Padding(0, 0, 0, Line);
    }

    int Line => Math.Max(1, LogicalToDeviceUnits(1));

    /// <summary>不可用：标题变淡（TS 是整个标题按钮半透明；WinForms 的子控件不能半透明，用次要色）。</summary>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        title.Tone = Enabled ? default : TextTone.Muted;
    }

    /// <summary>跟上组的展开状态：内容展开收起、箭头翻转、读屏的展开状态。</summary>
    internal void Sync()
    {
        var open = IsOpen;
        collapse.IsOpen = open;
        chevron.Data = open ? Lucide.ChevronUp : Lucide.ChevronDown;
        Trigger.Expanded = open;
    }

    /// <summary>底边一条 borderMuted 的线（borderWidths.xs）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(ProviderControl.ThemeOf(this).Colors.BorderMuted);
        e.Graphics.FillRectangle(brush, 0, Height - Line, Width, Line);
    }
}

/// <summary>手风琴的标题按钮：悬停 fill 底（PressableFlex 默认是 fillHover）；↑ ↓ Home End 交给手风琴；读屏带展开、收起的状态。</summary>
sealed class AccordionTrigger : PressableFlex
{
    public AccordionTrigger()
    {
        Align = FlexAlign.Center;
        Gap = Space.Md;
        Cursor = Cursors.Default;
    }

    /// <summary>展开着（读屏的状态）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
    }

    /// <summary>按了 ↑ ↓ Home End。</summary>
    public event Action<Keys>? Navigate;

    /// <summary>代码里点击（测试用）。</summary>
    internal void PerformClickForTest() => OnClick(EventArgs.Empty);

    protected override void PaintSurface(Graphics graphics, ThemeTokens theme, float radius)
    {
        if (!Hovered) return;
        using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
        using var brush = new SolidBrush(theme.Colors.Fill);
        graphics.FillPath(brush, path);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Up or Keys.Down or Keys.Home or Keys.End)) return;
        e.Handled = true;
        Navigate?.Invoke(e.KeyCode);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TriggerAccessibleObject(this);

    sealed class TriggerAccessibleObject(AccordionTrigger owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleStates State => base.State | (owner.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed);

        public override string DefaultAction => owner.Expanded ? "收起" : "展开";

        public override void DoDefaultAction() => owner.InvokeOnClick(owner, EventArgs.Empty);
    }
}

/// <summary>
/// 可展开收起的区域（vue-tsx-pro 的 Collapsible，WAI-ARIA 的 Disclosure 模式）：触发器 + <see cref="CollapseControl"/>。
/// <code>
/// var filters = new CollapsibleControl { Trigger = new ButtonControl { Text = "高级筛选", Variant = ButtonVariant.Ghost }, Content = grid };
/// </code>
/// 点触发器切换 <see cref="IsOpen"/>，变了发 <see cref="OpenChanged"/>；IsDisabled 时触发器不可用、点击不切换（IsOpen 仍可由代码改）。触发器靠起始边。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(OpenChanged))]
[Description("可展开收起的区域：触发器加展开收起的内容。")]
public class CollapsibleControl : FlexControl
{
    readonly CollapseControl collapse = new();

    /// <summary>新建：竖排、收起着。</summary>
    public CollapsibleControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        Controls.Add(collapse);
    }

    /// <summary>触发器：一个按钮（ButtonControl 等）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Trigger
    {
        get;
        set
        {
            if (field is not null)
            {
                field.Click -= OnTriggerClick;
                Controls.Remove(field);
            }
            field = value;
            if (value is null) return;
            value.Click += OnTriggerClick;
            Controls.Add(value);
            Controls.SetChildIndex(value, 0);
            SetAlignSelf(value, FlexAlign.Start);
            Sync();
        }
    }

    /// <summary>内容。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            collapse.Controls.Clear();
            field = value;
            if (value is not null) collapse.Controls.Add(value);
        }
    }

    /// <summary>展开着。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsOpen { get; set { if (field == value) return; field = value; Sync(); OpenChanged?.Invoke(this, EventArgs.Empty); } }

    /// <summary>不可用：触发器不可用，点击不切换。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsDisabled { get; set { field = value; Sync(); } }

    /// <summary>展开或收起了。</summary>
    public event EventHandler? OpenChanged;

    void OnTriggerClick(object? sender, EventArgs e)
    {
        if (!IsDisabled) IsOpen = !IsOpen;
    }

    void Sync()
    {
        collapse.IsOpen = IsOpen;
        if (Trigger is not { } trigger) return;
        trigger.Enabled = !IsDisabled;
    }
}
