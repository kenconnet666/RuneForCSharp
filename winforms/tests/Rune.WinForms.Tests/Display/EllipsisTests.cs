namespace Rune.WinForms.Tests;

/// <summary>EllipsisControl：没截断时是普通文字；截断时能聚焦、有提示（Expandable 时换成展开按钮），展开后显示全部。</summary>
public class EllipsisTests
{
    const string Long = "杭州远山精密机械有限公司（萧山二厂）";

    /// <summary>定宽的 Ellipsis（自动大小时就是文字的自然宽度，不会截断）。</summary>
    static EllipsisControl Sized(EllipsisControl ellipsis, int width)
    {
        ellipsis.AutoSize = false;
        ellipsis.Size = new Size(width, ellipsis.Lines * 40);
        return ellipsis;
    }

    [Fact]
    [Upstream("Ellipsis", "没截断：普通文字，不能聚焦、没有提示")]
    public Task Short_text_is_plain() => WinFormsUi.Run(() =>
    {
        var ellipsis = Sized(new EllipsisControl { Text = "短名称" }, 300);
        using var form = WinFormsUi.Show(ellipsis);
        WinFormsUi.Pump();

        Assert.False(ellipsis.IsTruncated);
        Assert.False(ellipsis.TextPart.TabStop);
        Assert.Null(ellipsis.ShownTooltip);
        Assert.False(ellipsis.Toggle.Visible);
    });

    [Fact]
    [Upstream("Ellipsis", "截断：文字能聚焦、经 aria-describedby 关联全文的提示")]
    public Task Truncated_text_is_focusable_with_the_full_text() => WinFormsUi.Run(() =>
    {
        var ellipsis = Sized(new EllipsisControl { Text = Long }, 80);
        using var form = WinFormsUi.Show(ellipsis);
        WinFormsUi.Until(() => ellipsis.IsTruncated);

        Assert.True(ellipsis.TextPart.TabStop);
        Assert.Equal(Long, ellipsis.ShownTooltip);
        Assert.Equal(Long, ellipsis.TextPart.AccessibleDescription);
        // 放宽以后不再截断
        ellipsis.Width = 800;
        WinFormsUi.Until(() => !ellipsis.IsTruncated);
        Assert.False(ellipsis.TextPart.TabStop);
    });

    [Fact]
    [Upstream("Ellipsis", "expandable：截断时后面是“展开”（不再给提示），展开后是“收起”")]
    public Task Expandable_has_a_toggle_instead_of_a_tooltip() => WinFormsUi.Run(() =>
    {
        var ellipsis = Sized(new EllipsisControl { Text = string.Concat(Enumerable.Repeat(Long, 6)), Lines = 3, Expandable = true }, 200);
        using var form = WinFormsUi.Show(ellipsis);
        WinFormsUi.Until(() => ellipsis.IsTruncated);

        Assert.False(ellipsis.TextPart.TabStop);
        Assert.Null(ellipsis.ShownTooltip);
        var toggle = ellipsis.Toggle;
        Assert.True(toggle.Visible);
        Assert.Equal("展开", toggle.Text);
        Assert.Equal(AccessibleRole.PushButton, toggle.AccessibilityObject.Role);
        Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
        toggle.AccessibilityObject.DoDefaultAction();
        WinFormsUi.Pump();
        Assert.True(ellipsis.IsExpanded);
        Assert.Equal("收起", toggle.Text);
        Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        Assert.Null(ellipsis.TextPart.LineClamp);
    });

    [Fact]
    public Task Tooltip_can_be_replaced_or_turned_off() => WinFormsUi.Run(() =>
    {
        var replaced = Sized(new EllipsisControl { Text = Long, Tooltip = "全称见合同" }, 80);
        var off = Sized(new EllipsisControl { Text = Long, Tooltip = null }, 80);
        using var form = WinFormsUi.Show(replaced, off);
        WinFormsUi.Until(() => replaced.IsTruncated && off.IsTruncated);

        Assert.Equal("全称见合同", replaced.ShownTooltip);
        Assert.Null(off.ShownTooltip);
        Assert.False(off.TextPart.TabStop);
    });
}
