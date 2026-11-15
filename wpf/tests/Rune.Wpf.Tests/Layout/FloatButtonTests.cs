using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>FloatButton：单独用时靠角落、组里跟着组排、名称转给按钮、提示默认是名称、角标；速拨展开收起、Esc 收起；BackTop 滚过阈值才出现、点了回到顶部。</summary>
public class FloatButtonTests
{
    static System.Windows.Controls.Grid Host(params UIElement[] children)
    {
        var host = new System.Windows.Controls.Grid { Width = 800, Height = 600 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    static FloatButton Named(FloatButton button, string name)
    {
        AutomationProperties.SetName(button, name);
        return button;
    }

    [Fact]
    [Upstream("FloatButton", "单个与组里的按钮外观不同（单个自己固定在角落）；组是带名称的 group")]
    public Task Alone_it_sits_in_the_corner_and_in_a_group_it_follows_the_group() => WpfUi.Run(() =>
    {
        var alone = Named(new FloatButton { Icon = Lucide.MessageCircle }, "联系客服");
        var grouped = Named(new FloatButton { Icon = Lucide.MessageSquareText }, "意见反馈");
        var group = new FloatButtonGroup { Children = { grouped } };
        AutomationProperties.SetName(group, "帮助");
        WpfUi.Show(Host(alone, group));
        WpfUi.Flush();

        var em = alone.FontSize;
        Assert.Equal((HorizontalAlignment.Right, VerticalAlignment.Bottom), (alone.HorizontalAlignment, alone.VerticalAlignment));
        Assert.Equal(new Thickness(em), alone.Margin);
        Assert.Equal(new Thickness(0), grouped.Margin);
        Assert.Equal(800 - em, alone.TranslatePoint(new Point(alone.ActualWidth, 0), (UIElement)alone.Parent).X, 0);
        Assert.Equal("联系客服", alone.Button.Peer().GetName());
        Assert.Equal("联系客服", alone.Button.ToolTip);
        Assert.Equal((ButtonVariant.Surface, Radius.Full, TextSize.Lg), (alone.Button.Variant, alone.Button.Radius, alone.Button.Size));
        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal("帮助", group.Peer().GetName());
        Assert.Equal(Visibility.Collapsed, group.Trigger.Visibility);

        var clicks = 0;
        alone.Click += (_, _) => clicks++;
        Invoke(alone.Button);
        Assert.Equal(1, clicks);
        alone.ShowTip = false;
        Assert.Null(alone.Button.ToolTip);
    });

    [Fact]
    [Upstream("FloatButton", "badge={false}（没有未读）和没写角标一样：照常固定在角落")]
    public Task Badge_shows_a_count_or_a_dot() => WpfUi.Run(() =>
    {
        var button = new FloatButton { Icon = Lucide.MessageCircle, Badge = 2 };
        WpfUi.Show(Host(button));
        Assert.Equal(2, button.Indicator.Count);
        button.Badge = null;
        button.BadgeDot = true;
        Assert.True(button.Indicator.Dot);
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
    });

    [Fact]
    [Upstream("FloatButton", "速拨：主按钮 aria-expanded 跟着切换、关联展开的那一列；Esc 收起")]
    public Task Speed_dial_expands_collapses_and_escape_closes() => WpfUi.Run(() =>
    {
        var action = Named(new FloatButton { Icon = Lucide.FileText }, "新建文档");
        var dial = new FloatButtonGroup { Icon = Lucide.Plus, Children = { action } };
        AutomationProperties.SetName(dial, "新建");
        WpfUi.Show(Host(dial));
        WpfUi.Flush();

        var trigger = dial.Trigger.Button;
        Assert.Equal(Visibility.Visible, dial.Trigger.Visibility);
        Assert.Equal("新建", trigger.Peer().GetName());
        Assert.Equal("", dial.Peer().GetName());
        Assert.Equal(Visibility.Collapsed, dial.Actions.Visibility);
        var expand = (IExpandCollapseProvider)trigger.Peer().GetPattern(PatternInterface.ExpandCollapse);
        Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);

        Invoke(trigger);
        Assert.True(dial.IsOpen);
        Assert.Equal(Visibility.Visible, dial.Actions.Visibility);
        Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
        Assert.Same(Lucide.X, trigger.Icon);
        action.Button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(action.Button)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.False(dial.IsOpen);
        Assert.Equal(Visibility.Collapsed, dial.Actions.Visibility);
        WpfUi.Flush();
        Assert.True(trigger.IsKeyboardFocused);
    });

    [Fact]
    public Task Speed_dial_named_by_the_language_pack_by_default() => WpfUi.Run(() =>
    {
        var dial = new FloatButtonGroup { Icon = Lucide.Plus };
        WpfUi.Show(Host(dial));
        Assert.Equal("更多操作", dial.Trigger.Button.Peer().GetName());
    });

    [Fact]
    [Upstream("BackTop", "滚过阈值才出现；点了容器回到顶部、焦点移到容器")]
    public Task Back_top_appears_past_the_threshold_and_scrolls_back() => WpfUi.Run(() =>
    {
        var first = new System.Windows.Controls.Button { Content = "第一个" };
        var scroller = new ScrollArea { Content = new StackPanel { Children = { first, new Border { Height = 2000 } } } };
        var back = new BackTop { Target = scroller, Threshold = 100 };
        WpfUi.Show(Host(scroller, back));
        WpfUi.Flush();

        Assert.False(back.IsShown);
        Assert.Equal(0, back.Opacity);
        Assert.False(back.IsHitTestVisible);
        Assert.Equal("回到顶部", back.Button.Peer().GetName());
        scroller.ScrollToVerticalOffset(500);
        WpfUi.Flush();
        Assert.True(back.IsShown);
        Assert.True(back.IsHitTestVisible);

        Invoke(back.Button);
        WpfUi.Until(() => first.IsKeyboardFocused);
        Assert.Equal(0, scroller.VerticalOffset);
    });

    [Fact]
    public Task Back_top_in_a_group_takes_no_room_until_it_appears() => WpfUi.Run(() =>
    {
        var scroller = new ScrollArea { Content = new Border { Height = 2000 } };
        var back = new BackTop { Threshold = 100 };
        var group = new FloatButtonGroup { Children = { back } };
        WpfUi.Show(Host(scroller, group));
        WpfUi.Until(() => back.Visibility == Visibility.Collapsed);

        // 不写 Target：窗口里的第一个滚动容器
        WpfUi.Until(() =>
        {
            scroller.ScrollToVerticalOffset(500);
            WpfUi.Flush();
            return back.Visibility == Visibility.Visible;
        });
    });
}
