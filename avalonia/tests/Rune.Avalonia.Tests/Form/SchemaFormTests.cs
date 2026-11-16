using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>SchemaForm、SchemaFields：按模型生成标签与控件、分组与页签、可增删的行与明细表（合计、焦点）、换控件与 Pick / Omit、提交；字段的条件与权限；数据来源。</summary>
public class SchemaFormTests
{
    enum Category
    {
        [Display(Name = "机械件")] Machined,
        [Display(Name = "塑料件")] Plastic,
    }

    sealed class Contact
    {
        [Display(Name = "姓名"), Required] public string Name { get; set; } = "";
    }

    sealed class Supplier
    {
        [Display(Name = "公司全称"), Required, MinLength(2)] public string Company { get; set; } = "";
        [Display(Name = "供货品类")] public Category? Category { get; set; }
        [Display(Name = "年产能"), SchemaField(Unit = "件")] public int? Capacity { get; set; }
        [Display(Name = "已签保密协议")] public bool Agreed { get; set; }
        [Display(Name = "联系电话", GroupName = "联系人")] public string Phone { get; set; } = "";
        [Display(Name = "联系人")] public List<Contact> Contacts { get; set; } = [];
    }

    static (SchemaForm Form, Window Window) Render<T>(FormController<T> controller, Action<SchemaForm>? setup = null) where T : class
    {
        var form = new SchemaForm { Controller = controller, Width = 720 };
        setup?.Invoke(form);
        var window = HeadlessUi.Show(form);
        HeadlessUi.Flush(window);
        return (form, window);
    }

    static List<Field> Fields(Control root) => [.. root.GetLogicalDescendants().OfType<Field>().Where(field => field.IsEffectivelyVisible)];

