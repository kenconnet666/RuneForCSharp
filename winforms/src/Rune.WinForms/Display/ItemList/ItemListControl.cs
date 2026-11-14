using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 条目列表（vue-tsx-pro 的 ItemList）：一行一条，起始边是图标、色点或头像，中间是标题与说明，结束边是标记、数字或操作。
/// 待办、通知、我负责的任务这类"界面里的列表"（正文里的列表用 ListControl）。
/// <code>
/// var list = new ItemListControl { Divided = true };
/// list.Controls.Add(new ItemListItemControl { Text = "资料审核", Description = "PO-2026-0187", Start = new StatusDotControl() });
/// </code>
/// <see cref="Divided"/> 在条目之间画线，<see cref="Hoverable"/> 悬停时着底色，<see cref="Inset"/> 是条目两侧的内边距（放在弹出层、卡片里时与容器对齐；
/// 悬停着底色、整条可点的条目不写时留 sm）。里面的链接悬停才有下划线（一列下来都带下划线太重）。读屏里是列表。
/// </summary>
[ToolboxItem(true)]
[Description("条目列表：待办、通知这类界面里的列表。")]
public class ItemListControl : FlexControl
{
    /// <summary>新建：条目竖着排、撑满宽度。</summary>
    public ItemListControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.List;
        LinkControl.SetUnderlineScope(this, LinkUnderline.Hover);
    }

    /// <summary>条目之间画线（borderMuted，最后一条下面没有）。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Divided { get; set { field = value; ApplyLooks(); } }

    /// <summary>悬停时着底色：整条可点时。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Hoverable { get; set { field = value; ApplyLooks(); } }

    /// <summary>疏密，默认 standard。</summary>
    [Category("Appearance")]
    public ItemListDensity Density
    {
        get => field.Value is null ? ItemListDensity.Standard : field;
        set
        {
            field = value;
            ApplyLooks();
        }
    }

    /// <summary>条目两侧的内边距；不写时悬停着底色、整条可点的条目留 sm，其余不留。</summary>
    [Category("Layout")]
    [DefaultValue(null)]
    public Space? Inset { get; set { field = value; ApplyLooks(); } }

    bool ShouldSerializeDensity() => Density != ItemListDensity.Standard;

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        ApplyEndLooks();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        ApplyEndLooks();
    }

    /// <summary>设置变了：每一条按列表的设置换外观。</summary>
    void ApplyLooks()
    {
        foreach (var item in Controls.OfType<ItemListItemControl>()) item.ApplyLook();
    }

    /// <summary>
    /// 条目增减只影响分隔线（最后一条下面没有）：末尾两条换外观就够，新加的那条挂上时自己会换。
    /// 每加一条都整列重换的话，一列几十条是几万次换外观。
    /// </summary>
    void ApplyEndLooks()
    {
        foreach (var item in Controls.OfType<ItemListItemControl>().TakeLast(2)) item.ApplyLook();
    }
}

/// <summary>
/// 条目列表的一条（vue-tsx-pro 的 ItemListItem）：<see cref="Start"/>、标题（<see cref="Text"/>）与 <see cref="Description"/>、<see cref="End"/> 三列。
/// 标题与说明放不下时换行；<see cref="Align"/> 为 start 时起始边、结束边贴着第一行。
/// 整条可点：写 <see cref="Href"/>（绝对地址，系统打开）或订阅 <see cref="Invoked"/>（TS 的 onSelect），点起始边、标题、说明、空白处都一样；
/// 结束边的控件照常可点，点它不触发这一条。<see cref="IsSelected"/> 是正在看的一条：accentSoft 底，读屏里是选中的。
/// 不可用（Enabled=false）只对整条可点的条目有外观：变淡、点不了。不可点的条目不能聚焦，读屏里是列表项。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Invoked))]
[Description("条目列表的一条：起始边、标题与说明、结束边。")]
public class ItemListItemControl : FlexControl
{
    // 起始边、结束边各一格，跟着里面的控件变大小；内容在格里竖直居中（对齐 start 时起始边那格至少一行高，色点对着标题）
    readonly FlexControl startHolder = new() { AutoSize = true, Align = FlexAlign.Center, Visible = false };
    readonly FlexControl body = new() { Direction = FlexDirection.Column, Gap = Space.Xxs, AutoSize = true };
    readonly TextControl title = new();
    readonly SmallControl description = new() { Tone = TextTone.Muted, TextSize = TextSize.Xs, Visible = false };
    // 结束边那一格总在（TS 没有 end 时也放一个空的 span）：标题与说明后面总有 space.sm
    readonly FlexControl endHolder = new() { AutoSize = true, Align = FlexAlign.Center };
    Control? start;
    Control? end;
    EventHandler? invoked;
    bool hovered;

