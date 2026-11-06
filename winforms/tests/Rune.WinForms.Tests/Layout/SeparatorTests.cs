namespace Rune.WinForms.Tests;

/// <summary>SeparatorControl：默认装饰性；有语义时是分隔符；带文字时以文字为名称；竖线在 FlexControl 里随行高拉伸。</summary>
public class SeparatorTests
{
    [Upstream("Separator", "默认装饰性")]
    [Fact]
    public Task Decorative_by_default_and_one_pixel_thick() => WinFormsUi.Run(() =>
    {
        var separator = new SeparatorControl { Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(separator);

        Assert.Equal(AccessibleRole.None, separator.AccessibilityObject.Role);
        Assert.Equal(Math.Max(1, (int)MathF.Round(separator.DeviceDpi / 96f)), separator.Height);
    });

    [Upstream("Separator", "有语义时是 separator")]
    [Upstream("Separator", "带文字")]
    [Fact]
    public Task Semantic_separators_are_separators_named_by_their_label() => WinFormsUi.Run(() =>
    {
        var plain = new SeparatorControl { Decorative = false };
        var labelled = new SeparatorControl { Decorative = false, Text = "更改后", LabelAlign = SeparatorLabelAlign.Start };
        using var form = WinFormsUi.Show(plain, labelled);

        Assert.Equal(AccessibleRole.Separator, plain.AccessibilityObject.Role);
        Assert.Equal((AccessibleRole.Separator, "更改后"), (labelled.AccessibilityObject.Role, labelled.AccessibilityObject.Name));
        // 带文字时高度是 0.93em 的文字高度，比一条线高
        Assert.True(labelled.Height > plain.Height);
    });

    [Fact]
    public Task Vertical_line_stretches_with_the_row_in_a_centered_flex() => WinFormsUi.Run(() =>
    {
        var line = new SeparatorControl { Orientation = SeparatorOrientation.Vertical };
        var flex = new FlexControl { Align = FlexAlign.Center };
        flex.Controls.AddRange([new Panel { Size = new Size(20, 30) }, line, new Panel { Size = new Size(20, 10) }]);
        using var form = WinFormsUi.Show(flex);

        Assert.Equal((0, 30), (line.Top, line.Height));
        Assert.Equal(Math.Max(1, (int)MathF.Round(line.DeviceDpi / 96f)), line.Width);
    });
}
