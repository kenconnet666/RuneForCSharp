using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>
/// AppShell 页的示例（vue-tsx-pro 文档站 patterns/AppShell）：admin、fullscreen、mixed 同上游。
/// window 是桌面特有的：在新窗口里打开外壳，顶栏兼作标题栏。
/// </summary>
static class AppShellExamples
{
    public static Example[] All =>
    [
        new("AppShell", "admin", "后台", "顶栏放品牌、搜索、通知与账号菜单；侧栏是分组的导航；宽于 80rem 时显示右侧栏。外壳按自己的宽度排版：宽于 64rem 时侧栏常驻，更窄时收进从起始边滑出的抽屉（顶栏最前面出现菜单按钮），点了导航项抽屉自己关上。", Admin, Framed: true),
        new("AppShell", "fullscreen", "全屏", "顶栏结束边默认有全屏按钮（Fullscreenable）：进入后不显示顶栏和左侧栏，正文占满；外壳是窗口的内容时窗口同时全屏。右上角留一个退出按钮，Esc 也退出。IsFullscreen 可以双向绑定，隐藏的顶栏和侧栏还在，退出后滚动位置、展开的分组都还在。", Fullscreen, Framed: true),
        new("AppShell", "mixed", "混合布局", "顶栏是一级导航（Nav 放 NavBar），侧栏是当前模块的二级导航；SidebarCollapsible 时侧栏底部的按钮把它收成图标栏，二级导航只剩图标，悬停看名称。账号只留头像（UserButton 的 Compact）。", Mixed, Framed: true),
        new("AppShell", "window", "顶栏兼作标题栏", "桌面特有：外壳是窗口的内容时（ExtendsContentIntoTitleBar，默认开），去掉系统的标题栏，顶栏的空白处拖动窗口、双击最大化，结束边是自绘的最小化、最大化、关闭；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。", WindowDemo),
    ];

    /// <summary>示例的效果区（TS 的 frame）：fullscreen 24rem，admin、mixed 40rem。</summary>
    static Control Frame(AppShell shell, double rem = 24) => new Border { Height = rem * 16, ClipToBounds = true, Child = shell };

    static NavList Navigation()
    {
        var list = new NavList { CurrentHref = "home" };
        AutomationProperties.SetName(list, "导航");
        list.Children.AddRange(
        [
            new NavItem { Href = "home", Icon = Lucide.House, Content = "工作台" },
            new NavItem { Href = "contracts", Icon = Lucide.FileText, Content = "合同" },
            new NavItem { Href = "suppliers", Icon = Lucide.Users, Content = "供应商" },
        ]);
        return list;
    }

    sealed record Contract(string Id, string Supplier, string Owner, decimal Amount, string Signed, string Status);

    static readonly Contract[] Contracts =
    [
        new("HT-2026-031", "华辰精密", "林小满", 128000, "2026-10-08", "执行中"),
        new("HT-2026-030", "青禾线缆", "周一航", 86500, "2026-10-02", "执行中"),
        new("HT-2026-029", "北辰电子", "陈默", 245300, "2026-09-25", "待验收"),
        new("HT-2026-028", "恒通紧固件", "许悠", 6800, "2026-09-18", "已完成"),
        new("HT-2026-027", "远山钣金", "王晴", 57200, "2026-09-11", "已完成"),
    ];

