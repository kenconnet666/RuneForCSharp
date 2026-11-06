using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Container：宽度撑满、超过最大宽度时居中，左右内边距按字号算。</summary>
public class ContainerTests
{
    [Upstream("Container", "div，档位追加样式")]
    [Fact]
    public Task Limits_width_centers_and_pads_inline() => WpfUi.Run(() =>
    {
        var (narrow, wide) = (new Border { Height = 10 }, new Border { Height = 10 });
        var small = new Container { Size = ContainerSize.Xs, Child = narrow };
        var roomy = new Container { Padding = Space.None, Child = wide };
        var host = new StackPanel { Width = 600, Children = { small, roomy } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        var em = TextElement.GetFontSize(small);

        // xs：最宽 480，居中；左右各 space.lg（1em）
        Assert.Equal(new Rect(60 + em, 0, 480 - 2 * em, 10), new Rect(narrow.TranslatePoint(default, small), narrow.RenderSize));
        // lg（1024）比父元素宽：撑满；没有内边距
        Assert.Equal(new Rect(0, 0, 600, 10), new Rect(wide.TranslatePoint(default, roomy), wide.RenderSize));
    });
}
