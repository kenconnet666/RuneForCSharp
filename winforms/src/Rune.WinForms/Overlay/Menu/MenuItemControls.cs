using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 按钮类菜单项共有的（vue-tsx-pro 的 useButtonItem、menuItemContent）：起始边的勾选标记（复选、单选项）、图标、文字（<see cref="Text"/>）、
/// 文字下方的说明（<see cref="Description"/>，弱化；读屏当作描述，不算进名称）、结束边的快捷键提示。
/// 列表行的外观：控件高度、上下 space.xs、左右 space.sm、sm 圆角，悬停 fillHover；不可用时文字变淡、方向键跳过；键盘聚焦时里侧焦点环。
/// 执行（点击、Enter、空格、快捷键）时发 Click，点击后按 <see cref="CloseOnSelect"/> 关闭菜单。
/// </summary>
[DefaultEvent(nameof(Click))]
public abstract class MenuItemControlBase : FlexControl
{
    readonly IconControl indicator = new() { Visible = false };
    readonly IconControl icon = new() { Visible = false };
    readonly FlexControl body = new() { Direction = FlexDirection.Column, AutoSize = true };
    readonly TextControl label = new();
    readonly SmallControl description = new() { Tone = TextTone.Muted, TextSize = TextSize.Sm, Visible = false };
    readonly SmallControl shortcut = new() { Tone = TextTone.Muted, TextSize = TextSize.Sm, Visible = false };

    /// <summary>子菜单的箭头（只有子菜单那一项显示）。</summary>
    private protected readonly IconControl Chevron = new() { Data = Lucide.ChevronRight, Visible = false };

    bool hovered;

    /// <summary>新建：标记、图标、文字与说明、快捷键、箭头横排，竖直居中。</summary>
    protected MenuItemControlBase()
    {
        Gap = Space.Sm;
        Align = FlexAlign.Center;
        AutoSize = true;
        AccessibleRole = AccessibleRole.MenuItem;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        // 点击由 OnMouseClick 自己执行：平台的 StandardClick 会再发一次 Click
        SetStyle(ControlStyles.StandardClick, false);
        BackColor = Color.Transparent;
        TabStop = false;
        body.Controls.AddRange([label, description]);
        Controls.AddRange([indicator, icon, body, shortcut, Chevron]);
        SetGrow(body, true);
        foreach (Control child in Controls) Forward(child);
    }

    /// <summary>文字前的图标。</summary>
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

    /// <summary>文字下方的说明（第二行，textMuted、sm）：读屏当作这一项的描述，不算进名称。</summary>
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

    /// <summary>快捷键（'Ctrl+D'、'Mod+Shift+P'）：结束边显示，按下时同点了这一项（菜单开没开都一样）；读屏念 KeyboardShortcut。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Shortcut
    {
        get;
        set
        {
            field = value;
            Hotkey = value is { Length: > 0 } combo ? global::Rune.WinForms.Hotkey.Parse(combo) : null;
            shortcut.Text = Hotkey?.Text ?? "";
            shortcut.Visible = Hotkey is not null;
        }
    }

    /// <summary>执行后是否关闭菜单：普通项、单选项默认关，复选项默认不关。</summary>
    [Category("Behavior")]
    public bool CloseOnSelect { get; set; } = true;

    /// <summary>解析好的快捷键；没写时为 null。</summary>
    internal Hotkey? Hotkey { get; private set; }

    /// <summary>勾选标记的图标（复选是对勾，单选是圆点）；普通项没有。</summary>
    private protected virtual IconData? Indicator => null;

    /// <summary>勾没勾上（复选、单选项）；普通项为 null。</summary>
    internal virtual bool? Checked => null;

    /// <summary>破坏性操作（危险色）。</summary>
    private protected virtual bool Danger => false;

    /// <summary>所在的一列菜单（菜单或子菜单）：沿 Parent 往外找，浮层窗口接着从弹出它的控件找。</summary>
    internal IMenuHost? Host => Ancestors().OfType<IMenuHost>().FirstOrDefault();

    /// <summary>所在的那一列。</summary>
    internal MenuList? List => Ancestors().OfType<MenuList>().FirstOrDefault();

    /// <summary>最外面的菜单（Tab 关掉整串菜单）。</summary>
    MenuControl? Root => Ancestors().OfType<MenuControl>().LastOrDefault();

    IEnumerable<Control> Ancestors()
    {
        for (var node = Parent; node is not null; node = node is FloatingForm floating ? floating.Origin : node.Parent) yield return node;
    }

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>平时 text，危险操作 dangerText，不可用 textDisabled；没写色调的文字、图标跟着它。</summary>
    public override Color ForeColor
    {
        get
        {
            var colors = ProviderControl.ThemeOf(this).Colors;
            return !Enabled ? colors.TextDisabled : Danger ? colors.DangerText : colors.Text;
        }
        set => base.ForeColor = value;
    }

