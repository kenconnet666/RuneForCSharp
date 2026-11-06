namespace Rune.WinForms.Tests;

/// <summary>Indicator：计数超过 max 显示 max+；0 默认不显示；读屏念完整的说明；角标的中心对准子控件的角，伸出的部分留出位置。</summary>
public class IndicatorTests
{
    static IndicatorControl Around(Control content, Action<IndicatorControl> setup)
    {
        var indicator = new IndicatorControl();
        setup(indicator);
        indicator.Controls.Add(content);
        return indicator;
    }

    static Point Center(Control control) => new(control.Left + control.Width / 2, control.Top + control.Height / 2);

    [Upstream("Indicator", "计数：超过 max 显示“max+”")]
    [Fact]
    public Task Count_over_max_shows_plus_and_the_badge_reads_the_label() => WinFormsUi.Run(() =>
    {
        var indicator = Around(new ButtonControl { Text = "通知" }, i => i.Count = 120);
        using var form = WinFormsUi.Show(indicator);

        Assert.Equal("99+", indicator.BadgeText);
        Assert.True(indicator.Badge.Visible);
        Assert.Equal((AccessibleRole.StaticText, RuneLang.Current.Value.IndicatorCount(120)), (indicator.Badge.AccessibilityObject.Role, indicator.Badge.AccessibilityObject.Name));
    });

    [Upstream("Indicator", "0 默认不显示")]
    [Fact]
    public Task Zero_hides_unless_show_zero_and_dot_has_no_number() => WinFormsUi.Run(() =>
    {
        var zero = Around(new Panel { Size = new Size(20, 20) }, i => i.Count = 0);
        var shown = Around(new Panel { Size = new Size(20, 20) }, i => (i.Count, i.ShowZero) = (0, true));
        var square = new Panel { Size = new Size(40, 40) };
        var dot = Around(square, i => (i.Dot, i.Label, i.Placement) = (true, "在线", IndicatorPlacement.BottomEnd));
        using var form = WinFormsUi.Show(zero, shown, dot);

        Assert.False(zero.Badge.Visible);
        Assert.Equal(new Size(20, 20), zero.Size); // 没有角标时不留位置
        Assert.True(shown.Badge.Visible);
        Assert.Equal("0", shown.BadgeText);
        Assert.Equal("", dot.BadgeText);
        Assert.Equal("在线", dot.Badge.AccessibilityObject.Name);
        // 圆点中心在子控件的右下角，伸出的一半留出位置
        Assert.Equal(new Point(square.Right, square.Bottom), Center(dot.Badge));
        Assert.Equal((0, 0), (square.Left, square.Top));
        Assert.Equal(dot.Badge.Right, dot.Width);
    });

    [Fact]
    public Task Badge_centers_on_the_corner_and_moves_in_on_circles() => WinFormsUi.Run(() =>
    {
        var squareContent = new Panel { Size = new Size(64, 64) };
        var square = Around(squareContent, i => i.Count = 3);
        var circleContent = new Panel { Size = new Size(64, 64) };
        var circle = Around(circleContent, i => (i.Count, i.Overlap) = (3, IndicatorOverlap.Circular));
        using var form = WinFormsUi.Show(square, circle);

        Assert.Equal(new Point(squareContent.Right, squareContent.Top), Center(square.Badge));
        var inset = 0.146f * 64;
        Assert.InRange(Center(circle.Badge).X, circleContent.Right - inset - 1, circleContent.Right - inset + 1);
        Assert.InRange(Center(circle.Badge).Y, circleContent.Top + inset - 1, circleContent.Top + inset + 1);
        // 角标总在被标记的控件上面
        Assert.Equal(0, square.Controls.GetChildIndex(square.Badge));
    });
}
