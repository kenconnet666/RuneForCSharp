using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 固钉（vue-tsx-pro 的 Affix）：滚动到某个位置后贴住滚动区域的顶部（或底部）不走，如表单的提交栏、长表格上方的筛选条、文章的目录。
/// <code>
/// var affix = new AffixControl { OffsetTop = 8, Content = filterBar };
/// page.Controls.Add(affix);   // page 是 ScrollAreaControl 的内容（或它里面的一层）
/// </code>
/// 同 CSS 的 position: sticky：不脱离排版（原来的位置照样占着），只在父控件的范围里生效（父控件滚出去时跟着走）。WinForms 的控件不能画到自己外面：
/// 贴住时内容挪到滚动区域上面一层、停在顶部（或底部），原来的位置留着同样大小的空位；滚回去时内容回到原位。
/// 滚动区域是 <see cref="Target"/>，不写时是外层最近的 ScrollAreaControl。<see cref="IsStuck"/> 变了报告 <see cref="StuckChanged"/>。
/// 写了 <see cref="OffsetBottom"/>（没写 <see cref="OffsetTop"/>）时贴在底部。WinForms 的控件画不出阴影，贴住时没有阴影（<see cref="Shadow"/> 只记下意图）。
/// 偏移按 96 DPI 的像素算。
/// </summary>
[DefaultEvent(nameof(StuckChanged))]
[Description("固钉：滚过时贴住滚动区域的顶部或底部，不脱离排版。")]
public class AffixControl : Control
{
    ScrollAreaControl? scroller;
    Control? parent;
    bool moving;

    /// <summary>新建：大小跟着内容。</summary>
    public AffixControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
    }

    /// <summary>贴住时离顶部多远（按 96 DPI 的像素），默认 0；与 OffsetBottom 二选一。</summary>
    [Category("Behavior")]
    [Description("贴住时离顶部多远（按 96 DPI 的像素）。")]
    [DefaultValue(null)]
    public int? OffsetTop { get; set { field = value; Reposition(); } }

    /// <summary>贴住时离底部多远：贴在底部。</summary>
    [Category("Behavior")]
    [Description("贴住时离底部多远（按 96 DPI 的像素）：贴在底部。")]
    [DefaultValue(null)]
    public int? OffsetBottom { get; set { field = value; Reposition(); } }

    /// <summary>滚动的区域；不写时是外层最近的 ScrollAreaControl。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScrollAreaControl? Target { get; set { field = value; Attach(); } }

    /// <summary>贴住时加阴影（WinForms 画不出，只记下意图）。</summary>
    [Category("Appearance")]
    [Description("贴住时加阴影（WinForms 的控件画不出阴影）。")]
    [DefaultValue(true)]
    public bool Shadow { get; set; } = true;

    /// <summary>内容。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
            {
                field.SizeChanged -= OnContentResized;
                field.Parent?.Controls.Remove(field);
            }
            field = value;
            if (value is null) return;
            value.SizeChanged += OnContentResized;
            Controls.Add(value);
            PerformLayout();
        }
    }

    /// <summary>贴住了。</summary>
    [Browsable(false)]
    public bool IsStuck { get; private set; }

    /// <summary>贴住、离开时（看 <see cref="IsStuck"/>）。</summary>
    public event EventHandler? StuckChanged;

    bool AtBottom => OffsetBottom is not null && OffsetTop is null;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Content switch
    {
        null => Size.Empty,
        // 关掉 AutoSize 的内容有自己的大小（同 FlexControl 对待子控件）
        { AutoSize: false } content => content.Size,
        // 自动大小的内容按固钉的宽度量（贴住时内容不在里面，量法不变，原位的空位就是内容的大小）
        var content => content.GetPreferredSize(proposedSize),
    };

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Content is { } content && content.Parent == this && !moving) PlaceHome(content);
        Reposition();
    }

    void OnContentResized(object? sender, EventArgs e)
    {
        if (!moving && Content?.Parent == this) Parent?.PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Attach();
    }

    /// <summary>外层的滚动区域可能是后来才套上的（先建内容再放进 ScrollAreaControl）：显示出来时再找一次。</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Attach();
    }

    /// <inheritdoc />
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        Reposition();
    }

    void Attach()
    {
        if (parent is not null) parent.SizeChanged -= OnHostResized;
        parent = Parent;
        if (parent is not null) parent.SizeChanged += OnHostResized;
        var next = Target;
        for (var node = Parent; next is null && node is not null; node = node.Parent) next = node as ScrollAreaControl;
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        Release();
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        Reposition();
    }

    void OnHostResized(object? sender, EventArgs e) => Reposition();

    void OnScrolled(object? sender, EventArgs e) => Reposition();

    /// <summary>控件左上角在 <paramref name="ancestor"/> 里的位置（不用句柄：一层层加位置）。</summary>
    static Point OffsetIn(Control control, Control ancestor)
    {
        var point = Point.Empty;
        for (var node = control; node is not null && node != ancestor; node = node.Parent) point.Offset(node.Location);
        return point;
    }

    /// <summary>
    /// 原来的位置滚到离顶部不到 OffsetTop 时贴住（不超出父控件的底边）；贴底时同理（不超出父控件的顶边）。
    /// 贴住时内容挪到滚动区域上面一层，停在钉住的位置；没贴住时回到原位。
    /// </summary>
    void Reposition()
    {
        if (scroller is null || Content is not { } content || Parent is not { } host || !scroller.Contains(this))
        {
            Pin(0);
            return;
        }
        var at = OffsetIn(this, scroller);
        var hostTop = OffsetIn(host, scroller).Y;
        var height = Height;
        int shift;
        if (AtBottom)
            shift = Math.Min(0, Math.Max(Math.Min(0, scroller.ClientSize.Height - LogicalToDeviceUnits(OffsetBottom ?? 0) - (at.Y + height)), hostTop - at.Y));
        else
            shift = Math.Max(0, Math.Min(Math.Max(0, LogicalToDeviceUnits(OffsetTop ?? 0) - at.Y), hostTop + host.Height - (at.Y + height)));
        Pin(shift);
        if (shift != 0) content.Bounds = new Rectangle(at.X, at.Y + shift, Width, height);
        // 原位的空位跟着内容的大小（固钉的大小按内容算，内容挪走了也一样）
        if (shift != 0 && GetPreferredSize(new Size(Width, 0)).Height != Height) Parent?.PerformLayout();
    }

    void Pin(int shift)
    {
        if (shift != 0 && scroller is not null && Content is { } content && content.Parent != scroller)
        {
            moving = true;
            scroller.Controls.Add(content);
            content.BringToFront();
            moving = false;
        }
        else if (shift == 0) Release();
        var stuck = shift != 0;
        if (stuck == IsStuck) return;
        IsStuck = stuck;
        StuckChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>内容回到原位。</summary>
    void Release()
    {
        if (Content is not { } content || content.Parent == this) return;
        moving = true;
        Controls.Add(content);
        PlaceHome(content);
        moving = false;
    }

    /// <summary>内容在原位：左上角、宽度同固钉（高度是内容自己的，固钉的高度跟着它）。</summary>
    void PlaceHome(Control content)
    {
        content.Location = Point.Empty;
        if (Width > 0) content.Width = Width;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
            if (parent is not null) parent.SizeChanged -= OnHostResized;
            // 挪到滚动区域上的内容跟着释放
            if (Content is { } content && content.Parent != this) content.Dispose();
        }
        base.Dispose(disposing);
    }
}
