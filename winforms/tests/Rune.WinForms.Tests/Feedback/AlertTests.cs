namespace Rune.WinForms.Tests;

/// <summary>AlertControl：图标按色调默认选、写 null 去掉；读屏的名称与描述；直接加的子控件是额外内容；订阅了 CloseRequested 才有关闭按钮。</summary>
public class AlertTests
{
    [Fact]
    [Upstream("Alert", "图标按色调默认选，可以去掉；live 决定角色；标题、正文、操作各有部件")]
    public Task Icon_follows_the_tone_and_can_be_removed_and_the_action_sits_at_the_end() => WinFormsUi.Run(() =>
    {
        var undo = new ButtonControl { Text = "撤销" };
        var saved = new AlertControl { Tone = StatusTone.Success, Title = "已保存", Description = "修改已生效", Action = undo, Dock = DockStyle.Top };
        var error = new AlertControl { Tone = StatusTone.Danger, Live = AlertLive.Assertive, Icon = null, Description = "出错了", Dock = DockStyle.Top };
        var polite = new AlertControl { Live = AlertLive.Polite, Description = "提示", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(polite, error, saved);

        Assert.Equal(Lucide.CircleCheck, saved.ShownIcon);
        Assert.Equal(("已保存", "修改已生效"), (saved.AccessibilityObject.Name, saved.AccessibilityObject.Description));
        Assert.True(undo.Right <= saved.Width && undo.Left > saved.Width / 2); // 操作在结束端
        Assert.Null(error.ShownIcon); // 写 null 不显示图标
        Assert.Equal(AlertLive.Assertive, error.Live);
        Assert.Equal(Lucide.Info, polite.ShownIcon);
        saved.IsLoading = true;
        Assert.Null(saved.ShownIcon); // 加载中换成加载指示
        Assert.True((saved.AccessibilityObject.State & AccessibleStates.Busy) != 0);
    });

    [Fact]
    [Upstream("Alert", "说明写在 description 或 children 上；两个都给时 children 是说明下面的额外内容")]
    public Task Child_controls_sit_below_the_description() => WinFormsUi.Run(() =>
    {
        var link = new LinkControl { Text = "查看失败原因" };
        var alert = new AlertControl { Title = "导入完成", Description = "成功 120 条", Dock = DockStyle.Top };
        alert.Controls.Add(link);
        using var form = WinFormsUi.Show(alert);

        var withoutExtra = new AlertControl { Title = "导入完成", Description = "成功 120 条" }.GetPreferredSize(new Size(alert.Width, 0)).Height;
        Assert.True(alert.Height > withoutExtra); // 额外内容在说明下面，提示条跟着变高
        Assert.True(link.Top > alert.Height / 2);
    });

    [Fact]
    [Upstream("Alert", "给了 onClose 才有关闭按钮；离场结束后才调用")]
    public Task Closing_needs_a_subscriber() => WinFormsUi.Run(() =>
    {
        var alert = new AlertControl { Description = "x", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(alert);
        Assert.Null(alert.CloseButton);

        var closed = 0;
        alert.CloseRequested += (_, _) => closed++;
        Assert.Equal("关闭", alert.CloseButton!.AccessibilityObject.Name);
        alert.CloseButton.PerformClick();
        Assert.Equal(1, closed);
    });
}
