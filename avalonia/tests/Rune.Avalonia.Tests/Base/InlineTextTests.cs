using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Code、Mark 是行内文字（跟着段落换行），Blockquote 是起始边带竖线的块。</summary>
public class InlineTextTests
{
    static object? Token(string key)
    {
        Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value);
        return value;
    }

    [Fact]
    public Task Code_and_mark_are_inline_runs_with_theme_backgrounds() => HeadlessUi.Run(() =>
    {
        var code = new Code { Inlines = { new Run("button") } };
        var mark = new Mark { Inlines = { new Run("Button 按钮") } };
        var text = new Paragraph { Inlines = [new Run("输入 "), code, new Run(" 找到 "), mark] };
        text.FontSize = 20;
        HeadlessUi.Show(new StackPanel { Children = { text } });

        // Code：等宽、浅底、字号 0.9 倍；Mark：高亮底色，文字颜色不变
        Assert.Equal(18, code.FontSize);
        Assert.Same(Token("Rune.Font.Mono"), code.FontFamily);
        Assert.Same(Token("Rune.Brush.Fill"), code.Background);
        Assert.Same(Token("Rune.Brush.Highlight"), mark.Background);
        Assert.Equal(text.Foreground, mark.Foreground);
    });

    [Fact]
    public Task Blockquote_has_a_start_rule_and_muted_text() => HeadlessUi.Run(() =>
    {
        var inner = new Paragraph { Text = "渐进式框架" };
        var quote = new Blockquote { Content = inner };
        var host = new StackPanel { Children = { quote } };
        host.SetValue(TextElement.FontSizeProperty, 16d);
        HeadlessUi.Show(host);

        Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
        Assert.Equal(new Thickness(16, 4, 0, 4), quote.Padding);
        Assert.Same(Token("Rune.Brush.BorderStrong"), quote.BorderBrush);
        Assert.Same(Token("Rune.Brush.TextMuted"), inner.Foreground);
    });
}
