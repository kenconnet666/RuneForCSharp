using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 时限标记（vue-tsx-pro 的 DeadlineBadge）：按工作日算还剩几天、今天到期、超时几天，颜色随状态（超时 danger、今天到期与快到期 warning、
/// 还早 neutral、完成 success 或超时完成 danger）。用在收件箱、待办、各种有时限的列表里。
/// <code>
/// new DeadlineBadgeControl { Due = new DateOnly(2026, 10, 23) };              // 截止日直接给
/// new DeadlineBadgeControl { Start = verifiedOn, Workdays = 10 };             // 开始日加几个工作日
/// new DeadlineBadgeControl { Due = item.DueOn, Done = item.CompletedOn };     // 完成了按完成那天算
/// </code>
/// 工作日按 ProviderControl 的 WorkCalendar（法定节假日与调休），没给时只按周末。今天默认按本机日期，业务以服务器的日期为准时写 <see cref="Today"/>。
/// 悬停提示（自带的 TooltipProvider）与读屏名称带上截止日。外观同 <see cref="BadgeControl"/>，色调由状态决定。截止日、开始日都没给时不显示。
/// </summary>
[ToolboxItem(true)]
[Description("时限标记：按工作日算剩余、到期、超时，颜色随状态。")]
public class DeadlineBadgeControl : BadgeControl
{
    readonly TooltipProvider tips = new();

    /// <summary>新建：没有截止日时不显示。</summary>
    public DeadlineBadgeControl() => Recompute();

    /// <summary>截止日；与 <see cref="Start"/> 加 <see cref="Workdays"/> 二选一。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public DateOnly? Due { get; set { field = value; Recompute(); } }

    /// <summary>开始日：截止日是它加 <see cref="Workdays"/> 个工作日。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public DateOnly? Start { get; set { field = value; Recompute(); } }

    /// <summary>开始日之后几个工作日截止。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public int? Workdays { get; set { field = value; Recompute(); } }

    /// <summary>完成的日期：完成了按完成那天算，显示按时或超时完成。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public DateOnly? Done { get; set { field = value; Recompute(); } }

    /// <summary>今天；不写时按本机的日期。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public DateOnly? Today { get; set { field = value; Recompute(); } }

    /// <summary>剩下几个工作日以内算快到期（warning），默认 2。</summary>
    [Category("Data")]
    [DefaultValue(2)]
    public int Soon { get; set { field = value; Recompute(); } } = 2;

    /// <summary>算出来的时限（TS 的 data-state 是它的状态）；截止日、开始日都没给时为 null。</summary>
    [Browsable(false)]
    public Deadline? Deadline { get; private set; }

    /// <summary>悬停提示的文字（TS 的 title：截止日）。</summary>
    [Browsable(false)]
    public string? Tip { get; private set; }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Recompute();
    }

    /// <summary>按时限换色调、文字、提示与读屏名称；工作日历、语言包取所在的作用域。</summary>
    void Recompute()
    {
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);
        Deadline = Due is not null || (Start is not null && Workdays is not null)
            ? ProviderControl.WorkCalendarOf(this).DeadlineOf(today, Due, Start, Workdays, Done, Soon)
            : null;
        Visible = Deadline is not null;
        if (Deadline is not { } deadline)
        {
            Tip = null;
            tips.SetTooltip(this, null);
            return;
        }
        var messages = PatternsMessages.For(ProviderControl.LangOf(this));
        var late = deadline.State == DeadlineState.Done && deadline.Remaining < 0;
        Tone = late ? StatusTone.Danger : deadline.State switch
        {
            DeadlineState.Overdue => StatusTone.Danger,
            DeadlineState.Due or DeadlineState.Soon => StatusTone.Warning,
            DeadlineState.Done => StatusTone.Success,
            _ => StatusTone.Neutral,
        };
        Text = deadline.State switch
        {
            DeadlineState.Overdue => messages.DeadlineOverdue(-deadline.Remaining),
            DeadlineState.Due => messages.DeadlineDue,
            DeadlineState.Done => late ? messages.DeadlineDoneLate(-deadline.Remaining) : messages.DeadlineDone,
            _ => messages.DeadlineRemaining(deadline.Remaining),
        };
        Tip = messages.DeadlineOn(deadline.Due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        tips.SetTooltip(this, Tip);
        // 提示读屏不一定念：截止日接在名称后面（TS 另放一段 VisuallyHidden）
        AccessibleName = $"{Text} {Tip}";
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
}
