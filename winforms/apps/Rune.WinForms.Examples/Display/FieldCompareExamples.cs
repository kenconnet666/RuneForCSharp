namespace Rune.WinForms.Examples;

/// <summary>FieldCompare 页的示例（vue-tsx-pro 文档站 display/FieldCompare）。versions、changes 等 FieldCompare。</summary>
static class FieldCompareExamples
{
    public static Example[] All =>
    [
        new("FieldCompare", "value-change", "单个值的变化：操作记录", "ValueChangeControl 是一个值的变化：Before 划掉、箭头、After，放在表格单元格、操作记录、审批意见里；没有原值（新建）时只显示新值。", Changes),
    ];

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
