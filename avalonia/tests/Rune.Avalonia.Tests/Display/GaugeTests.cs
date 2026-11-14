using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>Gauge：读屏按只读的范围值念（取值夹在范围里、取值文字带目标）；外圈按区间分段；数值弧取所在区间的色调。</summary>
public class GaugeTests
{
    [Fact]
    [Upstream("Gauge", "meter：取值范围、当前值（夹在范围里）、格式化的数值与目标")]
    public Task Reads_as_a_range_with_the_formatted_value_and_target() => HeadlessUi.Run(() =>
    {
        var gauge = new Gauge { Value = 0.82m, Max = 1, ValueFormat = new NumericFormat { Style = NumericStyle.Percent }, Target = 0.9m, Label = "完成率" };
        HeadlessUi.Show(new StackPanel { Children = { gauge } });

        var peer = gauge.Peer();
        var range = (IRangeValueProvider)peer;
        Assert.Equal((AutomationControlType.ProgressBar, "完成率"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal((0d, 1d, 0.82), (range.Minimum, range.Maximum, range.Value));
        Assert.Equal("82%，目标 90%", ((IValueProvider)peer).Value);
        Assert.Equal("82%", gauge.ShownValue);
        Assert.Equal(10 * gauge.FontSize, gauge.Bounds.Width, 3); // 宽 10em
    });

    [Fact]
    [Upstream("Gauge", "外圈按区间分段；超出范围的值画满、念原值")]
    public Task Bands_split_the_ring_and_out_of_range_values_fill_the_arc() => HeadlessUi.Run(() =>
    {
        var gauge = new Gauge { Value = 120, Unit = "分", Bands = [new(60, StatusTone.Danger), new(100, StatusTone.Success)] };
        HeadlessUi.Show(new StackPanel { Children = { gauge } });

        Assert.Equal(2, GaugeGeometry.Bands(gauge.Bands, 0, 100).Count());
        Assert.Equal(1, GaugeGeometry.Fraction(120, 0, 100)); // 画满
        Assert.Equal(100, ((IRangeValueProvider)gauge.Peer()).Value); // 夹在范围里
        Assert.Equal("120 分", ((IValueProvider)gauge.Peer()).Value); // 念原值
        Assert.Equal(StatusTone.Success, gauge.ShownTone); // 超出最后一段取最后一段的色调
        Assert.Equal(StatusTone.Danger, new Gauge { Value = 30, Bands = gauge.Bands }.ShownTone);
    });
}
