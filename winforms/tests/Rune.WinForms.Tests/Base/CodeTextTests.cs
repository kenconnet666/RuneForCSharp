namespace Rune.WinForms.Tests;

/// <summary>CodeText：反引号里的部分画成 Code，其余是普通文字，排成一行。</summary>
public class CodeTextTests
{
    [Upstream("CodeText", "反引号里的渲染成 code")]
    [Upstream("CodeText", "没有反引号")]
    [Fact]
    public Task Backticks_become_code() => WinFormsUi.Run(() =>
    {
        var mixed = new CodeTextControl { Source = "输入 `button`" };
        var text = new TextControl { Text = "输入 " };
        var code = new CodeControl { Text = "button" };
        var plain = new CodeTextControl { Source = "只有文字" };
        var plainText = new TextControl { Text = "只有文字" };
        using var form = WinFormsUi.Show(mixed, text, code, plain, plainText);

        Assert.Equal(text.Width + code.Width, mixed.Width);
        Assert.Equal(plainText.Width, plain.Width);
        Assert.Equal("输入 button", mixed.AccessibilityObject.Name);
    });
}