    /// <summary>新建：起始边、标题与说明、结束边横排。</summary>
    public ItemListItemControl()
    {
        Gap = Space.Sm;
        base.Align = FlexAlign.Center;
        AutoSize = true;
        AccessibleRole = AccessibleRole.ListItem;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        body.Controls.AddRange([title, description]);
        Controls.AddRange([startHolder, body, endHolder]);
        SetGrow(body, true);
        Forward(startHolder);
        Forward(body);
        endHolder.MouseEnter += (_, _) => SetHovered(true);
        endHolder.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        ApplyLook();
    }

    /// <summary>起始边：图标、色点、头像、状态。</summary>
    [Browsable(false)]
    [DefaultValue(null)]
    public Control? Start
    {
        get => start;
        set
        {
            Place(startHolder, ref start, value, forward: true);
            ApplyLook();
        }
    }

    /// <summary>结束边：标记、数字、时间、操作（点它们不触发这一条）。</summary>
    [Browsable(false)]
    [DefaultValue(null)]
    public Control? End
    {
        get => end;
        set => Place(endHolder, ref end, value, forward: false);
    }

    /// <summary>标题。</summary>
    [AllowNull]
    public override string Text
    {
        get => title.Text;
        set
        {
            title.Text = value ?? "";
            base.Text = value;
        }
    }

    /// <summary>标题下的说明（textMuted、xs）；空时不显示。</summary>
    [Category("Appearance")]
    [DefaultValue("")]
    [AllowNull]
    public string Description
    {
        get => description.Text;
        set
        {
            description.Text = value ?? "";
            description.Visible = description.Text.Length > 0;
        }
    }

    /// <summary>起始边、结束边与内容的对齐，默认 center（TS 的 align；行本身的 Flex 对齐按它设）。</summary>
    [Category("Appearance")]
    public new ItemListAlign Align
    {
        get => field.Value is null ? ItemListAlign.Center : field;
        set
        {
            field = value;
            ApplyLook();
        }
    }

    /// <summary>整条是链接（绝对地址）：点了用系统打开。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public Uri? Href { get; set { field = value; ApplyLook(); } }

    /// <summary>正在看的一条。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool IsSelected { get; set { field = value; Invalidate(); } }

    /// <summary>点了这一条（TS 的 onSelect）：订阅了才整条可点。</summary>
    public event EventHandler? Invoked
    {
        add
        {
            invoked += value;
            ApplyLook();
        }
        remove
        {
            invoked -= value;
            ApplyLook();
        }
    }

    /// <summary>整条可点：有地址或订阅了 Invoked。</summary>
    [Browsable(false)]
    public bool IsInteractive => Href is not null || invoked is not null;

    bool ShouldSerializeAlign() => Align != ItemListAlign.Center;

    float Em => this.EmPixels();

    ItemListControl? List => Parent as ItemListControl;

    /// <summary>往一格里放使用方的控件；起始边空时隐藏那一格（不留第一列）。</summary>
    void Place(Control holder, ref Control? slot, Control? value, bool forward)
    {
        if (slot is not null) holder.Controls.Remove(slot);
        slot = value;
        if (value is not null)
        {
            holder.Controls.Add(value);
            if (forward) Forward(value);
        }
        if (holder == startHolder) holder.Visible = value is not null;
        PerformLayout();
    }

    /// <summary>起始边、标题与说明也算这一条：点它们同点这一条，指针在它们上面也算悬停。</summary>
    void Forward(Control child)
    {
        child.MouseClick += (_, args) => OnMouseClick(args);
        child.MouseEnter += (_, _) => SetHovered(true);
        child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        foreach (Control grandchild in child.Controls) Forward(grandchild);
    }

