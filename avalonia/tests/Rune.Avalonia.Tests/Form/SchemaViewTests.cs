using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>SchemaView：按模型只读地显示值——选项显示文字、开关显示是否、人员与附件显示名字、对象展开、明细表带合计；权限与显示条件不显示的不列，分组各一块。</summary>
public class SchemaViewTests
{
    enum Kind
    {
        [Display(Name = "差旅")] Travel,
        [Display(Name = "招待")] Meal,
    }

    sealed class Address
    {
        [Display(Name = "城市")] public string City { get; set; } = "";
    }

    sealed class Line
    {
        [Display(Name = "项目")] public string Item { get; set; } = "";
        [Display(Name = "金额"), SchemaField(Sum = true)] public decimal Cost { get; set; }
    }

    sealed class Expense
    {
        [Display(Name = "标题")] public string Title { get; set; } = "";
        [Display(Name = "类型")] public Kind Kind { get; set; }
        [Display(Name = "成本中心"), SchemaField(Source = "centers")] public string Center { get; set; } = "";
        [Display(Name = "加急")] public bool Urgent { get; set; }
        [Display(Name = "天数"), SchemaField(Unit = "天")] public int Days { get; set; }
        [Display(Name = "备注")] public string? Memo { get; set; }
        [Display(Name = "宾客"), SchemaField(VisibleWhen = """{"field":"Kind","op":"eq","value":"Meal"}""")] public string? Guests { get; set; }
        [Display(Name = "经办人"), SchemaField(Control = SchemaControl.People)] public List<PeopleEntry> Owner { get; set; } = [];
        [Display(Name = "附件", GroupName = "附件"), SchemaField(Control = SchemaControl.File)] public List<UploadFile> Files { get; set; } = [];
        [Display(Name = "地址")] public Address Address { get; set; } = new();
        [Display(Name = "明细"), SchemaField(Control = SchemaControl.Table)] public List<Line> Lines { get; set; } = [];
    }

    static readonly Expense Values = new()
    {
        Title = "出差", Kind = Kind.Travel, Center = "rd", Urgent = true, Days = 3, Guests = "张三",
        Owner = [new("u1", "user", "李四")],
        Files = [new("f1", "发票.pdf", UploadStatus.Done) { Url = new Uri("https://example.com/files/1") }],
        Address = new() { City = "上海" },
        Lines = [new() { Item = "高铁", Cost = 0.1m }, new() { Item = "酒店", Cost = 0.2m }],
    };

    static SchemaView Render(IReadOnlyDictionary<string, SchemaFieldAccess>? access = null)
    {
        var view = new SchemaView { Values = Values, Access = access, Sources = new FormSources { Options = new Dictionary<string, OptionSource> { ["centers"] = new SchemaOption[] { new("rd", "研发中心") } } } };
        HeadlessUi.Show(view);
        return view;
    }

    /// <summary>标签 → 值（文字，或值是控件时里面的文字）。</summary>
    static Dictionary<string, object?> Pairs(SchemaView view) =>
        view.GetLogicalDescendants().OfType<Descriptions>().SelectMany(block => block.Items).ToDictionary(item => (string)item.Label!, item => item.Value);

    [Fact]
    [Upstream("SchemaView", "选项、开关、单位、人员、附件、对象显示成能读的文字；没有值显示占位")]
    public Task Values_are_shown_as_readable_text() => HeadlessUi.Run(() =>
    {
        var pairs = Pairs(Render());
        Assert.Equal(["出差", "差旅", "研发中心", "是", "3 天", "—", "李四", "上海"],
            new[] { "标题", "类型", "成本中心", "加急", "天数", "备注", "经办人", "地址 · 城市" }.Select(label => pairs[label]));
        // 附件是外部链接
        var link = Assert.IsType<Link>(Assert.IsType<Flex>(pairs["附件"]).Children.Single());
        Assert.Equal(("发票.pdf", new Uri("https://example.com/files/1"), true), (link.Content, link.Href, link.External));
    });

    [Fact]
    [Upstream("SchemaView", "明细表排成表格，写了 total 的列有精确合计")]
    public Task Lines_are_a_table_with_exact_totals() => HeadlessUi.Run(() =>
    {
        var table = Assert.IsType<Table>(Pairs(Render())["明细"]);
        Assert.Equal(2, table.Sections.OfType<TableBody>().Single().Rows.Count);
        var foot = ((TableRow)table.Sections.OfType<TableFoot>().Single().Rows[0]).Cells.OfType<TableCell>().Select(cell => cell.Content);
        Assert.Equal(["合计", "0.3"], foot);
    });

    [Fact]
    [Upstream("SchemaView", "visibleWhen 不满足的、access 隐藏的不列；分组各一块")]
    public Task Hidden_fields_are_not_listed_and_groups_are_blocks() => HeadlessUi.Run(() =>
    {
        var view = Render(new Dictionary<string, SchemaFieldAccess> { ["Days"] = SchemaFieldAccess.Hidden });
        var labels = Pairs(view).Keys;
        Assert.DoesNotContain("宾客", labels);
        Assert.DoesNotContain("天数", labels);
        var blocks = view.GetLogicalDescendants().OfType<Descriptions>().ToList();
        Assert.Equal([null, "附件"], blocks.Select(block => block.Title));
    });
}
