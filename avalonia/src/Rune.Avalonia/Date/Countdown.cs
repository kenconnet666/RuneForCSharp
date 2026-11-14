using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia;

/// <summary>倒计时的外观。</summary>
public enum CountdownVariant
{
    /// <summary>一串"时:分:秒"（默认）。</summary>
    Text,

    /// <summary>一格一格的数字块（限时抢购），天数单独一格；字号随上下文。</summary>
    Blocks,
}

/// <summary>
/// 倒计时（vue-tsx-pro 的 Countdown）：到某个时刻还剩多久，按秒（或分）更新；到点时触发 <see cref="Ended"/>，停在 0。
/// <code>
/// &lt;rune:Countdown To="{Binding StartsAt}" Ended="OnSaleStarted" /&gt;     &lt;!-- 2天 03:15:42 --&gt;
/// &lt;rune:Countdown To="{Binding Deadline}" Precision="Minute" /&gt;         &lt;!-- 2天 03:15 --&gt;
/// &lt;rune:Countdown To="{Binding StartsAt}" Variant="Blocks" /&gt;            &lt;!-- [2天] [03]:[15]:[42] --&gt;
/// countdown.Formatter = left =&gt; $"还剩 {left.Days} 天 {left.Hours} 小时";
/// </code>
/// 目标时刻同 <see cref="DateTimeText"/> 的值。默认显示 时:分:秒，超过一天时前面加天数。几个计时器对齐到整秒一起跳；不在界面上时不更新。
/// 读屏是一段文字，条目类型是 timer（TS 的 role="timer"，默认不播报），名称是显示的文字，写了 AutomationProperties.Name 时用它；
/// 到点的提示放在 Ended 里（Toast 等）。部件名在 <see cref="CountdownParts"/>。
/// </summary>
public class Countdown : Control
{
    /// <summary>目标时刻：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。</summary>
    public static readonly StyledProperty<object?> ToProperty = AvaloniaProperty.Register<Countdown, object?>(nameof(To));

    /// <summary>精度，默认到秒。</summary>
    public static readonly StyledProperty<CountdownPrecision> PrecisionProperty = AvaloniaProperty.Register<Countdown, CountdownPrecision>(nameof(Precision));

    /// <summary>外观，默认一串文字。</summary>
    public static readonly StyledProperty<CountdownVariant> VariantProperty = AvaloniaProperty.Register<Countdown, CountdownVariant>(nameof(Variant));

    readonly NowClock clock;
    readonly Text text = new() { Name = CountdownParts.Text, Numeric = true, Wrap = TextWrap.NoWrap };
    readonly Flex blocks = new() { Name = CountdownParts.Blocks, Align = FlexAlign.Center, Gap = Space.Xs };
    Func<CountdownRemaining, string>? formatter;
    bool? ended;

    /// <summary>新建。</summary>
    public Countdown()
    {
        clock = new NowClock(Update);
        LogicalChildren.Add(text);
        VisualChildren.Add(text);
        AutomationProperties.SetItemType(this, "timer");
        Update();
    }

    /// <summary>目标时刻。</summary>
    public object? To { get => GetValue(ToProperty); set => SetValue(ToProperty, value); }

    /// <summary>精度。</summary>
    public CountdownPrecision Precision { get => GetValue(PrecisionProperty); set => SetValue(PrecisionProperty, value); }

    /// <summary>外观。</summary>
    public CountdownVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>自定义显示（TS 的渲染函数）：拿到拆好的天、时、分、秒，返回要显示的文字；写了时忽略 Variant。</summary>
    public Func<CountdownRemaining, string>? Formatter
    {
        get => formatter;
        set
        {
            formatter = value;
            Update();
        }
    }

    /// <summary>还剩多久；目标认不出时为 null。</summary>
    public CountdownRemaining? Remaining { get; private set; }

    /// <summary>显示的文字（也是读屏的名称）。</summary>
    public string DisplayText { get; private set; } = "";

    /// <summary>时钟（测试里换成假的）。</summary>
    public TimeProvider TimeProvider
    {
        get => clock.Time;
        set
        {
            clock.Time = value;
            Update();
        }
    }

    /// <summary>到点时触发一次（一开始就已经过了的不触发）。</summary>
    public event EventHandler? Ended;

