namespace Rune.WinForms.Examples;

/// <summary>FieldCompare 页的示例（vue-tsx-pro 文档站 display/FieldCompare）：版本对比的表格、编辑前后的列表、单个值的变化（ValueChangeControl）。</summary>
static class FieldCompareExamples
{
    public static Example[] All =>
    [
        new("FieldCompare", "versions", "版本对比：表格", "选两个版本：字段、原值、新值、变化四列；金额是 decimal，变化量精确计算。单价、年度金额越低越好（Better = Lower），利润率越高越好；没改的字段淡显，勾上“只看改动”只列改了的（OnlyChanged）。", Versions),
        new("FieldCompare", "changes", "编辑前后：列表", "CompareLayout = List 一行一个字段，只列改了的——原值划掉、箭头、新值，数值字段跟着变化量。放在保存前的确认、审批意见、通知这类地方；没有原记录（新建）时只列新值，点“还原”后什么都没改，显示“没有改动”。", Edits),
        new("FieldCompare", "value-change", "单个值的变化：操作记录", "ValueChangeControl 是一个值的变化：Before 划掉、箭头、After，放在表格单元格、操作记录、审批意见里；没有原值（新建）时只显示新值。", Changes),
    ];

    sealed record Estimate(string Version, decimal UnitPrice, int AnnualVolume, decimal SavingRate, string Material, string Supplier, DateOnly ValidUntil, string Status);

    static readonly StatusMap EstimateStatuses = new()
    {
        ["draft"] = new("草稿", StatusTone.Neutral),
        ["reviewing"] = new("评审中", StatusTone.Info),
        ["approved"] = new("已通过", StatusTone.Success),
    };

    static readonly Estimate[] Versions3 =
    [
        new("V1", 128.60m, 42_000, 0.062m, "6061-T6 铝合金", "华辰精密", new DateOnly(2026, 12, 31), "draft"),
        new("V2", 119.35m, 42_000, 0.129m, "6063-T5 铝合金", "华辰精密", new DateOnly(2026, 12, 31), "reviewing"),
        new("V3", 116.80m, 45_500, 0.148m, "6063-T5 铝合金", "恒远五金", new DateOnly(2027, 6, 30), "approved"),
    ];

