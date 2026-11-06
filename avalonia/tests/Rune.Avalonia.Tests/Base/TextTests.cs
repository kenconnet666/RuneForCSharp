using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Text 家族：不写的属性继承外面；档位绑定主题资源并随主题切换；各组件的默认值在主题里、使用方的样式能改；
/// 对齐、换行、截断映射到 TextBlock 的属性，清掉后回到默认。
/// </summary>
public class TextTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    static StackPanel Host(params Control[] children)
    {
        var host = new StackPanel();
        host.Children.AddRange(children);
        host.SetValue(TextElement.FontSizeProperty, 20d);
        host.SetValue(TextElement.ForegroundProperty, Brushes.Purple);
        return host;
    }

    [Fact]
    public Task Unset_properties_inherit_and_each_component_has_its_defaults() => HeadlessUi.Run(() =>
    {
        var text = new Text { Text = "正文" };
        var paragraph = new Paragraph { Text = "段落" };
        var strong = new Strong { Text = "重要" };
        var em = new Em { Text = "强调" };
        var small = new Small { Text = "附注" };
        HeadlessUi.Show(Host(text, paragraph, strong, em, small));

        Assert.Equal(20, text.FontSize);
        Assert.Equal(Colors.Purple, BrushColor(text.Foreground));
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextWrap.Pretty, paragraph.Wrap);
        Assert.Equal(TextWrapping.Wrap, paragraph.TextWrapping);
        Assert.Equal(FontWeight.SemiBold, strong.FontWeight);
        Assert.Equal(FontStyle.Italic, em.FontStyle);
        Assert.Equal(12, small.FontSize); // fontSizes.xs
    });

    [Fact]
    public Task Steps_bind_theme_resources_and_follow_the_theme() => HeadlessUi.Run(() =>
    {
        var text = new Text { Text = "危险", Tone = Tone.Danger, Size = TextSize.Lg, Weight = TextWeight.Bold, Mono = true, Numeric = true };
        var window = HeadlessUi.Show(Host(text));
        try
        {
            Assert.Equal(16, text.FontSize);
            Assert.Equal(FontWeight.Bold, text.FontWeight);
            Application.Current!.TryGetResource("Rune.Brush.DangerText", ThemeVariant.Light, out var dangerText);
            Application.Current.TryGetResource("Rune.Font.Mono", ThemeVariant.Light, out var mono);
            Assert.Equal(BrushColor((IBrush?)dangerText), BrushColor(text.Foreground));
            Assert.Equal(mono, text.FontFamily);
            Assert.Contains(text.FontFeatures!, feature => feature.Tag == "tnum");

            var light = BrushColor(text.Foreground);
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            HeadlessUi.Flush(window);
            Assert.NotEqual(light, BrushColor(text.Foreground));

            // 清掉档位：回到继承
            text.Tone = default;
            text.Size = 30;
            HeadlessUi.Flush(window);
            Assert.Equal(Colors.Purple, BrushColor(text.Foreground));
            Assert.Equal(30, text.FontSize);
            text.Size = default;
            Assert.Equal(20, text.FontSize);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    });

    [Fact]
    public Task User_styles_override_component_defaults() => HeadlessUi.Run(() =>
    {
        var strong = new Strong { Text = "重要" };
        var host = Host(strong);
        host.Styles.Add(new Style(x => x.OfType<Strong>()) { Setters = { new Setter(Text.WeightProperty, TextWeight.Bold) } });
        HeadlessUi.Show(host);

        Assert.Equal(FontWeight.Bold, strong.FontWeight);
    });

    [Fact]
    public Task Truncate_line_clamp_and_wrap_map_to_text_block() => HeadlessUi.Run(() =>
    {
        var text = new Text { Text = "很长的一段文字", Truncate = true };
        var window = HeadlessUi.Show(Host(text));
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);

        text.Truncate = false;
        text.LineClamp = 2;
        Assert.Equal((TextWrapping.Wrap, 2), (text.TextWrapping, text.MaxLines));

        text.LineClamp = null;
        text.Wrap = TextWrap.NoWrap;
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(TextTrimming.None, text.TextTrimming);

        // 都清掉：回到主题里的默认（换行）
        text.Wrap = null;
        text.Align = TextAlign.Center;
        HeadlessUi.Flush(window);
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextAlignment.Center, text.TextAlignment);
    });
}
