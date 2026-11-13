using System.Windows;
using System.Windows.Automation;


namespace Rune.Wpf.Tests;

/// <summary>DeadlineBadge：按工作日算剩余、到期、超时，颜色随状态；悬停与读屏有截止日；工作日历取自 Provider；没有截止日时不显示。</summary>
public class DeadlineBadgeTests
{
    static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // 2026-10-21 是周三
    static readonly DateOnly Today = D("2026-10-21");

    static (string? Text, DeadlineState? State, StatusTone Tone, object? Tip, string? Reader) Show(DeadlineBadge badge, FrameworkElement? host = null)
    {
        var root = new System.Windows.Controls.StackPanel();
        root.Resources.MergedDictionaries.Add(new RuneTheme());
        root.Children.Add(host ?? badge);
        WpfUi.Show(root);
        return (badge.Content as string, badge.Deadline?.State, badge.Tone, badge.ToolTip, AutomationProperties.GetName(badge));
    }

    [Fact]
    [Upstream("DeadlineBadge", "还早、快到期、今天到期、超时：文字与状态；悬停与读屏有截止日")]
    public Task Pending_soon_due_and_overdue() => WpfUi.Run(() =>
    {
        var pending = Show(new DeadlineBadge { Due = D("2026-10-30"), Today = Today });
        Assert.Equal(("剩 7 个工作日", DeadlineState.Pending, StatusTone.Neutral, "截止 2026-10-30"), (pending.Text, pending.State, pending.Tone, pending.Tip));
        var soon = Show(new DeadlineBadge { Due = D("2026-10-23"), Today = Today });
        Assert.Equal(("剩 2 个工作日", DeadlineState.Soon, StatusTone.Warning), (soon.Text, soon.State, soon.Tone));
        var due = Show(new DeadlineBadge { Due = Today, Today = Today });
        Assert.Equal(("今天到期", DeadlineState.Due, StatusTone.Warning), (due.Text, due.State, due.Tone));
        var late = Show(new DeadlineBadge { Due = D("2026-10-16"), Today = Today });
        Assert.Equal(("超时 3 个工作日", DeadlineState.Overdue, StatusTone.Danger), (late.Text, late.State, late.Tone));
        Assert.Contains("截止 2026-10-16", late.Reader); // 读屏名称接上截止日
    });

    [Fact]
    [Upstream("DeadlineBadge", "开始日加几个工作日；完成了显示按时或超时完成")]
    public Task Start_plus_workdays_and_done() => WpfUi.Run(() =>
    {
        // 周五开始，加 2 个工作日是下周二
        Assert.Equal("截止 2026-10-20", Show(new DeadlineBadge { Start = D("2026-10-16"), Workdays = 2, Today = Today }).Tip);
        var onTime = Show(new DeadlineBadge { Due = D("2026-10-20"), Done = D("2026-10-19"), Today = Today });
        Assert.Equal(("按时完成", DeadlineState.Done, StatusTone.Success), (onTime.Text, onTime.State, onTime.Tone));
        var late = Show(new DeadlineBadge { Due = D("2026-10-16"), Done = D("2026-10-20") });
        Assert.Equal(("超时 2 个工作日完成", StatusTone.Danger), (late.Text, late.Tone));
    });

    [Fact]
    [Upstream("DeadlineBadge", "节假日不算工作日：按 Provider 的工作日历")]
    public Task Holidays_come_from_the_provider() => WpfUi.Run(() =>
    {
        var badge = new DeadlineBadge { Due = D("2026-10-26"), Today = Today };
        var provider = new Provider { WorkCalendar = new WorkCalendar(holidays: [(D("2026-10-22"), D("2026-10-23"))]), Child = badge };
        Assert.Equal("剩 1 个工作日", Show(badge, provider).Text);
    });

    [Fact]
    [Upstream("DeadlineBadge", "没有截止日时什么都不画")]
    public Task Nothing_without_a_deadline() => WpfUi.Run(() =>
    {
        var badge = new DeadlineBadge();
        Show(badge);
        Assert.Equal(Visibility.Collapsed, badge.Visibility);
        Assert.Null(badge.Deadline);
    });
}
