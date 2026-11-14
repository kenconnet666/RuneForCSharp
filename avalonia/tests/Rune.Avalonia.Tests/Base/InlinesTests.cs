using Avalonia.Media;

namespace Rune.Avalonia.Tests;

/// <summary>Sub、Sup：字号是外面的 0.75 倍、上下标基线；Del 划线 + 次要色，Ins 浅绿底，Strike 只划线。</summary>
public class InlinesTests
{
    [Fact]
    public Task Scripts_shrink_and_shift_and_edits_are_marked() => HeadlessUi.Run(() =>
    {
        var (sup, sub, del, ins, strike) = (new Sup { Text = "2" }, new Sub { Text = "2" }, new Del(), new Ins(), new Strike());
        var paragraph = new Paragraph { FontSize = 16 };
        paragraph.Inlines!.AddRange([sup, sub, del, ins, strike]);
        HeadlessUi.Show(paragraph);

        Assert.Equal(12, sup.FontSize);
        Assert.Equal(BaselineAlignment.Superscript, sup.BaselineAlignment);
        Assert.Equal(BaselineAlignment.Subscript, sub.BaselineAlignment);
        Assert.Equal(TextDecorations.Strikethrough, del.TextDecorations);
        Assert.Equal(TextDecorations.Strikethrough, strike.TextDecorations);
        Assert.Equal(paragraph.Resource<IBrush>("Rune.Brush.SuccessSoft"), ins.Background);
        Assert.Equal(paragraph.Resource<IBrush>("Rune.Brush.TextMuted"), del.Foreground);
    });
}