    /// <summary>
    /// 按所在列表的设置与自己的状态换外观：内边距（上下按疏密，两侧按 Inset）、对齐；对齐 start 时起始边至少一行高。
    /// 只有整条可点的条目能聚焦（可点时读屏里是链接或按钮）。
    /// </summary>
    internal void ApplyLook()
    {
        var list = List;
        var interactive = IsInteractive;
        var em = Em;
        var block = (list?.Density == ItemListDensity.Compact ? Tokens.Space.Xs : Tokens.Space.Sm) * em;
        var gutter = ((list?.Inset ?? (list?.Hoverable == true || interactive ? Space.Sm : null))?.Factor ?? 0) * em;
        var divider = list is { Divided: true } && list.Controls.OfType<ItemListItemControl>().LastOrDefault() != this ? LogicalToDeviceUnits(1) : 0;
        Padding = new Padding((int)MathF.Round(gutter), (int)MathF.Round(block), (int)MathF.Round(gutter), (int)MathF.Round(block) + divider);
        var start = Align == ItemListAlign.Start;
        base.Align = start ? FlexAlign.Start : FlexAlign.Center;
        startHolder.MinimumSize = new Size(0, start ? (int)MathF.Round(1.5f * em) : 0);
        TabStop = interactive;
        Cursor = interactive && !Enabled ? Cursors.No : Cursors.Default;
        AccessibleRole = Href is not null ? AccessibleRole.Link : interactive ? AccessibleRole.PushButton : AccessibleRole.ListItem;
        // 子控件不能半透明（上游整条 opacity disabled）：标题跟着 ForeColor 换成不可用色，说明也换
        description.Tone = Faded ? TextTone.Inherit : TextTone.Muted;
        OnForeColorChanged(EventArgs.Empty);
        Invalidate(true);
    }

    /// <summary>整条可点又不可用：文字换成不可用色。</summary>
    bool Faded => IsInteractive && !Enabled;

    /// <summary>平时 text，整条可点又不可用时 textDisabled；没写色调的标题、说明、图标跟着它。</summary>
    public override Color ForeColor
    {
        get
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            return Faded ? colors.TextDisabled : colors.Text;
        }
        set => base.ForeColor = value;
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyLook();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyLook();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        ApplyLook();
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
        if (e.Button == MouseButtons.Left) PerformClick();
    }

    /// <summary>点这一条：发 Invoked，有地址时用系统打开；不可点、不可用时不执行。</summary>
    public void PerformClick()
    {
        if (!IsInteractive || !Enabled) return;
        invoked?.Invoke(this, EventArgs.Empty);
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(href);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        PerformClick();
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

    /// <summary>
    /// 底色（hoverable、整条可点时悬停 fillHover；已选 accentSoft，悬停 accentSoftHover）、sm 圆角、下边的分隔线（borderMuted）、
    /// 键盘聚焦时的焦点环（宽 2，叠在边上：上游 outline 偏移 -1px）。整条可点又不可用时整体变淡（底色、焦点环不画，文字是不可用色）。
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var interactive = IsInteractive;
        var line = Padding.Bottom - (int)MathF.Round((List?.Density == ItemListDensity.Compact ? Tokens.Space.Xs : Tokens.Space.Sm) * Em);
        var box = new RectangleF(0, 0, Width, Height - line);
        var rounded = interactive || List?.Hoverable == true;
        var radius = rounded ? theme.Radii.Sm * Em : 0;
        var active = Enabled || !interactive;
        Color? fill = !active ? null : IsSelected ? hovered ? theme.Colors.AccentSoftHover : theme.Colors.AccentSoft : hovered && rounded ? theme.Colors.FillHover : null;
        if (fill is { } color)
        {
            using var path = Drawing.RoundedRectangle(box, radius);
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
        if (line > 0)
        {
            using var divider = new SolidBrush(theme.Colors.BorderMuted);
            graphics.FillRectangle(divider, 0, Height - line, Width, line);
        }
        if (Focused && ShowFocusCues && active)
        {
            var width = LogicalToDeviceUnits(2);
            using var path = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), radius);
            using var pen = new Pen(theme.Colors.FocusRing, width);
            graphics.DrawPath(pen, path);
        }
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ItemAccessibleObject(this);

    /// <summary>读屏：名称是标题，说明是描述；已选的是选中的。</summary>
    sealed class ItemAccessibleObject(ItemListItemControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : owner.Text;

        public override string? Description => owner.AccessibleDescription is { Length: > 0 } description ? description : owner.Description is { Length: > 0 } text ? text : null;

        public override AccessibleStates State => base.State | (owner.IsSelected ? AccessibleStates.Selected : 0) | (owner.IsInteractive && !owner.Enabled ? AccessibleStates.Unavailable : 0);

        public override string? DefaultAction => owner.IsInteractive ? "Press" : null;

        public override void DoDefaultAction()
        {
            if (owner.IsInteractive) owner.PerformClick();
        }
    }
}
