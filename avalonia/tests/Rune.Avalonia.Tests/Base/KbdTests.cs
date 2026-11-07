using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Kbd：字号是外面的 0.86 倍并跟着变，其余尺寸按自己的字号；等宽字体、底边加粗。</summary>
public class KbdTests
{
    static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer) => peer.GetChildren().SelectMany(child => Descendants(child).Prepend(child));

    [Upstream("Kbd", "渲染 <kbd>")]
    [Fact]
    public Task Font_is_a_mono_86_percent_of_the_parent_and_sizes_follow_it() => HeadlessUi.Run(() =>
    {
        var kbd = new Kbd { Content = "K" };
        var host = new StackPanel { Children = { kbd } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        var window = HeadlessUi.Show(host);

        // 读屏直接读到按键的文字（Kbd 本身不在控件视图里，同 Badge）
        var peer = ControlAutomationPeer.CreatePeerForElement(kbd);
        Assert.False(peer.IsControlElement());
        Assert.Contains(Descendants(peer), child => child.GetName() == "K");
        Assert.Equal(0.86 * 20, kbd.FontSize, 6);
        Assert.Equal(1.6 * 0.86 * 20, kbd.MinWidth, 6);
        Assert.Equal(0.25 * 0.86 * 20, kbd.Padding.Left, 6);
        Assert.Equal(new Thickness(1, 1, 1, 2), kbd.BorderThickness);
        Application.Current!.TryGetResource("Rune.Font.Mono", ThemeVariant.Light, out var mono);
        Assert.Equal(mono, kbd.FontFamily);

        // 外面的字号变了跟着变
        host.SetValue(TextElement.FontSizeProperty, 10d);
        HeadlessUi.Flush(window);
        Assert.Equal(0.86 * 10, kbd.FontSize, 6);
    });
}
