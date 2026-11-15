using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 浮动按钮（vue-tsx-pro 的 FloatButton）：窗体角落的常驻操作（新建、反馈、客服、回到顶部）。
/// <code>
/// page.Controls.Add(new FloatButtonControl { Icon = Lucide.MessageCircle, AccessibleName = "联系客服", Badge = 2 });
/// </code>
/// 按钮是 <see cref="IconButtonControl"/>（surface、中性色、圆形、lg），悬停或聚焦时提示名称（<see cref="Tip"/>，默认就是名称；<see cref="ShowTip"/> 为 false 不提示），
/// 提示在左边；<see cref="Badge"/> 是数字角标、<see cref="BadgeDot"/> 是小圆点（<see cref="IndicatorControl"/>，圆形重叠）。WinForms 的控件画不出阴影，按钮没有 md 阴影。
/// 桌面上没有“固定在视口”：单独用时摆在父控件客户区的结束边与底部（外边距 lg，从右到左排版时在左下），跟着父控件变大小、放在最上层；
/// 在 <see cref="FloatButtonGroupControl"/> 里跟着组排。名称写在 FloatButtonControl 上（AccessibleName），转给里面的按钮。
/// </summary>
[DefaultEvent(nameof(Click))]
[Description("浮动按钮：窗体角落的常驻操作，悬停提示名称，可带角标。")]
public class FloatButtonControl : Control
{
    /// <summary>所有浮动按钮共用的提示。</summary>
    static readonly TooltipProvider Tips = new();

    readonly IndicatorControl indicator = new() { Overlap = IndicatorOverlap.Circular };

    /// <summary>新建：圆形的图标按钮包在角标里。</summary>
    public FloatButtonControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
        AutoSize = true;
        Button = new IconButtonControl { Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full, TextSize = TextSize.Lg };
        Button.Click += (_, e) => OnClick(e);
        Button.SizeChanged += (_, _) => PerformLayout();
        indicator.Controls.Add(Button);
        indicator.SizeChanged += (_, _) => Size = indicator.Size;
        Controls.Add(indicator);
        Tips.SetTooltipPlacement(Button, FloatingPlacement.Left);
        // 摆位置的对象挂在自己的事件上，跟着控件活
        _ = new FloatCorner(this);
    }

    /// <summary>里面的图标按钮（TS 里落到 IconButton 上的属性：样式档位、色调写在它身上）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconButtonControl Button { get; }

    /// <summary>图标。</summary>
    [Category("Appearance")]
    [Description("图标。")]
    [DefaultValue(null)]
    public IconData? Icon { get => Button.Icon; set => Button.Icon = value; }

    /// <summary>数字角标（未读数）；0 时不显示。</summary>
    [Category("Appearance")]
    [Description("数字角标（未读数）；0 时不显示。")]
    [DefaultValue(null)]
    public int? Badge { get => indicator.Count; set => indicator.Count = value; }

    /// <summary>小圆点角标（TS 的 badge: true）。</summary>
    [Category("Appearance")]
    [Description("小圆点角标。")]
    [DefaultValue(false)]
    public bool BadgeDot { get => indicator.Dot; set => indicator.Dot = value; }

    /// <summary>悬停、聚焦时的提示；不写时是名称。</summary>
    [Category("Appearance")]
    [Description("悬停、聚焦时的提示；不写时是名称。")]
    [DefaultValue(null)]
    public string? Tip { get; set { field = value; SyncTip(); } }

    /// <summary>提示，默认 true（TS 的 tooltip: false 时为 false）。</summary>
    [Category("Appearance")]
    [Description("悬停、聚焦时提示。")]
    [DefaultValue(true)]
    public bool ShowTip { get; set { field = value; SyncTip(); } } = true;

    /// <summary>角标（测试用）。</summary>
    internal IndicatorControl Indicator => indicator;

    /// <summary>提示的文字（测试用）。</summary>
    internal string? ShownTip => Tips.GetTooltip(Button);

    /// <summary>在组里（跟着组排，不摆到角落）。</summary>
    internal bool Grouped => Parent?.Parent is FloatButtonGroupControl;

    /// <summary>名称：转给里面的按钮，也是默认的提示。</summary>
    [Category("Accessibility")]
    [Description("名称：读屏念的是里面的按钮，也是默认的提示。")]
    [DefaultValue(null)]
    public new string? AccessibleName
    {
        get => base.AccessibleName;
        set
        {
            base.AccessibleName = value;
            SyncTip();
        }
    }

    /// <summary>名称转给按钮；提示默认是名称。</summary>
    private protected virtual void SyncTip()
    {
        Button.AccessibleName = AccessibleName;
        Tips.SetTooltip(Button, ShowTip ? Tip ?? AccessibleName : null);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => indicator.GetPreferredSize(proposedSize);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        indicator.Location = Point.Empty;
        indicator.Size = indicator.GetPreferredSize(Size.Empty);
        if (Size != indicator.Size) Size = indicator.Size;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new HiddenAccessibleObject(this);

    /// <summary>自己不进读屏的树：读屏看到的是里面的按钮。</summary>
    sealed class HiddenAccessibleObject(Control owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;
    }
}

