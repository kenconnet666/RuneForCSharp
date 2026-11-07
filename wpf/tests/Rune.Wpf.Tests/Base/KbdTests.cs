using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Kbd：字号是外面的 0.86 倍并跟着变，其余尺寸按自己的字号；等宽字体、底边加粗；读屏直接读文字。</summary>
public class KbdTests
{
    [Upstream("Kbd", "渲染 <kbd>")]
    [Fact]
    public Task Font_is_a_mono_86_percent_of_the_parent_and_sizes_follow_it() => WpfUi.Run(() =>
    {
        var kbd = new Kbd { Content = "K" };
        var host = new StackPanel { Children = { kbd } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        TextElement.SetFontSize(host, 20);
        WpfUi.Show(host);

        Assert.False(UIElementAutomationPeer.CreatePeerForElement(kbd).IsControlElement());
        Assert.Equal(0.86 * 20, kbd.FontSize, 6);
        Assert.Equal(1.6 * 0.86 * 20, kbd.MinWidth, 6);
        Assert.Equal(new Thickness(0.25 * 0.86 * 20, 0, 0.25 * 0.86 * 20, 0), kbd.Padding);
        Assert.Equal(new Thickness(1, 1, 1, 2), kbd.BorderThickness);
        Assert.Equal(host.FindResource("Rune.Font.Mono"), kbd.FontFamily);

        // 外面的字号变了跟着变
        TextElement.SetFontSize(host, 10);
        WpfUi.Flush();
        Assert.Equal(0.86 * 10, kbd.FontSize, 6);
    });
}
