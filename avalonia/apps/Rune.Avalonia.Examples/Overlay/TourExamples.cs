using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Tour 的示例（vue-tsx-pro 文档站 overlay/Tour）。封面同 Image 的示例（docs/site/drawings）。</summary>
static class TourExamples
{
    public static Example[] All =>
    [
        new("Tour", "onboarding", "新手引导", "Steps 是各步：Target 是要高亮的控件（目标晚于引导才建出来时用 TargetFactory），不写时卡片居中；Placement 是卡片在哪一侧（放不下时翻转）；Cover 是标题上方的封面。引导是模态的：遮罩盖住窗口、目标处镂空，← → 切换步骤，Esc 或“跳过”随时退出；点“完成”发 Finished。", Onboarding),
    ];

    static readonly (string Number, string Customer, string Amount, StatusTone Tone, string Status)[] Orders =
    [
        ("#10234", "林小满", "¥1,280.00", StatusTone.Success, "已发货"),
        ("#10233", "周一航", "¥329.00", StatusTone.Warning, "待付款"),
        ("#10232", "陈默", "¥4,299.00", StatusTone.Info, "备货中"),
    ];

    // 订单中心的引导：欢迎（封面）→ 搜索 → 筛选 → 导出（卡片在左侧）→ 订单列表（卡片在上方）
    static Control Onboarding()
    {
        var search = new TextField { Type = TextFieldType.Search, PlaceholderText = "搜索订单号、客户、手机号" };
        AutomationProperties.SetName(search, "搜索订单");
        Flex.SetGrow(search, true);
        var filter = new IconButton { Icon = Lucide.ListFilter, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
        AutomationProperties.SetName(filter, "筛选");
        var export = new Button { Icon = Lucide.Download, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = "导出" };
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "订单号" }, new TableHeaderCell { Content = "客户" }, new TableHeaderCell { Content = "金额" }, new TableHeaderCell { Content = "状态" } } };
        var body = new TableBody();
        foreach (var order in Orders)
            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableCell { Content = order.Number },
                    new TableCell { Content = order.Customer },
                    new TableCell { Content = order.Amount },
                    new TableCell { Content = new Badge { Tone = order.Tone, Variant = StatusVariant.Soft, Content = order.Status } },
                },
            });
        var table = new Table { Density = TableDensity.Compact, Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "订单");
        var note = new Small { Tone = TextTone.Muted, Text = "← → 切换步骤，Esc 或“跳过”随时退出" };
        var tour = new Tour
        {
            Steps =
            [
                new TourStep
                {
                    Title = "欢迎使用订单中心",
                    Content = "花一分钟认识这几个常用功能。",
                    // 封面：示例图纸（真实应用放产品截图或插画）
                    Cover = new Image { Source = new Uri("drawings/DWG-1021.png", UriKind.Relative), Alt = "", Fit = ImageFit.Cover, Height = 112, HorizontalAlignment = HorizontalAlignment.Stretch },
                },
                new TourStep { Target = search, Title = "搜索", Content = "按订单号、客户姓名或手机号查找订单。" },
                new TourStep { Target = filter, Title = "筛选", Content = "按状态、日期、金额筛选，条件会记住。" },
                new TourStep { Target = export, Title = "导出", Content = "把当前筛选结果导出为 Excel。", Placement = FloatingPlacement.Left },
                new TourStep { Target = table, Title = "订单列表", Content = "点订单号查看详情，右键有更多操作。", Placement = FloatingPlacement.Top },
            ],
        };
        tour.Finished += (_, _) => note.Text = "已完成引导";
        var start = new Button { Icon = Lucide.Sparkles, Content = "开始引导" };
        start.Click += (_, _) =>
        {
            tour.Current = 0;
            tour.IsOpen = true;
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { start, note } },
                new Card
                {
                    Padding = Space.Sm,
                    Content = new Flex
                    {
                        Direction = FlexDirection.Column, Gap = Space.Sm,
                        Children = { new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { search, filter, export } }, table },
                    },
                },
                tour,
            },
        };
    }
}