/// <summary>
/// 单独用的浮动按钮（组）摆在父控件客户区的结束边与底部：外边距 lg（em），跟着父控件变大小、放到最上层（TS 的 position: fixed 加 inset-inline-end、inset-block-end）。
/// 组里的按钮不摆（父控件是组的那一列）。
/// </summary>
sealed class FloatCorner
{
    readonly Control owner;
    Control? parent;

    public FloatCorner(Control owner)
    {
        this.owner = owner;
        owner.ParentChanged += (_, _) => Watch();
        owner.SizeChanged += (_, _) => Place();
        owner.RightToLeftChanged += (_, _) => Place();
    }

    bool Grouped => owner.Parent?.Parent is FloatButtonGroupControl;

    void Watch()
    {
        if (parent is not null)
        {
            parent.Resize -= OnParentResized;
            parent.Layout -= OnParentLayout;
        }
        parent = Grouped ? null : owner.Parent;
        if (parent is null) return;
        parent.Resize += OnParentResized;
        parent.Layout += OnParentLayout;
        Place();
    }

    void OnParentResized(object? sender, EventArgs e) => Place();

    void OnParentLayout(object? sender, LayoutEventArgs e) => Place();

    public void Place()
    {
        if (parent is null || owner.Parent != parent) return;
        var margin = (int)MathF.Round(Tokens.Space.Lg * ElementContext.Of(owner).Em);
        var area = parent.DisplayRectangle;
        var x = owner.RightToLeft == RightToLeft.Yes ? area.Left + margin : area.Right - margin - owner.Width;
        var location = new Point(x, area.Bottom - margin - owner.Height);
        if (owner.Location != location) owner.Location = location;
        if (parent.Controls.GetChildIndex(owner) != 0) owner.BringToFront();
    }
}

/// <summary>
/// 一组浮动按钮（vue-tsx-pro 的 FloatButtonGroup）：竖排（间距 sm）摆在父控件的结束边与底部；给了 <see cref="Icon"/> 时是速拨：主按钮（solid、accent，名称默认“更多操作”）
/// 点一下展开、收起其余按钮；Esc 或点组以外的地方收起，Esc 时焦点回到主按钮。<see cref="BackTopControl"/> 也能放进来。
/// <code>
/// var dial = new FloatButtonGroupControl { Icon = Lucide.Plus, AccessibleName = "新建" };
/// dial.Buttons.Add(new FloatButtonControl { Icon = Lucide.FileText, AccessibleName = "新建文档" });
/// </code>
/// 组里的按钮放在 <see cref="Buttons"/>（主按钮总在最下面）。读屏：不是速拨时是一组（名称写在组上）；速拨时主按钮报展开、收起。
/// </summary>
[Description("一组浮动按钮：竖排在角落；给了 Icon 时是速拨，主按钮展开、收起其余按钮。")]
public class FloatButtonGroupControl : FlexControl, IMessageFilter
{
    const int WmLButtonDown = 0x201, WmRButtonDown = 0x204, WmNcLButtonDown = 0xA1;

