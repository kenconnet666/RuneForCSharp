using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 会滚动的数字（vue-tsx-pro 的 AnimatedNumber）：值变化时从旧值过渡到新值，按当前语言格式化，等宽数字不抖动。统计卡片的数值、金额、百分比、计数器。
/// <code>
/// new AnimatedNumber { Value = 0.123m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
/// </code>
/// 值是 decimal：过渡的中间帧按浮点数算并按终值的小数位取整，停下时按原值格式化，不丢精度。过渡的时长默认 durations.lg、缓动 standard，
/// 系统关闭动画时直接跳到新值；<see cref="From"/> 给了时加载时就从它滚到 Value。<see cref="Trend"/> 按正负着色（正数成功色、负数危险色）。
/// 读屏只读终值（连同前后缀的文字）：滚动中的数字不另念；需要播报变化时在外面设实时区域。部件样式键：<see cref="AnimatedNumberParts"/>。
/// </summary>
public class AnimatedNumber : Control
{
    /// <summary>数值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(decimal), typeof(AnimatedNumber), new FrameworkPropertyMetadata(0m, OnValueChanged));

    /// <summary>格式；不写时是普通数字（0–3 位小数）。</summary>
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(NumericFormat), typeof(AnimatedNumber), new FrameworkPropertyMetadata(null, (d, _) => ((AnimatedNumber)d).UpdateText()));

    /// <summary>过渡的时长；不写时取主题的 durations.lg。</summary>
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(TimeSpan?), typeof(AnimatedNumber));

    /// <summary>加载时从这个值滚到 Value；不写时直接显示 Value。</summary>
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(nameof(From), typeof(decimal?), typeof(AnimatedNumber));

    /// <summary>按正负着色：正数成功色、负数危险色（涨跌）；0 时是 Tone。</summary>
    public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(
        nameof(Trend), typeof(bool), typeof(AnimatedNumber), new FrameworkPropertyMetadata(false, (d, _) => ((AnimatedNumber)d).UpdateTone()));

    /// <summary>文字色调；写了 Trend 时由正负决定。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(TextTone), typeof(AnimatedNumber), new FrameworkPropertyMetadata(default(TextTone), (d, _) => ((AnimatedNumber)d).UpdateTone()));

    /// <summary>数字前的内容（说明文字、图标）。</summary>
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(
        nameof(Prefix), typeof(object), typeof(AnimatedNumber), new FrameworkPropertyMetadata(null, (d, _) => ((AnimatedNumber)d).UpdateParts()));

    /// <summary>数字后的内容（如单位"人"）。</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix), typeof(object), typeof(AnimatedNumber), new FrameworkPropertyMetadata(null, (d, _) => ((AnimatedNumber)d).UpdateParts()));

    /// <summary>过渡中显示的值（浮点数，只用于中间帧）。</summary>
    static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        "Current", typeof(double), typeof(AnimatedNumber), new FrameworkPropertyMetadata(0d, (d, _) => ((AnimatedNumber)d).UpdateText()));

    readonly Flex root = new() { Align = FlexAlign.Baseline };
    readonly ContentPresenter prefix = new() { Name = "PART_Prefix" };
    readonly Text number = new() { Name = "PART_Value", Numeric = true, Wrap = TextWrap.NoWrap };
    readonly ContentPresenter suffix = new() { Name = "PART_Suffix" };

    static AnimatedNumber()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AnimatedNumber), new FrameworkPropertyMetadata(typeof(AnimatedNumber)));
        FocusableProperty.OverrideMetadata(typeof(AnimatedNumber), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(AnimatedNumber), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：数值 0。</summary>
    public AnimatedNumber()
    {
        prefix.SetResourceReference(StyleProperty, AnimatedNumberParts.Prefix);
        number.SetResourceReference(StyleProperty, AnimatedNumberParts.Value);
        suffix.SetResourceReference(StyleProperty, AnimatedNumberParts.Suffix);
        root.Children.Add(prefix);
        root.Children.Add(number);
        root.Children.Add(suffix);
        AddVisualChild(root);
        AddLogicalChild(root);
        Loaded += (_, _) =>
        {
            if (From is { } start) Play((double)start);
            UpdateTone();
        };
        UpdateParts();
        UpdateText();
    }

    /// <summary>数值。</summary>
    public decimal Value { get => (decimal)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>格式。</summary>
    public NumericFormat? Format { get => (NumericFormat?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>过渡的时长。</summary>
    public TimeSpan? Duration { get => (TimeSpan?)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>加载时从这个值滚到 Value。</summary>
    public decimal? From { get => (decimal?)GetValue(FromProperty); set => SetValue(FromProperty, value); }

    /// <summary>按正负着色。</summary>
    public bool Trend { get => (bool)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }

    /// <summary>文字色调。</summary>
    public TextTone Tone { get => (TextTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>数字前的内容。</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>数字后的内容。</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>正负：up、down、flat（TS 的 data-trend）。</summary>
    public string Direction => Value > 0 ? "up" : Value < 0 ? "down" : "flat";

    /// <summary>终值的文字（读屏念的数字）。</summary>
    public string FinalText => (Format ?? NumericFormat.Default).Format(Value, Provider.GetLang(this).Code, Provider.GetLang(this).Currency);

    /// <summary>当前显示的数字（测试用）。</summary>
    internal string ShownText => number.Text ?? "";

    /// <summary>显示的色调（测试用）：Trend 时按正负。</summary>
    internal TextTone ShownTone => Trend ? Direction switch { "up" => TextTone.Success, "down" => TextTone.Danger, _ => Tone } : Tone;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (AnimatedNumber)d;
        if (owner.IsLoaded) owner.Play((double)(decimal)e.OldValue);
        else owner.SetValue(CurrentProperty, (double)owner.Value);
        owner.UpdateText();
        owner.UpdateTone();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Provider.LangProperty) UpdateText();
    }

    /// <summary>从 <paramref name="from"/> 过渡到 Value（durations.lg、standard）；系统关闭动画、时长为 0 时直接到位。FillBehavior.Stop 结束后留下终值。</summary>
    void Play(double from)
    {
        var to = (double)Value;
        SetValue(CurrentProperty, to);
        var duration = Duration is { } own ? (Motion.Enabled ? own : TimeSpan.Zero) : Motion.Duration(this, "Rune.Duration.Lg", TimeSpan.FromMilliseconds(300));
        if (duration <= TimeSpan.Zero || from == to)
        {
            BeginAnimation(CurrentProperty, null);
            return;
        }
        BeginAnimation(CurrentProperty, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>停在终值时按原值格式化（decimal 不经过浮点数）；过渡中按终值的小数位取整，整数不会滚出一串小数。</summary>
    void UpdateText()
    {
        var current = (double)GetValue(CurrentProperty);
        var format = Format ?? NumericFormat.Default;
        var lang = Provider.GetLang(this);
        number.Text = current == (double)Value
            ? format.Format(Value, lang.Code, lang.Currency)
            : format.Format(Math.Round((decimal)current, Math.Min(20, (int)Value.Scale)), lang.Code, lang.Currency);
    }

    void UpdateTone()
    {
        if (ShownTone.BrushKey is { } key) SetResourceReference(ForegroundProperty, key);
        else ClearValue(ForegroundProperty);
    }

    void UpdateParts()
    {
        prefix.Content = Prefix;
        prefix.Visibility = Prefix is null ? Visibility.Collapsed : Visibility.Visible;
        suffix.Content = Suffix;
        suffix.Visibility = Suffix is null ? Visibility.Collapsed : Visibility.Visible;
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
    protected override AutomationPeer OnCreateAutomationPeer() => new AnimatedNumberPeer(this);

    /// <summary>读屏：一段文字，念前缀、终值与后缀（滚动中的数字不另念）。</summary>
    sealed class AnimatedNumberPeer(AnimatedNumber owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(AnimatedNumber);

        protected override string GetNameCore() => string.Concat(Plain(owner.Prefix), owner.FinalText, Plain(owner.Suffix));

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        static string Plain(object? content) => content switch
        {
            string text => text,
            TextBlock block => block.Text ?? "",
            _ => "",
        };
    }
}

/// <summary>AnimatedNumber 的部件样式键。</summary>
public static class AnimatedNumberParts
{
    /// <summary>前缀（TS 的 propsPrefix）。</summary>
    public static readonly PartKey Prefix = new(typeof(AnimatedNumber), nameof(Prefix), typeof(ContentPresenter));

    /// <summary>数字（TS 的 propsValue）。</summary>
    public static readonly PartKey Value = new(typeof(AnimatedNumber), nameof(Value), typeof(Text));

    /// <summary>后缀（TS 的 propsSuffix）。</summary>
    public static readonly PartKey Suffix = new(typeof(AnimatedNumber), nameof(Suffix), typeof(ContentPresenter));
}