    static Field FieldAt(Control root, string path) => root.GetLogicalDescendants().OfType<Field>().Single(field => field.Path == path);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("SchemaForm", "按 schema 生成标签与控件：文字、下拉、带单位的数字、开关；分组是带标题的一组；最后是重置与提交")]
    public Task Generates_labels_controls_groups_and_actions() => HeadlessUi.Run(() =>
    {
        var (form, _) = Render(new FormController<Supplier>(new Supplier()));
        var fields = Fields(form);
        Assert.Equal(["公司全称", "供货品类", "年产能", "已签保密协议", "联系电话"], fields.Select(field => field.Label));
        Assert.IsType<TextField>(fields[0].Content);
        var select = Assert.IsType<Select>(fields[1].Content);
        Assert.Equal(["机械件", "塑料件"], select.ItemsSource!.Cast<ListboxOption>().Select(option => option.Label));
        Assert.Equal("件", Assert.IsType<NumberField>(fields[2].Content).Suffix);
        Assert.IsType<Switch>(fields[3].Content);
        var sections = form.GetLogicalDescendants().OfType<FormSection>().Where(section => section.Name == SchemaFormParts.Section).ToList();
        Assert.Contains(sections, section => section.Title == "联系人");
        Assert.Equal(("重置", "提交"), ((string?)form.Part<Button>(SchemaFormParts.Reset).Content, (string?)form.Part<Button>(SchemaFormParts.Submit).Content));
    });

    [Fact]
    [Upstream("SchemaForm", "列表：添加一行出现这一行的字段与删除按钮，删掉后没有了")]
    public Task List_rows_can_be_added_and_removed() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Supplier>(new Supplier());
        var (form, window) = Render(controller);
        Click(form.Part<Button>(SchemaFormParts.AddRow));
        HeadlessUi.Flush(window);
        Assert.Equal("Contacts.0.Name", FieldAt(form, "Contacts.0.Name").Path);
        var remove = form.GetLogicalDescendants().OfType<IconButton>().Single(button => button.Name == SchemaFormParts.RemoveRow);
        Assert.Equal("删除第 1 个联系人", global::Avalonia.Automation.AutomationProperties.GetName(remove));
        Click(remove);
        HeadlessUi.Flush(window);
        Assert.DoesNotContain(form.GetLogicalDescendants().OfType<Field>(), field => field.Path == "Contacts.0.Name");
        Assert.Empty(controller.Values.Contacts);
    });

    [Fact]
    [Upstream("SchemaForm", "fields 换掉控件（仍然自动绑定），pick 只生成这几个并按它的顺序")]
    public Task Custom_controls_and_pick() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Supplier>(new Supplier());
        var (form, window) = Render(controller, form =>
        {
            form.FieldsView.Pick = ["Capacity", "Company"];
            form.FieldsView.Fields = new Dictionary<string, Func<SchemaField, Control>> { ["Company"] = _ => new Textarea() };
        });
        Assert.Equal(["年产能", "公司全称"], Fields(form).Select(field => field.Label));
        var area = Assert.IsType<Textarea>(FieldAt(form, "Company").Content);
        area.Text = "华辰精密";
        HeadlessUi.Flush(window);
        Assert.Equal("华辰精密", controller.Values.Company);
    });

    [Fact]
    [Upstream("SchemaForm", "omit 不生成这几个")]
    public Task Omit_leaves_fields_out() => HeadlessUi.Run(() =>
    {
        var (form, _) = Render(new FormController<Supplier>(new Supplier()), form => form.FieldsView.Omit = ["Phone", "Contacts"]);
        Assert.DoesNotContain(Fields(form), field => field.Path is "Phone");
        Assert.Null(form.GetLogicalDescendants().OfType<Button>().FirstOrDefault(button => button.Name == SchemaFormParts.AddRow));
    });

    [Fact]
    [Upstream("SchemaForm", "提交：没通过时不调用 onSubmit；填好后拿到解析后的值")]
    public Task Submit_validates_then_calls_on_submit() => HeadlessUi.Run(async () =>
    {
        Supplier? submitted = null;
        var controller = new FormController<Supplier>(new Supplier()) { OnSubmit = values => { submitted = values; return Task.CompletedTask; } };
        var (form, window) = Render(controller);
        Assert.False(await controller.SubmitAsync());
        Assert.Null(submitted);
        ((TextField)FieldAt(form, "Company").Content!).Text = "华辰精密";
        HeadlessUi.Flush(window);
        Assert.True(await controller.SubmitAsync());
        Assert.Equal("华辰精密", submitted!.Company);
    });

    sealed class Line
    {
        [Display(Name = "项目")] public string Item { get; set; } = "";
        [Display(Name = "金额"), SchemaField(Sum = true)] public decimal? Cost { get; set; }
        [Display(Name = "备注"), SchemaField(VisibleWhen = """{"field":"Kind","op":"eq","value":"rush"}""")] public string Note { get; set; } = "";
    }

    sealed class Request
    {
        [Display(Name = "类型")] public string Kind { get; set; } = "normal";
        [Display(Name = "原因"), SchemaField(VisibleWhen = """{"field":"Kind","op":"eq","value":"rush"}""")] public string Reason { get; set; } = "";
        [Display(Name = "说明"), SchemaField(RequiredWhen = """{"field":"Kind","op":"eq","value":"rush"}""")] public string Memo { get; set; } = "";
        [Display(Name = "锁定")] public bool Locked { get; set; }
        [Display(Name = "编号"), SchemaField(ReadonlyWhen = """{"field":"Locked","op":"eq","value":true}""")] public string Code { get; set; } = "";
        [Display(Name = "明细"), SchemaField(Control = SchemaControl.Table)] public List<Line> Lines { get; set; } = [];
    }

    [Fact]
    [Upstream("schema-conditions", "显示条件不成立时不渲染、不校验；成立时出现")]
    public Task Visible_when_hides_and_shows() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Request>(new Request());
        var (form, window) = Render(controller);
        Assert.False(FieldAt(form, "Reason").IsEffectivelyVisible);
        controller.SetValue("Kind", "rush");
        HeadlessUi.Flush(window);
        Assert.True(FieldAt(form, "Reason").IsEffectivelyVisible);
    });

    [Fact]
    [Upstream("schema-conditions", "必填条件成立时空着报“必填”，不成立时不报")]
    public Task Required_when_reports_blank_fields() => HeadlessUi.Run(async () =>
    {
        var controller = new FormController<Request>(new Request());
        Render(controller);
        Assert.True(await controller.ValidateAsync());
        controller.SetValue("Kind", "rush");
        Assert.False(await controller.ValidateAsync());
        Assert.Equal("必填", controller.Errors["Memo"]);
    });

    [Fact]
    [Upstream("schema-conditions", "只读条件成立时控件只读")]
    public Task Readonly_when_makes_fields_read_only() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Request>(new Request());
        var (form, window) = Render(controller);
        Assert.False(FieldAt(form, "Code").IsReadOnly);
        controller.SetValue("Locked", true);
        HeadlessUi.Flush(window);
        Assert.True(FieldAt(form, "Code").IsReadOnly);
    });

    [Fact]
    [Upstream("schema-conditions", "权限：hidden 不显示，required 必填，readonly 只读；明细表的列没写时跟着明细表，只读的明细表不能增删行")]
    public Task Access_hides_requires_and_locks() => HeadlessUi.Run(async () =>
    {
        var controller = new FormController<Request>(new Request { Lines = [new() { Item = "高铁" }] })
        {
            Access = _ => new Dictionary<string, SchemaFieldAccess> { ["Code"] = SchemaFieldAccess.Hidden, ["Memo"] = SchemaFieldAccess.Required, ["Lines"] = SchemaFieldAccess.Readonly },
        };
        var (form, _) = Render(controller);
        Assert.False(FieldAt(form, "Code").IsEffectivelyVisible);
        Assert.True(FieldAt(form, "Lines.0.Item").IsReadOnly);
        Assert.DoesNotContain(form.GetLogicalDescendants().OfType<Button>(), button => button.Name == SchemaFormParts.AddRow);
        Assert.False(await controller.ValidateAsync());
        Assert.Equal(["Memo"], controller.Errors.Keys);
    });

    [Fact]
    [Upstream("schema-table", "表头是列标签，一行一条；金额列在表底精确合计")]
    public Task Table_rows_and_exact_totals() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Request>(new Request { Lines = [new() { Item = "高铁", Cost = 0.1m }, new() { Item = "酒店", Cost = 0.2m }] });
        var (form, _) = Render(controller);
        var table = form.Part<Table>(SchemaFormParts.Table);
        // 备注的显示条件不成立：整列不出
        Assert.Equal(["项目", "金额"], ((TableRow)table.Sections.OfType<TableHead>().Single().Rows[0]).Cells.OfType<TableHeaderCell>().Select(cell => cell.Content).OfType<string>());
        Assert.Equal(2, table.Sections.OfType<TableBody>().Single().Rows.Count);
        Assert.Equal(["合计", "0.3"], ((TableRow)table.Sections.OfType<TableFoot>().Single().Rows[0]).Cells.OfType<TableCell>().Select(cell => cell.Content).Take(2));
    });

    [Fact]
    [Upstream("schema-table", "添加、删除行，合计跟着变；没有行时显示暂无数据")]
    public Task Table_rows_add_remove_and_totals_follow() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Request>(new Request { Lines = [new() { Item = "高铁", Cost = 1m }] });
        var (form, window) = Render(controller);
        Click(form.Part<Button>(SchemaFormParts.AddRow));
        HeadlessUi.Flush(window);
        controller.SetValue("Lines.1.Cost", 2.5m);
        HeadlessUi.Flush(window);
        var foot = () => ((TableRow)form.Part<Table>(SchemaFormParts.Table).Sections.OfType<TableFoot>().Single().Rows[0]).Cells.OfType<TableCell>().ElementAt(1).Content;
        Assert.Equal("3.5", foot());
        foreach (var _ in Enumerable.Range(0, 2))
        {
            Click(form.GetLogicalDescendants().OfType<IconButton>().First(button => button.Name == SchemaFormParts.RemoveRow));
            HeadlessUi.Flush(window);
        }
        var body = form.Part<Table>(SchemaFormParts.Table).Sections.OfType<TableBody>().Single();
        Assert.Equal("暂无数据", ((TableRow)body.Rows[0]).Cells.OfType<TableCell>().Single().Content);
    });

    [Fact]
    [Upstream("schema-table", "添加后焦点到新行的第一个控件；删掉一行后焦点到相邻的行，没有行了到“添加”按钮")]
    public Task Focus_follows_added_and_removed_rows() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Request>(new Request());
        var (form, window) = Render(controller);
        Click(form.Part<Button>(SchemaFormParts.AddRow));
        HeadlessUi.Flush(window);
        Assert.True(FieldAt(form, "Lines.0.Item").IsKeyboardFocusWithin);
        Click(form.GetLogicalDescendants().OfType<IconButton>().Single(button => button.Name == SchemaFormParts.RemoveRow));
        HeadlessUi.Flush(window);
        Assert.True(form.Part<Button>(SchemaFormParts.AddRow).IsFocused);
    });

    sealed class Trip
    {
        [Display(Name = "事由")] public string Title { get; set; } = "";
        [Display(Name = "目的地"), SchemaField(Tab = "行程")] public string City { get; set; } = "";
        [Display(Name = "预支借款"), SchemaField(Tab = "费用")] public bool Advance { get; set; }
    }

    [Fact]
    [Upstream("schema-tabs", "写了 tab 的字段进标签页，没写的在页签前面；默认是第一个页签")]
    public Task Tabs_hold_their_fields() => HeadlessUi.Run(() =>
    {
        var (form, _) = Render(new FormController<Trip>(new Trip()));
        var tabs = form.GetLogicalDescendants().OfType<Tabs>().Single();
        Assert.Equal("行程", tabs.Value);
        Assert.Equal(["行程", "费用"], tabs.GetLogicalDescendants().OfType<Tab>().Select(tab => tab.Value));
        Assert.Null(FieldAt(form, "Title").FindLogicalAncestorOfType<TabPanel>());
        Assert.Equal("费用", FieldAt(form, "Advance").FindLogicalAncestorOfType<TabPanel>()!.Value);
    });

    sealed class Purchase
    {
        [Display(Name = "成本中心"), SchemaField(Source = "centers")] public string Center { get; set; } = "";
        [Display(Name = "经办人"), SchemaField(Control = SchemaControl.People)] public List<PeopleEntry> Owner { get; set; } = [];
    }

    [Fact]
    [Upstream("SchemaFields", "SchemaFields 的 sources 覆盖登记的同名来源")]
    public Task Sources_override_registered_ones() => HeadlessUi.Run(() =>
    {
        var people = new PeopleSource((_, _) => Task.FromResult<IReadOnlyList<PeopleEntry>>([]));
        var form = new SchemaForm { Controller = new FormController<Purchase>(new Purchase()), Width = 720 };
        form.FieldsView.Sources = new FormSources { Options = new Dictionary<string, OptionSource> { ["centers"] = new SchemaOption[] { new("rd", "研发中心（本页）") } } };
        var provider = new Provider
        {
            FormSources = new FormSources { Options = new Dictionary<string, OptionSource> { ["centers"] = new SchemaOption[] { new("rd", "研发中心") } }, People = people },
            Child = form,
        };
        HeadlessUi.Show(provider);
        var select = Assert.IsType<Select>(FieldAt(form, "Center").Content);
        Assert.Equal("研发中心（本页）", select.ItemsSource!.Cast<ListboxOption>().Single().Label);
        Assert.Same(people, Assert.IsType<PeoplePicker>(FieldAt(form, "Owner").Content).Source);
    });

    [Fact]
    [Upstream("schema-sources", "人员控件没有登记 people 时报错，说清楚要在哪里给")]
    public void People_without_a_source_throws()
    {
        var field = SchemaModel.Of<Purchase>().Single(item => item.Key == "Owner");
        var error = Assert.Throws<InvalidOperationException>(() => SchemaFields.ControlOf(field, new FormSources()));
        Assert.Contains("FormSources", error.Message);
    }

    enum OrderStatus
    {
        [Display(Name = "草稿")] Draft,
        [Display(Name = "已下单")] Ordered,
    }

    sealed class OrderFilters
    {
        [Display(Name = "关键字", Prompt = "单号或供应商")] public string Keyword { get; set; } = "";
        [Display(Name = "状态")] public OrderStatus? Status { get; set; }
        [Display(Name = "只看加急"), SchemaField(Control = SchemaControl.Checkbox)] public bool Urgent { get; set; }
        [Display(Name = "金额")] public decimal? Amount { get; set; }
        public Address Address { get; set; } = new();
    }

    sealed class Address
    {
        public string City { get; set; } = "";
    }

    [Fact]
    [Upstream("schema-filters", "按 pick 的顺序给出 Field，值读写传进来的对象；文字可以清空；嵌套对象不生成")]
    public Task Filters_read_and_write_the_object() => HeadlessUi.Run(() =>
    {
        var values = new OrderFilters();
        var fields = SchemaFields.Filters(typeof(OrderFilters), values, omit: ["Amount"]);
        Assert.Equal(["关键字", "状态", "只看加急"], fields.Select(field => field.Label));
        var text = Assert.IsType<TextField>(fields[0].Content);
        Assert.Equal(("单号或供应商", true), (text.PlaceholderText, text.Clearable));
        text.Text = "PO-1031";
        Assert.Equal("PO-1031", values.Keyword);
        var checkbox = Assert.IsType<Checkbox>(fields[2].Content);
        checkbox.Toggle();
        Assert.True(values.Urgent);
        var select = Assert.IsType<Select>(fields[1].Content);
        select.Value = "Ordered";
        Assert.Equal(OrderStatus.Ordered, values.Status);
    });

    [Fact]
    [Upstream("schema-filters", "fields 换掉某个条件的控件")]
    public Task Filters_can_take_custom_controls() => HeadlessUi.Run(() =>
    {
        var custom = new TextBlock { Text = "自定义" };
        var fields = SchemaFields.Filters(typeof(OrderFilters), new OrderFilters(), pick: ["Status"], fields: new Dictionary<string, Func<SchemaField, Control>> { ["Status"] = _ => custom });
        var field = Assert.Single(fields);
        Assert.Equal(("状态", (Control?)custom), (field.Label, field.Content));
    });
}
