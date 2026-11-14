namespace Rune.WinForms.Tests;

/// <summary>StepsControl：状态按 Current 推出（Status 可改）；可点回的步骤是按钮（写回 Current、发 CurrentChanged）；不可用的不能点；TextSize 缩放字号；横排各步等宽。</summary>
public class StepsTests
{
    static StepItem[] Items => [new("购物车"), new("收货地址"), new("支付") { Status = StepState.Error }, new("完成")];

    [Fact]
    public Task States_follow_the_current_step_and_read_out_as_text() => WinFormsUi.Run(() =>
    {
        var steps = new StepsControl { Items = Items, Current = 1, Width = 600 };
        using var form = WinFormsUi.Show(steps);

        Assert.Equal(["已完成", "当前步骤", "出错", "未开始"], steps.Contents.Select(content => content.AccessibleDescription));
        // 横排：每步放在等宽的一格里（格宽 = (600 - 3 × sm 间距) / 4）
        var lefts = steps.Contents.Select(content => content.Left).ToArray();
        Assert.Equal(lefts[1] - lefts[0], lefts[2] - lefts[1]);
        Assert.IsType<FlexControl>(steps.Contents[0]); // 没写 Navigable、也没有地址：已完成的步骤只是显示

        steps.Orientation = StepsOrientation.Vertical;
        Assert.True(steps.Contents[1].Top > steps.Contents[0].Bottom);
        Assert.Equal(0, steps.Contents[1].Left);
    });

    [Fact]
    public Task Navigable_steps_go_back_and_disabled_ones_do_not() => WinFormsUi.Run(() =>
    {
        StepItem[] items = [new("拉取代码"), new("安装依赖") { IsDisabled = true }, new("运行测试"), new("构建")];
        var steps = new StepsControl { Items = items, Current = 2, Navigable = true, Width = 600 };
        using var form = WinFormsUi.Show(steps);
        var changed = 0;
        steps.CurrentChanged += (_, _) => changed++;

        Assert.False(steps.Contents[1].TabStop); // 标了不可用
        Assert.False(steps.Contents[2].TabStop); // 当前步骤不是按钮
        var back = steps.Contents[0];
        Assert.True(back.TabStop);
        Assert.Equal("拉取代码", back.AccessibleName);
        back.AccessibilityObject.DoDefaultAction();
        Assert.Equal(0, steps.Current);
        Assert.Equal(1, changed);
        Assert.Equal("当前步骤", steps.Contents[0].AccessibleDescription);
    });

    [Fact]
    public Task TextSize_scales_the_circles_and_text() => WinFormsUi.Run(() =>
    {
        var steps = new StepsControl { Items = Items, TextSize = TextSize.Lg, Width = 600 };
        using var form = WinFormsUi.Show(steps);

        Assert.Equal(Tokens.FontSize.Lg * 72 / 96, steps.Font.SizeInPoints, 2);
        var circle = steps.Contents[0].Controls[0];
        Assert.Equal((int)MathF.Round(2 * 0.93f * steps.Font.EmPixels(steps.DeviceDpi)), circle.Height);
    });
}
