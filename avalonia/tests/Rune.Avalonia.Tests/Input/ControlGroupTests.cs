using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>ControlGroup：输入框外壳、选择框、按钮去掉相邻一侧的圆角；输入框分掉剩余宽度；在 Field 里时组拿标签，里面的控件不拿。</summary>
public class ControlGroupTests
{
    [Fact]
    public Task Inputs_share_the_width_and_neighbours_lose_inner_corners() => HeadlessUi.Run(() =>
    {
        var currency = new Select { ItemsSource = new[] { "CNY", "USD" }, Value = "USD" };
        var amount = new NumberField { Value = 1200 };
        var convert = new Button { Content = "换算" };
        var group = new ControlGroup { Width = 600, Children = { currency, amount, convert } };
        var field = new Field { Label = "合同金额", Description = "按当天中间价换算", Content = group };
        HeadlessUi.Show(field);

        Assert.True(currency.CornerRadius.TopLeft > 0);
        Assert.Equal(0, currency.CornerRadius.TopRight);
        Assert.Equal(new CornerRadius(0), amount.CornerRadius);
        Assert.Equal(0, convert.CornerRadius.BottomLeft);
        Assert.Equal(600, convert.Bounds.Right, 1); // 输入框分掉剩余宽度，按钮贴在末尾
        Assert.True(amount.Bounds.Width > currency.Bounds.Width);

        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal("合同金额", group.Peer().GetName());
        Assert.Equal("按当天中间价换算", group.Peer().GetHelpText());
        Assert.NotEqual("合同金额", amount.Peer().GetName()); // 里面的控件不拿标签
    });

    [Fact]
    public Task Inline_groups_keep_their_content_width() => HeadlessUi.Run(() =>
    {
        var amount = new NumberField { Value = 1 };
        var group = new ControlGroup { Inline = true, Children = { new Select { ItemsSource = new[] { "CNY" }, Value = "CNY" }, amount } };
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { group } });

        Assert.True(group.Bounds.Width < 600);
    });
}
