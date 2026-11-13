using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Result：按色调或状态选图标与颜色；状态码读屏跳过，标题、说明取语言包，显式写的优先；各部分从上到下依次排列。</summary>
public class ResultTests
{
    // 操作里的按钮模板也有 PART_Icon：Result 自己的部件排在前面
    static T Part<T>(Result result, string name) where T : Control => result.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    static double Top(Control part, Result result) => part.TranslatePoint(default, result)!.Value.Y;

    [Fact]
    [Upstream("Result", "色调写到 data-tone；标题、说明、操作、内容依次排列")]
    public Task Tone_picks_the_icon_and_parts_stack_in_order() => HeadlessUi.Run(() =>
    {
        var details = new Text { Text = "E42" };
        var retry = new Button { Content = "重试" };
        var result = new Result { Tone = StatusTone.Danger, Title = "支付失败", Description = "余额不足", Action = retry, Content = details };
        HeadlessUi.Show(new StackPanel { Width = 400, Children = { result } });

        var icon = Part<Icon>(result, ResultParts.Icon);
        Assert.Equal((Lucide.CircleExclamationPoint, TextTone.Danger), (icon.Data, icon.Tone));
        Assert.Equal(4 * result.FontSize, icon.Bounds.Width, 3);
        var title = Part<Text>(result, ResultParts.Title);
        Assert.Equal(("支付失败", TextWeight.Semibold), (title.Text, title.Weight));
        var description = Part<Paragraph>(result, ResultParts.Description);
        Assert.Equal((TextTone.Muted, "余额不足"), (description.Tone, description.Text));
        Assert.False(Part<Text>(result, ResultParts.Code).IsVisible); // 没有状态：没有状态码
        // 图标、标题、说明、内容、操作从上到下
        double[] tops = [Top(icon, result), Top(title, result), Top(description, result), Top(details, result), Top(retry, result)];
        Assert.Equal(tops.Order(), tops);
    });

    [Fact]
    [Upstream("Result", "图标可以换、可以去掉")]
    public Task Icon_can_be_replaced_or_removed() => HeadlessUi.Run(() =>
    {
        var replaced = new Result { Icon = Lucide.CircleX, Draw = false };
        var removed = new Result { Icon = null, Title = "无图标" };
        HeadlessUi.Show(new StackPanel { Children = { replaced, removed } });

        Assert.Equal(Lucide.CircleX, replaced.ShownIcon);
        Assert.Equal(1, replaced.IconPart.Drawn); // 不描线：直接画好
        Assert.Null(removed.ShownIcon);
        Assert.Equal(Lucide.CircleCheck, new Result().ShownIcon); // 默认 success
    });

    [Fact]
    [Upstream("Result", "status：状态码（读屏跳过）、语言包的标题与说明、按状态的色调；显式写的优先")]
    public Task Status_gives_a_code_and_default_texts_that_explicit_values_override() => HeadlessUi.Run(() =>
    {
        var notFound = new Result { Status = ResultStatus.NotFound };
        var forbidden = new Result { Status = ResultStatus.Forbidden, Description = "缺少财务查看权限", Tone = StatusTone.Danger };
        HeadlessUi.Show(new StackPanel { Children = { notFound, forbidden } });

        Assert.Equal((Lucide.FileQuestionMark, TextTone.Info), (notFound.ShownIcon, notFound.IconPart.Tone));
        var code = notFound.CodePart;
        Assert.Equal(("404", AccessibilityView.Raw), (code.Text, AutomationProperties.GetAccessibilityView(code)));
        Assert.Equal(2.5 * notFound.FontSize, code.FontSize, 3);
        Assert.Equal("找不到这个页面", Part<Text>(notFound, ResultParts.Title).Text);
        Assert.Equal("地址可能写错了，或者页面已经移走。", Part<Paragraph>(notFound, ResultParts.Description).Text);
        Assert.Equal(TextTone.Danger, forbidden.IconPart.Tone); // 显式的色调优先
        Assert.Equal(Lucide.ShieldX, forbidden.ShownIcon);
        Assert.Equal("没有访问权限", Part<Text>(forbidden, ResultParts.Title).Text);
        Assert.Equal("缺少财务查看权限", Part<Paragraph>(forbidden, ResultParts.Description).Text);
    });
}
