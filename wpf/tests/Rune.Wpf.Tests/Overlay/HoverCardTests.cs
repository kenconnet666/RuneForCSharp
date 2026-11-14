using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Time.Testing;

namespace Rune.Wpf.Tests;

/// <summary>HoverIntent：停留后打开、离开后可以移到浮层上、同组只开一个、刚关掉时下一个直接开；HoverCard：键盘聚焦打开但不抢焦点，Esc 关闭，点外面不关。</summary>
public class HoverCardTests
{
    static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);

    /// <summary>一个浮层：记下最后的开关，并像组件那样在开关时告诉时机。</summary>
    sealed class Probe
    {
        public HoverIntent Intent = null!;
        public bool Open;
        public OpenChangeReason? Reason;

        public static Probe In(HoverGroup group)
        {
            var probe = new Probe();
            var saved = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                probe.Intent = new HoverIntent(group, () => (700 * Ms, 300 * Ms, false), (open, reason) =>
                {
                    if (probe.Open == open) return;
                    (probe.Open, probe.Reason) = (open, reason);
                    if (open) probe.Intent.Opened();
                    else probe.Intent.Closed();
                });
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saved);
            }
            return probe;
        }
    }

    [Fact]
    public void Opens_after_the_delay_and_stays_while_moving_onto_the_panel()
    {
        var clock = new FakeTimeProvider();
        var card = Probe.In(new HoverGroup(time: clock));
        card.Intent.PointerEntered();
        clock.Advance(699 * Ms);
        Assert.False(card.Open);
        clock.Advance(Ms);
        Assert.True(card.Open);
        Assert.Equal(OpenChangeReason.Hover, card.Reason);

        card.Intent.PointerExited();
        clock.Advance(200 * Ms);
        card.Intent.PanelEntered(); // 移到浮层上：不关
        clock.Advance(1000 * Ms);
        Assert.True(card.Open);
        card.Intent.PointerExited();
        clock.Advance(300 * Ms);
        Assert.False(card.Open);
    }

    [Fact]
    public void One_at_a_time_and_the_next_opens_right_away_when_warm()
    {
        var clock = new FakeTimeProvider();
        var group = new HoverGroup(time: clock);
        var (first, second) = (Probe.In(group), Probe.In(group));
        first.Intent.PointerEntered();
        clock.Advance(700 * Ms);
        Assert.True(first.Open);

        // 同组"热"着：第二个直接开，第一个被关掉
        second.Intent.PointerEntered();
        Assert.True(second.Open);
        Assert.False(first.Open);

        second.Intent.CloseNow(OpenChangeReason.Trigger);
        clock.Advance(200 * Ms);
        first.Intent.PointerEntered();
        Assert.True(first.Open); // 刚关掉一个：不用等
        first.Intent.CloseNow(OpenChangeReason.Escape);
        clock.Advance(400 * Ms);
        second.Intent.PointerEntered();
        Assert.False(second.Open); // 过了 skipDelay：又要等
    }

    [Fact]
    [Upstream("HoverCard", "停留后打开，卡片里可以放链接；Esc 关闭")]
    public Task Keyboard_focus_opens_without_moving_focus_and_escape_closes() => WpfUi.Run(() =>
    {
        // WPF 的 Link 是行内文字：触发器是包着它的 TextBlock
        var link = new Link { Href = new Uri("https://example.com/u/ada") };
        link.Inlines.Add("@ada");
        var trigger = new TextBlock(link);
        var inside = new Button { Content = "关注" };
        var card = new HoverCard { Trigger = trigger, Content = inside };
        var host = new StackPanel { Children = { card } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(FloatingPlacement.Bottom, card.Placement);
        link.Focus();
        card.Intent.KeyboardFocused(); // 测试里分不出键盘与鼠标，直接告诉时机"是键盘聚焦"
        WpfUi.Flush();
        Assert.True(card.IsOpen);
        Assert.False(inside.IsKeyboardFocused); // 卡片只是预览：焦点不移进去

        link.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(trigger)!, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
        Assert.False(card.IsOpen);
    });
}
