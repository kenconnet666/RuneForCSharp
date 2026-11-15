using Avalonia;

namespace Rune.Avalonia;

/// <summary>时间输入框精确到哪一段（vue-tsx-pro 的 TimeFieldGranularities）。</summary>
public enum TimeFieldGranularity
{
    /// <summary>只有时：14:00。</summary>
    Hour,

    /// <summary>时分（默认）：14:30。</summary>
    Minute,

    /// <summary>时分秒：14:30:15。</summary>
    Second,
}

/// <summary>
/// 时间输入框（vue-tsx-pro 的 TimeField）：分段输入时、分（可选秒），12 / 24 小时制跟随语言（zh-CN 24 小时，en-US 12 小时加 AM / PM），也可以用 HourCycle 指定。
/// 参考 React Aria TimeField、原生 input type="time"。
/// <code>
/// &lt;rune:Field Label="提醒时间"&gt;&lt;rune:TimeField Value="{Binding Time}" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:TimeField Granularity="Second" HourCycle="h12" Value="{Binding At}" /&gt;
/// </code>
/// 值是 HH:mm（Granularity 为 Second 时 HH:mm:ss），时总是 24 小时制，同原生 input type="time"；要 TimeOnly 时用 <see cref="Time"/>。
/// 早于 Min、晚于 Max、不可选的时间显示出错。键盘同 DateField；12 小时制时在上午 / 下午那段打 a、p 切换。部件见 SegmentedField。
/// </summary>
public class TimeField : SegmentedField
{
    /// <summary>精确到哪一段，默认 Minute。</summary>
    public static readonly StyledProperty<TimeFieldGranularity> GranularityProperty = AvaloniaProperty.Register<TimeField, TimeFieldGranularity>(nameof(Granularity), TimeFieldGranularity.Minute);

    /// <summary>最早的时间（09:00）：早于它显示出错。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<TimeField, string?>(nameof(Min));

    /// <summary>最晚的时间（18:00）：晚于它显示出错。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<TimeField, string?>(nameof(Max));

    /// <summary>不可选的时间（午休、已约满的时段）。</summary>
    public static readonly StyledProperty<Func<TimeOnly, bool>?> IsTimeUnavailableProperty = AvaloniaProperty.Register<TimeField, Func<TimeOnly, bool>?>(nameof(IsTimeUnavailable));

    /// <summary>新建：时分。</summary>
    public TimeField() => Fields = FieldsOf(Granularity);

    /// <summary>精确到哪一段。</summary>
    public TimeFieldGranularity Granularity { get => GetValue(GranularityProperty); set => SetValue(GranularityProperty, value); }

    /// <summary>最早的时间。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚的时间。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>不可选的时间。</summary>
    public Func<TimeOnly, bool>? IsTimeUnavailable { get => GetValue(IsTimeUnavailableProperty); set => SetValue(IsTimeUnavailableProperty, value); }

    /// <summary>值是 TimeOnly；没填全时 null。</summary>
    public TimeOnly? Time
    {
        get => TimeSlots.Parse(Value);
        set => Value = value is { } time ? DateSegments.ToIso(new DateFieldValues { Hour = time.Hour, Minute = time.Minute, Second = time.Second }, Fields) : null;
    }

    static IReadOnlyList<DateSegmentType> FieldsOf(TimeFieldGranularity granularity) => DateSegments.FieldsOf(granularity switch
    {
        TimeFieldGranularity.Hour => DateFieldGranularity.Hour,
        TimeFieldGranularity.Second => DateFieldGranularity.Second,
        _ => DateFieldGranularity.Minute,
    }, time: true);

    /// <inheritdoc />
    protected override bool IsOutOfRange(string iso) =>
        TimeSlots.Parse(iso) is { } time && (TimeSlots.Parse(Min) is { } low && time < low || TimeSlots.Parse(Max) is { } high && time > high || IsTimeUnavailable?.Invoke(time) == true);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GranularityProperty) Fields = FieldsOf(Granularity);
        else if (change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsTimeUnavailableProperty) Revalidate();
    }
}
