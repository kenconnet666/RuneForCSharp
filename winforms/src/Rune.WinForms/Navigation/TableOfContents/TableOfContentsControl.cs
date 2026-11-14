using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 本页目录（vue-tsx-pro 的 TableOfContents）：列出页面的各节标题，滚动时高亮正在读的一节，侧边的指示条滑过去。
/// <code>
/// var toc = new TableOfContentsControl { ScrollRoot = article, Items = [new("install", "安装", 2), new("npm", "用 npm 安装", 3)] };
/// </code>
/// 目录项是 <see cref="HeadingEntry"/>（标题控件的 Name、文字与级别，2 级起每深一级多缩进一档）；标题按 Name 在 <see cref="ScrollRoot"/> 的内容里找。
/// 点目录项把那一节滚到顶上（TS 的页内锚点）。当前节 = 顶部已经越过视口顶边的最后一节；滚到底时最后几节到不了顶边，取视口里的最后一节。
/// 当前节的链接 accentText，读屏里是选中的链接（TS 的 aria-current="location"）。标题默认取语言包（本页目录），同时是导航的名称。
/// </summary>
[ToolboxItem(true)]
[Description("本页目录：列出各节标题，滚动时高亮正在读的一节。")]
public class TableOfContentsControl : FlexControl
{
    readonly TextControl title = new() { TextSize = TextSize.Sm, Weight = TextWeight.Semibold };
    readonly LinkList list;
    ScrollAreaControl? watched;

    /// <summary>新建：标题与左边一条细线的列表。</summary>
    public TableOfContentsControl()
    {
        Direction = FlexDirection.Column;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        list = new LinkList(this);
        Controls.AddRange([title, list]);
    }

    /// <summary>目录项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<HeadingEntry>? Items
    {
        get;
        set
        {
            field = value;
            list.Rebuild(value ?? []);
            Spy();
        }
    }

    /// <summary>目录的标题；不写时取语言包。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title
    {
        get;
        set
        {
            field = value;
            UpdateTitle();
        }
    }

    /// <summary>页面在哪个滚动区域里。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public ScrollAreaControl? ScrollRoot
    {
        get => watched;
        set
        {
            if (watched is not null) watched.ScrollChanged -= OnScrollChanged;
            watched = value;
            if (value is not null) value.ScrollChanged += OnScrollChanged;
            Spy();
        }
    }

    /// <summary>当前节的标题控件 Name；还没滚到第一节时为空。</summary>
    [Browsable(false)]
    public string? Current { get; private set; }

    /// <summary>目录链接（测试用）。</summary>
    internal IEnumerable<TableOfContentsLink> Links => list.Controls.OfType<TableOfContentsLink>();

    void OnScrollChanged(object? sender, EventArgs e) => Spy();

