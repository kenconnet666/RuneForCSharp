using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Result：按色调或状态选图标与颜色；状态码读屏跳过，标题、说明取语言包，显式写的优先；各部分从上到下依次排列。</summary>
public class ResultTests
{
    static FrameworkElement Show(params Result[] results)
    {
        var host = new StackPanel { Width = 400 };
        foreach (var result in results) host.Children.Add(result);
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        return host;
    }

    static T Part<T>(Result result, string name) where T : FrameworkElement => WpfUi.Descendants(result).OfType<T>().First(part => part.Name == name);

    static double Top(FrameworkElement part, Result result) => part.TranslatePoint(default, result).Y;

    [Fact]
    [Upstream("Result", "色调写到 data-tone；标题、说明、操作、内容依次排列")]
    public Task Tone_picks_the_icon_and_parts_stack_in_order() => WpfUi.Run(() =>
    {
        var details = new Text { Text = "E42" };
        var retry = new Button { Content = "重试" };
        var result = new Result { Tone = StatusTone.Danger, Title = "支付失败", Description = "余额不足", Action = retry, Content = details };
        Show(result);

        var icon = result.IconPart;
        Assert.Equal((Lucide.CircleExclamationPoint, TextTone.Danger), (icon.Data, icon.Tone));
        Assert.Equal(4 * result.FontSize, icon.ActualWidth, 3);
        var title = Part<Text>(result, "PART_Title");
        Assert.Equal(("支付失败", TextWeight.Semibold), (title.Text, title.Weight));
        var description = Part<Paragraph>(result, "PART_Description");
        Assert.Equal((TextTone.Muted, "余额不足"), (description.Tone, description.Text));
        Assert.Equal(Visibility.Collapsed, result.CodePart.Visibility); // 没有状态：没有状态码
        double[] tops = [Top(icon, result), Top(title, result), Top(description, result), Top(details, result), Top(retry, result)];
        Assert.Equal(tops.Order(), tops);
    });

    [Fact]
    [Upstream("Result", "图标可以换、可以去掉")]
    public Task Icon_can_be_replaced_or_removed() => WpfUi.Run(() =>
    {
        var replaced = new Result { Icon = Lucide.CircleX, Draw = false };
        var removed = new Result { Icon = null, Title = "无图标" };
        Show(replaced, removed);

        Assert.Equal(Lucide.CircleX, replaced.ShownIcon);
        Assert.Equal(1, replaced.IconPart.Drawn); // 不描线：直接画好
        Assert.Null(removed.ShownIcon);
        Assert.Equal(Lucide.CircleCheck, new Result().ShownIcon); // 默认 success
    });

    [Fact]
    [Upstream("Result", "status：状态码（读屏跳过）、语言包的标题与说明、按状态的色调；显式写的优先")]
    public Task Status_gives_a_code_and_default_texts_that_explicit_values_override() => WpfUi.Run(() =>
    {
        var notFound = new Result { Status = ResultStatus.NotFound };
        var forbidden = new Result { Status = ResultStatus.Forbidden, Description = "缺少财务查看权限", Tone = StatusTone.Danger };
        Show(notFound, forbidden);

        Assert.Equal((Lucide.FileQuestionMark, TextTone.Info), (notFound.ShownIcon, notFound.IconPart.Tone));
        Assert.Equal("404", notFound.CodePart.Text);
        Assert.Equal(2.5 * notFound.FontSize, notFound.CodePart.FontSize, 3);
        var children = UIElementAutomationPeer.CreatePeerForElement(notFound).GetChildren() ?? [];
        Assert.DoesNotContain(children, peer => peer.GetName() == "404"); // 状态码对读屏隐藏
        Assert.Contains(children, peer => peer.GetName() == "找不到这个页面");
        Assert.Equal("地址可能写错了，或者页面已经移走。", Part<Paragraph>(notFound, "PART_Description").Text);
        Assert.Equal((Lucide.ShieldX, TextTone.Danger), (forbidden.ShownIcon, forbidden.IconPart.Tone)); // 显式的色调优先
        Assert.Equal("没有访问权限", Part<Text>(forbidden, "PART_Title").Text);
        Assert.Equal("缺少财务查看权限", Part<Paragraph>(forbidden, "PART_Description").Text);
    });
}
