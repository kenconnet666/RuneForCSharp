using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Tooltip：平台的 ToolTip 换成 vue-tsx-pro 的外观与默认时机；位置 12 种（从右到左时 start / end 镜像）、与触发器隔开 offset；
/// 键盘聚焦立即显示、失焦与 Esc 隐藏；箭头贴着朝向触发器的边；提示文字是触发器的帮助文字。
/// </summary>
public class TooltipTests
{
    /// <summary>窗口中间的一个按钮（四周都有空间，提示不用翻转）。</summary>
    static (Window Window, Button Button) Host(object tip)
    {
        var button = new Button { Content = "保存", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(button, tip);
        return (HeadlessUi.Show(button), button);
    }

    static T Part<T>(Visual root, string name) where T : Visual => root.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    /// <summary>元素在屏幕上的矩形（与设备无关的像素）。</summary>
    static Rect Screen(Visual visual)
    {
        var origin = visual.PointToScreen(default);
        var scaling = TopLevel.GetTopLevel(visual)!.RenderScaling;
        return new Rect(origin.X / scaling, origin.Y / scaling, visual.Bounds.Width, visual.Bounds.Height);
    }

    static void Open(Button button, ToolTip tip)
    {
        ToolTip.SetIsOpen(button, true);
        HeadlessUi.Until(() => tip.IsLoaded && tip.Bounds.Width > 0);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public Task The_platform_tooltip_gets_the_inverse_panel_and_upstream_timing() => HeadlessUi.Run(() =>
    {
        var tip = new ToolTip { Content = "复制（Ctrl+C）" };
        var (_, button) = Host(tip);

        Assert.Equal((500, 300), (ToolTip.GetShowDelay(button), ToolTip.GetBetweenShowDelay(button)));
        Open(button, tip);
        var panel = Part<Border>(tip, TooltipParts.Panel);
        Assert.Equal(button.ThemeColor("Rune.Brush.InverseSurface"), Lookup.ColorOf(panel.Background));
        Assert.Equal(button.ThemeColor("Rune.Brush.InverseText"), Lookup.ColorOf(tip.Foreground));
        var em = button.Resource<double>("Rune.FontSize.Sm");
        Assert.Equal(em, tip.FontSize);
        Assert.Equal(new Thickness(0.5 * em, 0.25 * em), panel.Padding);
        Assert.Equal(20 * em, panel.MaxWidth);
        Assert.Equal(button.Resource<double>("Rune.Radius.Sm") * em, panel.CornerRadius.TopLeft, 3);
        Assert.False(Part<Border>(tip, TooltipParts.Arrow).IsVisible);
    });

    [Fact]
    public Task Placements_map_to_the_platform_and_keep_the_offset() => HeadlessUi.Run(() =>
    {
        var tip = new ToolTip { Content = "说明" };
        var (_, button) = Host(tip);

        Open(button, tip);
        Assert.Equal(PlacementMode.Top, ToolTip.GetPlacement(button));
        var panel = Screen(Part<Border>(tip, TooltipParts.Panel));
        var target = Screen(button);
        Assert.Equal(6, target.Top - panel.Bottom, 1);
        Assert.Equal(target.Center.X, panel.Center.X, 1);
        ToolTip.SetIsOpen(button, false);

        Tooltip.SetPlacement(button, FloatingPlacement.RightStart);
        Tooltip.SetOffset(button, 10);
        Open(button, tip);
        Assert.Equal(PlacementMode.RightEdgeAlignedTop, ToolTip.GetPlacement(button));
        panel = Screen(Part<Border>(tip, TooltipParts.Panel));
        Assert.Equal(10, panel.Left - target.Right, 1);
        Assert.Equal(target.Top, panel.Top, 1);
        ToolTip.SetIsOpen(button, false);

        // 从右到左排版：上下两边的 start 是右边
        button.FlowDirection = FlowDirection.RightToLeft;
        Tooltip.SetPlacement(button, FloatingPlacement.TopStart);
        Open(button, tip);
        Assert.Equal(PlacementMode.TopEdgeAlignedRight, ToolTip.GetPlacement(button));
    });

    [Fact]
    public Task The_arrow_sits_on_the_edge_facing_the_trigger() => HeadlessUi.Run(() =>
    {
        var tip = new ToolTip { Content = "placement=\"bottom\"" };
        var (_, button) = Host(tip);
        Tooltip.SetPlacement(button, FloatingPlacement.Bottom);
        Tooltip.SetArrow(button, true);

        Open(button, tip);
        var arrow = Part<Border>(tip, TooltipParts.Arrow);
        var size = arrow.Width;
        Assert.True(arrow.IsVisible);
        Assert.Equal(0.625 * tip.FontSize, size, 1.0); // 布局取整到整像素
        var panel = Screen(Part<Border>(tip, TooltipParts.Panel));
        var target = Screen(button);
        // 距离加上箭头伸出的一段；箭头跨在面板的上边上、对准按钮中心
        Assert.Equal(6 + 0.7 * size, panel.Top - target.Bottom, 1.0);
        Assert.Equal(-size / 2, Canvas.GetTop(arrow), 0.01);
        Assert.Equal(target.Center.X, panel.X + Canvas.GetLeft(arrow) + size / 2, 1);
    });

    [Fact]
    public Task Keyboard_focus_shows_it_and_escape_or_blur_hides_it() => HeadlessUi.Run(() =>
    {
        var before = new Button { Content = "前一个" };
        var button = new Button { Content = "保存" };
        var after = new Button { Content = "后一个" };
        ToolTip.SetTip(button, "保存草稿");
        var window = HeadlessUi.Show(new StackPanel { Children = { before, button, after } });

        // 指针点出来的焦点不显示（同 :focus-visible）
        button.Focus(NavigationMethod.Pointer);
        HeadlessUi.Flush(window);
        Assert.False(ToolTip.GetIsOpen(button));

        before.Focus(NavigationMethod.Tab);
        button.Focus(NavigationMethod.Tab);
        Assert.True(ToolTip.GetIsOpen(button));

        // headless 的按键送进窗口时焦点会被清掉（弹出窗口开着时），这里不核对 Esc 之后的焦点
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(ToolTip.GetIsOpen(button));

        before.Focus(NavigationMethod.Tab);
        button.Focus(NavigationMethod.Tab);
        Assert.True(ToolTip.GetIsOpen(button));
        after.Focus(NavigationMethod.Tab);
        Assert.False(ToolTip.GetIsOpen(button));

        // 不显示提示时键盘聚焦也不显示
        ToolTip.SetServiceEnabled(button, false);
        before.Focus(NavigationMethod.Tab);
        button.Focus(NavigationMethod.Tab);
        Assert.False(ToolTip.GetIsOpen(button));
    });

    [Fact]
    public Task The_tip_text_describes_the_trigger() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "保存" };
        ToolTip.SetTip(button, "保存草稿");
        Assert.Equal("保存草稿", button.Peer().GetHelpText());

        // 文字加按键提示：逐段连起来
        ToolTip.SetTip(button, new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "加粗" }, new Kbd { Content = "Ctrl" } } });
        Assert.Equal("加粗 Ctrl", button.Peer().GetHelpText());

        // 使用方写的帮助文字优先
        AutomationProperties.SetHelpText(button, "自定义");
        ToolTip.SetTip(button, "保存草稿");
        Assert.Equal("自定义", button.Peer().GetHelpText());
    });
}
