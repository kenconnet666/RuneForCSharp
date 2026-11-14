using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 仪表盘（vue-tsx-pro 的 Gauge）：半圆的刻度上显示一个量落在哪个区间——完成率、利用率、风险分。外圈是分段的区间（危险、提醒、良好），
/// 里面的弧是当前值（颜色取它所在区间的色调），中间是数值，两端是最小、最大值，下面是名称。
/// <code>
/// new Gauge { Value = 0.82m, Max = 1, ValueFormat = new NumericFormat { Style = NumericStyle.Percent }, Label = "销售完成率", Target = 0.9m,
///             Bands = [new(0.6, StatusTone.Danger), new(0.9, StatusTone.Warning), new(1, StatusTone.Success)] };
/// </code>
/// 和 Progress 的分工：Progress 是做到哪了（上传、步骤）；Gauge 是一个量处在好坏的哪一段（有区间、有目标）。
/// 宽 10em，随字号缩放（Size 档位或继承的字号）。轨道是 fill 色，数值弧与区间用色调的实色（中性是 borderStrong），目标刻度是正文色。
/// 中间的数值 1.5em、半粗、等宽数字，压在半圆的底边上；两端 0.75em、次要色；名称 sm、次要色。
/// 读屏按 meter 念（进度条控件、只读的范围值）：名称是 <see cref="Label"/>，取值文字是数值（按 ValueFormat 与单位）与目标。
/// 部件：<c>rune|Gauge rune|Text#PART_Value</c>、<c>rune|Text#PART_Label</c>。
/// </summary>
public class Gauge : Control
{
    /// <summary>当前值。</summary>
    public static readonly StyledProperty<decimal> ValueProperty = AvaloniaProperty.Register<Gauge, decimal>(nameof(Value));

    /// <summary>最小值，默认 0。</summary>
    public static readonly StyledProperty<decimal> MinProperty = AvaloniaProperty.Register<Gauge, decimal>(nameof(Min));

    /// <summary>最大值，默认 100。</summary>
    public static readonly StyledProperty<decimal> MaxProperty = AvaloniaProperty.Register<Gauge, decimal>(nameof(Max), 100);

    /// <summary>数值的格式（百分比写 Style = Percent，配 Max = 1）。</summary>
    public static readonly StyledProperty<NumericFormat?> ValueFormatProperty = AvaloniaProperty.Register<Gauge, NumericFormat?>(nameof(ValueFormat));

    /// <summary>单位：数值后面带上。</summary>
    public static readonly StyledProperty<string?> UnitProperty = AvaloniaProperty.Register<Gauge, string?>(nameof(Unit));

    /// <summary>外圈的区间，按 To 从小到大；不写时外圈不画。</summary>
    public static readonly StyledProperty<IReadOnlyList<GaugeBand>> BandsProperty = AvaloniaProperty.Register<Gauge, IReadOnlyList<GaugeBand>>(nameof(Bands), []);

    /// <summary>目标：外圈上画一道刻度，读屏念在数值后面。</summary>
    public static readonly StyledProperty<decimal?> TargetProperty = AvaloniaProperty.Register<Gauge, decimal?>(nameof(Target));

    /// <summary>数值弧的色调；不写时取它所在区间的色调，没有区间时 accent。</summary>
    public static readonly StyledProperty<StatusTone?> ToneProperty = AvaloniaProperty.Register<Gauge, StatusTone?>(nameof(Tone));

    /// <summary>名称：显示在下方，也是读屏的名称。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Gauge, string?>(nameof(Label));

    /// <summary>中间显示的文字；不写时是格式化的数值。</summary>
    public static readonly StyledProperty<string?> ValueLabelProperty = AvaloniaProperty.Register<Gauge, string?>(nameof(ValueLabel));

    /// <summary>字号档位：仪表盘的大小随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Gauge, TextSize>(nameof(Size));

    /// <summary>字号（继承），宽 10em 按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Gauge>();

