using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace Rune.Wpf.Examples;

/// <summary>CommandPalette 的示例（vue-tsx-pro 文档站 overlay/CommandPalette）。</summary>
static class CommandPaletteExamples
{
    public static Example[] All =>
    [
        new("CommandPalette", "commands", "命令与页面", "选项按 Group 分组，可以带图标、说明、不可用。焦点一直在输入框里：↑↓ 移动高亮，Enter 执行，Esc 关闭。Hotkey 默认是 Mod+K，这里换成 Mod+J。", Commands),
        new("CommandPalette", "remote", "远程搜索", "与 Select 相同的写法：InputText 拿到输入去服务端查，Filter 一律返回 true 表示 ItemsSource 就是结果、不再本地过滤；查询期间 IsLoading 为 true，列表里是骨架行。晚到的旧请求要自己丢掉。", Remote),
        new("CommandPalette", "custom", "任意数据与自定义选项", "后端数据直接给 ItemsSource，ToOption 说明值、文字和分组；Filter 自定义匹配（姓名、邮箱都能搜），ItemTemplate 自定义每一项的内容；Trigger 换成自己的按钮，Hotkey 为 null 时不注册快捷键；Empty 换掉没有匹配时的内容。", Mention),
    ];

    static readonly ListboxOption[] CommandItems =
    [
        new("home", "概览") { Icon = Lucide.House, Group = "页面" },
        new("orders", "订单") { Description = "全部订单、退款与售后", Icon = Lucide.ShoppingCart, Group = "页面" },
        new("products", "商品") { Icon = Lucide.Package, Group = "页面" },
        new("customers", "客户") { Icon = Lucide.Users, Group = "页面" },
        new("reports", "报表") { Description = "销售、库存、流量", Icon = Lucide.ChartLine, Group = "页面" },
        new("new-product", "新建商品") { Icon = Lucide.FilePlus, Group = "操作" },
        new("invite", "邀请成员") { Icon = Lucide.UserPlus, Group = "操作" },
        new("theme", "切换暗色模式") { Icon = Lucide.Moon, Group = "操作" },
        new("settings", "设置") { Icon = Lucide.Settings, Group = "操作" },
        new("logout", "退出登录") { Icon = Lucide.LogOut, Group = "操作", IsDisabled = true },
    ];

    static FrameworkElement Commands()
    {
        var palette = new CommandPalette { ItemsSource = CommandItems, Hotkey = "Mod+J", Label = "命令", Placeholder = "输入命令或页面名称" };
        var last = new Small { Tone = TextTone.Muted };
        palette.ItemInvoked += (_, e) => last.Text = $"执行了：{((ListboxOption)e.Item!).Label}";
        var hint = new Flex
        {
            Align = FlexAlign.Center, Gap = Space.Xs, Wrap = FlexWrap.Wrap,
            Children = { new Small { Tone = TextTone.Muted, Text = "也可以按" }, new Kbd { Content = "Ctrl" }, new Small { Tone = TextTone.Muted, Text = "+" }, new Kbd { Content = "J" }, new Small { Tone = TextTone.Muted, Text = "打开。" }, last },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { palette, hint } };
    }

    sealed record Order(string Id, string Customer, int Amount, string Status);

    static readonly string[] Customers = ["林小满", "周一航", "陈思远", "王可欣", "赵子墨", "孙若溪"];
    static readonly string[] Statuses = ["待付款", "已发货", "已完成"];
    static readonly Order[] Orders = [.. Enumerable.Range(0, 60).Select(index => new Order($"A-{10200 + index * 7}", Customers[index % Customers.Length], 120 + index * 389 % 2400, Statuses[index % Statuses.Length]))];

    /// <summary>模拟服务端搜索：按订单号、客户名匹配，延迟 450 毫秒返回。</summary>
    static async Task<Order[]> SearchOrders(string keyword)
    {
        await Task.Delay(450);
        var text = keyword.Trim();
        return [.. Orders.Where(order => text.Length == 0 || order.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || order.Customer.Contains(text)).Take(20)];
    }

