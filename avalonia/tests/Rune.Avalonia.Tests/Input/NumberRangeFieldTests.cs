using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>NumberRangeField：两端各自写回，起比止大时对调，一端可以清空；在 Field 里以标签为名称，只有第一个框跟着必填；外观交给两个框。</summary>
public class NumberRangeFieldTests
{
    static void Type(NumberField box, TextBox other, string text)
    {
        box.Focus();
        box.Text = text;
        other.Focus();
    }

    [Fact]
    [Upstream("NumberRangeField", "两端各自写回；起比止大时自动对调；一端可以清空")]
    public Task Each_end_writes_back_and_reversed_ends_swap() => HeadlessUi.Run(() =>
    {
        var range = new NumberRangeField();
        var other = new TextBox();
        HeadlessUi.Show(new StackPanel { Children = { range, other } });
        var (start, end) = range.Fields;
        Type(start, other, "50");
        Assert.Equal(new NumberRange(50, null), range.Value);
        Type(end, other, "10");
        Assert.Equal(new NumberRange(10, 50), range.Value);
        Assert.Equal((10m, 50m), (start.Value, end.Value));
        Type(start, other, "");
        Assert.Equal(new NumberRange(null, 50), range.Value);
    });

    [Fact]
    [Upstream("NumberRangeField", "在 Field 里：标签指向第一个框，组以标签为名称；两个框念最小值、最大值")]
    public Task Field_names_the_group_and_the_ends_read_min_and_max() => HeadlessUi.Run(() =>
    {
        var range = new NumberRangeField();
        var field = new Field { Label = "预期收益量级", Content = range };
        Provider.SetLang(field, Lang.ZhCn);
        HeadlessUi.Show(field);
        var (start, end) = range.Fields;
        var label = field.GetLogicalDescendants().OfType<FieldLabel>().Single();
        Assert.Same(range, label.Target);
        Assert.Equal(("预期收益量级", AutomationControlType.Group), (range.Peer().GetName(), range.Peer().GetAutomationControlType()));
        Assert.Equal(["最小值", "最大值"], new[] { start, end }.Select(box => box.Peer().GetName()));
    });

    [Fact]
    [Upstream("NumberRangeField", "外观（variant、size、radius）交给两个框：外壳的样式与单独的 NumberField 一样")]
    public Task Appearance_goes_to_both_boxes() => HeadlessUi.Run(() =>
    {
        var range = new NumberRangeField { Variant = ControlVariant.Soft, Size = TextSize.Lg, Radius = Radius.Full };
        HeadlessUi.Show(range);
        var (start, end) = range.Fields;
        Assert.All(new[] { start, end }, box => Assert.Equal((ControlVariant.Soft, TextSize.Lg, Radius.Full, false), (box.Variant, box.Size, box.Radius, box.Stepper)));
    });

    [Fact]
    [Upstream("Form", "控件里再嵌的控件不接 Field：区间的第二个框没有 Field 的 id、说明与必填")]
    public Task Only_the_first_box_takes_the_field() => HeadlessUi.Run(() =>
    {
        var range = new NumberRangeField();
        var host = new Field { Label = "预算", Description = "单位：万元", IsRequired = true, Content = range };
        HeadlessUi.Show(host);
        var (start, end) = range.Fields;
        Assert.Equal((true, false), (start.IsRequired, end.IsRequired));
        Assert.Contains("单位：万元", AutomationProperties.GetHelpText(range));
        // 第二个框的帮助文字只有自己的占位，没有字段的说明
        Assert.DoesNotContain("单位：万元", end.Peer().GetHelpText() ?? "");
    });
}
