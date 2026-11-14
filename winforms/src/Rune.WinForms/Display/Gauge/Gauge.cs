using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 仪表盘（vue-tsx-pro 的 Gauge）：半圆的刻度上显示一个量落在哪个区间——完成率、利用率、风险分。外圈是分段的区间（危险、提醒、良好），
/// 里面的弧是当前值（颜色取它所在区间的色调），中间是数值，两端是最小、最大值，下面是名称。无窗口元素，放进窗体用 <see cref="GaugeControl"/>。
/// 宽 10em，随字体缩放；刻度盘按 200 × 108 的画布（同 TS 的 viewBox）缩放着画，线宽跟着缩。轨道是 fill 色，数值弧与区间用色调的实色（中性是 borderStrong），
/// 目标刻度是正文色。中间的数值 1.5em、半粗（GDI 按粗体画），压在半圆的底边上；两端 0.75em、次要色；名称 sm、次要色。
/// </summary>
public class Gauge : Element
{
    Text number = Built(null, new TextProps { Weight = TextWeight.Semibold, Numeric = true });
    readonly Text min = new() { Tone = TextTone.Muted, Numeric = true, Wrap = TextWrap.NoWrap };
    readonly Text max = new() { Tone = TextTone.Muted, Numeric = true, Wrap = TextWrap.NoWrap };
    Text label = Built(null, new TextProps { Tone = TextTone.Muted, Align = TextAlign.Center }, wrap: true);
    Rectangle dial;

