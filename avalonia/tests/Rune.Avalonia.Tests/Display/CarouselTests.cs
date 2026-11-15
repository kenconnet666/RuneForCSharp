using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Carousel：轮播与每一张的名称；按钮与页点写回 Index，不循环时两头的按钮不可用；方向键换张；一次看几张时的最后一页；自动播放换张。</summary>
public class CarouselTests
{
    static readonly string[] Seasons = ["春", "夏", "秋"];

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    [Fact]
    [Upstream("Carousel", "区域与每一张的名称")]
    public Task Region_and_slide_names() => HeadlessUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Width = 300 };
        AutomationProperties.SetName(carousel, "四季");
        var unnamed = new Carousel { ItemsSource = Seasons, Width = 300 };
        HeadlessUi.Show(new StackPanel { Children = { carousel, unnamed } });

        Assert.Equal(AutomationControlType.Group, carousel.Peer().GetAutomationControlType());
        Assert.Equal(("四季", "carousel"), (carousel.Peer().GetName(), carousel.Peer().GetLocalizedControlType()));
        Assert.Equal("轮播", unnamed.Peer().GetName());
        Assert.Equal(["第 1 张，共 3 张", "第 2 张，共 3 张", "第 3 张，共 3 张"], carousel.Slides.Select(slide => slide.Peer().GetName()));
    });

    [Fact]
    [Upstream("Carousel", "按钮与页点写回 _index；不循环时两头的按钮不可用")]
    public Task Buttons_and_dots_write_the_index() => HeadlessUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Loop = false, Width = 300 };
        var window = HeadlessUi.Show(carousel);
        var (previous, next, _) = carousel.Buttons;

        Assert.False(previous.IsEnabled);
        Assert.Equal("下一张", next.Peer().GetName());
        Invoke(next);
        Assert.Equal(1, carousel.Index);
        Invoke(carousel.DotButtons[2]);
        Assert.Equal(2, carousel.Index);
        Assert.False(next.IsEnabled);
        Assert.Contains(":current", carousel.DotButtons[2].Classes);
        Assert.Equal("第 3 张，共 3 张", carousel.DotButtons[2].Peer().GetName());
        HeadlessUi.Flush(window);
    });

    [Fact]
    public Task Loop_wraps_and_arrow_keys_move() => HeadlessUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Width = 300 };
        HeadlessUi.Show(carousel);

        Invoke(carousel.Buttons.Previous);
        Assert.Equal(2, carousel.Index);
        carousel.Viewport.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right, Source = carousel.Viewport });
        Assert.Equal(0, carousel.Index);
    });

    [Fact]
    public Task Several_per_view_stop_at_the_last_page() => HeadlessUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = new[] { "1", "2", "3", "4", "5", "6" }, SlidesPerView = 2.5, Gap = 10, Loop = false, Width = 400 };
        var window = HeadlessUi.Show(carousel);
        HeadlessUi.Flush(window);

        // 6 张、一次看 2.5 张：最后一页的序号是 ceil(6 − 2.5) = 4
        Assert.Equal(5, carousel.DotButtons.Count);
        Assert.Equal((400 - 1.5 * 10) / 2.5, carousel.Slides[0].Bounds.Width, 3);
        carousel.Index = 4;
        Assert.False(carousel.Buttons.Next.IsEnabled);
    });

    [Fact]
    public Task Autoplay_moves_on_and_the_pause_button_stops_it() => HeadlessUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Autoplay = TimeSpan.FromMilliseconds(50), Width = 300 };
        HeadlessUi.Show(carousel);
        if (!Motion.Enabled) return;

        Assert.True(carousel.Buttons.Pause.IsVisible);
        Assert.Equal("暂停自动播放", carousel.Buttons.Pause.Peer().GetName());
        HeadlessUi.Until(() => carousel.Index == 1);
        Invoke(carousel.Buttons.Pause);
        Assert.False(carousel.Playing);
        Assert.Equal("开始自动播放", carousel.Buttons.Pause.Peer().GetName());
    });

    [Fact]
    public Task Controls_as_items_survive_a_rebuild() => HeadlessUi.Run(() =>
    {
        var slides = new[] { new Border(), new Border() };
        var carousel = new Carousel { ItemsSource = slides, Width = 300 };
        var window = HeadlessUi.Show(carousel);

        // 放进界面时会再建一遍；换一组数据再换回来，控件都能放回去
        carousel.ItemsSource = Seasons;
        carousel.ItemsSource = slides;
        HeadlessUi.Flush(window);
        Assert.Same(slides[1], ((global::Avalonia.Controls.Presenters.ContentPresenter)carousel.Slides[1]).Content);
    });
}
