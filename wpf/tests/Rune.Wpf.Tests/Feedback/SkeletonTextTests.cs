using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>SkeletonText：lines 行骨架，最后一行短一些，各行扫光依次错开；整块对读屏隐藏。</summary>
public class SkeletonTextTests
{
    [Upstream("SkeletonText", "lines 行骨架")]
    [Fact]
    public Task Lines_with_a_short_last_line_and_staggered_sweeps() => WpfUi.Run(() =>
    {
        var text = new SkeletonText { Lines = 4, Delay = 100 };
        var single = new SkeletonText { Lines = 1 };
        var host = new StackPanel { Width = 300, Children = { text, single } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        var lines = text.Children.Cast<Skeleton>().ToList();
        var em = TextElement.GetFontSize(text);

        Assert.Equal(4, lines.Count);
        Assert.Equal([100, 220, 340, 460], lines.Select(line => line.Delay));
        Assert.Equal(300, lines[2].RenderSize.Width, 1);
        Assert.Equal(180, lines[3].RenderSize.Width, 1); // 最后一行六成宽
        Assert.Equal(1.5 * em, lines[1].TranslatePoint(default, text).Y, 1); // 1em 的行加 0.5em 间隔
        Assert.Equal(300, single.Children[0].RenderSize.Width, 1); // 只有一行时不缩短
        Assert.Null(UIElementAutomationPeer.CreatePeerForElement(text).GetChildren());
    });
}
