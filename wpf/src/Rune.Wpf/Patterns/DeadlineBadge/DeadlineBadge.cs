using System.Globalization;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf;

/// <summary>
/// 时限标记（vue-tsx-pro 的 DeadlineBadge）：按工作日算还剩几天、今天到期、超时几天，颜色随状态（超时 danger、今天到期与快到期 warning、
/// 还早 neutral、完成 success 或超时完成 danger）。用在收件箱、待办、各种有时限的列表里。
/// <code>
/// &lt;rune:DeadlineBadge Due="2026-10-23" /&gt;                              截止日直接给
/// &lt;rune:DeadlineBadge Start="{Binding VerifiedOn}" Workdays="10" /&gt;    开始日加几个工作日
/// &lt;rune:DeadlineBadge Due="{Binding DueOn}" Done="{Binding CompletedOn}" /&gt;   完成了按完成那天算
/// </code>
/// 工作日按 <see cref="Provider.WorkCalendarProperty"/>（法定节假日与调休），没给时只按周末。今天默认按本机日期，业务以服务器的日期为准时写 <see cref="Today"/>。
/// 悬停提示与读屏名称带上截止日。外观同 <see cref="Badge"/>，色调由状态决定。截止日、开始日都没给时不显示。
/// </summary>
public class DeadlineBadge : Badge
{
    /// <summary>截止日；与 <see cref="Start"/> 加 <see cref="Workdays"/> 二选一。</summary>
    public static readonly DependencyProperty DueProperty = Register<DateOnly?>(nameof(Due), null);

    /// <summary>开始日：截止日是它加 <see cref="Workdays"/> 个工作日。</summary>
    public static readonly DependencyProperty StartProperty = Register<DateOnly?>(nameof(Start), null);

    /// <summary>开始日之后几个工作日截止。</summary>
    public static readonly DependencyProperty WorkdaysProperty = Register<int?>(nameof(Workdays), null);

    /// <summary>完成的日期：完成了按完成那天算，显示按时或超时完成。</summary>
    public static readonly DependencyProperty DoneProperty = Register<DateOnly?>(nameof(Done), null);

    /// <summary>今天；不写时按本机的日期。</summary>
    public static readonly DependencyProperty TodayProperty = Register<DateOnly?>(nameof(Today), null);

    /// <summary>剩下几个工作日以内算快到期（warning），默认 2。</summary>
    public static readonly DependencyProperty SoonProperty = Register(nameof(Soon), 2);

    static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(DeadlineBadge), new PropertyMetadata(fallback, (d, _) => ((DeadlineBadge)d).Update()));

    static DeadlineBadge()
    {
        // 工作日历、语言包沿元素树继承：外面换了也重算
        Provider.WorkCalendarProperty.OverrideMetadata(typeof(DeadlineBadge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((DeadlineBadge)d).Update()));
        Provider.LangProperty.OverrideMetadata(typeof(DeadlineBadge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((DeadlineBadge)d).Update()));
    }

    /// <summary>新建：没有截止日时不显示。</summary>
    public DeadlineBadge() => Update();

    /// <summary>截止日。</summary>
    public DateOnly? Due { get => (DateOnly?)GetValue(DueProperty); set => SetValue(DueProperty, value); }

    /// <summary>开始日。</summary>
    public DateOnly? Start { get => (DateOnly?)GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>开始日之后几个工作日截止。</summary>
    public int? Workdays { get => (int?)GetValue(WorkdaysProperty); set => SetValue(WorkdaysProperty, value); }

    /// <summary>完成的日期。</summary>
    public DateOnly? Done { get => (DateOnly?)GetValue(DoneProperty); set => SetValue(DoneProperty, value); }

    /// <summary>今天。</summary>
    public DateOnly? Today { get => (DateOnly?)GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <summary>剩下几个工作日以内算快到期。</summary>
    public int Soon { get => (int)GetValue(SoonProperty); set => SetValue(SoonProperty, value); }

    /// <summary>算出来的时限（TS 的 data-state 是它的状态）；截止日、开始日都没给时为 null。</summary>
    public Deadline? Deadline { get; private set; }

    /// <summary>按时限换色调、文字、提示与读屏名称（当前值：使用方写的本地值盖得过）。</summary>
    void Update()
    {
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);
        Deadline = Due is not null || (Start is not null && Workdays is not null)
            ? Provider.GetWorkCalendar(this).DeadlineOf(today, Due, Start, Workdays, Done, Soon)
            : null;
        SetCurrentValue(VisibilityProperty, Deadline is null ? Visibility.Collapsed : Visibility.Visible);
        if (Deadline is not { } deadline) return;
        var messages = PatternsMessages.For(Provider.GetLang(this));
        var late = deadline.State == DeadlineState.Done && deadline.Remaining < 0;
        var tone = late ? StatusTone.Danger : deadline.State switch
        {
            DeadlineState.Overdue => StatusTone.Danger,
            DeadlineState.Due or DeadlineState.Soon => StatusTone.Warning,
            DeadlineState.Done => StatusTone.Success,
            _ => StatusTone.Neutral,
        };
        var text = deadline.State switch
        {
            DeadlineState.Overdue => messages.DeadlineOverdue(-deadline.Remaining),
            DeadlineState.Due => messages.DeadlineDue,
            DeadlineState.Done => late ? messages.DeadlineDoneLate(-deadline.Remaining) : messages.DeadlineDone,
            _ => messages.DeadlineRemaining(deadline.Remaining),
        };
        var on = messages.DeadlineOn(deadline.Due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        SetCurrentValue(ToneProperty, tone);
        SetCurrentValue(ContentProperty, text);
        SetCurrentValue(ToolTipProperty, on);
        // 提示读屏不一定念：截止日接在名称后面（TS 另放一段 VisuallyHidden）
        SetCurrentValue(AutomationProperties.NameProperty, $"{text} {on}");
    }
}
