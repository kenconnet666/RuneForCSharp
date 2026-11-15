using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>PivotTable 的示例（vue-tsx-pro 文档站 table/PivotTable）。</summary>
static class PivotTableExamples
{
    public static Example[] All =>
    [
        new("PivotTable", "heatmap", "热力透视：区域 × 季度", "采购明细按“区域 → 供应商”与季度透视：相同区域合并成一格，每格是采购额之和（万元），带合计行与合计列；Heatmap 按金额给格子着色，一眼看出哪个供应商、哪个季度贡献大。金额是 decimal，合计精确相加。", Heatmap),
        new("PivotTable", "count", "计数与平均", "任务按主办人与状态计数（Aggregate = Count，不需要数值字段）；状态按流程的先后排（Order），显示成中文（Format）。下面一张是按主办人求平均处理天数（Aggregate = Avg），只有行维度、没有列维度。", Count),
    ];

    sealed record Saving(string Region, string Supplier, string Quarter, decimal Amount);

    static readonly Saving[] Savings =
    [
        new("华东", "华辰精密", "Q1", 128.4m), new("华东", "华辰精密", "Q2", 96.2m), new("华东", "华辰精密", "Q3", 210.5m), new("华东", "华辰精密", "Q4", 188.1m),
        new("华东", "恒远五金", "Q2", 42.8m), new("华东", "恒远五金", "Q3", 76.3m), new("华东", "恒远五金", "Q4", 120.6m),
        new("华南", "粤海机电", "Q1", 64.9m), new("华南", "粤海机电", "Q2", 71.2m), new("华南", "粤海机电", "Q3", 58.4m), new("华南", "粤海机电", "Q4", 90.7m),
        new("华南", "南方物流", "Q1", 15.3m), new("华南", "南方物流", "Q4", 33.0m),
        new("华北", "京津新材", "Q1", 22.1m), new("华北", "京津新材", "Q2", 35.6m), new("华北", "京津新材", "Q3", 41.9m), new("华北", "京津新材", "Q3", 18.2m),
        new("华北", "京津新材", "Q4", 52.4m),
    ];

    static Control Heatmap()
    {
        var table = new PivotTable
        {
            Data = Savings,
            Rows = [new("Region", "区域") { Order = ["华东", "华南", "华北"] }, new("Supplier", "供应商")],
            Columns = new("Quarter", "季度"),
            Value = new("Amount", "采购额") { Format = new NumericFormat { MinimumFractionDigits = 1 } },
            Heatmap = true,
        };
        AutomationProperties.SetName(table, "各区域、供应商的季度采购额（万元）");
        return table;
    }

    sealed record WorkItem(string Owner, string Status, int Days);

    static readonly Dictionary<string, string> StatusLabels = new() { ["todo"] = "待开始", ["doing"] = "进行中", ["review"] = "待审核", ["done"] = "已完成" };

    // 按位置算出一组看起来自然的数据（同上游）
    static readonly WorkItem[] Tasks =
    [
        .. Enumerable.Range(0, 46).Select(i => new WorkItem(
            new[] { "周宁", "陆川", "方圆", "韩笑" }[i * 7 % 4],
            StatusLabels.Keys.ElementAt((i * 3 + i / 5) % 4),
            2 + i * 13 % 11)),
    ];

    static Control Count()
    {
        var count = new PivotTable
        {
            Data = Tasks,
            Rows = [new("Owner", "主办人")],
            Columns = new("Status", "状态") { Order = [.. StatusLabels.Keys], Format = status => StatusLabels[status] },
            Aggregate = PivotAggregate.Count,
            Heatmap = true,
        };
        AutomationProperties.SetName(count, "各主办人的任务数");
        var average = new PivotTable
        {
            Data = Tasks,
            Rows = [new("Owner", "主办人")],
            Value = new("Days", "平均处理天数") { Format = new NumericFormat { MaximumFractionDigits = 1 } },
            Aggregate = PivotAggregate.Avg,
        };
        AutomationProperties.SetName(average, "各主办人的平均处理天数");
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { count, average } };
    }
}
