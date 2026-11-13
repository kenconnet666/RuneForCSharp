using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Tabs 的示例（vue-tsx-pro 文档站 navigation/Tabs）。</summary>
static class TabsExamples
{
    public static Example[] All =>
    [
        new("Tabs", "workspace", "多页签：关闭、新建、拖动与全部标签页", "Tab 订阅了 CloseRequested 就有关闭按钮（不进 Tab 顺序，焦点在标签上时按 Delete，鼠标中键也关）；关掉的是当前标签时选它后面的一个，焦点跟过去。TabList 订阅了 AddRequested 就在标签栏末尾放“新建标签页”，订阅了 Reordered 标签就能拖动排序（键盘：Alt + ← →），参数是新的顺序；ShowAllMenu 在右边放“全部标签页”的菜单。放不下时列表横向滚动，两端出现滚动按钮，切到的标签自己滚进来。", Workspace),
        new("Tabs", "line", "订单状态", "Tabs 管当前值（Value），TabList 是一排 Tab，每个 Tab 对应一个 TabPanel，只显示当前的。方向键在标签之间移动，焦点到哪个就切到哪个；当前标签下方的线切换时滑过去。", Orders),
        new("Tabs", "soft", "浅色底与手动切换", "Variant=\"soft\" 像分段控件；Activation=\"manual\" 时方向键只移动焦点，按 Enter / 空格才切换。不可用的标签跳过。", Soft),
        new("Tabs", "vertical", "竖排", "Orientation=\"vertical\"：标签在左、内容在右，键盘改用 ↑↓。设置页这类分组多、标签文字长的场景。", Vertical),
    ];

    static readonly (string Id, string Customer, string Amount, string Status)[] Orders_ =
    [
        ("#10234", "林小满", "¥1,280.00", "shipped"), ("#10233", "周一航", "¥329.00", "unpaid"), ("#10232", "陈默", "¥4,299.00", "shipped"),
        ("#10231", "许悠", "¥659.00", "unpaid"), ("#10230", "王晴", "¥88.00", "done"),
    ];

    static readonly (string Value, string Label)[] Statuses = [("all", "全部"), ("unpaid", "待付款"), ("shipped", "已发货"), ("done", "已完成"), ("refund", "退款")];

    static Control Orders()
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
            Control body;
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
        tabs.PropertyChanged += (_, e) =>
        {
            if (e.Property == Tabs.ValueProperty) Tone();
        };
        Tone();
        return tabs;
    }

    const string Source = """
        <Button tone="success" icon={Check}>
          保存
        </Button>
        """;

    static Control Soft()
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
        preview[!Border.BackgroundProperty] = preview.GetResourceObservable("Rune.Brush.Fill").ToBinding();
        return new Tabs
        {
            Variant = TabsVariant.Soft, Size = TextSize.Sm, Activation = TabsActivation.Manual,
            Children =
            {
                list,
                new TabPanel { Value = "preview", Content = preview },
                new TabPanel { Value = "code", Content = new CodeBlock { Code = Source, Language = "tsx" } },
            },
        };
    }

    static readonly (string Id, string Title, bool Pinned)[] Pages =
    [
        ("home", "工作台", true), ("po-2401", "采购单 PO-2401", false), ("po-2402", "采购单 PO-2402", false), ("supplier-17", "供应商：远山精密", false),
        ("report-q3", "第三季度销售报表", false), ("inspect-88", "来料检验 IQC-88", false), ("contract-5", "合同审批 CT-5", false),
    ];

    /// <summary>工作区的页签（同后台系统的多页签）：打开的单据各占一个标签，可以关闭、新建、拖动排序；首页固定，不能关。</summary>
    static Control Workspace()
    {
        var list = new TabList { ShowAllMenu = true };
        AutomationProperties.SetName(list, "打开的页面");
        var tabs = new Tabs { Value = "po-2401", Children = { list } };
        var created = 0;
        void Open(string id, string title, bool pinned)
        {
            var tab = new Tab { Value = id, Content = title };
            var panel = new TabPanel { Value = id };
            if (!pinned)
                tab.CloseRequested += (_, _) =>
                {
                    list.Children.Remove(tab);
                    tabs.Children.Remove(panel);
                    Count();
                };
            list.Children.Add(tab);
            tabs.Children.Add(panel);
            panel.Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Xs, Margin = new Thickness(0, 14),
                Children = { new Text { Text = title, Weight = TextWeight.Semibold }, new Small { Tone = TextTone.Muted } },
            };
            Count();
        }
        // 面板里写着共几个页签
        void Count()
        {
            foreach (var small in tabs.Children.OfType<TabPanel>().Select(panel => ((Flex)panel.Content!).Children[1]).OfType<Small>())
                small.Text = $"拖动标签换顺序（或在标签上按 Alt + ← →），Delete 或中键关闭；共 {list.Children.Count} 个页签";
        }
        foreach (var (id, title, pinned) in Pages) Open(id, title, pinned);
        list.AddRequested += (_, _) =>
        {
            created++;
            Open($"draft-{created}", $"新建草稿 {created}", false);
            tabs.Value = $"draft-{created}";
        };
        // TabList 已经挪好了标签；使用方据新顺序保存（这里不用另做什么）
        list.Reordered += (_, _) => { };
        return new Card { Padding = Space.Sm, Content = tabs };
    }

    static Control Vertical()
    {
        var list = new TabList { MinWidth = 9 * 16 };
        AutomationProperties.SetName(list, "设置");
        foreach (var (value, label, icon) in new[] { ("profile", "个人资料", Lucide.User), ("security", "账号安全", Lucide.KeyRound), ("notifications", "通知", Lucide.Bell) })
            list.Children.Add(new Tab { Value = value, Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = icon }, new Text { Text = label } } } });
        static Flex Column(Space gap, params Control[] children)
        {
            var column = new Flex { Direction = FlexDirection.Column, Gap = gap };
            column.Children.AddRange(children);
            return column;
        }
        return new Tabs
        {
            Orientation = TabsOrientation.Vertical,
            Children =
            {
                list,
                new TabPanel
                {
                    Value = "profile",
                    Content = Column(Space.Md,
                        new Field { Label = "昵称", Content = new TextField { Text = "周一航" } },
                        new Field { Label = "个人主页", Content = new TextField { Type = TextFieldType.Url, PlaceholderText = "https://" } }),
                },
                new TabPanel { Value = "security", Content = Column(Space.Md, new Switch { Content = "两步验证", IsChecked = true }, new Switch { Content = "新设备登录时提醒" }) },
                new TabPanel
                {
                    Value = "notifications",
                    Content = Column(Space.Sm, new Checkbox { Content = "订单状态变化", IsChecked = true }, new Checkbox { Content = "有人提到我", IsChecked = true }, new Checkbox { Content = "每周报告" }),
                },
            },
        };
    }
}
