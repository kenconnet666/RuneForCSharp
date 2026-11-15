using System.Globalization;
using System.ComponentModel;

namespace Rune.WinForms;

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
/// WinForms 里是 TimeFieldControl。参考 React Aria TimeField、原生 input type="time"。
/// 值是 HH:mm（Granularity 为 Second 时 HH:mm:ss），时总是 24 小时制，同原生 input type="time"；要 TimeOnly 时用 <see cref="Time"/>。
/// 早于 Min、晚于 Max、不可选的时间显示出错。键盘同 DateField；12 小时制时在上午 / 下午那段打 a、p 切换。部件见 SegmentedField。
/// </summary>
[ToolboxItem(true)]
[Description("时间输入框：分段输入，值是 ISO 字符串。")]
public class TimeFieldControl : SegmentedFieldControl
{
    /// <summary>新建：时分。</summary>
    public TimeFieldControl() => Fields = FieldsOf(Granularity);

    /// <summary>精确到哪一段。</summary>
    [Category("Behavior")]
    [DefaultValue(typeof(TimeFieldGranularity), "Minute")]
    public TimeFieldGranularity Granularity { get; set { field = value; Fields = FieldsOf(value); } } = TimeFieldGranularity.Minute;

    /// <summary>最早的时间。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Min { get; set { field = value; Revalidate(); } }

    /// <summary>最晚的时间。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Max { get; set { field = value; Revalidate(); } }

    /// <summary>不可选的时间。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TimeOnly, bool>? IsTimeUnavailable { get; set { field = value; Revalidate(); } }

    /// <summary>值是 TimeOnly；没填全时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly? Time
    {
        get => Parse(Value);
        set => Value = value is { } time ? DateSegments.ToIso(new DateFieldValues { Hour = time.Hour, Minute = time.Minute, Second = time.Second }, Fields) : null;
    }

    static IReadOnlyList<DateSegmentType> FieldsOf(TimeFieldGranularity granularity) => DateSegments.FieldsOf(granularity switch
    {
        TimeFieldGranularity.Hour => DateFieldGranularity.Hour,
        TimeFieldGranularity.Second => DateFieldGranularity.Second,
        _ => DateFieldGranularity.Minute,
    }, time: true);

    static TimeOnly? Parse(string? iso) =>
        iso is not null && TimeOnly.TryParseExact(iso, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    /// <inheritdoc />
    protected override bool IsOutOfRange(string iso) =>
        Parse(iso) is { } time && (Parse(Min) is { } low && time < low || Parse(Max) is { } high && time > high || IsTimeUnavailable?.Invoke(time) == true);
}
