using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Toolbar：整组一个 Tab 停靠点，方向键在按钮之间移动（漫游焦点），输入框不参与。</summary>
public class ToolbarTests
{
    static bool Stops(UIElement element) => KeyboardNavigation.GetIsTabStop(element);

    /// <summary>在有焦点的元素上按一个键（KeyDown 从它冒泡上去）。</summary>
    static void Press(UIElement focused, Key key) =>
        focused.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(focused), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    static Toolbar Themed(Toolbar toolbar)
    {
        var host = new StackPanel { Children = { toolbar } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        return toolbar;
    }

    [Upstream("Toolbar", "role=")]
    [Fact]
    public Task One_tab_stop_and_arrow_keys_move_between_buttons() => WpfUi.Run(() =>
    {
        var (bold, italic, underline) = (new Button { Content = "粗" }, new Button { Content = "斜" }, new Button { Content = "下" });
        var size = new TextBox();
        var toolbar = Themed(new Toolbar { Children = { bold, italic, underline, size } });

        Assert.Equal(AutomationControlType.ToolBar, UIElementAutomationPeer.CreatePeerForElement(toolbar).GetAutomationControlType());
        Assert.Equal([true, false, false], new UIElement[] { bold, italic, underline }.Select(Stops));
        Assert.True(Stops(size)); // 输入框不参与方向键移动，照常用 Tab

        bold.Focus();
        Press(bold, Key.Right);
        Assert.True(italic.IsKeyboardFocused);
        Assert.Equal([false, true, false], new UIElement[] { bold, italic, underline }.Select(Stops)); // Tab 再进来时落在上次聚焦的项
        Press(italic, Key.End);
        Assert.True(underline.IsKeyboardFocused);
        Press(underline, Key.Right);
        Assert.True(bold.IsKeyboardFocused); // 到头从另一端继续
    });

    [Fact]
    public Task Vertical_toolbars_use_up_and_down_and_skip_disabled_items() => WpfUi.Run(() =>
    {
        var (first, off, last) = (new Button { Content = "一" }, new Button { Content = "二", IsEnabled = false }, new Button { Content = "三" });
        Themed(new Toolbar { Orientation = ToolbarOrientation.Vertical, Loop = false, Children = { first, off, last } });

        first.Focus();
        Press(first, Key.Right);
        Assert.True(first.IsKeyboardFocused); // 竖排：左右键不动
        Press(first, Key.Down);
        Assert.True(last.IsKeyboardFocused); // 跳过不可用的
        Press(last, Key.Down);
        Assert.True(last.IsKeyboardFocused); // 不循环
        Assert.Equal(first.ActualWidth, last.ActualWidth); // 竖排时拉伸
    });
}
