using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 标签：里面放文字（<see cref="Control.Text"/>）或子控件（文字加计数），中等字重（GDI 只有常规与粗体，按常规画）；平时 textMuted、悬停与选中时 text；
/// 控件高度、左右 space.md、sm 圆角；soft 时选中的标签 surface 底；键盘聚焦时里侧焦点环。点击、Enter、空格切换（<see cref="TabClick"/> 先执行，
/// <see cref="TabClickEventArgs.Cancel"/> 拦下）。读屏里是选项卡。
/// 订阅了 <see cref="CloseRequested"/> 才能关闭（上游给了 onClose 才有）：末尾一个圆形的关闭按钮（不能聚焦，只给指针用），
/// 标签上按 Delete、鼠标中键点也关闭。使用方移除这个标签；关掉的是当前标签时选它后面（没有时前面）的一个，焦点也跟过去。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TabClick))]
[Description("标签：点击切换到对应的面板。")]
public class TabItemControl : FlexControl
{
    TabsControl? owner;
    bool hovered;
    EventHandler? closeRequested;
    TabCloseButtonControl? close;
    TextControl? label;

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

    /// <summary>请求关闭（关闭按钮、Delete、鼠标中键；TS 的 onClose）：使用方移除这个标签。订阅了才有关闭按钮。</summary>
    [Category("Action")]
    public event EventHandler? CloseRequested
    {
        add
        {
            closeRequested += value;
            SyncClose();
        }
        remove
        {
            closeRequested -= value;
            SyncClose();
        }
    }

    /// <summary>能关闭（订阅了 <see cref="CloseRequested"/>）。</summary>
    internal bool IsClosable => closeRequested is not null;

    /// <summary>关闭按钮（测试用）；没有订阅时为 null。</summary>
    internal CloseButtonControl? CloseButton => close;

    /// <summary>拖动排序中：文字淡一些（同上游的 0.6 不透明度）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool IsDragging
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnForeColorChanged(EventArgs.Empty);
            Invalidate(true);
        }
    }

    /// <summary>读屏、菜单里的文字：Text，没有时子控件的文字，再没有时是值。</summary>
    internal string Label => Text is { Length: > 0 } text ? text
        : string.Join(" ", Controls.Cast<Control>().Where(child => child != close && child != label).Select(child => child.Text).Where(text => text.Length > 0)) is { Length: > 0 } inner ? inner
        : Value ?? "";

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
        // 可关闭：结束端给关闭按钮留出 1.25em
        var end = inline + (IsClosable ? (int)MathF.Round(1.25f * em) : 0);
        Padding = vertical ? new Padding(inline, 0, end + bar, 0) : new Padding(inline, 0, end, bar);
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
            var color = IsSelected || hovered ? colors.Text : colors.TextMuted;
            return IsDragging ? Color.FromArgb((int)(0.6f * color.A), color) : color;
        }
        set => base.ForeColor = value;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control == close) return;
        // 里面的文字、计数也算标签的一部分：点它们同点标签，按下、拖动同在标签上（拖动排序）
        var child = e.Control!;
        MouseEventArgs Translate(MouseEventArgs args) => new(args.Button, args.Clicks, args.X + child.Left, args.Y + child.Top, args.Delta);
        child.MouseClick += (_, args) => OnMouseClick(Translate(args));
        child.MouseDown += (_, args) => OnMouseDown(Translate(args));
        child.MouseMove += (_, args) => OnMouseMove(Translate(args));
        child.MouseUp += (_, args) => OnMouseUp(Translate(args));
        child.MouseEnter += (_, _) => SetHovered(true);
        child.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
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
        // 鼠标中键关闭（同浏览器的标签页）
        else if (e.Button == MouseButtons.Middle) RequestClose();
    }

    /// <summary>订阅了关闭就放上关闭按钮，退订了就拿掉。</summary>
    void SyncClose()
    {
        if (closeRequested is null)
        {
            if (close is null) return;
            Controls.Remove(close);
            close.Dispose();
            close = null;
            if (owner is not null) Apply(owner, IsSelected);
            return;
        }
        if (close is not null) return;
        close = new TabCloseButtonControl { Radius = Radius.Full, TextSize = TextSize.Xs };
        close.Click += (_, _) => RequestClose();
        Controls.Add(close);
        SetOverlay(close, true);
        UpdateCloseLabel();
        if (owner is not null) Apply(owner, IsSelected);
        PerformLayout();
    }

    /// <summary>Text 由里面的一个文字控件显示（排在子控件的最前面）；没有文字时拿掉。</summary>
    void SyncLabel()
    {
        if (Text.Length == 0)
        {
            if (label is null) return;
            Controls.Remove(label);
            label.Dispose();
            label = null;
            return;
        }
        if (label is null)
        {
            label = new TextControl();
            Controls.Add(label);
            Controls.SetChildIndex(label, 0);
        }
        label.Text = Text;
    }

    void UpdateCloseLabel()
    {
        if (close is not null) close.AccessibleName = ProviderControl.LangOf(this).TabClose(Label);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        SyncLabel();
        UpdateCloseLabel();
        (Parent as TabListControl)?.SyncMenuItems();
    }

    /// <summary>
    /// 关闭按钮叠在结束端，看得见的按钮离边 space.xs（按钮自己的字号），竖直居中。按钮四周留着画焦点环的 Padding（同 TagControl 的移除按钮），
    /// 控件比看得见的按钮大一圈。
    /// </summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (close is null) return;
        var ring = close.Padding;
        var size = close.GetPreferredSize(Size.Empty) - ring.Size;
        var margin = (int)MathF.Round(0.25f * close.Font.SizeInPoints * DeviceDpi / 72f);
        var bar = owner?.Variant != TabsVariant.Soft && owner?.Orientation == TabsOrientation.Vertical ? LogicalToDeviceUnits(2) : 0;
        var left = Width - bar - margin - size.Width;
        var top = (Height - size.Height) / 2;
        close.SetBounds(left - ring.Left, top - ring.Top, size.Width + ring.Horizontal, size.Height + ring.Vertical);
    }

    /// <summary>请求关闭（不可用时不关）。</summary>
    internal void RequestClose()
    {
        if (closeRequested is not null && Enabled) closeRequested.Invoke(this, EventArgs.Empty);
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
        // Delete 关闭（TS 的 aria-keyshortcuts="Delete"）
        if (e.KeyData == Keys.Delete && IsClosable)
        {
            RequestClose();
            e.Handled = true;
            return;
        }
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

        public override string? KeyboardShortcut => owner.IsClosable ? "Delete" : base.KeyboardShortcut;

        public override void DoDefaultAction() => owner.Activate();
    }
}

/// <summary>点击标签的参数：设 <see cref="Cancel"/> 拦下切换（TS 的 preventDefault）。</summary>
public sealed class TabClickEventArgs : EventArgs
{
    /// <summary>拦下这次切换。</summary>
    public bool Cancel { get; set; }
}

/// <summary>标签的关闭按钮：不能聚焦（按下时焦点留在原处，不进 Tab 顺序）。</summary>
sealed class TabCloseButtonControl : CloseButtonControl
{
    public TabCloseButtonControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }
}
