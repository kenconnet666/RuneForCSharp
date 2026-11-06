using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>StatusDot：按色调取实色（中性是 textMuted），Color 优先；sm 8px、md 10px；写了 Label 才进读屏。</summary>
public class StatusDotTests
{
    [Fact]
    public Task Color_size_and_label() => HeadlessUi.Run(() =>
    {
        var plain = new StatusDot();
        var neutral = new StatusDot { Tone = StatusTone.Neutral, Size = StatusDotSize.Md, Label = "离线" };
        var custom = new StatusDot { Color = Brushes.Purple };
        HeadlessUi.Show(new StackPanel { Children = { plain, neutral, custom } });

        Assert.Equal(new Size(8, 8), plain.DesiredSize);
        Assert.Equal(new Size(10, 10), neutral.DesiredSize);
        var peer = ControlAutomationPeer.CreatePeerForElement(neutral);
        Assert.True(peer.IsControlElement());
        Assert.Equal("离线", peer.GetName());
        Assert.False(ControlAutomationPeer.CreatePeerForElement(plain).IsControlElement());
    });
}
