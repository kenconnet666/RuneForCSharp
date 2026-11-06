using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Headless;

namespace Rune.Avalonia.Tests;

/// <summary>Toolbar：整组一个 Tab 停靠点，方向键在按钮之间移动（漫游焦点），输入框不参与。</summary>
public class ToolbarTests
{
    static bool Stops(Control control) => KeyboardNavigation.GetIsTabStop(control);

    [Upstream("Toolbar", "role=")]
    [Fact]
    public Task One_tab_stop_and_arrow_keys_move_between_buttons() => HeadlessUi.Run(() =>
    {
        var (bold, italic, underline) = (new Button { Content = "粗" }, new Button { Content = "斜" }, new Button { Content = "下" });
        var size = new TextBox();
        var toolbar = new Toolbar { Children = { bold, italic, underline, size } };
        var window = HeadlessUi.Show(toolbar);

        Assert.Equal(AutomationControlType.ToolBar, ControlAutomationPeer.CreatePeerForElement(toolbar).GetAutomationControlType());
        Assert.Equal([true, false, false], new Control[] { bold, italic, underline }.Select(Stops));
        Assert.True(Stops(size)); // 输入框不参与方向键移动，照常用 Tab

        bold.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(italic.IsFocused);
        Assert.Equal([false, true, false], new Control[] { bold, italic, underline }.Select(Stops)); // Tab 再进来时落在上次聚焦的项
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.True(underline.IsFocused);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(bold.IsFocused); // 到头从另一端继续
    });

    [Fact]
    public Task Vertical_toolbars_use_up_and_down_and_skip_disabled_items() => HeadlessUi.Run(() =>
    {
        var (first, off, last) = (new Button { Content = "一" }, new Button { Content = "二", IsEnabled = false }, new Button { Content = "三" });
        var toolbar = new Toolbar { Orientation = ToolbarOrientation.Vertical, Loop = false, Children = { first, off, last } };
        var window = HeadlessUi.Show(toolbar);

        first.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(first.IsFocused); // 竖排：左右键不动
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.True(last.IsFocused); // 跳过不可用的
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.True(last.IsFocused); // 不循环
        Assert.Equal(first.Bounds.Width, last.Bounds.Width); // 竖排时拉伸
    });
}