    void UpdateTitle() => title.Text = Title ?? ProviderControl.LangOf(this).TableOfContents;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        UpdateTitle();
    }

    /// <summary>标题下面留 space.sm（按标题的字号）；链接按级别缩进都按字号，字体或 DPI 变了重新算。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        title.Margin = new Padding(0, 0, 0, (int)MathF.Round(0.5f * Tokens.FontSize.Sm * DeviceDpi / 96f));
        base.OnLayout(levent);
    }

    /// <summary>标题：按 Name 在滚动区域的内容里找。</summary>
    Control? Heading(string id) => ScrollRoot?.Content?.Controls.Find(id, true).FirstOrDefault();

    /// <summary>
    /// 滚动高亮（TS 的 useScrollSpy）：顶部已经越过视口顶边（+1 容许取整误差）的最后一节；滚到底时最后几节可能永远到不了顶边，取视口里的最后一节。
    /// </summary>
    internal void Spy()
    {
        if (ScrollRoot is not { Content: { } content } scroll || Items is null) return;
        var atBottom = scroll.ScrollOffset.Y + scroll.ClientSize.Height >= content.Height - 1;
        string? next = null;
        foreach (var item in Items)
        {
            if (Heading(item.Id) is not { } heading) continue;
            var top = scroll.PointToClient(heading.PointToScreen(Point.Empty)).Y;
            if (top <= 1 || (atBottom && top < scroll.ClientSize.Height)) next = item.Id;
        }
        if (next == Current) return;
        Current = next;
        foreach (var link in Links) link.SetCurrent(link.Entry.Id == next);
        list.PlaceIndicator();
    }

    /// <summary>点了目录项：那一节滚到视口顶上。</summary>
    internal void Go(HeadingEntry entry)
    {
        if (ScrollRoot is not { } scroll || Heading(entry.Id) is not { } heading) return;
        var top = scroll.PointToClient(heading.PointToScreen(Point.Empty)).Y;
        scroll.ScrollOffset = new Point(scroll.ScrollOffset.X, scroll.ScrollOffset.Y + top);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new TocAccessibleObject(this);

    sealed class TocAccessibleObject(TableOfContentsControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : owner.title.Text;
    }

    /// <summary>链接列表：左边一条 xs 宽的 border 细线，当前节的链接旁一条 sm 宽、圆角的品牌色指示条（盖住细线，滑过去）。</summary>
    sealed class LinkList : FlexControl
    {
        readonly TableOfContentsControl owner;
        readonly Signal<double> indicatorTop = new(0);
        readonly Signal<double> indicatorHeight = new(0);
        readonly Effect paint;
        bool placed;

        public LinkList(TableOfContentsControl toc)
        {
            owner = toc;
            Direction = FlexDirection.Column;
            AutoSize = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            paint = new Effect(Invalidate);
        }

        public void Rebuild(IReadOnlyList<HeadingEntry> items)
        {
            Controls.Cast<Control>().DisposeAll();
            foreach (var item in items) Controls.Add(new TableOfContentsLink(owner, item));
            Padding = new Padding(LogicalToDeviceUnits(1), 0, 0, 0);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            PlaceIndicator();
        }

        /// <summary>指示条对准当前节的链接：第一次直接到位，之后滑过去（durations.md、standard）。</summary>
        public void PlaceIndicator()
        {
            if (Controls.OfType<TableOfContentsLink>().FirstOrDefault(link => link.IsCurrent) is not { Height: > 0 } current)
            {
                Invalidate();
                return;
            }
            var duration = placed ? Tokens.Duration.Md : TimeSpan.Zero;
            placed = true;
            Animator.Animate(indicatorTop, current.Top, duration, Easing.Standard);
            Animator.Animate(indicatorHeight, current.Height, duration, Easing.Standard);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var _ = paint.Track();
            var colors = ProviderControl.ThemeOf(this).Colors;
            var graphics = e.Graphics;
            var line = LogicalToDeviceUnits(1);
            using (var border = new SolidBrush(colors.Border)) graphics.FillRectangle(border, 0, 0, line, Height);
            var (top, height) = ((float)indicatorTop.Value, (float)indicatorHeight.Value);
            if (owner.Current is null || height <= 0) return;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var width = LogicalToDeviceUnits(2);
            using var path = Drawing.RoundedRectangle(new RectangleF(0, top, width, height), width / 2f);
            using var accent = new SolidBrush(colors.Accent);
            graphics.FillPath(accent, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Animator.Stop(indicatorTop);
                Animator.Stop(indicatorHeight);
                paint.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>目录里的一项：标题控件的 Name（TS 的 id）、文字与级别（2 是二级标题）。</summary>
/// <param name="Id">标题控件的 Name。</param>
/// <param name="Label">标题文字。</param>
/// <param name="Level">级别：2 起，每深一级多缩进一档。</param>
public sealed record HeadingEntry(string Id, string Label, int Level);

/// <summary>
/// 目录链接（TS 的 a）：上下 space.xs、右边 space.xs，左边按级别缩进（每档 0.75em），sm 字号、行高 snug；平时 textMuted，悬停 text，当前节 accentText（悬停也是）。
/// 键盘聚焦时焦点环。读屏里是链接，当前节是选中的。
/// </summary>
public sealed class TableOfContentsLink : Control
{
    readonly TableOfContentsControl owner;
    bool hovered;

    internal TableOfContentsLink(TableOfContentsControl toc, HeadingEntry entry)
    {
        owner = toc;
        Entry = entry;
        Text = entry.Label;
        AccessibleRole = AccessibleRole.Link;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>对应的目录项。</summary>
    public HeadingEntry Entry { get; }

    /// <summary>是不是当前节。</summary>
    public bool IsCurrent { get; private set; }

    Font LinkFont => Fonts.Get(Font.FontFamily.Name, Tokens.FontSize.Sm * 72 / 96, FontStyle.Regular);

    float Em => Tokens.FontSize.Sm * DeviceDpi / 96f;

    (int Left, int Block) Insets => ((int)MathF.Round(Math.Max(Entry.Level - 1, 1) * 0.75f * Em), (int)MathF.Round(0.25f * Em));

    internal void SetCurrent(bool value)
    {
        IsCurrent = value;
        Invalidate();
    }

    /// <summary>宽度撑满列表；高度是一行（行高 snug）加上下 space.xs。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var (left, block) = Insets;
        return new Size(left + TextRenderer.MeasureText(Text, LinkFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + block,
            (int)MathF.Round(1.375f * Em) + 2 * block);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Activate();
    }

    /// <summary>把那一节滚到顶上。</summary>
    public void Activate() => owner.Go(Entry);

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Enter) return;
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

    /// <summary>文字（一行，放不下时省略号）与键盘聚焦时的焦点环。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        var (left, block) = Insets;
        var color = IsCurrent ? colors.AccentText : hovered ? colors.Text : colors.TextMuted;
        TextRenderer.DrawText(e.Graphics, Text, LinkFont, new Rectangle(left, 0, Math.Max(0, Width - left - block), Height), color,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (!Focused || !ShowFocusCues) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var width = LogicalToDeviceUnits(2);
        using var path = Drawing.RoundedRectangle(new RectangleF(width / 2f, width / 2f, Width - width, Height - width), theme.Radii.Xs * Em);
        using var pen = new Pen(colors.FocusRing, width);
        e.Graphics.DrawPath(pen, path);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new LinkAccessibleObject(this);

    sealed class LinkAccessibleObject(TableOfContentsLink owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.IsCurrent ? AccessibleStates.Selected : 0) | AccessibleStates.Linked;

        public override string DefaultAction => "跳转";

        public override void DoDefaultAction() => owner.Activate();
    }
}
