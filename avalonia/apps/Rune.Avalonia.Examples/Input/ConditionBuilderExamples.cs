using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>ConditionBuilder 页的示例（vue-tsx-pro 文档站 input/ConditionBuilder）。</summary>
static class ConditionBuilderExamples
{
    public static Example[] All =>
    [
        new("ConditionBuilder", "approval", "审批条件", "Fields 给出字段的名字、显示名与类型（文字、数字、日期、选项、是否）。比较方式随类型（日期是早于、晚于，选项是“是其中之一”），值的输入随类型与比较方式（介于是两个框，是其中之一是多选）。组的表头选满足方式，组里还能加组。Value 只写回填完整的比较，一条也没有时是 null；Describe 把条件写成一句话（收起时的摘要、流程连线上的文字），ToJson 是存库、传给后端的写法。", Approval),
        new("ConditionBuilder", "filter", "筛选本地数据", "Matches 在本地求值：同一份条件交给后端按同样的规则求值，结果一致。MaxDepth=\"1\" 不能加组，Size=\"sm\" 用小一档的控件。", Filter),
        new("ConditionBuilder", "custom-input", "自己画值的输入", "字段的 Input 换掉值的输入（人员选择器、带单位的数字），比较方式仍按 Type；Format 决定摘要里值的写法，Group 把字段列表分组。", CustomInput),
    ];

