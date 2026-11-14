namespace Rune.WinForms.Tests;

/// <summary>GaugeControl：读屏是只读的进度条（取值文字带目标）；外圈按区间分段；数值弧取所在区间的色调。</summary>
public class GaugeTests
{
    [Fact]
    [Upstream("Gauge", "meter：取值范围、当前值（夹在范围里）、格式化的数值与目标")]
    public Task Reads_as_a_range_with_the_formatted_value_and_target() => WinFormsUi.Run(() =>
    {
        var gauge = new GaugeControl { Value = 0.82m, Max = 1, ValueFormat = new NumericFormat { Style = NumericStyle.Percent }, Target = 0.9m, Label = "完成率" };
        using var form = WinFormsUi.Show(gauge);

        var accessible = gauge.AccessibilityObject;
        Assert.Equal((AccessibleRole.ProgressBar, "完成率", "82%，目标 90%"), (accessible.Role, accessible.Name, accessible.Value));
        Assert.Equal("82%", gauge.Element.ShownValue);
        Assert.Equal((int)MathF.Round(10 * gauge.Font.SizeInPoints * gauge.DeviceDpi / 72f), gauge.Width); // 宽 10em
    });

    [Fact]
    [Upstream("Gauge", "外圈按区间分段；超出范围的值画满、念原值")]
    public Task Bands_split_the_ring_and_out_of_range_values_fill_the_arc() => WinFormsUi.Run(() =>
    {
        var gauge = new GaugeControl { Value = 120, Unit = "分", Bands = [new(60, StatusTone.Danger), new(100, StatusTone.Success)] };
        using var form = WinFormsUi.Show(gauge);

        Assert.Equal(2, GaugeGeometry.Bands(gauge.Bands, 0, 100).Count());
        Assert.Equal(1, GaugeGeometry.Fraction(120, 0, 100)); // 画满
        Assert.Equal("120 分", gauge.AccessibilityObject.Value); // 念原值
        Assert.Equal(StatusTone.Success, gauge.Element.ShownTone);
    });
}
