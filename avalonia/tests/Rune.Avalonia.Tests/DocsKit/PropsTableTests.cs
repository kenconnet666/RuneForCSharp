using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>PropsTable：列标题取语言包，必填与可绑定的标记，默认值，档位类属性的可选值说明。</summary>
public class PropsTableTests
{
    static readonly ApiProp[] Props =
    [
        new("Value", ApiPropKind.Prop, "string", "选中的值") { Required = true, Bindable = true },
        new("Size", ApiPropKind.Prop, "TextSize", "尺寸") { Default = "Md", Values = [new("Sm", "紧凑"), new("Md", "常规")] },
        new("Tone", ApiPropKind.Prop, "StatusTone", "色调") { Values = [new("Accent"), new("Danger")] },
    ];

    /// <summary>一段控件里的文字：Text 的文字或行内内容、Badge 的内容，按视觉树的先后连起来。</summary>
    static string Content(Control cell) => string.Concat(cell.GetSelfAndVisualDescendants().Select(visual => visual switch
    {
        Text { Inlines.Count: > 0 } text => string.Concat(text.Inlines.Select(Plain)),
        Text text => text.Text,
        _ => null,
    }));

    static string Plain(Inline inline) => inline switch
    {
        Run run => run.Text ?? "",
        Span span => string.Concat(span.Inlines.Select(Plain)),
        _ => "",
    };

    [Fact]
    [Upstream("PropsTable", "一行一个 prop：名称、标记、类型、默认值、说明与可选值")]
    public Task Rows_list_name_marks_type_default_and_values() => HeadlessUi.Run(() =>
    {
        var table = new PropsTable { Props = Props, Caption = "属性" };
        HeadlessUi.Show(new StackPanel { Width = 900, Children = { table } });

        Assert.Equal(TableDensity.Compact, table.Density);
        Assert.Equal("属性", table.GetVisualDescendants().OfType<TableCaption>().Single().Content);
        Assert.Equal(["名称", "类型", "默认值", "说明"], table.GetVisualDescendants().OfType<TableHeaderCell>().Select(cell => cell.Content as string));
        var rows = table.Sections.OfType<TableBody>().Single().Rows.OfType<TableRow>().Select(row => row.Cells.OfType<TableCell>().ToList()).ToList();
        Assert.Equal("Value必填可绑定", Content(rows[0][0]));
        Assert.Equal("—", Content(rows[0][2]));
        Assert.Equal("Md", Content(rows[1][2]));
        Assert.True(rows[1][2].Nowrap);
        var values = rows[1][3].GetVisualDescendants().OfType<Text>().Skip(1).Select(text => Content(text)).ToList();
        Assert.Equal(["Sm", "紧凑", "Md", "常规"], values);
        // 可选值都没写含义时不列出来
        Assert.Single(rows[2][3].GetVisualDescendants().OfType<Text>());
        // 说明一列至少 18rem
        Assert.True(rows[0][3].Bounds.Width >= 18 * 16);
    });

    [Fact]
    public Task Headers_follow_the_language() => HeadlessUi.Run(() =>
    {
        var table = new PropsTable { Props = Props };
        Provider.SetLang(table, Lang.EnUs);
        HeadlessUi.Show(new StackPanel { Width = 900, Children = { table } });

        Assert.Empty(table.GetVisualDescendants().OfType<TableCaption>());
        Assert.Equal(["Name", "Type", "Default", "Description"], table.GetVisualDescendants().OfType<TableHeaderCell>().Select(cell => cell.Content as string));
        Assert.Contains("Required", table.GetVisualDescendants().OfType<Badge>().Select(badge => badge.Content as string));
    });
}
