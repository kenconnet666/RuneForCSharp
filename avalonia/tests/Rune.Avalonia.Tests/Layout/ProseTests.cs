using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Rune.Avalonia.Tests;

/// <summary>Prose：竖排、块之间 space.lg、行高 relaxed、最宽约 70ch；读屏里是文档。</summary>
public class ProseTests
{
    [Fact]
    public Task Blocks_are_spaced_relaxed_and_measured() => HeadlessUi.Run(() =>
    {
        var (first, second) = (new Paragraph { Text = "一" }, new Paragraph { Text = "二" });
        var prose = new Prose { Children = { first, second } };
        var wide = new Prose { LimitLineLength = false, Children = { new Border { Height = 1 } } };
        HeadlessUi.Show(new StackPanel { Width = 2000, Children = { prose, wide } });
        var em = prose.GetValue(TextElement.FontSizeProperty);

        Assert.Equal(first.Bounds.Bottom + em, second.Bounds.Y, 1); // space.lg = 1em
        Assert.Equal(1.75 * first.FontSize, first.LineHeight, 3); // lineHeights.relaxed
        using var zero = new TextLayout("0", new Typeface(prose.GetValue(TextElement.FontFamilyProperty)), em, null);
        Assert.Equal(70 * zero.WidthIncludingTrailingWhitespace, prose.Bounds.Width, 1);
        Assert.Equal(2000, wide.Bounds.Width);
        Assert.Equal(AutomationControlType.Document, ControlAutomationPeer.CreatePeerForElement(prose).GetAutomationControlType());
    });
}
