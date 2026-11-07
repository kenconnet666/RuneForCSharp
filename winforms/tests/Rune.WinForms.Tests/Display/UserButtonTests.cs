namespace Rune.WinForms.Tests;

/// <summary>UserButtonControl：头像 + 名字 + 箭头；有说明时多一行、占满宽度、换成上下双箭头；Compact 只剩头像；不要箭头。</summary>
public class UserButtonTests
{
    static int MeasuredWidth(UserButtonControl button, int available) =>
        button.Element.Measure(ElementContext.Of(button), new Size(available, int.MaxValue)).Width;

    [Upstream("UserButton", "头像、名字、箭头；名称来自看得见的名字")]
    [Fact]
    public Task Avatar_name_and_indicator_and_the_name_is_the_accessible_name() => WinFormsUi.Run(() =>
    {
        var button = new UserButtonControl { DisplayName = "林小满" };
        using var form = WinFormsUi.Show(button);

        Assert.Equal("林小满", button.Element.NamePart.Content);
        Assert.Equal("林小满", button.Element.AvatarPart.DisplayName);
        Assert.True(button.Element.IndicatorShown);
        Assert.Equal(Lucide.ChevronDown, button.Element.IndicatorPart.Data);
        Assert.Equal((AccessibleRole.PushButton, "林小满"), (button.AccessibilityObject.Role, button.AccessibilityObject.Name));
        Assert.True(MeasuredWidth(button, 300) < 300); // 胶囊：按内容的宽
        Assert.Equal(Radius.Full, button.Radius);
    });

    [Upstream("UserButton", "有说明时多一行；compact 只剩头像，名字成了 aria-label")]
    [Fact]
    public Task A_description_spans_the_row_and_compact_keeps_only_the_avatar() => WinFormsUi.Run(() =>
    {
        var full = new UserButtonControl { DisplayName = "林小满", Description = "采购部 · 管理员" };
        var compact = new UserButtonControl { DisplayName = "周一航", Compact = true };
        using var form = WinFormsUi.Show(full, compact);

        Assert.Equal(Lucide.ChevronsUpDown, full.Element.IndicatorPart.Data);
        Assert.Equal(300, MeasuredWidth(full, 300)); // 有说明：占满宽度
        Assert.Equal(Radius.Md, full.Radius);
        Assert.Equal("采购部 · 管理员", full.AccessibilityObject.Description);

        Assert.False(compact.Element.IndicatorShown);
        Assert.Equal("周一航", compact.AccessibilityObject.Name);
        Assert.Null(compact.AccessibilityObject.Description);
        // 只剩头像：四周 space.xs
        var em = compact.Font.SizeInPoints * compact.DeviceDpi / 72f;
        var avatar = compact.Element.AvatarPart.Measure(ElementContext.Of(compact), new Size(int.MaxValue, int.MaxValue)).Width;
        Assert.InRange(MeasuredWidth(compact, int.MaxValue) - avatar, 2 * 0.25f * em, 2 * 0.25f * em + 4);
    });

    [Upstream("UserButton", "indicator={false} 不要箭头；部件属性合并到对应的节点")]
    [Fact]
    public Task ShowIndicator_false_hides_the_arrow_and_part_props_merge() => WinFormsUi.Run(() =>
    {
        var none = new UserButtonControl { DisplayName = "林小满", ShowIndicator = false };
        var custom = new UserButtonControl { DisplayName = "林小满", Indicator = Lucide.Settings, PropsName = new TextProps { Tone = TextTone.Accent } };
        using var form = WinFormsUi.Show(none, custom);

        Assert.False(none.Element.IndicatorShown);
        Assert.Equal(Lucide.Settings, custom.Element.IndicatorPart.Data);
        Assert.Equal((TextTone.Accent, TextWeight.Medium), (custom.Element.NamePart.Tone, custom.Element.NamePart.Weight)); // 合并：字重仍是组件给的
    });
}
