using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Descriptions：每项是一组（名称是标签）、值在里面；跨列不超过栏数；auto 按宽度定栏数；窄于 32em 排成一列；复制按钮。</summary>
public class DescriptionsTests
{
    static Descriptions Of(Descriptions descriptions, params DescriptionItem[] items)
    {
        descriptions.Items.AddRange(items);
        return descriptions;
    }

    [Fact]
    [Upstream("Descriptions", "每项是 dl 里的 div > dt + dd；标题与部件属性")]
    public Task Items_are_labelled_groups_under_the_title() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions { Title = "订单信息", Columns = 2 },
            new DescriptionItem { Label = "订单号", Value = "A-1024" },
            new DescriptionItem { Label = "地址", Value = "上海", Span = 5 });
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { descriptions } });

        var title = descriptions.GetVisualDescendants().OfType<Text>().First(text => text.Name == DescriptionsParts.Title);
        Assert.Equal("订单信息", title.Text);
        var items = descriptions.GetVisualDescendants().OfType<Border>().Where(border => border.Name == DescriptionsParts.Item).ToList();
        Assert.Equal(2, items.Count);
        Assert.True(title.TranslatePoint(default, descriptions)!.Value.Y < items[0].TranslatePoint(default, descriptions)!.Value.Y); // 标题在前
        var first = items[0].Peer();
        Assert.Equal((AutomationControlType.Group, "订单号"), (first.GetAutomationControlType(), first.GetName()));
        Assert.Contains(items[0].GetVisualDescendants().OfType<Text>(), text => text.Text == "A-1024");
        Assert.Equal(2, descriptions.ColumnCount);
        Assert.Equal(2, descriptions.Cells[1].Span.Count); // 跨 5 列按栏数封顶
    });

    [Fact]
    [Upstream("Descriptions", "columns=\"auto\"：按宽度与 minItemWidth 算栏数，最多 maxColumns；跨列按算出的栏数封顶")]
    public Task Auto_columns_follow_the_width() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions { Columns = DescriptionsColumns.Auto, MinItemWidth = 200, MaxColumns = 3 },
            new DescriptionItem { Label = "编号", Value = "A-1" },
            new DescriptionItem { Label = "团队", Value = "研发" },
            new DescriptionItem { Label = "地址", Value = "上海", Span = 5 });
        var host = new StackPanel { Width = 450, Children = { descriptions } };
        var window = HeadlessUi.Show(host);
        Assert.Equal(2, descriptions.ColumnCount);
        Assert.Equal(2, descriptions.Cells[2].Span.Count);

        host.Width = 1000;
        HeadlessUi.Flush(window);
        Assert.Equal(3, descriptions.ColumnCount); // 最多 MaxColumns
        Assert.Equal(3, descriptions.Cells[2].Span.Count);
    });

    [Fact]
    public Task Fixed_columns_collapse_below_32em() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions { Columns = 3 }, new DescriptionItem { Label = "编号", Value = "A-1" });
        var host = new StackPanel { Width = 300, Children = { descriptions } };
        var window = HeadlessUi.Show(host);
        Assert.Equal(1, descriptions.ColumnCount); // 300 < 32em（448）

        host.Width = 600;
        HeadlessUi.Flush(window);
        Assert.Equal(3, descriptions.ColumnCount);
    });

    [Fact]
    [Upstream("Descriptions", "copyable：值后面有复制按钮，名称带标签，复制 copyText 或值的文字")]
    public Task Copyable_values_get_a_named_copy_button() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions(),
            new DescriptionItem { Label = "订单号", Value = "A-1024", Copyable = true },
            new DescriptionItem { Label = "收货地址", Value = new Text { Text = "上海浦东" }, Copyable = true, CopyText = "上海市浦东新区" });
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { descriptions } });

        var buttons = descriptions.GetVisualDescendants().OfType<CopyButton>().ToList();
        Assert.Equal(["复制订单号", "复制收货地址"], buttons.Select(AutomationProperties.GetName));
        Assert.Equal(["A-1024", "上海市浦东新区"], buttons.Select(button => button.Value));
    });

    [Fact]
    [Upstream("Descriptions", "Descriptions 的 inline 排法：不按栏分（没有 grid-template-columns）")]
    public Task Inline_layout_ignores_columns() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions { Layout = DescriptionsLayout.Inline, Columns = 3 },
            new DescriptionItem { Label = "主办", Value = "宋知行" }, new DescriptionItem { Label = "团队", Value = "研发" });
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { descriptions } });

        Assert.Equal(1, descriptions.ColumnCount);
        Assert.IsType<Flex>(descriptions.Cells[0].Parent); // 换行的一排，不是网格
        Assert.Equal(descriptions.Cells[0].Bounds.Y, descriptions.Cells[1].Bounds.Y); // 同一行
    });

    [Fact]
    public Task Empty_values_show_the_empty_placeholder() => HeadlessUi.Run(() =>
    {
        var descriptions = Of(new Descriptions(), new DescriptionItem { Label = "备注" }, new DescriptionItem { Label = "邮箱", Value = "" });
        HeadlessUi.Show(new StackPanel { Width = 600, Children = { descriptions } });

        var values = descriptions.GetVisualDescendants().OfType<Border>().Where(border => border.Name == DescriptionsParts.Value).ToList();
        Assert.All(values, value => Assert.Contains(value.GetVisualDescendants().OfType<Text>(), text => text.Text == "—"));
    });
}
