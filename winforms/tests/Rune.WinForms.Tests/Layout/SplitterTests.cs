using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>SplitterControl：分隔条的读屏取值、键盘、收起；两栏按比例分；拖动；ResizeHandleControl 改父控件的宽度。</summary>
public class SplitterTests
{
    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    static void Mouse(Control control, string handler, int x, int y, int clicks = 1) =>
        typeof(Control).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new MouseEventArgs(MouseButtons.Left, clicks, x, y, 0)]);

    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        host.Controls.AddRange(children);
        return host;
    }

    [Fact]
    [Upstream("Splitter", "分隔条是可聚焦的 separator，带当前比例与范围；两栏按比例分")]
    public Task Divider_is_a_focusable_separator_and_panes_follow_the_ratio() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Ratio = 30, Start = new Label { Text = "列表" }, End = new Label { Text = "预览" }, Height = 200 };
        using var form = WinFormsUi.Show(Host(splitter));
        var track = splitter.TrackThickness;
        splitter.Width = 400 + track;

        var divider = splitter.Divider;
        Assert.True(divider.TabStop);
        Assert.Equal(AccessibleRole.Separator, divider.AccessibilityObject.Role);
        Assert.Equal("调整分栏", divider.AccessibilityObject.Name);
        Assert.Equal("30", divider.AccessibilityObject.Value);
        Assert.Equal(new Rectangle(0, 0, 120, 200), splitter.StartPane.Bounds);
        Assert.Equal(new Rectangle(120, 0, track, 200), divider.Bounds);
        Assert.Equal(new Rectangle(120 + track, 0, 280, 200), splitter.EndPane.Bounds);
        Assert.False(splitter.Toggle.Visible);
    });

    [Fact]
    [Upstream("Splitter", "←/→ 每次 5%，Home / End 到最小、最大，写回 size")]
    public Task Arrows_step_five_percent_home_end_jump() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Width = 400, Height = 200 };
        var changes = new List<double>();
        splitter.RatioChanged += (_, _) => changes.Add(splitter.Ratio);
        using var form = WinFormsUi.Show(Host(splitter));
        var divider = splitter.Divider;

        Press(divider, Keys.Right);
        Assert.Equal(55, splitter.Ratio);
        Press(divider, Keys.Left);
        Press(divider, Keys.Left);
        Assert.Equal(45, splitter.Ratio);
        Press(divider, Keys.Home);
        Assert.Equal(20, splitter.Ratio);
        Press(divider, Keys.End);
        Assert.Equal(80, splitter.Ratio);
        // 上下键在左右并排时不调
        Press(divider, Keys.Down);
        Assert.Equal([55d, 50d, 45d, 20d, 80d], changes);
    });

    [Fact]
    public Task Right_to_left_puts_the_start_pane_on_the_right_and_mirrors_the_arrows() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Ratio = 30, Height = 200, RightToLeft = RightToLeft.Yes };
        using var form = WinFormsUi.Show(Host(splitter));
        splitter.Width = 400 + splitter.TrackThickness;

        Assert.Equal(400 + splitter.TrackThickness - 120, splitter.StartPane.Left);
        Assert.Equal(0, splitter.EndPane.Left);
        Press(splitter.Divider, Keys.Left);
        Assert.Equal(35, splitter.Ratio);
    });

    [Fact]
    [Upstream("Splitter", "collapsible：Enter 或按钮收起那一侧（hidden），再按展开；不能收起时没有按钮")]
    public Task Enter_or_the_button_collapses_the_collapsible_side() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Collapsible = PaneSide.End, Width = 400, Height = 200 };
        using var form = WinFormsUi.Show(Host(splitter));
        var divider = splitter.Divider;

        Press(divider, Keys.Enter);
        Assert.True(splitter.IsCollapsed);
        Assert.False(splitter.EndPane.Visible);
        Assert.Equal("100", divider.AccessibilityObject.Value);
        var toggle = splitter.Toggle;
        Assert.True(toggle.Visible);
        Assert.False(toggle.TabStop);
        Assert.Equal("展开", toggle.AccessibleName);
        Assert.Equal(Lucide.ChevronLeft, toggle.Icon);
        toggle.PerformClick();
        Assert.False(splitter.IsCollapsed);
        Assert.True(splitter.EndPane.Visible);
        Assert.Equal("收起", toggle.AccessibleName);
        Assert.Equal(Lucide.ChevronRight, toggle.Icon);
        // 收起按钮叠在分隔条中间、最上面
        Assert.InRange(toggle.Left + toggle.Width / 2 - (divider.Left + divider.Width / 2), -1, 1);
        Assert.Equal(0, splitter.Controls.GetChildIndex(toggle));

        // 调比例时展开
        splitter.IsCollapsed = true;
        Press(divider, Keys.Right);
        Assert.False(splitter.IsCollapsed);
    });

    [Fact]
    [Upstream("Splitter", "收起开始那一栏：它是 hidden、不占格子，模板只留分隔条与结束那一栏两轨")]
    public Task Collapsed_start_takes_no_space() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Collapsible = PaneSide.Start, IsCollapsed = true, Height = 200 };
        using var form = WinFormsUi.Show(Host(splitter));
        var track = splitter.TrackThickness;
        splitter.Width = 400 + track;

        Assert.False(splitter.StartPane.Visible);
        Assert.Equal(0, splitter.Divider.Left);
        Assert.Equal(new Rectangle(track, 0, 400, 200), splitter.EndPane.Bounds);
        Assert.Equal("0", splitter.Divider.AccessibilityObject.Value);
    });

    [Fact]
    [Upstream("Splitter", "上下排列：分隔条是横向的，用 ↑/↓")]
    public Task Vertical_uses_up_and_down() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Orientation = SplitterOrientation.Vertical, Width = 200 };
        using var form = WinFormsUi.Show(Host(splitter));
        var track = splitter.TrackThickness;
        splitter.Height = 400 + track;

        Assert.Equal(track, splitter.Divider.Height);
        Assert.Equal(200, splitter.StartPane.Height);
        Press(splitter.Divider, Keys.Down);
        Assert.Equal(55, splitter.Ratio);
        Press(splitter.Divider, Keys.Right);
        Assert.Equal(55, splitter.Ratio);
    });

    [Fact]
    public Task Dragging_the_divider_resizes_and_double_click_restores() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Ratio = 40, Width = 400, Height = 200 };
        using var form = WinFormsUi.Show(Host(splitter));
        var divider = splitter.Divider;

        Mouse(divider, "OnMouseDown", 2, 100);
        Assert.True(divider.IsDragging);
        // 往右整个分栏宽的 25%（分隔条随拖动移动：按它当时的位置换算）
        var before = divider.Left;
        Mouse(divider, "OnMouseMove", 2 + 100, 100);
        Assert.Equal(65, splitter.Ratio);
        Mouse(divider, "OnMouseMove", 2 + 100 - (divider.Left - before) + 200, 100);
        Assert.Equal(80, splitter.Ratio);
        Mouse(divider, "OnMouseUp", 2, 100);
        Assert.False(divider.IsDragging);

        Mouse(divider, "OnMouseDown", 2, 100, clicks: 2);
        Assert.Equal(40, splitter.Ratio);
    });

    [Fact]
    public Task Bordered_insets_the_panes_by_the_border() => WinFormsUi.Run(() =>
    {
        var splitter = new SplitterControl { Bordered = true, Width = 402, Height = 202 };
        using var form = WinFormsUi.Show(Host(splitter));
        Assert.Equal(1, splitter.StartPane.Left);
        Assert.Equal(1, splitter.StartPane.Top);
        Assert.Equal(200, splitter.EndPane.Height);
        WinFormsUi.Paint(splitter);
    });

    [Fact]
    public Task Resize_handle_sets_the_parent_width_with_keys_and_drag() => WinFormsUi.Run(() =>
    {
        var handle = new ResizeHandleControl { AccessibleName = "调整列表宽度", MinLength = 160, MaxLength = 360, DefaultLength = 220 };
        var panel = new Panel { Width = 220, Height = 100 };
        panel.Controls.Add(new Label { Text = "列表" });
        panel.Controls.Add(handle);
        var changes = new List<int>();
        handle.LengthChanged += (_, length) => changes.Add(length);
        using var form = WinFormsUi.Show(Host(panel));

        // 贴着结束边、在最上面
        Assert.Equal(220, handle.Right);
        Assert.Equal(100, handle.Height);
        Assert.Equal(0, panel.Controls.GetChildIndex(handle));
        Assert.Equal(AccessibleRole.Separator, handle.AccessibilityObject.Role);
        Assert.Equal("调整列表宽度", handle.AccessibilityObject.Name);
        Assert.Equal("220", handle.AccessibilityObject.Value);

        Press(handle, Keys.Right);
        Assert.Equal(236, panel.Width);
        Assert.Equal(236, handle.Right);
        Press(handle, Keys.Left);
        Press(handle, Keys.Left);
        Assert.Equal(204, panel.Width);
        Assert.Equal([236, 220, 204], changes);

        Mouse(handle, "OnMouseDown", 2, 50);
        // 手柄随父控件变宽移动：按屏幕坐标换算
        Mouse(handle, "OnMouseMove", 2 + 500, 50);
        Assert.Equal(360, panel.Width);
        Mouse(handle, "OnMouseMove", 2 - 30 - (360 - 204), 50);
        Assert.Equal(174, panel.Width);
        Mouse(handle, "OnMouseUp", 2, 50);
        Mouse(handle, "OnMouseDown", 2, 50, clicks: 2);
        Assert.Equal(220, panel.Width);
    });

    [Fact]
    public Task Resize_handle_at_the_start_edge_grows_toward_the_start() => WinFormsUi.Run(() =>
    {
        var handle = new ResizeHandleControl { Edge = PaneSide.Start };
        var panel = new Panel { Width = 200, Height = 100 };
        panel.Controls.Add(handle);
        using var form = WinFormsUi.Show(Host(panel));
        Assert.Equal(0, handle.Left);
        Press(handle, Keys.Left);
        Assert.Equal(216, panel.Width);
    });

    [Fact]
    public Task Horizontal_resize_handle_sets_the_height() => WinFormsUi.Run(() =>
    {
        var handle = new ResizeHandleControl { Orientation = Orientation.Horizontal };
        var panel = new Panel { Width = 200, Height = 100 };
        panel.Controls.Add(handle);
        using var form = WinFormsUi.Show(Host(panel));
        Assert.Equal((0, 100, 200), (handle.Left, handle.Bottom, handle.Width));
        Press(handle, Keys.Down);
        Assert.Equal(116, panel.Height);
        Assert.Equal(116, handle.Bottom);
        Press(handle, Keys.Right);
        Assert.Equal(116, panel.Height);
    });
}
