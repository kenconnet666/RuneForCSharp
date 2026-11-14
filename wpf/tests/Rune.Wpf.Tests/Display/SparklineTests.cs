using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Sparkline：读屏的概括、空值处断开、柱从 0 起、最后一个值的标记。</summary>
public class SparklineTests
{
    static void Show(Sparkline sparkline)
    {
        var host = new StackPanel { Width = 200, Children = { sparkline } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("Sparkline", "role=img，名称是 label 加上概括（几个值、从哪到哪、最低最高）")]
    public Task Screen_readers_get_a_summary_sentence() => WpfUi.Run(() =>
    {
        var sparkline = new Sparkline { Data = [40, 70, 360, 150], Label = "近 4 个月" };
        Show(sparkline);

        var peer = UIElementAutomationPeer.CreatePeerForElement(sparkline);
        Assert.Equal((AutomationControlType.Image, "近 4 个月：4 个值，从 40 到 150，最低 40，最高 360"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal(2 * sparkline.FontSize, sparkline.ActualHeight, 3); // 高度默认 2em
    });

    [Fact]
    [Upstream("Sparkline", "空值处断开成两段；默认标出最后一个点")]
    public Task Gaps_split_the_line_and_the_last_point_is_marked() => WpfUi.Run(() =>
    {
        var geometry = SparklineGeometry.Of([1, 2, null, 4, 5], SparklineType.Line);

        Assert.Equal(2, geometry.Segments.Count);
        Assert.Equal((4.0, 0.0), geometry.Last); // 最后一个值是最大值：在最上面
        Assert.Equal(4, geometry.Width);
        Assert.True(new Sparkline().ShowLast);
    });

    [Fact]
    [Upstream("Sparkline", "柱：每个值一根（空值是空位），最新的一根与前面的样式不同、不画圆点；Decimal 按原值念")]
    public Task Bars_start_at_zero_and_leave_gaps() => WpfUi.Run(() =>
    {
        var sparkline = new Sparkline { Data = [1.5m, null, 3], Type = SparklineType.Bar, ValueFormat = new NumericFormat { Suffix = "万" } };
        Show(sparkline);

        var geometry = sparkline.Geometry;
        Assert.Equal([0, 2], geometry.Bars.Select(bar => bar.Index));
        Assert.All(geometry.Bars, bar => Assert.Equal(100, bar.Bottom)); // 从 0 起
        Assert.Equal(3, geometry.Width); // 柱每个占一格，空值也占
        Assert.Equal(2, geometry.LastIndex);
        Assert.Contains("从 1.5 万", sparkline.Summary);
    });

    [Fact]
    [Upstream("Sparkline", "没有值时念“—”，什么也不画")]
    public Task No_values_read_a_dash_and_draw_nothing() => WpfUi.Run(() =>
    {
        var sparkline = new Sparkline { Data = [null, null] };
        Show(sparkline);

        Assert.Equal("—", sparkline.Summary);
        Assert.Empty(sparkline.Geometry.Segments);
        Assert.Null(sparkline.Geometry.Last);
    });
}
