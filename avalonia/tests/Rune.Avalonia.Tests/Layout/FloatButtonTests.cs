using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Rune.Avalonia.Tests;

/// <summary>FloatButton：单独用时靠角落、组里跟着组排、名称转给按钮、提示默认是名称、角标；速拨展开收起、Esc 收起；BackTop 滚过阈值才出现、点了回到顶部。</summary>
public class FloatButtonTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    [Fact]
    [Upstream("FloatButton", "单个与组里的按钮外观不同（单个自己固定在角落）；组是带名称的 group")]
    [Upstream("FloatButton", "组停在示例框的右下角；滚下去后多出回到顶部")]
    public Task Alone_it_sits_in_the_corner_and_in_a_group_it_follows_the_group() => HeadlessUi.Run(() =>
    {
        var alone = new FloatButton { Icon = Lucide.MessageCircle };
        AutomationProperties.SetName(alone, "联系客服");
        var grouped = new FloatButton { Icon = Lucide.MessageSquareText };
        AutomationProperties.SetName(grouped, "意见反馈");
        var group = new FloatButtonGroup { Children = { grouped } };
        AutomationProperties.SetName(group, "帮助");
        var window = HeadlessUi.Show(new Panel { Children = { alone, group } });

        Assert.Equal((HorizontalAlignment.Right, VerticalAlignment.Bottom), (alone.HorizontalAlignment, alone.VerticalAlignment));
        Assert.Equal(new Thickness(12), alone.Margin);
        Assert.Equal(new Thickness(0), grouped.Margin);
        Assert.Equal(800 - 12, alone.Bounds.Right, 0);
        Assert.Equal(600 - 12, alone.Bounds.Bottom, 0);
        Assert.Equal("联系客服", alone.Button.Peer().GetName());
        Assert.Equal("联系客服", ToolTip.GetTip(alone.Button));
        Assert.Equal((ButtonVariant.Surface, Radius.Full, TextSize.Lg), (alone.Button.Variant, alone.Button.Radius, alone.Button.Size));
        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal("帮助", group.Peer().GetName());
        Assert.False(group.Trigger.IsVisible);

        var clicks = 0;
        alone.Click += (_, _) => clicks++;
        Invoke(alone.Button);
        Assert.Equal(1, clicks);
        alone.ShowTip = false;
        Assert.Null(ToolTip.GetTip(alone.Button));
        HeadlessUi.Flush(window);
    });

    [Fact]
    [Upstream("FloatButton", "badge={false}（没有未读）和没写角标一样：照常固定在角落")]
    public Task Badge_shows_a_count_or_a_dot() => HeadlessUi.Run(() =>
    {
        var button = new FloatButton { Icon = Lucide.MessageCircle, Badge = 2 };
        HeadlessUi.Show(button);
        Assert.Equal(2, button.Indicator.Count);
        button.Badge = null;
        button.BadgeDot = true;
        Assert.True(button.Indicator.Dot);
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
    });

    [Fact]
    [Upstream("FloatButton", "速拨：主按钮 aria-expanded 跟着切换、关联展开的那一列；Esc 收起")]
    [Upstream("FloatButton", "速拨：点主按钮展开，选一项后收起")]
    public Task Speed_dial_expands_collapses_and_escape_closes() => HeadlessUi.Run(() =>
    {
        var action = new FloatButton { Icon = Lucide.FileText };
        AutomationProperties.SetName(action, "新建文档");
        var dial = new FloatButtonGroup { Icon = Lucide.Plus, Children = { action } };
        AutomationProperties.SetName(dial, "新建");
        var window = HeadlessUi.Show(new Panel { Children = { dial } });

        var trigger = dial.Trigger.Button;
        Assert.True(dial.Trigger.IsVisible);
        Assert.Equal("新建", trigger.Peer().GetName());
        Assert.True(string.IsNullOrEmpty(dial.Peer().GetName()));
        Assert.False(dial.Actions.IsVisible);
        var expand = (IExpandCollapseProvider)trigger.Peer();
        Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);

        Invoke(trigger);
        Assert.True(dial.IsOpen);
        Assert.True(dial.Actions.IsVisible);
        Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
        Assert.Same(Lucide.X, trigger.Icon);
        Press(action.Button, Key.Escape);
        Assert.False(dial.IsOpen);
        Assert.False(dial.Actions.IsVisible);
        HeadlessUi.Flush(window);
        Assert.True(trigger.IsFocused);
    });

    [Fact]
    public Task Speed_dial_named_by_the_language_pack_by_default() => HeadlessUi.Run(() =>
    {
        var dial = new FloatButtonGroup { Icon = Lucide.Plus };
        HeadlessUi.Show(new Panel { Children = { dial } });
        Assert.Equal("更多操作", dial.Trigger.Button.Peer().GetName());
    });

    [Fact]
    [Upstream("BackTop", "滚过阈值才出现；点了容器回到顶部、焦点移到容器")]
    [Upstream("FloatButton", "组停在示例框的右下角；滚下去后多出回到顶部")]
    public Task Back_top_appears_past_the_threshold_and_scrolls_back() => HeadlessUi.Run(() =>
    {
        var first = new global::Avalonia.Controls.Button { Content = "第一个" };
        var content = new StackPanel { Children = { first, new Border { Height = 2000 } } };
        var scroller = new ScrollArea { Height = 300, Content = content };
        var back = new BackTop { Target = scroller, Threshold = 100 };
        var window = HeadlessUi.Show(new Panel { Children = { scroller, back } });
        HeadlessUi.Flush(window);

        Assert.False(back.IsShown);
        Assert.Equal(0, back.Opacity);
        Assert.False(back.IsHitTestVisible);
        Assert.Equal("回到顶部", back.Button.Peer().GetName());
        scroller.Offset = new Vector(0, 500);
        HeadlessUi.Flush(window);
        Assert.True(back.IsShown, $"offset {scroller.Offset} extent {scroller.Extent} viewport {scroller.Viewport}");
        Assert.True(back.IsHitTestVisible);

        Invoke(back.Button);
        HeadlessUi.Until(() => first.IsFocused);
        // 动画结束后停在顶部，不跳回去
        Assert.Equal(0, scroller.Offset.Y);
    });

    [Fact]
    public Task Back_top_in_a_group_takes_no_room_until_it_appears() => HeadlessUi.Run(() =>
    {
        var scroller = new ScrollArea { Height = 300, Content = new Border { Height = 2000 } };
        var back = new BackTop { Threshold = 100 };
        var group = new FloatButtonGroup { Children = { back } };
        var window = HeadlessUi.Show(new Panel { Children = { scroller, group } });
        HeadlessUi.Until(() => !back.IsVisible);

        // 不写 Target：窗口里的第一个滚动容器
        scroller.Offset = new Vector(0, 500);
        HeadlessUi.Flush(window);
        Assert.True(back.IsVisible);
    });
}