    // 全屏多用来对着一张大表工作：正文是一张合同台账
    static Control Fullscreen()
    {
        var hint = new Small { Tone = TextTone.Muted, Text = "点右边的按钮进入全屏" };
        var title = new H1 { Text = "合同台账" };
        var table = new ColumnTable
        {
            ItemsSource = Contracts,
            Columns =
            [
                new("Id", "合同编号") { Width = 130 },
                new("Supplier", "供应商"),
                new("Owner", "负责人") { Width = 90 },
                new("Amount", "金额（元）") { Type = ColumnType.Number, Width = 120 },
                new("Signed", "签订日期") { Width = 120 },
                new("Status", "状态") { Width = 90 },
            ],
        };
        AutomationProperties.SetName(table, "合同台账");
        var shell = new AppShell
        {
            Brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" },
            Header = hint,
            Sidebar = Navigation(),
            Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { title, table } },
        };
        shell.PropertyChanged += (_, e) =>
        {
            if (e.Property != AppShell.IsFullscreenProperty) return;
            hint.Text = shell.IsFullscreen ? "全屏中" : "点右边的按钮进入全屏";
            title.Text = shell.IsFullscreen ? "合同台账（全屏）" : "合同台账";
        };
        return Frame(shell);
    }

    static readonly (string Key, string Label, IconData? Icon)[] AdminPages =
    [
        ("overview", "概览", Lucide.House), ("orders", "订单", Lucide.ShoppingCart), ("products", "商品", Lucide.Package),
        ("customers", "客户", Lucide.Users), ("reports", "报表", Lucide.ChartLine), ("team", "团队成员", null), ("billing", "账单", null),
    ];

    static readonly (string Id, string Customer, string Amount, StatusTone Tone, string Status)[] RecentOrders =
    [
        ("#10234", "林小满", "¥1,280.00", StatusTone.Success, "已发货"),
        ("#10233", "周一航", "¥329.00", StatusTone.Warning, "待付款"),
        ("#10232", "陈默", "¥4,299.00", StatusTone.Info, "备货中"),
        ("#10231", "许悠", "¥659.00", StatusTone.Success, "已完成"),
    ];

    // 示例里不真的换页：导航只改 CurrentHref，页头跟着换标题；抽屉里点了导航项要自己关上
    static Control Admin()
    {
        NavItem Item(string key)
        {
            var (_, label, icon) = AdminPages.Single(page => page.Key == key);
            return new NavItem { Href = key, Icon = icon, Content = label, End = key == "orders" ? new Badge { Radius = Radius.Full, Content = "12" } : null };
        }
        var nav = new NavList { CurrentHref = "overview" };
        AutomationProperties.SetName(nav, "后台");
        var settings = new NavGroup { Label = "设置", Icon = Lucide.Settings };
        settings.Items.AddRange([Item("team"), Item("billing")]);
        var work = new NavSection { Label = "工作台" };
        work.Items.AddRange([Item("overview"), Item("orders"), Item("products"), Item("customers")]);
        var analysis = new NavSection { Label = "分析" };
        analysis.Items.AddRange([Item("reports"), settings]);
        nav.Children.AddRange([work, analysis]);

        var search = new TextField { Type = TextFieldType.Search, Icon = Lucide.Search, PlaceholderText = "搜索订单、客户", Width = 16 * 16 };
        AutomationProperties.SetName(search, "搜索");
        var bell = new IconButton { Icon = Lucide.Bell, HorizontalAlignment = HorizontalAlignment.Right };
        AutomationProperties.SetName(bell, "通知");
        var billing = new MenuItem { Content = "账单与设置", Icon = Lucide.Settings };
        billing.Click += (_, _) => nav.CurrentHref = "billing";
        var account = new Menu
        {
            Trigger = new UserButton { DisplayName = "林小满" },
            Items =
            {
                new MenuGroup { Label = "xiaoman@example.com", Items = { billing } },
                new Separator { Decorative = false },
                new MenuItem { Content = "退出登录", Icon = Lucide.LogOut, Tone = MenuItemTone.Danger },
            },
        };
        // 通知与头像靠顶栏的结束边
        var header = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { search, bell, account } };
        Flex.SetGrow(bell, true);

        var page = new PageHeader { Title = "概览", Description = "宽于 64rem 时侧栏常驻，更窄时收进抽屉（顶栏最前面出现菜单按钮）；宽于 80rem 时显示右侧栏。" };
        var percent = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 };
        var stats = new Grid { MinItemWidth = "11rem", Gap = Space.Md, Balanced = true };
        stats.Children.AddRange(
        [
            new Card { Content = new Stat { Label = "今日销售额", Value = 48_210, Unit = "元", Change = 0.124m } },
            new Card { Content = new Stat { Label = "订单数", Value = 326, Change = 0.058m } },
            new Card { Content = new Stat { Label = "新客户", Value = 41, Change = -0.032m } },
            new Card { Content = new Stat { Label = "退款率", Value = 0.018m, Format = percent, Change = -0.2m, InvertChange = true } },
        ]);
        var body = new TableBody();
        foreach (var (id, customer, amount, tone, status) in RecentOrders)
            body.Rows.Add(new TableRow { Cells = { new TableCell { Content = id }, new TableCell { Content = customer }, new TableCell { Content = amount }, new TableCell { Content = new Badge { Tone = tone, Content = status } } } });
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "订单号" }, new TableHeaderCell { Content = "客户" }, new TableHeaderCell { Content = "金额" }, new TableHeaderCell { Content = "状态" } } };
        var table = new Table { Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "最近订单");

        var shell = new AppShell
        {
            Brand = new AppShellBrand { Icon = Lucide.Box, Content = "小满商城后台" },
            Header = header,
            Sidebar = nav,
            Aside = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children = { new Strong { Text = "今日提醒" }, new Small { Tone = TextTone.Muted, Text = "3 笔订单超过 24 小时未发货" }, new Small { Tone = TextTone.Muted, Text = "“云朵沙发”库存只剩 5 件" } },
            },
            Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Xl, Align = FlexAlign.Stretch, Children = { page, stats, new Card { Header = new Strong { Text = "最近订单" }, Content = table } } },
        };
        nav.PropertyChanged += (_, e) =>
        {
            if (e.Property != NavList.CurrentHrefProperty) return;
            page.Title = AdminPages.Single(item => item.Key == nav.CurrentHref).Label;
            shell.IsSidebarOpen = false;
        };
        return Frame(shell, 40);
    }

    static readonly (string Key, string Title, IconData Icon)[] SidePages =
    [
        ("po", "采购单", Lucide.ReceiptText), ("quote", "询价比价", Lucide.Scale), ("contract", "合同", Lucide.FilePenLine),
        ("suppliers", "供应商", Lucide.Users), ("logistics", "到货跟踪", Lucide.Truck),
    ];

    // 各页的几个指标：示例里页面只有标题与指标
    static readonly Dictionary<string, (string Label, decimal Value, string? Unit)[]> PageStats = new()
    {
        ["home"] = [("待我审批", 6, null), ("本月采购额", 1_284_000, "元"), ("在途订单", 23, null)],
        ["po"] = [("草稿", 4, null), ("审批中", 9, null), ("已下单", 57, null)],
        ["quote"] = [("进行中的询价", 2, null), ("已收报价", 11, null), ("平均降幅", 6.8m, "%")],
        ["contract"] = [("生效中", 38, null), ("30 天内到期", 3, null), ("待盖章", 1, null)],
        ["suppliers"] = [("合格供应商", 142, null), ("考察中", 7, null), ("本季新增", 5, null)],
        ["logistics"] = [("今日到货", 8, null), ("延误", 2, null), ("待验收", 5, null)],
    };

    static readonly NavMenuItem[] Modules =
    [
        new("工作台") { Href = "#home", Icon = Lucide.House },
        new("采购") { Href = "#purchase", Icon = Lucide.ShoppingCart },
        new("报表") { Href = "#reports", Icon = Lucide.ChartColumn },
        new("文档") { Href = "#docs", Icon = Lucide.FileText },
    ];

    static readonly (string Title, string From, string Due)[] Todos =
    [
        ("审核供应商资质：华辰精密", "采购部 · 林小满", "今天"),
        ("确认报价：线束卡箍二期", "商务部 · 周一航", "明天"),
        ("复核对账单：九月", "财务部 · 陈默", "周五"),
    ];

    // 顶栏是一级导航，侧栏是“采购”模块的二级导航；侧栏底部的按钮把侧栏收成图标栏
    static Control Mixed()
    {
        var modules = new NavBar { Items = Modules };
        AutomationProperties.SetName(modules, "模块");
        var nav = new NavList();
        AutomationProperties.SetName(nav, "采购");
        foreach (var (key, title, icon) in SidePages) nav.Children.Add(new NavItem { Href = key, Icon = icon, Badge = key == "quote" ? 2 : 0, Content = title });
        var heading = new H1 { Text = "工作台" };
        var stats = new Grid { MinItemWidth = "10rem", Gap = Space.Md, Balanced = true };
        void Show(string key)
        {
            heading.Text = SidePages.FirstOrDefault(page => page.Key == key).Title ?? "工作台";
            stats.Children.Clear();
            foreach (var (label, value, unit) in PageStats[key]) stats.Children.Add(new Card { Content = new Stat { Label = label, Value = value, Unit = unit } });
        }
        nav.PropertyChanged += (_, e) => { if (e.Property == NavList.CurrentHrefProperty && nav.CurrentHref is { } key) Show(key); };
        Show("home");
        var todos = new ItemList { Divided = true, Inset = Space.Sm };
        foreach (var (title, from, due) in Todos)
            todos.Children.Add(new ItemListItem { Title = title, Description = from, End = new Small { Tone = TextTone.Muted, Text = due } });
        var shell = new AppShell
        {
            Brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" },
            Nav = modules,
            // 顶栏放了一级导航，账号只留头像
            Header = new Menu { Trigger = new UserButton { DisplayName = "林小满", Compact = true }, Items = { new MenuItem { Content = "退出登录", Icon = Lucide.LogOut, Tone = MenuItemTone.Danger } } },
            Sidebar = nav,
            SidebarCollapsible = true,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
                Children = { heading, stats, new Card { Header = new Strong { Text = "待我处理" }, Padding = Space.Sm, Content = todos } },
            },
        };
        return Frame(shell, 40);
    }

    static Control WindowDemo()
    {
        var open = new Button { Content = "在新窗口打开", Icon = Lucide.AppWindow, HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) =>
        {
            var toc = new TableOfContents
            {
                Items = [new HeadingEntry("overview", "概览", 2), new HeadingEntry("drag", "拖动与贴靠", 2), new HeadingEntry("buttons", "窗口按钮", 2)],
            };
            var article = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new H1 { Text = "采购总览" },
                    new H2 { Name = "overview", Text = "概览" },
                    new Paragraph { Text = "这个窗口没有系统的标题栏：顶栏就是标题栏。窗口变窄到 1024 以下时侧栏收进抽屉，1280 以下不显示右侧栏。" },
                    new H2 { Name = "drag", Text = "拖动与贴靠" },
                    new Paragraph { Text = "拖顶栏的空白处移动窗口，双击最大化；拖到屏幕边缘贴边，悬停最大化按钮弹出 Windows 11 的贴靠布局。" },
                    new H2 { Name = "buttons", Text = "窗口按钮" },
                    new Paragraph { Text = "最小化、最大化（还原）、关闭都是自绘的，跟着主题与暗色模式；关闭按钮悬停是 danger 底。" },
                },
            };
            var shell = new AppShell
            {
                Brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" },
                Header = new Small { Tone = TextTone.Muted, Text = "拖动这里移动窗口" },
                Sidebar = Navigation(),
                SidebarCollapsible = true,
                Aside = toc,
                Footer = new Small { Text = "© 2026 采购协同" },
                Content = article,
            };
            var window = new Window { Title = "采购协同", Width = 1320, Height = 760, Content = shell };
            toc.ScrollRoot = shell.MainScroll;
            window.Show();
        };
        return open;
    }
}
