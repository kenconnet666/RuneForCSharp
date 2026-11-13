using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>Tabs 的示例（vue-tsx-pro 文档站 navigation/Tabs）。workspace（关闭、新建、拖动、全部标签页）要等 Menu；vertical 的面板要等 TextField。</summary>
static class TabsExamples
{
    public static Example[] All =>
    [
        new("Tabs", "line", "订单状态", "Tabs 管当前值（Value），TabList 是一排 Tab，每个 Tab 对应一个 TabPanel，只显示当前的。方向键在标签之间移动，焦点到哪个就切到哪个；当前标签下方的线切换时滑过去。", Orders),
        new("Tabs", "soft", "浅色底与手动切换", "Variant=\"soft\" 像分段控件；Activation=\"manual\" 时方向键只移动焦点，按 Enter / 空格才切换。不可用的标签跳过。", Soft),
    ];

    static readonly (string Id, string Customer, string Amount, string Status)[] Orders_ =
    [
        ("#10234", "林小满", "¥1,280.00", "shipped"), ("#10233", "周一航", "¥329.00", "unpaid"), ("#10232", "陈默", "¥4,299.00", "shipped"),
        ("#10231", "许悠", "¥659.00", "unpaid"), ("#10230", "王晴", "¥88.00", "done"),
    ];

    static readonly (string Value, string Label)[] Statuses = [("all", "全部"), ("unpaid", "待付款"), ("shipped", "已发货"), ("done", "已完成"), ("refund", "退款")];

    static FrameworkElement Orders()
    {
        var tabs = new Tabs { Value = "all" };
        var list = new TabList();
        AutomationProperties.SetName(list, "订单状态");
        var badges = new List<(string Value, Badge Badge)>();
        foreach (var (value, label) in Statuses)
        {
            var orders = Orders_.Where(order => value == "all" || order.Status == value).ToArray();
            var badge = new Badge { Radius = Radius.Full, Content = orders.Length.ToString() };
            badges.Add((value, badge));
            list.Children.Add(new Tab { Value = value, Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Text { Text = label }, badge } } });
            FrameworkElement body;
            if (orders.Length == 0) body = new Empty { Title = $"没有{label}的订单" };
            else
            {
                var rows = new ItemList { Divided = true };
                foreach (var order in orders)
                    rows.Children.Add(new ItemListItem { Title = order.Customer, Description = order.Id, End = new Strong { Text = order.Amount }, Href = new Uri($"https://example.com/orders/{order.Id[1..]}") });
                body = rows;
            }
            tabs.Children.Add(new TabPanel { Value = value, Content = body });
        }
        tabs.Children.Insert(0, list);
        tabs.Children.Add(new Small { Text = "方向键在标签之间移动，焦点到哪个就切到哪个", Tone = TextTone.Muted });
        // 当前标签的计数是品牌色，其余中性色
        void Tone() { foreach (var (value, badge) in badges) badge.Tone = tabs.SelectedValue == value ? StatusTone.Accent : StatusTone.Neutral; }
        DependencyPropertyDescriptor.FromProperty(Tabs.ValueProperty, typeof(Tabs)).AddValueChanged(tabs, (_, _) => Tone());
        Tone();
        return tabs;
    }

    const string Source = """
        <Button tone="success" icon={Check}>
          保存
        </Button>
        """;

    static FrameworkElement Soft()
    {
        var list = new TabList();
        AutomationProperties.SetName(list, "查看方式");
        Tab Item(string value, IconData icon, string label, bool enabled = true) => new()
        {
            Value = value, IsEnabled = enabled,
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = icon }, new Text { Text = label } } },
        };
        list.Children.Add(Item("preview", Lucide.Eye, "预览"));
        list.Children.Add(Item("code", Lucide.CodeXml, "代码"));
        list.Children.Add(Item("split", Lucide.SquareSplitHorizontal, "并排（宽屏）", enabled: false));
        // 效果区：fill 底、md 圆角、内边距 space.lg
        var preview = new Border
        {
            Padding = new Thickness(13), CornerRadius = new CornerRadius(0.375 * 13),
            Child = new Paragraph { Text = "这里是效果。Activation=\"manual\"：方向键只移动焦点，按 Enter / 空格才切换。" },
        };
        preview.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Fill");
        return new Tabs
        {
            Variant = TabsVariant.Soft, Size = TextSize.Sm, Activation = TabsActivation.Manual,
            Children =
            {
                list,
                new TabPanel { Value = "preview", Content = preview },
                new TabPanel { Value = "code", Content = new CodeBlock { Code = Source, CodeLanguage = "tsx" } },
            },
        };
    }
}