    static Control Approval()
    {
        ConditionField[] fields =
        [
            new("amount", "金额", ConditionFieldType.Number),
            new("category", "品类", ConditionFieldType.Select) { Options = [new("steel", "钢材"), new("aluminum", "铝材"), new("fastener", "紧固件"), new("service", "服务")] },
            new("supplier", "供应商", ConditionFieldType.Text),
            new("urgent", "加急", ConditionFieldType.Boolean),
            new("delivery", "交货日期", ConditionFieldType.Date),
        ];
        var builder = new ConditionBuilder
        {
            Fields = fields,
            Value = Condition.All(
                new ConditionRule("amount", ConditionOp.Gte, 100000),
                Condition.Any(new ConditionRule("category", ConditionOp.In, new[] { "steel", "aluminum" }), new ConditionRule("urgent", ConditionOp.Eq, true))),
        };
        AutomationProperties.SetName(builder, "审批条件");
        var summary = new Small { Tone = TextTone.Muted };
        var json = new CodeBlock { Language = "json", MaxLines = 12 };
        void Sync()
        {
            summary.Text = builder.Value is { } condition ? condition.Describe(fields, Provider.GetLang(builder)) : "没有条件：全部通过";
            json.Code = builder.Value?.ToJson(indented: true) ?? "null";
        }
        builder.PropertyChanged += (_, e) =>
        {
            if (e.Property == ConditionBuilder.ValueProperty) Sync();
        };
        builder.AttachedToVisualTree += (_, _) => Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { builder, summary, json } };
    }

    record Order(string Id, string Supplier, string Category, decimal Amount, string Delivery);

    static readonly Order[] Orders =
    [
        new("PO-1031", "华东钢铁", "钢材", 128000, "2026-11-03"),
        new("PO-1032", "西南铝业", "铝材", 86500, "2026-11-12"),
        new("PO-1033", "华东钢铁", "钢材", 42000, "2026-10-28"),
        new("PO-1034", "精工紧固件", "紧固件", 6800, "2026-11-01"),
        new("PO-1035", "北方检测", "服务", 15000, "2026-12-15"),
        new("PO-1036", "西南铝业", "铝材", 213000, "2026-12-02"),
    ];

    static Control Filter()
    {
        ConditionField[] fields =
        [
            new("supplier", "供应商", ConditionFieldType.Text),
            new("category", "品类", ConditionFieldType.Select) { Options = [.. new[] { "钢材", "铝材", "紧固件", "服务" }.Select(name => new ConditionOption(name, name))] },
            new("amount", "金额", ConditionFieldType.Number),
            new("delivery", "交货日期", ConditionFieldType.Date),
        ];
        var builder = new ConditionBuilder { Fields = fields, MaxDepth = 1, Size = TextSize.Sm, Value = Condition.All(new ConditionRule("amount", ConditionOp.Gte, 40000)) };
        AutomationProperties.SetName(builder, "订单筛选条件");
        var count = new Small { Tone = TextTone.Muted };
        var table = new ColumnTable
        {
            Columns =
            [
                new("Id", "订单号") { Width = 100 },
                new("Supplier", "供应商") { Width = 120 },
                new("Category", "品类") { Width = 80 },
                new("Amount", "金额（元）") { Width = 120, Type = ColumnType.Number, Format = new NumericFormat { MaximumFractionDigits = 0 } },
                new("Delivery", "交货日期") { Width = 120 },
            ],
        };
        AutomationProperties.SetName(table, "订单");
        void Sync()
        {
            var rows = builder.Value is { } condition ? Orders.Where(order => condition.Matches(order)).ToList() : [.. Orders];
            count.Text = $"{rows.Count} / {Orders.Length} 张订单";
            table.ItemsSource = rows;
        }
        builder.PropertyChanged += (_, e) =>
        {
            if (e.Property == ConditionBuilder.ValueProperty) Sync();
        };
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { builder, count, table } };
    }

    record Person(string Id, string Name, string Dept);

    static readonly Person[] People = [new("u01", "林晓", "采购部"), new("u02", "陈思雨", "质量部"), new("u03", "王建国", "财务部"), new("u04", "赵一凡", "采购部")];

    /// <summary>经办人的值：带搜索的 Select（真实场景接人员选择器）；是其中之一、不在其中时多选。</summary>
    static Control OwnerInput(ConditionInputContext context)
    {
        var select = new Select
        {
            ItemsSource = People, ToOption = item => item is Person person ? new ListboxOption(person.Id, person.Name) { Description = person.Dept } : new ListboxOption("", ""),
            Searchable = true,
        };
        AutomationProperties.SetName(select, context.Label);
        if (context.Op is ConditionOp.In or ConditionOp.NotIn)
        {
            select.SelectionMode = SelectionMode.Multiple;
            select.Values = [.. (context.Value as IEnumerable<object?> ?? []).OfType<string>()];
            select.PropertyChanged += (_, e) =>
            {
                if (e.Property == SelectBase.ValuesProperty) context.Change(select.Values?.ToArray() ?? []);
            };
        }
        else
        {
            select.Value = context.Value as string;
            select.PropertyChanged += (_, e) =>
            {
                if (e.Property == SelectBase.ValueProperty) context.Change(select.Value);
            };
        }
        return select;
    }

    static Control CustomInput()
    {
        ConditionField[] fields =
        [
            new("amount", "金额", ConditionFieldType.Number) { Group = "申请单" },
            new("reason", "事由", ConditionFieldType.Text) { Group = "申请单" },
            new("owner", "经办人", ConditionFieldType.Select)
            {
                Group = "流程变量", Input = OwnerInput,
                Format = id => People.FirstOrDefault(person => person.Id == id as string)?.Name ?? Condition.ToText(id),
            },
            new("round", "协商轮次", ConditionFieldType.Number) { Group = "流程变量" },
        ];
        var builder = new ConditionBuilder
        {
            Fields = fields,
            Value = Condition.Any(new ConditionRule("owner", ConditionOp.In, new[] { "u01", "u04" }), new ConditionRule("round", ConditionOp.Gte, 3)),
        };
        AutomationProperties.SetName(builder, "提级条件");
        var summary = new Small { Tone = TextTone.Muted };
        void Sync() => summary.Text = builder.Value is { } condition ? condition.Describe(fields, Provider.GetLang(builder)) : "没有条件";
        builder.PropertyChanged += (_, e) =>
        {
            if (e.Property == ConditionBuilder.ValueProperty) Sync();
        };
        builder.AttachedToVisualTree += (_, _) => Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { builder, summary } };
    }
}