    /// <summary>当前值。</summary>
    public decimal Value { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>最小值，默认 0。</summary>
    public decimal Min { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>最大值，默认 100。</summary>
    public decimal Max { get; set { field = value; InvalidateMeasure(); } } = 100;

    /// <summary>数值的格式（百分比写 Style = Percent，配 Max = 1）。</summary>
    public NumericFormat? ValueFormat { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>单位：数值后面带上。</summary>
    public string? Unit { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>外圈的区间，按 To 从小到大；不写时外圈不画。</summary>
    public IReadOnlyList<GaugeBand> Bands { get; set { field = value; Invalidate(); } } = [];

    /// <summary>目标：外圈上画一道刻度，读屏念在数值后面。</summary>
    public decimal? Target { get; set { field = value; Invalidate(); } }

    /// <summary>数值弧的色调；不写时取它所在区间的色调，没有区间时 accent。</summary>
    public StatusTone? Tone { get; set { field = value; Invalidate(); } }

    /// <summary>名称：显示在下方，也是读屏的名称。</summary>
    public string? Label { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>中间显示的文字；不写时是格式化的数值。</summary>
    public string? ValueLabel { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>中间数值的部件属性（TS 的 propsValue），逐字段盖在默认的半粗、等宽数字上；没写字号时 1.5em。</summary>
    public TextProps? PropsValue { get; set { field = value; number = Built(value, new TextProps { Weight = TextWeight.Semibold, Numeric = true }); InvalidateMeasure(); } }

    /// <summary>名称的部件属性（TS 的 propsLabel），逐字段盖在默认的次要色、居中上；没写字号时 sm。</summary>
    public TextProps? PropsLabel { get; set { field = value; label = Built(value, new TextProps { Tone = TextTone.Muted, Align = TextAlign.Center }, wrap: true); InvalidateMeasure(); } }

    /// <summary>新建文字元素，套上合并后的部件属性。</summary>
    static Text Built(TextProps? own, TextProps defaults, bool wrap = false)
    {
        var text = new Text();
        if (!wrap) text.Wrap = TextWrap.NoWrap;
        defaults.Merge(own).ApplyTo(text);
        return text;
    }

    /// <summary>数值弧的色调（测试用）。</summary>
    internal StatusTone ShownTone => GaugeGeometry.Tone(Tone, Bands, (double)Value);

    /// <summary>读屏念的取值文字：数值与目标。</summary>
    public string Spoken => GaugeGeometry.Spoken(Value, Target, ValueFormat, Unit, CurrentLang);

    /// <summary>中间显示的文字（测试用）。</summary>
    internal string ShownValue => number.Content;

    void UpdateTexts()
    {
        var lang = CurrentLang;
        var format = ValueFormat ?? NumericFormat.Default;
        number.Content = ValueLabel ?? GaugeGeometry.Text(Value, ValueFormat, Unit, lang);
        min.Content = format.Format(Min, lang.Code, lang.Currency);
        max.Content = format.Format(Max, lang.Code, lang.Currency);
        label.Content = Label ?? "";
    }

    /// <summary>[刻度盘（10em 宽、高 = 宽 × 108 / 200）][两端][名称]，行间 space.xs。</summary>
    Size Arrange(ElementContext context, Point origin)
    {
        UpdateTexts();
        var em = context.Em;
        var width = (int)MathF.Round(10 * em);
        var gap = (int)MathF.Round(Tokens.Space.Xs * em);
        dial = new Rectangle(origin.X, origin.Y, width, (int)MathF.Round(width * (float)(GaugeGeometry.Height / GaugeGeometry.Width)));
        if (PropsValue?.Size is null) number.Size = 1.5f * em * 96f / context.Dpi;
        min.Size = max.Size = 0.75f * em * 96f / context.Dpi;
        if (PropsLabel?.Size is null) label.Size = Tokens.FontSize.Sm;
        var valueSize = number.Measure(context, new Size(int.MaxValue, int.MaxValue));
        // 行高 1：只占 1.5em 高、压在半圆的底边上，字形在里面竖直居中（GDI 的行高是字体自己的，比字号高）
        var line = (int)MathF.Round(1.5f * em);
        number.Bounds = new Rectangle(dial.X + (width - valueSize.Width) / 2, dial.Bottom - line - (valueSize.Height - line) / 2, valueSize.Width, valueSize.Height);
        var y = dial.Bottom + gap;
        var minSize = min.Measure(context, new Size(int.MaxValue, int.MaxValue));
        var maxSize = max.Measure(context, new Size(int.MaxValue, int.MaxValue));
        min.Bounds = new Rectangle(dial.X, y, minSize.Width, minSize.Height);
        max.Bounds = new Rectangle(dial.Right - maxSize.Width, y, maxSize.Width, maxSize.Height);
        y += Math.Max(minSize.Height, maxSize.Height);
        if (label.Content.Length > 0)
        {
            var size = label.Measure(context, new Size(width, int.MaxValue));
            y += gap;
            label.Bounds = new Rectangle(dial.X, y, width, size.Height);
            y += size.Height;
        }
        return new Size(width, y - origin.Y);
    }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) => Arrange(context, Point.Empty);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        Arrange(context, Bounds.Location);
        var colors = context.Theme.Colors;
        Color ToneColor(StatusTone tone) => tone.Value == "neutral" ? colors.BorderStrong : StatusDot.ToneColor(tone, colors);
        var scale = dial.Width / (float)GaugeGeometry.Width;
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            void Arc(double from, double to, double radius, Color color, double width)
            {
                if (to <= from) return;
                using var pen = new Pen(color, (float)(width * scale));
                var r = (float)radius * scale;
                var (cx, cy) = (dial.X + (float)GaugeGeometry.Center * scale, dial.Y + (float)(GaugeGeometry.Center - GaugeGeometry.Top) * scale);
                // GDI 的角度从 x 轴顺时针量：左端 180°，经过上方（270°）到右端 360°
                graphics.DrawArc(pen, cx - r, cy - r, 2 * r, 2 * r, (float)(180 + from * 180), (float)((to - from) * 180));
            }

            var (low, high) = ((double)Min, (double)Max);
            Arc(0, 1, GaugeGeometry.Radius, colors.Fill, GaugeGeometry.ArcWidth);
            Arc(0, GaugeGeometry.Fraction((double)Value, low, high), GaugeGeometry.Radius, ToneColor(ShownTone), GaugeGeometry.ArcWidth);
            foreach (var (from, to, tone) in GaugeGeometry.Bands(Bands, low, high)) Arc(from, to, GaugeGeometry.BandRadius, ToneColor(tone), GaugeGeometry.BandWidth);
            if (Target is { } target)
            {
                var (a, b) = GaugeGeometry.Tick(GaugeGeometry.Fraction((double)target, low, high));
                PointF At((double X, double Y) point) => new(dial.X + (float)point.X * scale, dial.Y + (float)(point.Y - GaugeGeometry.Top) * scale);
                using var pen = new Pen(colors.Text, (float)(GaugeGeometry.TargetWidth * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                graphics.DrawLine(pen, At(a), At(b));
            }
        }
        finally
        {
            graphics.Restore(state);
        }
        var tinted = context with { ForeColor = colors.Text };
        number.Paint(graphics, tinted);
        min.Paint(graphics, tinted);
        max.Paint(graphics, tinted);
        if (label.Content.Length > 0) label.Paint(graphics, tinted);
    }
}

/// <summary>
/// 放进窗体的仪表盘（单元素宿主）：大小跟着内容（宽 10em）。读屏是进度条（只读），名称是 Label，取值是数值与目标。
/// <code>
/// var gauge = new GaugeControl { Value = 0.82m, Max = 1, ValueFormat = new NumericFormat { Style = NumericStyle.Percent }, Label = "完成率", Target = 0.9m };
/// </code>
/// TS 的 size 在这里叫 TextSize（控件自己有 Size）。
/// </summary>
public class GaugeControl : ElementControl<Gauge>
{
    /// <summary>新建：不能获得焦点。</summary>
    public GaugeControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>当前值。</summary>
    [Category("Appearance")]
    [DefaultValue(typeof(decimal), "0")]
    public decimal Value { get => Element.Value; set => Element.Value = value; }

    /// <summary>最小值。</summary>
    [Category("Appearance")]
    [DefaultValue(typeof(decimal), "0")]
    public decimal Min { get => Element.Min; set => Element.Min = value; }

    /// <summary>最大值。</summary>
    [Category("Appearance")]
    [DefaultValue(typeof(decimal), "100")]
    public decimal Max { get => Element.Max; set => Element.Max = value; }

    /// <summary>数值的格式。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? ValueFormat { get => Element.ValueFormat; set => Element.ValueFormat = value; }

    /// <summary>单位。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Unit { get => Element.Unit; set => Element.Unit = value; }

    /// <summary>外圈的区间。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<GaugeBand> Bands { get => Element.Bands; set => Element.Bands = value; }

    /// <summary>目标。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal? Target { get => Element.Target; set => Element.Target = value; }

    /// <summary>数值弧的色调。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StatusTone? Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>名称。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Label { get => Element.Label; set => Element.Label = value; }

    /// <summary>中间显示的文字。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? ValueLabel { get => Element.ValueLabel; set => Element.ValueLabel = value; }

    /// <summary>字号档位：仪表盘的大小随之缩放；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get; set { field = value; ApplyTextSize(); } }

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void ApplyTextSize()
    {
        if (TextSize.Pixels is { } pixels)
        {
            var outer = Parent?.Font ?? Font;
            Font = Fonts.Get(outer.FontFamily.Name, pixels * 72 / 96, outer.Style);
        }
        else ResetFont();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyTextSize();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GaugeAccessibleObject(this);

    /// <summary>读屏：进度条（只读），名称是 Label，取值是数值与目标。</summary>
    sealed class GaugeAccessibleObject(GaugeControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ProgressBar;

        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Label : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Value { get => owner.Element.Spoken; set { } }

        public override AccessibleStates State => (base.State | AccessibleStates.ReadOnly) & ~AccessibleStates.Focusable;
    }
}
