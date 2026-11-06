using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Flex：CSS flexbox 的排法（间距、伸缩、分布、对齐、换行、反向）。各项是定宽高的方块，结果按像素对。</summary>
public class FlexTests
{
    static Border Box(double width, double height) => new() { Width = width, Height = height };

    /// <summary>放进一个定宽（高）、带主题的宿主。</summary>
    static Flex Show(Flex flex, double width = 300, double height = double.NaN)
    {
        var host = new Border { Width = width, Height = height, Child = flex, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        return flex;
    }

    static Rect At(FrameworkElement child) => new(child.TranslatePoint(default, (UIElement)VisualTreeHelper.GetParent(child)), child.RenderSize);

    static Flex With(Flex flex, params UIElement[] children)
    {
        foreach (var child in children) flex.Children.Add(child);
        return flex;
    }

    [Upstream("Flex", "div，未声明的属性落到元素上")]
    [Fact]
    public Task Row_with_gap_in_em_stretches_items_across() => WpfUi.Run(() =>
    {
        var (a, b) = (new Border { Width = 40 }, Box(60, 30));
        var flex = Show(With(new Flex { Gap = Space.Sm }, a, b));
        var em = TextElement.GetFontSize(flex);

        Assert.Equal(new Rect(0, 0, 40, 30), At(a)); // stretch：没写高度的项跟着行高
        Assert.Equal(new Rect(40 + 0.5 * em, 0, 60, 30), At(b));
        Assert.Equal(300, flex.ActualWidth); // 块级：撑满宽度
    });

    [Upstream("Flex", "grow：在父级 flex 里占满剩余空间并允许收缩")]
    [Fact]
    public Task Grow_takes_the_rest_and_can_shrink_below_its_content() => WpfUi.Run(() =>
    {
        var icon = Box(20, 20);
        var text = new TextBlock { Text = "很长很长很长很长很长很长很长很长很长很长的一段说明文字", TextTrimming = TextTrimming.CharacterEllipsis };
        Flex.SetGrow(text, true);
        var end = Box(30, 20);
        Show(With(new Flex(), icon, text, end), width: 200);

        Assert.Equal(150, At(text).Width, 3); // 200 - 20 - 30，比内容窄
        Assert.Equal(170, At(end).X, 3);

        var spacer = new Border();
        Flex.SetGrow(spacer, true);
        var last = Box(30, 20);
        Show(With(new Flex(), Box(20, 20), spacer, last), width: 200);
        Assert.Equal(170, At(last).X, 3); // 伸展：把最后一项推到尾部
    });

    [Fact]
    public Task Items_shrink_by_their_content_width() => WpfUi.Run(() =>
    {
        // 两段文字放不下：按内容宽度加权收缩（CSS flex-shrink: 1），换行后变高
        var (a, b) = (new TextBlock { Text = "甲乙丙丁戊己庚辛壬癸", TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = "子丑寅卯", TextWrapping = TextWrapping.Wrap });
        a.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        b.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (wa, wb, line) = (a.DesiredSize.Width, b.DesiredSize.Width, a.DesiredSize.Height);
        var width = (wa + wb) * 0.8;
        Show(With(new Flex { Align = FlexAlign.Start }, a, b), width: width);

        Assert.Equal(wa / (wa + wb), At(a).Width / (At(a).Width + At(b).Width), 2);
        Assert.Equal(width, At(a).Width + At(b).Width, 1);
        Assert.True(At(a).Height > line); // 缩窄后换了行，行高跟着变
    });

    [Fact]
    public Task Justify_and_align_place_items_on_both_axes() => WpfUi.Run(() =>
    {
        var (a, b, c) = (Box(20, 10), Box(20, 30), Box(20, 20));
        Show(With(new Flex { Justify = FlexJustify.Between, Align = FlexAlign.Center }, a, b, c), width: 200);
        Assert.Equal(new Rect(0, 10, 20, 10), At(a));
        Assert.Equal(new Rect(90, 0, 20, 30), At(b));
        Assert.Equal(new Rect(180, 5, 20, 20), At(c));

        var (d, e) = (Box(20, 10), Box(20, 30));
        Show(With(new Flex { Justify = FlexJustify.Evenly, Align = FlexAlign.End }, d, e), width: 100);
        Assert.Equal(new Rect(20, 20, 20, 10), At(d));
        Assert.Equal(new Rect(60, 0, 20, 30), At(e));

        var (f, g) = (Box(20, 10), Box(20, 10));
        Show(With(new Flex { Justify = FlexJustify.Around }, f, g), width: 100);
        Assert.Equal((15d, 65d), (At(f).X, At(g).X));
    });

    [Fact]
    public Task Wrap_breaks_lines_and_reverse_flips_them() => WpfUi.Run(() =>
    {
        var boxes = Enumerable.Range(0, 5).Select(_ => Box(40, 20)).ToArray();
        var flex = Show(With(new Flex { Wrap = true, Gap = Space.None, RowGap = Space.Sm }, boxes), width: 130);
        var em = TextElement.GetFontSize(flex);

        Assert.Equal([0, 40, 80, 0, 40], boxes.Select(box => At(box).X));
        Assert.Equal(20 + 0.5 * em, At(boxes[3]).Y, 3);

        var reversed = Enumerable.Range(0, 3).Select(_ => Box(40, 20)).ToArray();
        Show(With(new Flex { Wrap = FlexWrap.Reverse }, reversed), width: 90);
        Assert.Equal((20d, 0d), (At(reversed[0]).Y, At(reversed[2]).Y)); // 新的一行在上面
    });

    [Fact]
    public Task Column_and_reverse_directions() => WpfUi.Run(() =>
    {
        var (a, b) = (Box(20, 10), Box(30, 20));
        Show(With(new Flex { Direction = FlexDirection.Column, Align = FlexAlign.Start }, a, b));
        Assert.Equal(new Rect(0, 0, 20, 10), At(a));
        Assert.Equal(new Rect(0, 10, 30, 20), At(b));

        var (c, d) = (Box(20, 10), Box(30, 10));
        Show(With(new Flex { Direction = FlexDirection.RowReverse }, c, d), width: 100);
        Assert.Equal((80d, 50d), (At(c).X, At(d).X)); // 从结束边排起
    });

    [Fact]
    public Task Baseline_aligns_the_first_line_of_text() => WpfUi.Run(() =>
    {
        var big = new TextBlock { Text = "128", FontSize = 30 };
        var small = new TextBlock { Text = "件", FontSize = 12 };
        Show(With(new Flex { Align = FlexAlign.Baseline }, big, small));

        double Baseline(TextBlock text) => At(text).Y + text.FontFamily.Baseline * text.FontSize;
        Assert.Equal(Baseline(big), Baseline(small), 3);
    });

    [Upstream("Flex", "FlexList 是 ul")]
    [Fact]
    public Task Flex_lists_are_lists_and_inline_flex_hugs_its_content() => WpfUi.Run(() =>
    {
        var list = With(new FlexList(), Box(10, 10));
        var inline = With(new Flex { Inline = true }, Box(40, 10), Box(40, 10));
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(list);
        host.Children.Add(inline);
        WpfUi.Show(host);

        Assert.Equal(AutomationControlType.List, UIElementAutomationPeer.CreatePeerForElement(list).GetAutomationControlType());
        Assert.Equal(AutomationControlType.List, UIElementAutomationPeer.CreatePeerForElement(new FlexOrderedList()).GetAutomationControlType());
        Assert.Equal(80, inline.ActualWidth);
    });
}
