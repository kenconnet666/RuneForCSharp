using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 迷你图（vue-tsx-pro 的 Sparkline）：没有坐标轴的小折线、面积或柱，嵌在指标卡（StatControl 的 Content）、表格的格子、列表的一行里看走势。
/// 无窗口元素，放进窗体用 <see cref="SparklineControl"/>。宽度占满给的宽度，高度 2em（随字体缩放）。折线 1.5 像素、圆头；面积在折线下面填 15% 的色调；
/// 柱每个值一根（0.7 格宽），从 0 起，85% 不透明，标出最后一个值时前面的淡到 40%、最新的一根实色；折线、面积在最后一个值上画一个 6 像素的点，外面一圈表面色。
/// 颜色是色调的实底（ToneFill，默认 accent）。空值（null）处断开（柱是空位）。
/// </summary>
public class Sparkline : Element
{
    /// <summary>各个值，按时间先后；null 处断开。</summary>
    public IReadOnlyList<decimal?> Data { get; set { field = value; Invalidate(); } } = [];

    /// <summary>画法，默认 Line。</summary>
    public SparklineType Type { get; set { field = value; Invalidate(); } }

    /// <summary>色调，默认 accent。</summary>
    public StatusTone Tone { get; set { field = value; Invalidate(); } }

    /// <summary>标出最后一个值，默认 true。</summary>
    public bool ShowLast { get; set { field = value; Invalidate(); } } = true;

    /// <summary>纵向范围（最小、最大）；不写的一端按数据取（柱从 0 起）。</summary>
    public (double? Min, double? Max) Domain { get; set { field = value; Invalidate(); } }

    /// <summary>读屏念的数值格式。</summary>
    public NumericFormat? ValueFormat { get; set; }

    /// <summary>名称：读屏先念它（"近 12 个月实际销售"）。</summary>
    public string? Label { get; set; }

    /// <summary>读屏念的一句话：几个值、从哪到哪、最低最高。</summary>
    public string Summary => SparklineGeometry.Summary(Data, ValueFormat, CurrentLang, Label);

    /// <summary>当前的几何（测试用）。</summary>
    internal SparklineGeometry Geometry => SparklineGeometry.Of(Data, Type, Domain.Min, Domain.Max);

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) =>
        new(available.Width == int.MaxValue ? 0 : available.Width, (int)MathF.Round(2 * context.Em));

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var geometry = Geometry;
        var colors = context.Theme.Colors;
        var fill = ToneFill.Colors(Tone, colors).Fill;
        var (radius, ring) = (context.Scale(3), context.Scale(Tokens.BorderWidth.Sm));
        // 控件画不到自己外面：标出最后一个点时，结束边与上下各让出点的半径加一圈（TS 的点有一半伸到图外）
        var inset = ShowLast && Type != SparklineType.Bar && geometry.Last is not null ? (int)MathF.Ceiling(radius + ring) : 0;
        var plot = Rectangle.FromLTRB(Bounds.Left, Bounds.Top + inset, Bounds.Right - inset, Bounds.Bottom - inset);
        var (sx, sy) = (plot.Width / geometry.Width, plot.Height / 100.0);
        PointF At((double X, double Y) point) => new((float)(plot.X + point.X * sx), (float)(plot.Y + point.Y * sy));
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Type == SparklineType.Bar)
            {
                foreach (var bar in geometry.Bars)
                {
                    // 标出最后一个值：前面的柱淡下去，最新的一根实色
                    var opacity = !ShowLast ? 0.85 : bar.Index == geometry.LastIndex ? 1 : 0.4;
                    using var brush = new SolidBrush(Color.FromArgb((int)Math.Round(255 * opacity), fill));
                    graphics.FillRectangle(brush, (float)(plot.X + (bar.Index + 0.15) * sx), (float)(plot.Y + bar.Top * sy), (float)(0.7 * sx), (float)((bar.Bottom - bar.Top) * sy));
                }
                return;
            }
            using var pen = new Pen(fill, context.Scale(1.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            foreach (var points in geometry.Segments)
            {
                var line = points.Select(At).ToArray();
                if (Type == SparklineType.Area && line.Length > 1)
                {
                    using var area = new SolidBrush(Color.FromArgb((int)Math.Round(255 * 0.15), fill));
                    graphics.FillPolygon(area, [.. line, new PointF(line[^1].X, plot.Bottom), new PointF(line[0].X, plot.Bottom)]);
                }
                if (line.Length > 1) graphics.DrawLines(pen, line);
                else
                {
                    // 只有一个值的段：圆头的线长度为 0，画成一个点
                    using var point = new SolidBrush(fill);
                    graphics.FillEllipse(point, line[0].X - pen.Width / 2, line[0].Y - pen.Width / 2, pen.Width, pen.Width);
                }
            }
            if (!ShowLast || geometry.Last is not { } last) return;
            // 最后一个点 6 像素，外面一圈表面色（borderWidths.sm）把点与线分开
            var center = At(last);
            using (var surface = new SolidBrush(colors.Surface)) graphics.FillEllipse(surface, center.X - radius - ring, center.Y - radius - ring, 2 * (radius + ring), 2 * (radius + ring));
            using (var dot = new SolidBrush(fill)) graphics.FillEllipse(dot, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}

/// <summary>
/// 放进窗体的迷你图（单元素宿主）：宽度由父容器给（停靠、锚定，或放进 StatControl 的 Content 占满一行），高度 2em。
/// 读屏是一张图，名称是概括的一句话（Label 在前）。
/// <code>
/// var trend = new SparklineControl { Data = monthly, Type = SparklineType.Area, Label = "近 12 个月实际销售" };
/// </code>
/// </summary>
public class SparklineControl : ElementControl<Sparkline>
{
    /// <summary>新建：不能获得焦点。</summary>
    public SparklineControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>各个值，按时间先后；null 处断开。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<decimal?> Data { get => Element.Data; set => Element.Data = value; }

    /// <summary>画法，默认 Line。</summary>
    [Category("Appearance")]
    [DefaultValue(SparklineType.Line)]
    public SparklineType Type { get => Element.Type; set => Element.Type = value; }

    /// <summary>色调，默认 accent。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>标出最后一个值，默认 true。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowLast { get => Element.ShowLast; set => Element.ShowLast = value; }

    /// <summary>纵向范围（最小、最大）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public (double? Min, double? Max) Domain { get => Element.Domain; set => Element.Domain = value; }

    /// <summary>读屏念的数值格式。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? ValueFormat { get => Element.ValueFormat; set => Element.ValueFormat = value; }

    /// <summary>名称：读屏先念它。</summary>
    [Category("Accessibility")]
    [DefaultValue(null)]
    public string? Label { get => Element.Label; set => Element.Label = value; }

    bool ShouldSerializeTone() => Tone.Value is not null;

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SparklineAccessibleObject(this);

    /// <summary>读屏：一张图，名称是概括的一句话；不能获得焦点。</summary>
    sealed class SparklineAccessibleObject(SparklineControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Graphic;

        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Element.Summary : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override AccessibleStates State => base.State & ~AccessibleStates.Focusable;
    }
}
