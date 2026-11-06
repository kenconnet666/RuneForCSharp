using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// Badge：默认值在主题样式里、使用方的样式能改；样式 × 色调引用主题画刷并随主题切换；图标部件引用部件样式键，
/// Provider 的部件样式叠得上去；尺寸按 em；读屏直接读里面的文字。
/// </summary>
public class BadgeTests
{
    static (StackPanel Host, RuneTheme Theme) Host(params UIElement[] children)
    {
        var host = new StackPanel();
        var theme = new RuneTheme();
        host.Resources.MergedDictionaries.Add(theme);
        foreach (var child in children) host.Children.Add(child);
        return (host, theme);
    }

    static Color BrushColor(Brush? brush) => Assert.IsType<SolidColorBrush>(brush, exactMatch: false).Color;

    [Fact]
    public Task Defaults_come_from_the_theme_and_styles_can_change_them() => WpfUi.Run(() =>
    {
        var plain = new Badge { Content = "草稿" };
        var styled = new Badge { Content = "新功能" };
        var (host, _) = Host(plain, styled);
        styled.Style = new Style(typeof(Badge)) { Setters = { new Setter(Badge.VariantProperty, StatusVariant.Solid), new Setter(Badge.ToneProperty, StatusTone.Accent) } };
        WpfUi.Show(host);

        Assert.Equal((StatusVariant.Soft, StatusTone.Neutral, TextSize.Xs, Radius.Sm), (plain.Variant, plain.Tone, plain.Size, plain.Radius));
        Assert.Equal(12, plain.FontSize);
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.Text")), BrushColor(plain.Foreground));
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.AccentContrast")), BrushColor(styled.Foreground));
    });

    [Fact]
    public Task Colors_follow_variant_tone_and_theme() => WpfUi.Run(() =>
    {
        var badge = new Badge { Content = "已失败", Variant = StatusVariant.Outline, Tone = StatusTone.Danger };
        var (host, theme) = Host(badge);
        WpfUi.Show(host);
        var light = BrushColor(badge.Foreground);
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.DangerText")), light);

        theme.Theme = "dark";
        WpfUi.Flush();
        Assert.NotEqual(light, BrushColor(badge.Foreground));
        var text = LogicalTreeHelper.GetChildren(badge).OfType<Text>().Single();
        Assert.Equal(BrushColor(badge.Foreground), BrushColor(text.Foreground)); // 文字从 Badge 继承颜色
    });

    [Fact]
    public Task Provider_part_styles_reach_the_icon_and_the_dot_shows_only_without_icon() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:BadgeParts.Icon}" TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="3" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <StackPanel>
                  <rune:Badge x:Name="withIcon" Icon="{x:Static rune:Lucide.CircleCheck}" Dot="True">已通过</rune:Badge>
                  <rune:Badge x:Name="withDot" Dot="True">运行中</rune:Badge>
                </StackPanel>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        var withIcon = (Badge)root.FindName("withIcon");
        var icon = LogicalTreeHelper.GetChildren(withIcon).OfType<Icon>().Single();
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Empty(LogicalTreeHelper.GetChildren(withIcon).OfType<Border>());
        var dot = LogicalTreeHelper.GetChildren((Badge)root.FindName("withDot")).OfType<Border>().Single();
        Assert.Equal(6, dot.ActualWidth); // 0.5em，字号 12
    });

    [Fact]
    public Task Screen_readers_read_the_text_inside() => WpfUi.Run(() =>
    {
        var badge = new Badge { Content = "已发货", Size = TextSize.Lg };
        WpfUi.Show(Host(badge).Host);

        Assert.True(badge.ActualHeight >= 1.75 * 16); // 最小高度 1.75em
        var peer = UIElementAutomationPeer.CreatePeerForElement(badge);
        Assert.False(peer.IsControlElement());
        Assert.Contains(peer.GetChildren(), child => child.GetName() == "已发货");
    });
}
