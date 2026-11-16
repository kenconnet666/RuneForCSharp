using System.ComponentModel.DataAnnotations;

namespace Rune.Avalonia.Tests;

/// <summary>SchemaModel：从模型推出字段（控件按类型选、Display 与 SchemaField 特性的标签与选项、嵌套与列表）；字段此刻的样子（权限、条件）；逐个走字段；条件构造器的字段；明细表的合计。</summary>
public class SchemaModelTests
{
    enum Category
    {
        [Display(Name = "机械件")] Machined,
        Plastic,
    }

    enum Level { A, B }

    enum Tag { Urgent, Export }

    sealed class Address
    {
        [Display(Name = "城市")] public string City { get; set; } = "";
    }

    sealed class Contact
    {
        [Display(Name = "姓名")] public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    sealed class Supplier
    {
        [Display(Name = "公司全称", Prompt = "营业执照上的名称"), MinLength(2)] public string Company { get; set; } = "";
        [Display(Name = "供货品类")] public Category? Category { get; set; }
        [Display(Name = "等级"), SchemaField(Control = SchemaControl.Radio)] public Level Level { get; set; }
        [Display(Name = "金额"), SchemaField(Unit = "元")] public decimal? Amount { get; set; }
        public int? Days { get; set; }
        public bool Agreed { get; set; }
        [Display(Name = "标记")] public List<Tag> Tags { get; set; } = [];
        [Display(Name = "开始", GroupName = "交付")] public DateOnly? Start { get; set; }
        [Display(Name = "时间"), SchemaField(Group = "交付")] public DateTime? At { get; set; }
        [Display(Name = "说明"), StringLength(500), SchemaField(Span = SchemaFieldAttribute.Full)] public string Note { get; set; } = "";
        [Display(Name = "地址")] public Address Address { get; set; } = new();
        [Display(Name = "联系人")] public List<Contact> Contacts { get; set; } = [];
        [SchemaOption("normal", "普通"), SchemaOption("rush")] public string Mode { get; set; } = "normal";
    }

    static SchemaField ByKey(string key) => SchemaModel.Of<Supplier>().Single(field => field.Key == key);

    [Fact]
    [Upstream("schema-fields", "按类型选控件，meta 写了 control 时用它")]
    public void Controls_follow_types_and_the_attribute()
    {
        Assert.Equal(
        [
            ("Company", SchemaControl.Text), ("Category", SchemaControl.Select), ("Level", SchemaControl.Radio), ("Amount", SchemaControl.Number),
            ("Days", SchemaControl.Number), ("Agreed", SchemaControl.Switch), ("Tags", SchemaControl.Multiselect), ("Start", SchemaControl.Date),
            ("At", SchemaControl.Datetime), ("Note", SchemaControl.Textarea), ("Address", SchemaControl.Object), ("Contacts", SchemaControl.List), ("Mode", SchemaControl.Select),
        ], SchemaModel.Of<Supplier>().Select(field => (field.Key, field.Control)));
    }

    [Fact]
    [Upstream("schema-fields", "标签、占位、单位、分组、栏宽取自 meta；没写标题时是键")]
    public void Labels_placeholders_units_groups_and_spans()
    {
        Assert.Equal(("公司全称", "营业执照上的名称"), (ByKey("Company").Label, ByKey("Company").Placeholder));
        Assert.Equal(("金额", "元"), (ByKey("Amount").Label, ByKey("Amount").Unit));
        Assert.Equal("Days", ByKey("Days").Label);
        Assert.Equal(("交付", "交付"), (ByKey("Start").Group, ByKey("At").Group));
        Assert.Equal(SchemaFieldAttribute.Full, ByKey("Note").Span);
    }

    [Fact]
    [Upstream("schema-fields", "枚举的可选值：meta 的 options 是显示文字，没写的显示值本身；数组按元素的枚举")]
    public void Options_come_from_enums_and_attributes()
    {
        Assert.Equal([new SchemaOption("Machined", "机械件"), new SchemaOption("Plastic", "Plastic")], ByKey("Category").Options);
        Assert.Equal(["Urgent", "Export"], ByKey("Tags").Options!.Select(option => option.Value));
        Assert.Equal([new SchemaOption("normal", "普通"), new SchemaOption("rush", "rush")], ByKey("Mode").Options);
    }

