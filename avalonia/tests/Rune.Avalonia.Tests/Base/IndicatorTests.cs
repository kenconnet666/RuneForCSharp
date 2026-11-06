using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Indicator：计数超过 max 显示 max+；0 默认不显示；读屏念完整的说明；角标的中心对准角。</summary>
public class IndicatorTests
{
    static Border Badge(Indicator indicator) => indicator.GetLogicalChildren().OfType<Border>().Single(b => b.Name == IndicatorParts.Badge);

    static TextBlock Count(Indicator indicator) => (TextBlock)Badge(indicator).Child!;

    /// <summary>布局取整（UseLayoutRounding，大小向上取整）后差一个像素以内。</summary>
    static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1, expected + 1);

    static void Near(Point expected, Point actual)
    {
        Near(expected.X, actual.X);
        Near(expected.Y, actual.Y);
    }

    [Upstream("Indicator", "计数：超过 max 显示“max+”")]
    [Fact]
    public Task Count_over_max_shows_plus_and_the_badge_reads_the_label() => HeadlessUi.Run(() =>
    {
        var indicator = new Indicator { Count = 120, Content = new Button { Content = "通知" } };
        HeadlessUi.Show(indicator);

        Assert.Equal("99+", Count(indicator).Text);
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(Count(indicator)));
        var peer = global::Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Badge(indicator));
        Assert.Equal((Provider.GetLang(indicator).IndicatorCount(120), true, global::Avalonia.Automation.Peers.AutomationControlType.Text),
            (peer.GetName(), peer.IsControlElement(), peer.GetAutomationControlType()));
        Assert.Empty(peer.GetChildren());
    });

    [Upstream("Indicator", "0 默认不显示")]
    [Fact]
    public Task Zero_hides_unless_show_zero_and_dot_has_no_number() => HeadlessUi.Run(() =>
    {
        var zero = new Indicator { Count = 0, Content = new Border { Width = 20, Height = 20 } };
        var shown = new Indicator { Count = 0, ShowZero = true, Content = new Border { Width = 20, Height = 20 } };
        var dot = new Indicator { Dot = true, Label = "在线", Placement = IndicatorPlacement.BottomEnd, Content = new Border { Width = 40, Height = 40 } };
        HeadlessUi.Show(new StackPanel { Spacing = 20, Children = { zero, shown, dot } });

        Assert.False(Badge(zero).IsVisible);
        Assert.True(Badge(shown).IsVisible);
        Assert.Equal("0", Count(shown).Text);
        Assert.False(Count(dot).IsVisible);
        Assert.Equal("在线", AutomationProperties.GetName(Badge(dot)));
        // 圆点 0.625em，中心在右下角
        var em = dot.FontSize;
        Near(0.625 * em, Badge(dot).Bounds.Width);
        Near(new Point(40, 40), Badge(dot).Bounds.Center);
    });

    [Upstream("Indicator", "部件属性：计数文字")]
    [Fact]
    public Task Count_part_is_reachable_and_stays_hidden_from_screen_readers() => HeadlessUi.Run(() =>
    {
        var indicator = new Indicator { Count = 5, Content = new TextBlock { Text = "消息" } };
        var host = new StackPanel { Children = { indicator } };
        host.Styles.Add(new Style(x => x.OfType<Indicator>().Descendant().OfType<TextBlock>().Name(IndicatorParts.Count))
        {
            Setters = { new Setter(TextBlock.FontStyleProperty, global::Avalonia.Media.FontStyle.Italic) },
        });
        HeadlessUi.Show(host);

        Assert.Equal(("5", global::Avalonia.Media.FontStyle.Italic), (Count(indicator).Text, Count(indicator).FontStyle));
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(Count(indicator)));
    });

    [Fact]
    public Task Badge_centers_on_the_corner_and_moves_in_on_circles() => HeadlessUi.Run(() =>
    {
        var square = new Indicator { Count = 3, Content = new Border { Width = 32, Height = 32 } };
        var circle = new Indicator { Count = 3, Overlap = IndicatorOverlap.Circular, Content = new Border { Width = 32, Height = 32 } };
        HeadlessUi.Show(new StackPanel { Spacing = 20, Children = { square, circle } });

        Assert.Equal(new Size(32, 32), square.Bounds.Size); // 角标不占位置
        Near(new Point(32, 0), Badge(square).Bounds.Center);
        Near(new Point(32 - 0.146 * 32, 0.146 * 32), Badge(circle).Bounds.Center);
        // 计数：0.75em 的字，高 1.6 倍
        Near(1.6 * 0.75 * square.FontSize, Badge(square).Bounds.Height);
    });
}