    bool ShouldSerializeCloseOnSelect() => !CloseOnSelect;

    /// <summary>子控件也算这一项：点它们同点这一项，指针在它们上面也算悬停。</summary>
    void Forward(Control child)
    {
        child.MouseClick += (_, args) => OnMouseClick(args);
        child.MouseEnter += (_, _) => SetHovered(true);
        child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        foreach (Control grandchild in child.Controls) Forward(grandchild);
    }

    /// <summary>按字号：上下 space.xs、左右 space.sm，至少控件高度；标记占 1em 宽（勾没勾都对齐）；快捷键、箭头离文字 space.lg。</summary>
    void ApplyMetrics()
    {
        var em = Em;
        var (inline, block) = ((int)MathF.Round(Tokens.Space.Sm * em), (int)MathF.Round(Tokens.Space.Xs * em));
        Padding = new Padding(inline, block, inline, block);
        MinimumSize = new Size(0, (int)MathF.Ceiling(ControlHeight.Of(ElementContext.Of(this), em, Tokens.Size.Control)));
        indicator.MinimumSize = new Size((int)MathF.Round(em), 0);
        var gap = new Padding((int)MathF.Round(Tokens.Space.Lg * em), 0, 0, 0);
        shortcut.Margin = Chevron.Margin = gap;
    }

    /// <summary>勾选标记：有标记的项占一列，没勾时只占位（图标留空）。</summary>
    private protected void UpdateMark()
    {
        indicator.Visible = Indicator is not null;
        indicator.Data = Checked == true ? Indicator : null;
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyMetrics();
        UpdateMark();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        OnForeColorChanged(EventArgs.Empty);
        Invalidate(true);
    }

    void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        OnHoverChanged(value);
        Invalidate();
    }

    /// <summary>指针进出这一项（子菜单按它打开、关上）。</summary>
    private protected virtual void OnHoverChanged(bool hovering)
    {
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

    /// <summary>执行这一项（点击、Enter、空格）：发 Click，按 <see cref="CloseOnSelect"/> 关闭菜单；不可用时不执行。</summary>
    public virtual void PerformClick()
    {
        if (!Enabled) return;
        Run();
        if (CloseOnSelect) Host?.CloseFromItem();
    }

    /// <summary>执行这一项（也是快捷键执行的）：发 Click。</summary>
    internal virtual void Run() => OnClick(EventArgs.Empty);

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    /// <summary>Enter、空格执行；↑↓、Home、End 在这一列里移动；子菜单里 ←（从右到左时 →）回到打开它的那一项。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var back = RightToLeft == RightToLeft.Yes ? Keys.Right : Keys.Left;
        if (e.KeyCode is Keys.Enter or Keys.Space) PerformClick();
        else if (e.KeyCode == back && Host is MenuSubControl sub) sub.CloseToItem(OpenChangeReason.Escape);
        else if (List?.MoveFocus(this, e.KeyCode) != true) return;
        e.Handled = true;
    }

    /// <summary>按了一个字：跳到以它开头的项。</summary>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!e.Handled && !char.IsControl(e.KeyChar) && List?.TypeAhead(this, e.KeyChar) == true) e.Handled = true;
    }

    /// <summary>Tab：关掉整串菜单，焦点从触发器往后（Shift+Tab 往前）走。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) != Keys.Tab || Root is not { } root) return base.ProcessDialogKey(keyData);
        root.TabOut(keyData.HasFlag(Keys.Shift));
        return true;
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

    /// <summary>底色（悬停 fillHover，危险操作悬停 dangerSoft）、sm 圆角、键盘聚焦时的焦点环。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var radius = theme.Radii.Sm * Em;
        if (Enabled && hovered)
        {
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
            using var brush = new SolidBrush(Danger ? theme.Colors.DangerSoft : theme.Colors.FillHover);
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

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new MenuItemAccessibleObject(this);

    /// <summary>读屏：菜单项；名称是文字，说明是描述，快捷键是 KeyboardShortcut；复选、单选项报勾选状态。</summary>
    private protected class MenuItemAccessibleObject(MenuItemControlBase owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name : owner.Text;

        public override string? Description => owner.AccessibleDescription is { Length: > 0 } text ? text : owner.Description is { Length: > 0 } description ? description : null;

        public override string? KeyboardShortcut => owner.Hotkey?.AccessKey;

        public override AccessibleStates State =>
            base.State | (owner.Checked == true ? AccessibleStates.Checked : 0) | (owner.Enabled ? 0 : AccessibleStates.Unavailable);

        public override string? DefaultAction => "Press";

        public override void DoDefaultAction() => owner.PerformClick();
    }
}

