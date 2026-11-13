using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 会滚动的数字（vue-tsx-pro 的 AnimatedNumber）：值变化时从旧值过渡到新值，按当前语言格式化，等宽数字不抖动。统计卡片的数值、金额、百分比、计数器。
/// <code>
/// &lt;rune:AnimatedNumber Value="{Binding Revenue}" Format="{x:Static local:Formats.Currency}" /&gt;
/// new AnimatedNumber { Value = 0.123m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
/// </code>
/// 值是 decimal：过渡的中间帧按浮点数算并按终值的小数位取整，停下时按原值格式化，不丢精度。过渡的时长默认 durations.lg、缓动 standard，
/// 系统关闭动画时直接跳到新值；<see cref="From"/> 给了时挂上就从它滚到 Value。<see cref="Trend"/> 按正负着色（正数成功色、负数危险色）。
/// 读屏只读终值（连同前后缀的文字）：滚动中的数字不另念；需要播报变化时在外面设实时区域。
/// 部件：<c>rune|AnimatedNumber ContentPresenter#PART_Prefix</c>、<c>rune|Text#PART_Value</c>、<c>ContentPresenter#PART_Suffix</c>。
/// </summary>
public class AnimatedNumber : Control
{
    /// <summary>数值。</summary>
    public static readonly StyledProperty<decimal> ValueProperty = AvaloniaProperty.Register<AnimatedNumber, decimal>(nameof(Value));

    /// <summary>格式；不写时是普通数字（0–3 位小数）。</summary>
    public static readonly StyledProperty<NumericFormat?> FormatProperty = AvaloniaProperty.Register<AnimatedNumber, NumericFormat?>(nameof(Format));

    /// <summary>过渡的时长；不写时取主题的 durations.lg。</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty = AvaloniaProperty.Register<AnimatedNumber, TimeSpan?>(nameof(Duration));

    /// <summary>挂上时从这个值滚到 Value；不写时直接显示 Value。</summary>
    public static readonly StyledProperty<decimal?> FromProperty = AvaloniaProperty.Register<AnimatedNumber, decimal?>(nameof(From));

    /// <summary>按正负着色：正数成功色、负数危险色（涨跌）；0 时是 Tone。</summary>
    public static readonly StyledProperty<bool> TrendProperty = AvaloniaProperty.Register<AnimatedNumber, bool>(nameof(Trend));

    /// <summary>文字色调；写了 Trend 时由正负决定。</summary>
    public static readonly StyledProperty<TextTone> ToneProperty = AvaloniaProperty.Register<AnimatedNumber, TextTone>(nameof(Tone));

    /// <summary>数字前的内容（说明文字、图标）。</summary>
    public static readonly StyledProperty<object?> PrefixProperty = AvaloniaProperty.Register<AnimatedNumber, object?>(nameof(Prefix));

    /// <summary>数字后的内容（如单位"人"）。</summary>
    public static readonly StyledProperty<object?> SuffixProperty = AvaloniaProperty.Register<AnimatedNumber, object?>(nameof(Suffix));

    /// <summary>过渡中显示的值（浮点数，只用于中间帧）。</summary>
    static readonly StyledProperty<double> CurrentProperty = AvaloniaProperty.Register<AnimatedNumber, double>("Current");

    readonly Flex root = new() { Align = FlexAlign.Baseline };
    readonly ContentPresenter prefix = new() { Name = AnimatedNumberParts.Prefix };
    readonly Text number = new() { Name = AnimatedNumberParts.Value, Numeric = true, Wrap = TextWrap.NoWrap };
    readonly ContentPresenter suffix = new() { Name = AnimatedNumberParts.Suffix };
    readonly TemplateResources resources;
    CancellationTokenSource? tween;

