namespace Rune.WinForms.Tests;

/// <summary>Sub、Sup：和旁边的文字一样高（不撑高一行）、比它窄；Del 划线 + 次要色，Strike 只划线。</summary>
public class InlinesTests
{
    [Fact]
    public Task Scripts_keep_the_line_height_and_edits_are_marked() => WinFormsUi.Run(() =>
    {
        var (text, sup, sub) = (new TextControl { Text = "2" }, new SupControl { Text = "2" }, new SubControl { Text = "2" });
        var (del, strike) = (new DelControl { Text = "10 月 20 日" }, new StrikeControl { Text = "¥199" });
        using var form = WinFormsUi.Show(text, sup, sub, del, strike);

        Assert.Equal(text.GetPreferredSize(Size.Empty).Height, sup.GetPreferredSize(Size.Empty).Height);
        Assert.True(sup.GetPreferredSize(Size.Empty).Width < text.GetPreferredSize(Size.Empty).Width);
        Assert.Equal(sup.GetPreferredSize(Size.Empty), sub.GetPreferredSize(Size.Empty));
        Assert.True(del.Element.Strikethrough);
        Assert.Equal(TextTone.Muted, del.Element.Tone);
        Assert.True(strike.Element.Strikethrough);
    });
}
