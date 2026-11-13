namespace Rune.WinForms.Tests;

/// <summary>DeadlineBadgeControl：按工作日算剩余、到期、超时，颜色随状态；悬停与读屏有截止日；工作日历取自 ProviderControl；没有截止日时不显示。</summary>
public class DeadlineBadgeTests
{
    static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // 2026-10-21 是周三
    static readonly DateOnly Today = D("2026-10-21");

    [Fact]
    [Upstream("DeadlineBadge", "还早、快到期、今天到期、超时：文字与状态；悬停与读屏有截止日")]
    public void Pending_soon_due_and_overdue()
    {
        var pending = new DeadlineBadgeControl { Due = D("2026-10-30"), Today = Today };
        Assert.Equal(("剩 7 个工作日", DeadlineState.Pending, StatusTone.Neutral, "截止 2026-10-30"), (pending.Text, pending.Deadline?.State, pending.Tone, pending.Tip));
        var soon = new DeadlineBadgeControl { Due = D("2026-10-23"), Today = Today };
        Assert.Equal(("剩 2 个工作日", DeadlineState.Soon, StatusTone.Warning), (soon.Text, soon.Deadline?.State, soon.Tone));
        var due = new DeadlineBadgeControl { Due = Today, Today = Today };
        Assert.Equal(("今天到期", DeadlineState.Due, StatusTone.Warning), (due.Text, due.Deadline?.State, due.Tone));
        var late = new DeadlineBadgeControl { Due = D("2026-10-16"), Today = Today };
        Assert.Equal(("超时 3 个工作日", DeadlineState.Overdue, StatusTone.Danger), (late.Text, late.Deadline?.State, late.Tone));
        Assert.Contains("截止 2026-10-16", late.AccessibleName); // 读屏名称接上截止日
    }

    [Fact]
    [Upstream("DeadlineBadge", "开始日加几个工作日；完成了显示按时或超时完成")]
    public void Start_plus_workdays_and_done()
    {
        // 周五开始，加 2 个工作日是下周二
        Assert.Equal("截止 2026-10-20", new DeadlineBadgeControl { Start = D("2026-10-16"), Workdays = 2, Today = Today }.Tip);
        var onTime = new DeadlineBadgeControl { Due = D("2026-10-20"), Done = D("2026-10-19"), Today = Today };
        Assert.Equal(("按时完成", DeadlineState.Done, StatusTone.Success), (onTime.Text, onTime.Deadline?.State, onTime.Tone));
        var late = new DeadlineBadgeControl { Due = D("2026-10-16"), Done = D("2026-10-20") };
        Assert.Equal(("超时 2 个工作日完成", StatusTone.Danger), (late.Text, late.Tone));
    }

    [Fact]
    [Upstream("DeadlineBadge", "节假日不算工作日：按 Provider 的工作日历")]
    public void Holidays_come_from_the_provider()
    {
        var badge = new DeadlineBadgeControl { Due = D("2026-10-26"), Today = Today };
        using var provider = new ProviderControl { WorkCalendar = new WorkCalendar(holidays: [(D("2026-10-22"), D("2026-10-23"))]) };
        provider.Controls.Add(badge);
        Assert.Equal("剩 1 个工作日", badge.Text);
    }

    [Fact]
    [Upstream("DeadlineBadge", "没有截止日时什么都不画")]
    public Task Nothing_without_a_deadline() => WinFormsUi.Run(() =>
    {
        var empty = new DeadlineBadgeControl();
        var dated = new DeadlineBadgeControl { Due = Today, Today = Today };
        using var form = WinFormsUi.Show(empty, dated);
        Assert.Equal((false, true), (empty.Visible, dated.Visible)); // 放在显示着的窗体里比：没有截止日的不显示
        Assert.Null(empty.Deadline);
    });
}
