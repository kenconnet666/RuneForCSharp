using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>Steps：状态按 Current 推出（Status 可改）；可点回的步骤是按钮（写回 Current）或链接；不可用的不能点；Size 缩放字号；竖排一列。</summary>
public class StepsTests
{
    static StepItem[] Items => [new("购物车"), new("收货地址"), new("支付") { Status = StepState.Error }, new("完成")];

    static Control Content(Steps steps, int index) => ((Flex)steps.List.Children[index]).Children[0];

    static Border Indicator(Control content) => content.GetLogicalDescendants().OfType<Border>().First(border => border.Name == StepsParts.Indicator);

    [Fact]
    public Task States_follow_the_current_step_and_read_out_as_text() => HeadlessUi.Run(() =>
    {
        var steps = new Steps { Items = Items, Current = 1 };
        HeadlessUi.Show(steps);

        Assert.Equal(["complete", "current", "error", "upcoming"], Enumerable.Range(0, 4).Select(i => Indicator(Content(steps, i)).Classes.Single()));
        Assert.Equal(["已完成", "当前步骤", "出错", "未开始"], steps.List.Children.Select(item => AutomationProperties.GetItemStatus(item)));
        Assert.Equal(4, steps.List.Columns);
        // 没写 Navigable、也没有地址：已完成的步骤只是显示
        Assert.IsType<Flex>(Content(steps, 0));

        steps.Orientation = StepsOrientation.Vertical;
        Assert.Equal(1, steps.List.Columns);
    });

    [Fact]
    public Task Navigable_steps_go_back_and_disabled_ones_do_not() => HeadlessUi.Run(() =>
    {
        StepItem[] items = [new("拉取代码"), new("安装依赖") { IsDisabled = true }, new("运行测试"), new("构建")];
        var steps = new Steps { Items = items, Current = 2, Navigable = true };
        HeadlessUi.Show(steps);

        Assert.IsType<Flex>(Content(steps, 1)); // 标了不可用
        Assert.IsType<Flex>(Content(steps, 2)); // 当前步骤不是按钮
        var back = Assert.IsType<ButtonBase>(Content(steps, 0));
        Assert.Equal("拉取代码", AutomationProperties.GetName(back));
        back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(0, steps.Current);
        Assert.Equal("current", Indicator(Content(steps, 0)).Classes.Single());
    });

    [Fact]
    public Task Size_scales_the_circles_and_text() => HeadlessUi.Run(() =>
    {
        var steps = new Steps { Items = Items, Size = TextSize.Lg };
        HeadlessUi.Show(steps);

        var size = steps.GetValue(TextElement.FontSizeProperty);
        Assert.Equal(steps.FindResource(TextSize.Lg.ResourceKey!), size);
        Assert.Equal(2 * 0.93 * size, Indicator(Content(steps, 0)).Width, 3);
    });
}
