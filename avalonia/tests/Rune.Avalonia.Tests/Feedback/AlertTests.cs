using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>Alert：图标按色调默认选、写 null 去掉；Live 是读屏的实时区域；说明与额外内容；订阅了 CloseRequested 才有关闭按钮，离场结束才发。</summary>
public class AlertTests
{
    static T Part<T>(Alert alert, string name) where T : Control => alert.GetLogicalDescendants().OfType<T>().First(control => control.Name == name);

    [Fact]
    [Upstream("Alert", "图标按色调默认选，可以去掉；live 决定角色；标题、正文、操作各有部件")]
    public Task Icon_follows_the_tone_live_maps_to_a_live_region_and_parts_are_named() => HeadlessUi.Run(() =>
    {
        var undo = new Button { Content = "撤销" };
        var saved = new Alert { Tone = StatusTone.Success, Title = "已保存", Action = undo, Content = "修改已生效" };
        var error = new Alert { Tone = StatusTone.Danger, Live = AlertLive.Assertive, Icon = null, Content = "出错了" };
        var polite = new Alert { Live = AlertLive.Polite, Content = "提示" };
        HeadlessUi.Show(new StackPanel { Children = { saved, error, polite } });

        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(saved));
        Assert.Equal(Lucide.CircleCheck, saved.ShownIcon);
        Assert.Equal("已保存", Part<Text>(saved, AlertParts.Title).Text);
        Assert.Equal(undo, Part<ContentPresenter>(saved, AlertParts.Action).Content);
        Assert.Equal("已保存", saved.Peer().GetName()); // 读屏的名称是标题
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(error));
        Assert.Null(error.ShownIcon); // 写 null 不显示图标
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(polite));
        Assert.Equal(Lucide.Info, polite.ShownIcon);
    });

    [Fact]
    [Upstream("Alert", "说明写在 description 或 children 上；两个都给时 children 是说明下面的额外内容")]
    public Task Description_or_content_is_the_text_and_both_put_content_below() => HeadlessUi.Run(() =>
    {
        var link = new Button { Content = "查看失败原因" };
        var both = new Alert { Description = "成功 120 条", Content = link };
        var only = new Alert { Content = "只有内容" };
        HeadlessUi.Show(new StackPanel { Children = { both, only } });

        Assert.Equal("成功 120 条", Part<ContentPresenter>(both, AlertParts.Description).Content);
        Assert.Equal(link, Part<ContentPresenter>(both, AlertParts.Content).Content);
        Assert.Equal("只有内容", Part<ContentPresenter>(only, AlertParts.Description).Content);
        Assert.False(Part<ContentPresenter>(only, AlertParts.Content).IsVisible);
    });

    [Fact]
    [Upstream("Alert", "给了 onClose 才有关闭按钮；离场结束后才调用")]
    public Task Closing_needs_a_subscriber_and_fires_after_leaving() => HeadlessUi.Run(() =>
    {
        var alert = new Alert { Content = "x" };
        var window = HeadlessUi.Show(new StackPanel { Children = { alert } });
        Assert.Null(alert.CloseButton);

        var closed = 0;
        alert.CloseRequested += (_, _) => closed++;
        HeadlessUi.Flush(window);
        Assert.Equal("关闭", alert.CloseButton!.Peer().GetName());
        ((IInvokeProvider)alert.CloseButton!.Peer()).Invoke();
        HeadlessUi.Until(() => closed == 1);
    });
}
