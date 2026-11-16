using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>FilterBar 页的示例（vue-tsx-pro 文档站 patterns/FilterBar）。上游 advanced 示例用 zod schema 生成条件与字段，桌面上没有 schema，直接写。</summary>
static class FilterBarExamples
{
    public static Example[] All =>
    [
        new("FilterBar", "tasks", "项目任务的查询", "条件是 Field。默认只显示一排：按钮在结束边，按剩下的宽度算出放得下几个条件（每个不窄于 MinFieldWidth，默认 14em），常用条件在前、More 在后，放得下的铺满这一排；其余的都收进“展开”，展开后从第二排起，列宽与第一排的每格相同。全部放得下时不出“展开”。比一格宽的条件（一排单选）包一层 GridItem Span=\"2\" 跨两格。Actions 是加在“重置”前面的按钮，End 接在“查询”后面（QueryTable 的工具栏按钮），Start 在条件前面（标题）。标签默认在起始边、宽度随文字（LabelWidth 默认随文字，条件多、要几行对齐时写固定宽度）。MoreActive 是收起的条件里已经填了几项，显示在“展开”旁边，免得忘了还有条件生效。在输入框里按回车就是 SearchRequested，IsLoading 时“查询”显示加载中。", Tasks),
        new("FilterBar", "advanced", "高级筛选", "ConditionFields 给出能组合的字段时，按钮区多一个“高级筛选”，展开后在查询栏下面用 ConditionBuilder 编辑 Condition，写出任一满足、介于、不包含这类固定条件写不出的组合。收起时有条件就写成一句话，带“清空”；“重置”同时把条件清成 null。条件是纯 JSON，交给后端或用 Matches 在本地筛。", Advanced),
    ];

    static readonly StatusMap Statuses = new()
    {
        ["pending"] = new("待启动", StatusTone.Neutral),
        ["designing"] = new("设计中", StatusTone.Info),
        ["verifying"] = new("测试中", StatusTone.Info),
        ["awaiting"] = new("待验收", StatusTone.Warning),
        ["closed"] = new("关闭", StatusTone.Success),
    };

    record ProjectTask(string Name, string Category, string Team, string Year, string Status, bool Config);

    static readonly ProjectTask[] ProjectTasks =
    [
        new("华东门店陈列改造", "产品改进", "研发中心", "2026", "verifying", true),
        new("仓库拣货流程优化", "流程优化", "产品部", "2026", "closed", false),
        new("包装纸箱第二供应商", "供应商切换", "运营部", "2026", "awaiting", false),
        new("会员小程序改版", "产品改进", "研发中心", "2027", "designing", true),
        new("仓库盘点流程优化", "流程优化", "产品部", "2027", "pending", false),
    ];

    static Control Tasks()
    {
        var keyword = new TextField { Icon = Lucide.Search, PlaceholderText = "名称或编码" };
        var category = new Select { ItemsSource = new[] { "产品改进", "供应商切换", "流程优化" }, Placeholder = "全部", Clearable = true };
        var status = new Select { ItemsSource = Statuses.Options(), SelectionMode = SelectionMode.Multiple, Placeholder = "全部", Clearable = true };
        var year = new DatePicker { Granularity = DateFieldGranularity.Year };
        var team = new Select { ItemsSource = new[] { "研发中心", "产品部", "运营部" }, Placeholder = "全部", Clearable = true };
        var config = new RadioGroup { Orientation = Orientation.Horizontal, Value = "any" };
        config.Items.Add(new Radio { Value = "any", Content = "不限" });
        config.Items.Add(new Radio { Value = "yes", Content = "是" });
        config.Items.Add(new Radio { Value = "no", Content = "否" });
        var bar = new FilterBar
        {
            Fields = { new Field { Label = "项目名称", Content = keyword }, new Field { Label = "任务类别", Content = category }, new Field { Label = "状态", Content = status } },
            // 一排单选比一格宽：跨两格，不折行
            More = { new Field { Label = "完成年度", Content = year }, new Field { Label = "责任团队", Content = team }, new GridItem { Span = 2, Child = new Field { Label = "涉构型更改", Content = config } } },
        };
        AutomationProperties.SetName(bar, "项目任务查询");
        var found = new Small { Tone = TextTone.Muted };
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };

        // 收起的条件里填了几项：完成年度、责任团队、是否涉构型更改
        void Count() => bar.MoreActive = new[] { !string.IsNullOrEmpty(year.Value), !string.IsNullOrEmpty(team.Value), config.Value != "any" }.Count(set => set);
        year.PropertyChanged += (_, e) => { if (e.Property == DatePicker.ValueProperty) Count(); };
        team.PropertyChanged += (_, e) => { if (e.Property == SelectBase.ValueProperty) Count(); };
        config.PropertyChanged += (_, e) => { if (e.Property == RadioGroup.ValueProperty) Count(); };

