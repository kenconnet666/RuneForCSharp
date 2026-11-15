using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>AsideLayout 的示例（vue-tsx-pro 文档站 layout/AsideLayout）。</summary>
static class AsideLayoutExamples
{
    public static Example[] All =>
    [
        new("AsideLayout", "detail", "详情页", "主栏放概览与明细，侧栏放金额与里程碑；自己不到 56rem 宽时叠成一列。", Detail),
    ];

    static Card Section(string title, Control content) => new() { Variant = CardVariant.Outline, Header = new H3 { Text = title, Size = TextSize.Sm }, Content = content };

    static Control Detail()
    {
        var overview = new Descriptions { Columns = 2, Size = TextSize.Sm };
        overview.Items.AddRange([
            new DescriptionItem { Label = "任务类别", Value = "产品改进" },
            new DescriptionItem { Label = "责任团队", Value = "技术部" },
            new DescriptionItem { Label = "主办", Value = "宋知行" },
            new DescriptionItem { Label = "计划完成", Value = "2026-10-06" },
        ]);
        return new AsideLayout
        {
            AsideLabel = "收益与里程碑",
            AsideWidth = "18rem",
            Sticky = false,
            Aside =
            {
                Section("收益（万元）", new Grid { Columns = 2, Gap = Space.Sm, Children = { new Stat { Label = "预期", Value = 17.48m }, new Stat { Label = "测算", Text = "—" } } }),
                Section("里程碑", new Timeline
                {
                    Size = TextSize.Sm,
                    Items =
                    [
                        new TimelineItem { Title = "方案确定", Time = "2026-08-13", Tone = StatusTone.Success },
                        new TimelineItem { Title = "验证样件交付", Time = "2026-09-22", Tone = StatusTone.Success },
                    ],
                    Pending = "验证中，计划 10-30 完成",
                }),
            },
            Children =
            {
                Section("概览", overview),
                Section("改进明细", new Text { Text = "卡箍改用铝合金，合并两件支架，减少紧固件。" }),
            },
        };
    }
}
