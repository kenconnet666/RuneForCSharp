using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Tests;

/// <summary>SkeletonText：lines 行骨架，最后一行短一些，各行扫光依次错开；整块对读屏隐藏。</summary>
public class SkeletonTextTests
{
    [Fact]
    public Task Lines_with_a_short_last_line_and_staggered_sweeps() => HeadlessUi.Run(() =>
    {
        var text = new SkeletonText { Lines = 4, Delay = 100 };
        var single = new SkeletonText { Lines = 1 };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { text, single } });
        var lines = text.Children.Cast<Skeleton>().ToList();
        var em = text.GetValue(TextElement.FontSizeProperty);

        Assert.Equal(4, lines.Count);
        Assert.Equal([100, 220, 340, 460], lines.Select(line => line.Delay));
        Assert.Equal(300, lines[2].Bounds.Width, 1);
        Assert.Equal(180, lines[3].Bounds.Width, 1); // 最后一行六成宽
        Assert.Equal(1.5 * em, lines[1].Bounds.Y, 1); // 1em 的行加 0.5em 间隔
        Assert.Equal(300, single.Children[0].Bounds.Width, 1); // 只有一行时不缩短
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(text));
    });
}