    [Fact]
    [Upstream("schema-fields", "嵌套对象与对象的数组带上各自的字段")]
    public void Nested_objects_and_lists_carry_their_fields()
    {
        Assert.Equal(["城市"], ByKey("Address").Fields!.Select(field => field.Label));
        Assert.Equal([("Name", "姓名"), ("Phone", "Phone")], ByKey("Contacts").Fields!.Select(field => (field.Key, field.Label)));
        Assert.IsType<Contact>(SchemaModel.NewItem(ByKey("Contacts")));
    }

    [Fact]
    [Upstream("schema-fields", "decimalSchema 给了 type、bigintSchema 时记下值类型（包了 meta、optional 也认得）：控件一开始为空时按它写回")]
    public void Numbers_record_their_value_type()
    {
        Assert.Equal([typeof(decimal), typeof(int)], new[] { ByKey("Amount"), ByKey("Days") }.Select(field => field.ValueType));
        // 不可空的选择、数字、日期不写就没有值：算必填；可空的不算
        Assert.Equal([true, false, false], new[] { ByKey("Level"), ByKey("Amount"), ByKey("Category") }.Select(field => field.Required));
    }

    [Fact]
    [Upstream("schema-fields", "不是对象的 schema 没有字段")]
    public void Scalar_types_have_no_fields() => Assert.Empty(SchemaModel.Of<string>());

    sealed class Line
    {
        public string Part { get; set; } = "";
        [SchemaField(Sum = true, Unit = "元")] public decimal? Amount { get; set; }
        [SchemaField(ReadonlyWhen = """{"field":"Locked","op":"eq","value":true}""")] public string Note { get; set; } = "";
    }

    sealed class Request
    {
        public string Kind { get; set; } = "normal";
        public bool Locked { get; set; }
        [SchemaField(VisibleWhen = """{"field":"Kind","op":"eq","value":"rush"}""", RequiredWhen = """{"field":"Kind","op":"eq","value":"rush"}""")]
        public string Reason { get; set; } = "";
        [SchemaField(Control = SchemaControl.Table)] public List<Line> Lines { get; set; } = [];
    }

    [Fact]
    public void State_and_walk_follow_access_and_conditions()
    {
        var request = new Request { Lines = [new() { Part = "P-1", Amount = 1.5m }, new() { Part = "P-2", Amount = 2.25m }] };
        var fields = SchemaModel.Of<Request>();
        var seen = new Dictionary<string, SchemaFieldState>();
        SchemaModel.Walk(fields, request, new Dictionary<string, SchemaFieldAccess> { ["Lines.Part"] = SchemaFieldAccess.Readonly }, (path, _, state) => seen[path] = state);
        // 显示条件不成立：不显示、只读、不必填
        Assert.Equal(new SchemaFieldState(false, true, false), seen["Reason"]);
        // 明细表逐行走，权限按去掉行号的路径找；列的条件先读同一行、再读顶层
        Assert.Equal(new SchemaFieldState(true, true, false), seen["Lines.1.Part"]);
        Assert.Equal(new SchemaFieldState(true, false, false), seen["Lines.0.Note"]);

        request.Kind = "rush";
        request.Locked = true;
        seen.Clear();
        SchemaModel.Walk(fields, request, new Dictionary<string, SchemaFieldAccess> { ["Lines"] = SchemaFieldAccess.Readonly }, (path, _, state) => seen[path] = state);
        Assert.Equal(new SchemaFieldState(true, false, true), seen["Reason"]);
        // 列没写权限时跟着明细表
        Assert.True(seen["Lines.0.Amount"].Readonly);
        Assert.True(seen["Lines.1.Note"].Readonly);

        Assert.Equal("3.75 元", SchemaModel.ColumnTotal(fields.Single(field => field.Key == "Lines").Fields!.Single(field => field.Key == "Amount"), request.Lines));
    }

    [Fact]
    public void Condition_fields_reuse_labels_types_and_options()
    {
        var conditions = SchemaModel.ConditionFields(typeof(Supplier), omit: ["Company"]);
        Assert.DoesNotContain(conditions, field => field.Name is "Company" or "Address" or "Contacts");
        var category = conditions.Single(field => field.Name == "Category");
        Assert.Equal(("供货品类", ConditionFieldType.Select, "机械件"), (category.Label, category.Type, category.Options![0].Label));
        Assert.Equal(ConditionFieldType.Boolean, conditions.Single(field => field.Name == "Agreed").Type);
        Assert.Equal(["Amount", "Days"], SchemaModel.ConditionFields(typeof(Supplier), pick: ["Amount", "Days"]).Select(field => field.Name));
    }
}
