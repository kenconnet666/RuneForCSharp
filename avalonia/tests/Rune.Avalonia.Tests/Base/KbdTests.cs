using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Kbd：字号是外面的 0.85 倍并跟着变，其余尺寸按自己的字号；等宽字体、底边加粗。</summary>
public class KbdTests
{
    static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer) => peer.GetChildren().SelectMany(child => Descendants(child).Prepend(child));

    [Upstream("Kbd", "渲染 <kbd>")]
    [Fact]
    public Task Font_is_a_mono_85_percent_of_the_parent_and_sizes_follow_it() => HeadlessUi.Run(() =>
    {
        var kbd = new Kbd { Content = "K" };
        var host = new StackPanel { Children = { kbd } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        var window = HeadlessUi.Show(host);

        // 读屏直接读到按键的文字（Kbd 本身不在控件视图里，同 Badge）
        var peer = ControlAutomationPeer.CreatePeerForElement(kbd);
        Assert.False(peer.IsControlElement());
        Assert.Contains(Descendants(peer), child => child.GetName() == "K");
        Assert.Equal(17, kbd.FontSize);
        Assert.Equal(1.6 * 17, kbd.MinWidth);
        Assert.Equal(new Thickness(0.25 * 17, 0), kbd.Padding);
        Assert.Equal(new Thickness(1, 1, 1, 2), kbd.BorderThickness);
        Application.Current!.TryGetResource("Rune.Font.Mono", ThemeVariant.Light, out var mono);
        Assert.Equal(mono, kbd.FontFamily);

        // 外面的字号变了跟着变
        host.SetValue(TextElement.FontSizeProperty, 10d);
        HeadlessUi.Flush(window);
        Assert.Equal(8.5, kbd.FontSize);
    });
}
