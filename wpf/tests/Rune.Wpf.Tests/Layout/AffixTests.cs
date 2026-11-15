using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Affix：滚过 OffsetTop 时钉在滚动区域的顶部（IsStuck、阴影、StuckChanged），滚回来取消；不超出父元素；OffsetBottom 时贴在底部，滚到原位时取消。</summary>
public class AffixTests
{
    static ScrollArea Scroller(UIElement content)
    {
        var scroller = new ScrollArea { Height = 300, Content = content };
        scroller.Resources.MergedDictionaries.Add(new RuneTheme());
        return scroller;
    }

    [Fact]
    [Upstream("Affix", "哨兵滚到 offsetTop 之上就是贴住：data-stuck、阴影、onChange；滚回来取消")]
    [Upstream("Affix", "贴顶：滚下去后贴在面板顶部")]
    public Task Scrolling_past_the_offset_pins_it_to_the_top() => WpfUi.Run(() =>
    {
        var affix = new Affix { OffsetTop = 8, Content = new Border { Height = 40 } };
        var changes = new List<bool>();
        affix.StuckChanged += (_, _) => changes.Add(affix.IsStuck);
        var scroller = Scroller(new StackPanel { Children = { new Border { Height = 100 }, affix, new Border { Height = 2000 } } });
        WpfUi.Show(scroller);

        scroller.ScrollToVerticalOffset(50);
        WpfUi.Flush();
        Assert.False(affix.IsStuck);
        Assert.Equal(0, affix.Shift);

        scroller.ScrollToVerticalOffset(200);
        WpfUi.Flush();
        Assert.True(affix.IsStuck);
        Assert.Equal(8, affix.TranslatePoint(default, scroller).Y, 3);
        Assert.Equal(5, Panel.GetZIndex(affix));
        Assert.NotNull(affix.Layer.Effect);

        scroller.ScrollToVerticalOffset(0);
        WpfUi.Flush();
        Assert.False(affix.IsStuck);
        Assert.Null(affix.Layer.Effect);
        Assert.Equal([true, false], changes);
    });

    [Fact]
    public Task It_stays_inside_its_parent() => WpfUi.Run(() =>
    {
        var affix = new Affix { Content = new Border { Height = 40 } };
        var section = new StackPanel { Children = { affix, new Border { Height = 200 } } };
        var scroller = Scroller(new StackPanel { Children = { section, new Border { Height = 2000 } } });
        WpfUi.Show(scroller);

        // 父元素（240 高）滚出去 220：钉住的位移最多到父元素的底边（200）
        scroller.ScrollToVerticalOffset(220);
        WpfUi.Flush();
        Assert.Equal(200, affix.Shift, 3);
    });

    [Fact]
    [Upstream("Affix", "贴底：压着内容时停在面板底边并标记贴住；滚到末尾回到原位")]
    public Task Offset_bottom_pins_it_to_the_bottom_until_its_place_scrolls_in() => WpfUi.Run(() =>
    {
        var affix = new Affix { OffsetBottom = 0, Shadow = false, Content = new Border { Height = 40 } };
        var scroller = Scroller(new StackPanel { Children = { new Border { Height = 2000 }, affix } });
        WpfUi.Show(scroller);
        WpfUi.Flush();

        Assert.True(affix.IsStuck);
        Assert.Equal(scroller.ViewportHeight - 40, affix.TranslatePoint(default, scroller).Y, 3);
        Assert.Null(affix.Layer.Effect);
        scroller.ScrollToVerticalOffset(scroller.ScrollableHeight);
        WpfUi.Flush();
        Assert.False(affix.IsStuck);
    });
}
