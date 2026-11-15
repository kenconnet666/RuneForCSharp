using System.Numerics;
using System.Text.Json;

namespace Rune.WinForms.Examples;

/// <summary>JsonViewerControl 的示例（vue-tsx-pro 文档站 display/JsonViewer）。</summary>
static class JsonViewerExamples
{
    public static Example[] All =>
    [
        new("JsonViewer", "response", "接口返回", "Value 可以是 JsonElement、JsonNode，也可以是任意 .NET 值：BigInteger 的编号、decimal 的金额照实显示（不丢精度），日期是 ISO 8601，HashSet 是 Set。ExpandDepth 是一开始展开几层；点一项看它的路径，复制值或路径。", Response),
        new("JsonViewer", "paste", "粘贴 JSON 看结构", "解析出错时在 FieldControl 里提示位置；“生成 5000 条”看大数据下的搜索：树只画看得见的行，搜索留下匹配的项与它们的上级。", Paste),
    ];

    // 订单详情：编号是 BigInteger、金额是 decimal、下单时间是 DateTimeOffset，标签是 HashSet
    static Control Response()
    {
        var response = new Dictionary<string, object?>
        {
            ["code"] = "ok",
            ["data"] = new Dictionary<string, object?>
            {
                ["id"] = BigInteger.Parse("9007199254740993"),
                ["number"] = "SO-2026-11-0042",
                ["status"] = "paid",
                ["customer"] = new { name = "林小满", phone = "138****0000", vip = true },
                ["total"] = 1288.50m,
                ["placedAt"] = new DateTimeOffset(2026, 11, 8, 8, 30, 0, TimeSpan.Zero),
                ["remark"] = null,
                ["delivery-window"] = new[] { "09:00", "12:00" },
                ["items"] = new[]
                {
                    new { sku = "MUG-01", name = "青瓷马克杯", qty = 2, price = 89.00m },
                    new { sku = "TEA-12", name = "龙井 250g", qty = 1, price = 1110.50m },
                },
                ["tags"] = new HashSet<string> { "首单", "包邮" },
            },
        };
        var viewer = new JsonViewerControl { Value = response, ExpandDepth = 2, AccessibleName = "订单详情" };
        return Build.Card(new CardControl { CardPadding = Space.Sm }, viewer);
    }

    const string Initial = """
        {
          "name": "vue-tsx-pro",
          "private": true,
          "workspaces": ["packages/*", "apps/*"],
          "engines": { "node": ">=22" }
        }
        """;

    static string Big() => JsonSerializer.Serialize(Enumerable.Range(0, 5000).Select(index => new
    {
        id = index + 1,
        level = new[] { "info", "warn", "error" }[index % 3],
        message = $"job-{index % 37} finished in {index * 7 % 1000} ms",
    }));

    // 粘贴 JSON：解析出错时提示位置，“生成 5000 条”看大数据
    static Control Paste()
    {
        var text = new TextareaControl { Text = Initial.ReplaceLineEndings("\r\n"), Rows = 5 };
        var field = new FieldControl { Label = "JSON", Content = text };
        var viewer = new JsonViewerControl { AccessibleName = "解析结果" };
        viewer.TreeMaxHeight = viewer.LogicalToDeviceUnits(18 * 16);
        var big = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Text = "生成 5000 条" };
        var reset = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "还原" };
        big.Click += (_, _) => text.Text = Big();
        reset.Click += (_, _) => text.Text = Initial.ReplaceLineEndings("\r\n");
        void Parse()
        {
            try
            {
                viewer.Value = JsonDocument.Parse(text.Text).RootElement;
                field.Error = null;
                viewer.Visible = true;
            }
            catch (JsonException error)
            {
                field.Error = error.Message;
                viewer.Visible = false;
            }
        }
        text.TextChanged += (_, _) => Parse();
        Parse();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            field, Build.Flex(new FlexControl { Gap = Space.Xs }, big, reset), viewer);
    }
}
