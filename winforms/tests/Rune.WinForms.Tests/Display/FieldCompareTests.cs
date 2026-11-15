namespace Rune.WinForms.Tests;

/// <summary>FieldCompareControl：表格列出全部字段并标出改了的、列表只列改了的、变化量按好坏着色、没有改动时的提示。</summary>
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

    static Form Show(params Control[] controls)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        stack.Controls.AddRange(controls);
        host.Controls.Add(stack);
        return WinFormsUi.Show(host);
    }

    /// <summary>单元格里看得见的文字：自己的 Text，或里面的文字控件、状态的名字。</summary>
    static string TextOf(Control cell) => cell.Controls.Count == 0 ? cell.Text : cell.Controls[0] switch
    {
        StatusBadgeControl badge => badge.Statuses!.Label(badge.Value!),
        var child => child.Text,
    };

    static List<TableRowControl> BodyRows(FieldCompareControl compare) =>
        [.. ((TableControl)compare.Shown!).Controls.OfType<TableBodyControl>().Single().Controls.Cast<TableRowControl>()];

    static IEnumerable<string> Headers(FieldCompareControl compare) =>
        ((TableControl)compare.Shown!).Controls.OfType<TableHeadControl>().Single().Controls[0].Controls.Cast<Control>().Select(cell => cell.Text);

    [Fact]
    [Upstream("FieldCompare", "表格：字段、原值、新值、变化四列；全部字段都列出，改了的行带 data-changed")]
    public Task Table_lists_every_field_and_marks_the_changed_ones() => WinFormsUi.Run(() =>
    {
        var compare = new FieldCompareControl { Fields = Fields, Before = Before, After = After };
        using var form = Show(compare);

        Assert.Equal(["字段", "原值", "新值", "变化"], Headers(compare));
        var rows = BodyRows(compare);
        Assert.Equal([true, true, false, true], rows.Select(row => (string?)row.Tag == "changed"));
        Assert.Equal(["单价", "¥1,200.00", "¥1,350.00", "+¥150.00（+12.5%）"], rows[0].Controls.Cast<Control>().Select(TextOf));
        Assert.Equal("已批准", TextOf(rows[3].Controls[2]));
    });

    [Fact]
    [Upstream("FieldCompare", "变化量按 better 着色：越低越好的涨了是 danger，越高越好的降了也是 danger")]
    public void Changes_are_toned_by_better()
    {
        var rows = FieldComparison.Diff(Fields, Before, After);
        Assert.Equal(TextTone.Danger, FieldComparison.Change(rows[0], "zh-CN", "CNY")!.Value.Tone);
        Assert.Equal(TextTone.Danger, FieldComparison.Change(rows[1], "zh-CN", "CNY")!.Value.Tone);
        Assert.Equal(TextTone.Success, FieldComparison.Change(FieldComparison.Diff(Fields, After, Before)[0], "zh-CN", "CNY")!.Value.Tone);
        Assert.Null(FieldComparison.Change(rows[2], "zh-CN", "CNY"));
    }

    [Fact]
    [Upstream("FieldCompare", "列表：只列改了的；原值是 del、新值是 ins，中间有给读屏的“改为”")]
    public Task List_shows_only_the_changes() => WinFormsUi.Run(() =>
    {
        var compare = new FieldCompareControl { Fields = Fields, Before = Before, After = After, CompareLayout = FieldCompareLayout.List };
        using var form = Show(compare);

        Assert.Equal(["单价", "数量", "状态"], compare.Rows.Select(row => row.Field.Header));
        var values = compare.Shown!.Controls.OfType<FlexControl>().ToList();
        var quantity = values[1];
        var change = (ValueChangeControl)quantity.Controls[0];
        Assert.Equal(("10", "8"), (change.Before, change.After));
        Assert.Equal("-2（-20%）", quantity.Controls[1].Text);
        Assert.Equal(("草稿", "已批准"), (((ValueChangeControl)values[2].Controls[0]).Before, ((ValueChangeControl)values[2].Controls[0]).After));
    });

    [Fact]
    [Upstream("FieldCompare", "onlyChanged 可以改默认；没有原记录（新建）时列表只有新值")]
    public Task Only_changed_overrides_the_default_and_a_new_record_has_no_before() => WinFormsUi.Run(() =>
    {
        var table = new FieldCompareControl { Fields = Fields, Before = Before, After = After, OnlyChanged = true };
        var created = new FieldCompareControl { Fields = Fields, Before = null, After = After, CompareLayout = FieldCompareLayout.List };
        using var form = Show(table, created);

        Assert.Equal(3, BodyRows(table).Count);
        Assert.Equal(4, created.Rows.Count);
        Assert.All(created.Shown!.Controls.OfType<FlexControl>(), value => Assert.Null(((ValueChangeControl)value.Controls[0]).Before));
    });

    [Fact]
    [Upstream("FieldCompare", "没有改动时显示“没有改动”；不是数值的改动不出“变化”一列")]
    public Task Nothing_changed_and_text_only_changes() => WinFormsUi.Run(() =>
    {
        var same = new FieldCompareControl { Fields = Fields, Before = Before, After = Before with { }, CompareLayout = FieldCompareLayout.List };
        var text = new FieldCompareControl { Fields = Fields, Before = Before, After = Before with { Supplier = "恒远" } };
        using var form = Show(same, text);

        Assert.Equal("没有改动", same.Shown!.Text);
        Assert.Equal(["字段", "原值", "新值"], Headers(text));
    });
}
