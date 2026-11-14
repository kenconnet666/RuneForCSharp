namespace Rune.WinForms.Examples;

/// <summary>Breadcrumbs 与 Steps 的示例（vue-tsx-pro 文档站 navigation/Breadcrumbs）。WinForms 里是 BreadcrumbsControl、StepsControl。</summary>
static class BreadcrumbsExamples
{
    public static Example[] All =>
    [
        new("Breadcrumbs", "breadcrumbs", "面包屑", "最后一项是当前页（读屏的说明是\"当前页\"），不是链接。路径太长时 MaxItems 把中间几级收进菜单（同 Radix Themes），不会把布局撑乱。分隔符从右到左排版时翻转。", Paths),
        new("Breadcrumbs", "steps", "下单流程", "Current 是当前步骤的序号，变了发 CurrentChanged；Navigable 时已完成的步骤可以点回去，有 Href 的已完成步骤是链接。已完成的显示对勾，读屏的说明是这一步的状态（当前步骤、已完成……）；图标可以自定义，StepItem 的 Status 改写某一步的状态（出错的一步）。", Checkout),
        new("Breadcrumbs", "vertical", "竖排与出错", "Orientation=Vertical 适合说明文字长的流程，步骤之间有竖的连接线；Status=Error 标出出错的步骤，IsDisabled 的步骤不能点。", Deploy),
    ];

    static Control Checkout()
    {
        StepItem[] items =
        [
            new("购物车") { Description = "3 件商品", Icon = Lucide.ShoppingCart }, new("收货地址") { Description = "上海市徐汇区", Icon = Lucide.MapPin },
            new("支付") { Description = "微信 / 支付宝", Icon = Lucide.CreditCard }, new("完成") { Icon = Lucide.PackageCheck },
        ];
        var steps = new StepsControl { Items = items, Current = 1, Navigable = true };
        var body = new ParagraphControl { MinimumSize = new Size(0, 4 * 16) };
        var back = new ButtonControl { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "上一步" };
        var next = new ButtonControl();
        void Sync()
        {
            var step = steps.Current;
            body.Text = step < items.Length - 1 ? $"第 {step + 1} 步：{items[step].Title}。已完成的步骤可以点回去修改。" : "订单已提交，预计明天送达。";
            back.Enabled = step > 0;
            next.Enabled = step < items.Length - 1;
            next.Text = step == items.Length - 2 ? "提交订单" : "下一步";
        }
        back.Click += (_, _) => steps.Current--;
        next.Click += (_, _) => steps.Current++;
        steps.CurrentChanged += (_, _) => Sync();
        Sync();
        var actions = Build.Flex(new FlexControl { Justify = FlexJustify.End, Gap = Space.Sm, AutoSize = true }, back, next);
        var card = Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true }, body, actions));
        var column = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true }, steps, card);
        column.Align = FlexAlign.Stretch;
        return column;
    }

    static Control Deploy() => new StepsControl
    {
        Orientation = StepsOrientation.Vertical, TextSize = TextSize.Sm, Current = 2,
        Items =
        [
            new("拉取代码") { Description = "main@3f9c2e1 · 12 秒" }, new("安装依赖") { Description = "pnpm install · 41 秒" },
            new("运行测试") { Description = "978 个通过，2 个失败", Status = StepState.Error }, new("构建") { Description = "等待测试通过" },
            new("发布到生产环境") { Description = "需要审批", IsDisabled = true },
        ],
    };

    static Control Paths()
    {
        // 示例里的链接只写锚点：点了不离开文档
        BreadcrumbItem[] deep =
        [
            new("首页", "#home", Lucide.House), new("项目", "#projects"), new("官网改版", "#redesign"),
            new("设计稿", "#designs"), new("2026 秋季", "#autumn"), new("首页 v3.fig"),
        ];
        Control Case(string caption, BreadcrumbsControl breadcrumbs, string name)
        {
            breadcrumbs.AccessibleName = name;
            return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, new SmallControl { Tone = TextTone.Muted, Text = caption }, breadcrumbs);
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true },
            Case("默认：最后一项是当前页，不是链接", new BreadcrumbsControl { Items = [new("首页", "#home", Lucide.House), new("订单", "#orders"), new("#10234")] }, "订单位置"),
            Case("MaxItems=4：中间几级收进菜单", new BreadcrumbsControl { Items = deep, MaxItems = 4 }, "设计稿位置"),
            Case("自定义分隔符与图标", new BreadcrumbsControl
            {
                Separator = Lucide.Slash,
                Items = [new("vue-tsx-pro", "#repo", Lucide.Folder), new("packages", "#packages", Lucide.Folder), new("ui", "#ui", Lucide.Folder), new("src")],
            }, "代码位置"));
    }
}
