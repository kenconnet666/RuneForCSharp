using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Splitter：分隔条的读屏取值、键盘、收起；两栏按比例分；拖动；ResizeHandle 改父元素的宽度。</summary>
public class SplitterTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static IRangeValueProvider Range(Control handle) => (IRangeValueProvider)handle.Peer();

    [Fact]
    [Upstream("Splitter", "分隔条是可聚焦的 separator，带当前比例与范围；两栏按比例分")]
    public Task Handle_is_a_focusable_separator_and_panes_follow_the_size() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Size = 30, Start = "列表", End = "预览", Width = 409, Height = 200 };
        HeadlessUi.Show(splitter);

        var handle = splitter.Handle;
        Assert.True(handle.Focusable);
        Assert.Equal(AutomationControlType.Separator, handle.Peer().GetAutomationControlType());
        Assert.Equal("调整分栏", handle.Peer().GetName());
        Assert.Equal((30d, 20d, 80d), (Range(handle).Value, Range(handle).Minimum, Range(handle).Maximum));
        // 分隔条占 space.md（0.75em，测试窗口的字号是 12），剩下 400 按 30 : 70 分
        Assert.Equal(9, handle.Bounds.Width);
        Assert.Equal(120, splitter.StartPane.Bounds.Width);
        Assert.Equal(280, splitter.EndPane.Bounds.Width);
        Assert.Equal(129, splitter.EndPane.Bounds.X);
        Assert.False(splitter.Toggle.IsVisible);
    });

    [Fact]
    [Upstream("Splitter", "←/→ 每次 5%，Home / End 到最小、最大，写回 size")]
    public Task Arrows_step_five_percent_home_end_jump() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Start = "a", End = "b", Width = 400, Height = 200 };
        HeadlessUi.Show(splitter);
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
    public Task Right_to_left_mirrors_the_arrows() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Start = "a", End = "b", Width = 400, Height = 200, FlowDirection = global::Avalonia.Media.FlowDirection.RightToLeft };
        HeadlessUi.Show(splitter);
        Press(splitter.Handle, Key.Left);
        Assert.Equal(55, splitter.Size);
    });

    [Fact]
    [Upstream("Splitter", "collapsible：Enter 或按钮收起那一侧（hidden），再按展开；不能收起时没有按钮")]
    public Task Enter_or_the_button_collapses_the_collapsible_side() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Collapsible = PaneSide.End, Start = "a", End = "b", Width = 400, Height = 200 };
        HeadlessUi.Show(splitter);
        var handle = splitter.Handle;

        Press(handle, Key.Enter);
        Assert.True(splitter.IsCollapsed);
        Assert.False(splitter.EndPane.IsVisible);
        Assert.Equal(100, Range(handle).Value);
        var toggle = splitter.Toggle;
        Assert.True(toggle.IsVisible);
        Assert.False(toggle.Focusable);
        Assert.Equal("展开", toggle.Peer().GetName());
        Assert.Equal(Lucide.ChevronLeft, toggle.Icon);
        toggle.Peer().GetProvider<IInvokeProvider>()!.Invoke();
        Assert.False(splitter.IsCollapsed);
        Assert.True(splitter.EndPane.IsVisible);
        Assert.Equal("收起", toggle.Peer().GetName());
        Assert.Equal(Lucide.ChevronRight, toggle.Icon);

        // 调比例时展开
        splitter.IsCollapsed = true;
        Press(handle, Key.Right);
        Assert.False(splitter.IsCollapsed);
    });

    [Fact]
    [Upstream("Splitter", "收起开始那一栏：它是 hidden、不占格子，模板只留分隔条与结束那一栏两轨")]
    public Task Collapsed_start_takes_no_space() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Collapsible = PaneSide.Start, IsCollapsed = true, Start = "a", End = "b", Width = 409, Height = 200 };
        HeadlessUi.Show(splitter);

        Assert.False(splitter.StartPane.IsVisible);
        Assert.Equal(0, splitter.Handle.Bounds.X);
        Assert.Equal(new Rect(9, 0, 400, 200), splitter.EndPane.Bounds);
        Assert.Equal(0, Range(splitter.Handle).Value);
    });

    [Fact]
    [Upstream("Splitter", "上下排列：分隔条是横向的，用 ↑/↓")]
    public Task Vertical_uses_up_and_down() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Orientation = SplitterOrientation.Vertical, Start = "a", End = "b", Width = 200, Height = 409 };
        HeadlessUi.Show(splitter);
        var handle = splitter.Handle;

        Assert.Equal(9, handle.Bounds.Height);
        Assert.Equal(200, splitter.StartPane.Bounds.Height);
        Press(handle, Key.Down);
        Assert.Equal(55, splitter.Size);
        Press(handle, Key.Right);
        Assert.Equal(55, splitter.Size);
    });

    [Fact]
    public Task Dragging_the_handle_resizes_and_double_click_restores() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Size = 40, Collapsible = PaneSide.End, IsCollapsed = true, Start = "a", End = "b", Width = 409, Height = 200 };
        var window = HeadlessUi.Show(splitter);
        splitter.IsCollapsed = false;
        HeadlessUi.Flush(window);

        var center = splitter.Handle.TranslatePoint(new Point(4, 150), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        Assert.True(splitter.Handle.IsDragging);
        // 往右整个分栏宽的 25%
        window.MouseMove(center + new Point(409 * 0.25, 0));
        Assert.Equal(65, splitter.Size);
        // 夹在 maxSize
        window.MouseMove(center + new Point(300, 0));
        Assert.Equal(80, splitter.Size);
        window.MouseUp(center + new Point(300, 0), MouseButton.Left);
        Assert.False(splitter.Handle.IsDragging);

        splitter.Size = 60;
        HeadlessUi.Flush(window);
        var at = splitter.Handle.TranslatePoint(new Point(4, 50), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(40, splitter.Size);
    });

    [Fact]
    public Task Bordered_has_a_surface_border() => HeadlessUi.Run(() =>
    {
        var splitter = new Splitter { Bordered = true, Start = "a", End = "b", Width = 400, Height = 200 };
        HeadlessUi.Show(splitter);
        var surface = (Border)splitter.GetVisualChildren().Single();
        Assert.Equal(new Thickness(1), surface.BorderThickness);
        Assert.Equal(8.4, surface.CornerRadius.TopLeft, 3);
        Assert.NotNull(surface.Background);
    });

    [Fact]
    public Task Resize_handle_sets_the_parent_width_with_keys_and_drag() => HeadlessUi.Run(() =>
    {
        var handle = new ResizeHandle { MinLength = 160, MaxLength = 360, DefaultLength = 220 };
        AutomationProperties.SetName(handle, "调整列表宽度");
        var panel = new Panel { Width = 220, Height = 100, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left, Children = { new TextBlock { Text = "列表" }, handle } };
        var changes = new List<double>();
        handle.LengthChanged += (_, length) => changes.Add(length);
        var window = HeadlessUi.Show(panel);

        // 贴着结束边、space.sm（0.5em）宽
        Assert.Equal(new Rect(214, 0, 6, 100), handle.Bounds);
        Assert.Equal(AutomationControlType.Separator, handle.Peer().GetAutomationControlType());
        Assert.Equal("调整列表宽度", handle.Peer().GetName());
        Assert.Equal((220d, 160d, 360d), (Range(handle).Value, Range(handle).Minimum, Range(handle).Maximum));

        Press(handle, Key.Right);
        Assert.Equal(236, panel.Width);
        Press(handle, Key.Left);
        Press(handle, Key.Left);
        Assert.Equal(204, panel.Width);
        Assert.Equal([236d, 220d, 204d], changes);

        HeadlessUi.Flush(window);
        var at = handle.TranslatePoint(new Point(4, 50), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseMove(at + new Point(500, 0));
        Assert.Equal(360, panel.Width);
        window.MouseMove(at + new Point(-30.4, 0));
        Assert.Equal(174, panel.Width);
        window.MouseUp(at, MouseButton.Left);
    });

    [Fact]
    public Task Resize_handle_at_the_start_edge_grows_toward_the_start() => HeadlessUi.Run(() =>
    {
        var handle = new ResizeHandle { Edge = PaneSide.Start };
        var panel = new Panel { Width = 200, Height = 100, Children = { handle } };
        HeadlessUi.Show(panel);
        Assert.Equal(0, handle.Bounds.X);
        Press(handle, Key.Left);
        Assert.Equal(216, panel.Width);
    });

    [Fact]
    public Task Horizontal_resize_handle_sets_the_height() => HeadlessUi.Run(() =>
    {
        var handle = new ResizeHandle { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
        var panel = new Panel { Width = 200, Height = 100, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top, Children = { handle } };
        HeadlessUi.Show(panel);
        Assert.Equal(new Rect(0, 94, 200, 6), handle.Bounds);
        Press(handle, Key.Down);
        Assert.Equal(116, panel.Height);
        Press(handle, Key.Right);
        Assert.Equal(116, panel.Height);
    });
}
