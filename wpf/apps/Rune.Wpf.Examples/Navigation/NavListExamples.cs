using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>NavList 的示例（vue-tsx-pro 文档站 navigation/NavList，本页目录 TableOfContents 也在这页）。</summary>
static class NavListExamples
{
    public static Example[] All =>
    [
        new("NavList", "groups", "后台导航", "当前页按 NavList 的 CurrentHref 判断（也可以用 Active 指定），读屏的项状态是\"当前页\"，并且高亮；含当前页的 NavGroup 自动展开。End 放计数之类的尾部内容。", Groups),
        new("NavList", "compact", "窄侧栏：只显示图标", "Compact 时各项只剩图标，名称留给读屏、悬停或聚焦时用提示显示；NavSection 的 Icon 是小节标题前的标记（不能点），紧凑时代替标题。NavItem 的 Badge 是计数角标，展开时在文字后面，紧凑时是图标上的小圆点。", Compact),
        new("NavList", "sections", "文档站导航", "NavSection 是带小标题、不能收起的一组：链接多、按主题分组的侧栏。不可用的项不能点、不能聚焦，End 放在项的末尾（计数、标记）。", Sections),
        new("NavList", "toc", "本页目录", "TableOfContents 列出各节标题，滚动时高亮正在读的一节，指示条滑过去；当前节的读屏项状态是\"当前位置\"。目录项的 Id 是标题元素的 Name；正文所在的滚动区域给 ScrollRoot。", Contents),
    ];

    /// <summary>15rem（根字号 16）宽的卡片：侧栏的宽度。</summary>
    static Card Sidebar(FrameworkElement content, Space padding) => new() { Padding = padding, Width = 15 * 16, HorizontalAlignment = HorizontalAlignment.Left, Content = content };

    static FrameworkElement Groups()
    {
        // 示例里不真的换页：地址只是标识，NavList 记着当前项
        var list = new NavList { CurrentHref = "orders-all" };
        AutomationProperties.SetName(list, "后台");
        NavItem Item(string key, string label, IconData? icon = null, int? end = null) =>
            new() { Href = key, Icon = icon, Content = label, End = end is { } count ? new Badge { Radius = Radius.Full, Content = count.ToString() } : null };
        list.Children.Add(Item("home", "概览", Lucide.House));
        list.Children.Add(new NavGroup { Label = "订单", Icon = Lucide.ShoppingCart, Items = { Item("orders-all", "全部订单", end: 12), Item("orders-refund", "退款售后", end: 3), Item("orders-invoice", "发票") } });
        list.Children.Add(new NavGroup { Label = "商品", Icon = Lucide.Package, Items = { Item("products", "商品列表"), Item("inventory", "库存") } });
        list.Children.Add(Item("customers", "客户", Lucide.Users));
        list.Children.Add(Item("reports", "报表", Lucide.ChartLine));
        list.Children.Add(Item("settings", "设置", Lucide.Settings));
        return Sidebar(list, Space.Sm);
    }

    static FrameworkElement Sections()
    {
        (string Label, string[] Pages)[] sections =
        [
            ("指南", ["快速开始", "主题定制", "国际化"]),
            ("组件", ["Button 按钮", "Dialog 对话框", "Tabs 标签页", "DataTable 数据表格"]),
            ("资源", ["更新日志", "设计资源"]),
        ];
        var list = new NavList { CurrentHref = "Dialog 对话框" };
        AutomationProperties.SetName(list, "文档");
        foreach (var (label, pages) in sections)
        {
            var section = new NavSection { Label = label };
            foreach (var page in pages)
            {
                // 还没上线的页面：不能点、不能聚焦
                var soon = page == "设计资源";
                section.Items.Add(new NavItem { Href = page, Content = page, IsEnabled = !soon, End = soon ? new Badge { Variant = StatusVariant.Soft, Content = "即将上线" } : null });
            }
            list.Children.Add(section);
        }
        return Sidebar(list, Space.Sm);
    }

    static FrameworkElement Compact()
    {
        var toggle = new Switch { Content = "只显示图标", IsChecked = true };
        var list = new NavList { CurrentHref = "单价台账" };
        AutomationProperties.SetName(list, "销售管理");
        list.Children.Add(new NavItem { Href = "首页", Icon = Lucide.House, Content = "首页" });
        list.Children.Add(new NavGroup
        {
            Label = "审批", Icon = Lucide.ClipboardCheck,
            Items = { new NavItem { Href = "待我审批", Content = "待我审批", Badge = 4 }, new NavItem { Href = "我发起的", Content = "我发起的" } },
        });
        (string Label, IconData Icon, (string Page, IconData Icon, int Count)[] Pages)[] sections =
        [
            ("项目", Lucide.FolderKanban, [("单价台账", Lucide.Wrench, 0), ("库存台账", Lucide.Gauge, 0)]),
            ("收益", Lucide.CircleDollarSign, [("财务复核", Lucide.BadgeCheck, 12), ("总账", Lucide.BookOpenText, 0)]),
        ];
        foreach (var (label, icon, pages) in sections)
        {
            var section = new NavSection { Label = label, Icon = icon };
            foreach (var (page, pageIcon, count) in pages) section.Items.Add(new NavItem { Href = page, Icon = pageIcon, Content = page, Badge = count });
            list.Children.Add(section);
        }
        var card = Sidebar(list, Space.Xs);
        void Apply()
        {
            list.Compact = toggle.IsChecked == true;
            card.Width = list.Compact ? 3.5 * 16 : 15 * 16;
        }
        DependencyPropertyDescriptor.FromProperty(Switch.IsCheckedProperty, typeof(Switch)).AddValueChanged(toggle, (_, _) => Apply());
        Apply();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { toggle, card } };
    }

    /// <summary>本页目录：名字带前缀（同一页上的名字不能重复）。</summary>
    static readonly HeadingEntry[] Headings =
    [
        new("toc_demo_install", "安装", 2),
        new("toc_demo_npm", "用 npm 安装", 3),
        new("toc_demo_cdn", "用 CDN 引入", 3),
        new("toc_demo_usage", "使用", 2),
        new("toc_demo_theme", "主题", 2),
        new("toc_demo_faq", "常见问题", 2),
    ];

    const string Filler = "组件库按需引入，样式在用到时才生成，页面只带用到的部分。每个组件都可以用 sx 覆盖样式，用 propsXxx 给内部的部件加属性。";

    static FrameworkElement Contents()
    {
        var article = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Margin = new Thickness(0, 0, 0.75 * 14, 0) };
        foreach (var heading in Headings)
        {
            FrameworkElement title = heading.Level == 2 ? new H3 { Text = heading.Label, Size = TextSize.Lg } : new H4 { Text = heading.Label, Size = TextSize.Md };
            title.Name = heading.Id;
            article.Children.Add(title);
            article.Children.Add(new Paragraph { Text = Filler, Size = TextSize.Sm });
            article.Children.Add(new Paragraph { Text = Filler, Size = TextSize.Sm });
        }
        // 18rem 高的正文、9rem 宽的目录，中间隔 space.lg（rem 按根字号 16）
        var scroll = new ScrollArea { Height = 18 * 16, Content = article };
        AutomationProperties.SetName(scroll, "文章");
        var toc = new TableOfContents { Items = Headings, ScrollRoot = scroll, Title = "本文目录", Width = 9 * 16, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var layout = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(toc, System.Windows.Controls.Dock.Right);
        layout.Children.Add(toc);
        layout.Children.Add(scroll);
        return new Card { Content = layout };
    }
}
