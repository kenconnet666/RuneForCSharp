using Avalonia;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>Affix：滚过 OffsetTop 时钉在滚动区域的顶部（伪类 :stuck、阴影、StuckChanged），滚回来取消；不超出父元素；OffsetBottom 时贴在底部，滚到原位时取消。</summary>
public class AffixTests
{
    [Fact]
    [Upstream("Affix", "哨兵滚到 offsetTop 之上就是贴住：data-stuck、阴影、onChange；滚回来取消")]
    public Task Scrolling_past_the_offset_pins_it_to_the_top() => HeadlessUi.Run(() =>
    {
        var affix = new Affix { OffsetTop = 8, Content = new Border { Height = 40 } };
        var changes = new List<bool>();
        affix.StuckChanged += (_, _) => changes.Add(affix.IsStuck);
        var scroller = new ScrollArea { Height = 300, Content = new StackPanel { Children = { new Border { Height = 100 }, affix, new Border { Height = 2000 } } } };
        var window = HeadlessUi.Show(scroller);

        scroller.Offset = new Vector(0, 50);
        HeadlessUi.Flush(window);
        Assert.False(affix.IsStuck);
        Assert.Equal(0, affix.Shift);

        scroller.Offset = new Vector(0, 200);
        HeadlessUi.Flush(window);
        Assert.True(affix.IsStuck);
        Assert.Contains(":stuck", affix.Classes);
        Assert.Equal(8, affix.TranslatePoint(default, scroller)!.Value.Y, 3);
        Assert.Equal(5, affix.ZIndex);
        Assert.NotEqual(default, affix.Part<Border>(AffixParts.Root).BoxShadow);

        scroller.Offset = new Vector(0, 0);
        HeadlessUi.Flush(window);
        Assert.False(affix.IsStuck);
        Assert.Equal(default, affix.Part<Border>(AffixParts.Root).BoxShadow);
        Assert.Equal([true, false], changes);
    });

    [Fact]
    public Task It_stays_inside_its_parent() => HeadlessUi.Run(() =>
    {
        var affix = new Affix { Content = new Border { Height = 40 } };
        var section = new StackPanel { Children = { affix, new Border { Height = 200 } } };
        var scroller = new ScrollArea { Height = 300, Content = new StackPanel { Children = { section, new Border { Height = 2000 } } } };
        var window = HeadlessUi.Show(scroller);

        // 父元素（240 高）滚出去 220：钉住的位移最多到父元素的底边（200）
        scroller.Offset = new Vector(0, 220);
        HeadlessUi.Flush(window);
        Assert.Equal(200, affix.Shift, 3);
    });

    [Fact]
    public Task Offset_bottom_pins_it_to_the_bottom_until_its_place_scrolls_in() => HeadlessUi.Run(() =>
    {
        var affix = new Affix { OffsetBottom = 0, Shadow = false, Content = new Border { Height = 40 } };
        var scroller = new ScrollArea { Height = 300, Content = new StackPanel { Children = { new Border { Height = 2000 }, affix } } };
        var window = HeadlessUi.Show(scroller);
        HeadlessUi.Flush(window);

        Assert.True(affix.IsStuck);
        Assert.Equal(300 - 40, affix.TranslatePoint(default, scroller)!.Value.Y, 3);
        Assert.Equal(default, affix.Part<Border>(AffixParts.Root).BoxShadow);
        scroller.Offset = new Vector(0, 1740);
        HeadlessUi.Flush(window);
        Assert.False(affix.IsStuck);
    });
}
