using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>比例条的一段（vue-tsx-pro 的 ProportionSegment）。</summary>
/// <param name="Key">标识。</param>
/// <param name="Label">名称。</param>
/// <param name="Value">数量。</param>
/// <param name="Tone">色调；不写时按顺序取 accent、info、success、warning、danger、neutral。</param>
/// <param name="Color">任意颜色（如图表色板）；写了时不看色调。</param>
public sealed record ProportionSegment(string Key, string Label, double Value, StatusTone? Tone = null, Color? Color = null);

/// <summary>
/// 比例条（vue-tsx-pro 的 ProportionBar）：一根横条按各部分的数量分段，下面是图例（色点、名称、数量），看一个总数由哪几块构成
/// （工单的三种状态、各状态的占比、存储空间）。部分之间互斥、加起来是总数时用它；要比较趋势用图表。
/// <code>
/// new ProportionBarControl { Label = "工单构成", Segments = [new("done", "已完成", 25, StatusTone.Success), new("open", "进行中", 35, StatusTone.Info)], ValueText = segment => $"{segment.Value} 项" };
/// </code>
/// 横条是圆角的胶囊（full），段与段之间空 borderWidths.sm，粗细 sm 8 像素、md 12 像素；颜色同 StatusDot 的色调实色或任意颜色，数量为 0 的段不画但留在图例里。
/// 图例在横条下面（space.sm），换行排开（列间距 lg、行间距 xs，sm 的字号）：色点（md）、名称、数量（半粗——GDI 按粗体画）与补充（次要色、xs，TS 里常放 Small）。
/// 都是画出来的；读屏里横条是一张图（名称是 <see cref="Label"/> 加上各段的数量），图例每一项是一段文字。宽度由父容器给，高度跟着内容。
/// </summary>
public class ProportionBarControl : Control, IElementHost, IScopeAware
{
    readonly Effect paint;
    readonly List<(ProportionSegment Segment, StatusDot Dot, Text Label, Text Value, Text? Extra)> items = [];
    Rectangle barBounds;

