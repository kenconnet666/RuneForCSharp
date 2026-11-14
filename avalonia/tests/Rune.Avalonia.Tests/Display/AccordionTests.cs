using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>Accordion：单开再点收起、多开各自独立、不能收起时保留一项；↑ ↓ Home End 在标题之间移动；读屏的展开状态与标题层级。Collapsible：触发器切换。</summary>
public class AccordionTests
{
    static AccordionItem Item(string value) => new() { Value = value, Title = value, Content = new TextBlock { Text = value } };

    static void Click(AccordionItem item) => item.Trigger.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));

    [Fact]
    public Task Single_mode_opens_one_and_can_close_it() => HeadlessUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new Accordion { Value = "a", Children = { a, b } };
        HeadlessUi.Show(accordion);

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

        var peer = (IExpandCollapseProvider)a.Trigger.Peer();
        Assert.Equal(ExpandCollapseState.Expanded, peer.ExpandCollapseState);
        Assert.Equal(3, AutomationProperties.GetHeadingLevel(a.Trigger));
    });

    [Fact]
    public Task Multiple_mode_toggles_each_item() => HeadlessUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new Accordion { SelectionMode = SelectionMode.Multiple, Values = ["a"], Children = { a, b } };
        HeadlessUi.Show(accordion);

        Click(b);
        Assert.Equal(["a", "b"], accordion.Values);
        Click(a);
        Assert.Equal(["b"], accordion.Values);
    });

    [Fact]
    public Task Arrow_keys_move_between_triggers() => HeadlessUi.Run(() =>
    {
        var (a, b, c) = (Item("a"), Item("b"), Item("c"));
        HeadlessUi.Show(new Accordion { Children = { a, b, c } });
        a.Trigger.Focus();

        void Press(Key key) => FocusManagerOf(a).GetFocusedElement()!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Press(Key.Down);
        Assert.True(b.Trigger.IsFocused);
        Press(Key.End);
        Assert.True(c.Trigger.IsFocused);
        Press(Key.Down);
        Assert.True(a.Trigger.IsFocused);
    });

    static IFocusManager FocusManagerOf(Control control) => TopLevel.GetTopLevel(control)!.FocusManager!;

    [Fact]
    public Task Collapsible_trigger_toggles_the_content() => HeadlessUi.Run(() =>
    {
        var trigger = new Button { Content = "高级筛选" };
        var collapsible = new Collapsible { Trigger = trigger, Content = new TextBlock { Text = "条件" } };
        HeadlessUi.Show(collapsible);

        trigger.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        Assert.True(collapsible.IsOpen);
        Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)trigger.Peer()).ExpandCollapseState);

        collapsible.IsDisabled = true;
        trigger.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        Assert.True(collapsible.IsOpen);
    });
}
