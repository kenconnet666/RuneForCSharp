using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ProportionBar：横条是一张图，名称带上各段的数量；数量为 0 的段不画但留在图例里。</summary>
public class ProportionBarTests
{
    [Fact]
    [Upstream("ProportionBar", "横条的名称带上各段的数量；数量为 0 的段不画但留在图例里")]
    public Task Bar_names_each_count_and_zero_segments_stay_in_the_legend() => HeadlessUi.Run(() =>
    {
        var bar = new ProportionBar
        {
            Label = "工单池",
            Segments = [new("a", "已确认", 25, StatusTone.Success), new("b", "暂缓", 0), new("c", "待定", 35)],
            ValueText = segment => $"{segment.Value} 项",
        };
        HeadlessUi.Show(new StackPanel { Width = 400, Children = { bar } });

        var peer = bar.Bar.Peer();
        Assert.Equal((AutomationControlType.Image, "工单池：已确认 25，暂缓 0，待定 35"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal(["已确认25 项", "暂缓0 项", "待定35 项"], bar.LegendItems.Select(item => string.Concat(item.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text))));
        // 没写色调的按顺序取：第 2 段 info、第 3 段 success
        Assert.Equal([StatusTone.Success, StatusTone.Info, StatusTone.Success], bar.LegendItems.Select(item => item.GetVisualDescendants().OfType<StatusDot>().Single().Tone));
        Assert.Equal(12, bar.Bar.Bounds.Height); // md：0.75rem
    });
}
