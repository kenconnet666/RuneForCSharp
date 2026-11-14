using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>DocArticle、DocSection、TextTable：文章的标题（h1）、简介、lead 与正文；一节的标题（h2）带 id；文字表格的反引号是行内代码。</summary>
public class DocArticleTests
{
    [Fact]
    [Upstream("DocArticle", "article 里有 h1、简介、lead 与正文；一节的 h2 带 id")]
    public Task Articles_have_a_title_description_lead_and_sections() => HeadlessUi.Run(() =>
    {
        var section = new DocSection { SectionId = "install", Title = "安装", Content = { new Paragraph { Text = "运行命令" } } };
        var article = new DocArticle { Title = "快速开始", Description = "两分钟跑起来", Lead = new Text { Text = "import" }, Content = { section } };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { article } });

        var title = article.GetVisualDescendants().OfType<H1>().Single();
        Assert.Equal(("快速开始", 1), (title.Text, AutomationProperties.GetHeadingLevel(title)));
        var description = article.GetVisualDescendants().OfType<Paragraph>().First(paragraph => paragraph.Name == DocArticleParts.Description);
        Assert.Equal(("两分钟跑起来", TextTone.Muted), (description.Text, description.Tone));
        Assert.Contains(article.GetVisualDescendants().OfType<Text>(), text => text.Text == "import");
        Assert.Equal(("安装", 2, "install"), (section.TitlePart.Text, AutomationProperties.GetHeadingLevel(section.TitlePart), section.SectionId));
        Assert.Equal(15, article.GetValue(TextElement.FontSizeProperty)); // 长文阅读比界面文字大一号
        Assert.True(section.TranslatePoint(default, article)!.Value.Y >= description.TranslatePoint(default, article)!.Value.Y + description.Bounds.Height + 48 - 1); // 正文与头部之间 3rem
    });

    [Fact]
    [Upstream("TextTable", "表头加各行，单元格里反引号是行内代码")]
    public Task Text_tables_render_inline_code() => HeadlessUi.Run(() =>
    {
        var table = new TextTable { Head = ["方法", "含义"], TextRows = [["`Has(值)`", "含有这个值"]] };
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { table } });

        Assert.Equal(["方法", "含义"], table.GetVisualDescendants().OfType<TableHeaderCell>().Select(cell => cell.Content as string));
        var cells = table.GetVisualDescendants().OfType<TableCell>().Where(cell => cell is not TableHeaderCell).ToList();
        Assert.True(cells[0].Nowrap); // 第一列不折行
        var first = (CodeText)cells[0].Content!;
        Assert.IsType<Code>(Assert.Single(first.Inlines!));
    });
}
