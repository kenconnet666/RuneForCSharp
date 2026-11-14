using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Marquee：后面接一份画出来的复制（点不到、读屏里没有），走一份长度回到开头；倒着走、竖着走；暂停不走；系统关闭动画时不复制、可以手动滚动。</summary>
public class MarqueeTests
{
    static Marquee Items(params string[] texts)
    {
        var marquee = new Marquee { Width = 200, Speed = 50, Gap = Space.Md, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var text in texts) marquee.Children.Add(new Border { Width = 60, Height = 20 });
        return marquee;
    }

    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Width = 400, Children = { child } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return host;
    }

    [Fact]
    [Upstream("Marquee", "两份内容：第二份 aria-hidden 且 inert")]
    public Task The_copy_is_drawn_and_hidden_from_input_and_screen_readers() => WpfUi.Run(() =>
    {
        var marquee = Items("a", "b", "c");
        WpfUi.Show(Host(marquee));

        Assert.Equal(3, marquee.Children.Count); // 内容只有一份
        Assert.False(marquee.Copy.IsHitTestVisible);
        Assert.Null(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(marquee.Copy)); // 读屏里没有它
        // 一份 = 三项 60 + 两个间距，再加后面一个间距（space.md）
        var gap = 0.75 * TextElement.GetFontSize(marquee);
        Assert.Equal(3 * 60 + 3 * gap, marquee.Length, 1);
    });

    [Fact]
    public void Offset_wraps_after_one_length_and_reverses()
    {
        Assert.Equal(-50, MarqueeMotion.Offset(50, 216, reverse: false));
        Assert.Equal(-10, MarqueeMotion.Offset(226, 216, reverse: false)); // 走过一份回到开头
        Assert.Equal(-216, MarqueeMotion.Offset(0, 216, reverse: true));
        Assert.Equal(-166, MarqueeMotion.Offset(50, 216, reverse: true)); // 倒着走：往 0 走
        Assert.Equal(0, MarqueeMotion.Offset(50, 0, reverse: false));
    }

    [Fact]
    [Upstream("Marquee", "方向与暂停")]
    public Task Advances_by_speed_unless_paused_and_turns_vertical() => WpfUi.Run(() =>
    {
        var marquee = Items("a", "b");
        var window = WpfUi.Show(Host(marquee));
        if (!Motion.Enabled) return; // 系统关闭动画时不走，见下一条

        // 显示时可能已经走了几帧：比前后的差
        var before = marquee.Offset;
        marquee.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(-50, marquee.Offset - before, 1);
        marquee.IsPaused = true;
        before = marquee.Offset;
        marquee.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(before, marquee.Offset);

        marquee.IsPaused = false;
        marquee.Direction = MarqueeDirection.Up;
        WpfUi.Flush();
        var gap = 0.75 * TextElement.GetFontSize(marquee);
        Assert.Equal(2 * 20 + 2 * gap, marquee.Length, 1); // 竖排：两项 20 高
    });

    [Fact]
    [Upstream("Marquee", "减少动态效果时不滚动")]
    public Task Reduced_motion_stops_and_allows_manual_scrolling() => WpfUi.Run(() =>
    {
        var marquee = Items("a", "b", "c", "d");
        WpfUi.Show(Host(marquee));
        // 跟着系统设置：关闭动画时不复制、能聚焦（手动滚动），否则照常滚动
        Assert.Equal(!Motion.Enabled, marquee.Focusable);
        Assert.Equal(Motion.Enabled, marquee.Copy.IsVisible);
        Assert.Equal(Motion.Enabled, marquee.Running);
    });
}
