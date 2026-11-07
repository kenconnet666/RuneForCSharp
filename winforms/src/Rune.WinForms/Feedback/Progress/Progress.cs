using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 进度（vue-tsx-pro 的 Progress）：条形（默认）或环形。有 <see cref="Value"/> 时显示完成比例；没有（null）时是不确定进度，循环动画。
/// 取值是 decimal（TS 的 number、bigint、Decimal）：按原值夹在 0–Max 之间比较，<see cref="ValueText"/> 拿到的是原值。
/// 条形有标签时上方是标签行（标签与数值，0.93em），右侧默认显示按语言格式化的百分比，<see cref="ValueLabel"/> 换成自己的文字；
/// 环形直径 5em，中间显示数值，标签在下方。<see cref="Target"/> 是目标值：条上画竖线、环上画刻度，读屏念成"60%，目标 80%"。
/// 粗细、直径随字号档位缩放；进度变化在 durations.md 内过渡；不确定进度条形 1.4 秒一轮、环形 1 秒一圈，系统关闭动画时变慢到 3 秒。
/// 控件是 <see cref="ProgressControl"/>。
/// </summary>
public class Progress : Element
{
    readonly Text label = new();
    readonly Text valueLabel = new();
    readonly Signal<double> fraction = new(0);

    /// <summary>当前值；null 时是不确定进度。</summary>
    public decimal? Value { get; set { field = value; Update(); } }

    /// <summary>最大值，默认 100。</summary>
    public decimal Max { get; set { field = value; Update(); } } = 100;

    /// <summary>形状；不写时 bar。</summary>
    public ProgressShape Shape { get => field.Value is null ? ProgressShape.Bar : field; set { field = value; Update(); } }

    /// <summary>目标值（与 Value 同一单位）。</summary>
    public decimal? Target { get; set { field = value; Update(); } }

