using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

/// <summary>Code、Mark 是行内文字（跟着段落换行），Blockquote 是起始边带竖线的块。</summary>
public class InlineTextTests
{
    [Fact]
    public Task Code_and_mark_are_inline_runs_with_theme_backgrounds() => WpfUi.Run(() =>
    {
        var code = new Code { Inlines = { new Run("button") } };
        var mark = new Mark { Inlines = { new Run("Button 按钮") } };
        var text = new Paragraph();
        text.Inlines.AddRange(new Inline[] { new Run("输入 "), code, new Run(" 找到 "), mark });
        var host = new StackPanel { Children = { text } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        TextElement.SetFontSize(host, 20);
        WpfUi.Show(host);

        // Code：等宽、浅底、字号 0.9 倍；Mark：高亮底色，文字颜色不变
        Assert.Equal(18, code.FontSize, 6);
        Assert.Same(host.FindResource("Rune.Font.Mono"), code.FontFamily);
        Assert.Same(host.FindResource("Rune.Brush.Fill"), code.Background);
        Assert.Same(host.FindResource("Rune.Brush.Highlight"), mark.Background);
        Assert.Equal(text.Foreground, mark.Foreground);
    });

    [Fact]
    public Task Blockquote_has_a_start_rule_and_muted_text() => WpfUi.Run(() =>
    {
        var inner = new Paragraph { Text = "渐进式框架" };
        var quote = new Blockquote { Content = inner };
        var host = new StackPanel { Children = { quote } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        TextElement.SetFontSize(host, 16);
        WpfUi.Show(host);

        Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
        Assert.Equal(new Thickness(16, 4, 0, 4), quote.Padding);
        Assert.Same(host.FindResource("Rune.Brush.BorderStrong"), quote.BorderBrush);
        Assert.Same(host.FindResource("Rune.Brush.TextMuted"), inner.Foreground);
    });
}
