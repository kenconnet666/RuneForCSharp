using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Prose：竖排、块之间 space.lg、行高 relaxed、最宽约 70ch；读屏里是文档。</summary>
public class ProseTests
{
    [Fact]
    public Task Blocks_are_spaced_relaxed_and_measured() => WpfUi.Run(() =>
    {
        var (first, second) = (new Paragraph { Text = "一" }, new Paragraph { Text = "二" });
        var prose = new Prose { Children = { first, second } };
        var wide = new Prose { LimitLineLength = false, Children = { new Border { Height = 1 } } };
        var host = new StackPanel { Width = 2000, Children = { prose, wide } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        var em = TextElement.GetFontSize(prose);

        Assert.Equal(first.ActualHeight + em, second.TranslatePoint(default, prose).Y, 1); // space.lg = 1em
        Assert.Equal(1.75 * first.FontSize, first.LineHeight, 3); // lineHeights.relaxed
        var zero = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(prose), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), em, Brushes.Black, 1);
        Assert.Equal(70 * zero.WidthIncludingTrailingWhitespace, prose.ActualWidth, 1);
        Assert.Equal(2000, wide.ActualWidth);
        Assert.Equal(AutomationControlType.Document, UIElementAutomationPeer.CreatePeerForElement(prose).GetAutomationControlType());
    });
}
