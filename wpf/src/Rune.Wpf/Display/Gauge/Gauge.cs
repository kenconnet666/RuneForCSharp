using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Rune.Wpf;

/// <summary>
/// 仪表盘（vue-tsx-pro 的 Gauge）：半圆的刻度上显示一个量落在哪个区间——完成率、利用率、风险分。外圈是分段的区间（危险、提醒、良好），
/// 里面的弧是当前值（颜色取它所在区间的色调），中间是数值，两端是最小、最大值，下面是名称。
/// <code>
/// new Gauge { Value = 0.82m, Max = 1, ValueFormat = new NumericFormat { Style = NumericStyle.Percent }, Label = "销售完成率", Target = 0.9m,
///             Bands = [new(0.6, StatusTone.Danger), new(0.9, StatusTone.Warning), new(1, StatusTone.Success)] };
/// </code>
/// 和 Progress 的分工：Progress 是做到哪了（上传、步骤）；Gauge 是一个量处在好坏的哪一段（有区间、有目标）。
/// 宽 10em，随字号缩放（Size 档位或继承的字号）。刻度盘是 200 × 108 的画布（同 TS 的 viewBox）放进 Viewbox 等比缩放，线宽跟着缩；
/// 轨道是 fill 色，数值弧与区间用色调的实色（中性是 borderStrong），目标刻度是正文色——都是资源引用，换主题跟着变。
/// 中间的数值 1.5em、半粗、等宽数字，压在半圆的底边上；两端 0.75em、次要色；名称 sm、次要色。
/// 读屏按 meter 念（进度条控件、只读的范围值）：名称是 <see cref="Label"/>，取值文字是数值（按 ValueFormat 与单位）与目标。
/// </summary>
public class Gauge : Control
{
    /// <summary>当前值。</summary>
    public static readonly DependencyProperty ValueProperty = Register(nameof(Value), typeof(decimal), 0m);

    /// <summary>最小值，默认 0。</summary>
    public static readonly DependencyProperty MinProperty = Register(nameof(Min), typeof(decimal), 0m);

    /// <summary>最大值，默认 100。</summary>
    public static readonly DependencyProperty MaxProperty = Register(nameof(Max), typeof(decimal), 100m);

    /// <summary>数值的格式（百分比写 Style = Percent，配 Max = 1）。</summary>
    public static readonly DependencyProperty ValueFormatProperty = Register(nameof(ValueFormat), typeof(NumericFormat), null);

    /// <summary>单位：数值后面带上。</summary>
    public static readonly DependencyProperty UnitProperty = Register(nameof(Unit), typeof(string), null);

    /// <summary>外圈的区间，按 To 从小到大；不写时外圈不画。</summary>
    public static readonly DependencyProperty BandsProperty = Register(nameof(Bands), typeof(IReadOnlyList<GaugeBand>), Array.Empty<GaugeBand>());

    /// <summary>目标：外圈上画一道刻度，读屏念在数值后面。</summary>
    public static readonly DependencyProperty TargetProperty = Register(nameof(Target), typeof(decimal?), null);

    /// <summary>数值弧的色调；不写时取它所在区间的色调，没有区间时 accent。</summary>
    public static readonly DependencyProperty ToneProperty = Register(nameof(Tone), typeof(StatusTone?), null);

