using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>CarouselControl：轮播与每一张的名称；按钮与页点写回 Index，不循环时两头的按钮不可用；方向键换张；一次看几张时的最后一页；自动播放换张。</summary>
public class CarouselTests
{
    static Form Show(params Control[] controls)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        stack.Controls.AddRange(controls);
        host.Controls.Add(stack);
        return WinFormsUi.Show(host);
    }

    static CarouselControl Seasons(int width = 300)
    {
        var carousel = new CarouselControl { Width = width };
        foreach (var season in new[] { "春", "夏", "秋" }) carousel.Slides.Add(new Label { Text = season, Height = 40 });
        return carousel;
    }

    [Fact]
    [Upstream("Carousel", "区域与每一张的名称")]
    public Task Region_and_slide_names() => WinFormsUi.Run(() =>
    {
        var carousel = Seasons();
        carousel.AccessibleName = "四季";
        var unnamed = Seasons();
        using var form = Show(carousel, unnamed);

        Assert.Equal(AccessibleRole.Grouping, carousel.AccessibilityObject.Role);
        Assert.Equal("四季", carousel.AccessibilityObject.Name);
        Assert.Equal("轮播", unnamed.AccessibilityObject.Name);
        Assert.Equal(["第 1 张，共 3 张", "第 2 张，共 3 张", "第 3 张，共 3 张"], carousel.SlideHosts.Select(host => host.AccessibilityObject.Name));
        Assert.All(carousel.SlideHosts, host => Assert.Equal(AccessibleRole.Grouping, host.AccessibilityObject.Role));
    });

    [Fact]
    [Upstream("Carousel", "按钮与页点写回 _index；不循环时两头的按钮不可用")]
    public Task Buttons_and_dots_write_the_index() => WinFormsUi.Run(() =>
    {
        var carousel = Seasons();
        carousel.Loop = false;
        var changes = 0;
        carousel.IndexChanged += (_, _) => changes++;
        using var form = Show(carousel);
        var (previous, next, _) = carousel.Buttons;

        Assert.False(previous.Enabled);
        Assert.Equal("下一张", next.AccessibleName);
        next.PerformClick();
        Assert.Equal(1, carousel.Index);
        carousel.DotButtons[2].PerformClick();
        Assert.Equal(2, carousel.Index);
        Assert.Equal(2, changes);
        Assert.False(next.Enabled);
        Assert.True(carousel.DotButtons[2].IsCurrent);
        Assert.False(carousel.DotButtons[0].IsCurrent);
        Assert.Equal("第 3 张，共 3 张", carousel.DotButtons[2].AccessibilityObject.Name);
        Assert.Equal("第 3 张，共 3 张", carousel.Announced);
    });

    [Fact]
    [Upstream("Carousel", "下一张：滚过去、序号写回；第一张往前绕到最后一张")]
    public Task Loop_wraps_and_arrow_keys_move() => WinFormsUi.Run(() =>
    {
        var carousel = Seasons();
        using var form = Show(carousel);

        carousel.Buttons.Previous.PerformClick();
        Assert.Equal(2, carousel.Index);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(carousel.Viewport, [new KeyEventArgs(Keys.Right)]);
        Assert.Equal(0, carousel.Index);
    });

    [Fact]
    public Task Several_per_view_stop_at_the_last_page() => WinFormsUi.Run(() =>
    {
        var carousel = new CarouselControl { SlidesPerView = 2.5, Gap = 10, Loop = false };
        for (var index = 0; index < 6; index++) carousel.Slides.Add(new Label { Text = $"{index + 1}", Height = 40 });
        using var form = Show(carousel);
        carousel.Width = carousel.LogicalToDeviceUnits(400);
        WinFormsUi.Pump();

        // 6 张、一次看 2.5 张：最后一页的序号是 ceil(6 − 2.5) = 4
        Assert.Equal(5, carousel.DotButtons.Count);
        Assert.Equal((carousel.Width - 1.5 * carousel.LogicalToDeviceUnits(10)) / 2.5, carousel.SlideHosts[0].Width, 0.5);
        carousel.Index = 4;
        Assert.False(carousel.Buttons.Next.Enabled);
        Assert.Equal(40, carousel.Height);
    });

    [Fact]
    public Task Autoplay_moves_on_and_the_pause_button_stops_it() => WinFormsUi.Run(() =>
    {
        var carousel = Seasons();
        carousel.Autoplay = TimeSpan.FromMilliseconds(50);
        using var form = Show(carousel);
        if (!Motion.Enabled) return;

        Assert.True(carousel.Buttons.Pause.Visible);
        Assert.Equal("暂停自动播放", carousel.Buttons.Pause.AccessibleName);
        WinFormsUi.Until(() => carousel.Index == 1);
        carousel.Buttons.Pause.PerformClick();
        Assert.False(carousel.Playing);
        Assert.Equal("开始自动播放", carousel.Buttons.Pause.AccessibleName);
    });

    [Fact]
    [Upstream("Carousel", "手动滚动停下后序号跟着变；不循环时到头那侧的按钮不可用")]
    public Task Horizontal_wheel_moves_and_the_end_disables_next() => WinFormsUi.Run(() =>
    {
        var carousel = Seasons();
        carousel.Slides.Add(new Label { Text = "冬", Height = 40 });
        carousel.Loop = false;
        using var form = Show(carousel);
        Assert.False(carousel.Buttons.Previous.Enabled);

        // 触摸板横向滑动（WM_MOUSEHWHEEL，往右为正）：子控件不处理时转到轮播上
        var wndProc = typeof(Control).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance)!;
        void Swipe() => wndProc.Invoke(carousel, [Message.Create(carousel.Handle, 0x020E, 120 << 16, 0)]);
        Swipe();
        Swipe();
        Assert.Equal(2, carousel.Index);
        for (var i = 0; i < 4; i++) Swipe();
        Assert.Equal(3, carousel.Index);
        Assert.False(carousel.Buttons.Next.Enabled);
    });
}
