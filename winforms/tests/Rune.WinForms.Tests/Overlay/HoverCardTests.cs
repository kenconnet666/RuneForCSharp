using System.Reflection;
using Microsoft.Extensions.Time.Testing;

namespace Rune.WinForms.Tests;

/// <summary>HoverIntent：停留后打开、离开后可以移到浮层上、同组只开一个、刚关掉时下一个直接开；HoverCardControl：键盘聚焦打开，卡片窗口不激活（焦点留在触发器上），Esc 关闭。</summary>
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
    public Task Keyboard_focus_opens_without_activating_the_card_and_escape_closes() => WinFormsUi.Run(() =>
    {
        var trigger = new ButtonControl { Text = "@ada" };
        var inside = new ButtonControl { Text = "关注" };
        var card = new HoverCardControl { Trigger = trigger, Content = inside };
        using var form = WinFormsUi.Show(card);

        Assert.Equal(FloatingPlacement.Bottom, card.Placement);
        trigger.Focus(); // 鼠标键没按着：算键盘聚焦
        WinFormsUi.Pump();
        Assert.True(card.IsOpen);
        Assert.False(inside.Focused); // 卡片只是预览：焦点不移进去

        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(trigger, [new KeyEventArgs(Keys.Escape)]);
        Assert.False(card.IsOpen);
    });
}
