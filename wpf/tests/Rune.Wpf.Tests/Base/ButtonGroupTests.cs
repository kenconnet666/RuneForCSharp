using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Button = Rune.Wpf.Button;

namespace Rune.Wpf.Tests;

/// <summary>ButtonGroup：相邻一侧去掉圆角、边框叠一条、交叉轴拉齐；组的外观是默认值（按钮自己写的优先，没写色调时是 accent）；读屏是一个组。</summary>
public class ButtonGroupTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Children = { child } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return host;
    }

    [Fact]
    public Task Neighbours_lose_the_inner_corners_and_overlap_one_border() => WpfUi.Run(() =>
    {
        var (first, middle, last) = (new Button { Content = "上一页" }, new Button { Content = "第 3 页 · 共 12 页" }, new IconButton { Icon = Lucide.ChevronRight });
        var group = new ButtonGroup { Children = { first, middle, last } };
        WpfUi.Show(Host(group));

        Assert.True(first.CornerRadius.TopLeft > 0);
        Assert.Equal(0, first.CornerRadius.TopRight);
        Assert.Equal(0, first.CornerRadius.BottomRight);
        Assert.Equal(new CornerRadius(0), middle.CornerRadius);
        Assert.Equal(0, last.CornerRadius.TopLeft);
        Assert.True(last.CornerRadius.BottomRight > 0);
        Assert.Equal(first.ActualWidth - 1, middle.TranslatePoint(default, group).X, 3);
        Assert.Equal(middle.ActualHeight, last.ActualHeight, 3);

        group.Children.Remove(last); // 移出组：圆角照常
        WpfUi.Flush();
        Assert.Equal(AttachedCorners.None, Attached.GetCorners(last));
        Assert.True(middle.CornerRadius.BottomRight > 0); // 现在是最后一个
    });

    [Fact]
    public Task Vertical_groups_stretch_to_the_widest() => WpfUi.Run(() =>
    {
        var (pdf, markdown) = (new Button { Content = "PDF" }, new Button { Content = "导出 Markdown" });
        var group = new ButtonGroup { Orientation = AttachedOrientation.Vertical, Children = { pdf, markdown } };
        WpfUi.Show(Host(group));

        Assert.Equal(markdown.ActualWidth, pdf.ActualWidth, 3);
        Assert.Equal(pdf.ActualHeight - 1, markdown.TranslatePoint(default, group).Y, 3);
        Assert.Equal(0, pdf.CornerRadius.BottomLeft);
        Assert.True(pdf.CornerRadius.TopRight > 0);
    });

    [Fact]
    public Task Group_appearance_is_a_default_the_button_can_override() => WpfUi.Run(() =>
    {
        var plain = new Button { Content = "合并请求" };
        var icon = new IconButton { Icon = Lucide.ChevronDown };
        var own = new Button { Content = "取消", Variant = ButtonVariant.Ghost };
        var group = new ButtonGroup { Variant = ButtonVariant.Outline, Size = TextSize.Sm, Children = { plain, icon, own } };
        AutomationProperties.SetName(group, "合并");
        WpfUi.Show(Host(group));

        Assert.Equal(ButtonVariant.Outline, plain.Variant);
        Assert.Equal(ButtonVariant.Ghost, own.Variant);
        Assert.Equal(TextSize.Sm, icon.Size);
        Assert.Equal(StatusTone.Accent, icon.Tone); // 图标按钮默认中性色，在组里与旁边的按钮同色

        plain.Variant = ButtonVariant.Soft; // 之后自己写的也优先
        WpfUi.Flush();
        Assert.Equal(ButtonVariant.Soft, plain.Variant);
        group.Variant = null;
        Assert.Equal(ButtonVariant.Ghost, icon.Variant); // 组不写样式：图标按钮回到自己的 ghost
        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal("合并", group.Peer().GetName());
    });
}
