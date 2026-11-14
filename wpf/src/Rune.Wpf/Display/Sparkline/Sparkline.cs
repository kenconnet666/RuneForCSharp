using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 迷你图（vue-tsx-pro 的 Sparkline）：没有坐标轴的小折线、面积或柱，嵌在指标卡（Stat 的 Content）、表格的格子、列表的一行里看走势。
/// <code>
/// &lt;rune:Stat Label="实际销售" Value="910.36" Unit="万元"&gt;&lt;rune:Sparkline Data="{Binding Monthly}" Type="Area" Label="近 12 个月" /&gt;&lt;/rune:Stat&gt;
/// </code>
/// 宽度占满容器，高度默认 2em（随字号缩放，写 Height 换成像素）。折线 1.5 像素、圆头；面积在折线下面填 15% 的色调；柱每个值一根（0.7 格宽），从 0 起，
/// 85% 不透明，标出最后一个值时前面的淡到 40%、最新的一根实色；折线、面积在最后一个值上画一个 6 像素的点，外面一圈表面色。
/// 颜色是色调的实底（ToneFill，默认 accent）。空值（null）处断开（柱是空位）。
/// 读屏是一张图，念成一句话：几个值、从哪到哪、最低最高（按 <see cref="ValueFormat"/> 与语言格式化），<see cref="Label"/> 写在前面。自绘（OnRender）。
/// </summary>
public class Sparkline : Control
{
    /// <summary>各个值，按时间先后；null 处断开。</summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IReadOnlyList<decimal?>), typeof(Sparkline), new FrameworkPropertyMetadata(Array.Empty<decimal?>(), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>画法，默认 Line。</summary>
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(
        nameof(Type), typeof(SparklineType), typeof(Sparkline), new FrameworkPropertyMetadata(SparklineType.Line, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>色调，默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Sparkline), new FrameworkPropertyMetadata(default(StatusTone), (d, _) => ((Sparkline)d).BindFill()));

    /// <summary>标出最后一个值，默认 true。</summary>
    public static readonly DependencyProperty ShowLastProperty = DependencyProperty.Register(
        nameof(ShowLast), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>纵向范围（最小、最大）；不写的一端按数据取（柱从 0 起）。</summary>
    public static readonly DependencyProperty DomainProperty = DependencyProperty.Register(
        nameof(Domain), typeof((double? Min, double? Max)), typeof(Sparkline), new FrameworkPropertyMetadata(default((double?, double?)), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>读屏念的数值格式。</summary>
    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(nameof(ValueFormat), typeof(NumericFormat), typeof(Sparkline));

    /// <summary>名称：读屏先念它（"近 12 个月实际销售"）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Sparkline));

    static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        "Fill", typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty SurfaceProperty = DependencyProperty.Register(
        "Surface", typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static Sparkline()
    {
        FocusableProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>新建：没有值。</summary>
    public Sparkline()
    {
        BindFill();
        SetResourceReference(SurfaceProperty, "Rune.Brush.Surface");
    }

    /// <summary>各个值。</summary>
    public IReadOnlyList<decimal?> Data { get => (IReadOnlyList<decimal?>)GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>画法。</summary>
    public SparklineType Type { get => (SparklineType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>标出最后一个值。</summary>
    public bool ShowLast { get => (bool)GetValue(ShowLastProperty); set => SetValue(ShowLastProperty, value); }

    /// <summary>纵向范围。</summary>
    public (double? Min, double? Max) Domain { get => ((double?, double?))GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    /// <summary>读屏念的数值格式。</summary>
    public NumericFormat? ValueFormat { get => (NumericFormat?)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>读屏念的一句话。</summary>
    public string Summary => SparklineGeometry.Summary(Data, ValueFormat, Provider.GetLang(this), Label);

    /// <summary>当前的几何（测试用）。</summary>
    internal SparklineGeometry Geometry => SparklineGeometry.Of(Data, Type, Domain.Min, Domain.Max);

    void BindFill() => SetResourceReference(FillProperty, ToneFill.Keys(Tone).Fill);

    /// <summary>宽度占满（父元素不限宽时 0），高度不写时 2em。</summary>
    protected override Size MeasureOverride(Size constraint) => new(double.IsInfinity(constraint.Width) ? 0 : constraint.Width, 2 * FontSize);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds) => arrangeBounds;

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (GetValue(FillProperty) is not SolidColorBrush fill) return;
        var geometry = Geometry;
        var (width, height) = (ActualWidth, ActualHeight);
        var (sx, sy) = (width / geometry.Width, height / 100);
        Point At((double X, double Y) point) => new(point.X * sx, point.Y * sy);
        SolidColorBrush Faded(double opacity) => new(fill.Color) { Opacity = opacity };
        if (Type == SparklineType.Bar)
        {
            foreach (var bar in geometry.Bars)
            {
                // 标出最后一个值：前面的柱淡下去，最新的一根实色
                var opacity = !ShowLast ? 0.85 : bar.Index == geometry.LastIndex ? 1 : 0.4;
                drawingContext.DrawRectangle(Faded(opacity), null, new Rect((bar.Index + 0.15) * sx, bar.Top * sy, 0.7 * sx, Math.Max(0, (bar.Bottom - bar.Top) * sy)));
            }
            return;
        }
        var pen = new Pen(fill, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        foreach (var points in geometry.Segments)
        {
            if (Type == SparklineType.Area && points.Count > 1)
            {
                var area = new StreamGeometry();
                using (var figure = area.Open())
                {
                    figure.BeginFigure(At(points[0]), true, true);
                    foreach (var point in points.Skip(1)) figure.LineTo(At(point), false, false);
                    figure.LineTo(new Point(points[^1].X * sx, height), false, false);
                    figure.LineTo(new Point(points[0].X * sx, height), false, false);
                }
                drawingContext.DrawGeometry(Faded(0.15), null, area);
            }
            var line = new StreamGeometry();
            using (var figure = line.Open())
            {
                figure.BeginFigure(At(points[0]), false, false);
                foreach (var point in points.Skip(1)) figure.LineTo(At(point), true, true);
            }
            drawingContext.DrawGeometry(null, pen, line);
        }
        if (!ShowLast || geometry.Last is not { } last) return;
        // 最后一个点 6 像素，外面一圈表面色（borderWidths.sm）把点与线分开
        var ring = TryFindResource("Rune.BorderWidth.Sm") is double value ? value : 2;
        var center = At(last);
        if (GetValue(SurfaceProperty) is Brush surface) drawingContext.DrawEllipse(surface, null, center, 3 + ring, 3 + ring);
        drawingContext.DrawEllipse(fill, null, center, 3, 3);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SparklinePeer(this);

    /// <summary>读屏：一张图，名称是概括的一句话。</summary>
    sealed class SparklinePeer(Sparkline owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(Sparkline);

        protected override string GetNameCore() => owner.Summary;
    }
}
