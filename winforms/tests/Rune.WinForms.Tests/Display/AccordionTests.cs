using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>AccordionControl：单开再点收起、多开各自独立、不能收起时保留一项；↑ ↓ Home End 在标题之间移动；读屏的展开状态。CollapsibleControl：触发器切换。</summary>
public class AccordionTests
{
    static AccordionItemControl Item(string value) => new() { Value = value, Title = value, Content = new TextControl { Text = value } };

    static void Click(AccordionItemControl item) => item.Trigger.PerformClickForTest();

    [Fact]
    public Task Single_mode_opens_one_and_can_close_it() => WinFormsUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new AccordionControl { Value = "a", Width = 400 };
        accordion.Controls.AddRange([a, b]);
        using var form = WinFormsUi.Show(accordion);
        var changes = 0;
        accordion.ValueChanged += (_, _) => changes++;

        Assert.True(a.IsOpen);
        Click(b);
        Assert.Equal("b", accordion.Value);
        Assert.False(a.IsOpen);
        Click(b);
        Assert.Null(accordion.Value);
        Assert.Equal(2, changes);

        accordion.Collapsible = false;
        Click(a);
        Click(a);
        Assert.Equal("a", accordion.Value);
        Assert.True(a.Trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        Assert.True(b.Trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
    });

    [Fact]
    public Task Multiple_mode_toggles_each_item() => WinFormsUi.Run(() =>
    {
        var (a, b) = (Item("a"), Item("b"));
        var accordion = new AccordionControl { SelectionMode = SelectionMode.Multiple, Values = ["a"] };
        accordion.Controls.AddRange([a, b]);
        using var form = WinFormsUi.Show(accordion);

        Click(b);
        Assert.Equal(["a", "b"], accordion.Values);
        Click(a);
        Assert.Equal(["b"], accordion.Values);
    });

    [Fact]
    public Task Arrow_keys_move_between_triggers() => WinFormsUi.Run(() =>
    {
        var (a, b, c) = (Item("a"), Item("b"), Item("c"));
        var accordion = new AccordionControl();
        accordion.Controls.AddRange([a, b, c]);
        using var form = WinFormsUi.Show(accordion);

        void Press(Control target, Keys key) =>
            typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, [new KeyEventArgs(key)]);
        form.ActiveControl = a.Trigger;
        Press(a.Trigger, Keys.Down);
        Assert.Same(b.Trigger, form.ActiveControl);
        Press(b.Trigger, Keys.End);
        Assert.Same(c.Trigger, form.ActiveControl);
        Press(c.Trigger, Keys.Down);
        Assert.Same(a.Trigger, form.ActiveControl);
    });

    [Fact]
    public Task Collapsible_trigger_toggles_the_content() => WinFormsUi.Run(() =>
    {
        var trigger = new ButtonControl { Text = "高级筛选" };
        var collapsible = new CollapsibleControl { Trigger = trigger, Content = new TextControl { Text = "条件" } };
        using var form = WinFormsUi.Show(collapsible);

        trigger.PerformClick();
        Assert.True(collapsible.IsOpen);
        collapsible.IsDisabled = true;
        Assert.False(trigger.Enabled);
    });
}
