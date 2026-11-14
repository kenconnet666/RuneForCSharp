namespace Rune.WinForms.Tests;

/// <summary>MarqueeControl：内容只有一份、后面画一份快照（不是子控件）；按速度往前走、暂停不走、竖着走；系统关闭动画时不复制、可以手动滚动。</summary>
public class MarqueeTests
{
    static MarqueeControl Items(int count)
    {
        var marquee = new MarqueeControl { Width = 200, Speed = 50, Gap = Space.Md };
        for (var i = 0; i < count; i++) marquee.Items.Add(new Panel { Size = new Size(60, 20) });
        return marquee;
    }

    [Fact]
    [Upstream("Marquee", "两份内容：第二份 aria-hidden 且 inert")]
    public Task The_copy_is_a_painted_snapshot_not_controls() => WinFormsUi.Run(() =>
    {
        var marquee = Items(3);
        using var form = WinFormsUi.Show(marquee);

        Assert.Single(marquee.Controls.Cast<Control>()); // 只有内容那一组，复制是画出来的
        Assert.Equal(3, marquee.Items.Count);
        Assert.Equal(AccessibleRole.Grouping, marquee.AccessibilityObject.Role);
        var gap = (int)MathF.Round(0.75f * marquee.Font.SizeInPoints * marquee.DeviceDpi / 72f);
        Assert.Equal(3 * 60 + 2 * gap + gap, marquee.Length);
        Assert.Equal(Motion.Enabled, marquee.CopyShown);
    });

    [Fact]
    [Upstream("Marquee", "方向与暂停")]
    public Task Advances_by_speed_unless_paused_and_turns_vertical() => WinFormsUi.Run(() =>
    {
        var marquee = Items(2);
        using var form = WinFormsUi.Show(marquee);
        if (!Motion.Enabled) return; // 系统关闭动画时不走，见下一条

        // 显示时可能已经走了几帧：比前后的差
        var before = marquee.Offset;
        marquee.Advance(TimeSpan.FromSeconds(1));
        Assert.InRange(marquee.Offset - before, -51, -49);
        marquee.IsPaused = true;
        before = marquee.Offset;
        marquee.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(before, marquee.Offset);

        marquee.Direction = MarqueeDirection.Up;
        marquee.PerformLayout();
        var gap = (int)MathF.Round(0.75f * marquee.Font.SizeInPoints * marquee.DeviceDpi / 72f);
        Assert.Equal(2 * 20 + 2 * gap, marquee.Length); // 竖排：两项 20 高
    });

    [Fact]
    [Upstream("Marquee", "减少动态效果时不滚动")]
    public Task Reduced_motion_stops_and_allows_manual_scrolling() => WinFormsUi.Run(() =>
    {
        var marquee = Items(4);
        using var form = WinFormsUi.Show(marquee);
        // 跟着系统设置：关闭动画时不复制、能聚焦、自动滚动条，否则照常滚动
        Assert.Equal(!Motion.Enabled, marquee.AutoScroll);
        Assert.Equal(!Motion.Enabled, marquee.TabStop);
        Assert.Equal(Motion.Enabled, marquee.CopyShown);
    });
}
