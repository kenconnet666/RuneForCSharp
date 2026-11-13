using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Banner：标题与正文同一行、操作在后面；Live 是读屏的实时区域；关闭先离场再发，没被移除就重新出现。</summary>
public class BannerTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("Banner", "标题加粗、与正文同一行；操作在后面；页面加载时就在的不带角色，live 时带")]
    public Task Title_and_text_share_a_line_and_live_maps_to_a_live_region() => WpfUi.Run(() =>
    {
        var notice = new Button { Content = "查看详情" };
        var first = new Banner { Title = "系统维护", Action = notice, Content = "今晚 22:00–23:00 暂停服务" };
        var second = new Banner { Live = AlertLive.Polite, Tone = StatusTone.Success, Content = "已恢复服务" };
        WpfUi.Show(Host(first, second));

        Assert.Equal(["系统维护", "今晚 22:00–23:00 暂停服务"], first.TextLine.Inlines.OfType<Run>().Select(run => run.Text));
        Assert.Equal("系统维护", first.Peer().GetName());
        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(first));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(second));
        Assert.Null(first.CloseButton);
        Assert.Equal(Lucide.CircleCheck, second.ShownIcon);
    });

    [Fact]
    [Upstream("Banner", "关闭：先离场（等过渡结束）再调用 onClose；使用方没移除时重新出现")]
    public Task Closing_leaves_first_and_comes_back_when_not_removed() => WpfUi.Run(() =>
    {
        var banner = new Banner { Tone = StatusTone.Warning, Content = "试用还剩 3 天" };
        var closed = 0;
        banner.CloseRequested += (_, _) => closed++;
        WpfUi.Show(Host(banner));

        ((IInvokeProvider)banner.CloseButton!.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Until(() => banner.IsDismissing || closed == 1);
        WpfUi.Until(() => closed == 1);
        Assert.False(banner.IsDismissing);
        WpfUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.True(banner.IsVisible); // 使用方没移除：重新出现
    });
}
