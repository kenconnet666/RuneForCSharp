using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Splitter：分隔条的读屏取值、键盘、收起；两栏按比例分；拖动；ResizeHandle 改父元素的宽度。</summary>
public class SplitterTests
{
    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    static IRangeValueProvider Range(UIElement element) => (IRangeValueProvider)element.Peer().GetPattern(PatternInterface.RangeValue);

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    /// <summary>元素在 relativeTo 里的矩形。</summary>
    static Rect Bounds(FrameworkElement element, UIElement relativeTo) => new(element.TranslatePoint(new Point(0, 0), relativeTo), element.RenderSize);

    /// <summary>分隔条的粗细（space.md = 0.75em）。</summary>
    static double Track(Splitter splitter) => 0.75 * System.Windows.Documents.TextElement.GetFontSize(splitter);

    [Fact]
    [Upstream("Splitter", "分隔条是可聚焦的 separator，带当前比例与范围；两栏按比例分")]
    public Task Handle_is_a_focusable_separator_and_panes_follow_the_size() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Size = 30, Start = "列表", End = "预览", Height = 200 };
        splitter.Width = 400 + Track(splitter);
        WpfUi.Show(Host(splitter));

        var handle = splitter.Handle;
        Assert.True(handle.Focusable);
        Assert.Equal(AutomationControlType.Separator, handle.Peer().GetAutomationControlType());
        Assert.Equal("调整分栏", handle.Peer().GetName());
        Assert.Equal((30d, 20d, 80d), (Range(handle).Value, Range(handle).Minimum, Range(handle).Maximum));
        Assert.Equal(Track(splitter), handle.RenderSize.Width, 3);
        Assert.Equal(120, splitter.StartPane.RenderSize.Width, 3);
        Assert.Equal(280, splitter.EndPane.RenderSize.Width, 3);
        Assert.Equal(120 + Track(splitter), Bounds(splitter.EndPane, splitter).X, 3);
        Assert.Equal(Visibility.Collapsed, splitter.Toggle.Visibility);
    });

    [Fact]
    [Upstream("Splitter", "←/→ 每次 5%，Home / End 到最小、最大，写回 size")]
    public Task Arrows_step_five_percent_home_end_jump() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Start = "a", End = "b", Width = 400, Height = 200 };
        WpfUi.Show(Host(splitter));
        var handle = splitter.Handle;

        Press(handle, Key.Right);
        Assert.Equal(55, splitter.Size);
        Press(handle, Key.Left);
        Press(handle, Key.Left);
        Assert.Equal(45, splitter.Size);
        Press(handle, Key.Home);
        Assert.Equal(20, splitter.Size);
        Press(handle, Key.End);
        Assert.Equal(80, splitter.Size);
        // 上下键在左右并排时不调
        Press(handle, Key.Down);
        Assert.Equal(80, splitter.Size);
    });

    [Fact]
    public Task Right_to_left_mirrors_the_arrows() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Start = "a", End = "b", Width = 400, Height = 200, FlowDirection = FlowDirection.RightToLeft };
        WpfUi.Show(Host(splitter));
        Press(splitter.Handle, Key.Left);
        Assert.Equal(55, splitter.Size);
    });

    [Fact]
    [Upstream("Splitter", "collapsible：Enter 或按钮收起那一侧（hidden），再按展开；不能收起时没有按钮")]
    public Task Enter_or_the_button_collapses_the_collapsible_side() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Collapsible = PaneSide.End, Start = "a", End = "b", Width = 400, Height = 200 };
        WpfUi.Show(Host(splitter));
        var handle = splitter.Handle;

        Press(handle, Key.Enter);
        Assert.True(splitter.IsCollapsed);
        Assert.Equal(Visibility.Collapsed, splitter.EndPane.Visibility);
        Assert.Equal(100, Range(handle).Value);
        var toggle = splitter.Toggle;
        Assert.Equal(Visibility.Visible, toggle.Visibility);
        Assert.False(toggle.Focusable);
        Assert.Equal("展开", toggle.Peer().GetName());
        Assert.Equal(Lucide.ChevronLeft, toggle.Icon);
        ((IInvokeProvider)toggle.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.False(splitter.IsCollapsed);
        Assert.Equal(Visibility.Visible, splitter.EndPane.Visibility);
        Assert.Equal("收起", toggle.Peer().GetName());
        Assert.Equal(Lucide.ChevronRight, toggle.Icon);

        // 调比例时展开
        splitter.IsCollapsed = true;
        Press(handle, Key.Right);
        Assert.False(splitter.IsCollapsed);
    });

    [Fact]
    [Upstream("Splitter", "收起开始那一栏：它是 hidden、不占格子，模板只留分隔条与结束那一栏两轨")]
    public Task Collapsed_start_takes_no_space() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Collapsible = PaneSide.Start, IsCollapsed = true, Start = "a", End = "b", Height = 200 };
        splitter.Width = 400 + Track(splitter);
        WpfUi.Show(Host(splitter));

        Assert.Equal(Visibility.Collapsed, splitter.StartPane.Visibility);
        Assert.Equal(0, Bounds(splitter.Handle, splitter).X);
        var end = Bounds(splitter.EndPane, splitter);
        Assert.Equal((Track(splitter), 400d), (end.X, end.Width));
        Assert.Equal(0, Range(splitter.Handle).Value);
    });

    [Fact]
    [Upstream("Splitter", "上下排列：分隔条是横向的，用 ↑/↓")]
    public Task Vertical_uses_up_and_down() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Orientation = SplitterOrientation.Vertical, Start = "a", End = "b", Width = 200 };
        splitter.Height = 400 + Track(splitter);
        WpfUi.Show(Host(splitter));
        var handle = splitter.Handle;

        Assert.Equal(Track(splitter), handle.RenderSize.Height, 3);
        Assert.Equal(200, splitter.StartPane.RenderSize.Height, 3);
        Press(handle, Key.Down);
        Assert.Equal(55, splitter.Size);
        Press(handle, Key.Right);
        Assert.Equal(55, splitter.Size);
    });

    [Fact]
    public Task Dragging_the_handle_resizes_and_restores_the_initial_size() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Size = 40, Start = "a", End = "b", Width = 400, Height = 200 };
        WpfUi.Show(Host(splitter));
        var handle = splitter.Handle;

        handle.PointerDown(new Point(170, 100));
        Assert.True(handle.IsDragging);
        // 往右整个分栏宽的 25%
        handle.PointerMove(new Point(270, 100));
        Assert.Equal(65, splitter.Size);
        // 夹在 maxSize
        handle.PointerMove(new Point(470, 100));
        Assert.Equal(80, splitter.Size);
        handle.PointerUp();
        Assert.False(handle.IsDragging);

        handle.Restore();
        Assert.Equal(40, splitter.Size);
    });

    [Fact]
    public Task Bordered_has_a_surface_border() => WpfUi.Run(() =>
    {
        var splitter = new Splitter { Bordered = true, Start = "a", End = "b", Width = 400, Height = 200 };
        WpfUi.Show(Host(splitter));
        var surface = (Border)System.Windows.Media.VisualTreeHelper.GetChild(splitter, 0);
        Assert.Equal(new Thickness(1), surface.BorderThickness);
        Assert.Equal(0.7 * System.Windows.Documents.TextElement.GetFontSize(splitter), surface.CornerRadius.TopLeft, 3);
        Assert.NotNull(surface.Background);
    });

    [Fact]
    public Task Resize_handle_sets_the_parent_width_with_keys_and_drag() => WpfUi.Run(() =>
    {
        var handle = new ResizeHandle { MinLength = 160, MaxLength = 360, DefaultLength = 220 };
        AutomationProperties.SetName(handle, "调整列表宽度");
        var panel = new System.Windows.Controls.Grid { Width = 220, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, Children = { new TextBlock { Text = "列表" }, handle } };
        var changes = new List<double>();
        handle.LengthChanged += (_, length) => changes.Add(length);
        WpfUi.Show(Host(panel));

        // 贴着结束边、space.sm（0.5em）宽
        var thickness = 0.5 * System.Windows.Documents.TextElement.GetFontSize(handle);
        var bounds = Bounds(handle, panel);
        Assert.Equal((220 - thickness, 0d, thickness, 100d), (bounds.X, bounds.Y, bounds.Width, bounds.Height));
        Assert.Equal(AutomationControlType.Separator, handle.Peer().GetAutomationControlType());
        Assert.Equal("调整列表宽度", handle.Peer().GetName());
        Assert.Equal((220d, 160d, 360d), (Range(handle).Value, Range(handle).Minimum, Range(handle).Maximum));

        Press(handle, Key.Right);
        Assert.Equal(236, panel.Width);
        Press(handle, Key.Left);
        Press(handle, Key.Left);
        Assert.Equal(204, panel.Width);
        Assert.Equal([236d, 220d, 204d], changes);

        handle.PointerDown(new Point(200, 50));
        handle.PointerMove(new Point(700, 50));
        Assert.Equal(360, panel.Width);
        handle.PointerMove(new Point(169.6, 50));
        Assert.Equal(174, panel.Width);
        handle.PointerUp();
        handle.Restore();
        Assert.Equal(220, panel.Width);
    });

    [Fact]
    public Task Resize_handle_at_the_start_edge_grows_toward_the_start() => WpfUi.Run(() =>
    {
        var handle = new ResizeHandle { Edge = PaneSide.Start };
        var panel = new System.Windows.Controls.Grid { Width = 200, Height = 100, Children = { handle } };
        WpfUi.Show(Host(panel));
        Assert.Equal(0, Bounds(handle, panel).X);
        Press(handle, Key.Left);
        Assert.Equal(216, panel.Width);
    });

    [Fact]
    public Task Horizontal_resize_handle_sets_the_height() => WpfUi.Run(() =>
    {
        var handle = new ResizeHandle { Orientation = Orientation.Horizontal };
        var panel = new System.Windows.Controls.Grid { Width = 200, Height = 100, Children = { handle } };
        WpfUi.Show(Host(panel));
        var thickness = 0.5 * System.Windows.Documents.TextElement.GetFontSize(handle);
        var bounds = Bounds(handle, panel);
        Assert.Equal((0d, 100 - thickness, 200d), (bounds.X, bounds.Y, bounds.Width));
        Press(handle, Key.Down);
        Assert.Equal(116, panel.Height);
        Press(handle, Key.Right);
        Assert.Equal(116, panel.Height);
    });
}