    /// <summary>新建：没有段，大小跟着内容。</summary>
    public ProportionBarControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        // 宽度由父容器给，高度跟着内容：Flex、Grid 按想要的大小排
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        paint = new Effect(Invalidate);
    }

    /// <summary>各段：按顺序从起始边排。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ProportionSegment> Segments { get; set { field = value; Rebuild(); } } = [];

    /// <summary>横条的名称（读屏）：后面自动带上各段的数量。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public string? Label { get; set; }

    /// <summary>图例里数量的写法；不写时是数字。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ProportionSegment, string>? ValueText { get; set { field = value; Rebuild(); } }

    /// <summary>下面的图例，默认显示。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool Legend { get; set { field = value; Relayout(); } } = true;

    /// <summary>横条的粗细，默认 md。</summary>
    [Category("Appearance")]
    public ProportionBarSize BarSize { get; set { field = value; Relayout(); } }

    /// <summary>图例每一项后面的补充（"待定价"），次要色、xs。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<ProportionSegment, string?>? LegendExtra { get; set { field = value; Rebuild(); } }

    bool ShouldSerializeBarSize() => BarSize.Value is not null;

    /// <inheritdoc />
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    /// <summary>横条给读屏的名称。</summary>
    public string BarLabel => Proportions.Label(Label, Segments.Select(segment => (segment.Label, segment.Value)));

    /// <summary>横条的位置（测试用）。</summary>
    internal Rectangle BarBounds => barBounds;

    /// <summary>图例各项的文字（测试用）。</summary>
    internal IReadOnlyList<string> LegendTexts => [.. items.Select(item => item.Label.Content + item.Value.Content + item.Extra?.Content)];

    /// <summary>图例各项色点的色调（测试用）。</summary>
    internal IReadOnlyList<StatusTone> LegendTones => [.. items.Select(item => item.Dot.Tone)];

    ElementContext Context => ElementContext.Of(this);

    void Rebuild()
    {
        items.Clear();
        for (var index = 0; index < Segments.Count; index++)
        {
            var segment = Segments[index];
            var dot = new StatusDot { Size = StatusDotSize.Md, Tone = Proportions.ToneAt(segment.Tone, index), Color = segment.Color, Host = this };
            var label = new Text { Content = segment.Label, Size = Tokens.FontSize.Sm, Wrap = TextWrap.NoWrap, Host = this };
            var value = new Text { Content = ValueText?.Invoke(segment) ?? segment.Value.ToString(System.Globalization.CultureInfo.CurrentCulture), Size = Tokens.FontSize.Sm, Weight = TextWeight.Semibold, Numeric = true, Wrap = TextWrap.NoWrap, Host = this };
            var extra = LegendExtra?.Invoke(segment) is { Length: > 0 } note ? new Text { Content = note, Size = Tokens.FontSize.Xs, Tone = TextTone.Muted, Wrap = TextWrap.NoWrap, Host = this } : null;
            items.Add((segment, dot, label, value, extra));
        }
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
        Relayout();
    }

    void Relayout()
    {
        if (Parent is not null) Height = Arrange(Width);
        Invalidate();
    }

    void IScopeAware.ScopeChanged() => Relayout();

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var height = Arrange(Width);
        if (Height != height) Height = height;
    }

    /// <summary>横条在上，图例在下（space.sm）：每项 [色点][名称][数量][补充] 间距 space.sm，项之间 space.lg，放不下换行（space.xs）。</summary>
    int Arrange(int width)
    {
        var context = Context;
        var em = context.Em;
        var sm = context.Scale(Tokens.FontSize.Sm);
        barBounds = new Rectangle(0, 0, width, (int)MathF.Round(context.Scale((float)(BarSize.Value is null ? ProportionBarSize.Md : BarSize).Height)));
        var y = barBounds.Bottom;
        if (!Legend || items.Count == 0) return y;
        y += (int)MathF.Round(Tokens.Space.Sm * em);
        var (gap, columnGap, rowGap) = ((int)MathF.Round(Tokens.Space.Sm * sm), (int)MathF.Round(Tokens.Space.Lg * sm), (int)MathF.Round(Tokens.Space.Xs * sm));
        var (x, lineHeight) = (0, 0);
        foreach (var (_, dot, label, value, extra) in items)
        {
            var parts = new List<(Element Element, Size Size)> { (dot, dot.Measure(context, Size.Empty)) };
            foreach (var text in new[] { label, value, extra }.OfType<Text>()) parts.Add((text, text.Measure(context, new Size(int.MaxValue, int.MaxValue))));
            var itemWidth = parts.Sum(part => part.Size.Width) + gap * (parts.Count - 1);
            var itemHeight = parts.Max(part => part.Size.Height);
            if (x > 0 && x + itemWidth > width)
            {
                y += lineHeight + rowGap;
                (x, lineHeight) = (0, 0);
            }
            var left = x;
            foreach (var (element, size) in parts)
            {
                element.Bounds = new Rectangle(left, y + (itemHeight - size.Height) / 2, size.Width, size.Height);
                left += size.Width + gap;
            }
            x += itemWidth + columnGap;
            lineHeight = Math.Max(lineHeight, itemHeight);
        }
        return y + lineHeight;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : Width;
        return new Size(width, Arrange(width));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context with { ForeColor = Context.Theme.Colors.Text };
        PaintBar(e.Graphics, context);
        if (!Legend) return;
        foreach (var (_, dot, label, value, extra) in items)
        {
            dot.Paint(e.Graphics, context);
            label.Paint(e.Graphics, context);
            value.Paint(e.Graphics, context);
            extra?.Paint(e.Graphics, context);
        }
    }

    /// <summary>横条：按数量分宽、段间空 borderWidths.sm，整根按胶囊裁。</summary>
    void PaintBar(Graphics graphics, ElementContext context)
    {
        var shown = Segments.Select((segment, index) => (Segment: segment, Index: index)).Where(entry => entry.Segment.Value > 0).ToList();
        var total = shown.Sum(entry => entry.Segment.Value);
        if (total <= 0 || barBounds.Width <= 0) return;
        var gap = context.Scale(Tokens.BorderWidth.Sm);
        var room = Math.Max(0, barBounds.Width - gap * (shown.Count - 1));
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var capsule = Drawing.RoundedRectangle(barBounds, barBounds.Height / 2f);
        graphics.SetClip(capsule);
        var x = (float)barBounds.X;
        foreach (var (segment, index) in shown)
        {
            var share = (float)(room * segment.Value / total);
            using var brush = new SolidBrush(segment.Color ?? StatusDot.ToneColor(Proportions.ToneAt(segment.Tone, index), context.Theme.Colors));
            graphics.FillRectangle(brush, x, barBounds.Y, share, barBounds.Height);
            x += share + gap;
        }
        graphics.Restore(state);
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate();

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new BarAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>读屏：子项是横条（一张图，名称带上各段的数量）与图例的各项（一段文字）。</summary>
    sealed class BarAccessibleObject(ProportionBarControl owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount() => 1 + (owner.Legend ? owner.items.Count : 0);

        public override AccessibleObject? GetChild(int index) => index switch
        {
            0 => new PartAccessibleObject(this, owner, AccessibleRole.Graphic, owner.BarLabel, owner.barBounds),
            _ when owner.Legend && index - 1 < owner.items.Count => Item(owner.items[index - 1]),
            _ => null,
        };

        AccessibleObject Item((ProportionSegment Segment, StatusDot Dot, Text Label, Text Value, Text? Extra) item) =>
            new PartAccessibleObject(this, owner, AccessibleRole.StaticText, string.Join(" ", new[] { item.Label.Content, item.Value.Content, item.Extra?.Content }.OfType<string>()),
                Rectangle.Union(item.Dot.Bounds, (item.Extra ?? item.Value).Bounds));
    }

    sealed class PartAccessibleObject(AccessibleObject parent, Control owner, AccessibleRole role, string name, Rectangle bounds) : AccessibleObject
    {
        public override AccessibleObject Parent => parent;

        public override AccessibleRole Role => role;

        public override string Name => name;

        public override Rectangle Bounds => owner.RectangleToScreen(bounds);

        public override AccessibleStates State => AccessibleStates.ReadOnly;
    }
}