        void Show()
        {
            var text = keyword.Text ?? "";
            var statuses = status.Values ?? [];
            var results = ProjectTasks.Where(task => task.Name.Contains(text)
                                               && (string.IsNullOrEmpty(category.Value) || task.Category == category.Value)
                                               && (statuses.Count == 0 || statuses.Contains(task.Status))
                                               && (string.IsNullOrEmpty(year.Value) || task.Year == year.Value[..4])
                                               && (string.IsNullOrEmpty(team.Value) || task.Team == team.Value)
                                               && (config.Value == "any" || task.Config == (config.Value == "yes"))).ToList();
            found.Text = $"找到 {results.Count} 项";
            list.Children.Clear();
            foreach (var task in results)
                list.Children.Add(new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Sm,
                    Children = { new StatusBadge { Statuses = Statuses, Value = task.Status, Radius = Radius.Full }, new Small { Text = task.Name }, new Small { Tone = TextTone.Muted, Text = $"{task.Category} · {task.Team} · {task.Year}" } },
                });
        }
        async void Search()
        {
            bar.IsLoading = true;
            await Task.Delay(500);
            Show();
            bar.IsLoading = false;
        }
        bar.SearchRequested += (_, _) => Search();
        bar.ResetRequested += (_, _) =>
        {
            keyword.Text = "";
            category.Value = null;
            status.Values = [];
            year.Value = null;
            team.Value = null;
            config.Value = "any";
            Search();
        };
        Show();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children = { new Card { Content = bar }, new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { found, list } } },
        };
    }

    record Order(string Id, string Supplier, string Category, decimal Amount, bool Urgent, string Delivery);

    static readonly Order[] Orders =
    [
        new("PO-1031", "华东钢铁", "钢材", 128000, false, "2026-11-03"),
        new("PO-1032", "西南铝业", "铝材", 86500, true, "2026-11-12"),
        new("PO-1033", "华东钢铁", "钢材", 42000, false, "2026-10-28"),
        new("PO-1034", "精工紧固件", "紧固件", 6800, true, "2026-11-01"),
        new("PO-1035", "北方检测", "服务", 15000, false, "2026-12-15"),
        new("PO-1036", "西南铝业", "铝材", 213000, false, "2026-12-02"),
    ];

    static Control Advanced()
    {
        ConditionField[] fields =
        [
            new("amount", "金额", ConditionFieldType.Number),
            new("category", "品类", ConditionFieldType.Select) { Options = [.. new[] { "钢材", "铝材", "紧固件", "服务" }.Select(name => new ConditionOption(name, name))] },
            new("urgent", "加急", ConditionFieldType.Boolean),
            new("delivery", "交货日期", ConditionFieldType.Date),
        ];
        var keyword = new TextField { PlaceholderText = "订单号、供应商" };
        var bar = new FilterBar
        {
            Fields = { new Field { Label = "关键字", Content = keyword } },
            ConditionFields = fields,
            Condition = Condition.Any(new ConditionRule("amount", ConditionOp.Between, new object?[] { 50000, 150000 }), new ConditionRule("urgent", ConditionOp.Eq, true)),
        };
        AutomationProperties.SetName(bar, "订单查询");
        var count = new Small { Tone = TextTone.Muted };
        var table = new ColumnTable
        {
            Columns =
            [
                new("Id", "订单号") { Width = 100 },
                new("Supplier", "供应商") { Width = 120 },
                new("Category", "品类") { Width = 80 },
                new("Amount", "金额（元）") { Width = 120, Type = ColumnType.Number, Format = new NumericFormat { MaximumFractionDigits = 0 } },
                new("Urgent", "加急") { Width = 72, Value = row => ((Order)row).Urgent ? "是" : "" },
                new("Delivery", "交货日期") { Width = 120 },
            ],
        };
        AutomationProperties.SetName(table, "订单");
        // 点"查询"时用 Matches 在本地筛，接后端时把同一份条件交给接口
        void Search()
        {
            var text = (keyword.Text ?? "").Trim();
            var rows = Orders.Where(order => (text.Length == 0 || order.Id.Contains(text) || order.Supplier.Contains(text)) && (bar.Condition is not { } condition || condition.Matches(order))).ToList();
            count.Text = $"{rows.Count} / {Orders.Length} 张订单";
            table.ItemsSource = rows;
        }
        bar.SearchRequested += (_, _) => Search();
        bar.ResetRequested += (_, _) =>
        {
            keyword.Text = "";
            Search();
        };
        Search();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { bar, count, table } };
    }
}
