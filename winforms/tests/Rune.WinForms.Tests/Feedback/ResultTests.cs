namespace Rune.WinForms.Tests;

/// <summary>ResultControl：按色调或状态选图标与颜色；状态码画出来、读屏里没有，标题、说明取语言包，显式写的优先；各部分从上到下依次排列。</summary>
public class ResultTests
{
    [Fact]
    [Upstream("Result", "色调写到 data-tone；标题、说明、操作、内容依次排列")]
    public Task Tone_picks_the_icon_and_parts_stack_in_order() => WinFormsUi.Run(() =>
    {
        var details = new CodeControl { Text = "E42" };
        var retry = new ButtonControl { Text = "重试" };
        var result = new ResultControl { Tone = StatusTone.Danger, Title = "支付失败", Description = "余额不足", Action = retry, Dock = DockStyle.Top };
        result.Controls.Add(details);
        using var form = WinFormsUi.Show(result);
        var em = result.Font.SizeInPoints * result.DeviceDpi / 72f;

        var (icon, code, title, description) = result.Elements;
        Assert.Equal((Lucide.CircleExclamationPoint, TextTone.Danger), (icon.Data, icon.Tone));
        Assert.Equal(4 * em, icon.Bounds.Width, 1f);
        Assert.Equal((TextWeight.Semibold, TextTone.Muted), (title.Weight, description.Tone));
        Assert.Equal("", code.Content); // 没有状态：没有状态码
        int[] tops = [icon.Bounds.Top, title.Bounds.Top, description.Bounds.Top, details.Top, retry.Top];
        Assert.Equal(tops.Order(), tops);
        Assert.Equal(("支付失败", "余额不足"), (result.AccessibilityObject.Name, result.AccessibilityObject.Description));
    });

    [Fact]
    [Upstream("Result", "图标可以换、可以去掉")]
    public Task Icon_can_be_replaced_or_removed() => WinFormsUi.Run(() =>
    {
        var replaced = new ResultControl { Icon = Lucide.CircleX, Draw = false, Dock = DockStyle.Top };
        var removed = new ResultControl { Icon = null, Title = "无图标", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(removed, replaced);

        Assert.Equal(Lucide.CircleX, replaced.ShownIcon);
        Assert.Equal(1, replaced.Elements.Icon.Drawn); // 不描线：直接画好
        Assert.Null(removed.ShownIcon);
        Assert.Equal(Lucide.CircleCheck, new ResultControl().ShownIcon); // 默认 success
    });

    [Fact]
    [Upstream("Result", "status：状态码（读屏跳过）、语言包的标题与说明、按状态的色调；显式写的优先")]
    public Task Status_gives_a_code_and_default_texts_that_explicit_values_override() => WinFormsUi.Run(() =>
    {
        var notFound = new ResultControl { Status = ResultStatus.NotFound, Dock = DockStyle.Top };
        var forbidden = new ResultControl { Status = ResultStatus.Forbidden, Description = "缺少财务查看权限", Tone = StatusTone.Danger, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(forbidden, notFound);

        Assert.Equal((Lucide.FileQuestionMark, TextTone.Info), (notFound.ShownIcon, notFound.Elements.Icon.Tone));
        Assert.Equal("404", notFound.Elements.Code.Content);
        Assert.Equal(("找不到这个页面", "地址可能写错了，或者页面已经移走。"), (notFound.AccessibilityObject.Name, notFound.AccessibilityObject.Description)); // 读屏念标题，不念状态码
        Assert.Equal((Lucide.ShieldX, TextTone.Danger), (forbidden.ShownIcon, forbidden.Elements.Icon.Tone)); // 显式的色调优先
        Assert.Equal(("没有访问权限", "缺少财务查看权限"), (forbidden.Elements.Title.Content, forbidden.Elements.Description.Content));
    });

    [Fact]
    public Task Drawing_plays_once_and_again_when_turned_back_on() => WinFormsUi.Run(() =>
    {
        var result = new ResultControl { Title = "提交成功", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(result);
        WinFormsUi.Paint(form);
        WinFormsUi.Until(() => !Animator.IsRunning);

        result.Title = "已提交"; // 改文字不重画图标
        WinFormsUi.Paint(form);
        Assert.False(Animator.IsRunning);
        result.Draw = false;
        result.Draw = true; // 由 false 变为 true 再播（系统关闭动画时直接画好）
        WinFormsUi.Paint(form);
        Assert.Equal(Motion.Enabled, Animator.IsRunning);
    });
}
