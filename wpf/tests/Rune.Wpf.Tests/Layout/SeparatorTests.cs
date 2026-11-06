using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Separator：默认装饰性；有语义时是分隔符（竖向带朝向）；带文字时文字给读屏、两段线隐藏；竖线在 Flex 里随行高拉伸。</summary>
public class SeparatorTests
{
    static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element);

    static List<Border> Lines(Separator separator) => [.. LogicalTreeHelper.GetChildren(separator).OfType<Border>()];

    static T Themed<T>(T root) where T : FrameworkElement
    {
        root.Resources.MergedDictionaries.Add(new RuneTheme());
        return root;
    }

    [Upstream("Separator", "默认装饰性")]
    [Fact]
    public Task Decorative_by_default_and_one_pixel_thick() => WpfUi.Run(() =>
    {
        var separator = new Separator();
        WpfUi.Show(Themed(new StackPanel { Width = 200, Children = { separator } }));

        Assert.False(Peer(separator).IsControlElement());
        Assert.Equal(new Size(200, 1), separator.RenderSize);
    });

    [Upstream("Separator", "有语义时是 separator")]
    [Fact]
    public Task Semantic_separators_are_separators_with_an_orientation() => WpfUi.Run(() =>
    {
        var horizontal = new Separator { Decorative = false };
        var vertical = new Separator { Decorative = false, Orientation = SeparatorOrientation.Vertical };
        WpfUi.Show(Themed(new StackPanel { Children = { horizontal, vertical } }));

        Assert.Equal((true, AutomationControlType.Separator), (Peer(horizontal).IsControlElement(), Peer(horizontal).GetAutomationControlType()));
        Assert.Equal(AutomationOrientation.Vertical, Peer(vertical).GetOrientation());
    });

    [Upstream("Separator", "带文字")]
    [Fact]
    public Task Labels_sit_between_two_hidden_lines() => WpfUi.Run(() =>
    {
        var center = new Separator { Content = "或者" };
        var start = new Separator { Content = "更改后", LabelAlign = SeparatorLabelAlign.Start, Decorative = false };
        var end = new Separator { Content = "结束", LabelAlign = SeparatorLabelAlign.End };
        WpfUi.Show(Themed(new StackPanel { Width = 300, Children = { center, start, end } }));

        Assert.Equal(2, Lines(center).Count);
        Assert.Single(Lines(start));
        Assert.Single(Lines(end));
        Assert.All(Lines(center), line => Assert.Null(Peer(line)));
        // 文字在两段线中间；两段线一样长，和文字隔 0.75em（文字是外面的 0.93 倍）
        var label = LogicalTreeHelper.GetChildren(center).OfType<Text>().Single();
        var (left, right) = (Lines(center)[0], Lines(center)[1]);
        Assert.Equal(left.ActualWidth, right.ActualWidth, 3);
        Assert.Equal(0.75 * center.FontSize, label.TranslatePoint(default, center).X - left.ActualWidth, 3);
        Assert.Equal(0.93 * System.Windows.Documents.TextElement.GetFontSize((StackPanel)center.Parent), center.FontSize, 3);
        Assert.Equal("更改后", Peer(start).GetName());
    });

    [Fact]
    public Task Vertical_line_stretches_with_the_row_in_a_centered_flex() => WpfUi.Run(() =>
    {
        var line = new Separator { Orientation = SeparatorOrientation.Vertical };
        var flex = new Flex { Align = FlexAlign.Center, Children = { new Border { Width = 20, Height = 30 }, line, new Border { Width = 20, Height = 10 } } };
        WpfUi.Show(Themed(new Border { Width = 200, Child = flex, VerticalAlignment = VerticalAlignment.Top }));

        Assert.Equal(new Size(1, 30), line.RenderSize);
        Assert.Equal(0, line.TranslatePoint(default, flex).Y);
    });
}
