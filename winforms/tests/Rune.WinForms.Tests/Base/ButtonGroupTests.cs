namespace Rune.WinForms.Tests;

/// <summary>ButtonGroupControl：相邻一侧去掉圆角与焦点环的边距、边框叠一条、交叉轴拉齐；组的外观是默认值（按钮自己写的优先，没写色调时是 accent）；读屏是一个组。</summary>
public class ButtonGroupTests
{
    static ButtonCore Core(Control button) => ((IGroupButton)button).Core;

    [Fact]
    public Task Neighbours_lose_the_inner_corners_and_overlap_one_border() => WinFormsUi.Run(() =>
    {
        var (first, middle, last) = (new ButtonControl { Text = "上一页" }, new ButtonControl { Text = "第 3 页 · 共 12 页" }, new IconButtonControl { Icon = Lucide.ChevronRight, AccessibleName = "下一页" });
        var group = new ButtonGroupControl { AccessibleName = "翻页" };
        group.Controls.AddRange([first, middle, last]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal(AttachedCorners.TopRight | AttachedCorners.BottomRight, Core(first).Attached);
        Assert.Equal(AttachedCorners.TopLeft | AttachedCorners.TopRight | AttachedCorners.BottomRight | AttachedCorners.BottomLeft, Core(middle).Attached);
        Assert.Equal(0, first.Padding.Right); // 相邻一侧不给焦点环留位置
        Assert.True(first.Padding.Left > 0);
        var overlap = (int)Math.Round(group.DeviceDpi / 96d);
        Assert.Equal(first.Right - overlap, middle.Left);
        Assert.Equal(middle.Height, last.Height);
        Assert.Equal(AccessibleRole.Grouping, group.AccessibilityObject.Role);

        group.Controls.Remove(last); // 移出组：圆角、边距照常
        group.PerformLayout();
        Assert.Equal(AttachedCorners.None, Core(last).Attached);
        Assert.Equal(AttachedCorners.TopLeft | AttachedCorners.BottomLeft, Core(middle).Attached);
        Assert.True(last.Padding.Left > 0);
    });

    [Fact]
    public Task Vertical_groups_stretch_to_the_widest() => WinFormsUi.Run(() =>
    {
        var (pdf, markdown) = (new ButtonControl { Text = "PDF" }, new ButtonControl { Text = "导出 Markdown" });
        var group = new ButtonGroupControl { Orientation = AttachedOrientation.Vertical };
        group.Controls.AddRange([pdf, markdown]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal(markdown.Width, pdf.Width);
        Assert.True(markdown.Top < pdf.Bottom);
        Assert.Equal(AttachedCorners.BottomLeft | AttachedCorners.BottomRight, Core(pdf).Attached);
    });

    [Fact]
    public Task Group_appearance_is_a_default_the_button_can_override() => WinFormsUi.Run(() =>
    {
        var plain = new ButtonControl { Text = "合并请求" };
        var icon = new IconButtonControl { Icon = Lucide.ChevronDown, AccessibleName = "更多合并方式" };
        var own = new ButtonControl { Text = "取消", Variant = ButtonVariant.Ghost };
        var group = new ButtonGroupControl { Variant = ButtonVariant.Outline, TextSize = TextSize.Sm };
        group.Controls.AddRange([plain, icon, own]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal(ButtonVariant.Outline, plain.Variant);
        Assert.Equal(ButtonVariant.Ghost, own.Variant);
        Assert.Equal(TextSize.Sm, icon.TextSize);
        Assert.Equal(StatusTone.Accent, icon.Tone); // 图标按钮默认中性色，在组里与旁边的按钮同色
        Assert.False(plain.Element.Explicit.Variant); // 组给的不算按钮自己写的：设计器不序列化

        group.Variant = null;
        Assert.Equal(ButtonVariant.Solid, plain.Variant);
        group.Controls.Remove(icon);
        Assert.Equal(StatusTone.Neutral, icon.Tone);
    });
}
