namespace Rune.WinForms.Tests;

/// <summary>ProportionBarControl：横条是一张图，名称带上各段的数量；数量为 0 的段不画但留在图例里。</summary>
public class ProportionBarTests
{
    [Fact]
    [Upstream("ProportionBar", "横条的名称带上各段的数量；数量为 0 的段不画但留在图例里")]
    public Task Bar_names_each_count_and_zero_segments_stay_in_the_legend() => WinFormsUi.Run(() =>
    {
        var bar = new ProportionBarControl
        {
            Label = "工单池", Dock = DockStyle.Top,
            Segments = [new("a", "已确认", 25, StatusTone.Success), new("b", "暂缓", 0), new("c", "待定", 35)],
            ValueText = segment => $"{segment.Value} 项",
        };
        using var form = WinFormsUi.Show(bar);

        var image = bar.AccessibilityObject.GetChild(0)!;
        Assert.Equal((AccessibleRole.Graphic, "工单池：已确认 25，暂缓 0，待定 35"), (image.Role, image.Name));
        Assert.Equal(["已确认25 项", "暂缓0 项", "待定35 项"], bar.LegendTexts);
        Assert.Equal(4, bar.AccessibilityObject.GetChildCount()); // 横条 + 三项图例
        // 没写色调的按顺序取：第 2 段 info、第 3 段 success
        Assert.Equal([StatusTone.Success, StatusTone.Info, StatusTone.Success], bar.LegendTones);
        Assert.Equal((int)MathF.Round(12 * bar.DeviceDpi / 96f), bar.BarBounds.Height); // md：0.75rem
    });
}
