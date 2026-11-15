using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>FieldCompare：表格列出全部字段并标出改了的、列表只列改了的、变化量按好坏着色、没有改动时的提示。</summary>
public class FieldCompareTests
{
    sealed record Quote(decimal Price, int Quantity, string Supplier, string Status);

    static readonly StatusMap Statuses = new() { ["draft"] = new("草稿", StatusTone.Neutral), ["approved"] = new("已批准", StatusTone.Success) };

    static readonly CompareField[] Fields =
    [
        new("Price", "单价") { Format = new NumericFormat { Style = NumericStyle.Currency }, Better = CompareBetter.Lower },
        new("Quantity", "数量") { Better = CompareBetter.Higher },
        new("Supplier", "供应商"),
        new("Status", "状态") { Statuses = Statuses },
    ];

    static readonly Quote Before = new(1200, 10, "华辰", "draft");
    static readonly Quote After = new(1350, 8, "华辰", "approved");

    /// <summary>单元格、列表值里看得见的文字。</summary>
    static string TextOf(object? content) => content switch
    {
        null => "",
        string text => text,
        Text text => text.Text ?? "",
        StatusBadge badge => badge.Statuses!.Label(badge.Value!),
        ContentControl control => TextOf(control.Content),
        _ => "",
    };

    static List<TableRow> BodyRows(FieldCompare compare) => [.. ((Table)compare.Shown!).Sections.OfType<TableBody>().Single().Rows.Cast<TableRow>()];

    [Fact]
    [Upstream("FieldCompare", "表格：字段、原值、新值、变化四列；全部字段都列出，改了的行带 data-changed")]
    public Task Table_lists_every_field_and_marks_the_changed_ones() => HeadlessUi.Run(() =>
    {
        var compare = new FieldCompare { Fields = Fields, Before = Before, After = After };
        HeadlessUi.Show(compare);

        var table = (Table)compare.Shown!;
        var headers = table.Sections.OfType<TableHead>().Single().Rows.Cast<TableRow>().Single().Cells.Select(TextOf);
        Assert.Equal(["字段", "原值", "新值", "变化"], headers);
        var rows = BodyRows(compare);
        Assert.Equal([true, true, false, true], rows.Select(row => row.Classes.Contains("changed")));
        Assert.Equal(["单价", "¥1,200.00", "¥1,350.00", "+¥150.00（+12.5%）"], rows[0].Cells.Select(TextOf));
        Assert.Equal("已批准", TextOf(rows[3].Cells[2]));
    });

    [Fact]
    [Upstream("FieldCompare", "变化量按 better 着色：越低越好的涨了是 danger，越高越好的降了也是 danger")]
    public void Changes_are_toned_by_better()
    {
        var rows = FieldComparison.Diff(Fields, Before, After);
        Assert.Equal(TextTone.Danger, FieldComparison.Change(rows[0], "zh-CN", "CNY")!.Value.Tone);
        Assert.Equal(TextTone.Danger, FieldComparison.Change(rows[1], "zh-CN", "CNY")!.Value.Tone);
        var cheaper = FieldComparison.Diff(Fields, After, Before);
        Assert.Equal(TextTone.Success, FieldComparison.Change(cheaper[0], "zh-CN", "CNY")!.Value.Tone);
        Assert.Null(FieldComparison.Change(rows[2], "zh-CN", "CNY"));
    }

    [Fact]
    [Upstream("FieldCompare", "列表：只列改了的；原值是 del、新值是 ins，中间有给读屏的“改为”")]
    public Task List_shows_only_the_changes() => HeadlessUi.Run(() =>
    {
        var compare = new FieldCompare { Fields = Fields, Before = Before, After = After, Layout = FieldCompareLayout.List };
        HeadlessUi.Show(compare);

        Assert.Equal(["单价", "数量", "状态"], compare.Rows.Select(row => row.Field.Header));
        var changes = compare.GetLogicalDescendants().OfType<ValueChange>().ToList();
        Assert.Equal(("10", "8"), (TextOf(changes[1].Before), TextOf(changes[1].After)));
        var quantity = changes[1].GetLogicalParent() as Flex;
        Assert.Equal("-2（-20%）", TextOf(quantity!.Children[1]));
    });

    [Fact]
    [Upstream("FieldCompare", "onlyChanged 可以改默认；没有原记录（新建）时列表只有新值")]
    public Task Only_changed_overrides_the_default_and_a_new_record_has_no_before() => HeadlessUi.Run(() =>
    {
        var table = new FieldCompare { Fields = Fields, Before = Before, After = After, OnlyChanged = true };
        var created = new FieldCompare { Fields = Fields, Before = null, After = After, Layout = FieldCompareLayout.List };
        HeadlessUi.Show(new StackPanel { Children = { table, created } });

        Assert.Equal(3, BodyRows(table).Count);
        Assert.Equal(4, created.Rows.Count);
        Assert.All(created.GetLogicalDescendants().OfType<ValueChange>(), change => Assert.Null(change.Before));
    });

    [Fact]
    [Upstream("FieldCompare", "没有改动时显示“没有改动”；不是数值的改动不出“变化”一列")]
    public Task Nothing_changed_and_text_only_changes() => HeadlessUi.Run(() =>
    {
        var same = new FieldCompare { Fields = Fields, Before = Before, After = Before with { }, Layout = FieldCompareLayout.List };
        var text = new FieldCompare { Fields = Fields, Before = Before, After = Before with { Supplier = "恒远" } };
        HeadlessUi.Show(new StackPanel { Children = { same, text } });

        Assert.Equal("没有改动", TextOf(same.Shown));
        var headers = ((Table)text.Shown!).Sections.OfType<TableHead>().Single().Rows.Cast<TableRow>().Single().Cells.Select(TextOf);
        Assert.Equal(["字段", "原值", "新值"], headers);
    });
}
