using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

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
