using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 迷你图（vue-tsx-pro 的 Sparkline）：没有坐标轴的小折线、面积或柱，嵌在指标卡（Stat 的 Content）、表格的格子、列表的一行里看走势。
/// <code>
/// &lt;rune:Stat Label="实际销售" Value="910.36" Unit="万元"&gt;&lt;rune:Sparkline Data="{Binding Monthly}" Type="Area" Label="近 12 个月" /&gt;&lt;/rune:Stat&gt;
/// </code>
/// 宽度占满容器，高度默认 2em（随字号缩放，写 Height 换成像素）。折线 1.5 像素、圆头；面积在折线下面填 15% 的色调；柱每个值一根（0.7 格宽），从 0 起，
/// 85% 不透明，标出最后一个值时前面的淡到 40%、最新的一根实色；折线、面积在最后一个值上画一个 6 像素的点，外面一圈表面色。
/// 颜色是色调的实底（ToneFill，默认 accent）。空值（null）处断开（柱是空位）。
/// 读屏是一张图，念成一句话：几个值、从哪到哪、最低最高（按 <see cref="ValueFormat"/> 与语言格式化），<see cref="Label"/> 写在前面。
/// </summary>
public class Sparkline : Control
{
    /// <summary>各个值，按时间先后；null 处断开。</summary>
    public static readonly StyledProperty<IReadOnlyList<decimal?>> DataProperty = AvaloniaProperty.Register<Sparkline, IReadOnlyList<decimal?>>(nameof(Data), []);

    /// <summary>画法，默认 Line。</summary>
    public static readonly StyledProperty<SparklineType> TypeProperty = AvaloniaProperty.Register<Sparkline, SparklineType>(nameof(Type));

    /// <summary>色调，默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Sparkline, StatusTone>(nameof(Tone));

    /// <summary>标出最后一个值，默认 true。</summary>
    public static readonly StyledProperty<bool> ShowLastProperty = AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowLast), true);

    /// <summary>纵向范围（最小、最大）；不写的一端按数据取（柱从 0 起）。</summary>
    public static readonly StyledProperty<(double? Min, double? Max)> DomainProperty = AvaloniaProperty.Register<Sparkline, (double? Min, double? Max)>(nameof(Domain));

    /// <summary>读屏念的数值格式。</summary>
    public static readonly StyledProperty<NumericFormat?> ValueFormatProperty = AvaloniaProperty.Register<Sparkline, NumericFormat?>(nameof(ValueFormat));

    /// <summary>名称：读屏先念它（"近 12 个月实际销售"）。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Sparkline, string?>(nameof(Label));

    /// <summary>字号（继承），默认高度 2em 按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Sparkline>();

    static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<Sparkline, IBrush?>("Fill");
    static readonly StyledProperty<IBrush?> SurfaceProperty = AvaloniaProperty.Register<Sparkline, IBrush?>("Surface");

    readonly TemplateResources resources;

    static Sparkline()
    {
        AffectsRender<Sparkline>(DataProperty, TypeProperty, ShowLastProperty, DomainProperty, FillProperty, SurfaceProperty);
        AffectsMeasure<Sparkline>(FontSizeProperty);
    }

    /// <summary>新建：没有值。</summary>
    public Sparkline()
    {
        resources = new TemplateResources(this);
        resources.Bind(FillProperty, ToneFill.Keys(default).Fill);
        resources.Bind(SurfaceProperty, "Rune.Brush.Surface");
    }

    /// <summary>各个值。</summary>
    public IReadOnlyList<decimal?> Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>画法。</summary>
    public SparklineType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>标出最后一个值。</summary>
    public bool ShowLast { get => GetValue(ShowLastProperty); set => SetValue(ShowLastProperty, value); }

    /// <summary>纵向范围。</summary>
    public (double? Min, double? Max) Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    /// <summary>读屏念的数值格式。</summary>
    public NumericFormat? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>读屏念的一句话。</summary>
    public string Summary => SparklineGeometry.Summary(Data, ValueFormat, Provider.GetLang(this), Label);

    /// <summary>当前的几何（测试用）。</summary>
    internal SparklineGeometry Geometry => SparklineGeometry.Of(Data, Type, Domain.Min, Domain.Max);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) resources.Bind(FillProperty, ToneFill.Keys(Tone).Fill);
    }

    /// <summary>宽度占满（父元素不限宽时 0），高度不写时 2em。</summary>
    protected override LayoutSize MeasureOverride(LayoutSize availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 2 * FontSize);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (GetValue(FillProperty) is not ISolidColorBrush fill) return;
        var geometry = Geometry;
        var (width, height) = (Bounds.Width, Bounds.Height);
        var (sx, sy) = (width / geometry.Width, height / 100);
        Point At((double X, double Y) point) => new(point.X * sx, point.Y * sy);
        if (Type == SparklineType.Bar)
        {
            foreach (var bar in geometry.Bars)
            {
                // 标出最后一个值：前面的柱淡下去，最新的一根实色
                var opacity = !ShowLast ? 0.85 : bar.Index == geometry.LastIndex ? 1 : 0.4;
                context.FillRectangle(new SolidColorBrush(fill.Color, opacity), new Rect((bar.Index + 0.15) * sx, bar.Top * sy, 0.7 * sx, (bar.Bottom - bar.Top) * sy));
            }
            return;
        }
        var pen = new Pen(fill, 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var points in geometry.Segments)
        {
            if (Type == SparklineType.Area && points.Count > 1)
            {
                var area = new StreamGeometry();
                using (var figure = area.Open())
                {
                    figure.BeginFigure(At(points[0]), true);
                    foreach (var point in points.Skip(1)) figure.LineTo(At(point));
                    figure.LineTo(new Point(points[^1].X * sx, height));
                    figure.LineTo(new Point(points[0].X * sx, height));
                    figure.EndFigure(true);
                }
                context.DrawGeometry(new SolidColorBrush(fill.Color, 0.15), null, area);
            }
            var line = new StreamGeometry();
            using (var figure = line.Open())
            {
                figure.BeginFigure(At(points[0]), false);
                foreach (var point in points.Skip(1)) figure.LineTo(At(point));
                figure.EndFigure(false);
            }
            context.DrawGeometry(null, pen, line);
        }
        if (!ShowLast || geometry.Last is not { } last) return;
        // 最后一个点 6 像素，外面一圈表面色（borderWidths.sm）把点与线分开
        var ring = this.Resource("Rune.BorderWidth.Sm", 2.0);
        var center = At(last);
        if (GetValue(SurfaceProperty) is { } surface) context.DrawEllipse(surface, null, center, 3 + ring, 3 + ring);
        context.DrawEllipse(fill, null, center, 3, 3);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SparklinePeer(this);

    /// <summary>读屏：一张图，名称是概括的一句话。</summary>
    sealed class SparklinePeer(Sparkline owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetNameCore() => owner.Summary;

        protected override bool IsControlElementCore() => true;
    }
}
