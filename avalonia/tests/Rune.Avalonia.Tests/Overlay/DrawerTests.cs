using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Drawer：贴着所在一侧、宽度档位与 Width、贴边一侧没有描边与圆角；宽度调整手柄改 Width；上下两侧没有手柄；Esc 关闭（内核同 Dialog）。</summary>
public class DrawerTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static Window Open(Drawer drawer)
    {
        var window = HeadlessUi.Show(drawer);
        drawer.IsOpen = true;
        HeadlessUi.Until(() => drawer.Panel.Bounds.Width > 0);
        HeadlessUi.Flush(window);
        return window;
    }

    [Fact]
    public Task End_side_hugs_the_right_edge_at_the_size_width() => HeadlessUi.Run(() =>
    {
        var drawer = new Drawer { Title = "筛选" };
        Open(drawer);

        Assert.Equal(new Rect(800 - 480, 0, 480, 600), drawer.Panel.Bounds); // xs，窗口高
        Assert.Equal(new Thickness(1, 1, 0, 1), drawer.Panel.BorderThickness);
        Assert.Equal(default, drawer.Panel.CornerRadius);
        // 手柄贴着靠正文的一边（面板的起始边）
        Assert.True(drawer.Handle.IsVisible);
        Assert.Equal(800 - 480, drawer.Handle.Bounds.X);
        Assert.Equal(600, drawer.Handle.Bounds.Height);
        Assert.Equal(AutomationControlType.Separator, drawer.Handle.Peer().GetAutomationControlType());
        Assert.Equal("调整抽屉宽度", drawer.Handle.Peer().GetName());
    });

    [Fact]
    public Task Start_side_hugs_the_left_edge_and_width_overrides_the_size() => HeadlessUi.Run(() =>
    {
        var drawer = new Drawer { Title = "导航", Side = DrawerSide.Start, Size = DialogSize.Md, Width = 300 };
        Open(drawer);

        Assert.Equal(new Rect(0, 0, 300, 600), drawer.Panel.Bounds);
        Assert.Equal(new Thickness(0, 1, 1, 1), drawer.Panel.BorderThickness);
        Assert.Equal(300 - drawer.Handle.Bounds.Width, drawer.Handle.Bounds.X);
    });

    [Fact]
    public Task Resize_handle_writes_width_within_min_and_window() => HeadlessUi.Run(() =>
    {
        var drawer = new Drawer { Title = "详情", Width = 400 };
        var window = Open(drawer);

        // 结束边的抽屉：手柄在它的起始边，往左（←）是变宽
        Press(drawer.Handle, Key.Left);
        Assert.Equal(416, drawer.Width);
        HeadlessUi.Flush(window);
        Assert.Equal(416, drawer.Panel.Bounds.Width);
        drawer.Handle.Resize(10);
        Assert.Equal(48, drawer.Width); // MinWidth
        drawer.Handle.Resize(2000);
        Assert.Equal(800 - 48, drawer.Width);
        HeadlessUi.Flush(window);
        // 拖窄以后内容保持最小宽度（20em），被面板裁掉
        drawer.Handle.Resize(48);
        HeadlessUi.Flush(window);
        Assert.Equal(48, drawer.Panel.Bounds.Width);
        Assert.True(drawer.Panel.Child!.Bounds.Width >= 20 * 12 - 1);
    });

    [Fact]
    public Task Top_and_bottom_span_the_width_without_a_handle() => HeadlessUi.Run(() =>
    {
        var bottom = new Drawer { Title = "操作", Side = DrawerSide.Bottom, Content = new TextBlock { Text = "正文" } };
        Open(bottom);

        var box = bottom.Panel.Bounds;
        Assert.Equal((0d, 800d, 600d), (box.X, box.Width, box.Bottom));
        Assert.True(box.Height < 600);
        Assert.False(bottom.Handle.IsVisible);
        Assert.Equal(new Thickness(1, 1, 1, 0), bottom.Panel.BorderThickness);
    });

    [Fact]
    public Task Escape_closes_like_a_dialog() => HeadlessUi.Run(() =>
    {
        var drawer = new Drawer { Title = "筛选" };
        var reasons = new List<OpenChangeReason>();
        drawer.OpenChanged += (_, e) => reasons.Add(e.Reason);
        Open(drawer);

        drawer.Panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = drawer.Panel });
        HeadlessUi.Until(() => !drawer.IsOpen);
        Assert.Equal([OpenChangeReason.Escape], reasons);
    });
}
