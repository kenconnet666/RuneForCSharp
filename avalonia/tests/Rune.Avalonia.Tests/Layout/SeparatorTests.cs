using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>Separator：默认装饰性；有语义时是分隔符；带文字时文字给读屏、两段线隐藏；竖线在 Flex 里随行高拉伸。</summary>
public class SeparatorTests
{
    static List<Border> Lines(Separator separator) => [.. separator.GetLogicalChildren().OfType<Border>().Where(b => b.Name == SeparatorParts.Line)];

    [Upstream("Separator", "默认装饰性")]
    [Fact]
    public Task Decorative_by_default_and_one_pixel_thick() => HeadlessUi.Run(() =>
    {
        var separator = new Separator();
        HeadlessUi.Show(new StackPanel { Width = 200, Children = { separator } });

        Assert.False(separator.Peer().IsControlElement());
        Assert.Equal((200d, 1d), (separator.Bounds.Width, separator.Bounds.Height));
    });

    [Upstream("Separator", "有语义时是 separator")]
    [Fact]
    public Task Semantic_separators_are_separators() => HeadlessUi.Run(() =>
    {
        var horizontal = new Separator { Decorative = false };
        var vertical = new Separator { Decorative = false, Orientation = SeparatorOrientation.Vertical };
        HeadlessUi.Show(new StackPanel { Children = { horizontal, vertical } });

        Assert.Equal((true, AutomationControlType.Separator), (horizontal.Peer().IsControlElement(), horizontal.Peer().GetAutomationControlType()));
        Assert.Equal(AutomationControlType.Separator, vertical.Peer().GetAutomationControlType());
    });

    [Upstream("Separator", "带文字")]
    [Fact]
    public Task Labels_sit_between_two_hidden_lines() => HeadlessUi.Run(() =>
    {
        var center = new Separator { Content = "或者" };
        var start = new Separator { Content = "更改后", LabelAlign = SeparatorLabelAlign.Start, Decorative = false };
        var end = new Separator { Content = "结束", LabelAlign = SeparatorLabelAlign.End };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { center, start, end } });

        Assert.Equal(2, Lines(center).Count);
        Assert.Single(Lines(start));
        Assert.Single(Lines(end));
        Assert.All(Lines(center), line => Assert.Equal(global::Avalonia.Automation.AccessibilityView.Raw, global::Avalonia.Automation.AutomationProperties.GetAccessibilityView(line)));
        // 文字在两段线中间；两段线一样长，和文字隔 0.75em
        var label = center.GetLogicalChildren().OfType<Text>().Single(t => t.Name == SeparatorParts.Label);
        var (left, right) = (Lines(center)[0], Lines(center)[1]);
        Assert.Equal(left.Bounds.Width, right.Bounds.Width, 1);
        var gap = 0.75 * center.GetValue(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty);
        Assert.InRange(label.Bounds.X - left.Bounds.Right, gap - 1.5, gap + 1.5); // 布局取整
        // 有语义时以文字为名称
        Assert.Equal("更改后", start.Peer().GetName());
        Assert.True(start.Peer().IsControlElement());
    });

    [Fact]
    public Task Vertical_line_stretches_with_the_row_in_a_centered_flex() => HeadlessUi.Run(() =>
    {
        var line = new Separator { Orientation = SeparatorOrientation.Vertical };
        var flex = new Flex { Align = FlexAlign.Center, Children = { new Border { Width = 20, Height = 30 }, line, new Border { Width = 20, Height = 10 } } };
        HeadlessUi.Show(new Border { Width = 200, Child = flex, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top });

        Assert.Equal((1d, 30d), (line.Bounds.Width, line.Bounds.Height));
        Assert.Equal(0, line.Bounds.Y);
    });
}
