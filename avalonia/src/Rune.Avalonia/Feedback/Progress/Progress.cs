using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

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
/// 状态是伪类 :indeterminate、:complete（TS 的 data-state）。部件见 <see cref="ProgressParts"/>。
/// </summary>
public class Progress : Control
{
    /// <summary>当前值；null 时是不确定进度。</summary>
    public static readonly StyledProperty<decimal?> ValueProperty = AvaloniaProperty.Register<Progress, decimal?>(nameof(Value));

    /// <summary>最大值，默认 100。</summary>
    public static readonly StyledProperty<decimal> MaxProperty = AvaloniaProperty.Register<Progress, decimal>(nameof(Max), 100);

    /// <summary>形状，默认 bar。</summary>
    public static readonly StyledProperty<ProgressShape> ShapeProperty = AvaloniaProperty.Register<Progress, ProgressShape>(nameof(Shape), ProgressShape.Bar);

    /// <summary>目标值（与 Value 同一单位）。</summary>
    public static readonly StyledProperty<decimal?> TargetProperty = AvaloniaProperty.Register<Progress, decimal?>(nameof(Target));

    /// <summary>色调，默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Progress, StatusTone>(nameof(Tone), StatusTone.Accent);

    /// <summary>字号档位：粗细、直径随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Progress, TextSize>(nameof(Size));

    /// <summary>圆角，默认 full。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Progress, Radius>(nameof(Radius), Radius.Full);

    /// <summary>读屏念的取值文字（参数是夹过的值与最大值）；默认是百分比。</summary>
    public static readonly StyledProperty<Func<decimal, decimal, string>?> ValueTextProperty = AvaloniaProperty.Register<Progress, Func<decimal, decimal, string>?>(nameof(ValueText));

    /// <summary>标签：条形在上方、环形在下方，同时是读屏名称。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Progress, string?>(nameof(Label));

    /// <summary>显示数值；不写时有标签或 ValueLabel 就显示（不确定进度不显示）。</summary>
    public static readonly StyledProperty<bool?> ShowValueLabelProperty = AvaloniaProperty.Register<Progress, bool?>(nameof(ShowValueLabel));

    /// <summary>数值文字，替换默认的百分比（只是显示，读屏念的是 ValueText）。</summary>
    public static readonly StyledProperty<string?> ValueLabelProperty = AvaloniaProperty.Register<Progress, string?>(nameof(ValueLabel));

    /// <summary>字号（继承），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Progress>();

    /// <summary>状态：不确定、进行中、完成。</summary>
    public static readonly DirectProperty<Progress, ProgressState> StateProperty = AvaloniaProperty.RegisterDirect<Progress, ProgressState>(nameof(State), o => o.State);

    /// <summary>着色部分的比例（0–1，过渡驱动）。</summary>
    static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<Progress, double>("Fraction");

    /// <summary>不确定进度的相位（0–1，循环）。</summary>
    static readonly StyledProperty<double> PhaseProperty = AvaloniaProperty.Register<Progress, double>("Phase");

    static readonly StyledProperty<Color> TrackColorProperty = AvaloniaProperty.Register<Progress, Color>("TrackColor");
    static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<Progress, Color>("TextColor");
    static readonly StyledProperty<Color> IndicatorColorProperty = AvaloniaProperty.Register<Progress, Color>("IndicatorColor");
    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Progress, double>("RadiusFactor");
    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<Progress, double>("GapFactor", 0.25);
    static readonly StyledProperty<double> LineWidthProperty = AvaloniaProperty.Register<Progress, double>("LineWidth", 2);

    readonly Flex header = new() { Name = ProgressParts.Header, Justify = FlexJustify.Between, Align = FlexAlign.Baseline, Gap = Space.Sm };
    readonly Text label = new() { Name = ProgressParts.Label };
    readonly Text valueLabel = new() { Name = ProgressParts.ValueLabel };
    readonly Text ringLabel = new() { Name = ProgressParts.Label };
    readonly Text ringValue = new() { Name = ProgressParts.ValueLabel };
    readonly ProgressTrack track;
    readonly ProgressRing ring;
    readonly TemplateResources resources;
    CancellationTokenSource? fill;
    CancellationTokenSource? spin;
    ProgressState state = ProgressState.Indeterminate;

