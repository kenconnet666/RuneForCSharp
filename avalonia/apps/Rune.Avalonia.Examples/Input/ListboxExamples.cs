using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Listbox 的示例（vue-tsx-pro 文档站 input/Listbox）。remote 要等远程选项（load）。</summary>
static class ListboxExamples
{
    public static Example[] All =>
    [
        new("Listbox", "multiple", "多选与虚拟滚动", "一千个可选的列：Listbox 始终虚拟滚动，只建看得见的几十行。SelectionMode=\"multiple\" 时每项独立切换，已选的项行尾打勾；分组的组名是一行小标题。", Columns),
        new("Listbox", "single", "单选、分组与说明", "选项带图标、说明与分组，不可用的项能被方向键经过、不能选中；在必填的 Field 里再点已选项不会取消。", Plans),
    ];

    static Control Columns()
    {
        string[] common = ["订单号", "客户", "金额", "状态", "下单时间", "收货地址"];
        var fields = Enumerable.Range(0, 1000)
            .Select(index => new ListboxOption($"field-{index}", index < 6 ? common[index] : $"自定义字段 {index - 5}") { Group = index < 6 ? "常用" : "自定义" })
            .ToList();
        var list = new Listbox { ItemsSource = fields, SelectionMode = SelectionMode.Multiple, Values = ["field-0", "field-1", "field-2", "field-3"], Height = 16 * 16 };
        AutomationProperties.SetName(list, "显示的列");
        var summary = new Small { Tone = TextTone.Muted };
        void Update() => summary.Text = $"显示 {list.Values?.Count ?? 0} 列 / 共 {fields.Count} 个字段";
        list.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValuesProperty) Update();
        };
        Update();
        return new Card { Padding = Space.Sm, Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { list, summary } } };
    }

    static Control Plans()
    {
        ListboxOption[] plans =
        [
            new("free", "免费版") { Icon = Lucide.Sprout, Description = "3 个项目、1 GB 存储", Group = "个人" },
            new("pro", "专业版") { Icon = Lucide.Rocket, Description = "不限项目、100 GB 存储", Group = "个人" },
            new("team", "团队版") { Icon = Lucide.Users, Description = "20 人以内，共享空间", Group = "团队" },
            new("enterprise", "企业版") { Icon = Lucide.BuildingComplex, Description = "单点登录、审计日志（请联系销售）", Group = "团队", IsDisabled = true },
        ];
        var list = new Listbox { ItemsSource = plans, Value = "pro" };
        var picked = new Small { Tone = TextTone.Muted };
        void Update() => picked.Text = $"选了：{plans.FirstOrDefault(plan => plan.Value == list.Value)?.Label ?? "无"}";
        list.PropertyChanged += (_, e) =>
        {
            if (e.Property == Listbox.ValueProperty) Update();
        };
        Update();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Field { Label = "套餐", IsRequired = true, Content = list }, picked } };
    }
}
