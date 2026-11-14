using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>Ellipsis 页的示例（vue-tsx-pro 文档站 display/Ellipsis）。列表宽度用 ToggleGroup 选几档（上游是 Slider，还没做）。</summary>
static class EllipsisExamples
{
    public static Example[] All =>
    [
        new("Ellipsis", "tooltip", "截断时才有提示", "是否截断按排好的文字量出，宽度、内容变了重新量（换几档列表宽度试试）。截断时文字能用 Tab 聚焦，悬停或聚焦显示全文（提示，读屏的帮助文字也是全文）；没截断时就是普通文字，不多一个 Tab 停留。只要省略、不需要看全文时用 Text 的 Truncate / LineClamp。", Suppliers),
        new("Ellipsis", "expand", "多行与展开", "Lines 是最多几行，Expandable 在截断时后面放“展开”，展开后显示全部、按钮换成“收起”（读屏念展开、收起）；写了 Expandable 时默认不再给提示。IsExpanded 可以双向绑定，Tooltip 换提示的内容或写 null 关掉。", Expand),
    ];

    static FrameworkElement Suppliers()
    {
        (string Name, string City, string Grade)[] suppliers =
        [
            ("杭州远山精密机械有限公司（萧山二厂）", "杭州", "A"),
            ("宁波海川液压件有限公司", "宁波", "A"),
            ("苏州明德电子科技有限公司 · 华东区域服务中心", "苏州", "B"),
            ("无锡恒通", "无锡", "C"),
        ];
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        foreach (var (name, city, grade) in suppliers)
        {
            var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm };
            var ellipsis = new Ellipsis { Text = name };
            Flex.SetGrow(ellipsis, true);
            var place = new Small { Text = city, Tone = TextTone.Muted };
            Flex.SetShrink(place, false);
            foreach (var child in new UIElement[] { new Badge { Content = grade, Size = TextSize.Xs, Tone = grade == "A" ? StatusTone.Success : StatusTone.Neutral }, ellipsis, place }) row.Children.Add(child);
            list.Children.Add(row);
        }
        var card = new Card { Padding = Space.Sm, Width = 18 * 16, Content = list, HorizontalAlignment = HorizontalAlignment.Left };
        var widths = new ToggleGroup { IsRequired = true, Value = "18" };
        foreach (var rem in new[] { "10", "18", "30" }) widths.Children.Add(new ToggleItem { Value = rem, Content = $"{rem}rem" });
        AutomationProperties.SetName(widths, "列表宽度");
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(widths, (_, _) =>
        {
            if (widths.Value is { } value) card.Width = int.Parse(value) * 16;
        });
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Field { Label = "列表宽度", Content = widths }, card } };
    }

    static FrameworkElement Expand()
    {
        (string Title, string Description)[] items =
        [
            ("设备支架改用整体压铸件", "原方案由三块钣金件焊接，焊后需要校形与喷涂，单件工时 4.2 小时。改为整体压铸件后取消焊接与校形工序，生产余量从 3 mm 降到 1.5 mm，单件材料费增加 120 元，工时降到 1.6 小时，综合测算单件节省 486 元，年产 1,200 件约 58 万元。需要压铸件供应商在 11 月前完成样品验收。"),
            ("紧固件标准化", "把 37 种非标螺栓并成 12 种标准件。"),
        ];
        var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Md };
        foreach (var (title, description) in items)
            column.Children.Add(new Card
            {
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Xs,
                    Children = { new Text { Text = title, Weight = TextWeight.Semibold }, new Ellipsis { Text = description, Lines = 2, Expandable = true } },
                },
            });
        return column;
    }
}
