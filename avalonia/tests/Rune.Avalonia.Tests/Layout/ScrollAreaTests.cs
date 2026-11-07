using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// ScrollArea：方向决定平台滚动条的设置；滚动条浮在内容上（不占位）、粗 0.5em + 4、两个方向时让出右下角；
/// 内容超出时能聚焦；hover / scroll / always 三种显示时机，停下后按 HideDelay 淡出。
/// </summary>
public class ScrollAreaTests
{
    static ScrollBar Bar(ScrollArea area, string name) => area.GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Name == name);

    static ScrollArea Tall(ScrollbarType type) => new()
    {
        Height = 100, Width = 200, Type = type, HideDelay = TimeSpan.FromMilliseconds(100),
        Content = new Border { Height = 400 },
    };

    [Fact]
    [Upstream("ScrollArea", "视口带上 propsViewport；属性落到外层；纵向时只有一条滚动条且对读屏隐藏")]
    public Task Vertical_by_default_and_hidden_when_nothing_overflows() => HeadlessUi.Run(() =>
    {
        var area = new ScrollArea { Height = 100, Content = new TextBlock { Text = "内容" } };
        HeadlessUi.Show(area);

        Assert.Equal((ScrollBarVisibility.Disabled, ScrollBarVisibility.Auto), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
        Assert.False(area.IsOverflowing);
        Assert.False(area.Focusable); // 没有超出时不进 Tab 顺序
        Assert.DoesNotContain(":active", area.Classes);
        Assert.Equal(0, Bar(area, ScrollAreaParts.VerticalScrollbar).Opacity);
        // 滚动条浮在内容上：视口占满整个区域
        Assert.Equal(area.Bounds.Width, area.GetVisualDescendants().OfType<ScrollContentPresenter>().Single().Bounds.Width);
    });

    [Fact]
    [Upstream("ScrollArea", "axis=")]
    public Task Both_axes_get_two_bars_that_leave_the_corner() => HeadlessUi.Run(() =>
    {
        var area = new ScrollArea { Axis = ScrollAreaAxis.Both, Width = 200, Height = 100, FontSize = 16, Content = new Border { Width = 400, Height = 400 } };
        HeadlessUi.Show(area);

        Assert.Equal((ScrollBarVisibility.Auto, ScrollBarVisibility.Auto), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
        var vertical = Bar(area, ScrollAreaParts.VerticalScrollbar);
        var horizontal = Bar(area, ScrollAreaParts.HorizontalScrollbar);
        Assert.Equal(12, vertical.Bounds.Width); // 0.5em + 两侧各 2
        Assert.Equal(12, horizontal.Bounds.Height);
        Assert.Equal(88, vertical.Bounds.Height);
        Assert.Equal(188, horizontal.Bounds.Width);

        area.Axis = ScrollAreaAxis.X;
        Assert.Equal((ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
    });

    [Fact]
    [Upstream("ScrollArea", "内容层、滚动条、滑块都能加属性")]
    public Task Overflowing_content_makes_the_area_focusable_with_a_thin_rounded_thumb() => HeadlessUi.Run(() =>
    {
        var area = Tall(ScrollbarType.Always);
        HeadlessUi.Show(area);

        Assert.True(area.IsOverflowing);
        Assert.True(area.Focusable);
        Assert.Contains(":active", area.Classes); // always：超出时一直显示
        var thumb = area.GetVisualDescendants().OfType<Thumb>().Single(part => part.Name == ScrollAreaParts.Thumb && part.IsEffectivelyVisible);
        Assert.Equal(0.5, thumb.Opacity);
        Assert.Equal(new Thickness(2), thumb.Margin);
        Assert.InRange(thumb.Bounds.Height, 20, 30); // 视口 100 / 内容 400 的四分之一

        area.IsViewportFocusable = false;
        Assert.False(area.Focusable);
    });

    [Fact]
    public Task Hover_shows_the_bars_and_they_fade_after_the_delay() => HeadlessUi.Run(() =>
    {
        var area = Tall(ScrollbarType.Hover);
        var window = HeadlessUi.Show(area);
        Assert.DoesNotContain(":active", area.Classes);

        var center = area.TranslatePoint(new Point(100, 50), window)!.Value;
        window.MouseMove(center);
        HeadlessUi.Flush(window);
        Assert.Contains(":active", area.Classes);
        Assert.True(Bar(area, ScrollAreaParts.VerticalScrollbar).IsHitTestVisible);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(250));
        Assert.Contains(":active", area.Classes); // 指针还在上面

        window.MouseMove(new Point(700, 500));
        HeadlessUi.Until(() => !area.Classes.Contains(":active"));
    });

    [Fact]
    public Task Scroll_type_shows_only_while_scrolling() => HeadlessUi.Run(() =>
    {
        var area = Tall(ScrollbarType.Scroll);
        var window = HeadlessUi.Show(area);
        window.MouseMove(area.TranslatePoint(new Point(100, 50), window)!.Value);
        HeadlessUi.Flush(window);
        Assert.DoesNotContain(":active", area.Classes);

        area.Offset = new Vector(0, 120);
        Assert.Contains(":active", area.Classes);
        HeadlessUi.Until(() => !area.Classes.Contains(":active"));
    });
}
