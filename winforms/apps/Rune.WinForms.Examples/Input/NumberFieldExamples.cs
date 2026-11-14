namespace Rune.WinForms.Examples;

/// <summary>
/// NumberField 的示例（vue-tsx-pro 文档站 input/NumberField）。WinForms 里是 NumberFieldControl，值是 decimal：上游 number、bigint、Decimal 三种的示例在这里都是它；
/// submit（原生表单提交）是网页的，range 要等 NumberRangeField。各框的值各自独立（上游几个框共用一个值）。
/// </summary>
static class NumberFieldExamples
{
    public static Example[] All =>
    [
        new("NumberField", "basic", "基础用法", "输入时是普通文字，失焦或回车时解析、夹到 Min–Max、对齐到 Step；解析不了时恢复原值，清空写回 null。Format 是数字格式（NumericFormat），认当前语言的小数点与千分位，也认中文输入法打出的全角数字。", Basic),
        new("NumberField", "precise", "精确数值", "值是 decimal：十进制精确，加减与对齐步长都不经过浮点数，金额、超过 2⁵³ 的整数、长小数都不丢位。一开始为空时就是 null。", Precise),
        new("NumberField", "units", "单位与缩放", "Suffix、Prefix 把单位放进输入框。值以元保存、按万元输入时写 Format 的 Scale = 10000：显示与编辑都是万元，写回时换回元；Min、Max、Step 仍按值本身的单位。只读时不显示加减按钮。", Units),
        new("NumberField", "appearance", "外观与状态", "外观同 TextFieldControl：Variant、TextSize、Radius；减少、增加按钮不在 Tab 顺序里，按住时连续加减。", Appearance),
    ];

    static GridControl Fields(string minItemWidth, params Control[] fields)
    {
        var grid = new GridControl { MinItemWidth = minItemWidth, Gap = Space.Lg, AutoSize = true, Dock = DockStyle.Top };
        grid.Controls.AddRange(fields);
        return grid;
    }

    static FieldControl Field(string label, Control content, string? description = null, string? error = null) =>
        new() { Label = label, Description = description, Error = error, Content = content };

    static Control Basic() => Fields("14em",
        Field("数量", new NumberFieldControl { Value = 2, Min = 1, Max = 99 }, "1–99，↑ ↓ 加减 1，Page Up / Down 加减 10"),
        Field("重量", new NumberFieldControl { Value = 1.5m, Min = 0, Step = 0.1m, Format = new NumericFormat { Style = NumericStyle.Unit, Unit = "kilogram" } }, "步长 0.1，提交时对齐，没有浮点误差"),
        Field("折扣", new NumberFieldControl { Value = 0.85m, Min = 0, Max = 1, Step = 0.05m, Format = new NumericFormat { Style = NumericStyle.Percent } }, "存 0–1 的小数，显示成百分比"),
        Field("室外温度", new NumberFieldControl { Value = -4, Min = -40, Max = 60, Format = new NumericFormat { Style = NumericStyle.Unit, Unit = "celsius" } }, "可以是负数，Home / End 到两端"),
        Field("评分", new NumberFieldControl { Min = 1, Max = 5, PlaceholderText = "1–5" }, "空着时为 null，按 ↑ 从 Min 开始"));

    static Control Precise()
    {
        Control Case(string label, string description, NumberFieldControl field)
        {
            var value = new SmallControl { Tone = TextTone.Muted };
            void Show() => value.Text = $"值：{field.Value?.ToString() ?? "null"}";
            field.ValueChanged += (_, _) => Show();
            Show();
            return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, AutoSize = true, Align = FlexAlign.Stretch }, Field(label, field, description), value);
        }
        return Fields("16em",
            Case("单价", "按两次 ↑：0.1 → 0.3，精确", new NumberFieldControl { Value = 0.1m, Step = 0.1m, Format = new NumericFormat { Style = NumericStyle.Currency, Currency = "CNY" } }),
            Case("合计", "一开始为空，步长 0.01", new NumberFieldControl { Step = 0.01m, Min = 0, PlaceholderText = "0.00" }),
            Case("播放量", "超过 2⁵³ 的整数，加减不丢位", new NumberFieldControl { Value = 9_007_199_254_740_993 }));
    }

    static Control Units()
    {
        var budget = new NumberFieldControl { Value = 155353.2m, Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Step = 100, Min = 0, Prefix = "¥", Suffix = "万元" };
        var saved = new SmallControl { Tone = TextTone.Muted };
        void Show() => saved.Text = $"保存的值：{budget.Value?.ToString() ?? "null"} 元";
        budget.ValueChanged += (_, _) => Show();
        Show();
        var stores = new NumberFieldControl { Value = 40, Suffix = "家", IsReadOnly = true, AccessibleName = "门店数量（只读）" };
        FlexControl Column(params Control[] children) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, AutoSize = true, Align = FlexAlign.Stretch }, children);
        return Fields("16em",
            Column(Field("年度预算", budget, "值是元，按万元输入；步长 100 元"), saved),
            Field("运费目标", new NumberFieldControl { Value = 12.5m, Step = 0.01m, Min = 0, Format = new NumericFormat { MinimumFractionDigits = 2 }, Suffix = "元/单", Stepper = false }),
            Column(Field("门店数量", new NumberFieldControl { Value = 40, Min = 0, Suffix = "家" }), new SmallControl { Tone = TextTone.Muted, Text = "只读时不显示加减按钮：" }, stores));
    }

    static Control Appearance()
    {
        var fields = new List<Control>();
        foreach (var variant in new[] { ControlVariant.Surface, ControlVariant.Soft })
            foreach (var size in new[] { TextSize.Sm, TextSize.Md, TextSize.Lg })
                fields.Add(Field($"{variant} · {size}", new NumberFieldControl { Value = 12, Variant = variant, TextSize = size }));
        fields.AddRange(
        [
            Field("胶囊形", new NumberFieldControl { Value = 12, Radius = Radius.Full }),
            Field("可清空", new NumberFieldControl { Value = 12, Clearable = true }, "Clearable：选填的数量，清空后值是 null"),
            Field("不要加减按钮", new NumberFieldControl { Value = 12, Stepper = false }, "Stepper=false，方向键照常可用"),
            Field("只读", new NumberFieldControl { Value = 12, IsReadOnly = true }),
            Field("不可用", new NumberFieldControl { Value = 12, Enabled = false }),
            Field("出错", new NumberFieldControl { Value = 12, Max = 10 }, error: "库存只剩 10 件"),
        ]);
        return Fields("13em", [.. fields]);
    }
}
