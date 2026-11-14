using System.ComponentModel;
namespace Rune.WinForms;

/// <summary>子控件在格子里怎么竖着放。</summary>
enum PaneAlign
{
    /// <summary>贴着上边、按想要的高度（正文、侧栏的内容）。</summary>
    Top,

    /// <summary>竖直居中、按想要的高度（顶栏里的各格）。</summary>
    Center,

    /// <summary>占满高度（顶栏里的导航）。</summary>
    Stretch,
}

/// <summary>
/// 外壳的一格：底色、某几边的细线、内边距，里面放一个子控件（插槽的内容）。颜色取主题，换主题时重画。
/// 子控件横向占满内容区（<see cref="FitChild"/> 时按想要的宽度），竖向按 <see cref="Align"/>。
/// </summary>
sealed class ShellPane : Control
{
    readonly Effect paint;

    public ShellPane()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        paint = new Effect(Invalidate);
    }

    /// <summary>底色；不写时透明（父控件的底色透出来）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ThemeColors, Color>? Fill { get; init; }

    /// <summary>细线的颜色。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ThemeColors, Color>? BorderColor { get; init; }

    /// <summary>哪几边有细线（xs 宽）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnchorStyles Edges { get; set { field = value; Invalidate(); PerformLayout(); } }

    /// <summary>子控件竖着怎么放。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PaneAlign Align { get; set { field = value; PerformLayout(); } }

    /// <summary>子控件按想要的宽度、靠起始边（品牌），不占满一行。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FitChild { get; init; }

    /// <summary>页脚：里面的文字 sm、textMuted。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Muted { get; init; }

    /// <summary>里面的内容。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Child
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is not null) Controls.Add(value);
            PerformLayout();
        }
    }

    /// <summary>细线的粗细（xs，按 DPI）。</summary>
    internal int Line => LogicalToDeviceUnits(1);

    /// <summary>细线占的位置：在内边距之外。</summary>
    Padding EdgeInsets => new(Edges.HasFlag(AnchorStyles.Left) ? Line : 0, Edges.HasFlag(AnchorStyles.Top) ? Line : 0,
        Edges.HasFlag(AnchorStyles.Right) ? Line : 0, Edges.HasFlag(AnchorStyles.Bottom) ? Line : 0);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var insets = Padding + EdgeInsets;
        if (Child is not { Visible: true } child) return new Size(insets.Horizontal, insets.Vertical);
        var inner = proposedSize.Width > 0 ? new Size(Math.Max(0, proposedSize.Width - insets.Horizontal), 0) : Size.Empty;
        var size = child.GetPreferredSize(inner);
        return new Size(size.Width + insets.Horizontal, size.Height + insets.Vertical);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Child is not { } child) return;
        var area = new Rectangle(Point.Empty, ClientSize);
        var insets = Padding + EdgeInsets;
        area = new Rectangle(area.X + insets.Left, area.Y + insets.Top, Math.Max(0, area.Width - insets.Horizontal), Math.Max(0, area.Height - insets.Vertical));
        var wanted = child.GetPreferredSize(new Size(area.Width, 0));
        var width = FitChild ? Math.Min(wanted.Width, area.Width) : area.Width;
        var x = area.X;
        var height = Align == PaneAlign.Stretch ? area.Height : Align == PaneAlign.Center ? Math.Min(wanted.Height, area.Height) : wanted.Height;
        var y = Align == PaneAlign.Center ? area.Y + (area.Height - height) / 2 : area.Y;
        child.Bounds = new Rectangle(x, y, width, height);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var _ = paint.Track();
        if (Fill is null)
        {
            base.OnPaintBackground(e);
            return;
        }
        using var brush = new SolidBrush(Fill(ProviderControl.ThemeOf(this).Colors));
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var colors = ProviderControl.ThemeOf(this).Colors;
        if (Muted && Child is not null) Child.ForeColor = colors.TextMuted;
        if (BorderColor is null || Edges == AnchorStyles.None) return;
        using var brush = new SolidBrush(BorderColor(colors));
        var line = Line;
        if (Edges.HasFlag(AnchorStyles.Top)) e.Graphics.FillRectangle(brush, 0, 0, Width, line);
        if (Edges.HasFlag(AnchorStyles.Bottom)) e.Graphics.FillRectangle(brush, 0, Height - line, Width, line);
        if (Edges.HasFlag(AnchorStyles.Left)) e.Graphics.FillRectangle(brush, 0, 0, line, Height);
        if (Edges.HasFlag(AnchorStyles.Right)) e.Graphics.FillRectangle(brush, Width - line, 0, line, Height);
    }
}

/// <summary>正文那一列：正文在上，页脚在下；内容少时页脚贴着底部（滚动区域把这一列至少撑到视口高）。</summary>
sealed class ShellColumn : Control
{
    readonly ShellPane main;
    readonly ShellPane footer;

