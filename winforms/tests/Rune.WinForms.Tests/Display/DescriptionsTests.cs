namespace Rune.WinForms.Tests;

/// <summary>DescriptionsControl：每项是一组（名称是标签、描述是值）；跨列不超过栏数；auto 按宽度定栏数；窄于 32em 排成一列；复制按钮。</summary>
public class DescriptionsTests
{
    static DescriptionsControl Of(DescriptionsControl descriptions, params DescriptionItem[] items)
    {
        foreach (var item in items) descriptions.Items.Add(item);
        return descriptions;
    }

    static float Em(Control control) => control.Font.SizeInPoints * control.DeviceDpi / 72f;

    [Fact]
    [Upstream("Descriptions", "每项是 dl 里的 div > dt + dd；标题与部件属性")]
    public Task Items_are_labelled_groups_under_the_title() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { Title = "订单信息", Columns = 2, Dock = DockStyle.Top },
            new DescriptionItem { Label = "订单号", Value = "A-1024" },
            new DescriptionItem { Label = "地址", Value = "上海", Span = 5 });
        using var form = WinFormsUi.Show(descriptions);
        form.Width = (int)(40 * Em(descriptions));
        WinFormsUi.Pump();

        var cells = descriptions.Cells;
        Assert.Equal(2, cells.Count);
        Assert.True(cells[0].Parent!.Parent!.Top > 0); // 标题在列表上面
        Assert.Equal((AccessibleRole.Grouping, "订单号", "A-1024"), (cells[0].AccessibilityObject.Role, cells[0].AccessibilityObject.Name, cells[0].AccessibilityObject.Description));
        Assert.Equal(2, descriptions.ColumnCount);
        Assert.Equal(2, descriptions.SpanOf(cells[1])); // 跨 5 列按栏数封顶
    });

    [Fact]
    [Upstream("Descriptions", "columns=\"auto\"：按宽度与 minItemWidth 算栏数，最多 maxColumns；跨列按算出的栏数封顶")]
    public Task Auto_columns_follow_the_width() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { Columns = DescriptionsColumns.Auto, MinItemWidth = 200, MaxColumns = 3, Dock = DockStyle.Top },
            new DescriptionItem { Label = "编号", Value = "A-1" },
            new DescriptionItem { Label = "团队", Value = "研发" },
            new DescriptionItem { Label = "地址", Value = "上海", Span = 5 });
        using var form = WinFormsUi.Show(descriptions);
        var scale = descriptions.DeviceDpi / 96f;
        descriptions.Dock = DockStyle.None;
        descriptions.Width = (int)(450 * scale);
        WinFormsUi.Pump();
        Assert.Equal(2, descriptions.ColumnCount);
        Assert.Equal(2, descriptions.SpanOf(descriptions.Cells[2]));

        descriptions.Width = (int)(1000 * scale);
        WinFormsUi.Pump();
        Assert.Equal(3, descriptions.ColumnCount); // 最多 MaxColumns
        Assert.Equal(3, descriptions.SpanOf(descriptions.Cells[2]));
    });

    [Fact]
    public Task Fixed_columns_collapse_below_32em() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { Columns = 3 }, new DescriptionItem { Label = "编号", Value = "A-1" });
        using var form = WinFormsUi.Show(descriptions);
        descriptions.Width = (int)(20 * Em(descriptions));
        WinFormsUi.Pump();
        Assert.Equal(1, descriptions.ColumnCount);

        descriptions.Width = (int)(40 * Em(descriptions));
        WinFormsUi.Pump();
        Assert.Equal(3, descriptions.ColumnCount);
    });

    [Fact]
    [Upstream("Descriptions", "copyable：值后面有复制按钮，名称带标签，复制 copyText 或值的文字")]
    public Task Copyable_values_get_a_named_copy_button() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { Dock = DockStyle.Top },
            new DescriptionItem { Label = "订单号", Value = "A-1024", Copyable = true },
            new DescriptionItem { Label = "收货地址", Value = new TextControl { Text = "上海浦东" }, Copyable = true, CopyText = "上海市浦东新区" });
        using var form = WinFormsUi.Show(descriptions);

        var buttons = descriptions.Cells.SelectMany(cell => cell.Controls.OfType<CopyButtonControl>()).ToList();
        Assert.Equal(["复制订单号", "复制收货地址"], buttons.Select(button => button.AccessibleName));
        Assert.Equal(["A-1024", "上海市浦东新区"], buttons.Select(button => button.Value));
    });

    [Fact]
    [Upstream("Descriptions", "Descriptions 的 inline 排法：不按栏分（没有 grid-template-columns）")]
    public Task Inline_layout_ignores_columns() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { ItemLayout = DescriptionsLayout.Inline, Columns = 3, Dock = DockStyle.Top },
            new DescriptionItem { Label = "主办", Value = "宋知行" }, new DescriptionItem { Label = "团队", Value = "研发" });
        using var form = WinFormsUi.Show(descriptions);

        Assert.Equal(1, descriptions.ColumnCount);
        Assert.IsType<FlexControl>(descriptions.Cells[0].Parent); // 换行的一排，不是网格
        Assert.Equal(descriptions.Cells[0].Top, descriptions.Cells[1].Top); // 同一行
        Assert.True(descriptions.Cells[1].Left > descriptions.Cells[0].Right);
    });

    [Fact]
    public Task Empty_values_show_the_empty_placeholder() => WinFormsUi.Run(() =>
    {
        var descriptions = Of(new DescriptionsControl { Dock = DockStyle.Top }, new DescriptionItem { Label = "备注" }, new DescriptionItem { Label = "邮箱", Value = "" });
        using var form = WinFormsUi.Show(descriptions);

        Assert.Equal(["—", "—"], descriptions.Cells.Select(cell => cell.AccessibilityObject.Description));
    });
}
