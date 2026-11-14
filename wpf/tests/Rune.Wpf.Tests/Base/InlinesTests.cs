using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Sub、Sup：字号是外面的 0.75 倍、贴着文字的底 / 顶；Del 划线 + 次要色，Ins 浅绿底，Strike 只划线。</summary>
public class InlinesTests
{
    [Fact]
    public Task Scripts_shrink_and_shift_and_edits_are_marked() => WpfUi.Run(() =>
    {
        var (sup, sub, del, ins, strike) = (new Sup { Text = "2" }, new Sub { Text = "2" }, new Del(), new Ins(), new Strike());
        var paragraph = new Paragraph { FontSize = 16 };
        paragraph.Inlines.AddRange(new System.Windows.Documents.Inline[] { sup, sub, del, ins, strike });
        var host = new StackPanel { Children = { paragraph } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(12, sup.FontSize);
        Assert.Equal(BaselineAlignment.TextTop, sup.BaselineAlignment);
        Assert.Equal(BaselineAlignment.TextBottom, sub.BaselineAlignment);
        Assert.Equal(TextDecorations.Strikethrough, del.TextDecorations);
        Assert.Equal(TextDecorations.Strikethrough, strike.TextDecorations);
        Assert.Equal(((SolidColorBrush)host.FindResource("Rune.Brush.SuccessSoft")).Color, ((SolidColorBrush)ins.Background).Color);
        Assert.Equal(((SolidColorBrush)host.FindResource("Rune.Brush.TextMuted")).Color, ((SolidColorBrush)del.Foreground).Color);
    });
}