    static Progress()
    {
        AffectsMeasure<Progress>(FontSizeProperty, ShapeProperty, GapFactorProperty);
        AffectsRender<Progress>(TrackColorProperty, TextColorProperty, IndicatorColorProperty, RadiusFactorProperty, LineWidthProperty);
    }

    /// <summary>新建：颜色、间距绑到主题。</summary>
    public Progress()
    {
        resources = new TemplateResources(this);
        resources.Bind(TrackColorProperty, "Rune.Color.Fill");
        resources.Bind(TextColorProperty, "Rune.Color.Text");
        resources.Bind(GapFactorProperty, "Rune.Space.Xs");
        resources.Bind(LineWidthProperty, "Rune.BorderWidth.Sm");
        resources.Bind(RadiusFactorProperty, Radius.Full.ResourceKey);
        BindTone();
        track = new ProgressTrack(this);
        ring = new ProgressRing(this, ringValue);
        header.Children.AddRange([label, valueLabel]);
        // 读屏从进度条的名称与取值得知这些文字，不重复念
        foreach (var part in new Control[] { header, track, ring, ringLabel })
            AutomationProperties.SetAccessibilityView(part, AccessibilityView.Raw);
        LogicalChildren.AddRange([header, track, ring, ringLabel]);
        VisualChildren.AddRange([header, track, ring, ringLabel]);
        UpdateFonts();
        Update();
    }

    /// <summary>当前值；null 时是不确定进度。</summary>
    public decimal? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最大值。</summary>
    public decimal Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>形状。</summary>
    public ProgressShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>目标值。</summary>
    public decimal? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>读屏念的取值文字。</summary>
    public Func<decimal, decimal, string>? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <summary>标签。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>显示数值。</summary>
    public bool? ShowValueLabel { get => GetValue(ShowValueLabelProperty); set => SetValue(ShowValueLabelProperty, value); }

