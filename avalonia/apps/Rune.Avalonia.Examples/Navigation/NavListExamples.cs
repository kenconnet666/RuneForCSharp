using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>NavList 的示例（vue-tsx-pro 文档站 navigation/NavList，本页目录 TableOfContents 也在这页）。</summary>
static class NavListExamples
{
    public static Example[] All =>
    [
        new("NavList", "groups", "后台导航", "当前页按 NavList 的 CurrentHref 判断（也可以用 Active 指定），读屏的项状态是\"当前页\"，并且高亮；含当前页的 NavGroup 自动展开。End 放计数之类的尾部内容。", Groups),
        new("NavList", "compact", "窄侧栏：只显示图标", "Compact 时各项只剩图标，名称留给读屏、悬停或聚焦时用提示显示；NavSection 的 Icon 是小节标题前的标记（不能点），紧凑时代替标题。NavItem 的 Badge 是计数角标，展开时在文字后面，紧凑时是图标上的小圆点。", Compact),
        new("NavList", "sections", "文档站导航", "NavSection 是带小标题、不能收起的一组：链接多、按主题分组的侧栏。不可用的项不能点、不能聚焦，End 放在项的末尾（计数、标记）。", Sections),
        new("NavList", "toc", "本页目录", "TableOfContents 列出各节标题，滚动时高亮正在读的一节，指示条滑过去；当前节的读屏项状态是\"当前位置\"。目录项的 Id 是标题控件的 Name；正文所在的滚动区域给 ScrollRoot。", Contents),
    ];

    /// <summary>15rem（根字号 16）宽的卡片：侧栏的宽度。</summary>
    static Card Sidebar(Control content, Space padding) => new() { Padding = padding, Width = 15 * 16, HorizontalAlignment = HorizontalAlignment.Left, Content = content };

    static Control Groups()
    {
        // 示例里不真的换页：地址只是标识，NavList 记着当前项
        var list = new NavList { CurrentHref = "orders-all" };
        AutomationProperties.SetName(list, "后台");
        NavItem Item(string key, string label, IconData? icon = null, int? end = null) =>
            new() { Href = key, Icon = icon, Content = label, End = end is { } count ? new Badge { Radius = Radius.Full, Content = count.ToString() } : null };
        list.Children.AddRange(
        [
            Item("home", "概览", Lucide.House),
            new NavGroup { Label = "订单", Icon = Lucide.ShoppingCart, Items = { Item("orders-all", "全部订单", end: 12), Item("orders-refund", "退款售后", end: 3), Item("orders-invoice", "发票") } },
            new NavGroup { Label = "商品", Icon = Lucide.Package, Items = { Item("products", "商品列表"), Item("inventory", "库存") } },
            Item("customers", "客户", Lucide.Users),
            Item("reports", "报表", Lucide.ChartLine),
            Item("settings", "设置", Lucide.Settings),
        ]);
        return Sidebar(list, Space.Sm);
    }

    static Control Sections()
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

    static Control Compact()
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
        toggle.PropertyChanged += (_, e) =>
        {
            if (e.Property == Switch.IsCheckedProperty) Apply();
        };
        Apply();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { toggle, card } };
    }

    /// <summary>本页目录：名字带前缀（同一页上的名字不能重复）。</summary>
    static readonly HeadingEntry[] Headings =
    [
        new("toc-demo-install", "安装", 2),
        new("toc-demo-npm", "用 npm 安装", 3),
        new("toc-demo-cdn", "用 CDN 引入", 3),
        new("toc-demo-usage", "使用", 2),
        new("toc-demo-theme", "主题", 2),
        new("toc-demo-faq", "常见问题", 2),
    ];

    /// <summary>每一节的正文。</summary>
    static readonly Dictionary<string, string[]> TocSections = new()
    {
        ["toc-demo-install"] = ["支持 Vue 3.5 以上与 Vite 6 以上；组件按需引入，样式在用到时才生成。"],
        ["toc-demo-npm"] = ["用 pnpm add 安装组件库与 TSX 运行时两个包，再在 vite.config.ts 里加上插件。", "升级时两个包一起升到同一个版本，发布说明里写了每一版对使用方的变化。"],
        ["toc-demo-cdn"] = ["没有构建工具的页面可以从 CDN 引入打包好的 ES 模块，用 import map 指向 Vue。", "这种方式没有按需生成样式，页面会带上全部组件的样式，适合原型与演示。"],
        ["toc-demo-usage"] = ["在 main.ts 里调用 app.use(createUi())，之后在任意组件里直接写 Button、Field。", "每个组件都能用 sx 覆盖样式，用 propsXxx 给里面的部件加属性，层层合并。"],
        ["toc-demo-theme"] = ["内置十几套主题，createUi 的 theme 指定默认主题，Provider 可以给一块区域换主题。", "主题定制页能实时调品牌色、圆角、缩放并检查对比度，导出的代码直接粘进项目。"],
        ["toc-demo-faq"] = ["服务端渲染时样式随页面一起输出，水合后不会闪；内容安全策略用 nonce 放行。", "要改某个组件全站的默认值，写在 createUi 的 defaults 里，不用每处都传。"],
    };

    static Control Contents()
    {
        var article = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Margin = new Thickness(0, 0, 0.75 * 14, 0) };
        foreach (var heading in Headings)
        {
            Control title = heading.Level == 2 ? new H3 { Text = heading.Label, Size = TextSize.Lg } : new H4 { Text = heading.Label, Size = TextSize.Md };
            title.Name = heading.Id;
            article.Children.Add(title);
            article.Children.AddRange(TocSections[heading.Id].Select(text => new Paragraph { Text = text, Size = TextSize.Sm }));
        }
        // 18rem 高的正文、9rem 宽的目录，中间隔 space.lg（rem 按根字号 16）
        var scroll = new ScrollArea { Height = 18 * 16, Content = article };
        AutomationProperties.SetName(scroll, "文章");
        var toc = new TableOfContents { Items = Headings, ScrollRoot = scroll, Title = "本文目录", Width = 9 * 16, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var layout = new DockPanel { Children = { toc, scroll } };
        DockPanel.SetDock(toc, Dock.Right);
        return new Card { Content = layout };
    }
}
