using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Button = Rune.Wpf.Button;

namespace Rune.Wpf.Tests;

/// <summary>Accordion：单开再点收起、多开各自独立、不能收起时保留一项；↑ ↓ Home End 在标题之间移动；读屏的展开状态与标题层级。Collapsible：触发器切换。</summary>
public class AccordionTests
{
    static AccordionItem Item(string value) => new() { Value = value, Title = value, Content = new TextBlock { Text = value } };

    static void Click(AccordionItem item) => item.Trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    static void Show(UIElement element)
    {
        var host = new StackPanel { Children = { element } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    public Task Single_mode_opens_one_and_can_close_it() => WpfUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new Accordion { Value = "a", Children = { a, b } };
        Show(accordion);

        Assert.True(a.IsOpen);
        Click(b);
        Assert.Equal("b", accordion.Value);
        Assert.False(a.IsOpen);
        Click(b);
        Assert.Null(accordion.Value);

        accordion.Collapsible = false;
        Click(a);
        Click(a);
        Assert.Equal("a", accordion.Value);

        var peer = (IExpandCollapseProvider)a.Trigger.Peer().GetPattern(PatternInterface.ExpandCollapse);
        Assert.Equal(ExpandCollapseState.Expanded, peer.ExpandCollapseState);
        Assert.Equal(AutomationHeadingLevel.Level3, AutomationProperties.GetHeadingLevel(a.Trigger));
    });

    [Fact]
    public Task Multiple_mode_toggles_each_item() => WpfUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new Accordion { SelectionMode = SelectionMode.Multiple, Values = ["a"], Children = { a, b } };
        Show(accordion);

        Click(b);
        Assert.Equal(["a", "b"], accordion.Values);
        Click(a);
        Assert.Equal(["b"], accordion.Values);
    });

    [Fact]
    public Task Arrow_keys_move_between_triggers() => WpfUi.Run(() =>
    {
        var (a, b, c) = (Item("a"), Item("b"), Item("c"));
        Show(new Accordion { Children = { a, b, c } });
        Keyboard.Focus(a.Trigger);

        static void Press(UIElement target, Key key) =>
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        Press(a.Trigger, Key.Down);
        Assert.True(b.Trigger.IsKeyboardFocused);
        Press(b.Trigger, Key.End);
        Assert.True(c.Trigger.IsKeyboardFocused);
        Press(c.Trigger, Key.Down);
        Assert.True(a.Trigger.IsKeyboardFocused);
    });

    [Fact]
    public Task Collapsible_trigger_toggles_the_content() => WpfUi.Run(() =>
    {
        var trigger = new Button { Content = "高级筛选" };
        var collapsible = new Collapsible { Trigger = trigger, Content = new TextBlock { Text = "条件" } };
        Show(collapsible);

        trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.True(collapsible.IsOpen);

        collapsible.IsDisabled = true;
        trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.True(collapsible.IsOpen);
    });
}