    public ShellColumn(ShellPane main, ShellPane footer)
    {
        this.main = main;
        this.footer = footer;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        Controls.AddRange([main, footer]);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width;
        var height = main.GetPreferredSize(new Size(width, 0)).Height + (footer.Visible ? footer.GetPreferredSize(new Size(width, 0)).Height : 0);
        return new Size(width, height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var width = ClientSize.Width;
        var footerHeight = footer.Visible ? footer.GetPreferredSize(new Size(width, 0)).Height : 0;
        main.Bounds = new Rectangle(0, 0, width, Math.Max(main.GetPreferredSize(new Size(width, 0)).Height, ClientSize.Height - footerHeight));
        footer.Bounds = new Rectangle(0, ClientSize.Height - footerHeight, width, footerHeight);
    }
}

/// <summary>
/// 窄时的侧栏抽屉：盖住整个外壳，遮罩是打开那一刻外壳的截图叠上 overlay（WinForms 的子控件不能半透明）；
/// 面板贴着起始边，宽 sizes.xs（不超过外壳），surfaceRaised、除起始边外一圈 border，标题行是标题与关闭按钮。
/// 点遮罩、关闭按钮、Esc，或面板里的导航换了当前页时关上。
/// </summary>
sealed class ShellDrawer : Control
{
    readonly AppShellControl owner;
    readonly H2Control title = new() { TextSize = TextSize.Lg };
    readonly CloseButtonControl close = new();
    readonly ScrollAreaControl scroll = new();
    readonly ShellPane content = new();
    readonly Effect paint;
    Bitmap? snapshot;

    public ShellDrawer(AppShellControl owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        Visible = false;
        paint = new Effect(Invalidate);
        Panel = new ShellPane { Fill = colors => colors.SurfaceRaised, BorderColor = colors => colors.Border, Edges = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
        scroll.Content = content;
        Panel.Controls.AddRange([title, close, scroll]);
        Panel.Layout += (_, _) => LayoutPanel();
        Controls.Add(Panel);
        close.Click += (_, _) => owner.IsSidebarOpen = false;
        MouseDown += (_, _) => owner.IsSidebarOpen = false;
    }

    /// <summary>面板（测试用）。</summary>
    public ShellPane Panel { get; }

    /// <summary>标题（语言包的"导航"）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get => title.Text; set => title.Text = value; }

    /// <summary>侧栏的内容（窄时放在这里）：里面的导航换了当前页就关上。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get => content.Child;
        set
        {
            if (content.Child == value) return;
            if (content.Child is { } old) foreach (var list in NavLists(old)) list.CurrentHrefChanged -= OnNavigated;
            content.Child = value;
            if (value is not null) foreach (var list in NavLists(value)) list.CurrentHrefChanged += OnNavigated;
        }
    }

    static IEnumerable<NavListControl> NavLists(Control root) =>
        root is NavListControl list ? [list] : root.Controls.Cast<Control>().SelectMany(NavLists);

    void OnNavigated(object? sender, EventArgs e) => owner.IsSidebarOpen = false;

    /// <summary>开关：打开时先截下外壳当遮罩的底，焦点进到关闭按钮；关上时焦点回到打开它的按钮。</summary>
    public void Toggle(bool open)
    {
        if (open == Visible) return;
        if (open)
        {
            snapshot?.Dispose();
            snapshot = null;
            if (owner.Width > 0 && owner.Height > 0)
            {
                snapshot = new Bitmap(owner.Width, owner.Height);
                owner.DrawToBitmap(snapshot, new Rectangle(Point.Empty, owner.Size));
            }
            Visible = true;
            BringToFront();
            close.Focus();
            return;
        }
        var focusInside = ContainsFocus;
        Visible = false;
        snapshot?.Dispose();
        snapshot = null;
        if (focusInside) owner.MenuButtonPart.Focus();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Panel.Bounds = new Rectangle(0, 0, Math.Min(LogicalToDeviceUnits((int)Tokens.Size.Xs), Width), Height);
    }

    /// <summary>标题行：上 space.xl、左右 space.xl，关闭按钮贴着结束边；下面是滚动区域，内容上下 space.lg、左右 space.xl。</summary>
    void LayoutPanel()
    {
        var em = this.EmPixels();
        var xl = (int)MathF.Round(Tokens.Space.Xl * em);
        var lg = (int)MathF.Round(Tokens.Space.Lg * em);
        var line = LogicalToDeviceUnits(1);
        var inner = Panel.Width - line;
        var button = close.GetPreferredSize(Size.Empty);
        var heading = title.GetPreferredSize(new Size(Math.Max(0, inner - 2 * xl - button.Width), 0));
        var rowHeight = Math.Max(button.Height, heading.Height);
        close.Bounds = new Rectangle(inner - xl - button.Width + (int)MathF.Round(Tokens.Space.Sm * em), line + xl + (rowHeight - button.Height) / 2, button.Width, button.Height);
        title.Bounds = new Rectangle(xl, line + xl + (rowHeight - heading.Height) / 2, Math.Max(0, inner - 2 * xl - button.Width), heading.Height);
        var top = line + xl + rowHeight;
        scroll.Bounds = new Rectangle(0, top, inner, Math.Max(0, Panel.Height - top - line));
        content.Padding = new Padding(xl, lg, xl, lg);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var _ = paint.Track();
        var colors = ProviderControl.ThemeOf(this).Colors;
        if (snapshot is not null) e.Graphics.DrawImage(snapshot, Point.Empty);
        else
        {
            using var background = new SolidBrush(colors.Background);
            e.Graphics.FillRectangle(background, ClientRectangle);
        }
        using var overlay = new SolidBrush(colors.Overlay);
        e.Graphics.FillRectangle(overlay, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) snapshot?.Dispose();
        base.Dispose(disposing);
    }
}
