using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Carousel：轮播与每一张的名称；按钮与页点写回 Index，不循环时两头的按钮不可用；方向键换张；一次看几张时的最后一页；自动播放换张。</summary>
public class CarouselTests
{
    static readonly string[] Seasons = ["春", "夏", "秋"];

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Carousel", "区域与每一张的名称")]
    public Task Region_and_slide_names() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Width = 300 };
        AutomationProperties.SetName(carousel, "四季");
        var unnamed = new Carousel { ItemsSource = Seasons, Width = 300 };
        WpfUi.Show(Host(carousel, unnamed));

        Assert.Equal(AutomationControlType.Group, carousel.Peer().GetAutomationControlType());
        Assert.Equal(("四季", "carousel"), (carousel.Peer().GetName(), carousel.Peer().GetLocalizedControlType()));
        Assert.Equal("轮播", unnamed.Peer().GetName());
        Assert.Equal(["第 1 张，共 3 张", "第 2 张，共 3 张", "第 3 张，共 3 张"], carousel.Slides.Select(slide => slide.Peer().GetName()));
        Assert.Equal("slide", carousel.Slides[0].Peer().GetLocalizedControlType());
    });

    [Fact]
    [Upstream("Carousel", "按钮与页点写回 _index；不循环时两头的按钮不可用")]
    public Task Buttons_and_dots_write_the_index() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Loop = false, Width = 300 };
        WpfUi.Show(Host(carousel));
        var (previous, next, _) = carousel.Buttons;

        Assert.False(previous.IsEnabled);
        Assert.Equal("下一张", next.Peer().GetName());
        Invoke(next);
        Assert.Equal(1, carousel.Index);
        Invoke(carousel.DotButtons[2]);
        Assert.Equal(2, carousel.Index);
        Assert.False(next.IsEnabled);
        Assert.True(carousel.DotButtons[2].IsCurrent);
        Assert.False(carousel.DotButtons[0].IsCurrent);
        Assert.Equal("第 3 张，共 3 张", carousel.DotButtons[2].Peer().GetName());
    });

    [Fact]
    [Upstream("Carousel", "下一张：滚过去、序号写回；第一张往前绕到最后一张")]
    public Task Loop_wraps_and_arrow_keys_move() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Width = 300 };
        var host = Host(carousel);
        WpfUi.Show(host);

        Invoke(carousel.Buttons.Previous);
        Assert.Equal(2, carousel.Index);
        var source = PresentationSource.FromVisual(host)!;
        carousel.Viewport.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.Equal(0, carousel.Index);
    });

    [Fact]
    public Task Several_per_view_stop_at_the_last_page() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = new[] { "1", "2", "3", "4", "5", "6" }, SlidesPerView = 2.5, Gap = 10, Loop = false, Width = 400 };
        WpfUi.Show(Host(carousel));
        WpfUi.Flush();

        // 6 张、一次看 2.5 张：最后一页的序号是 ceil(6 − 2.5) = 4
        Assert.Equal(5, carousel.DotButtons.Count);
        Assert.Equal((400 - 1.5 * 10) / 2.5, carousel.Slides[0].ActualWidth, 3);
        carousel.Index = 4;
        Assert.False(carousel.Buttons.Next.IsEnabled);
    });

    [Fact]
    public Task Autoplay_moves_on_and_the_pause_button_stops_it() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = Seasons, Autoplay = TimeSpan.FromMilliseconds(50), Width = 300 };
        WpfUi.Show(Host(carousel));
        if (!Motion.Enabled) return;

        Assert.Equal(Visibility.Visible, carousel.Buttons.Pause.Visibility);
        Assert.Equal("暂停自动播放", carousel.Buttons.Pause.Peer().GetName());
        WpfUi.Until(() => carousel.Index == 1);
        Invoke(carousel.Buttons.Pause);
        Assert.False(carousel.Playing);
        Assert.Equal("开始自动播放", carousel.Buttons.Pause.Peer().GetName());
    });

    [Fact]
    public Task Controls_as_items_survive_a_rebuild() => WpfUi.Run(() =>
    {
        var slides = new[] { new Border(), new Border() };
        var carousel = new Carousel { ItemsSource = slides, Width = 300 };
        var host = Host(carousel);
        WpfUi.Show(host);

        // 加载时会再建一遍；换一组数据再换回来，控件都能放回去
        carousel.ItemsSource = Seasons;
        carousel.ItemsSource = slides;
        WpfUi.Flush();
        Assert.Same(slides[1], ((ContentPresenter)carousel.Slides[1]).Content);
    });

    [Fact]
    [Upstream("Carousel", "手动滚动停下后序号跟着变；不循环时到头那侧的按钮不可用")]
    public Task Dragging_snaps_and_writes_the_index() => WpfUi.Run(() =>
    {
        var carousel = new Carousel { ItemsSource = new[] { "1", "2", "3", "4" }, Loop = false, Width = 300, Height = 120 };
        WpfUi.Show(Host(carousel));
        Assert.False(carousel.Buttons.Previous.IsEnabled);

        // WPF 的鼠标位置模拟不了：直接喂张片区里的坐标。往回拖两张多一点，松开时吸附到最近的一张
        carousel.Drag(new Point(200, 60), new Point(200 - 620, 60));
        Assert.Equal(2, carousel.Index);
        carousel.Drag(new Point(200, 60), new Point(-900, 60));
        Assert.Equal(3, carousel.Index);
        Assert.False(carousel.Buttons.Next.IsEnabled);
    });
}
