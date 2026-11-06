using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Indicator：计数超过 max 显示 max+；0 默认不显示；读屏念完整的说明；角标的中心对准角；颜色按色调。</summary>
public class IndicatorTests
{
    static Border Badge(Indicator indicator) => LogicalTreeHelper.GetChildren(indicator).OfType<Border>().Single(b => b.Name == "PART_Badge");

    static TextBlock Count(Indicator indicator) => (TextBlock)Badge(indicator).Child;

    static Point Center(Indicator indicator)
    {
        var badge = Badge(indicator);
        return badge.TranslatePoint(new Point(badge.ActualWidth / 2, badge.ActualHeight / 2), indicator);
    }

    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Upstream("Indicator", "计数：超过 max 显示“max+”")]
    [Fact]
    public Task Count_over_max_shows_plus_and_the_badge_reads_the_label() => WpfUi.Run(() =>
    {
        var indicator = new Indicator { Count = 120, Content = new Button { Content = "通知" } };
        WpfUi.Show(Themed(indicator));

        Assert.Equal("99+", Count(indicator).Text);
        var peer = UIElementAutomationPeer.CreatePeerForElement(Badge(indicator));
        Assert.Equal((Provider.GetLang(indicator).IndicatorCount(120), true, AutomationControlType.Text),
            (peer.GetName(), peer.IsControlElement(), peer.GetAutomationControlType()));
        Assert.Null(peer.GetChildren());
    });

    [Upstream("Indicator", "0 默认不显示")]
    [Fact]
    public Task Zero_hides_unless_show_zero_and_dot_has_no_number() => WpfUi.Run(() =>
    {
        var zero = new Indicator { Count = 0, Content = new Border { Width = 20, Height = 20 } };
        var shown = new Indicator { Count = 0, ShowZero = true, Content = new Border { Width = 20, Height = 20 } };
        var dot = new Indicator { Dot = true, Label = "在线", Placement = IndicatorPlacement.BottomEnd, Content = new Border { Width = 40, Height = 40 } };
        WpfUi.Show(Themed(zero, shown, dot));

        Assert.Equal(Visibility.Collapsed, Badge(zero).Visibility);
        Assert.Equal(Visibility.Visible, Badge(shown).Visibility);
        Assert.Equal("0", Count(shown).Text);
        Assert.Equal(Visibility.Collapsed, Count(dot).Visibility);
        Assert.Equal("在线", UIElementAutomationPeer.CreatePeerForElement(Badge(dot)).GetName());
        // 圆点 0.625em（外面一圈 2px 的页面底色），中心在右下角
        Assert.Equal(0.625 * dot.FontSize + 4, Badge(dot).ActualWidth, 3);
        Assert.Equal(new Point(40, 40), Center(dot));
    });

    [Upstream("Indicator", "部件属性：计数文字")]
    [Fact]
    public Task Count_part_style_from_the_provider() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:IndicatorParts.Count}" TargetType="TextBlock"><Setter Property="FontStyle" Value="Italic" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:Indicator x:Name="indicator" Count="5"><TextBlock Text="消息" /></rune:Indicator>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        var count = Count((Indicator)root.FindName("indicator"));
        Assert.Equal(("5", FontStyles.Italic), (count.Text, count.FontStyle));
    });

    [Fact]
    public Task Badge_centers_on_the_corner_moves_in_on_circles_and_takes_the_tone() => WpfUi.Run(() =>
    {
        var square = new Indicator { Count = 3, Content = new Border { Width = 32, Height = 32 } };
        var circle = new Indicator { Count = 3, Overlap = IndicatorOverlap.Circular, Tone = StatusTone.Success, Content = new Border { Width = 32, Height = 32 } };
        var host = Themed(square, circle);
        WpfUi.Show(host);

        Assert.Equal(new Size(32, 32), square.RenderSize); // 角标不占位置
        Assert.Equal(new Point(32, 0), Center(square));
        var inset = 0.146 * 32;
        Assert.Equal(32 - inset, Center(circle).X, 3);
        Assert.Equal(inset, Center(circle).Y, 3);
        // 计数：0.75em 的字，高 1.6 倍（外加一圈 2px）
        Assert.Equal(1.6 * 0.75 * square.FontSize + 4, Badge(square).ActualHeight, 3);
        Assert.Equal(((SolidColorBrush)host.FindResource("Rune.Brush.Danger")).Color, ((SolidColorBrush)Badge(square).Background).Color);
        Assert.Equal(((SolidColorBrush)host.FindResource("Rune.Brush.Success")).Color, ((SolidColorBrush)Badge(circle).Background).Color);
        Assert.Equal(((SolidColorBrush)host.FindResource("Rune.Brush.SuccessContrast")).Color, ((SolidColorBrush)Count(circle).Foreground).Color);
    });
}
