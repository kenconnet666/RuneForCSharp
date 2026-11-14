namespace Rune.WinForms.Tests;

/// <summary>DocArticleControl、DocSectionControl、TextTableControl：文章的标题（h1）、简介、lead 与正文；一节的标题（h2）带 id；文字表格的反引号是行内代码。</summary>
public class DocArticleTests
{
    [Fact]
    [Upstream("DocArticle", "article 里有 h1、简介、lead 与正文；一节的 h2 带 id")]
    public Task Articles_have_a_title_description_lead_and_sections() => WinFormsUi.Run(() =>
    {
        var section = new DocSectionControl { SectionId = "install", Title = "安装" };
        section.Controls.Add(new ParagraphControl { Text = "运行命令" });
        var lead = new TextControl { Text = "import" };
        var article = new DocArticleControl { Title = "快速开始", Description = "两分钟跑起来", Lead = lead, Dock = DockStyle.Top };
        article.Controls.Add(section);
        using var form = WinFormsUi.Show(article);

        Assert.Equal(("快速开始", true), (article.TitlePart.Text, article.TitlePart.Visible));
        Assert.Equal(("两分钟跑起来", TextTone.Muted), (article.DescriptionPart.Text, article.DescriptionPart.Tone));
        Assert.Same(article, lead.Parent!.Parent); // lead 在头部里
        Assert.Equal(("安装", "install"), (section.TitlePart.Text, section.SectionId));
        Assert.Equal(15, article.Font.SizeInPoints * 96 / 72f, 1); // 长文阅读比界面文字大一号
        Assert.True(section.Top >= article.DescriptionPart.Parent!.Bottom + article.LogicalToDeviceUnits(48) - 1, $"{section.Top} {article.DescriptionPart.Parent!.Bottom} {section.Margin} {article.LogicalToDeviceUnits(48)}"); // 正文与头部之间 3rem
    });

    [Fact]
    [Upstream("TextTable", "表头加各行，单元格里反引号是行内代码")]
    public Task Text_tables_render_inline_code() => WinFormsUi.Run(() =>
    {
        var table = new TextTableControl { Head = ["方法", "含义"], TextRows = [["`Has(值)`", "含有这个值"]], Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(table);

        var cells = table.Controls.OfType<TableSectionControl>().SelectMany(section => section.Controls.OfType<TableRowControl>()).SelectMany(row => row.Controls.OfType<TableCellControl>()).ToList();
        Assert.Equal(["方法", "含义"], cells.OfType<TableHeaderCellControl>().Select(cell => cell.Text));
        var first = cells.First(cell => cell is not TableHeaderCellControl);
        Assert.True(first.Nowrap); // 第一列不折行
        Assert.Equal("`Has(值)`", first.Controls.OfType<CodeTextControl>().Single().Source);
    });
}
