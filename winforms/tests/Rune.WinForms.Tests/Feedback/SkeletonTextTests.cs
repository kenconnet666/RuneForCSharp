namespace Rune.WinForms.Tests;

/// <summary>SkeletonTextControl：lines 行骨架，最后一行短一些，各行扫光依次错开；读屏里没有角色。</summary>
public class SkeletonTextTests
{
    [Upstream("SkeletonText", "lines 行骨架")]
    [Fact]
    public Task Lines_with_a_short_last_line_and_staggered_sweeps() => WinFormsUi.Run(() =>
    {
        var text = new SkeletonTextControl { Lines = 4, Delay = 100, Width = 300 };
        using var form = WinFormsUi.Show(text);
        text.Width = 300;
        text.PerformLayout();
        var lines = text.Controls.Cast<SkeletonControl>().ToList();
        var em = text.Font.SizeInPoints * text.DeviceDpi / 72f;

        Assert.Equal(4, lines.Count);
        Assert.Equal([100, 220, 340, 460], lines.Select(line => line.Delay));
        Assert.Equal((300, 180), (lines[2].Width, lines[3].Width)); // 最后一行六成宽
        Assert.Equal((int)Math.Round(1.5 * em), lines[1].Top);
        Assert.Equal(AccessibleRole.None, text.AccessibilityObject.Role);
    });
}
