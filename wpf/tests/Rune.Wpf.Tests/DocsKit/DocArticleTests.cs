using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>DocArticle、DocSection、TextTable：文章的标题（h1）、简介、lead 与正文；一节的标题（h2）带 id；文字表格的反引号是行内代码。</summary>
public class DocArticleTests
{
    static void Show(FrameworkElement content)
    {
        var host = new StackPanel { Width = 800, Children = { content } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("DocArticle", "article 里有 h1、简介、lead 与正文；一节的 h2 带 id")]
    public Task Articles_have_a_title_description_lead_and_sections() => WpfUi.Run(() =>
    {
        var section = new DocSection { SectionId = "install", Title = "安装", Content = { new Paragraph { Text = "运行命令" } } };
        var article = new DocArticle { Title = "快速开始", Description = "两分钟跑起来", Lead = new Text { Text = "import" }, Content = { section } };
        Show(article);

        var title = WpfUi.Descendants(article).OfType<H1>().Single();
        Assert.Equal(("快速开始", AutomationHeadingLevel.Level1), (title.Text, AutomationProperties.GetHeadingLevel(title)));
        var description = WpfUi.Descendants(article).OfType<Paragraph>().First(paragraph => paragraph.Text == "两分钟跑起来");
        Assert.Equal(TextTone.Muted, description.Tone);
        Assert.Contains(WpfUi.Descendants(article).OfType<Text>(), text => text.Text == "import");
        Assert.Equal(("安装", AutomationHeadingLevel.Level2, "install"), (section.TitlePart.Text, AutomationProperties.GetHeadingLevel(section.TitlePart), section.SectionId));
        Assert.Equal(15, article.FontSize); // 长文阅读比界面文字大一号
        var gap = section.TranslatePoint(default, article).Y - description.TranslatePoint(default, article).Y - description.ActualHeight;
        Assert.True(gap >= 48 - 1, $"正文与头部之间 3rem，实际 {gap}");
    });

    [Fact]
    [Upstream("TextTable", "表头加各行，单元格里反引号是行内代码")]
    public Task Text_tables_render_inline_code() => WpfUi.Run(() =>
    {
        var table = new TextTable { Head = ["方法", "含义"], TextRows = [["`Has(值)`", "含有这个值"]] };
        Show(table);

        Assert.Equal(["方法", "含义"], WpfUi.Descendants(table).OfType<TableHeaderCell>().Select(cell => cell.Content as string));
        var cells = WpfUi.Descendants(table).OfType<TableCell>().Where(cell => cell is not TableHeaderCell).ToList();
        Assert.True(cells[0].Nowrap); // 第一列不折行
        var first = (CodeText)cells[0].Content!;
        Assert.Single(first.Inlines, inline => inline is Code);
    });
}