/// <summary>菜单项（vue-tsx-pro 的 MenuItem）：执行一个操作，Click（TS 的 onAction）；<see cref="Tone"/> 为 danger 时是破坏性操作（删除）。</summary>
[ToolboxItem(true)]
[Description("菜单项：执行一个操作。")]
public class MenuItemControl : MenuItemControlBase
{
    /// <summary>色调：删除等破坏性操作用 danger，默认 neutral。</summary>
    [Category("Appearance")]
    public MenuItemTone Tone
    {
        get => field.Value is null ? MenuItemTone.Neutral : field;
        set
        {
            field = value;
            OnForeColorChanged(EventArgs.Empty);
            Invalidate(true);
        }
    }

    bool ShouldSerializeTone() => Tone != MenuItemTone.Neutral;

    private protected override bool Danger => Tone == MenuItemTone.Danger;
}

/// <summary>链接菜单项（vue-tsx-pro 的 MenuLinkItem）：<see cref="Href"/> 是绝对地址，执行时用系统打开（桌面没有站内地址：应用里的跳转用 MenuItemControl 的 Click）。</summary>
[ToolboxItem(true)]
[Description("链接菜单项：执行时用系统打开地址。")]
public class MenuLinkItemControl : MenuItemControlBase
{
    /// <summary>地址（绝对地址）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public Uri? Href { get; set; }

    /// <inheritdoc />
    internal override void Run()
    {
        base.Run();
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(href);
    }
}

/// <summary>复选菜单项（vue-tsx-pro 的 MenuCheckboxItem）：执行时切换 <see cref="IsChecked"/>；默认执行后不关闭菜单（常连续勾选几项）。</summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(IsCheckedChanged))]
[Description("复选菜单项：执行时切换勾选。")]
public class MenuCheckboxItemControl : MenuItemControlBase
{
    /// <summary>新建：执行后不关菜单。</summary>
    public MenuCheckboxItemControl() => CloseOnSelect = false;

    /// <summary>是否勾选。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsChecked
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            UpdateMark();
            IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>勾选变了。</summary>
    public event EventHandler? IsCheckedChanged;

    private protected override IconData? Indicator => Lucide.Check;

    internal override bool? Checked => IsChecked;

    /// <inheritdoc />
    internal override void Run()
    {
        IsChecked = !IsChecked;
        base.Run();
    }
}

/// <summary>单选菜单项（vue-tsx-pro 的 MenuRadioItem）：放在 <see cref="MenuRadioGroupControl"/> 里，组的 Value 等于 <see cref="Value"/> 时选中；执行时选中它。</summary>
[ToolboxItem(true)]
[Description("单选菜单项：放在单选组里。")]
public class MenuRadioItemControl : MenuItemControlBase
{
    /// <summary>这一项的值。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            field = value;
            UpdateMark();
        }
    }

    MenuRadioGroupControl? Group => Parent as MenuRadioGroupControl;

    private protected override IconData? Indicator => Lucide.Dot;

    internal override bool? Checked => Group?.Value is { } value && value == Value;

    /// <summary>组的值变了：换标记。</summary>
    internal void Sync() => UpdateMark();

    /// <inheritdoc />
    internal override void Run()
    {
        if (Group is { } group) group.Value = Value;
        base.Run();
    }
}

/// <summary>
/// 菜单里的一组项（vue-tsx-pro 的 MenuGroup）：<see cref="Label"/> 是组内第一行的标题（textMuted、sm，上下 space.xs、左右 space.sm），
/// 同时是分组的读屏名称。项直接加进 Controls（标题是第一个子控件）。
/// </summary>
[ToolboxItem(true)]
[Description("菜单里的一组项，带标题。")]
public class MenuGroupControl : FlexControl
{
    readonly SmallControl label = new() { Tone = TextTone.Muted, TextSize = TextSize.Sm, Visible = false };

    /// <summary>新建：标题与组里的项竖着排。</summary>
    public MenuGroupControl()
    {
        Direction = FlexDirection.Column;
        Align = FlexAlign.Stretch;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        Controls.Add(label);
    }

    /// <summary>分组的标题。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Label
    {
        get;
        set
        {
            field = value;
            label.Text = value ?? "";
            label.Visible = label.Text.Length > 0;
            AccessibleName = value;
        }
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyMetrics();
    }

    /// <summary>标题的外边距：上下 space.xs、左右 space.sm（按标题自己的 sm 字号）。</summary>
    void ApplyMetrics()
    {
        var em = label.Font.SizeInPoints * DeviceDpi / 72f;
        var (inline, block) = ((int)MathF.Round(Tokens.Space.Sm * em), (int)MathF.Round(Tokens.Space.Xs * em));
        label.Margin = new Padding(inline, block, inline, block);
    }
}

/// <summary>一组单选菜单项（vue-tsx-pro 的 MenuRadioGroup）：同一时间只选中一项，<see cref="Value"/> 是选中项的值。</summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("一组单选菜单项。")]
public class MenuRadioGroupControl : MenuGroupControl
{
    /// <summary>选中项的值。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var item in Controls.OfType<MenuRadioItemControl>()) item.Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>选中项变了。</summary>
    public event EventHandler? ValueChanged;
}