    readonly FlexControl actions = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly FloatCorner corner;
    bool filtering;

    /// <summary>新建：按钮竖排；速拨的主按钮在最下面。</summary>
    public FloatButtonGroupControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        Trigger.ShowTip = false;
        Trigger.Visible = false;
        Trigger.Button.Variant = ButtonVariant.Solid;
        Trigger.Button.Tone = StatusTone.Accent;
        Trigger.Click += (_, _) => IsOpen = !IsOpen;
        Controls.AddRange([actions, Trigger]);
        corner = new FloatCorner(this);
        Sync();
    }

    /// <summary>组里的按钮（FloatButtonControl、BackTopControl）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ControlCollection Buttons => actions.Controls;

    /// <summary>给了就是速拨：主按钮用这个图标。</summary>
    [Category("Appearance")]
    [Description("给了就是速拨：主按钮用这个图标。")]
    [DefaultValue(null)]
    public IconData? Icon { get; set { field = value; Sync(); } }

    /// <summary>速拨展开着。</summary>
    [Category("Behavior")]
    [Description("速拨展开着。")]
    [DefaultValue(false)]
    public bool IsOpen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            OpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>速拨展开、收起了。</summary>
    public event EventHandler? OpenChanged;

    /// <summary>速拨的主按钮（TS 的 propsTrigger）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FloatButtonControl Trigger { get; } = new();

    /// <summary>速拨展开的那一列（测试用）。</summary>
    internal FlexControl Actions => actions;

    /// <summary>不是速拨时是组的名称；速拨时是主按钮的名称（默认“更多操作”）。</summary>
    [Category("Accessibility")]
    [Description("不是速拨时是组的名称；速拨时是主按钮的名称。")]
    [DefaultValue(null)]
    public new string? AccessibleName
    {
        get => base.AccessibleName;
        set
        {
            base.AccessibleName = value;
            Sync();
        }
    }

    /// <summary>不是速拨：按钮都显示，名称在组上。速拨：主按钮显示（展开时图标换成 ×、报展开），其余按钮展开时显示；展开期间点在组外面就收起。</summary>
    void Sync()
    {
        var dial = Icon is not null;
        Trigger.Visible = dial;
        Trigger.Icon = dial && IsOpen ? Lucide.X : Icon;
        Trigger.AccessibleName = dial ? AccessibleName ?? ProviderControl.LangOf(this).FloatButtonMore : null;
        Trigger.Button.Expanded = dial ? IsOpen : null;
        actions.Visible = !dial || IsOpen;
        Filter(dial && IsOpen);
        PerformLayout();
        corner.Place();
    }

    void Filter(bool on)
    {
        if (on == filtering) return;
        filtering = on;
        if (on) Application.AddMessageFilter(this);
        else Application.RemoveMessageFilter(this);
    }

    /// <summary>展开期间按在组以外的地方：收起（不吃掉这次点击）。</summary>
    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        if (m.Msg is not (WmLButtonDown or WmRButtonDown or WmNcLButtonDown)) return false;
        var target = FromHandle(m.HWnd);
        if (target != this && (target is null || !Contains(target))) IsOpen = false;
        return false;
    }

    /// <summary>Esc 收起，焦点回到主按钮。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && Icon is not null && IsOpen)
        {
            IsOpen = false;
            Trigger.Button.Focus();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Esc（测试用：ProcessCmdKey 要有窗口消息）。</summary>
    internal void Escape()
    {
        var message = new Message();
        ProcessCmdKey(ref message, Keys.Escape);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Filter(false);
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GroupAccessibleObject(this);

    /// <summary>读屏：一组（速拨时名称在主按钮上，组本身不另起名）。</summary>
    sealed class GroupAccessibleObject(FloatButtonGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;

        public override string? Name => owner.Icon is null ? owner.AccessibleName : null;
    }
}
