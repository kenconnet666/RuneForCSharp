namespace Rune.WinForms.Tests;

/// <summary>Code、Mark 是单独的小盒子（WinForms 没有行内元素），Blockquote 是起始边带竖线、文字变淡的面板。</summary>
public class InlineTextTests
{
    [Fact]
    public Task Code_is_mono_at_90_percent_with_inline_padding() => WinFormsUi.Run(() =>
    {
        var code = new CodeControl { Text = "button" };
        var plain = new TextControl { Text = "button", Mono = true };
        using var form = WinFormsUi.Show(code, plain);
        var em = form.Font.SizeInPoints * 0.9f * code.DeviceDpi / 72f;

        // 宽度 = 0.9 倍字号的等宽文字 + 左右各 space.xs；比同样文字的普通字号版本窄（字小）但多了内边距
        var text = TextRenderer.MeasureText("button", Fonts.Get(Fonts.Installed(RuneTheme.Current.Value.Fonts.Mono), form.Font.SizeInPoints * 0.9f, form.Font.Style), default, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        Assert.Equal((int)MathF.Ceiling(text.Width + 2 * Tokens.Space.Xs * em), code.Width - code.Padding.Horizontal);
        Assert.Equal(AccessibleRole.StaticText, code.AccessibilityObject.Role);
    });

    [Fact]
    public Task Blockquote_has_a_start_rule_and_muted_text() => WinFormsUi.Run(() =>
    {
        var quote = new BlockquoteControl();
        var inner = new ParagraphControl { Text = "渐进式框架", Dock = DockStyle.Top };
        quote.Controls.Add(inner);
        using var form = WinFormsUi.Show(quote);
        var scale = quote.DeviceDpi / 96f;
        var em = quote.Font.SizeInPoints * quote.DeviceDpi / 72f;

        Assert.Equal(RuneTheme.Current.Value.Colors.TextMuted.ToArgb(), inner.ForeColor.ToArgb());
        Assert.Equal((int)MathF.Round(Tokens.BorderWidth.Md * scale + Tokens.Space.Lg * em), quote.Padding.Left);
        Assert.Equal(0, quote.Padding.Right);
    });
}
