using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Tests;

/// <summary>Abbr：全称是提示与读屏的帮助文字，能聚焦，点状下划线；放进段落时按第一行基线对齐。</summary>
public class AbbrTests
{
    [Fact]
    public Task Title_is_the_tooltip_and_the_help_text() => HeadlessUi.Run(() =>
    {
        var abbr = new Abbr { Text = "SLA", Title = "服务等级协议" };
        var paragraph = new Paragraph();
        paragraph.Inlines!.Add(new InlineUIContainer(abbr));
        HeadlessUi.Show(paragraph);

        Assert.Equal("服务等级协议", ToolTip.GetTip(abbr));
        Assert.Equal("服务等级协议", abbr.Peer().GetHelpText());
        Assert.True(abbr.Focusable);
        Assert.NotNull(abbr.TextDecorations);
        Assert.True(abbr.BaselineOffset > 0 && abbr.BaselineOffset < abbr.Bounds.Height);
    });
}
