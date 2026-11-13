using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 卡片（vue-tsx-pro 的 Card）：把一组相关内容放在一块表面上。可选的页头、页脚与正文之间有分隔线。直接加的子控件是正文，竖着排、撑满宽度：
/// <code>
/// var order = new CardControl { Header = "订单 #1024", Footer = new ButtonControl { Text = "查看" }, Dock = DockStyle.Top };
/// order.Controls.Add(new ParagraphControl { Text = "…" });
/// </code>
/// <see cref="Header"/> 是页头的文字，<see cref="Extra"/> 是页头结束边的控件（"全部"、按钮），<see cref="Footer"/> 是页脚里靠结束边的控件。
/// 样式（surface、outline、elevated、ghost）、内边距（space 档位，默认 lg）、圆角（默认 lg）。加载中正文换成骨架（页头、页脚照常），
/// 很快完成时不闪；读屏状态里带"忙"。WinForms 画不到控件外面：elevated 的阴影不画（只有抬高的表面色与弱边框）。
/// 宽度由父容器给（停靠、锚定），高度跟着内容（AutoSize）。TS 的装饰效果（tilt、spotlight、beam）还没做。
/// </summary>
public class CardControl : Panel, IScopeAware
{
    readonly CardEngine engine;
    readonly Effect paint;
    readonly Text title = new() { Weight = TextWeight.Semibold };
    readonly SkeletonTextControl skeleton = new() { Visible = false };
    readonly CornerClip cornerClip = new();
    DelayedLoading loading;
    bool skeletonShown;

