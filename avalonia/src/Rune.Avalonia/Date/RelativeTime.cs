using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 相对时间（vue-tsx-pro 的 RelativeTime）："刚刚""3 分钟前""昨天""2 年后"，随时间自动更新；悬停显示完整的日期时间。
/// <code>
/// &lt;rune:RelativeTime Value="{Binding CreatedAt}" /&gt;
/// &lt;rune:RelativeTime Value="{Binding Deadline}" Threshold="7.00:00:00" /&gt;   &lt;!-- 一周以外直接写日期 --&gt;
/// </code>
/// 值同 <see cref="DateTimeText"/>。单位按相差多久自动取（45 秒以内用秒，45 分钟以内用分……），措辞是"昨天"而不是"1 天前"。
/// 更新的频率随单位变：秒级每秒，分钟级每 10 秒，小时级每分钟，更远的每小时；不在界面上时不更新。
/// 是 <see cref="Text"/> 的子类：字号、色调、字重照 Text 写。
/// </summary>
public class RelativeTime : Text
{
    /// <summary>值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。</summary>
    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<RelativeTime, object?>(nameof(Value));

    /// <summary>详略：Long（"3 分钟前"，默认）、Short、Narrow。</summary>
    public static readonly StyledProperty<RelativeTimeStyle> FormatProperty = AvaloniaProperty.Register<RelativeTime, RelativeTimeStyle>(nameof(Format));

    /// <summary>相差超过多久就改写日期；不写时一直是相对时间。</summary>
    public static readonly StyledProperty<TimeSpan?> ThresholdProperty = AvaloniaProperty.Register<RelativeTime, TimeSpan?>(nameof(Threshold));

    /// <summary>以哪个时刻为"现在"：写了就不再随时间更新（截图、测试）。</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty = AvaloniaProperty.Register<RelativeTime, DateTimeOffset?>(nameof(Now));

    readonly NowClock clock;

    /// <summary>新建。</summary>
    public RelativeTime() => clock = new NowClock(Update);

    /// <summary>值。</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>详略。</summary>
    public RelativeTimeStyle Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>改写日期的阈值。</summary>
    public TimeSpan? Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    /// <summary>固定的"现在"。</summary>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <summary>机器可读的值（TS 的 time 元素的 datetime）。</summary>
    public string? MachineValue { get; private set; }

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

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Text);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == FormatProperty || change.Property == ThresholdProperty
            || change.Property == NowProperty || change.Property == Provider.LangProperty) Update();
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

    /// <summary>按现在重写文字与提示，排下一次更新。</summary>
    void Update()
    {
        var described = TimeText.DescribeRelative(Value, Now ?? clock.Now, Provider.GetLang(this), Format, Threshold);
        Text = described?.Text;
        MachineValue = described?.Machine;
        ToolTip.SetTip(this, described?.Title);
        AutomationProperties.SetHelpText(this, described?.Title);
        clock.Schedule(Now is null && TopLevel.GetTopLevel(this) is not null ? described?.Every : null);
    }
}
