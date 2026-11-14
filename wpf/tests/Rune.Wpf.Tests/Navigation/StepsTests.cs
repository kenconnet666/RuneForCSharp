using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Steps：状态按 Current 推出（Status 可改）；可点回的步骤是按钮（写回 Current）；不可用的不能点；Size 缩放字号；竖排一列。</summary>
public class StepsTests
{
    static StepItem[] Items => [new("购物车"), new("收货地址"), new("支付") { Status = StepState.Error }, new("完成")];

    static UIElement Content(Steps steps, int index) => ((Flex)steps.List.Children[index]).Children[0];

    static Border Indicator(UIElement content) => (Border)((Flex)(content is ButtonBase trigger ? trigger.Content : content)).Children[0];

    static Steps Show(Steps steps)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(steps);
        WpfUi.Show(host);
        return steps;
    }

    [Fact]
    public Task States_follow_the_current_step_and_read_out_as_text() => WpfUi.Run(() =>
    {
        var steps = Show(new Steps { Items = Items, Current = 1 });

        Assert.Equal([StepState.Complete, StepState.Current, StepState.Error, StepState.Upcoming], Enumerable.Range(0, 4).Select(i => Steps.GetState(Indicator(Content(steps, i)))));
        Assert.Equal(["已完成", "当前步骤", "出错", "未开始"], steps.List.Children.Cast<UIElement>().Select(AutomationProperties.GetItemStatus));
        Assert.Equal(4, steps.List.Columns);
        Assert.IsType<Flex>(Content(steps, 0)); // 没写 Navigable、也没有地址：已完成的步骤只是显示

        steps.Orientation = StepsOrientation.Vertical;
        Assert.Equal(1, steps.List.Columns);
    });

    [Fact]
    public Task Navigable_steps_go_back_and_disabled_ones_do_not() => WpfUi.Run(() =>
    {
        StepItem[] items = [new("拉取代码"), new("安装依赖") { IsDisabled = true }, new("运行测试"), new("构建")];
        var steps = Show(new Steps { Items = items, Current = 2, Navigable = true });

        Assert.IsType<Flex>(Content(steps, 1)); // 标了不可用
        Assert.IsType<Flex>(Content(steps, 2)); // 当前步骤不是按钮
        var back = Assert.IsType<ButtonBase>(Content(steps, 0));
        Assert.Equal("拉取代码", AutomationProperties.GetName(back));
        back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(0, steps.Current);
        Assert.Equal(StepState.Current, Steps.GetState(Indicator(Content(steps, 0))));
    });

    [Fact]
    public Task Size_scales_the_circles_and_text() => WpfUi.Run(() =>
    {
        var steps = Show(new Steps { Items = Items, Size = TextSize.Lg });
        WpfUi.Flush();

        var size = TextElement.GetFontSize(steps);
        Assert.Equal(steps.FindResource(TextSize.Lg.ResourceKey!), size);
        Assert.Equal(2 * 0.93 * size, Indicator(Content(steps, 0)).Width, 3);
    });
}