    static readonly CompareField[] EstimateFields =
    [
        new("UnitPrice", "单价") { Format = new NumericFormat { Style = NumericStyle.Currency }, Better = CompareBetter.Lower },
        new("AnnualVolume", "年用量（件）"),
        new("AnnualCost", "年度金额（万元）")
        {
            Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 },
            Value = record => ((Estimate)record).UnitPrice * ((Estimate)record).AnnualVolume,
            Better = CompareBetter.Lower,
        },
        new("SavingRate", "利润率") { Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Better = CompareBetter.Higher },
        new("Material", "材料"),
        new("Supplier", "供应商"),
        new("ValidUntil", "报价有效期"),
        new("Status", "状态") { Statuses = EstimateStatuses },
    ];

    static ToggleGroupControl VersionPicker(string name, string value)
    {
        var group = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = name };
        group.Controls.AddRange([.. Versions3.Select(version => new ToggleItemControl { Value = version.Version, Text = version.Version })]);
        group.Value = value;
        return group;
    }

    // 测算版本对比：选两个版本，勾上“只看改动”只列改了的
    static Control Versions()
    {
        var from = VersionPicker("原版本", "V1");
        var to = VersionPicker("对比版本", "V3");
        var only = new CheckboxControl { Text = "只看改动" };
        var compare = new FieldCompareControl { Fields = EstimateFields };
        void Sync()
        {
            compare.Before = Versions3.First(version => version.Version == from.Value);
            compare.After = Versions3.First(version => version.Version == to.Value);
            compare.OnlyChanged = only.IsChecked ? true : null;
        }
        from.ValueChanged += (_, _) => Sync();
        to.ValueChanged += (_, _) => Sync();
        only.IsCheckedChanged += (_, _) => Sync();
        Sync();
        return Build.Card(new CardControl { Dock = DockStyle.Top },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
                Build.Flex(new FlexControl { Gap = Space.Md, Wrap = true, Align = FlexAlign.Center }, from, new TextControl { Text = "→" }, to, only),
                compare));
    }

    sealed record Order(string Customer, decimal Quantity, decimal Discount, decimal Amount, DateOnly Delivery, string Priority);

    static readonly StatusMap Priorities = new() { ["normal"] = new("普通", StatusTone.Neutral), ["urgent"] = new("加急", StatusTone.Warning) };

    static readonly CompareField[] OrderFields =
    [
        new("Customer", "客户"),
        new("Quantity", "数量"),
        new("Discount", "折扣") { Format = new NumericFormat { Style = NumericStyle.Percent } },
        new("Amount", "金额") { Format = new NumericFormat { Style = NumericStyle.Currency } },
        new("Delivery", "交期"),
        new("Priority", "优先级") { Statuses = Priorities },
    ];

    // 金额跟着数量与折扣算：单价 200
    static Order WithAmount(Order order) => order with { Amount = Math.Round(order.Quantity * 200 * (1 - order.Discount)) };

    // 编辑前后：左边改订单，右边是保存前的确认（只列改了的）与新建的订单（只有新值）
    static Control Edits()
    {
        var saved = new Order("远航物流", 120, 0.05m, 22_800, new DateOnly(2026, 11, 8), "normal");
        var draft = WithAmount(saved with { Quantity = 150, Discount = 0.08m, Priority = "urgent" });
        var confirm = new FieldCompareControl { Fields = OrderFields, Before = saved, After = draft, CompareLayout = FieldCompareLayout.List };
        var quantity = new NumberFieldControl { Value = draft.Quantity, Min = 0 };
        var discount = new NumberFieldControl { Value = draft.Discount, Min = 0, Max = 0.5m, Step = 0.01m, Format = new NumericFormat { Style = NumericStyle.Percent } };
        void Edit(Order next) => confirm.After = draft = WithAmount(next);
        quantity.ValueChanged += (_, _) => Edit(draft with { Quantity = quantity.Value ?? 0 });
        discount.ValueChanged += (_, _) => Edit(draft with { Discount = discount.Value ?? 0 });
        var restore = new ButtonControl { Variant = ButtonVariant.Surface, Text = "还原" };
        restore.Click += (_, _) =>
        {
            quantity.Value = saved.Quantity;
            discount.Value = saved.Discount;
            confirm.After = draft = saved;
        };
        var editor = Build.Card(new CardControl(),
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
                new H3Control { Text = "编辑订单 SO-1024", TextSize = TextSize.Md },
                new FieldControl { Label = "数量", Content = quantity },
                new FieldControl { Label = "折扣", Content = discount },
                Build.Flex(new FlexControl { Justify = FlexJustify.End }, restore)));
        CardControl Section(string title, FieldCompareControl compare) => Build.Card(new CardControl(),
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch }, new H3Control { Text = title, TextSize = TextSize.Md }, compare));
        var created = new FieldCompareControl { Fields = OrderFields, After = saved with { Customer = "北辰电子", Priority = "urgent" }, CompareLayout = FieldCompareLayout.List };
        return Build.Grid(new GridControl { MinItemWidth = "20em", Gap = Space.Lg, Align = GridAlign.Start },
            editor, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch }, Section("保存前确认", confirm), Section("新建的订单", created)));
    }

    // 操作记录：每条写出改了什么，原值划掉、箭头、新值；新建的字段只有新值
    static Control Changes()
    {
        var log = new DescriptionsControl { Columns = 1 };
        foreach (var item in new DescriptionItem[]
        {
            new() { Label = "单价", Value = new ValueChangeControl { Before = "¥1,200.00", After = "¥1,350.00" } },
            new() { Label = "交期", Value = new ValueChangeControl { Before = "2026-11-08", After = "2026-11-20" } },
            new() { Label = "负责人", Value = new ValueChangeControl { Before = "林晓", After = "周航" } },
            new() { Label = "备注", Value = new ValueChangeControl { After = "供应商产能不足，交期顺延" } },
        }) log.Items.Add(item);
        return log;
    }
}
