using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;

namespace Rune.Avalonia.Tests;

/// <summary>Container：宽度撑满、超过最大宽度时居中，左右内边距按字号算。</summary>
public class ContainerTests
{
    [Upstream("Container", "div，档位追加样式")]
    [Fact]
    public Task Limits_width_centers_and_pads_inline() => HeadlessUi.Run(() =>
    {
        var (narrow, wide) = (new Border { Height = 10 }, new Border { Height = 10 });
        var small = new Container { Size = ContainerSize.Xs, Content = narrow };
        var roomy = new Container { Padding = Space.None, Content = wide };
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { small, roomy } });
        var em = small.GetValue(TextElement.FontSizeProperty);

        // xs：最宽 480，居中；左右各 space.lg（1em）
        Assert.Equal(new Rect(60 + em, 0, 480 - 2 * em, 10), narrow.Bounds);
        // lg（1024）比父元素宽：撑满；没有内边距
        Assert.Equal(new Rect(0, 0, 600, 10), wide.Bounds);
    });
}
