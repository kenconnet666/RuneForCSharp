using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>FieldCompare 页的示例（vue-tsx-pro 文档站 display/FieldCompare）：版本对比的表格、编辑前后的列表、单个值的变化（ValueChange）。</summary>
static class FieldCompareExamples
{
    public static Example[] All =>
    [
        new("FieldCompare", "versions", "版本对比：表格", "选两个版本：字段、原值、新值、变化四列；金额是 decimal，变化量精确计算。单价、年度金额越低越好（Better = Lower），利润率越高越好；没改的字段淡显，勾上“只看改动”只列改了的（OnlyChanged）。", Versions),
        new("FieldCompare", "changes", "编辑前后：列表", "Layout=\"List\" 一行一个字段，只列改了的——原值划掉、箭头、新值，数值字段跟着变化量。放在保存前的确认、审批意见、通知这类地方；没有原记录（新建）时只列新值，点“还原”后什么都没改，显示“没有改动”。", Edits),
        new("FieldCompare", "value-change", "单个值的变化：操作记录", "ValueChange 是一个值的变化：Before 划掉、箭头、After，放在表格单元格、操作记录、审批意见里；没有原值（新建）时只显示新值。", Changes),
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

    static void Watch(DependencyObject target, DependencyProperty property, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(property, target.GetType()).AddValueChanged(target, (_, _) => changed());

    static ToggleGroup VersionPicker(string name, string value)
    {
        var group = new ToggleGroup { IsRequired = true, Value = value, Size = TextSize.Sm };
        AutomationProperties.SetName(group, name);
        foreach (var version in Versions3) group.Children.Add(new ToggleItem { Value = version.Version, Content = version.Version });
        return group;
    }

    // 测算版本对比：选两个版本，勾上“只看改动”只列改了的
    static FrameworkElement Versions()
    {
        var from = VersionPicker("原版本", "V1");
        var to = VersionPicker("对比版本", "V3");
        var only = new Checkbox { Content = "只看改动" };
        var compare = new FieldCompare { Fields = EstimateFields };
        void Sync()
        {
            compare.Before = Versions3.First(version => version.Version == from.Value);
            compare.After = Versions3.First(version => version.Version == to.Value);
            compare.OnlyChanged = only.IsChecked ? true : null;
        }
        Watch(from, ToggleGroup.ValueProperty, Sync);
        Watch(to, ToggleGroup.ValueProperty, Sync);
        Watch(only, ChoiceBase.IsCheckedProperty, Sync);
        Sync();
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children = { new Flex { Gap = Space.Md, Wrap = true, Align = FlexAlign.Center, Children = { from, new Text { Text = "→" }, to, only } }, compare },
            },
        };
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
    static FrameworkElement Edits()
    {
        var saved = new Order("远航物流", 120, 0.05m, 22_800, new DateOnly(2026, 11, 8), "normal");
        var draft = WithAmount(saved with { Quantity = 150, Discount = 0.08m, Priority = "urgent" });
        var confirm = new FieldCompare { Fields = OrderFields, Before = saved, After = draft, Layout = FieldCompareLayout.List };
        var quantity = new NumberField { Value = draft.Quantity, Min = 0 };
        var discount = new NumberField { Value = draft.Discount, Min = 0, Max = 0.5m, Step = 0.01m, Format = new NumericFormat { Style = NumericStyle.Percent } };
        void Edit(Order next) => confirm.After = draft = WithAmount(next);
        Watch(quantity, NumberField.ValueProperty, () => Edit(draft with { Quantity = quantity.Value ?? 0 }));
        Watch(discount, NumberField.ValueProperty, () => Edit(draft with { Discount = discount.Value ?? 0 }));
        var restore = new Button { Variant = ButtonVariant.Surface, Content = "还原" };
        restore.Click += (_, _) =>
        {
            confirm.After = draft = saved;
            quantity.Value = saved.Quantity;
            discount.Value = saved.Discount;
        };
        var editor = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children =
                {
                    new H3 { Text = "编辑订单 SO-1024", Size = TextSize.Md },
                    new Field { Label = "数量", Content = quantity },
                    new Field { Label = "折扣", Content = discount },
                    new Flex { Justify = FlexJustify.End, Children = { restore } },
                },
            },
        };
        Card Section(string title, FieldCompare compare) => new()
        {
            Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new H3 { Text = title, Size = TextSize.Md }, compare } },
        };
        var created = new FieldCompare { Fields = OrderFields, After = saved with { Customer = "北辰电子", Priority = "urgent" }, Layout = FieldCompareLayout.List };
        return new Grid
        {
            MinItemWidth = "20em", Gap = Space.Lg,
            Children = { editor, new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { Section("保存前确认", confirm), Section("新建的订单", created) } } },
        };
    }

    // 操作记录：每条写出改了什么，原值划掉、箭头、新值；新建的字段只有新值
    static FrameworkElement Changes()
    {
        var log = new Descriptions { Columns = 1 };
        foreach (var item in new DescriptionItem[]
        {
            new DescriptionItem { Label = "单价", Value = new ValueChange { Before = "¥1,200.00", After = "¥1,350.00" } },
            new DescriptionItem { Label = "交期", Value = new ValueChange { Before = "2026-11-08", After = "2026-11-20" } },
            new DescriptionItem { Label = "负责人", Value = new ValueChange { Before = "林晓", After = "周航" } },
            new DescriptionItem { Label = "备注", Value = new ValueChange { After = "供应商产能不足，交期顺延" } },
        }) log.Items.Add(item);
        return log;
    }
}
