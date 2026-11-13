using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;

namespace Rune.Avalonia;

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
    public static readonly StyledProperty<DateOnly?> DueProperty = AvaloniaProperty.Register<DeadlineBadge, DateOnly?>(nameof(Due));

    /// <summary>开始日：截止日是它加 <see cref="Workdays"/> 个工作日。</summary>
    public static readonly StyledProperty<DateOnly?> StartProperty = AvaloniaProperty.Register<DeadlineBadge, DateOnly?>(nameof(Start));

    /// <summary>开始日之后几个工作日截止。</summary>
    public static readonly StyledProperty<int?> WorkdaysProperty = AvaloniaProperty.Register<DeadlineBadge, int?>(nameof(Workdays));

    /// <summary>完成的日期：完成了按完成那天算，显示按时或超时完成。</summary>
    public static readonly StyledProperty<DateOnly?> DoneProperty = AvaloniaProperty.Register<DeadlineBadge, DateOnly?>(nameof(Done));

    /// <summary>今天；不写时按本机的日期。</summary>
    public static readonly StyledProperty<DateOnly?> TodayProperty = AvaloniaProperty.Register<DeadlineBadge, DateOnly?>(nameof(Today));

    /// <summary>剩下几个工作日以内算快到期（warning），默认 2。</summary>
    public static readonly StyledProperty<int> SoonProperty = AvaloniaProperty.Register<DeadlineBadge, int>(nameof(Soon), 2);

    readonly List<IDisposable?> shown = [];

    /// <summary>截止日。</summary>
    public DateOnly? Due { get => GetValue(DueProperty); set => SetValue(DueProperty, value); }

    /// <summary>开始日。</summary>
    public DateOnly? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>开始日之后几个工作日截止。</summary>
    public int? Workdays { get => GetValue(WorkdaysProperty); set => SetValue(WorkdaysProperty, value); }

    /// <summary>完成的日期。</summary>
    public DateOnly? Done { get => GetValue(DoneProperty); set => SetValue(DoneProperty, value); }

    /// <summary>今天。</summary>
    public DateOnly? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <summary>剩下几个工作日以内算快到期。</summary>
    public int Soon { get => GetValue(SoonProperty); set => SetValue(SoonProperty, value); }

    /// <summary>算出来的时限（TS 的 data-state 是它的状态）；截止日、开始日都没给时为 null。</summary>
    public Deadline? Deadline { get; private set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Badge);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DueProperty || change.Property == StartProperty || change.Property == WorkdaysProperty || change.Property == DoneProperty
            || change.Property == TodayProperty || change.Property == SoonProperty || change.Property == Provider.WorkCalendarProperty || change.Property == Provider.LangProperty)
            Update();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Update();
    }

    /// <summary>按时限换色调、文字、提示与读屏名称：都按模板优先级设，使用方写的盖得过。</summary>
    void Update()
    {
        foreach (var value in shown) value?.Dispose();
        shown.Clear();
        var calendar = Provider.GetWorkCalendar(this);
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);
        Deadline = Due is not null || (Start is not null && Workdays is not null)
            ? calendar.DeadlineOf(today, Due, Start, Workdays, Done, Soon)
            : null;
        if (Deadline is not { } deadline)
        {
            shown.Add(SetValue(IsVisibleProperty, false, BindingPriority.Template));
            return;
        }
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
        shown.Add(SetValue(ToneProperty, tone, BindingPriority.Template));
        shown.Add(SetValue(ContentProperty, text, BindingPriority.Template));
        shown.Add(SetValue(ToolTip.TipProperty, on, BindingPriority.Template));
        // 提示读屏不一定念：截止日接在名称后面（TS 另放一段 VisuallyHidden）
        shown.Add(SetValue(AutomationProperties.NameProperty, $"{text} {on}", BindingPriority.Template));
    }
}