    /// <summary>新建：数值 0。</summary>
    public AnimatedNumber()
    {
        resources = new TemplateResources(this);
        root.Children.AddRange([prefix, number, suffix]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        UpdateParts();
        UpdateText();
    }

    /// <summary>数值。</summary>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>格式。</summary>
    public NumericFormat? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>过渡的时长。</summary>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>挂上时从这个值滚到 Value。</summary>
    public decimal? From { get => GetValue(FromProperty); set => SetValue(FromProperty, value); }

    /// <summary>按正负着色。</summary>
    public bool Trend { get => GetValue(TrendProperty); set => SetValue(TrendProperty, value); }

    /// <summary>文字色调。</summary>
    public TextTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (VisualRoot is not null) Play((double)change.GetOldValue<decimal>());
            else SetValue(CurrentProperty, (double)Value);
            UpdateText();
            UpdateTone();
        }
        else if (change.Property == CurrentProperty || change.Property == FormatProperty || change.Property == Provider.LangProperty) UpdateText();
        else if (change.Property == TrendProperty || change.Property == ToneProperty) UpdateTone();
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty) UpdateParts();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (From is { } start) Play((double)start);
        else SetValue(CurrentProperty, (double)Value);
        UpdateTone();
    }

    /// <summary>从 <paramref name="from"/> 过渡到 Value（durations.lg、standard）；系统关闭动画、时长为 0 时直接到位。</summary>
    async void Play(double from)
    {
        tween?.Cancel();
        var to = (double)Value;
        var duration = Motion.Duration(Duration ?? this.Resource("Rune.Duration.Lg", TimeSpan.FromMilliseconds(300)));
        // 先写终值：动画在动画优先级上盖住它，结束或被打断后留下的就是终值
        SetValue(CurrentProperty, to);
        if (duration <= TimeSpan.Zero || from == to) return;
        var cancel = tween = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = duration,
            Easing = this.Resource<Easing>("Rune.Easing.Standard", new CubicEaseInOut()),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(CurrentProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(CurrentProperty, to) } },
            },
        }.RunAsync(this, cancel.Token);
        // 结束按真实时间算（同 Dismissal；headless 的动画时钟不走），到点停掉动画
        await Task.Delay(duration);
        cancel.Cancel();
    }

    /// <summary>停在终值时按原值格式化（decimal 不经过浮点数）；过渡中按终值的小数位取整，整数不会滚出一串小数。</summary>
    void UpdateText()
    {
        var current = GetValue(CurrentProperty);
        var format = Format ?? NumericFormat.Default;
        var lang = Provider.GetLang(this);
        number.Text = current == (double)Value
            ? format.Format(Value, lang.Code, lang.Currency)
            : format.Format(Math.Round((decimal)current, Math.Min(20, (int)Value.Scale)), lang.Code, lang.Currency);
    }

    void UpdateTone()
    {
        resources.Bind(TextElement.ForegroundProperty, ShownTone.BrushKey);
        PseudoClasses.Set(":up", Trend && Direction == "up");
        PseudoClasses.Set(":down", Trend && Direction == "down");
    }

    void UpdateParts()
    {
        prefix.Content = Prefix;
        prefix.IsVisible = Prefix is not null;
        suffix.Content = Suffix;
        suffix.IsVisible = Suffix is not null;
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
    protected override AutomationPeer OnCreateAutomationPeer() => new AnimatedNumberPeer(this);

    /// <summary>读屏：一段文字，念前缀、终值与后缀（滚动中的数字不另念）。</summary>
    sealed class AnimatedNumberPeer(AnimatedNumber owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore() => string.Concat(Plain(owner.Prefix), owner.FinalText, Plain(owner.Suffix));

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        static string Plain(object? content) => content switch
        {
            string text => text,
            TextBlock block => block.Text ?? "",
            _ => "",
        };
    }
}

/// <summary>AnimatedNumber 的部件名。</summary>
public static class AnimatedNumberParts
{
    /// <summary>前缀（TS 的 propsPrefix）。</summary>
    public const string Prefix = "PART_Prefix";

    /// <summary>数字（TS 的 propsValue）。</summary>
    public const string Value = "PART_Value";

    /// <summary>后缀（TS 的 propsSuffix）。</summary>
    public const string Suffix = "PART_Suffix";
}