    /// <summary>数值文字。</summary>
    public string? ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>状态。</summary>
    public ProgressState State { get => state; private set => SetAndRaise(StateProperty, ref state, value); }

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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == ToneProperty) BindTone();
        else if (change.Property == RadiusProperty) resources.Bind(RadiusFactorProperty, Radius.ResourceKey);
        else if (change.Property == ValueProperty || change.Property == MaxProperty || change.Property == ShapeProperty || change.Property == TargetProperty
                 || change.Property == LabelProperty || change.Property == ShowValueLabelProperty || change.Property == ValueLabelProperty
                 || change.Property == ValueTextProperty) Update();
        else if (change.Property == FractionProperty || change.Property == PhaseProperty || change.Property == TrackColorProperty
                 || change.Property == IndicatorColorProperty || change.Property == TextColorProperty || change.Property == RadiusFactorProperty
                 || change.Property == LineWidthProperty)
        {
            track.InvalidateArrange();
            track.InvalidateVisual();
            ring.InvalidateVisual();
            if (change.Property == IndicatorColorProperty || change.Property == TextColorProperty) track.UpdateColors();
        }
        if (change.Property == FontSizeProperty) UpdateFonts();
    }

    /// <summary>着色：中性用次要文字色、accent 用强调色，其余状态色用各自的文字色（与轨道之间有 3:1）。</summary>
    void BindTone() => resources.Bind(IndicatorColorProperty, Tone.Value switch
    {
        "neutral" => "Rune.Color.TextMuted",
        "accent" or null => "Rune.Color.Accent",
        var tone => $"Rune.Color.{char.ToUpperInvariant(tone[0])}{tone[1..]}Text",
    });

    /// <summary>按取值重排：哪些部分显示、文字、状态、着色比例的过渡、不确定进度的循环。</summary>
    void Update()
    {
        var determinate = Determinate;
        State = !determinate ? ProgressState.Indeterminate : Clamped >= Max ? ProgressState.Complete : ProgressState.Loading;
        PseudoClasses.Set(":indeterminate", !determinate);
        PseudoClasses.Set(":complete", State == ProgressState.Complete);
        var showValue = determinate && (ShowValueLabel ?? true);
        var hasLabel = !string.IsNullOrEmpty(Label);
        label.Text = ringLabel.Text = Label;
        valueLabel.Text = ringValue.Text = ValueLabel ?? PercentText(Percent);
        // 条形：有标签、或要显示数值时上方加一行；环形：数值在中间，标签在下方
        header.IsVisible = !Circle && (Label is not null || (determinate && (ShowValueLabel ?? ValueLabel is not null)));
        label.IsVisible = hasLabel;
        valueLabel.IsVisible = showValue;
        track.IsVisible = !Circle;
        ring.IsVisible = Circle;
        // 环形按内容宽（TS 是 inline-grid），条形占满一行
        SetValue(HorizontalAlignmentProperty, Circle ? HorizontalAlignment.Left : HorizontalAlignment.Stretch, BindingPriority.Template);
        ringValue.IsVisible = showValue;
        ringLabel.IsVisible = Circle && hasLabel;
        InvalidateMeasure();
        Animate(determinate ? Percent / 100 : 0);
        Spin();
    }

    /// <summary>标签行、环形的数值与标签比所在的文字小一档（0.93em）。</summary>
    void UpdateFonts()
    {
        foreach (var part in new Control[] { header, ringValue, ringLabel })
            part.SetValue(TextElement.FontSizeProperty, 0.93 * Em, BindingPriority.Template);
    }

    /// <summary>着色比例在 durations.md 内过渡到新值；没挂上时直接到位。</summary>
    void Animate(double to)
    {
        var from = GetValue(FractionProperty);
        fill?.Cancel();
        fill = null;
        SetValue(FractionProperty, to);
        var duration = VisualRoot is null ? TimeSpan.Zero
            : Motion.Duration(this.TryFindResource("Rune.Duration.Md", ActualThemeVariant, out var value) && value is TimeSpan md ? md : TimeSpan.FromMilliseconds(200));
        if (duration <= TimeSpan.Zero || Math.Abs(from - to) < 1e-9) return;
        fill = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = duration,
            Easing = Standard,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(FractionProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(FractionProperty, to) } },
            },
        }.RunAsync(this, fill.Token);
    }

    Easing Standard => this.TryFindResource("Rune.Easing.Standard", ActualThemeVariant, out var found) && found is Easing standard ? standard : new CubicEaseInOut();

    /// <summary>不确定进度：挂在可视树上时一直循环（条形 1.4 秒、环形 1 秒；系统关闭动画时 3 秒）。</summary>
    void Spin()
    {
        spin?.Cancel();
        spin = null;
        if (Determinate || VisualRoot is null) return;
        spin = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = TimeSpan.FromSeconds(!Motion.Enabled ? 3 : Circle ? 1 : 1.4),
            IterationCount = IterationCount.Infinite,
            Easing = Circle ? new LinearEasing() : Standard,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PhaseProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PhaseProperty, 1d) } },
            },
        }.RunAsync(this, spin.Token);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Spin();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        spin?.Cancel();
        spin = null;
    }

    double Gap => GetValue(GapFactorProperty) * Em;

    /// <summary>轨道的粗细：space.xs × 1.5。</summary>
    double Thickness => 1.5 * Gap;

    /// <summary>圆角（full 是粗细的一半）。</summary>
    double CornerRadius => Math.Min(Radius == Radius.Full ? double.MaxValue : GetValue(RadiusFactorProperty) * Em, Thickness / 2);

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        if (Circle)
        {
            ring.Measure(availableSize);
            ringLabel.Measure(availableSize);
            var labelSize = ringLabel.IsVisible ? ringLabel.DesiredSize : default;
            return new LayoutSize(Math.Max(ring.DesiredSize.Width, labelSize.Width), ring.DesiredSize.Height + (ringLabel.IsVisible ? Gap + labelSize.Height : 0));
        }
        header.Measure(availableSize);
        track.Measure(availableSize);
        var headerHeight = header.IsVisible ? header.DesiredSize.Height + Gap : 0;
        // 条形占满一行：宽度由布局给（Flex 里写 Grow），不限宽时取标签行的宽度
        var width = double.IsInfinity(availableSize.Width) ? header.IsVisible ? header.DesiredSize.Width : 0 : availableSize.Width;
        return new LayoutSize(width, headerHeight + Thickness);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        if (Circle)
        {
            var side = ring.DesiredSize.Width;
            ring.Arrange(new Rect((finalSize.Width - side) / 2, 0, side, side));
            if (ringLabel.IsVisible) ringLabel.Arrange(new Rect((finalSize.Width - ringLabel.DesiredSize.Width) / 2, side + Gap, ringLabel.DesiredSize.Width, ringLabel.DesiredSize.Height));
            return finalSize;
        }
        var y = 0d;
        if (header.IsVisible)
        {
            header.Arrange(new Rect(0, 0, finalSize.Width, header.DesiredSize.Height));
            y = header.DesiredSize.Height + Gap;
        }
        track.Arrange(new Rect(0, y, finalSize.Width, Thickness));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ProgressAutomationPeer(this);

    /// <summary>
    /// 条形的轨道（TS 的 track）：画底色，里面是进度（部件 <see cref="ProgressParts.Indicator"/>）与目标线；按圆角裁剪。
    /// 不确定进度时进度是四成宽的一段，从起始边外滑到结束边外（从右到左时 Avalonia 整体镜像）。
    /// </summary>
    sealed class ProgressTrack : Control
    {
        readonly Progress owner;
        readonly Border indicator = new() { Name = ProgressParts.Indicator };
        readonly Border target = new();

        public ProgressTrack(Progress owner)
        {
            this.owner = owner;
            ClipToBounds = true;
            LogicalChildren.AddRange([indicator, target]);
            VisualChildren.AddRange([indicator, target]);
            UpdateColors();
        }

        /// <summary>进度与目标线的颜色写在模板优先级上：部件样式盖得过。</summary>
        public void UpdateColors()
        {
            indicator.SetValue(Border.BackgroundProperty, new ImmutableSolidColorBrush(owner.GetValue(IndicatorColorProperty)), BindingPriority.Template);
            target.SetValue(Border.BackgroundProperty, new ImmutableSolidColorBrush(owner.GetValue(TextColorProperty)), BindingPriority.Template);
        }

        protected override LayoutSize MeasureOverride(LayoutSize availableSize) => default;

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var (width, height) = (finalSize.Width, finalSize.Height);
            var radius = owner.CornerRadius;
            Clip = new RectangleGeometry(new Rect(finalSize), radius, radius);
            indicator.CornerRadius = new CornerRadius(radius);
            var rect = owner.Determinate
                ? new Rect(0, 0, owner.GetValue(FractionProperty) * width, height)
                : new Rect((-0.4 + 1.4 * owner.GetValue(PhaseProperty)) * width, 0, 0.4 * width, height);
            indicator.Arrange(rect);
            target.IsVisible = owner.TargetPercent is not null;
            var line = owner.GetValue(LineWidthProperty);
            target.Arrange(new Rect((owner.TargetPercent ?? 0) / 100 * width - line / 2, 0, line, height));
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var radius = owner.CornerRadius;
            context.DrawRectangle(new ImmutableSolidColorBrush(owner.GetValue(TrackColorProperty)), null, new Rect(Bounds.Size), radius, radius);
        }
    }

    /// <summary>
    /// 环形（TS 的 circle）：直径 5em，按 100 × 100 的画布画轨道（线宽 10、半径 45）、从正上方顺时针的进度弧与目标刻度；
    /// 中间放数值。不确定进度时四分之一圈的弧一直转。圆角 none 时弧的端头是平的。
    /// </summary>
    sealed class ProgressRing : Control
    {
        readonly Progress owner;
        readonly Text value;

        public ProgressRing(Progress owner, Text value)
        {
            (this.owner, this.value) = (owner, value);
            Name = "PART_Circle";
            LogicalChildren.Add(value);
            VisualChildren.Add(value);
        }

        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            value.Measure(availableSize);
            return new LayoutSize(5 * owner.Em, 5 * owner.Em);
        }

        /// <summary>数值居中（行高 1：按字号而不是行盒居中）。</summary>
        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var size = value.DesiredSize;
            value.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var scale = Bounds.Width / 100;
            var center = new Point(50 * scale, 50 * scale);
            var (radius, stroke) = (45 * scale, 10 * scale);
            context.DrawEllipse(null, new Pen(new ImmutableSolidColorBrush(owner.GetValue(TrackColorProperty)), stroke), center, radius, radius);
            var cap = owner.Radius == Radius.None ? PenLineCap.Flat : PenLineCap.Round;
            var indicator = new Pen(new ImmutableSolidColorBrush(owner.GetValue(IndicatorColorProperty)), stroke, lineCap: cap);
            var (start, sweep) = owner.Determinate ? (0d, owner.GetValue(FractionProperty)) : (owner.GetValue(PhaseProperty), 0.25);
            if (sweep >= 0.9999) context.DrawEllipse(null, indicator, center, radius, radius);
            else if (sweep > 0) context.DrawGeometry(null, indicator, Arc(center, radius, start, sweep));
            if (owner.TargetPercent is not { } target) return;
            // 目标刻度：从轨道外缘外 2 到内缘内 2 的一道线
            var angle = target / 100 * 2 * Math.PI - Math.PI / 2;
            Point At(double distance) => new(center.X + distance * scale * Math.Cos(angle), center.Y + distance * scale * Math.Sin(angle));
            context.DrawLine(new Pen(new ImmutableSolidColorBrush(owner.GetValue(TextColorProperty)), 2.5 * scale, lineCap: PenLineCap.Round), At(45 + 5 + 2), At(45 - 5 - 2));
        }

        /// <summary>从 start（圈数，0 在正上方）顺时针画 sweep 圈的弧。</summary>
        static StreamGeometry Arc(Point center, double radius, double start, double sweep)
        {
            Point At(double turns) => new(center.X + radius * Math.Sin(turns * 2 * Math.PI), center.Y - radius * Math.Cos(turns * 2 * Math.PI));
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            context.BeginFigure(At(start), false);
            context.ArcTo(At(start + sweep), new LayoutSize(radius, radius), 0, sweep > 0.5, SweepDirection.Clockwise);
            context.EndFigure(false);
            return geometry;
        }
    }

    /// <summary>读屏：进度条（RangeValue，取值文字在 Value 里），名称默认取标签。</summary>
    sealed class ProgressAutomationPeer(Progress owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Label;

        public bool IsReadOnly => true;

        public double Minimum => 0;

        public double Maximum => (double)owner.Max;

        double IRangeValueProvider.Value => (double)owner.Clamped;

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("进度只读");

        string? IValueProvider.Value => owner.SpokenValue;

        public void SetValue(string? value) => throw new InvalidOperationException("进度只读");
    }
}

/// <summary>
/// Progress 的部件名（代码组合的子元素）：<c>rune|Progress Border#PART_Indicator</c>。外层（TS 的 propsRoot）就是 Progress 自己。
/// 部件的默认外观在 Progress.axaml 的样式里（数值等宽、标签行的数值次要色、环形的数值半粗）。
/// </summary>
public static class ProgressParts
{
    /// <summary>条形的进度（着色部分，TS 的 propsIndicator）：Border。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>标签行（TS 的 propsHeader）：Flex。</summary>
    public const string Header = "PART_Header";

    /// <summary>标签（TS 的 propsLabel）：Text。</summary>
    public const string Label = "PART_Label";

    /// <summary>数值文字（TS 的 propsValueLabel）：Text。</summary>
    public const string ValueLabel = "PART_ValueLabel";
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
