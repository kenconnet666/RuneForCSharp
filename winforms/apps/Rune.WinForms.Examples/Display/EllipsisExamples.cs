namespace Rune.WinForms.Examples;

/// <summary>Ellipsis 页的示例（vue-tsx-pro 文档站 display/Ellipsis）。列表宽度用 ToggleGroup 选几档（上游是 Slider，还没做）。</summary>
static class EllipsisExamples
{
    public static Example[] All =>
    [
        new("Ellipsis", "tooltip", "截断时才有提示", "是否截断按文字的自然大小与排到的大小比，宽度、内容变了重新量（换几档列表宽度试试）。截断时文字能用 Tab 聚焦，悬停或聚焦显示全文（提示，读屏的描述也是全文）；没截断时就是普通文字，不多一个 Tab 停留。只要省略、不需要看全文时用 TextControl 的 Truncate / LineClamp。", Suppliers),
        new("Ellipsis", "expand", "多行与展开", "Lines 是最多几行，Expandable 在截断时后面放“展开”，展开后显示全部、按钮换成“收起”（读屏里是带展开状态的按钮）；写了 Expandable 时默认不再给提示。ExpandedChanged 报告展开与收起，Tooltip 换提示的内容或写 null 关掉。", Expand),
    ];

    static Control Suppliers()
    {
        (string Name, string City, string Grade)[] suppliers =
        [
            ("杭州远山精密机械有限公司（萧山二厂）", "杭州", "A"),
            ("宁波海川液压件有限公司", "宁波", "A"),
            ("苏州明德电子科技有限公司 · 华东区域服务中心", "苏州", "B"),
            ("无锡恒通", "无锡", "C"),
        ];
        var list = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
        foreach (var (name, city, grade) in suppliers)
        {
            var row = new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm };
            var ellipsis = new EllipsisControl { Text = name };
            var place = new SmallControl { Text = city, Tone = TextTone.Muted };
            row.Controls.AddRange([new BadgeControl { Text = grade, TextSize = TextSize.Xs, Tone = grade == "A" ? StatusTone.Success : StatusTone.Neutral }, ellipsis, place]);
            row.SetGrow(ellipsis, true);
            row.SetShrink(place, false);
            list.Controls.Add(row);
        }
        var card = Build.Card(new CardControl { CardPadding = Space.Sm }, list);
        var rem = 18;
        // 卡片定宽（rem 按根字号 16 换算）：最小、最大宽度都是它，高度照常按内容
        void Resize(int next)
        {
            rem = next;
            var width = (int)Math.Round(rem * 16 * card.DeviceDpi / 96f);
            card.MinimumSize = new Size(width, 0);
            card.MaximumSize = new Size(width, 0);
        }
        var widths = new ToggleGroupControl { IsRequired = true, AccessibleName = "列表宽度" };
        widths.Controls.AddRange([.. new[] { "10", "18", "30" }.Select(rem => new ToggleItemControl { Value = rem, Text = $"{rem}rem" })]);
        widths.Value = "18";
        widths.ValueChanged += (_, _) =>
        {
            if (widths.Value is { } value) Resize(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        };
        card.HandleCreated += (_, _) => Resize(rem);
        card.DpiChangedAfterParent += (_, _) => Resize(rem);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, new FieldControl { Label = "列表宽度", Content = widths }, card);
    }

    static Control Expand()
    {
        (string Title, string Description)[] items =
        [
            ("设备支架改用整体压铸件", "原方案由三块钣金件焊接，焊后需要校形与喷涂，单件工时 4.2 小时。改为整体压铸件后取消焊接与校形工序，生产余量从 3 mm 降到 1.5 mm，单件材料费增加 120 元，工时降到 1.6 小时，综合测算单件节省 486 元，年产 1,200 件约 58 万元。需要压铸件供应商在 11 月前完成样品验收。"),
            ("紧固件标准化", "把 37 种非标螺栓并成 12 种标准件。"),
        ];
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
            [.. items.Select(item => Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch },
                new TextControl { Text = item.Title, Weight = TextWeight.Semibold }, new EllipsisControl { Text = item.Description, Lines = 2, Expandable = true })))]);
    }
}
