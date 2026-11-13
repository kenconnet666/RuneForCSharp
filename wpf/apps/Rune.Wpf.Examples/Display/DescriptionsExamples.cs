using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>Descriptions 的示例（vue-tsx-pro 文档站 display/Descriptions）。</summary>
static class DescriptionsExamples
{
    public static Example[] All =>
    [
        new("Descriptions", "basic", "基础用法", "Items 每项一个标签与值，值可以是任意内容；Columns 分栏，Span 跨列。读屏里每项是一组、名称是标签；列表窄于 32em 时自动排成一列。", Basic),
        new("Descriptions", "layouts", "边框、竖排与尺寸", "Bordered 是表格样式，Layout=\"vertical\" 标签在上，Layout=\"inline\" 各项一行排开、放不下换行（详情页页头里的编号、团队、主办），Size 跟字号档位缩放。", Layouts),
        new("Descriptions", "auto", "自适应栏数与复制：供应商档案", "Columns=\"auto\" 时按列表的宽度定栏数：每栏不窄于 MinItemWidth（默认 240），最多 MaxColumns（默认 4）栏，同一份详情放在抽屉、整页里各自合适；跨列按算出的栏数封顶。项写 Copyable 时值后面有复制按钮（名称“复制 + 标签”），复制的是值的文字，要复制完整写法时用 CopyText 指定。", Supplier),
    ];

    static Descriptions Of(Descriptions descriptions, params DescriptionItem[] items)
    {
        foreach (var item in items) descriptions.Items.Add(item);
        return descriptions;
    }

    static FrameworkElement Basic()
    {
        var copy = new CopyButton { Value = "A-10234", Size = TextSize.Xs };
        System.Windows.Automation.AutomationProperties.SetName(copy, "复制订单号");
        return new Card
        {
            Content = Of(new Descriptions { Title = "订单信息", Columns = 3 },
                new DescriptionItem { Label = "订单号", Value = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Text { Text = "A-10234" }, copy } } },
                new DescriptionItem { Label = "状态", Value = new Badge { Tone = StatusTone.Success, Radius = Radius.Full, Content = "已发货" } },
                new DescriptionItem { Label = "下单时间", Value = "2026-10-18 14:32" },
                new DescriptionItem { Label = "收货人", Value = "林小满" },
                new DescriptionItem { Label = "电话", Value = "138****0021" },
                new DescriptionItem { Label = "物流", Value = new Text { Inlines = { new Link { Href = new Uri("https://example.com/track/SF1234567890"), Inlines = { new System.Windows.Documents.Run("SF1234567890") } } } } },
                new DescriptionItem { Label = "收货地址", Value = "上海市徐汇区漕溪北路 88 号 12 楼 1203 室", Span = 3 }),
        };
    }

    static DescriptionItem[] Person() =>
    [
        new() { Label = "姓名", Value = "周一航" },
        new() { Label = "部门", Value = "设计中心" },
        new() { Label = "职位", Value = "高级交互设计师" },
        new() { Label = "入职日期", Value = "2021-03-15" },
    ];

    static FrameworkElement Layouts() => new Flex
    {
        Direction = FlexDirection.Column, Gap = Space.Xl,
        Children =
        {
            Of(new Descriptions { Title = "Bordered：带边框的表格样式", Columns = 2, Bordered = true }, Person()),
            Of(new Descriptions { Title = "Layout=\"vertical\"：标签在上", Columns = 4, Layout = DescriptionsLayout.Vertical }, Person()),
            Of(new Descriptions { Title = "Size=\"sm\"", Columns = 2, Size = TextSize.Sm, Bordered = true }, Person()),
            Of(new Descriptions { Title = "Layout=\"inline\"：一行排开（页头里的关键信息）", Size = TextSize.Sm, Layout = DescriptionsLayout.Inline }, Person()),
        },
    };

    static DescriptionItem[] SupplierItems() =>
    [
        new() { Label = "供应商", Value = "华辰精密机械有限公司" },
        new() { Label = "信用代码", Value = "91310115MA1K4QXX2R", Copyable = true },
        new() { Label = "等级", Value = new Badge { Tone = StatusTone.Success, Content = "A 级" } },
        new() { Label = "联系人", Value = "林小满 · 138 0013 8000" },
        new() { Label = "开户行", Value = "招商银行上海张江支行" },
        new() { Label = "银行账号", Value = "6214 8301 2345 6789", Copyable = true, CopyText = "6214830123456789" },
        new() { Label = "地址", Value = "上海浦东张江高科园区 88 号", Copyable = true, CopyText = "上海市浦东新区张江高科技园区碧波路 88 号 3 号楼", Span = 3 },
    ];

    // 同一份详情放在窄的卡片与宽的区域里，各自按宽度排成一栏、两栏或三栏；桌面上改窗口宽度看栏数变化（TS 用可以拖宽的区域）
    static FrameworkElement Supplier() => new Flex
    {
        Gap = Space.Lg, Wrap = true, Align = FlexAlign.Start,
        Children =
        {
            new Card { Width = 20 * 16, Content = Of(new Descriptions { Title = "抽屉里（窄）", Columns = DescriptionsColumns.Auto, MinItemWidth = 220 }, SupplierItems()) },
            new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Xs, MinWidth = 16 * 16,
                Children =
                {
                    Of(new Descriptions { Title = "详情页（随窗口变宽）", Columns = DescriptionsColumns.Auto, MinItemWidth = 220 }, SupplierItems()),
                    new Small { Tone = TextTone.Muted, Text = "每栏不窄于 220，最多 4 栏；地址跨 3 栏，栏数少时按栏数封顶" },
                },
            },
        },
    };
}
