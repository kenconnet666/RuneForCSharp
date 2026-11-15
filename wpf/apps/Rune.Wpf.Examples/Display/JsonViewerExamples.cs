using System.Numerics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>JsonViewer 的示例（vue-tsx-pro 文档站 display/JsonViewer）。</summary>
static class JsonViewerExamples
{
    public static Example[] All =>
    [
        new("JsonViewer", "response", "接口返回", "Value 可以是 JsonElement、JsonNode，也可以是任意 .NET 值：BigInteger 的编号、decimal 的金额照实显示（不丢精度），日期是 ISO 8601，HashSet 是 Set。ExpandDepth 是一开始展开几层；点一项看它的路径，复制值或路径。", Response),
        new("JsonViewer", "paste", "粘贴 JSON 看结构", "解析出错时在 Field 里提示位置；“生成 5000 条”看大数据下的虚拟滚动与搜索：树只建看得见的行，搜索留下匹配的项与它们的上级。", Paste),
    ];

    // 订单详情：编号是 BigInteger、金额是 decimal、下单时间是 DateTimeOffset，标签是 HashSet
    static FrameworkElement Response()
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
        var viewer = new JsonViewer { Value = response, ExpandDepth = 2 };
        AutomationProperties.SetName(viewer, "订单详情");
        return new Card { Padding = Space.Sm, Content = viewer };
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
    static FrameworkElement Paste()
    {
        var text = new Textarea { Text = Initial, Rows = 5 };
        var field = new Field { Label = "JSON", Content = text };
        var viewer = new JsonViewer { TreeMaxHeight = 18 * 16 };
        AutomationProperties.SetName(viewer, "解析结果");
        var big = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Content = "生成 5000 条" };
        var reset = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "还原" };
        big.Click += (_, _) => text.Text = Big();
        reset.Click += (_, _) => text.Text = Initial;
        void Parse()
        {
            try
            {
                viewer.Value = JsonDocument.Parse(text.Text ?? "").RootElement;
                field.Error = null;
                viewer.Visibility = Visibility.Visible;
            }
            catch (JsonException error)
            {
                field.Error = error.Message;
                viewer.Visibility = Visibility.Collapsed;
            }
        }
        text.TextChanged += (_, _) => Parse();
        Parse();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children = { field, new Flex { Gap = Space.Xs, Children = { big, reset } }, viewer },
        };
    }
}
