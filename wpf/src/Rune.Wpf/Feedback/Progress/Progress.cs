using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 进度（vue-tsx-pro 的 Progress）：条形（默认）或环形。有 <see cref="Value"/> 时显示完成比例；没有（null）时是不确定进度，循环动画。
/// <code>
/// &lt;rune:Progress Value="{Binding Uploaded}" Max="{Binding Total}" Label="上传" ValueLabel="{Binding UploadedText}" /&gt;
/// &lt;rune:Progress Value="60" AutomationProperties.Name="上传进度" /&gt;      &lt;!-- 只有进度条 --&gt;
/// &lt;rune:Progress AutomationProperties.Name="正在加载" /&gt;                 &lt;!-- 不确定进度 --&gt;
/// &lt;rune:Progress Shape="circle" Value="72" Target="80" Label="本月销售完成率" /&gt;
/// </code>
/// 取值是 decimal（TS 的 number、bigint、Decimal）：按原值夹在 0–Max 之间比较，<see cref="ValueText"/> 拿到的是原值。
/// 条形有标签时上方是标签行（标签与数值，0.93em），右侧默认显示按语言格式化的百分比，<see cref="ValueLabel"/> 换成自己的文字；
/// 环形直径 5em，中间显示数值，标签在下方。<see cref="Target"/> 是目标值：条上画竖线、环上画刻度，读屏念成"60%，目标 80%"。
/// 粗细、直径随字号档位 <see cref="Size"/> 缩放；进度变化在 durations.md 内过渡；不确定进度条形 1.4 秒一轮、环形 1 秒一圈，
/// 系统关闭动画时变慢到 3 秒（它本身就是信息，不停）。读屏：进度条（RangeValue 与取值文字），名称是 Label 或 AutomationProperties.Name；
/// 状态是 <see cref="State"/>（TS 的 data-state）。部件样式键见 <see cref="ProgressParts"/>，外层（TS 的 propsRoot）就是 Progress 自己。
/// </summary>
public class Progress : Control
{
    /// <summary>当前值；null 时是不确定进度。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(decimal?), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>最大值，默认 100。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(decimal), typeof(Progress), new FrameworkPropertyMetadata(100m, OnChanged));