    // 刻度盘、两端占满 10em 的宽，名称居中（TS 的 inline-grid、justify-items: center 而刻度盘 width: 100%）
    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Xs };
    readonly Panel dial = new();
    readonly Dial arcs;
    readonly Text value = new() { Name = GaugeParts.Value, Numeric = true, Weight = TextWeight.Semibold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    readonly Flex ends = new() { Justify = FlexJustify.Between };
    readonly Text min = new() { Numeric = true, Tone = TextTone.Muted };
    readonly Text max = new() { Numeric = true, Tone = TextTone.Muted };
    readonly Text label = new() { Name = GaugeParts.Label, Align = TextAlign.Center, Tone = TextTone.Muted };
    readonly TemplateResources resources;

    static Gauge() => AffectsMeasure<Gauge>(FontSizeProperty);

    /// <summary>新建：0–100，值 0。</summary>
    public Gauge()
    {
        resources = new TemplateResources(this);
        arcs = new Dial(this);
        // 中间的数值行高 1：压在半圆的底边上
        value.SetValue(Leading.FactorProperty, 1.0);
        // 行高 1 比字形矮：字形伸出行框的部分照画（TS 的 overflow: visible）
        value.ClipToBounds = false;
        value.Tone = TextTone.Neutral;
        dial.Children.AddRange([arcs, value]);
        ends.Children.AddRange([min, max]);
        // 中间的数值与两端只是显示：读屏念 meter 的取值文字
        foreach (var part in new Control[] { dial, ends }) AutomationProperties.SetAccessibilityView(part, AccessibilityView.Raw);
        root.Children.AddRange([dial, ends, label]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Update();
        UpdateMetrics();
    }

    /// <summary>当前值。</summary>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最小值。</summary>
    public decimal Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public decimal Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>数值的格式。</summary>
    public NumericFormat? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>单位。</summary>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>外圈的区间。</summary>
    public IReadOnlyList<GaugeBand> Bands { get => GetValue(BandsProperty); set => SetValue(BandsProperty, value); }

    /// <summary>目标。</summary>
    public decimal? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>数值弧的色调。</summary>
    public StatusTone? Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>中间显示的文字。</summary>
    public string? ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>数值弧的色调（测试用）。</summary>
    internal StatusTone ShownTone => GaugeGeometry.Tone(Tone, Bands, (double)Value);

    /// <summary>读屏念的取值文字。</summary>
    public string Spoken => GaugeGeometry.Spoken(Value, Target, ValueFormat, Unit, Provider.GetLang(this));

    /// <summary>中间显示的文字（测试用）。</summary>
    internal string ShownValue => value.Text ?? "";

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == ValueProperty || change.Property == MinProperty || change.Property == MaxProperty || change.Property == ValueFormatProperty
                 || change.Property == UnitProperty || change.Property == BandsProperty || change.Property == TargetProperty || change.Property == ToneProperty
                 || change.Property == LabelProperty || change.Property == ValueLabelProperty || change.Property == Provider.LangProperty) Update();
    }

    void Update()
    {
        var lang = Provider.GetLang(this);
        value.Text = ValueLabel ?? GaugeGeometry.Text(Value, ValueFormat, Unit, lang);
        min.Text = (ValueFormat ?? NumericFormat.Default).Format(Min, lang.Code, lang.Currency);
        max.Text = (ValueFormat ?? NumericFormat.Default).Format(Max, lang.Code, lang.Currency);
        label.Text = Label;
        label.IsVisible = !string.IsNullOrEmpty(Label);
        arcs.InvalidateVisual();
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
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GaugePeer(this);

    /// <summary>色调的实色（TS 的 gaugeToneStyles）：中性是 borderStrong，其余是色调的实色。</summary>
    static string BrushKey(StatusTone tone) => tone.Value == "neutral" ? ThemeKeys.Brush.BorderStrong : StatusDot.BrushKey(tone);

    /// <summary>半圆：轨道、数值弧、外圈的区间与目标刻度，按宽度把 200 × 108 的画布缩放着画；高 = 宽 × 108 / 200。</summary>
    sealed class Dial : Control
    {
        readonly Gauge owner;

        public Dial(Gauge owner)
        {
            this.owner = owner;
            this.RedrawOnResourceChanges(owner);
        }

        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            var width = owner.Width;
            return new LayoutSize(width, width * GaugeGeometry.Height / GaugeGeometry.Width);
        }

        public override void Render(DrawingContext context)
        {
            var scale = Bounds.Width / GaugeGeometry.Width;
            Point At((double X, double Y) point) => new(point.X * scale, (point.Y - GaugeGeometry.Top) * scale);
            IBrush? Brush(string key) => owner.Resource<IBrush?>(key);
            void Arc(double from, double to, double radius, IBrush? brush, double width)
            {
                if (brush is null || to <= from) return;
                var geometry = new StreamGeometry();
                using (var figure = geometry.Open())
                {
                    figure.BeginFigure(At(GaugeGeometry.Point(from, radius)), false);
                    figure.ArcTo(At(GaugeGeometry.Point(to, radius)), new LayoutSize(radius * scale, radius * scale), 0, false, SweepDirection.Clockwise);
                    figure.EndFigure(false);
                }
                context.DrawGeometry(null, new Pen(brush, width * scale), geometry);
            }

            var (min, max, current) = ((double)owner.Min, (double)owner.Max, (double)owner.Value);
            Arc(0, 1, GaugeGeometry.Radius, Brush(ThemeKeys.Brush.Fill), GaugeGeometry.ArcWidth);
            Arc(0, GaugeGeometry.Fraction(current, min, max), GaugeGeometry.Radius, Brush(BrushKey(owner.ShownTone)), GaugeGeometry.ArcWidth);
            foreach (var (from, to, tone) in GaugeGeometry.Bands(owner.Bands, min, max)) Arc(from, to, GaugeGeometry.BandRadius, Brush(BrushKey(tone)), GaugeGeometry.BandWidth);
            if (owner.Target is { } target && Brush(ThemeKeys.Brush.Text) is { } text)
            {
                var (a, b) = GaugeGeometry.Tick(GaugeGeometry.Fraction((double)target, min, max));
                context.DrawLine(new Pen(text, GaugeGeometry.TargetWidth * scale, lineCap: PenLineCap.Round), At(a), At(b));
            }
        }
    }

    /// <summary>读屏：只读的范围值（同 meter），名称是 Label，取值文字是数值与目标。</summary>
    sealed class GaugePeer(Gauge owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Label;

        public bool IsReadOnly => true;

        public double Minimum => (double)owner.Min;

        public double Maximum => (double)owner.Max;

        double IRangeValueProvider.Value => (double)Math.Clamp(owner.Value, owner.Min, Math.Max(owner.Min, owner.Max));

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("仪表盘只读");

        string? IValueProvider.Value => owner.Spoken;

        public void SetValue(string? value) => throw new InvalidOperationException("仪表盘只读");
    }
}

/// <summary>Gauge 的部件名。</summary>
public static class GaugeParts
{
    /// <summary>中间的数值（TS 的 propsValue）。</summary>
    public const string Value = "PART_Value";

    /// <summary>名称（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";
}
