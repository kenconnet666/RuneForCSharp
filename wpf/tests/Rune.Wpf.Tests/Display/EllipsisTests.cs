using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Ellipsis：没截断时是普通文字；截断时能聚焦、有提示（Expandable 时换成展开按钮），展开后显示全部。</summary>
public class EllipsisTests
{
    const string Long = "杭州远山精密机械有限公司（萧山二厂）";

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static IExpandCollapseProvider Expander(UIElement toggle) => (IExpandCollapseProvider)toggle.Peer().GetPattern(PatternInterface.ExpandCollapse);

    [Fact]
    [Upstream("Ellipsis", "没截断：普通文字，不能聚焦、没有提示")]
    public Task Short_text_is_plain() => WpfUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = "短名称", Width = 300 };
        WpfUi.Show(Host(ellipsis));
        WpfUi.Flush();

        Assert.False(ellipsis.IsTruncated);
        Assert.False(ellipsis.TextPart.Focusable);
        Assert.Null(ellipsis.TextPart.ToolTip);
        Assert.Equal(Visibility.Collapsed, ellipsis.Toggle.Visibility);
    });

    [Fact]
    [Upstream("Ellipsis", "截断：文字能聚焦、经 aria-describedby 关联全文的提示")]
    public Task Truncated_text_is_focusable_with_the_full_text() => WpfUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = Long, Width = 80 };
        WpfUi.Show(Host(ellipsis));
        WpfUi.Until(() => ellipsis.IsTruncated);
        WpfUi.Flush();

        Assert.True(ellipsis.TextPart.Focusable);
        Assert.True(KeyboardNavigation.GetIsTabStop(ellipsis.TextPart));
        Assert.Equal(Long, ellipsis.TextPart.ToolTip);
        Assert.Equal(Long, AutomationProperties.GetHelpText(ellipsis.TextPart));
        // 放宽以后不再截断
        ellipsis.Width = 800;
        WpfUi.Until(() => !ellipsis.IsTruncated);
        WpfUi.Flush();
        Assert.False(ellipsis.TextPart.Focusable);
    });

    [Fact]
    [Upstream("Ellipsis", "expandable：截断时后面是“展开”（不再给提示），展开后是“收起”")]
    public Task Expandable_has_a_toggle_instead_of_a_tooltip() => WpfUi.Run(() =>
    {
        var ellipsis = new Ellipsis { Text = string.Concat(Enumerable.Repeat(Long, 6)), Lines = 3, Expandable = true, Width = 200 };
        WpfUi.Show(Host(ellipsis));
        WpfUi.Until(() => ellipsis.IsTruncated);
        WpfUi.Flush();

        Assert.False(ellipsis.TextPart.Focusable);
        Assert.Null(ellipsis.TextPart.ToolTip);
        var toggle = ellipsis.Toggle;
        Assert.Equal(Visibility.Visible, toggle.Visibility);
        Assert.Equal("展开", ((TextBlock)toggle.Content).Text);
        Assert.Equal(ExpandCollapseState.Collapsed, Expander(toggle).ExpandCollapseState);
        ((IInvokeProvider)toggle.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.True(ellipsis.IsExpanded);
        Assert.Equal("收起", ((TextBlock)toggle.Content).Text);
        Assert.Equal(ExpandCollapseState.Expanded, Expander(toggle).ExpandCollapseState);
        Assert.Null(ellipsis.TextPart.LineClamp);
    });

    [Fact]
    public Task Tooltip_can_be_replaced_or_turned_off() => WpfUi.Run(() =>
    {
        var replaced = new Ellipsis { Text = Long, Width = 80, Tooltip = "全称见合同" };
        var off = new Ellipsis { Text = Long, Width = 80, Tooltip = null };
        WpfUi.Show(Host(replaced, off));
        WpfUi.Until(() => replaced.IsTruncated && off.IsTruncated);
        WpfUi.Flush();

        Assert.Equal("全称见合同", replaced.TextPart.ToolTip);
        Assert.Null(off.TextPart.ToolTip);
        Assert.False(off.TextPart.Focusable);
    });
}
