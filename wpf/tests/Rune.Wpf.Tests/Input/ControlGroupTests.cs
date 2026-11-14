using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Button = Rune.Wpf.Button;

namespace Rune.Wpf.Tests;

/// <summary>ControlGroup：输入框外壳、选择框、按钮去掉相邻一侧的圆角；输入框分掉剩余宽度；在 Field 里时组拿标签，里面的控件不拿。</summary>
public class ControlGroupTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Width = 600, Children = { child } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return host;
    }

    [Fact]
    public Task Inputs_share_the_width_and_neighbours_lose_inner_corners() => WpfUi.Run(() =>
    {
        var currency = new Select { ItemsSource = new[] { "CNY", "USD" }, Value = "USD" };
        var amount = new NumberField { Value = 1200 };
        var convert = new Button { Content = "换算" };
        var group = new ControlGroup { Children = { currency, amount, convert } };
        var field = new Field { Label = "合同金额", Description = "按当天中间价换算", Content = group };
        WpfUi.Show(Host(field));

        Assert.True(currency.CornerRadius.TopLeft > 0);
        Assert.Equal(0, currency.CornerRadius.TopRight);
        Assert.Equal(new CornerRadius(0), amount.CornerRadius);
        Assert.Equal(0, convert.CornerRadius.BottomLeft);
        Assert.Equal(group.ActualWidth, convert.TranslatePoint(new Point(convert.ActualWidth, 0), group).X, 1); // 按钮贴在末尾
        Assert.True(amount.ActualWidth > currency.ActualWidth);

        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal("合同金额", group.Peer().GetName());
        Assert.Equal("按当天中间价换算", group.Peer().GetHelpText());
        Assert.NotEqual("合同金额", amount.Peer().GetName()); // 里面的控件不拿标签
    });

    [Fact]
    public Task Inline_groups_keep_their_content_width() => WpfUi.Run(() =>
    {
        var group = new ControlGroup { Inline = true, Children = { new Select { ItemsSource = new[] { "CNY" }, Value = "CNY" }, new NumberField { Value = 1 } } };
        WpfUi.Show(Host(group));

        Assert.True(group.ActualWidth < 600);
    });
}