    /// <summary>色调；不写时 accent。</summary>
    public StatusTone Tone { get => field.Value is null ? StatusTone.Accent : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位：粗细、直径随之缩放；不写时跟随宿主的字体。</summary>
    public TextSize Size { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>圆角；不写时 full。</summary>
    public Radius Radius { get => field.Value is null ? Radius.Full : field; set { field = value; Invalidate(); } }

    /// <summary>读屏念的取值文字（参数是夹过的值与最大值）；默认是百分比。</summary>
    public Func<decimal, decimal, string>? ValueText { get; set; }

    /// <summary>标签：条形在上方、环形在下方，同时是读屏名称。</summary>
    public string? Label { get; set { field = value; Update(); } }

    /// <summary>显示数值；不写时有标签或 ValueLabel 就显示（不确定进度不显示）。</summary>
    public bool? ShowValueLabel { get; set { field = value; Update(); } }

    /// <summary>数值文字，替换默认的百分比（只是显示，读屏念的是 ValueText）。</summary>
    public string? ValueLabel { get; set { field = value; Update(); } }

    /// <summary>标签部件的属性（TS 的 propsLabel），盖在组件给的默认上。</summary>
    public TextProps? PropsLabel { get; set { field = value; Update(); } }

    /// <summary>数值部件的属性（TS 的 propsValueLabel），盖在组件给的默认上。</summary>
    public TextProps? PropsValueLabel { get; set { field = value; Update(); } }

    /// <summary>状态：不确定、进行中、完成。</summary>
    public ProgressState State => Value is null ? ProgressState.Indeterminate : Clamped >= Max ? ProgressState.Complete : ProgressState.Loading;

    bool Circle => Shape == ProgressShape.Circle;

    /// <summary>夹在 0–Max 之间的值（按原值比较）。</summary>
    internal decimal Clamped => Value is not { } value ? 0 : value < 0 ? 0 : value > Max ? Max : value;

    static double PercentOf(decimal value, decimal max) => max <= 0 ? 0 : Math.Clamp((double)(Math.Clamp(value, 0, max) / max) * 100, 0, 100);

    double Percent => PercentOf(Clamped, Max);

    double? TargetPercent => Target is { } target ? PercentOf(target, Max) : null;

    string PercentText(double percent) => (percent / 100).ToString("P0", CultureInfo.GetCultureInfo(CurrentLang.Code));

    /// <summary>读屏念的取值文字："75%"，有目标时"60%，目标 80%"；不确定进度时为 null。</summary>
    public string? SpokenValue
    {
        get
        {
            if (Value is null) return null;
            var spoken = ValueText?.Invoke(Clamped, Max) ?? PercentText(Percent);
            return TargetPercent is { } target ? $"{spoken}，{CurrentLang.ProgressTarget(PercentText(target))}" : spoken;
        }
    }

    /// <summary>标签行显示吗（条形）：有标签，或要显示数值。</summary>
    bool HeaderShown => !Circle && (Label is not null || (Value is not null && (ShowValueLabel ?? ValueLabel is not null)));

    bool ValueShown => Value is not null && (ShowValueLabel ?? true);

    bool LabelShown => !string.IsNullOrEmpty(Label);

    /// <summary>显示的文字（测试、inspect 用）。</summary>
    internal IEnumerable<string> VisibleTexts =>
        (Circle ? [ValueShown ? valueLabel.Content : null, LabelShown ? label.Content : null] : HeaderShown ? [LabelShown ? label.Content : null, ValueShown ? valueLabel.Content : null] : Array.Empty<string?>())
        .OfType<string>();

    /// <summary>标签与数值部件（测试用）。</summary>
    internal (Text Label, Text ValueLabel) Parts => (label, valueLabel);

    /// <summary>
    /// 文字与部件按形状重设：数值等宽，条形的次要色、环形的半粗；环形下方的标签次要色、居中；字号比所在的文字小一档（0.93em，
    /// 在 Measure 里按宿主字体算）。进度比例在 durations.md 内过渡（宿主还没显示时直接到位）。
    /// </summary>
    void Update()
    {
        label.Content = Label ?? "";
        valueLabel.Content = ValueLabel ?? PercentText(Percent);
        foreach (var text in new[] { label, valueLabel }) (text.Tone, text.Weight, text.Align, text.Numeric) = (default, default, null, false);
        new TextProps { Tone = Circle ? TextTone.Muted : null, Align = Circle ? TextAlign.Center : null }.Merge(PropsLabel).ApplyTo(label);
        new TextProps { Numeric = true, Tone = Circle ? null : TextTone.Muted, Weight = Circle ? TextWeight.Semibold : null }.Merge(PropsValueLabel).ApplyTo(valueLabel);
        var shown = Host is Control { IsHandleCreated: true, Visible: true };
        Animator.Animate(fraction, Value is null ? 0 : Percent / 100, shown ? Tokens.Duration.Md : TimeSpan.Zero, Easing.Standard);
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>标签与数值的字号：0.93em（像素数，档位 TextSize 是设备无关像素）。</summary>
    void SizeTexts(ElementContext context)
    {
        var small = (TextSize)(0.93f * context.Font.SizeInPoints * 96 / 72);
        foreach (var text in new[] { label, valueLabel }) text.Size = PropsSize(text == label ? PropsLabel : PropsValueLabel) ?? small;
    }

    static TextSize? PropsSize(TextProps? props) => props?.Size;

    static float Gap(ElementContext context) => Tokens.Space.Xs * context.Em;

    static float Thickness(ElementContext context) => 1.5f * Gap(context);

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        context = context.Sized(Size);
        SizeTexts(context);
        var gap = Gap(context);
        if (Circle)
        {
            var side = (int)MathF.Ceiling(5 * context.Em);
            if (!LabelShown) return new Size(side, side);
            var size = label.Measure(context, available);
            return new Size(Math.Max(side, size.Width), side + (int)MathF.Round(gap) + size.Height);
        }
        var header = HeaderShown ? Header(context, available.Width).Row : default;
        // 条形占满一行：宽度由布局给，不限宽时取标签行的宽度
        var width = available.Width == int.MaxValue ? header.Width : available.Width;
        return new Size(width, (HeaderShown ? header.Height + (int)MathF.Round(gap) : 0) + (int)MathF.Ceiling(Thickness(context)));
    }

    /// <summary>标签行：标签与数值排一行，中间至少 space.sm；标签放不下时换行，数值不挤。</summary>
    (Size Label, Size Value, Size Row) Header(ElementContext context, int available)
    {
        var value = ValueShown ? valueLabel.Measure(context, new Size(int.MaxValue, int.MaxValue)) : default;
        var spacing = LabelShown && ValueShown ? (int)MathF.Round(Tokens.Space.Sm * 0.93f * context.Em) : 0;
        var limit = available == int.MaxValue ? int.MaxValue : Math.Max(0, available - value.Width - spacing);
        var labelSize = LabelShown ? label.Measure(context, new Size(limit, int.MaxValue)) : default;
        return (labelSize, value, new Size(labelSize.Width + spacing + value.Width, Math.Max(labelSize.Height, value.Height)));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        context = context.Sized(Size);
        SizeTexts(context);
        var colors = context.Theme.Colors;
        var indicatorColor = Tone.Value switch
        {
            "neutral" => colors.TextMuted,
            "info" => colors.InfoText,
            "success" => colors.SuccessText,
            "warning" => colors.WarningText,
            "danger" => colors.DangerText,
            _ => colors.Accent,
        };
        if (Circle) PaintRing(graphics, context, indicatorColor);
        else PaintBar(graphics, context, indicatorColor);
    }

    void PaintBar(Graphics graphics, ElementContext context, Color indicatorColor)
    {
        var colors = context.Theme.Colors;
        var (x, y, width) = (Bounds.X, (float)Bounds.Y, Bounds.Width);
        if (HeaderShown)
        {
            var (labelSize, value, header) = Header(context, width);
            // 标签在起始边、数值在结束边（从右到左时对调）
            var (labelX, valueX) = context.RightToLeft ? (x + width - labelSize.Width, x) : (x, x + width - value.Width);
            if (LabelShown)
            {
                label.Bounds = new Rectangle(labelX, Bounds.Y, labelSize.Width, labelSize.Height);
                label.Paint(graphics, context);
            }
            if (ValueShown)
            {
                valueLabel.Bounds = new Rectangle(valueX, Bounds.Y + header.Height - value.Height, value.Width, value.Height);
                valueLabel.Paint(graphics, context);
            }
            y += header.Height + MathF.Round(Gap(context));
        }
        var thickness = Thickness(context);
        var track = new RectangleF(x, y, width, thickness);
        var radius = MathF.Min((Radius.In(context.Theme.Radii) ?? 0) * context.Em, thickness / 2);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Drawing.RoundedRectangle(track, radius);
            using (var fill = new SolidBrush(colors.Fill)) graphics.FillPath(fill, path);
            graphics.SetClip(path, CombineMode.Intersect);
            var (start, length) = Value is null ? IndeterminateSegment() : (0f, (float)fraction.Value);
            // 不确定进度：四成宽的一段从起始边外滑到结束边外；从右到左时从右往左
            var segment = new RectangleF(context.RightToLeft ? x + width * (1 - start - length) : x + width * start, y, width * length, thickness);
            if (segment.Width > 0)
            {
                using var indicator = Drawing.RoundedRectangle(segment, radius);
                using var brush = new SolidBrush(indicatorColor);
                graphics.FillPath(brush, indicator);
            }
            if (TargetPercent is { } target)
            {
                var line = Tokens.BorderWidth.Sm * context.Dpi / 96f;
                var at = (float)(target / 100) * width;
                using var brush = new SolidBrush(colors.Text);
                graphics.FillRectangle(brush, (context.RightToLeft ? x + width - at : x + at) - line / 2, y, line, thickness);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>不确定进度的那一段（起点、长度，按轨道宽度的比例）：1.4 秒一轮、标准缓动；系统关闭动画时 3 秒。</summary>
    static (float Start, float Length) IndeterminateSegment()
    {
        var period = Motion.Enabled ? 1.4 : 3;
        var phase = Easing.Standard.Ease(FrameClock.Shared.Now.TotalSeconds % period / period);
        return ((float)(-0.4 + 1.4 * phase), 0.4f);
    }

    void PaintRing(Graphics graphics, ElementContext context, Color indicatorColor)
    {
        var colors = context.Theme.Colors;
        var side = 5 * context.Em;
        var ring = new RectangleF(Bounds.X + (Bounds.Width - side) / 2, Bounds.Y, side, side);
        var scale = side / 100;
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var circle = RectangleF.Inflate(ring, -5 * scale, -5 * scale);
            using (var track = new Pen(colors.Fill, 10 * scale)) graphics.DrawEllipse(track, circle);
            // 从正上方顺时针；不确定进度时四分之一圈一直转（1 秒一圈，系统关闭动画时 3 秒）
            var (start, sweep) = Value is null ? ((float)(FrameClock.Shared.Now.TotalSeconds % (Motion.Enabled ? 1 : 3) / (Motion.Enabled ? 1 : 3)), 0.25f) : (0f, (float)fraction.Value);
            using var indicator = new Pen(indicatorColor, 10 * scale);
            if (Radius != Radius.None) indicator.StartCap = indicator.EndCap = LineCap.Round;
            if (sweep >= 0.9999f) graphics.DrawEllipse(indicator, circle);
            else if (sweep > 0) graphics.DrawArc(indicator, circle, -90 + start * 360, sweep * 360);
            if (TargetPercent is { } target)
            {
                var angle = target / 100 * 2 * Math.PI - Math.PI / 2;
                var center = new PointF(ring.X + 50 * scale, ring.Y + 50 * scale);
                PointF At(double distance) => new(center.X + (float)(distance * scale * Math.Cos(angle)), center.Y + (float)(distance * scale * Math.Sin(angle)));
                using var tick = new Pen(colors.Text, 2.5f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                graphics.DrawLine(tick, At(45 + 5 + 2), At(45 - 5 - 2));
            }
        }
        finally
        {
            graphics.Restore(state);
        }
        if (ValueShown)
        {
            var size = valueLabel.Measure(context, new Size(int.MaxValue, int.MaxValue));
            valueLabel.Bounds = new Rectangle((int)MathF.Round(ring.X + (side - size.Width) / 2), (int)MathF.Round(ring.Y + (side - size.Height) / 2), size.Width, size.Height);
            valueLabel.Paint(graphics, context);
        }
        if (LabelShown)
        {
            var size = label.Measure(context, new Size(Bounds.Width, int.MaxValue));
            label.Bounds = new Rectangle(Bounds.X + (Bounds.Width - size.Width) / 2, (int)MathF.Round(ring.Bottom + MathF.Round(Gap(context))), size.Width, size.Height);
            label.Paint(graphics, context);
        }
    }
}

/// <summary>
/// 进度控件（vue-tsx-pro 的 Progress）：承载 <see cref="Progress"/> 元素。
/// <code>
/// var upload = new ProgressControl { Value = uploaded, Max = total, Label = "上传", ValueLabel = $"{uploaded} / {total}", Dock = DockStyle.Top };
/// var ring = new ProgressControl { Shape = ProgressShape.Circle, Value = 72, Target = 80, Label = "本月销售完成率" };
/// </code>
/// 条形占满一行（停靠、锚定或 Flex 的 Grow 给宽度），环形按内容大小。读屏是进度条，名称是 AccessibleName 或 Label，
/// 值是取值文字（"60%，目标 80%"）；WinForms 的自定义控件给不了 RangeValue（最小、最大、当前的数值）。
/// 部件：PropsLabel、PropsValueLabel（<see cref="TextProps"/>）；进度、标签行这类没有元素的部件等部件记录（DESIGN §7）。
/// </summary>
public class ProgressControl : ElementControl<Progress>
{
    /// <summary>新建：不能获得焦点。</summary>
    public ProgressControl() => SetStyle(ControlStyles.Selectable, false);

    /// <summary>当前值；null 时是不确定进度。</summary>
    [Category("Behavior")]
    [Description("当前值；不写是不确定进度。")]
    [DefaultValue(null)]
    public decimal? Value { get => Element.Value; set { Element.Value = value; AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1); } }

    /// <summary>最大值。</summary>
    [Category("Behavior")]
    [Description("最大值，默认 100。")]
    [DefaultValue(typeof(decimal), "100")]
    public decimal Max { get => Element.Max; set => Element.Max = value; }

    /// <summary>形状。</summary>
    [Category("Appearance")]
    [Description("形状：bar（默认）、circle。")]
    public ProgressShape Shape { get => Element.Shape; set => Element.Shape = value; }

    /// <summary>目标值。</summary>
    [Category("Behavior")]
    [Description("目标值：条上画竖线、环上画刻度。")]
    [DefaultValue(null)]
    public decimal? Target { get => Element.Target; set => Element.Target = value; }

    /// <summary>色调。</summary>
    [Category("Appearance")]
    [Description("色调：neutral、accent（默认）、info、success、warning、danger。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>字号档位。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；粗细、直径随之缩放。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>圆角。</summary>
    [Category("Appearance")]
    [Description("圆角：none、xs、sm、md、lg、xl、full（默认）。")]
    public Radius Radius { get => Element.Radius; set => Element.Radius = value; }

    /// <summary>标签。</summary>
    [Category("Appearance")]
    [Description("标签：条形在上方、环形在下方，同时是读屏名称。")]
    [DefaultValue(null)]
    public string? Label { get => Element.Label; set => Element.Label = value; }

    /// <summary>显示数值。</summary>
    [Category("Appearance")]
    [Description("显示数值；不写时有标签或 ValueLabel 就显示。")]
    [DefaultValue(null)]
    public bool? ShowValueLabel { get => Element.ShowValueLabel; set => Element.ShowValueLabel = value; }

    /// <summary>数值文字。</summary>
    [Category("Appearance")]
    [Description("数值文字，替换默认的百分比（只是显示）。")]
    [DefaultValue(null)]
    public string? ValueLabel { get => Element.ValueLabel; set => Element.ValueLabel = value; }

    /// <summary>读屏念的取值文字（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<decimal, decimal, string>? ValueText { get => Element.ValueText; set => Element.ValueText = value; }

    /// <summary>标签部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsLabel { get => Element.PropsLabel; set => Element.PropsLabel = value; }

    /// <summary>数值部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsValueLabel { get => Element.PropsValueLabel; set => Element.PropsValueLabel = value; }

    /// <summary>状态：不确定、进行中、完成。</summary>
    [Browsable(false)]
    public ProgressState State => Element.State;

    bool ShouldSerializeShape() => Shape != ProgressShape.Bar;

    void ResetShape() => Shape = default;

    bool ShouldSerializeTone() => Tone != StatusTone.Accent;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    void ResetTextSize() => TextSize = default;

    bool ShouldSerializeRadius() => Radius != Radius.Full;

    void ResetRadius() => Radius = default;

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ProgressAccessibleObject(this);

    sealed class ProgressAccessibleObject(ProgressControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ProgressBar;

        public override string? Name { get => string.IsNullOrEmpty(owner.AccessibleName) ? owner.Label : owner.AccessibleName; set => owner.AccessibleName = value; }

        public override string? Value { get => owner.Element.SpokenValue ?? ""; set { } }
    }
}

/// <summary>进度的状态（TS 的 data-state）。</summary>
public enum ProgressState
{
    /// <summary>不确定进度（没有 Value）。</summary>
    Indeterminate,

    /// <summary>进行中。</summary>
    Loading,

    /// <summary>完成（Value 到了 Max）。</summary>
    Complete,
}

/// <summary>进度的形状（vue-tsx-pro 的 ProgressShapes）。</summary>
/// <param name="Value">取值（bar、circle）。</param>
[TypeConverter(typeof(VariantConverter<ProgressShape>))]
public readonly record struct ProgressShape(string Value) : IVariant<ProgressShape>
{
    /// <summary>条形（默认）：占满一行。</summary>
    public static readonly ProgressShape Bar = new("bar");

    /// <summary>环形：中间显示数值，适合仪表盘、卡片里的完成率；直径随字号缩放。</summary>
    public static readonly ProgressShape Circle = new("circle");

    /// <inheritdoc />
    public static IReadOnlyList<ProgressShape> Known { get; } = [Bar, Circle];

    /// <inheritdoc />
    public static ProgressShape Parse(string value)
    {
        var shape = new ProgressShape(value);
        Variants.WarnIfUnknown(shape);
        return shape;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