    /// <summary>新建：surface、内边距 lg、圆角 lg，高度跟着内容。</summary>
    public CardControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new CardEngine(this);
        paint = new Effect(Invalidate);
        loading = new DelayedLoading(ShowSkeleton);
        Controls.Add(skeleton);
    }

    /// <summary>样式；不写时 surface。</summary>
    [Category("Appearance")]
    [Description("样式：surface（默认）、outline、elevated、ghost。")]
    public CardVariant Variant { get => variant.Or(ScopeDefaults?.Variant, CardVariant.Surface); set { variant = value; Invalidate(); } }

    /// <summary>页头、正文、页脚的内边距档位；不写时 lg。控件自己有 Padding，TS 的 padding 在这里叫 CardPadding。</summary>
    [Category("Layout")]
    [Description("页头、正文、页脚的内边距档位，默认 lg。")]
    public Space CardPadding { get => padding.Or(ScopeDefaults?.Padding, Space.Lg); set { padding = value; PerformLayout(); Invalidate(); } }

    /// <summary>圆角；不写时 lg。</summary>
    [Category("Appearance")]
    [Description("圆角，默认 lg。")]
    public Radius Radius { get => radius.Or(ScopeDefaults?.Radius, Radius.Lg); set { radius = value; PerformLayout(); Invalidate(); } }

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="CardSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CardSx? Sx { get; set { field = value; PerformLayout(); Invalidate(); } }

    /// <summary>合并好的外观覆盖：作用域的在下，实例的在上。</summary>
    CardSx? ResolvedSx => ProviderControl.Of(this)?.ResolveStyles(Sx) ?? Sx;

    /// <summary>页头的文字（semibold）；读屏里是卡片的名称。</summary>
    [Category("Appearance")]
    [Description("页头的文字。")]
    [DefaultValue("")]
    public string Header
    {
        get => title.Content;
        set
        {
            title.Content = value;
            if (string.IsNullOrEmpty(AccessibleName)) AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>页头结束边的控件（"全部"、按钮、筛选）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Extra
    {
        get;
        set
        {
            // 页头的操作链接（"全部"）悬停才有下划线；正文里的链接照旧（上游 Card 页头的 LinkUnderlineScope）
            if (field is not null) LinkControl.SetUnderlineScope(field, null);
            Replace(ref field, value);
            if (value is not null) LinkControl.SetUnderlineScope(value, LinkUnderline.Hover);
        }
    }

    /// <summary>页脚里靠结束边的控件（按钮、一组按钮）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Footer { get; set => Replace(ref field, value); }

    /// <summary>正文加载中：换成骨架。</summary>
    [Category("Behavior")]
    [Description("正文加载中：换成几行扫光的骨架。")]
    [DefaultValue(false)]
    public bool IsLoading
    {
        get;
        set
        {
            field = value;
            if (value && !IsHandleCreated && !skeletonShown)
            {
                // 还没显示就在加载：立即显示骨架（没有内容可看，空白比骨架更糟）
                loading.Stop();
                loading = new DelayedLoading(ShowSkeleton, loading: true);
                ShowSkeleton(true);
            }
            else loading.Update(value);
        }
    }

    /// <summary>加载中的骨架（行数在它上面改，TS 的 propsSkeleton）。</summary>
    [Browsable(false)]
    public SkeletonTextControl Skeleton => skeleton;

    /// <summary>骨架在显示吗（防闪烁之后的）。</summary>
    internal bool SkeletonShown => skeletonShown;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    bool ShouldSerializeVariant() => variant.Value is not null;

    void ResetVariant() => Variant = default;

    bool ShouldSerializeCardPadding() => padding.Value is not null;

    void ResetCardPadding() => CardPadding = default;

    bool ShouldSerializeRadius() => radius.Value is not null;

    void ResetRadius() => Radius = default;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>正文的内边距：space 系数 × 字号。</summary>
    int Inset => (int)MathF.Round((CardPadding.Factor ?? 0) * Em);

    /// <summary>页头、页脚的左右、上下内边距：同正文；none 时只有正文贴边（表格、列表），页头、页脚的文字不能贴着边框，上下 sm、左右 md。</summary>
    (int Inline, int Block) EdgeInset => CardPadding == Space.None
        ? ((int)MathF.Round(Tokens.Space.Md * Em), (int)MathF.Round(Tokens.Space.Sm * Em))
        : (Inset, Inset);

    int Line => Math.Max(1, (int)MathF.Round(Tokens.BorderWidth.Xs * DeviceDpi / 96f));

    ElementContext Context => ElementContext.Of(this);

    /// <summary>作用域合并的默认值（<see cref="CardDefaults"/>）：没写的样式、内边距、圆角取它。</summary>
    CardDefaults? ScopeDefaults => ProviderControl.Of(this)?.ResolveDefaults<CardDefaults>();

    CardVariant variant;
    Space padding;
    Radius radius;

    /// <summary>所在作用域的主题、默认值变了：重新排版、重画。</summary>
    void IScopeAware.ScopeChanged()
    {
        PerformLayout();
        Invalidate(true);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 换了位置可能换了作用域
        ((IScopeAware)this).ScopeChanged();
    }

    bool HasHeader => Header.Length > 0 || Extra is not null;

    /// <summary>正文的子控件：除页头、页脚的控件与骨架外的。</summary>
    List<Control> BodyControls() => [.. Controls.Cast<Control>().Where(control => control != skeleton && control != Extra && control != Footer)];

    void Replace(ref Control? slot, Control? value)
    {
        if (slot is not null) Controls.Remove(slot);
        slot = value;
        if (value is not null) Controls.Add(value);
        PerformLayout();
        Invalidate();
    }

    void ShowSkeleton(bool shown)
    {
        skeletonShown = shown;
        skeleton.Visible = shown;
        foreach (var control in BodyControls()) control.Visible = !shown;
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (skeletonShown && e.Control != skeleton && e.Control != Extra && e.Control != Footer) e.Control!.Visible = false;
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        loading.Stop();
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var colors = ProviderControl.ThemeOf(this).Colors;
        var (fill, border) = Variant.Value switch
        {
            "outline" => ((Color?)null, (Color?)colors.Border),
            "elevated" => (colors.SurfaceRaised, colors.BorderMuted),
            "ghost" => (null, null),
            _ => (colors.Surface, colors.Border),
        };
        var sx = ResolvedSx;
        (fill, border) = (sx?.Background ?? fill, sx?.BorderColor ?? border);
        var graphics = e.Graphics;
        var line = Line;
        var radius = CornerRadius(sx);
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), radius))
        {
            if (fill is { } back)
                using (var brush = new SolidBrush(back)) graphics.FillPath(brush, shape);
            if (border is { } edge)
                using (var pen = new Pen(edge, line)) graphics.DrawPath(pen, shape);
        }
        graphics.Restore(state);
        // 分隔线：页头下面、页脚上面（borderMuted）
        using var divider = new SolidBrush(colors.BorderMuted);
        if (engine.HeaderBottom is { } headerBottom) graphics.FillRectangle(divider, line, headerBottom, Width - 2 * line, line);
        if (engine.FooterTop is { } footerTop) graphics.FillRectangle(divider, line, footerTop, Width - 2 * line, line);
        if (Header.Length > 0) title.Paint(graphics, Context with { ForeColor = colors.Text });
    }

    float CornerRadius(CardSx? sx)
    {
        var corners = sx?.Radius ?? Radius;
        return corners.Value == "full" ? Height / 2f : (corners.In(ProviderControl.ThemeOf(this).Radii) ?? 0) * Em;
    }

    /// <summary>碰到圆角的子控件按边框里面的圆角裁：内边距 none 时贴边的横幅、代码块不会盖住卡片的圆角。</summary>
    void ClipCorners()
    {
        var line = Line;
        cornerClip.Apply(Controls.Cast<Control>(), new RectangleF(line, line, Width - 2 * line, Height - 2 * line), Math.Max(0, CornerRadius(ResolvedSx) - line));
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is { } child) cornerClip.Release(child);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CardAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            paint.Dispose();
            loading.Stop();
        }
        base.Dispose(disposing);
    }

    /// <summary>读屏：一组内容，名称是页头的文字；加载中时状态带"忙"（TS 的 aria-busy）。</summary>
    sealed class CardAccessibleObject(CardControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Header : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override AccessibleStates State => owner.IsLoading ? base.State | AccessibleStates.Busy : base.State;
    }

    /// <summary>排版：页头（文字在起始边、Extra 在结束边，放不下时 Extra 换到下一行）、正文（子控件竖排或骨架）、页脚（Footer 靠结束边）。</summary>
    sealed class CardEngine(CardControl owner) : LayoutEngine
    {
        /// <summary>页头分隔线的位置；没有页头时为 null。</summary>
        public int? HeaderBottom { get; private set; }

        /// <summary>页脚分隔线的位置；没有页脚时为 null。</summary>
        public int? FooterTop { get; private set; }

        static Size SizeOf(Control control, int width) => control.AutoSize ? control.GetPreferredSize(new Size(width, 0)) : control.Size;

        /// <summary>按宽度排一遍；arrange 为 false 时只算高度。</summary>
        int Run(int width, bool arrange)
        {
            // 卡片被拉得比内容高（网格里拉齐的一排）：多出来的给正文，页脚贴着底边（上游 body 的 flex-grow）
            var slack = arrange ? Math.Max(0, owner.ClientSize.Height - Run(width, arrange: false)) : 0;
            var (inset, edge, line) = (owner.Inset, owner.EdgeInset, owner.Line);
            var inner = Math.Max(0, width - 2 * line - 2 * inset);
            var edgeInner = Math.Max(0, width - 2 * line - 2 * edge.Inline);
            var gap = (int)MathF.Round(Tokens.Space.Sm * owner.Em);
            var rtl = owner.RightToLeft == RightToLeft.Yes;
            int X(int left, int w) => rtl ? width - left - w : left;
            var y = line;
            HeaderBottom = FooterTop = null;
            if (owner.HasHeader)
            {
                var context = owner.Context;
                var text = owner.Header.Length == 0 ? Size.Empty : owner.title.Measure(context, new Size(edgeInner, int.MaxValue));
                var extra = owner.Extra is { } e ? SizeOf(e, edgeInner) : Size.Empty;
                // 文字与 Extra 一行放得下时并排、交叉轴居中；放不下时 Extra 换到下一行（flex-wrap）
                var together = text.Width + (extra.Width > 0 ? gap + extra.Width : 0) <= edgeInner;
                var row = together ? Math.Max(text.Height, extra.Height) : text.Height + (extra.Height > 0 ? gap + extra.Height : 0);
                if (arrange)
                {
                    var top = y + edge.Block;
                    owner.title.Bounds = new Rectangle(X(line + edge.Inline, text.Width), top + (together ? (row - text.Height) / 2 : 0), text.Width, text.Height);
                    if (owner.Extra is { } control)
                    {
                        var ey = together ? top + (row - extra.Height) / 2 : top + text.Height + gap;
                        var ex = together ? line + edge.Inline + edgeInner - extra.Width : line + edge.Inline;
                        control.SetBounds(X(ex, extra.Width), ey, extra.Width, extra.Height);
                    }
                }
                y += 2 * edge.Block + row;
                HeaderBottom = y;
                y += line;
            }
            var bodyTop = y + inset;
            var bodyChildren = owner.skeletonShown ? [owner.skeleton] : owner.BodyControls().Where(control => control.Visible || !owner.Visible).ToList();
            var bodyHeight = 0;
            foreach (var child in bodyChildren)
            {
                var size = child == owner.skeleton ? new Size(inner, child.Height) : SizeOf(child, inner);
                if (arrange) child.SetBounds(X(line + inset, inner), bodyTop + bodyHeight, inner, size.Height);
                bodyHeight += size.Height;
            }
            y = bodyTop + bodyHeight + inset + slack;
            if (owner.Footer is { } footer)
            {
                FooterTop = y;
                y += line;
                // 页脚是布局容器（一组按钮、两端对齐的一行）时撑满一行由它自己排，单个控件靠结束边（TS 的页脚是 justify-content: flex-end 的一行）
                var size = SizeOf(footer, edgeInner);
                if (footer is Panel) size.Width = edgeInner;
                if (arrange) footer.SetBounds(X(line + edge.Inline + edgeInner - size.Width, size.Width), y + edge.Block, size.Width, size.Height);
                y += 2 * edge.Block + size.Height;
            }
            return y + line;
        }

        public Size Measure(Size proposed)
        {
            var width = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : owner.Width;
            return new Size(width, Run(width, arrange: false));
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            Run(owner.ClientSize.Width, arrange: true);
            owner.ClipCorners();
            return owner.AutoSize;
        }
    }
}