    /// <summary>名称：显示在下方，也是读屏的名称。</summary>
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), typeof(string), null);

    /// <summary>中间显示的文字；不写时是格式化的数值。</summary>
    public static readonly DependencyProperty ValueLabelProperty = Register(nameof(ValueLabel), typeof(string), null);

    /// <summary>字号档位：仪表盘的大小随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Gauge), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Xs };
    // 写全名：Rune.Wpf.Grid 是 CSS 的网格布局；这里要数值叠在刻度盘上
    readonly System.Windows.Controls.Grid dial = new();
    readonly Canvas canvas = new() { Width = GaugeGeometry.Width, Height = GaugeGeometry.Height };
    readonly Text value = new() { Name = "PART_Value", Numeric = true, Weight = TextWeight.Semibold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    readonly Flex ends = new() { Justify = FlexJustify.Between };
    readonly Text min = new() { Numeric = true, Tone = TextTone.Muted };
    readonly Text max = new() { Numeric = true, Tone = TextTone.Muted };
    readonly Text label = new() { Name = "PART_Label", Align = TextAlign.Center, Tone = TextTone.Muted };

    static Gauge()
    {
        FocusableProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Gauge)d).UpdateMetrics()));
    }

    /// <summary>新建：0–100，值 0。</summary>
    public Gauge()
    {
        value.SetResourceReference(StyleProperty, GaugeParts.Value);
        label.SetResourceReference(StyleProperty, GaugeParts.Label);
        // 中间的数值行高 1：压在半圆的底边上
        Leading.SetFactor(value, 1);
        value.Tone = TextTone.Neutral;
        dial.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = canvas });
        dial.Children.Add(value);
        ends.Children.Add(min);
        ends.Children.Add(max);
        root.Children.Add(dial);
        root.Children.Add(ends);
        root.Children.Add(label);
        AddVisualChild(root);
        AddLogicalChild(root);
        HorizontalAlignment = HorizontalAlignment.Left;
        Update();
        UpdateMetrics();
    }

    static DependencyProperty Register(string name, Type type, object? fallback) =>
        DependencyProperty.Register(name, type, typeof(Gauge), new FrameworkPropertyMetadata(fallback, (d, _) => ((Gauge)d).Update()));

    /// <summary>当前值。</summary>
    public decimal Value { get => (decimal)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最小值。</summary>
    public decimal Min { get => (decimal)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public decimal Max { get => (decimal)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>数值的格式。</summary>
    public NumericFormat? ValueFormat { get => (NumericFormat?)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>单位。</summary>
    public string? Unit { get => (string?)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>外圈的区间。</summary>
    public IReadOnlyList<GaugeBand> Bands { get => (IReadOnlyList<GaugeBand>)GetValue(BandsProperty); set => SetValue(BandsProperty, value); }

    /// <summary>目标。</summary>
    public decimal? Target { get => (decimal?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>数值弧的色调。</summary>
    public StatusTone? Tone { get => (StatusTone?)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>中间显示的文字。</summary>
    public string? ValueLabel { get => (string?)GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>数值弧的色调（测试用）。</summary>
    internal StatusTone ShownTone => GaugeGeometry.Tone(Tone, Bands, (double)Value);

    /// <summary>读屏念的取值文字。</summary>
    public string Spoken => GaugeGeometry.Spoken(Value, Target, ValueFormat, Unit, Provider.GetLang(this));

    /// <summary>中间显示的文字（测试用）。</summary>
    internal string ShownValue => value.Text ?? "";

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <summary>档位的字号：像素数是本地值，档位取主题资源。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var size = (TextSize)e.NewValue;
        if (size.Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (size.ResourceKey is { } key) ((Gauge)d).SetResourceReference(FontSizeProperty, key);
        else d.ClearValue(FontSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Provider.LangProperty) Update();
    }

    /// <summary>色调的实色（TS 的 gaugeToneStyles）：中性是 borderStrong，其余是色调的实色。</summary>
    static string BrushKey(StatusTone tone) => tone.Value == "neutral" ? ThemeKeys.Brush.BorderStrong : StatusDot.BrushKey(tone);

    /// <summary>文字与刻度盘：轨道、数值弧、外圈的区间、目标刻度按画布坐标重建。</summary>
    void Update()
    {
        var lang = Provider.GetLang(this);
        var format = ValueFormat ?? NumericFormat.Default;
        value.Text = ValueLabel ?? GaugeGeometry.Text(Value, ValueFormat, Unit, lang);
        min.Text = format.Format(Min, lang.Code, lang.Currency);
        max.Text = format.Format(Max, lang.Code, lang.Currency);
        label.Text = Label;
        label.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(this, Label ?? "");
        canvas.Children.Clear();
        var (low, high) = ((double)Min, (double)Max);
        canvas.Children.Add(Arc(0, 1, GaugeGeometry.Radius, ThemeKeys.Brush.Fill, GaugeGeometry.ArcWidth));
        canvas.Children.Add(Arc(0, GaugeGeometry.Fraction((double)Value, low, high), GaugeGeometry.Radius, BrushKey(ShownTone), GaugeGeometry.ArcWidth));
        foreach (var (from, to, tone) in GaugeGeometry.Bands(Bands, low, high)) canvas.Children.Add(Arc(from, to, GaugeGeometry.BandRadius, BrushKey(tone), GaugeGeometry.BandWidth));
        if (Target is { } target)
        {
            var (a, b) = GaugeGeometry.Tick(GaugeGeometry.Fraction((double)target, low, high));
            var tick = new Line { X1 = a.X, Y1 = a.Y - GaugeGeometry.Top, X2 = b.X, Y2 = b.Y - GaugeGeometry.Top, StrokeThickness = GaugeGeometry.TargetWidth, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            tick.SetResourceReference(Shape.StrokeProperty, ThemeKeys.Brush.Text);
            canvas.Children.Add(tick);
        }
    }

    /// <summary>一段弧（画布坐标，纵向挪掉上面伸出去的 4）。</summary>
    static Path Arc(double from, double to, double radius, string brushKey, double width)
    {
        var path = new Path { StrokeThickness = width };
        path.SetResourceReference(Shape.StrokeProperty, brushKey);
        if (to <= from) return path;
        var (a, b) = (GaugeGeometry.Point(from, radius), GaugeGeometry.Point(to, radius));
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(a.X, a.Y - GaugeGeometry.Top), false, false);
            figure.ArcTo(new Point(b.X, b.Y - GaugeGeometry.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        path.Data = geometry;
        return path;
    }

    /// <summary>em 尺寸：宽 10em；数值 1.5em；两端 0.75em；名称 sm（固定档位）。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        Width = 10 * em;
        value.FontSize = 1.5 * em;
        min.FontSize = max.FontSize = 0.75 * em;
        label.FontSize = this.Resource<double>(ThemeKeys.FontSize.Sm);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GaugePeer(this);

    /// <summary>读屏：只读的范围值（同 meter），名称是 Label，取值文字是数值与目标；里面的文字不另念。</summary>
    sealed class GaugePeer(Gauge owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(Gauge);

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface is PatternInterface.RangeValue or PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => true;

        public double Minimum => (double)owner.Min;

        public double Maximum => (double)owner.Max;

        double IRangeValueProvider.Value => (double)Math.Clamp(owner.Value, owner.Min, Math.Max(owner.Min, owner.Max));

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("仪表盘只读");

        string IValueProvider.Value => owner.Spoken;

        public void SetValue(string value) => throw new InvalidOperationException("仪表盘只读");
    }
}

/// <summary>Gauge 的部件样式键。</summary>
public static class GaugeParts
{
    /// <summary>中间的数值（TS 的 propsValue）。</summary>
    public static readonly PartKey Value = new(typeof(Gauge), nameof(Value), typeof(Text));

    /// <summary>名称（TS 的 propsLabel）。</summary>
    public static readonly PartKey Label = new(typeof(Gauge), nameof(Label), typeof(Text));
}
