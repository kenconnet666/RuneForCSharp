using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Drawer：贴着所在一侧、宽度档位与 Width、贴边一侧没有描边与圆角；宽度调整手柄改 Width；上下两侧没有手柄；Esc 关闭（内核同 Dialog）。</summary>
public class DrawerTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    /// <summary>排版给的位置（不含滑入动画的位移）。</summary>
    static Rect Slot(FrameworkElement element) => LayoutInformation.GetLayoutSlot(element);

    static Size Open(Drawer drawer)
    {
        var host = Host(drawer);
        WpfUi.Show(host);
        drawer.IsOpen = true;
        WpfUi.Flush();
        WpfUi.Until(() => drawer.Panel.ActualWidth > 0);
        return new Size(host.ActualWidth, ((FrameworkElement)drawer.Panel.Parent).ActualHeight);
    }

    [Fact]
    public Task End_side_hugs_the_right_edge_at_the_size_width() => WpfUi.Run(() =>
    {
        var drawer = new Drawer { Title = "筛选" };
        var area = Open(drawer);

        Assert.Equal(new Rect(area.Width - 480, 0, 480, area.Height), Slot(drawer.Panel)); // xs，窗口高
        Assert.Equal(new Thickness(1, 1, 0, 1), drawer.Panel.BorderThickness);
        Assert.Equal(default, drawer.Panel.CornerRadius);
        Assert.Equal(Visibility.Visible, drawer.Handle.Visibility);
        Assert.Equal(area.Width - 480, Slot(drawer.Handle).X);
        Assert.Equal(AutomationControlType.Separator, drawer.Handle.Peer().GetAutomationControlType());
        Assert.Equal("调整抽屉宽度", drawer.Handle.Peer().GetName());
        drawer.IsOpen = false;
    });

    [Fact]
    public Task Start_side_hugs_the_left_edge_and_width_overrides_the_size() => WpfUi.Run(() =>
    {
        var drawer = new Drawer { Title = "导航", Side = DrawerSide.Start, Size = DialogSize.Md, Width = 300 };
        var area = Open(drawer);

        Assert.Equal(new Rect(0, 0, 300, area.Height), Slot(drawer.Panel));
        Assert.Equal(new Thickness(0, 1, 1, 1), drawer.Panel.BorderThickness);
        Assert.Equal(300 - drawer.Handle.ActualWidth, Slot(drawer.Handle).X, 0.5);
        drawer.IsOpen = false;
    });

    [Fact]
    public Task Resize_handle_writes_width_within_min_and_window() => WpfUi.Run(() =>
    {
        var drawer = new Drawer { Title = "详情", Width = 400 };
        var area = Open(drawer);

        // 结束边的抽屉：手柄在它的起始边，往左（←）是变宽
        Press(drawer.Handle, Key.Left);
        Assert.Equal(416, drawer.Width);
        WpfUi.Flush();
        Assert.Equal(416, drawer.Panel.ActualWidth);
        drawer.Handle.Resize(10);
        Assert.Equal(48, drawer.Width);
        drawer.Handle.Resize(5000);
        Assert.Equal(area.Width - 48, drawer.Width);
        drawer.Handle.Resize(48);
        WpfUi.Flush();
        Assert.Equal(48, drawer.Panel.ActualWidth);
        // 拖窄以后内容保持最小宽度（20em），被面板裁掉
        Assert.True(((FrameworkElement)drawer.Panel.Child).ActualWidth >= 20 * System.Windows.Documents.TextElement.GetFontSize(drawer) - 1);
        drawer.IsOpen = false;
    });

    [Fact]
    public Task Top_and_bottom_span_the_width_without_a_handle() => WpfUi.Run(() =>
    {
        var bottom = new Drawer { Title = "操作", Side = DrawerSide.Bottom, Content = new TextBlock { Text = "正文" } };
        var area = Open(bottom);

        var box = Slot(bottom.Panel);
        Assert.Equal((0d, area.Width, area.Height), (box.X, box.Width, box.Bottom));
        Assert.True(box.Height < area.Height);
        Assert.Equal(Visibility.Collapsed, bottom.Handle.Visibility);
        Assert.Equal(new Thickness(1, 1, 1, 0), bottom.Panel.BorderThickness);
        bottom.IsOpen = false;
    });

    [Fact]
    public Task Escape_closes_like_a_dialog() => WpfUi.Run(() =>
    {
        var drawer = new Drawer { Title = "筛选" };
        var reasons = new List<OpenChangeReason>();
        drawer.OpenChanged += (_, e) => reasons.Add(e.Reason);
        Open(drawer);

        drawer.Panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(drawer.Panel)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        WpfUi.Until(() => !drawer.IsOpen);
        Assert.Equal([OpenChangeReason.Escape], reasons);
    });
}
