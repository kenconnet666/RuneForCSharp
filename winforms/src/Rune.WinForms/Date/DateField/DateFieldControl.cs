using System.Globalization;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 日期输入框（vue-tsx-pro 的 DateField）：分段输入年、月、日（不带弹层），段的顺序按语言（zh-CN 年/月/日，en-US 月/日/年）。
/// WinForms 里是 DateFieldControl。参考 React Aria DateField、原生 input type="date"。要弹出日历用 DatePicker。
/// 值是 ISO 字符串，形式随 <see cref="Granularity"/>：Year 2026、Month 2026-10、Day 2026-10-20、Quarter 2026-Q4、Week 2026-W43，
/// Hour / Minute / Second 是日期加时间 2026-10-20T14:30[:15]（同原生 datetime-local）。要 DateOnly、DateTime 时用 <see cref="Date"/>、<see cref="DateTime"/>。
/// 值在 Min–Max 之外、是不可选的日期或时刻时显示出错，值照样写回，由表单校验决定能不能提交。键盘见 DateSegmentGroup；部件见 SegmentedField。
/// </summary>
[ToolboxItem(true)]
[Description("日期输入框：分段输入，值是 ISO 字符串。")]
public class DateFieldControl : SegmentedFieldControl
{
    /// <summary>新建：年月日。</summary>
    public DateFieldControl() => Fields = DateSegments.FieldsOf(Granularity);

    /// <summary>精确到哪一段。</summary>
    [Category("Behavior")]
    [DefaultValue(typeof(DateFieldGranularity), "Day")]
    public DateFieldGranularity Granularity { get; set { field = value; Fields = DateSegments.FieldsOf(value); } } = DateFieldGranularity.Day;

    /// <summary>最早的日期。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Min { get; set { field = value; Revalidate(); } }

    /// <summary>最晚的日期。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Max { get; set { field = value; Revalidate(); } }

    /// <summary>不可选的日期。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<DateOnly, bool>? IsDateUnavailable { get; set { field = value; Revalidate(); } }

    /// <summary>带时间时不可选的时刻。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TimeOnly, DateOnly, bool>? IsTimeUnavailable { get; set { field = value; Revalidate(); } }

    /// <summary>值是日期（Year … Day、Quarter、Week 取这一段的第一天；带时间时取日期部分）；没填全时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateOnly? Date
    {
        get => Value is { } iso && DateFieldRange.Period(iso) is { } period ? period.Start : null;
        set => Value = value is { } date ? DateSegments.ToIso(DateSegments.FromDateTime(date.ToDateTime(TimeOnly.MinValue)), Fields) : null;
    }

    /// <summary>值是时刻（带时间时；日期粒度时是那一天的零点）；没填全时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? DateTime
    {
        get => Value is { } iso && DateFieldRange.Instant(iso) is { } instant ? instant : null;
        set => Value = value is { } time ? DateSegments.ToIso(DateSegments.FromDateTime(time), Fields) : null;
    }

    /// <inheritdoc />
    protected override bool IsOutOfRange(string iso) => DateFieldRange.IsOutOfRange(iso, Min, Max, IsDateUnavailable, IsTimeUnavailable);
}
