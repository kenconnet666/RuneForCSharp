using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 日期时间的文字（vue-tsx-pro 的 DateTime；平台自带 System.DateTime，这里叫 DateTimeText）：按语言包的语言与时区写。
/// <code>
/// &lt;rune:DateTimeText Value="{Binding PlacedAt}" /&gt;                    &lt;!-- 2026年10月20日 14:30 --&gt;
/// &lt;rune:DateTimeText Value="2026-10-20" DateStyle="Full" /&gt;            &lt;!-- 2026年10月20日星期二 --&gt;
/// &lt;rune:DateTimeText Value="{Binding PlacedAt}" Format="M" /&gt;          &lt;!-- 10月20日：.NET 的格式字符串 --&gt;
/// </code>
/// 值是 DateTimeOffset、DateTime、DateOnly 或 ISO 字符串：时刻（DateTimeOffset、UTC 与本地的 DateTime、带 Z 或偏移的字符串）按语言包的时区换算，
/// 未指定时区的 DateTime、不带偏移的字符串是墙上时间，原样写。不写格式时只有日期的值显示日期，带时间的再加上时分。
/// 要"3 分钟前"用 <see cref="RelativeTime"/>。是 <see cref="Text"/> 的子类：字号、色调、字重照 Text 写。
/// </summary>
public class DateTimeText : Text
{
    /// <summary>值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串；认不出时不显示。</summary>
    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<DateTimeText, object?>(nameof(Value));

    /// <summary>日期的详略；不写时 Medium。</summary>
    public static readonly StyledProperty<DateStyle?> DateStyleProperty = AvaloniaProperty.Register<DateTimeText, DateStyle?>(nameof(DateStyle));

    /// <summary>时间的详略；不写时带时间的值用 Short，只有日期的值不显示。</summary>
    public static readonly StyledProperty<TimeStyle?> TimeStyleProperty = AvaloniaProperty.Register<DateTimeText, TimeStyle?>(nameof(TimeStyle));

    /// <summary>.NET 的日期时间格式字符串（TS 的 options），按语言包的文化写；写了时忽略 DateStyle、TimeStyle。</summary>
    public static readonly StyledProperty<string?> FormatProperty = AvaloniaProperty.Register<DateTimeText, string?>(nameof(Format));

    /// <summary>新建。</summary>
    public DateTimeText() => Update();

    /// <summary>值。</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>日期的详略。</summary>
    public DateStyle? DateStyle { get => GetValue(DateStyleProperty); set => SetValue(DateStyleProperty, value); }

    /// <summary>时间的详略。</summary>
    public TimeStyle? TimeStyle { get => GetValue(TimeStyleProperty); set => SetValue(TimeStyleProperty, value); }

    /// <summary>格式字符串。</summary>
    public string? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>机器可读的值（TS 的 time 元素的 datetime）：时刻写 UTC 的 ISO 8601，其余原样；认不出时为 null。</summary>
    public string? MachineValue { get; private set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Text);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == DateStyleProperty || change.Property == TimeStyleProperty
            || change.Property == FormatProperty || change.Property == Provider.LangProperty) Update();
    }

    void Update()
    {
        var described = TimeText.Describe(Value, Provider.GetLang(this), DateStyle, TimeStyle, Format);
        Text = described?.Text;
        MachineValue = described?.Machine;
    }
}
