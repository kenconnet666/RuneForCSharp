using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>H1–H6：默认字号按级别、semibold、行高 tight；读屏念级别；Size 能改字号不改级别。</summary>
public class HeadingTests
{
    [Upstream("Heading", "每个组件渲染对应的标签")]
    [Upstream("Heading", "默认字号随级别不同")]
    [Fact]
    public Task Levels_have_their_defaults() => WpfUi.Run(() =>
    {
        Heading[] levels = [new H1 { Text = "1" }, new H2 { Text = "2" }, new H3 { Text = "3" }, new H4 { Text = "4" }, new H5 { Text = "5" }, new H6 { Text = "6" }];
        var smaller = new H2 { Text = "账户设置", Size = TextSize.Lg };
        var host = new StackPanel();
        foreach (var heading in levels) host.Children.Add(heading);
        host.Children.Add(smaller);
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(new[] { "Xxxl", "Xxl", "Xl", "Lg", "Md", "Sm" }.Select(step => (double)host.FindResource($"Rune.FontSize.{step}")), levels.Select(h => h.FontSize));
        Assert.All(levels, h => Assert.Equal(FontWeights.SemiBold, h.FontWeight));
        Assert.Equal(1.25 * levels[0].FontSize, levels[0].LineHeight, 6);
        Assert.Equal(Enumerable.Range(1, 6).Select(level => AutomationHeadingLevel.Level1 + (level - 1)), levels.Select(h => UIElementAutomationPeer.CreatePeerForElement(h).GetHeadingLevel()));
        Assert.Equal(((double)host.FindResource("Rune.FontSize.Lg"), AutomationHeadingLevel.Level2), (smaller.FontSize, UIElementAutomationPeer.CreatePeerForElement(smaller).GetHeadingLevel()));
    });
}