    static FrameworkElement Remote()
    {
        var palette = new CommandPalette
        {
            Filter = (_, _) => true,
            Hotkey = null,
            Label = "搜索订单",
            Placeholder = "订单号或客户名",
            ToOption = item => item is Order order
                ? new ListboxOption(order.Id, $"{order.Id} · {order.Customer}") { Description = $"¥{order.Amount:N0}", Group = order.Status }
                : throw new ArgumentException(null, nameof(item)),
            Trigger = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Search, Content = "搜索订单" },
        };
        var picked = new Small { Tone = TextTone.Muted, Text = "每次输入都向“服务端”查询，结果约半秒后到" };
        palette.ItemInvoked += (_, e) =>
        {
            var order = (Order)e.Item!;
            picked.Text = $"打开了订单 {order.Id}（{order.Customer}，{order.Status}）";
        };
        // 输入变了就重新搜索；晚到的旧结果丢掉
        var latest = 0;
        async void Search(string text)
        {
            var request = ++latest;
            palette.IsLoading = true;
            var found = await SearchOrders(text);
            if (request != latest) return;
            palette.ItemsSource = found;
            palette.IsLoading = false;
        }
        DependencyPropertyDescriptor.FromProperty(CommandPalette.InputTextProperty, typeof(CommandPalette)).AddValueChanged(palette, (_, _) => Search(palette.InputText));
        Search("");
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { palette, picked } };
    }

    sealed record Person(string Id, string Name, string Email, string Team);

    static readonly Person[] Members =
    [
        new("zyh", "周一航", "yihang@example.com", "设计中心"),
        new("lxm", "林小满", "xiaoman@example.com", "前端"),
        new("cm", "陈默", "chenmo@example.com", "后端"),
        new("xy", "许悠", "xuyou@example.com", "产品"),
        new("wq", "王晴", "wangqing@example.com", "市场"),
        new("zhr", "赵浩然", "haoran@example.com", "前端"),
    ];

    /// <summary>成员的选项：头像，姓名与邮箱两行。</summary>
    static DataTemplate PersonTemplate()
    {
        var row = new FrameworkElementFactory(typeof(Flex));
        row.SetValue(Flex.AlignProperty, FlexAlign.Center);
        row.SetValue(Flex.GapProperty, Space.Sm);
        var avatar = new FrameworkElementFactory(typeof(Avatar));
        avatar.SetValue(Avatar.SizeProperty, TextSize.Sm);
        avatar.SetBinding(Avatar.DisplayNameProperty, new Binding(nameof(Person.Name)));
        var text = new FrameworkElementFactory(typeof(Flex));
        text.SetValue(Flex.DirectionProperty, FlexDirection.Column);
        text.SetValue(Flex.GrowProperty, true);
        var name = new FrameworkElementFactory(typeof(Text));
        name.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(nameof(Person.Name)));
        var email = new FrameworkElementFactory(typeof(Small));
        email.SetValue(Text.ToneProperty, TextTone.Muted);
        email.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(nameof(Person.Email)));
        text.AppendChild(name);
        text.AppendChild(email);
        row.AppendChild(avatar);
        row.AppendChild(text);
        return new DataTemplate(typeof(Person)) { VisualTree = row };
    }

    static FrameworkElement Mention()
    {
        var palette = new CommandPalette
        {
            ItemsSource = Members,
            ToOption = item => item is Person person ? new ListboxOption(person.Id, person.Name) { Group = person.Team } : throw new ArgumentException(null, nameof(item)),
            // 姓名、邮箱都能搜
            Filter = (item, query) => item is Person person && (person.Name.Contains(query) || person.Email.Contains(query, StringComparison.OrdinalIgnoreCase)),
            Hotkey = null,
            Label = "提及成员",
            Placeholder = "输入姓名或邮箱",
            Trigger = new Button { Variant = ButtonVariant.Soft, Icon = Lucide.AtSign, Content = "提及成员" },
            ItemTemplate = PersonTemplate(),
            Empty = new Empty { Icon = Lucide.UserPlus, Title = "没有找到这个人", Description = "可以先邀请对方加入团队", Size = TextSize.Sm },
        };
        var mentioned = new Small { Tone = TextTone.Muted, Text = "还没有提及谁" };
        palette.ItemInvoked += (_, e) =>
        {
            var person = (Person)e.Item!;
            mentioned.Text = $"已提及 @{person.Name}（{person.Team}）";
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { palette, mentioned } };
    }
}