/// <summary>卡片样式（vue-tsx-pro 的 CardVariants）。</summary>
/// <param name="Value">取值（surface、outline、elevated、ghost）。</param>
[TypeConverter(typeof(VariantConverter<CardVariant>))]
public readonly record struct CardVariant(string Value) : IVariant<CardVariant>
{
    /// <summary>表面色加细边框（默认）。</summary>
    public static readonly CardVariant Surface = new("surface");

    /// <summary>只有边框，底色透明。</summary>
    public static readonly CardVariant Outline = new("outline");

    /// <summary>表面色加阴影，浮在页面上（WinForms 不画阴影）。</summary>
    public static readonly CardVariant Elevated = new("elevated");

    /// <summary>没有底色与边框，只保留内边距与分区。</summary>
    public static readonly CardVariant Ghost = new("ghost");

    /// <inheritdoc />
    public static IReadOnlyList<CardVariant> Known { get; } = [Surface, Outline, Elevated, Ghost];

    /// <inheritdoc />
    public static CardVariant Parse(string value)
    {
        var variant = new CardVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Card 在作用域里的默认值（上游的 <c>defaults.Card</c>）。</summary>
public sealed record CardDefaults : IScopeRecord<CardDefaults>
{
    /// <summary>样式。</summary>
    public CardVariant? Variant { get; init; }

    /// <summary>内边距档位（控件上叫 CardPadding）。</summary>
    public Space? Padding { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <inheritdoc />
    public CardDefaults Merge(CardDefaults? over) => over is null ? this : new() { Variant = over.Variant ?? Variant, Padding = over.Padding ?? Padding, Radius = over.Radius ?? Radius };
}

/// <summary>Card 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Card.styles</c>）：作用域的在下，实例的 Sx 在上，盖过样式的配色。</summary>
public sealed record CardSx : IScopeRecord<CardSx>
{
    /// <summary>底色（ghost、outline 写了也画）。</summary>
    public Color? Background { get; init; }

    /// <summary>描边的颜色（ghost 写了也画）。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>圆角。</summary>
    public Radius? Radius { get; init; }

    /// <inheritdoc />
    public CardSx Merge(CardSx? over) => over is null ? this : new() { Background = over.Background ?? Background, BorderColor = over.BorderColor ?? BorderColor, Radius = over.Radius ?? Radius };
}
