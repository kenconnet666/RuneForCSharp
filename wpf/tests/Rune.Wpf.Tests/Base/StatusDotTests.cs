using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>StatusDot：按色调取实色（中性是 textMuted），Color 优先；sm 8px、md 10px；写了 Label 才进读屏。</summary>
public class StatusDotTests
{
    [Fact]
    public Task Color_size_and_label() => WpfUi.Run(() =>
    {
        var plain = new StatusDot();
        var neutral = new StatusDot { Tone = StatusTone.Neutral, Size = StatusDotSize.Md, Label = "离线" };
        var custom = new StatusDot { Color = Brushes.Purple };
        var host = new StackPanel { Children = { plain, neutral, custom } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(new Size(8, 8), plain.DesiredSize);
        Assert.Equal(new Size(10, 10), neutral.DesiredSize);
        var peer = UIElementAutomationPeer.CreatePeerForElement(neutral);
        Assert.True(peer.IsControlElement());
        Assert.Equal("离线", peer.GetName());
        Assert.False(UIElementAutomationPeer.CreatePeerForElement(plain).IsControlElement());
    });
}
