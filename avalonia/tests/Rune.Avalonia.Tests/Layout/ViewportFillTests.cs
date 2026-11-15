using Avalonia;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>ViewportFill：里面竖排；高度是滚动区域的可视区高减去自己的顶边与底部留白（不随滚动变），不小于最小高度；撑开的子元素占满剩下的。</summary>
public class ViewportFillTests
{
    static (ViewportFill Fill, Border Grow, ScrollArea Scroller, Window Window) Show(double viewport, double above)
    {
        var grow = new Border();
        Flex.SetGrow(grow, true);
        var fill = new ViewportFill { Gap = Space.Md, Children = { new Border { Height = 30 }, grow, new Border { Height = 20 } } };
        var scroller = new ScrollArea { Height = viewport, Content = new StackPanel { Children = { new Border { Height = above }, fill, new Border { Height = 400 } } } };
        var window = HeadlessUi.Show(scroller);
        HeadlessUi.Flush(window);
        HeadlessUi.Flush(window);
        return (fill, grow, scroller, window);
    }

    [Fact]
    [Upstream("ViewportFill", "里面竖排，高度写在行内的 CSS 变量里（视口高度减去顶边）")]
    public Task Fills_to_the_bottom_of_the_viewport() => HeadlessUi.Run(() =>
    {
        var (fill, grow, scroller, window) = Show(viewport: 800, above: 100);
        double Space(string key) => fill.Resource<double>(key) * global::Avalonia.Controls.Documents.TextElement.GetFontSize(fill);
        // 可视区 800 − 顶边 100 − 底部 xl
        var expected = 800 - 100 - Space(ThemeKeys.Space.Xl);
        Assert.Equal(expected, fill.Bounds.Height, tolerance: 1);
        Assert.Equal(3, fill.Children.Count);
        // 中间的撑满：去掉上下两块与两个 md 的间距
        Assert.Equal(expected - 30 - 20 - 2 * Space(ThemeKeys.Space.Md), grow.Bounds.Height, tolerance: 1);
        scroller.Offset = new Vector(0, 60);
        HeadlessUi.Flush(window);
        Assert.Equal(expected, fill.Bounds.Height, tolerance: 1);
    });

    [Fact]
    public Task Never_shorter_than_the_minimum() => HeadlessUi.Run(() =>
    {
        var (fill, _, _, _) = Show(viewport: 300, above: 100);
        // 24rem（rem 按 16px）
        Assert.Equal(384, fill.Bounds.Height, tolerance: 1);
    });
}