    /// <summary>数字块那一排（测试用）。</summary>
    internal Flex BlocksPart => blocks;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToProperty || change.Property == PrecisionProperty || change.Property == VariantProperty
            || change.Property == Provider.LangProperty) Update();
        else if (change.Property == TextElement.FontSizeProperty) SizeBlocks();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Update();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        clock.Stop();
    }

    /// <summary>按现在重算剩余、重写显示，排下一跳；从没到点变成到点时触发 Ended。</summary>
    void Update()
    {
        var lang = Provider.GetLang(this);
        var remaining = TimeText.Remaining(To, clock.Now, lang, Precision);
        Remaining = remaining?.Remaining;
        var left = remaining?.Remaining ?? default;
        DisplayText = remaining is null ? "" : formatter?.Invoke(left) ?? TimeText.CountdownText(left, Precision, lang.Code);
        var showBlocks = remaining is not null && formatter is null && Variant == CountdownVariant.Blocks;
        if (showBlocks) BuildBlocks(TimeText.Segments(left, Precision, lang.Code));
        else text.Text = DisplayText;
        Show(showBlocks ? blocks : text);
        clock.Schedule(TopLevel.GetTopLevel(this) is not null ? remaining?.Every : null);

        var was = ended;
        ended = remaining?.Remaining.Ended;
        if (ended == true && was == false) Ended?.Invoke(this, EventArgs.Empty);
    }

    void Show(Control part)
    {
        if (VisualChildren.Contains(part)) return;
        LogicalChildren.Clear();
        VisualChildren.Clear();
        LogicalChildren.Add(part);
        VisualChildren.Add(part);
    }

    /// <summary>天数一格、时分秒各一格，中间是冒号；格子的排法没变时只换文字，免得每秒重建。</summary>
    void BuildBlocks((string? Days, string[] Clock) segments)
    {
        string[] values = segments.Days is null ? segments.Clock : [segments.Days, .. segments.Clock];
        var cells = blocks.Children.OfType<Border>().ToList();
        var sameShape = cells.Count == values.Length && (segments.Days is null) == (cells.Count == 0 || !Equals(cells[0].Tag, Days));
        if (sameShape)
        {
            for (var i = 0; i < values.Length; i++) ((Text)cells[i].Child!).Text = values[i];
            return;
        }
        blocks.Children.Clear();
        if (segments.Days is { } days) blocks.Children.Add(Segment(days, Days));
        for (var i = 0; i < segments.Clock.Length; i++)
        {
            if (i > 0) blocks.Children.Add(new Text { Name = CountdownParts.Separator, Text = ":", Weight = TextWeight.Semibold });
            blocks.Children.Add(Segment(segments.Clock[i], null));
        }
        SizeBlocks();
    }

    /// <summary>天数那一格的标记（格数相同时分辨有没有天数）。</summary>
    const string Days = "days";

    static Border Segment(string value, string? tag)
    {
        var cell = new Border
        {
            Name = CountdownParts.Segment,
            Tag = tag,
            Child = new Text { Text = value, Weight = TextWeight.Semibold, Numeric = true, Align = TextAlign.Center, Wrap = TextWrap.NoWrap },
        };
        var resources = new TemplateResources(cell);
        resources.Bind(Border.BackgroundProperty, ThemeKeys.Brush.Accent);
        resources.Bind(TextElement.ForegroundProperty, ThemeKeys.Brush.AccentContrast);
        return cell;
    }

    /// <summary>格子的最小宽度、内边距与圆角按字号算（TS 的 2em、xxs / xs、sm）。</summary>
    void SizeBlocks()
    {
        var em = GetValue(TextElement.FontSizeProperty);
        foreach (var cell in blocks.Children.OfType<Border>())
        {
            cell.MinWidth = 2 * em;
            cell.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em, this.Resource<double>(ThemeKeys.Space.Xxs) * em);
            cell.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        }
    }

    /// <summary>量完写上基线（放进 InlineUIContainer、基线对齐的 Flex 时与旁边的文字对齐）：文字的第一行基线，数字块按格子里的文字。</summary>
    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        var part = (Control)VisualChildren[0];
        part.Measure(availableSize);
        var baseline = part == text ? text.BaselineOffset
            : blocks.Children.OfType<Border>().FirstOrDefault() is { Child: Text cell } first ? first.Padding.Top + (part.DesiredSize.Height - first.DesiredSize.Height) / 2 + cell.BaselineOffset
            : 0;
        SetCurrentValue(TextBlock.BaselineOffsetProperty, baseline);
        return part.DesiredSize;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        ((Control)VisualChildren[0]).Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CountdownPeer(this);

    /// <summary>读屏：一段文字，名称是显示的文字（写了 Name 时用它）；里面的格子不另占节点。</summary>
    sealed class CountdownPeer(Countdown owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.DisplayText;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Countdown 的部件名：样式里用 <c>rune|Countdown Border#PART_Segment</c> 这样选中。</summary>
public static class CountdownParts
{
    /// <summary>一串文字的外观（与自定义显示）里的文字（TS 的 root）。</summary>
    public const string Text = "PART_Text";

    /// <summary>数字块那一排。</summary>
    public const string Blocks = "PART_Blocks";

    /// <summary>一格（TS 的 segment）。</summary>
    public const string Segment = "PART_Segment";

    /// <summary>格之间的冒号（TS 的 separator）。</summary>
    public const string Separator = "PART_Separator";
}
