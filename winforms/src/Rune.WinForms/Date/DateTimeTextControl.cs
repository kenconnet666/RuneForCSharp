using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 日期时间的文字（vue-tsx-pro 的 DateTime；平台自带 System.DateTime，这里叫 DateTimeText）：按语言包的语言与时区写。
/// <code>
/// new DateTimeTextControl { Value = order.PlacedAt };                                   // 2026年10月20日 14:30
/// new DateTimeTextControl { Value = new DateOnly(2026, 10, 20), DateStyle = DateStyle.Full };   // 2026年10月20日星期二
/// new DateTimeTextControl { Value = order.PlacedAt, Format = "M" };                     // 10月20日：.NET 的格式字符串
/// </code>
/// 值是 DateTimeOffset、DateTime、DateOnly 或 ISO 字符串：时刻（DateTimeOffset、UTC 与本地的 DateTime、带 Z 或偏移的字符串）按语言包的时区换算，
/// 未指定时区的 DateTime、不带偏移的字符串是墙上时间，原样写。不写格式时只有日期的值显示日期，带时间的再加上时分。
/// 要"3 分钟前"用 <see cref="RelativeTimeControl"/>。是文字控件：字号、色调、字重照 TextControl 写，文字由值算出。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("日期时间的文字：按语言包的语言与时区写。")]
public class DateTimeTextControl : TextControlBase<Text>
{
    object? value;
    DateStyle? dateStyle;
    TimeStyle? timeStyle;
    string? format;

    /// <summary>值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串；认不出时不显示。</summary>
    [Category("Data")]
    [Description("值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Value { get => value; set { this.value = value; Recompute(); } }

    /// <summary>日期的详略；不写时 Medium。</summary>
    [Category("Appearance")]
    [Description("日期的详略：Full、Long、Medium、Short；空着时 Medium。")]
    [DefaultValue(null)]
    public DateStyle? DateStyle { get => dateStyle; set { dateStyle = value; Recompute(); } }

    /// <summary>时间的详略；不写时带时间的值用 Short，只有日期的值不显示。</summary>
    [Category("Appearance")]
    [Description("时间的详略：Full、Long、Medium、Short；空着时带时间的值用 Short。")]
    [DefaultValue(null)]
    public TimeStyle? TimeStyle { get => timeStyle; set { timeStyle = value; Recompute(); } }

    /// <summary>.NET 的日期时间格式字符串（TS 的 options），按语言包的文化写；写了时忽略 DateStyle、TimeStyle。</summary>
    [Category("Appearance")]
    [Description(".NET 的日期时间格式字符串，按语言包的文化写；写了时忽略 DateStyle、TimeStyle。")]
    [DefaultValue(null)]
    public string? Format { get => format; set { format = value; Recompute(); } }

    /// <summary>机器可读的值（TS 的 time 元素的 datetime）：时刻写 UTC 的 ISO 8601，其余原样；认不出时为 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? MachineValue { get; private set; }

    /// <inheritdoc />
    protected override void OnScopeChanged()
    {
        Recompute();
        base.OnScopeChanged();
    }

    void Recompute()
    {
        var described = TimeText.Describe(value, ProviderControl.LangOf(this), dateStyle, timeStyle, format);
        Text = described?.Text ?? "";
        MachineValue = described?.Machine;
    }
}
