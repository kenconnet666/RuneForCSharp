using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Ellipsis：没截断时是普通文字；截断时能聚焦、有提示（Expandable 时换成展开按钮），展开后显示全部。</summary>
public class EllipsisTests
{
    const string Long = "杭州远山精密机械有限公司（萧山二厂）";

    [Fact]
    [Upstream("Ellipsis", "没截断：普通文字，不能聚焦、没有提示")]
    public Task Short_text_is_plain() => HeadlessUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = "短名称", Width = 300 };
        var window = HeadlessUi.Show(ellipsis);
        HeadlessUi.Flush(window);

        Assert.False(ellipsis.IsTruncated);
        Assert.False(ellipsis.TextPart.Focusable);
        Assert.Null(ToolTip.GetTip(ellipsis.TextPart));
        Assert.False(ellipsis.Toggle.IsVisible);
    });

    [Fact]
    [Upstream("Ellipsis", "截断：文字能聚焦、经 aria-describedby 关联全文的提示")]
    public Task Truncated_text_is_focusable_with_the_full_text() => HeadlessUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = Long, Width = 80 };
        var window = HeadlessUi.Show(ellipsis);
        HeadlessUi.Until(() => ellipsis.IsTruncated);
        HeadlessUi.Flush(window);

        Assert.True(ellipsis.TextPart.Focusable);
        Assert.True(KeyboardNavigation.GetIsTabStop(ellipsis.TextPart));
        Assert.Equal(Long, ToolTip.GetTip(ellipsis.TextPart));
        Assert.Equal(Long, AutomationProperties.GetHelpText(ellipsis.TextPart));
        // 放宽以后不再截断
        ellipsis.Width = 800;
        HeadlessUi.Until(() => !ellipsis.IsTruncated);
        HeadlessUi.Flush(window);
        Assert.False(ellipsis.TextPart.Focusable);
    });

    [Fact]
    [Upstream("Ellipsis", "expandable：截断时后面是“展开”（不再给提示），展开后是“收起”")]
    public Task Expandable_has_a_toggle_instead_of_a_tooltip() => HeadlessUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = string.Concat(Enumerable.Repeat(Long, 6)), Lines = 3, Expandable = true, Width = 200 };
        var window = HeadlessUi.Show(ellipsis);
        HeadlessUi.Until(() => ellipsis.IsTruncated);
        HeadlessUi.Flush(window);

        Assert.False(ellipsis.TextPart.Focusable);
        Assert.Null(ToolTip.GetTip(ellipsis.TextPart));
        var toggle = ellipsis.Toggle;
        Assert.True(toggle.IsVisible);
        Assert.Equal("展开", toggle.Content);
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)toggle.Peer()).ExpandCollapseState);
        ((IInvokeProvider)toggle.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.True(ellipsis.IsExpanded);
        Assert.Equal("收起", toggle.Content);
        Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)toggle.Peer()).ExpandCollapseState);
        Assert.Null(ellipsis.TextPart.LineClamp);
    });

    [Fact]
    public Task Tooltip_can_be_replaced_or_turned_off() => HeadlessUi.Run(() =>
    {
        var replaced = new Ellipsis { Text = Long, Width = 80, Tooltip = "全称见合同" };
        var off = new Ellipsis { Text = Long, Width = 80, Tooltip = null };
        var window = HeadlessUi.Show(new StackPanel { Children = { replaced, off } });
        HeadlessUi.Until(() => replaced.IsTruncated && off.IsTruncated);
        HeadlessUi.Flush(window);

        Assert.Equal("全称见合同", ToolTip.GetTip(replaced.TextPart));
        Assert.Null(ToolTip.GetTip(off.TextPart));
        Assert.False(off.TextPart.Focusable);
    });
}
