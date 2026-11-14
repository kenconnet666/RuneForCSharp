using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 相对时间（vue-tsx-pro 的 RelativeTime）："刚刚""3 分钟前""昨天""2 年后"，随时间自动更新；悬停显示完整的日期时间。
/// <code>
/// new RelativeTimeControl { Value = comment.CreatedAt };
/// new RelativeTimeControl { Value = deadline, Threshold = TimeSpan.FromDays(7) };   // 一周以外直接写日期
/// </code>
/// 值同 <see cref="DateTimeTextControl"/>。单位按相差多久自动取（45 秒以内用秒，45 分钟以内用分……），措辞是"昨天"而不是"1 天前"。
/// 更新的频率随单位变：秒级每秒，分钟级每 10 秒，小时级每分钟，更远的每小时；没有窗口句柄（不在界面上）时不更新。
/// 完整的日期时间是悬停提示，也是读屏的描述。是文字控件：字号、色调、字重照 TextControl 写。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("相对时间：3 分钟前、昨天，随时间自动更新。")]
public class RelativeTimeControl : TextControlBase<Text>
{
    readonly NowClock clock;
    readonly TooltipProvider tooltips = new();
    object? value;
    RelativeTimeStyle format;
    TimeSpan? threshold;
    DateTimeOffset? now;

    /// <summary>新建。</summary>
    public RelativeTimeControl() => clock = new NowClock(Recompute);

    /// <summary>值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。</summary>
    [Category("Data")]
    [Description("值：DateTimeOffset、DateTime、DateOnly 或 ISO 字符串。")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Value { get => value; set { this.value = value; Recompute(); } }

    /// <summary>详略：Long（"3 分钟前"，默认）、Short、Narrow。</summary>
    [Category("Appearance")]
    [Description("详略：Long（默认）、Short、Narrow。")]
    [DefaultValue(RelativeTimeStyle.Long)]
    public RelativeTimeStyle Format { get => format; set { format = value; Recompute(); } }

    /// <summary>相差超过多久就改写日期；不写时一直是相对时间。</summary>
    [Category("Behavior")]
    [Description("相差超过多久就改写日期；空着时一直是相对时间。")]
    [DefaultValue(null)]
    public TimeSpan? Threshold { get => threshold; set { threshold = value; Recompute(); } }

    /// <summary>以哪个时刻为"现在"：写了就不再随时间更新（截图、测试）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTimeOffset? Now { get => now; set { now = value; Recompute(); } }

    /// <summary>机器可读的值（TS 的 time 元素的 datetime）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? MachineValue { get; private set; }

    /// <summary>时钟（测试里换成假的）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeProvider TimeProvider
    {
        get => clock.Time;
        set
        {
            clock.Time = value;
            Recompute();
        }
    }

    /// <summary>悬停提示（完整的日期时间；测试用）。</summary>
    internal string? ShownTooltip => tooltips.GetTooltip(this);

    /// <inheritdoc />
    protected override void OnScopeChanged()
    {
        Recompute();
        base.OnScopeChanged();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Recompute();
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        clock.Stop();
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            clock.Stop();
            tooltips.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>按现在重写文字与提示，排下一次更新。</summary>
    void Recompute()
    {
        var described = TimeText.DescribeRelative(value, now ?? clock.Now, ProviderControl.LangOf(this), format, threshold);
        Text = described?.Text ?? "";
        MachineValue = described?.Machine;
        tooltips.SetTooltip(this, described?.Title);
        AccessibleDescription = described?.Title;
        clock.Schedule(now is null && IsHandleCreated ? described?.Every : null);
    }
}
