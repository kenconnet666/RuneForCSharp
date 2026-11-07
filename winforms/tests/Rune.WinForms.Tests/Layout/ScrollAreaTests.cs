using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// ScrollAreaControl：内容按方向定大小、按滚动位置挪开；滚动条浮在内容上（滑块是叠上去的子控件）、粗 0.5em + 4、两个方向时让出右下角；
/// 内容超出时能聚焦；滚轮、键盘、拖动滑块都能滚；scroll / always 的显示时机，停下后按 HideDelay 隐藏。
/// </summary>
public class ScrollAreaTests
{
    /// <summary>要多高给多高的内容（同自动大小的 Flex）。</summary>
    sealed class Tall(Size wanted) : Control
    {
        public override Size GetPreferredSize(Size proposedSize) => wanted;
    }

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);

    [Fact]
    [Upstream("ScrollArea", "视口带上 propsViewport；属性落到外层；纵向时只有一条滚动条且对读屏隐藏")]
    public Task Vertical_by_default_and_hidden_when_nothing_overflows() => WinFormsUi.Run(() =>
    {
        var area = new ScrollAreaControl { Size = new Size(200, 100), Content = new Tall(new Size(100, 50)) };
        using var form = WinFormsUi.Show(area);

        Assert.Equal(new Rectangle(0, 0, 200, 100), area.Content!.Bounds); // 纵向：宽同区域，不小于区域
        Assert.False(area.IsOverflowing);
        Assert.False(area.TabStop);
        Assert.False(area.IsScrollbarVisible);
    });

    [Fact]
    [Upstream("ScrollArea", "axis=")]
    public Task Both_axes_get_two_bars_that_leave_the_corner() => WinFormsUi.Run(() =>
    {
        var area = new ScrollAreaControl { Size = new Size(200, 100), Axis = ScrollAreaAxis.Both, Type = ScrollbarType.Always, Content = new Tall(new Size(400, 400)) };
        using var form = WinFormsUi.Show(area);

        Assert.True(area.IsScrollbarVisible);
        var vertical = area.VerticalThumb;
        var horizontal = area.HorizontalThumb;
        Assert.True(vertical.Visible && horizontal.Visible);
        var inset = area.LogicalToDeviceUnits(2);
        var thickness = (int)MathF.Round(0.5f * area.Font.SizeInPoints * area.DeviceDpi / 72f) + 2 * inset;
        Assert.Equal(thickness - 2 * inset, vertical.Width);
        Assert.Equal(200 - thickness + inset, vertical.Left);
        // 让出右下角：纵向的轨道到 100 - 粗细为止
        Assert.InRange(vertical.Bottom, 0, 100 - thickness);
        Assert.InRange(horizontal.Right, 0, 200 - thickness);
    });

    [Fact]
    public Task Wheel_keyboard_and_dragging_scroll_the_content() => WinFormsUi.Run(() =>
    {
        var area = new ScrollAreaControl { Size = new Size(200, 100), Type = ScrollbarType.Always, Content = new Tall(new Size(100, 400)) };
        using var form = WinFormsUi.Show(area);

        Assert.True(area.IsOverflowing);
        Assert.True(area.TabStop);
        Raise(area, "OnMouseWheel", new HandledMouseEventArgs(MouseButtons.None, 0, 10, 10, -120));
        Assert.True(area.ScrollOffset.Y > 0);
        Assert.Equal(-area.ScrollOffset.Y, area.Content!.Top);

        Raise(area, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.Equal(300, area.ScrollOffset.Y);
        Raise(area, "OnKeyDown", new KeyEventArgs(Keys.Home));
        Assert.Equal(0, area.ScrollOffset.Y);

        // 滑块长度 = 视口 / 内容 × 轨道，减去上下各 2；拖到底就滚到底
        var thumb = area.VerticalThumb;
        var inset = area.LogicalToDeviceUnits(2);
        Assert.Equal(100 * 100 / 400 - 2 * inset, thumb.Height);
        area.ScrollOffset = new Point(0, 1000);
        Assert.Equal(300, area.ScrollOffset.Y);
        Assert.Equal(100 - inset, thumb.Bottom);

        area.IsViewportFocusable = false;
        Assert.False(area.TabStop);
    });

    [Fact]
    public Task Scroll_type_shows_only_while_scrolling() => WinFormsUi.Run(() =>
    {
        var area = new ScrollAreaControl
        {
            Size = new Size(200, 100), Type = ScrollbarType.Scroll, HideDelay = TimeSpan.FromMilliseconds(100), Content = new Tall(new Size(100, 400)),
        };
        using var form = WinFormsUi.Show(area);
        Assert.False(area.IsScrollbarVisible);

        area.ScrollOffset = new Point(0, 120);
        Assert.True(area.IsScrollbarVisible);
        Assert.True(area.VerticalThumb.Visible);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(300));
        Assert.False(area.IsScrollbarVisible);
    });

    [Fact]
    public Task Focusing_a_control_inside_scrolls_it_into_view() => WinFormsUi.Run(() =>
    {
        var flex = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg };
        var buttons = Enumerable.Range(1, 12).Select(i => new ButtonControl { Text = $"第 {i} 项" }).ToArray();
        flex.Controls.AddRange(buttons);
        var area = new ScrollAreaControl { Size = new Size(200, 100), Content = flex };
        using var form = WinFormsUi.Show(area);

        buttons[^1].Focus();
        WinFormsUi.Pump();
        var bounds = area.RectangleToClient(buttons[^1].RectangleToScreen(buttons[^1].ClientRectangle));
        Assert.InRange(bounds.Bottom, 0, 100);
    });
}
