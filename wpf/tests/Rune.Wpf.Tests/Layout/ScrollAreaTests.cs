using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Rune.Wpf.Tests;

/// <summary>
/// ScrollArea：方向决定平台滚动条的设置；滚动条浮在内容上（不占位）、粗 0.5em + 4、两个方向时让出右下角；
/// 内容超出时能聚焦；hover / scroll / always 三种显示时机，停下后按 HideDelay 淡出。
/// </summary>
public class ScrollAreaTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    static ScrollBar Bar(ScrollArea area, string name) => (ScrollBar)area.Template.FindName(name, area);

    static ScrollArea Tall(ScrollbarType type) => new()
    {
        Height = 100, Width = 200, Type = type, HideDelay = TimeSpan.FromMilliseconds(100),
        Content = new Border { Height = 400 },
    };

    [Fact]
    [Upstream("ScrollArea", "视口带上 propsViewport；属性落到外层；纵向时只有一条滚动条且对读屏隐藏")]
    public Task Vertical_by_default_and_hidden_when_nothing_overflows() => WpfUi.Run(() =>
    {
        var area = new ScrollArea { Height = 100, Content = new TextBlock { Text = "内容" } };
        WpfUi.Show(Host(area));

        Assert.Equal((ScrollBarVisibility.Disabled, ScrollBarVisibility.Auto), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
        Assert.False(area.IsOverflowing);
        Assert.False(area.Focusable);
        Assert.False(area.IsScrollbarVisible);
        // 滚动条浮在内容上：视口占满整个区域
        Assert.Equal(area.ActualWidth, ((FrameworkElement)area.Template.FindName("PART_ScrollContentPresenter", area)).ActualWidth);
    });

    [Fact]
    [Upstream("ScrollArea", "axis=")]
    public Task Both_axes_get_two_bars_that_leave_the_corner() => WpfUi.Run(() =>
    {
        var area = new ScrollArea { Axis = ScrollAreaAxis.Both, Width = 200, Height = 100, FontSize = 16, Content = new Border { Width = 400, Height = 400 } };
        WpfUi.Show(Host(area));

        Assert.Equal((ScrollBarVisibility.Auto, ScrollBarVisibility.Auto), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
        var vertical = Bar(area, "PART_VerticalScrollBar");
        var horizontal = Bar(area, "PART_HorizontalScrollBar");
        Assert.Equal(12, vertical.ActualWidth); // 0.5em + 两侧各 2
        Assert.Equal(12, horizontal.ActualHeight);
        Assert.Equal(88, vertical.ActualHeight);
        Assert.Equal(188, horizontal.ActualWidth);

        area.Axis = ScrollAreaAxis.X;
        Assert.Equal((ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled), (area.HorizontalScrollBarVisibility, area.VerticalScrollBarVisibility));
    });

    [Fact]
    [Upstream("ScrollArea", "内容层、滚动条、滑块都能加属性")]
    public Task Overflowing_content_makes_the_area_focusable_with_a_thin_rounded_thumb() => WpfUi.Run(() =>
    {
        var area = Tall(ScrollbarType.Always);
        WpfUi.Show(Host(area));

        Assert.True(area.IsOverflowing);
        Assert.True(area.Focusable);
        Assert.True(area.IsScrollbarVisible); // always：超出时一直显示
        var thumb = WpfUi.Descendants(Bar(area, "PART_VerticalScrollBar")).OfType<Thumb>().Single();
        Assert.Equal(0.5, thumb.Opacity);
        Assert.Equal(new Thickness(2), thumb.Margin);
        Assert.InRange(thumb.ActualHeight, 16, 30); // 视口 100 / 内容 400 的四分之一，减去上下各 2

        area.IsViewportFocusable = false;
        Assert.False(area.Focusable);
    });

    [Fact]
    public Task Scroll_type_shows_only_while_scrolling() => WpfUi.Run(() =>
    {
        var area = Tall(ScrollbarType.Scroll);
        WpfUi.Show(Host(area));
        Assert.False(area.IsScrollbarVisible);

        area.ScrollToVerticalOffset(120);
        WpfUi.Flush();
        Assert.True(area.IsScrollbarVisible);
        Assert.True(Bar(area, "PART_VerticalScrollBar").IsHitTestVisible);
        WpfUi.Until(() => !area.IsScrollbarVisible);
        Assert.False(Bar(area, "PART_VerticalScrollBar").IsHitTestVisible);
    });
}
