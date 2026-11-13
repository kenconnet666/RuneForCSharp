using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Tests;

/// <summary>Banner：标题与正文同一行、操作在后面；Live 是读屏的实时区域；关闭先离场再发，没被移除就重新出现。</summary>
public class BannerTests
{
    [Fact]
    [Upstream("Banner", "标题加粗、与正文同一行；操作在后面；页面加载时就在的不带角色，live 时带")]
    public Task Title_and_text_share_a_line_and_live_maps_to_a_live_region() => HeadlessUi.Run(() =>
    {
        var notice = new Button { Content = "查看详情" };
        var first = new Banner { Title = "系统维护", Action = notice, Content = "今晚 22:00–23:00 暂停服务" };
        var second = new Banner { Live = AlertLive.Polite, Tone = StatusTone.Success, Content = "已恢复服务" };
        HeadlessUi.Show(new StackPanel { Children = { first, second } });

        var runs = first.TextLine.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["系统维护", "今晚 22:00–23:00 暂停服务"], runs.Select(run => run.Text));
        Assert.Equal(BannerParts.Title, runs[0].Name); // 标题是同一行里的一段
        Assert.Equal("系统维护", first.Peer().GetName());
        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(first));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(second));
        Assert.Null(first.CloseButton);
        Assert.Equal(Lucide.CircleCheck, second.ShownIcon);
    });

    [Fact]
    [Upstream("Banner", "关闭：先离场（等过渡结束）再调用 onClose；使用方没移除时重新出现")]
    public Task Closing_leaves_first_and_comes_back_when_not_removed() => HeadlessUi.Run(() =>
    {
        var banner = new Banner { Tone = StatusTone.Warning, Content = "试用还剩 3 天" };
        var closed = 0;
        banner.CloseRequested += (_, _) => closed++;
        var window = HeadlessUi.Show(new StackPanel { Children = { banner } });

        ((IInvokeProvider)banner.CloseButton!.Peer()).Invoke();
        Assert.True(banner.IsDismissing);
        HeadlessUi.Until(() => closed == 1);
        Assert.False(banner.IsDismissing);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(400));
        HeadlessUi.Flush(window);
        Assert.True(banner.IsVisible); // 使用方没移除：重新出现
    });
}
