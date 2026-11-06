using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Rune.Wpf.Tests;

/// <summary>RuneTheme 与生成的 token（tools/token-gen）：类型对、换主题时 DynamicResource 跟着变、16 套主题各有自己的字典。</summary>
public class RuneThemeTests
{
    [Fact]
    public Task Shared_tokens_have_platform_types() => WpfUi.Run(() =>
    {
        var theme = new RuneTheme();

        Assert.Equal(14d, theme["Rune.FontSize.Md"]);
        Assert.Equal(0.75d, theme["Rune.Space.Md"]);
        Assert.Equal(FontWeights.Medium, theme["Rune.FontWeight.Medium"]);
        Assert.Equal(TimeSpan.FromMilliseconds(200), theme["Rune.Duration.Md"]);
        Assert.Same(CubicBezierEase.Standard, theme["Rune.Easing.Standard"]);
        // rem 换成设备无关像素：30rem → 480
        Assert.Equal(480d, theme["Rune.Size.Xs"]);
    });

    [Fact]
    public Task Switching_the_theme_updates_dynamic_resources() => WpfUi.Run(() =>
    {
        var theme = new RuneTheme();
        var border = new Border();
        border.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Background");
        var host = new StackPanel { Children = { border } };
        host.Resources.MergedDictionaries.Add(theme);
        WpfUi.Show(host);

        Assert.Equal((Color)ColorConverter.ConvertFromString("#f9f9fb"), ((SolidColorBrush)border.Background).Color);
        Assert.True(border.Background.IsFrozen);

        theme.Theme = "dark";
        WpfUi.Flush();
        Assert.Equal((Color)ColorConverter.ConvertFromString("#111113"), ((SolidColorBrush)border.Background).Color);
        Assert.Equal(0.25d, new RuneTheme { Theme = "high-contrast" }["Rune.Radius.Md"]);
        Assert.IsType<DropShadowEffect>(theme["Rune.Shadow.Md"]);
    });

    [Fact]
    public Task Every_built_in_theme_has_its_own_dictionary() => WpfUi.Run(() =>
    {
        Assert.Equal(16, RuneTheme.BuiltinThemes.Count);
        var colors = RuneTheme.BuiltinThemes
            .Select(entry => new ResourceDictionary { Source = entry.Source })
            .Select(dictionary => (dictionary["Rune.Color.Background"], dictionary["Rune.Color.Accent"]))
            .ToList();
        Assert.DoesNotContain(colors, pair => pair.Item1 is null || pair.Item2 is null);
        // high-contrast 与 mono 的底色都是白：比（底色, 品牌色）这一对
        Assert.Equal(colors.Count, colors.Distinct().Count());
        Assert.Equal(["dark", "midnight", "high-contrast-dark", "cyberpunk", "big-screen", "ember"], RuneTheme.BuiltinThemes.Where(t => t.IsDark).Select(t => t.Name));
        Assert.Throws<ArgumentException>(() => new RuneTheme { Theme = "nope" });
        // 相对的组件 URI：Application 建好之前（pack 协议还没注册）读 BuiltinThemes 也不抛异常
        Assert.DoesNotContain(RuneTheme.BuiltinThemes, entry => entry.Source.IsAbsoluteUri);
    });

    [Fact]
    public Task Cubic_bezier_follows_the_css_curve() => WpfUi.Run(() =>
    {
        var linear = new CubicBezierEase(0, 0, 1, 1);
        foreach (var t in new[] { 0, 0.25, 0.5, 0.75, 1 }) Assert.Equal(t, linear.Ease(t), 6);

        // standard 先快后慢：前半段走得多；EasingMode 固定为 EaseIn，曲线不被 WPF 翻转
        Assert.True(CubicBezierEase.Standard.Ease(0.5) > 0.8);
        Assert.True(CubicBezierEase.Standard.IsFrozen);
        var copy = (CubicBezierEase)CubicBezierEase.Enter.Clone();
        Assert.Equal((0.05, 0.7, 0.1, 1d), (copy.X1, copy.Y1, copy.X2, copy.Y2));
    });
}