    /// <summary>形状，默认 bar。</summary>
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(ProgressShape), typeof(Progress), new FrameworkPropertyMetadata(ProgressShape.Bar, FrameworkPropertyMetadataOptions.AffectsMeasure, OnChanged));

    /// <summary>目标值（与 Value 同一单位）。</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(decimal?), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>色调；主题默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Progress), new FrameworkPropertyMetadata(default(StatusTone), (d, _) => ((Progress)d).BindTone()));

    /// <summary>字号档位：粗细、直径随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Progress), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>圆角；主题默认 full。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(Radius), typeof(Progress), new FrameworkPropertyMetadata(default(Radius), (d, _) => ((Progress)d).BindRadius()));

    /// <summary>读屏念的取值文字（参数是夹过的值与最大值）；默认是百分比。</summary>
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(Func<decimal, decimal, string>), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>标签：条形在上方、环形在下方，同时是读屏名称。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>显示数值；不写时有标签或 ValueLabel 就显示（不确定进度不显示）。</summary>
    public static readonly DependencyProperty ShowValueLabelProperty = DependencyProperty.Register(
        nameof(ShowValueLabel), typeof(bool?), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>数值文字，替换默认的百分比（只是显示，读屏念的是 ValueText）。</summary>
    public static readonly DependencyProperty ValueLabelProperty = DependencyProperty.Register(
        nameof(ValueLabel), typeof(string), typeof(Progress), new FrameworkPropertyMetadata(null, OnChanged));

    static readonly DependencyPropertyKey StatePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(State), typeof(ProgressState), typeof(Progress), new FrameworkPropertyMetadata(ProgressState.Indeterminate));

    /// <summary>状态：不确定、进行中、完成（主题、使用方的样式触发器用）。</summary>
    public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

    /// <summary>着色部分的比例（0–1，过渡驱动）。</summary>
    static readonly DependencyProperty FractionProperty = DependencyProperty.Register("Fraction", typeof(double), typeof(Progress), new FrameworkPropertyMetadata(0d, OnFrame));

    /// <summary>不确定进度的相位（0–1，循环）。</summary>
    static readonly DependencyProperty PhaseProperty = DependencyProperty.Register("Phase", typeof(double), typeof(Progress), new FrameworkPropertyMetadata(0d, OnFrame));

    static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register("TrackBrush", typeof(Brush), typeof(Progress), new FrameworkPropertyMetadata(null, OnFrame));
    static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register("TextBrush", typeof(Brush), typeof(Progress), new FrameworkPropertyMetadata(null, OnFrame));
    static readonly DependencyProperty IndicatorBrushProperty = DependencyProperty.Register("IndicatorBrush", typeof(Brush), typeof(Progress), new FrameworkPropertyMetadata(null, OnFrame));
    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register("RadiusFactor", typeof(double), typeof(Progress), new FrameworkPropertyMetadata(0d, OnFrame));
    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register("GapFactor", typeof(double), typeof(Progress), new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.AffectsMeasure));
    static readonly DependencyProperty LineWidthProperty = DependencyProperty.Register("LineWidth", typeof(double), typeof(Progress), new FrameworkPropertyMetadata(2d, OnFrame));

    readonly Flex header = new() { Justify = FlexJustify.Between, Align = FlexAlign.Baseline, Gap = Space.Sm };
    readonly Text label = new();
    readonly Text valueLabel = new();
    readonly Text ringLabel = new();
    readonly Text ringValue = new();
    readonly ProgressTrack track;
    readonly ProgressRing ring;
    readonly UIElement[] children;

    static Progress()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Progress), new FrameworkPropertyMetadata(typeof(Progress)));
        FocusableProperty.OverrideMetadata(typeof(Progress), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Progress), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Progress), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((Progress)d).UpdateFonts()));
    }

    /// <summary>新建：颜色、间距引用主题资源。</summary>
    public Progress()
    {
        track = new ProgressTrack(this);
        ring = new ProgressRing(this, ringValue);
        header.SetResourceReference(StyleProperty, ProgressParts.Header);
        foreach (var text in new[] { label, ringLabel }) text.SetResourceReference(StyleProperty, ProgressParts.Label);
        foreach (var text in new[] { valueLabel, ringValue }) text.SetResourceReference(StyleProperty, ProgressParts.ValueLabel);
        SetResourceReference(TrackBrushProperty, "Rune.Brush.Fill");
        SetResourceReference(TextBrushProperty, "Rune.Brush.Text");
        SetResourceReference(GapFactorProperty, "Rune.Space.Xs");
        SetResourceReference(LineWidthProperty, "Rune.BorderWidth.Sm");
        header.Children.Add(label);
        header.Children.Add(valueLabel);
        children = [header, track, ring, ringLabel];
        foreach (var child in children)
        {
            AddVisualChild(child);
            AddLogicalChild(child);
        }
        BindTone();
        BindRadius();
        UpdateFonts();
        Update();
        Loaded += (_, _) => Spin();
        Unloaded += (_, _) => BeginAnimation(PhaseProperty, null);
    }

    /// <summary>当前值；null 时是不确定进度。</summary>
    public decimal? Value { get => (decimal?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最大值。</summary>
    public decimal Max { get => (decimal)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>形状。</summary>
    public ProgressShape Shape { get => (ProgressShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>目标值。</summary>
    public decimal? Target { get => (decimal?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>读屏念的取值文字。</summary>
    public Func<decimal, decimal, string>? ValueText { get => (Func<decimal, decimal, string>?)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <summary>标签。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>显示数值。</summary>
    public bool? ShowValueLabel { get => (bool?)GetValue(ShowValueLabelProperty); set => SetValue(ShowValueLabelProperty, value); }

    /// <summary>数值文字。</summary>
    public string? ValueLabel { get => (string?)GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <summary>状态。</summary>
    public ProgressState State => (ProgressState)GetValue(StateProperty);

    bool Determinate => Value is not null;

    bool Circle => Shape == ProgressShape.Circle;

    double Em => FontSize;

    /// <summary>夹在 0–Max 之间的值（按原值比较）。</summary>
    internal decimal Clamped => Value is not { } value ? 0 : value < 0 ? 0 : value > Max ? Max : value;

    /// <summary>值占 Max 的比例（0–100）。</summary>
    static double PercentOf(decimal value, decimal max) => max <= 0 ? 0 : Math.Clamp((double)(Math.Clamp(value, 0, max) / max) * 100, 0, 100);

    double Percent => PercentOf(Clamped, Max);

    double? TargetPercent => Target is { } target ? PercentOf(target, Max) : null;

    string PercentText(double percent) => (percent / 100).ToString("P0", CultureInfo.GetCultureInfo(Provider.GetLang(this).Code));

    /// <summary>读屏念的取值文字："75%"，有目标时"60%，目标 80%"；不确定进度时为 null。</summary>
    internal string? SpokenValue
    {
        get
        {
            if (!Determinate) return null;
            var spoken = ValueText?.Invoke(Clamped, Max) ?? PercentText(Percent);
            return TargetPercent is { } target ? $"{spoken}，{Provider.GetLang(this).ProgressTarget(PercentText(target))}" : spoken;
        }
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => children.Length;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => children[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => children.GetEnumerator();

    static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Progress)d).Update();

    static void OnFrame(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var progress = (Progress)d;
        progress.track.InvalidateArrange();
        progress.track.InvalidateVisual();
        progress.ring.InvalidateVisual();
    }

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <summary>环形的着色：中性用次要文字色、accent 用强调色，其余状态色用各自的文字色（条形的进度在部件样式的触发器里）。</summary>
    void BindTone() => SetResourceReference(IndicatorBrushProperty, Tone.Value switch
    {
        "neutral" => "Rune.Brush.TextMuted",
        "accent" or null => "Rune.Brush.Accent",
        var tone => $"Rune.Brush.{char.ToUpperInvariant(tone[0])}{tone[1..]}Text",
    });

    void BindRadius()
    {
        if (Radius.ResourceKey is { } key) SetResourceReference(RadiusFactorProperty, key);
        else ClearValue(RadiusFactorProperty);
    }

    /// <summary>按取值重排：哪些部分显示、文字、状态、着色比例的过渡、不确定进度的循环。</summary>
    void Update()
    {
        var determinate = Determinate;
        SetValue(StatePropertyKey, !determinate ? ProgressState.Indeterminate : Clamped >= Max ? ProgressState.Complete : ProgressState.Loading);
        var showValue = determinate && (ShowValueLabel ?? true);
        var hasLabel = !string.IsNullOrEmpty(Label);
        label.Text = ringLabel.Text = Label;
        valueLabel.Text = ringValue.Text = ValueLabel ?? PercentText(Percent);
        // 条形：有标签、或要显示数值时上方加一行；环形：数值在中间，标签在下方
        header.Visibility = Shown(!Circle && (Label is not null || (determinate && (ShowValueLabel ?? ValueLabel is not null))));
        label.Visibility = Shown(hasLabel);
        valueLabel.Visibility = Shown(showValue);
        track.Visibility = Shown(!Circle);
        ring.Visibility = Shown(Circle);
        ringValue.Visibility = Shown(showValue);
        ringLabel.Visibility = Shown(Circle && hasLabel);
        InvalidateMeasure();
        Animate(determinate ? Percent / 100 : 0);
        Spin();
    }

    static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>标签行、环形的数值与标签比所在的文字小一档（0.93em）。</summary>
    void UpdateFonts()
    {
        foreach (var part in new DependencyObject[] { header, ringValue, ringLabel })
            part.SetValue(TextElement.FontSizeProperty, 0.93 * Em);
    }

    /// <summary>着色比例在 durations.md 内过渡到新值；没加载时直接到位。</summary>
    void Animate(double to)
    {
        var from = (double)GetValue(FractionProperty);
        SetValue(FractionProperty, to);
        var duration = IsLoaded ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero || Math.Abs(from - to) < 1e-9)
        {
            BeginAnimation(FractionProperty, null);
            return;
        }
        BeginAnimation(FractionProperty, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>不确定进度：加载后一直循环（条形 1.4 秒、环形 1 秒；系统关闭动画时 3 秒）。</summary>
    void Spin()
    {
        if (Determinate || !IsLoaded)
        {
            BeginAnimation(PhaseProperty, null);
            return;
        }
        var seconds = !Motion.Enabled ? 3 : Circle ? 1 : 1.4;
        BeginAnimation(PhaseProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = Circle ? null : TryFindResource("Rune.Easing.Standard") as IEasingFunction,
        });
    }

    double Gap => (double)GetValue(GapFactorProperty) * Em;

    /// <summary>轨道的粗细：space.xs × 1.5。</summary>
    double Thickness => 1.5 * Gap;

    /// <summary>圆角（full 是粗细的一半）。</summary>
    double CornerRadius => Math.Min(Radius == Radius.Full ? double.MaxValue : (double)GetValue(RadiusFactorProperty) * Em, Thickness / 2);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        if (Circle)
        {
            ring.Measure(constraint);
            ringLabel.Measure(constraint);
            var labelSize = ringLabel.Visibility == Visibility.Visible ? ringLabel.DesiredSize : default;
            return new Size(Math.Max(ring.DesiredSize.Width, labelSize.Width), ring.DesiredSize.Height + (ringLabel.Visibility == Visibility.Visible ? Gap + labelSize.Height : 0));
        }
        header.Measure(constraint);
        var visible = header.Visibility == Visibility.Visible;
        var headerHeight = visible ? header.DesiredSize.Height + Gap : 0;
        // 条形占满一行：宽度由布局给（Flex 里写 Grow），不限宽时取标签行的宽度
        var width = double.IsInfinity(constraint.Width) ? visible ? header.DesiredSize.Width : 0 : constraint.Width;
        track.Measure(new Size(width, Thickness));
        return new Size(width, headerHeight + Thickness);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        if (Circle)
        {
            var side = ring.DesiredSize.Width;
            ring.Arrange(new Rect((arrangeBounds.Width - side) / 2, 0, side, side));
            if (ringLabel.Visibility == Visibility.Visible)
                ringLabel.Arrange(new Rect((arrangeBounds.Width - ringLabel.DesiredSize.Width) / 2, side + Gap, ringLabel.DesiredSize.Width, ringLabel.DesiredSize.Height));
            return arrangeBounds;
        }
        var y = 0d;
        if (header.Visibility == Visibility.Visible)
        {
            header.Arrange(new Rect(0, 0, arrangeBounds.Width, header.DesiredSize.Height));
            y = header.DesiredSize.Height + Gap;
        }
        track.Arrange(new Rect(0, y, arrangeBounds.Width, Thickness));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ProgressAutomationPeer(this);

    /// <summary>
    /// 条形的轨道（TS 的 track）：画底色，里面是进度（部件 <see cref="ProgressParts.Indicator"/>）与目标线；按圆角裁剪。
    /// 不确定进度时进度是四成宽的一段，从起始边外滑到结束边外（从右到左时 WPF 整体镜像）。
    /// </summary>
    sealed class ProgressTrack : FrameworkElement
    {
        readonly Progress owner;
        readonly Border indicator = new();
        readonly Border target = new();

        public ProgressTrack(Progress owner)
        {
            this.owner = owner;
            indicator.SetResourceReference(StyleProperty, ProgressParts.Indicator);
            target.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Text");
            foreach (var child in new UIElement[] { indicator, target })
            {
                AddVisualChild(child);
                AddLogicalChild(child);
            }
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index) => index == 0 ? indicator : target;

        protected override System.Collections.IEnumerator LogicalChildren => new UIElement[] { indicator, target }.GetEnumerator();

        protected override Size MeasureOverride(Size availableSize)
        {
            indicator.Measure(default);
            target.Measure(default);
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (width, height) = (finalSize.Width, finalSize.Height);
            var radius = owner.CornerRadius;
            Clip = new RectangleGeometry(new Rect(finalSize), radius, radius);
            indicator.CornerRadius = new CornerRadius(radius);
            var phase = (double)owner.GetValue(PhaseProperty);
            indicator.Arrange(owner.Determinate
                ? new Rect(0, 0, (double)owner.GetValue(FractionProperty) * width, height)
                : new Rect((-0.4 + 1.4 * phase) * width, 0, 0.4 * width, height));
            target.Visibility = owner.TargetPercent is null ? Visibility.Collapsed : Visibility.Visible;
            var line = (double)owner.GetValue(LineWidthProperty);
            target.Arrange(new Rect((owner.TargetPercent ?? 0) / 100 * width - line / 2, 0, line, height));
            return finalSize;
        }

        protected override void OnRender(DrawingContext context)
        {
            var radius = owner.CornerRadius;
            context.DrawRoundedRectangle((Brush?)owner.GetValue(TrackBrushProperty), null, new Rect(RenderSize), radius, radius);
        }
    }

    /// <summary>
    /// 环形（TS 的 circle）：直径 5em，按 100 × 100 的画布画轨道（线宽 10、半径 45）、从正上方顺时针的进度弧与目标刻度；
    /// 中间放数值。不确定进度时四分之一圈的弧一直转。圆角 none 时弧的端头是平的。
    /// </summary>
    sealed class ProgressRing : FrameworkElement
    {
        readonly Progress owner;
        readonly Text value;

        public ProgressRing(Progress owner, Text value)
        {
            (this.owner, this.value) = (owner, value);
            AddVisualChild(value);
            AddLogicalChild(value);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => value;

        protected override System.Collections.IEnumerator LogicalChildren => new[] { value }.GetEnumerator();

        protected override Size MeasureOverride(Size availableSize)
        {
            value.Measure(availableSize);
            return new Size(5 * owner.Em, 5 * owner.Em);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = value.DesiredSize;
            value.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            return finalSize;
        }

        protected override void OnRender(DrawingContext context)
        {
            var scale = RenderSize.Width / 100;
            var center = new Point(50 * scale, 50 * scale);
            var (radius, stroke) = (45 * scale, 10 * scale);
            context.DrawEllipse(null, new Pen((Brush?)owner.GetValue(TrackBrushProperty), stroke), center, radius, radius);
            var cap = owner.Radius == Radius.None ? PenLineCap.Flat : PenLineCap.Round;
            var indicator = new Pen((Brush?)owner.GetValue(IndicatorBrushProperty), stroke) { StartLineCap = cap, EndLineCap = cap };
            var (start, sweep) = owner.Determinate ? (0d, (double)owner.GetValue(FractionProperty)) : ((double)owner.GetValue(PhaseProperty), 0.25);
            if (sweep >= 0.9999) context.DrawEllipse(null, indicator, center, radius, radius);
            else if (sweep > 0) context.DrawGeometry(null, indicator, Arc(center, radius, start, sweep));
            if (owner.TargetPercent is not { } target) return;
            // 目标刻度：从轨道外缘外 2 到内缘内 2 的一道线
            var angle = target / 100 * 2 * Math.PI - Math.PI / 2;
            Point At(double distance) => new(center.X + distance * scale * Math.Cos(angle), center.Y + distance * scale * Math.Sin(angle));
            var tick = new Pen((Brush?)owner.GetValue(TextBrushProperty), 2.5 * scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            context.DrawLine(tick, At(45 + 5 + 2), At(45 - 5 - 2));
        }

        /// <summary>从 start（圈数，0 在正上方）顺时针画 sweep 圈的弧。</summary>
        static StreamGeometry Arc(Point center, double radius, double start, double sweep)
        {
            Point At(double turns) => new(center.X + radius * Math.Sin(turns * 2 * Math.PI), center.Y - radius * Math.Cos(turns * 2 * Math.PI));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(At(start), false, false);
                context.ArcTo(At(start + sweep), new Size(radius, radius), 0, sweep > 0.5, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            return geometry;
        }
    }

    /// <summary>读屏：进度条（RangeValue，取值文字在 Value 里），名称默认取标签；里面的文字不重复念。</summary>
    sealed class ProgressAutomationPeer(Progress owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(Progress);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Label ?? "";

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface is PatternInterface.RangeValue or PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => true;

        public double Minimum => 0;

        public double Maximum => (double)owner.Max;

        double IRangeValueProvider.Value => (double)owner.Clamped;

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("进度只读");

        string IValueProvider.Value => owner.SpokenValue ?? "";

        public void SetValue(string value) => throw new InvalidOperationException("进度只读");
    }
}

/// <summary>Progress 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ProgressParts.Indicator}" TargetType="Border"&gt;</c>。</summary>
public static class ProgressParts
{
    /// <summary>条形的进度（着色部分，TS 的 propsIndicator）：Border，默认底色按色调。</summary>
    public static readonly PartKey Indicator = new(typeof(Progress), nameof(Indicator), typeof(Border));

    /// <summary>标签行（TS 的 propsHeader）：Flex。</summary>
    public static readonly PartKey Header = new(typeof(Progress), nameof(Header), typeof(Flex));

    /// <summary>标签（TS 的 propsLabel）：Text；环形的在下方，次要色、居中。</summary>
    public static readonly PartKey Label = new(typeof(Progress), nameof(Label), typeof(Text));

    /// <summary>数值文字（TS 的 propsValueLabel）：Text，等宽数字；条形的次要色，环形的半粗。</summary>
    public static readonly PartKey ValueLabel = new(typeof(Progress), nameof(ValueLabel), typeof(Text));
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
