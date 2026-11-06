using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// Text 家族：不写的属性继承外面；档位引用主题资源并随主题切换；各组件的默认值在主题样式里、使用方的样式能改；
/// 对齐、换行、截断映射到 TextBlock 的属性，清掉后回到默认。
/// </summary>
public class TextTests
{
    static (StackPanel Host, RuneTheme Theme) Host(params UIElement[] children)
    {
        var host = new StackPanel();
        var theme = new RuneTheme();
        host.Resources.MergedDictionaries.Add(theme);
        foreach (var child in children) host.Children.Add(child);
        host.SetValue(TextElement.FontSizeProperty, 20d);
        host.SetValue(TextElement.ForegroundProperty, Brushes.Purple);
        return (host, theme);
    }

    static Color BrushColor(Brush brush) => Assert.IsType<SolidColorBrush>(brush, exactMatch: false).Color;

    [Fact]
    public Task Unset_properties_inherit_and_each_component_has_its_defaults() => WpfUi.Run(() =>
    {
        var text = new Text { Text = "正文" };
        var paragraph = new Paragraph { Text = "段落" };
        var strong = new Strong { Text = "重要" };
        var em = new Em { Text = "强调" };
        var small = new Small { Text = "附注" };
        WpfUi.Show(Host(text, paragraph, strong, em, small).Host);

        Assert.Equal(20, text.FontSize);
        Assert.Equal(Colors.Purple, BrushColor(text.Foreground));
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextWrap.Pretty, paragraph.Wrap);
        Assert.Equal(FontWeights.SemiBold, strong.FontWeight);
        Assert.Equal(FontStyles.Italic, em.FontStyle);
        Assert.Equal(12, small.FontSize); // fontSizes.xs
        Assert.Equal(BaseValueSource.DefaultStyle, DependencyPropertyHelper.GetValueSource(strong, Text.WeightProperty).BaseValueSource);
    });

    [Fact]
    public Task Steps_reference_theme_resources_and_follow_the_theme() => WpfUi.Run(() =>
    {
        var text = new Text { Text = "危险", Tone = TextTone.Danger, Size = TextSize.Lg, Weight = TextWeight.Bold, Mono = true, Numeric = true };
        var (host, theme) = Host(text);
        WpfUi.Show(host);

        Assert.Equal(16, text.FontSize);
        Assert.Equal(FontWeights.Bold, text.FontWeight);
        Assert.Equal(BrushColor((Brush)host.FindResource("Rune.Brush.DangerText")), BrushColor(text.Foreground));
        Assert.Equal(host.FindResource("Rune.Font.Mono"), text.FontFamily);
        Assert.Equal(FontNumeralAlignment.Tabular, Typography.GetNumeralAlignment(text));

        var light = BrushColor(text.Foreground);
        theme.Theme = "dark";
        WpfUi.Flush();
        Assert.NotEqual(light, BrushColor(text.Foreground));

        // 清掉档位：回到继承
        text.Tone = default;
        text.Size = 30;
        Assert.Equal(Colors.Purple, BrushColor(text.Foreground));
        Assert.Equal(30, text.FontSize);
        text.Size = default;
        Assert.Equal(20, text.FontSize);
    });

    [Fact]
    public Task User_styles_override_component_defaults() => WpfUi.Run(() =>
    {
        var strong = new Strong { Text = "重要" };
        var (host, _) = Host(strong);
        host.Resources.Add(typeof(Strong), new Style(typeof(Strong)) { Setters = { new Setter(Text.WeightProperty, TextWeight.Bold) } });
        WpfUi.Show(host);

        Assert.Equal(FontWeights.Bold, strong.FontWeight);
    });

    [Fact]
    public Task Truncate_line_clamp_and_wrap_map_to_text_block() => WpfUi.Run(() =>
    {
        var text = new Text { Text = "很长的一段文字", Truncate = true };
        WpfUi.Show(Host(text).Host);
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);

        text.Truncate = false;
        text.LineClamp = 2;
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(Math.Ceiling(2 * Leading.Normal * 20), text.MaxHeight); // 两行 × 行高（1.5 × 字号）
        text.FontSize = 10;
        Assert.Equal(Math.Ceiling(2 * Leading.Normal * 10), text.MaxHeight);

        text.LineClamp = null;
        text.Wrap = TextWrap.NoWrap;
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(TextTrimming.None, text.TextTrimming);
        Assert.True(double.IsPositiveInfinity(text.MaxHeight));

        // 都清掉：回到主题里的默认（换行）
        text.Wrap = null;
        text.Align = TextAlign.End;
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextAlignment.Right, text.TextAlignment);
    });

    [Fact]
    public Task Line_height_is_the_inherited_factor_times_own_font_size() => WpfUi.Run(() =>
    {
        // CSS 不带单位的 line-height：正文 1.5（lineHeights.normal），12px 的 Small 是 18；按钮里是 tight 1.25
        var body = new Text { Text = "正文" };
        var small = new Small { Text = "附注" };
        var inButton = new Text { Text = "保存" };
        var styled = new Text { Text = "自定", Style = new Style(typeof(Text)) { Setters = { new Setter(TextBlock.LineHeightProperty, 40d) } } };
        var (host, _) = Host(body, small, new Rune.Wpf.Button { Content = inButton }, styled);
        WpfUi.Show(host);

        Assert.Equal((30d, 1.5 * (double)host.FindResource("Rune.FontSize.Xs"), 1.25 * inButton.FontSize, 40d), (body.LineHeight, small.LineHeight, inButton.LineHeight, styled.LineHeight));
        Assert.Equal(host.FindResource("Rune.LineHeight.Normal"), Leading.Normal);
    });
}
